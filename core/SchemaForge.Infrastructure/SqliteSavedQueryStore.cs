using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;
using System.Text.Json;

namespace SchemaForge.Infrastructure;

public sealed class SqliteSavedQueryStore(string dbPath) : ISavedQueryStore
{
    public async Task<IReadOnlyCollection<SavedQuery>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<SavedQueryRow>(@"
select id, connection_id as ConnectionId, title, sql_text as Sql, tags_json as TagsJson, created_at_utc as CreatedAtUtc, updated_at_utc as UpdatedAtUtc
from app_saved_queries
where connection_id = @ConnectionId
order by updated_at_utc desc;", new { ConnectionId = connectionId.ToString() });

        return rows.Select(Map).ToArray();
    }

    public async Task<SavedQuery?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var row = await connection.QuerySingleOrDefaultAsync<SavedQueryRow>(@"
select id, connection_id as ConnectionId, title, sql_text as Sql, tags_json as TagsJson, created_at_utc as CreatedAtUtc, updated_at_utc as UpdatedAtUtc
from app_saved_queries
where id = @Id;", new { Id = id.ToString() });

        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(SavedQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        const string sql = @"
insert into app_saved_queries(id, connection_id, title, sql_text, tags_json, created_at_utc, updated_at_utc)
values(@Id, @ConnectionId, @Title, @Sql, @TagsJson, @CreatedAtUtc, @UpdatedAtUtc)
on conflict(id) do update set
    connection_id = excluded.connection_id,
    title = excluded.title,
    sql_text = excluded.sql_text,
    tags_json = excluded.tags_json,
    created_at_utc = excluded.created_at_utc,
    updated_at_utc = excluded.updated_at_utc;";

        await connection.ExecuteAsync(sql, new
        {
            Id = query.Id.ToString(),
            ConnectionId = query.ConnectionId.ToString(),
            query.Title,
            Sql = query.Sql,
            TagsJson = JsonSerializer.Serialize(query.Tags),
            CreatedAtUtc = query.CreatedAtUtc.UtcDateTime,
            UpdatedAtUtc = query.UpdatedAtUtc.UtcDateTime,
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        await connection.ExecuteAsync("delete from app_saved_queries where id = @Id", new { Id = id.ToString() });
    }

    private static SavedQuery Map(SavedQueryRow row)
    {
        return new SavedQuery
        {
            Id = Guid.Parse(row.Id),
            ConnectionId = Guid.Parse(row.ConnectionId),
            Title = row.Title,
            Sql = row.Sql,
            Tags = JsonSerializer.Deserialize<string[]>(row.TagsJson) ?? Array.Empty<string>(),
            CreatedAtUtc = DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc),
            UpdatedAtUtc = DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc),
        };
    }

    private static async Task EnsureSchemaAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        const string sql = @"
create table if not exists app_connections (
    id text primary key,
    name text not null,
    database_type text not null,
    host text not null,
    port integer not null,
    database_name text not null,
    username text not null,
    password text null
);

create table if not exists app_saved_queries (
    id text primary key,
    connection_id text not null,
    title text not null,
    sql_text text not null,
    tags_json text not null,
    created_at_utc text not null,
    updated_at_utc text not null,
    foreign key(connection_id) references app_connections(id)
);

create index if not exists idx_app_saved_queries_connection_id on app_saved_queries(connection_id);
create index if not exists idx_app_saved_queries_updated_at on app_saved_queries(updated_at_utc desc);";

        await connection.ExecuteAsync(sql);
    }

    private sealed record SavedQueryRow(
        string Id,
        string ConnectionId,
        string Title,
        string Sql,
        string TagsJson,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
