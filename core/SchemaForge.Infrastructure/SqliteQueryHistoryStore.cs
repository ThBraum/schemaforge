using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;

namespace SchemaForge.Infrastructure;

public sealed class SqliteQueryHistoryStore(string dbPath) : IQueryHistoryStore
{
    public async Task AddAsync(QueryHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        const string sql = @"
insert into app_query_history(id, connection_id, sql_text, status, duration_ms, error_message, executed_at_utc)
values(@Id, @ConnectionId, @Sql, @Status, @DurationMs, @ErrorMessage, @ExecutedAtUtc);";

        await connection.ExecuteAsync(sql, new
        {
            Id = entry.Id.ToString(),
            ConnectionId = entry.ConnectionId.ToString(),
            Sql = entry.Sql,
            Status = entry.Status == QueryExecutionStatus.Succeeded ? "succeeded" : "failed",
            entry.DurationMs,
            entry.ErrorMessage,
            ExecutedAtUtc = entry.ExecutedAtUtc.UtcDateTime,
        });
    }

    public async Task<IReadOnlyCollection<QueryHistoryEntry>> ListByConnectionAsync(Guid connectionId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<QueryHistoryRow>(@"
select id, connection_id as ConnectionId, sql_text as Sql, status, duration_ms as DurationMs, error_message as ErrorMessage, executed_at_utc as ExecutedAtUtc
from app_query_history
where connection_id = @ConnectionId
order by executed_at_utc desc
limit @Limit;", new
        {
            ConnectionId = connectionId.ToString(),
            Limit = Math.Clamp(limit, 1, 500),
        });

        return rows.Select(Map).ToArray();
    }

    private static QueryHistoryEntry Map(QueryHistoryRow row)
    {
        return new QueryHistoryEntry
        {
            Id = Guid.Parse(row.Id),
            ConnectionId = Guid.Parse(row.ConnectionId),
            Sql = row.Sql,
            Status = row.Status.ToLowerInvariant() == "failed" ? QueryExecutionStatus.Failed : QueryExecutionStatus.Succeeded,
            DurationMs = row.DurationMs,
            ErrorMessage = row.ErrorMessage,
            ExecutedAtUtc = DateTime.SpecifyKind(row.ExecutedAtUtc, DateTimeKind.Utc),
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

create table if not exists app_query_history (
    id text primary key,
    connection_id text not null,
    sql_text text not null,
    status text not null,
    duration_ms integer not null,
    error_message text null,
    executed_at_utc text not null,
    foreign key(connection_id) references app_connections(id)
);

create index if not exists idx_app_query_history_connection_id on app_query_history(connection_id);
create index if not exists idx_app_query_history_executed_at on app_query_history(executed_at_utc desc);";

        await connection.ExecuteAsync(sql);
    }

    private sealed record QueryHistoryRow(
        string Id,
        string ConnectionId,
        string Sql,
        string Status,
        long DurationMs,
        string? ErrorMessage,
        DateTime ExecutedAtUtc);
}
