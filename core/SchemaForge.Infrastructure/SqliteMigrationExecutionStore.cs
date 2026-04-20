using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;

namespace SchemaForge.Infrastructure;

public sealed class SqliteMigrationExecutionStore(string dbPath) : IMigrationExecutionStore
{
    public async Task AddAsync(MigrationExecutionRun run, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        const string sql = @"
insert into app_migration_runs(
    migration_run_id,
    migration_id,
    connection_id,
    executed_at_utc,
    direction,
    status,
    execution_log,
    duration_ms,
    error_message)
values(
    @MigrationRunId,
    @MigrationId,
    @ConnectionId,
    @ExecutedAtUtc,
    @Direction,
    @Status,
    @ExecutionLog,
    @DurationMs,
    @ErrorMessage);";

        await connection.ExecuteAsync(sql, new
        {
            MigrationRunId = run.MigrationRunId.ToString(),
            MigrationId = run.MigrationId.ToString(),
            ConnectionId = run.ConnectionId.ToString(),
            ExecutedAtUtc = run.ExecutedAtUtc.UtcDateTime,
            Direction = ToDirectionString(run.Direction),
            Status = ToStatusString(run.Status),
            run.ExecutionLog,
            run.DurationMs,
            run.ErrorMessage,
        });
    }

    public async Task<IReadOnlyCollection<MigrationExecutionRun>> ListByConnectionAsync(Guid connectionId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<MigrationExecutionRow>(@"
select migration_run_id as MigrationRunId,
       migration_id as MigrationId,
       connection_id as ConnectionId,
       executed_at_utc as ExecutedAtUtc,
           direction as Direction,
           status as Status,
       execution_log as ExecutionLog,
       duration_ms as DurationMs,
       error_message as ErrorMessage
from app_migration_runs
where connection_id = @ConnectionId
order by executed_at_utc desc
limit @Limit;", new
        {
            ConnectionId = connectionId.ToString(),
            Limit = Math.Clamp(limit, 1, 1000),
        });

        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyCollection<MigrationExecutionRun>> ListByMigrationAsync(Guid migrationId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<MigrationExecutionRow>(@"
select migration_run_id as MigrationRunId,
       migration_id as MigrationId,
       connection_id as ConnectionId,
       executed_at_utc as ExecutedAtUtc,
           direction as Direction,
           status as Status,
       execution_log as ExecutionLog,
       duration_ms as DurationMs,
       error_message as ErrorMessage
from app_migration_runs
where migration_id = @MigrationId
order by executed_at_utc desc
limit @Limit;", new
        {
            MigrationId = migrationId.ToString(),
            Limit = Math.Clamp(limit, 1, 1000),
        });

        return rows.Select(Map).ToArray();
    }

    private static MigrationExecutionRun Map(MigrationExecutionRow row)
    {
        return new MigrationExecutionRun
        {
            MigrationRunId = Guid.Parse(row.MigrationRunId),
            MigrationId = Guid.Parse(row.MigrationId),
            ConnectionId = Guid.Parse(row.ConnectionId),
            ExecutedAtUtc = ParseUtc(row.ExecutedAtUtc),
            Direction = ParseDirection(row.Direction),
            Status = ParseStatus(row.Status),
            ExecutionLog = row.ExecutionLog,
            DurationMs = row.DurationMs,
            ErrorMessage = row.ErrorMessage,
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
create table if not exists app_migration_runs (
    migration_run_id text primary key,
    migration_id text not null,
    connection_id text not null,
    executed_at_utc text not null,
    direction text not null,
    status text not null,
    execution_log text null,
    duration_ms integer not null,
    error_message text null,
    foreign key(connection_id) references app_connections(id),
    foreign key(migration_id) references app_migrations(id)
);

create index if not exists idx_app_migration_runs_connection_id on app_migration_runs(connection_id);
create index if not exists idx_app_migration_runs_migration_id on app_migration_runs(migration_id);
create index if not exists idx_app_migration_runs_executed_at on app_migration_runs(executed_at_utc desc);";

        await connection.ExecuteAsync(sql);
    }

    private static string ToDirectionString(MigrationDirection direction)
    {
        return direction switch
        {
            MigrationDirection.Up => "up",
            MigrationDirection.Down => "down",
            _ => throw new InvalidOperationException($"Unsupported migration direction '{direction}'."),
        };
    }

    private static MigrationDirection ParseDirection(string direction)
    {
        return direction.Trim().ToLowerInvariant() switch
        {
            "up" => MigrationDirection.Up,
            "down" => MigrationDirection.Down,
            _ => throw new InvalidOperationException($"Unexpected migration direction '{direction}'."),
        };
    }

    private static string ToStatusString(MigrationExecutionStatus status)
    {
        return status switch
        {
            MigrationExecutionStatus.Succeeded => "succeeded",
            MigrationExecutionStatus.Failed => "failed",
            _ => throw new InvalidOperationException($"Unsupported migration execution status '{status}'."),
        };
    }

    private static MigrationExecutionStatus ParseStatus(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "succeeded" => MigrationExecutionStatus.Succeeded,
            "failed" => MigrationExecutionStatus.Failed,
            _ => throw new InvalidOperationException($"Unexpected migration execution status '{status}'."),
        };
    }

    private sealed class MigrationExecutionRow
    {
        public string MigrationRunId { get; init; } = string.Empty;
        public string MigrationId { get; init; } = string.Empty;
        public string ConnectionId { get; init; } = string.Empty;
        public string ExecutedAtUtc { get; init; } = string.Empty;
        public string Direction { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? ExecutionLog { get; init; }
        public long DurationMs { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
