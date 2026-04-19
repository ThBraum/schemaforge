using SchemaForge.Application;
using SchemaForge.Domain;

namespace SchemaForge.Infrastructure;

public sealed class DatabaseProviderResolver(IEnumerable<IDatabaseProvider> providers) : IDatabaseProviderResolver
{
    private readonly IReadOnlyDictionary<DatabaseType, IDatabaseProvider> _providers = providers.ToDictionary(x => x.DatabaseType);

    public IDatabaseProvider Resolve(DatabaseType databaseType)
    {
        if (_providers.TryGetValue(databaseType, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Provider not found for {databaseType}.");
    }
}
