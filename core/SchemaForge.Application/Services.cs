using SchemaForge.Domain;
using System.Diagnostics;
using System.Globalization;

namespace SchemaForge.Application;

public sealed class ExplorerService(
    IConnectionStore connectionStore,
    IDatabaseProviderResolver providerResolver,
    IQueryHistoryStore queryHistoryStore)
{
    public async Task<SchemaSummary> GetSchemaAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        var provider = providerResolver.Resolve(connection.DatabaseType);
        return await provider.GetSchemaAsync(connection, cancellationToken);
    }

    public async Task<TablePreview> PreviewTableAsync(Guid connectionId, string schemaName, string tableName, int limit, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        var provider = providerResolver.Resolve(connection.DatabaseType);
        return await provider.PreviewTableAsync(connection, schemaName, tableName, limit, cancellationToken);
    }

    public async Task<QueryResult> RunQueryAsync(Guid connectionId, string sql, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        var provider = providerResolver.Resolve(connection.DatabaseType);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await provider.RunQueryAsync(connection, sql, cancellationToken);
            stopwatch.Stop();

            await queryHistoryStore.AddAsync(new QueryHistoryEntry
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                Sql = sql,
                Status = QueryExecutionStatus.Succeeded,
                DurationMs = result.DurationMs,
                ExecutedAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);

            return result;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            await queryHistoryStore.AddAsync(new QueryHistoryEntry
            {
                Id = Guid.NewGuid(),
                ConnectionId = connectionId,
                Sql = sql,
                Status = QueryExecutionStatus.Failed,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ErrorMessage = exception.Message,
                ExecutedAtUtc = DateTimeOffset.UtcNow,
            }, cancellationToken);

            throw;
        }
    }

    private async Task<SavedConnection> GetConnectionOrThrowAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await connectionStore.GetAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("Connection not found.");
        }

        return connection;
    }
}

public sealed class SavedQueryService(ISavedQueryStore savedQueryStore, IConnectionStore connectionStore)
{
    public Task<IReadOnlyCollection<SavedQuery>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        return savedQueryStore.ListByConnectionAsync(connectionId, cancellationToken);
    }

    public async Task<SavedQuery> UpsertAsync(Guid? id, Guid connectionId, string title, string sql, IReadOnlyCollection<string>? tags, CancellationToken cancellationToken = default)
    {
        _ = await GetConnectionOrThrowAsync(connectionId, cancellationToken);

        var existing = id.HasValue ? await savedQueryStore.GetAsync(id.Value, cancellationToken) : null;
        var normalizedTags = (tags ?? Array.Empty<string>())
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var now = DateTimeOffset.UtcNow;
        var entity = new SavedQuery
        {
            Id = existing?.Id ?? id ?? Guid.NewGuid(),
            ConnectionId = connectionId,
            Title = title.Trim(),
            Sql = sql,
            Tags = normalizedTags,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now,
        };

        await savedQueryStore.SaveAsync(entity, cancellationToken);
        return entity;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return savedQueryStore.DeleteAsync(id, cancellationToken);
    }

    private async Task<SavedConnection> GetConnectionOrThrowAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await connectionStore.GetAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("Connection not found.");
        }

        return connection;
    }
}

public sealed class QueryHistoryService(IQueryHistoryStore queryHistoryStore)
{
    public Task<IReadOnlyCollection<QueryHistoryEntry>> ListByConnectionAsync(Guid connectionId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return queryHistoryStore.ListByConnectionAsync(connectionId, limit, cancellationToken);
    }
}

public sealed class SchemaSnapshotService(
    IConnectionStore connectionStore,
    ISchemaSnapshotStore snapshotStore,
    IDatabaseProviderResolver providerResolver)
{
    public async Task<SchemaSnapshot> CaptureAsync(Guid connectionId, string? name, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        var provider = providerResolver.Resolve(connection.DatabaseType);
        var structure = await provider.CaptureSnapshotStructureAsync(connection, cancellationToken);

        var snapshot = new SchemaSnapshot
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            Name = string.IsNullOrWhiteSpace(name)
                ? $"snapshot-{DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}"
                : name.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Structure = structure,
        };

        await snapshotStore.SaveAsync(snapshot, cancellationToken);
        return snapshot;
    }

    public Task<IReadOnlyCollection<SchemaSnapshot>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        return snapshotStore.ListByConnectionAsync(connectionId, cancellationToken);
    }

    public async Task<SchemaSnapshot> GetByIdOrThrowAsync(Guid snapshotId, CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshotStore.GetAsync(snapshotId, cancellationToken);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Snapshot not found.");
        }

        return snapshot;
    }

    private async Task<SavedConnection> GetConnectionOrThrowAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var connection = await connectionStore.GetAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("Connection not found.");
        }

        return connection;
    }
}
