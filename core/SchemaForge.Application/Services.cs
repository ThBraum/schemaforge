using SchemaForge.Domain;

namespace SchemaForge.Application;

public sealed class ExplorerService(
    IConnectionStore connectionStore,
    IDatabaseProviderResolver providerResolver)
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
        return await provider.RunQueryAsync(connection, sql, cancellationToken);
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
