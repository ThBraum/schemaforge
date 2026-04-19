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
}

public interface IDatabaseProviderResolver
{
    IDatabaseProvider Resolve(DatabaseType databaseType);
}
