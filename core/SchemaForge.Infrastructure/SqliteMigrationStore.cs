using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;

namespace SchemaForge.Infrastructure;

public sealed class SqliteMigrationStore(string dbPath) : IMigrationStore
{
    public async Task<IReadOnlyCollection<MigrationDefinition>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<MigrationRow>(@"
    select id as Id,
       connection_id as ConnectionId,
       migration_name as Name,
       migration_description as Description,
           up_script as UpScript,
           down_script as DownScript,
           status as Status,
           checksum as Checksum,
       source_snapshot_id as SourceSnapshotId,
       target_snapshot_id as TargetSnapshotId,
       created_at_utc as CreatedAtUtc,
       updated_at_utc as UpdatedAtUtc
from app_migrations
where connection_id = @ConnectionId
order by created_at_utc asc;", new { ConnectionId = connectionId.ToString() });

        return rows.Select(Map).ToArray();
    }

    public async Task<MigrationDefinition?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var row = await connection.QuerySingleOrDefaultAsync<MigrationRow>(@"
    select id as Id,
       connection_id as ConnectionId,
       migration_name as Name,
       migration_description as Description,
           up_script as UpScript,
           down_script as DownScript,
           status as Status,
           checksum as Checksum,
       source_snapshot_id as SourceSnapshotId,
       target_snapshot_id as TargetSnapshotId,
       created_at_utc as CreatedAtUtc,
       updated_at_utc as UpdatedAtUtc
from app_migrations
where id = @Id;", new { Id = id.ToString() });

        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(MigrationDefinition migration, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        const string sql = @"
insert into app_migrations(
    id,
    connection_id,
    migration_name,
    migration_description,
    up_script,
    down_script,
    status,
    checksum,
    source_snapshot_id,
    target_snapshot_id,
    created_at_utc,
    updated_at_utc)
values(
    @Id,
    @ConnectionId,
    @Name,
    @Description,
    @UpScript,
    @DownScript,
    @Status,
    @Checksum,
    @SourceSnapshotId,
    @TargetSnapshotId,
    @CreatedAtUtc,
    @UpdatedAtUtc)
on conflict(id) do update set
    connection_id = excluded.connection_id,
    migration_name = excluded.migration_name,
    migration_description = excluded.migration_description,
    up_script = excluded.up_script,
    down_script = excluded.down_script,
    status = excluded.status,
    checksum = excluded.checksum,
    source_snapshot_id = excluded.source_snapshot_id,
    target_snapshot_id = excluded.target_snapshot_id,
    created_at_utc = excluded.created_at_utc,
    updated_at_utc = excluded.updated_at_utc;";

        await connection.ExecuteAsync(sql, new
        {
            Id = migration.Id.ToString(),
            ConnectionId = migration.ConnectionId.ToString(),
            migration.Name,
            Description = migration.Description,
            migration.UpScript,
            migration.DownScript,
            Status = ToStatusString(migration.Status),
            migration.Checksum,
            SourceSnapshotId = migration.SourceSnapshotId?.ToString(),
            TargetSnapshotId = migration.TargetSnapshotId?.ToString(),
            CreatedAtUtc = migration.CreatedAtUtc.UtcDateTime,
            UpdatedAtUtc = migration.UpdatedAtUtc.UtcDateTime,
        });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        await connection.ExecuteAsync("delete from app_migrations where id = @Id", new { Id = id.ToString() });
    }

    private static MigrationDefinition Map(MigrationRow row)
    {
        return new MigrationDefinition
        {
            Id = Guid.Parse(row.Id),
            ConnectionId = Guid.Parse(row.ConnectionId),
            Name = row.Name,
            Description = row.Description,
            UpScript = row.UpScript,
            DownScript = row.DownScript,
            Status = ParseStatus(row.Status),
            Checksum = row.Checksum,
            SourceSnapshotId = string.IsNullOrWhiteSpace(row.SourceSnapshotId) ? null : Guid.Parse(row.SourceSnapshotId),
            TargetSnapshotId = string.IsNullOrWhiteSpace(row.TargetSnapshotId) ? null : Guid.Parse(row.TargetSnapshotId),
            CreatedAtUtc = ParseUtc(row.CreatedAtUtc),
            UpdatedAtUtc = ParseUtc(row.UpdatedAtUtc),
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
create table if not exists app_migrations (
    id text primary key,
    connection_id text not null,
    migration_name text not null,
    migration_description text null,
    up_script text not null,
    down_script text not null,
    status text not null,
    checksum text null,
    source_snapshot_id text null,
    target_snapshot_id text null,
    created_at_utc text not null,
    updated_at_utc text not null,
    foreign key(connection_id) references app_connections(id)
);

create index if not exists idx_app_migrations_connection_id on app_migrations(connection_id);
create index if not exists idx_app_migrations_status on app_migrations(status);
create index if not exists idx_app_migrations_created_at on app_migrations(created_at_utc asc);";

        await connection.ExecuteAsync(sql);
    }

    private static string ToStatusString(MigrationStatus status)
    {
        return status switch
        {
            MigrationStatus.Draft => "draft",
            MigrationStatus.Pending => "pending",
            MigrationStatus.Applied => "applied",
            MigrationStatus.Failed => "failed",
            MigrationStatus.RolledBack => "rolledback",
            _ => throw new InvalidOperationException($"Unsupported migration status '{status}'."),
        };
    }

    private static MigrationStatus ParseStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "draft" => MigrationStatus.Draft,
            "pending" => MigrationStatus.Pending,
            "applied" => MigrationStatus.Applied,
            "failed" => MigrationStatus.Failed,
            "rolledback" => MigrationStatus.RolledBack,
            _ => throw new InvalidOperationException($"Unexpected migration status '{status}'."),
        };
    }

    private sealed class MigrationRow
    {
        public string Id { get; init; } = string.Empty;
        public string ConnectionId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string UpScript { get; init; } = string.Empty;
        public string DownScript { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? Checksum { get; init; }
        public string? SourceSnapshotId { get; init; }
        public string? TargetSnapshotId { get; init; }
        public string CreatedAtUtc { get; init; } = string.Empty;
        public string UpdatedAtUtc { get; init; } = string.Empty;
    }
}
