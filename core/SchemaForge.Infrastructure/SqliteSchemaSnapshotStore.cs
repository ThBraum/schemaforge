using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;
using System.Text.Json;

namespace SchemaForge.Infrastructure;

public sealed class SqliteSchemaSnapshotStore(string dbPath) : ISchemaSnapshotStore
{
    public async Task SaveAsync(SchemaSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        const string sql = @"
insert into app_schema_snapshots(id, connection_id, snapshot_name, structure_json, created_at_utc)
values(@Id, @ConnectionId, @Name, @StructureJson, @CreatedAtUtc)
on conflict(id) do update set
    connection_id = excluded.connection_id,
    snapshot_name = excluded.snapshot_name,
    structure_json = excluded.structure_json,
    created_at_utc = excluded.created_at_utc;";

        await connection.ExecuteAsync(sql, new
        {
            Id = snapshot.Id.ToString(),
            ConnectionId = snapshot.ConnectionId.ToString(),
            Name = snapshot.Name,
            StructureJson = JsonSerializer.Serialize(snapshot.Structure),
            CreatedAtUtc = snapshot.CreatedAtUtc.UtcDateTime,
        });
    }

    public async Task<SchemaSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var row = await connection.QuerySingleOrDefaultAsync<SchemaSnapshotRow>(@"
    select id as Id,
           connection_id as ConnectionId,
           snapshot_name as Name,
           structure_json as StructureJson,
           created_at_utc as CreatedAtUtc
from app_schema_snapshots
where id = @Id;", new { Id = id.ToString() });

        return row is null ? null : Map(row);
    }

    public async Task<IReadOnlyCollection<SchemaSnapshot>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<SchemaSnapshotRow>(@"
    select id as Id,
           connection_id as ConnectionId,
           snapshot_name as Name,
           structure_json as StructureJson,
           created_at_utc as CreatedAtUtc
from app_schema_snapshots
where connection_id = @ConnectionId
order by created_at_utc desc;", new { ConnectionId = connectionId.ToString() });

        return rows.Select(Map).ToArray();
    }

    private static SchemaSnapshot Map(SchemaSnapshotRow row)
    {
        var structure = JsonSerializer.Deserialize<DatabaseStructureSnapshot>(row.StructureJson)
            ?? throw new InvalidOperationException("Invalid snapshot structure payload.");

        return new SchemaSnapshot
        {
            Id = Guid.Parse(row.Id),
            ConnectionId = Guid.Parse(row.ConnectionId),
            Name = row.Name,
            CreatedAtUtc = ParseUtc(row.CreatedAtUtc),
            Structure = structure,
        };
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        return DateTimeOffset.TryParse(
            value,
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid UTC timestamp '{value}'.");
    }

    private static async Task EnsureSchemaAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        await SqliteLocalSchema.EnsureConnectionsTableAsync(connection);

        const string sql = @"
create table if not exists app_schema_snapshots (
    id text primary key,
    connection_id text not null,
    snapshot_name text null,
    structure_json text not null,
    created_at_utc text not null,
    foreign key(connection_id) references app_connections(id)
);

create index if not exists idx_app_schema_snapshots_connection_id on app_schema_snapshots(connection_id);
create index if not exists idx_app_schema_snapshots_created_at on app_schema_snapshots(created_at_utc desc);";

        await connection.ExecuteAsync(sql);
    }

    private sealed class SchemaSnapshotRow
    {
        public string Id { get; init; } = string.Empty;
        public string ConnectionId { get; init; } = string.Empty;
        public string? Name { get; init; }
        public string StructureJson { get; init; } = string.Empty;
        public string CreatedAtUtc { get; init; } = string.Empty;
    }
}
