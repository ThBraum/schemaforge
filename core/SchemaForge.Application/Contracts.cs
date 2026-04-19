using SchemaForge.Domain;

namespace SchemaForge.Application;

public interface IConnectionStore
{
    Task<IReadOnlyCollection<SavedConnection>> ListAsync(CancellationToken cancellationToken = default);
    Task<SavedConnection?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(SavedConnection connection, CancellationToken cancellationToken = default);
}

public interface IDatabaseProvider
{
    DatabaseType DatabaseType { get; }
    Task<SchemaSummary> GetSchemaAsync(SavedConnection connection, CancellationToken cancellationToken = default);
    Task<TablePreview> PreviewTableAsync(SavedConnection connection, string schemaName, string tableName, int limit, CancellationToken cancellationToken = default);
    Task<QueryResult> RunQueryAsync(SavedConnection connection, string sql, CancellationToken cancellationToken = default);
    Task<DatabaseStructureSnapshot> CaptureSnapshotStructureAsync(SavedConnection connection, CancellationToken cancellationToken = default);
}

public interface IDatabaseProviderResolver
{
    IDatabaseProvider Resolve(DatabaseType databaseType);
}

public interface ISavedQueryStore
{
    Task<IReadOnlyCollection<SavedQuery>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<SavedQuery?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveAsync(SavedQuery query, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IQueryHistoryStore
{
    Task AddAsync(QueryHistoryEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<QueryHistoryEntry>> ListByConnectionAsync(Guid connectionId, int limit, CancellationToken cancellationToken = default);
}

public interface ISchemaSnapshotStore
{
    Task SaveAsync(SchemaSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<SchemaSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SchemaSnapshot>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
}
