using SchemaForge.Domain;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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

            await TryAddQueryHistoryAsync(new QueryHistoryEntry
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

            await TryAddQueryHistoryAsync(new QueryHistoryEntry
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

    private async Task TryAddQueryHistoryAsync(QueryHistoryEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await queryHistoryStore.AddAsync(entry, cancellationToken);
        }
        catch (Exception exception)
        {
            Trace.TraceError(
                "Failed to record query history for connection '{0}' at '{1:O}'. Status: {2}. Error: {3}",
                entry.ConnectionId,
                entry.ExecutedAtUtc,
                entry.Status,
                exception);
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
        if (existing is not null && existing.ConnectionId != connectionId)
        {
            throw new InvalidOperationException("Saved query does not belong to the provided connection.");
        }

        var normalizedTitle = title.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            throw new ArgumentException("Title cannot be empty or whitespace.", nameof(title));
        }

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
            Title = normalizedTitle,
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

public sealed class SchemaDiffService(
    ISchemaSnapshotStore snapshotStore,
    ISchemaDiffEngine diffEngine)
{
    public async Task<SchemaDiffResult> CompareAsync(Guid sourceSnapshotId, Guid targetSnapshotId, CancellationToken cancellationToken = default)
    {
        if (sourceSnapshotId == targetSnapshotId)
        {
            throw new ArgumentException("Source and target snapshots must be different.");
        }

        var sourceSnapshot = await snapshotStore.GetAsync(sourceSnapshotId, cancellationToken)
            ?? throw new InvalidOperationException("Source snapshot not found.");
        var targetSnapshot = await snapshotStore.GetAsync(targetSnapshotId, cancellationToken)
            ?? throw new InvalidOperationException("Target snapshot not found.");

        if (sourceSnapshot.ConnectionId != targetSnapshot.ConnectionId)
        {
            throw new InvalidOperationException("Snapshots must belong to the same connection.");
        }

        return diffEngine.Compare(sourceSnapshot, targetSnapshot);
    }
}

public sealed class SchemaDiffEngine : ISchemaDiffEngine
{
    public SchemaDiffResult Compare(SchemaSnapshot sourceSnapshot, SchemaSnapshot targetSnapshot)
    {
        var sourceTables = FlattenTables(sourceSnapshot.Structure);
        var targetTables = FlattenTables(targetSnapshot.Structure);

        var sourceKeys = sourceTables.Keys.ToHashSet(TableKeyComparer.Instance);
        var targetKeys = targetTables.Keys.ToHashSet(TableKeyComparer.Instance);

        var addedKeys = targetKeys
            .Except(sourceKeys, TableKeyComparer.Instance)
            .OrderBy(key => key.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key.TableName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removedKeys = sourceKeys
            .Except(targetKeys, TableKeyComparer.Instance)
            .OrderBy(key => key.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key.TableName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var commonKeys = sourceKeys
            .Intersect(targetKeys, TableKeyComparer.Instance)
            .OrderBy(key => key.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key.TableName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tablesAdded = addedKeys.Select(ToTableEntry).ToArray();
        var tablesRemoved = removedKeys.Select(ToTableEntry).ToArray();

        var columnsAdded = new List<ColumnDiffEntry>();
        var columnsRemoved = new List<ColumnDiffEntry>();
        var columnsModified = new List<ColumnModificationDiff>();
        var primaryKeysAdded = new List<PrimaryKeyDiffEntry>();
        var primaryKeysRemoved = new List<PrimaryKeyDiffEntry>();
        var foreignKeysAdded = new List<ForeignKeyDiffEntry>();
        var foreignKeysRemoved = new List<ForeignKeyDiffEntry>();
        var indexesAdded = new List<IndexDiffEntry>();
        var indexesRemoved = new List<IndexDiffEntry>();
        var breakingChanges = new List<BreakingChangeEntry>();
        var tablesModified = new List<TableModificationDiff>();

        foreach (var tableKey in commonKeys)
        {
            var sourceTable = sourceTables[tableKey];
            var targetTable = targetTables[tableKey];

            var sourceColumns = sourceTable.Columns
                .ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);
            var targetColumns = targetTable.Columns
                .ToDictionary(column => column.Name, StringComparer.OrdinalIgnoreCase);

            var sourceColumnNames = sourceColumns.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var targetColumnNames = targetColumns.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var tableColumnsAdded = targetColumnNames
                .Except(sourceColumnNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => ToColumnEntry(tableKey, targetColumns[name]))
                .ToArray();

            var tableColumnsRemoved = sourceColumnNames
                .Except(targetColumnNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => ToColumnEntry(tableKey, sourceColumns[name]))
                .ToArray();

            var tableColumnsModified = sourceColumnNames
                .Intersect(targetColumnNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => BuildColumnModification(tableKey, sourceColumns[name], targetColumns[name]))
                .Where(item => item is not null)
                .Cast<ColumnModificationDiff>()
                .ToArray();

            var sourcePrimaryKey = NormalizeOrdered(sourceTable.PrimaryKeyColumns);
            var targetPrimaryKey = NormalizeOrdered(targetTable.PrimaryKeyColumns);
            var primaryKeyChanged = !SequenceEquals(sourcePrimaryKey, targetPrimaryKey);

            PrimaryKeyDiffEntry? primaryKeyAdded = null;
            PrimaryKeyDiffEntry? primaryKeyRemoved = null;

            if (primaryKeyChanged)
            {
                if (sourcePrimaryKey.Count > 0)
                {
                    primaryKeyRemoved = new PrimaryKeyDiffEntry
                    {
                        SchemaName = tableKey.SchemaName,
                        TableName = tableKey.TableName,
                        ColumnNames = sourcePrimaryKey,
                    };
                }

                if (targetPrimaryKey.Count > 0)
                {
                    primaryKeyAdded = new PrimaryKeyDiffEntry
                    {
                        SchemaName = tableKey.SchemaName,
                        TableName = tableKey.TableName,
                        ColumnNames = targetPrimaryKey,
                    };
                }
            }

            var sourceForeignKeys = sourceTable.ForeignKeys
                .ToDictionary(ToForeignKeySignature, item => item, StringComparer.OrdinalIgnoreCase);
            var targetForeignKeys = targetTable.ForeignKeys
                .ToDictionary(ToForeignKeySignature, item => item, StringComparer.OrdinalIgnoreCase);

            var tableForeignKeysAdded = targetForeignKeys.Keys
                .Except(sourceForeignKeys.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(key => ToForeignKeyDiff(tableKey, targetForeignKeys[key]))
                .ToArray();

            var tableForeignKeysRemoved = sourceForeignKeys.Keys
                .Except(targetForeignKeys.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(key => ToForeignKeyDiff(tableKey, sourceForeignKeys[key]))
                .ToArray();

            var sourceIndexes = sourceTable.Indexes
                .ToDictionary(ToIndexSignature, item => item, StringComparer.OrdinalIgnoreCase);
            var targetIndexes = targetTable.Indexes
                .ToDictionary(ToIndexSignature, item => item, StringComparer.OrdinalIgnoreCase);

            var tableIndexesAdded = targetIndexes.Keys
                .Except(sourceIndexes.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(key => ToIndexDiff(tableKey, targetIndexes[key]))
                .ToArray();

            var tableIndexesRemoved = sourceIndexes.Keys
                .Except(targetIndexes.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(key => ToIndexDiff(tableKey, sourceIndexes[key]))
                .ToArray();

            columnsAdded.AddRange(tableColumnsAdded);
            columnsRemoved.AddRange(tableColumnsRemoved);
            columnsModified.AddRange(tableColumnsModified);

            if (primaryKeyAdded is not null)
            {
                primaryKeysAdded.Add(primaryKeyAdded);
            }

            if (primaryKeyRemoved is not null)
            {
                primaryKeysRemoved.Add(primaryKeyRemoved);
            }

            foreignKeysAdded.AddRange(tableForeignKeysAdded);
            foreignKeysRemoved.AddRange(tableForeignKeysRemoved);
            indexesAdded.AddRange(tableIndexesAdded);
            indexesRemoved.AddRange(tableIndexesRemoved);

            foreach (var item in tableColumnsRemoved)
            {
                breakingChanges.Add(new BreakingChangeEntry
                {
                    Category = "column_removed",
                    SchemaName = item.SchemaName,
                    TableName = item.TableName,
                    ColumnName = item.ColumnName,
                    Description = $"Column '{item.ColumnName}' was removed.",
                });
            }

            foreach (var item in tableColumnsModified.Where(column => column.IsBreakingChange))
            {
                var reasons = new List<string>();
                if (item.DataTypeChanged)
                {
                    reasons.Add($"type changed from '{item.SourceDataType}' to '{item.TargetDataType}'");
                }

                if (item.NullabilityChanged && item.SourceIsNullable && !item.TargetIsNullable)
                {
                    reasons.Add("column became NOT NULL");
                }

                breakingChanges.Add(new BreakingChangeEntry
                {
                    Category = "column_modified",
                    SchemaName = item.SchemaName,
                    TableName = item.TableName,
                    ColumnName = item.ColumnName,
                    Description = $"Column '{item.ColumnName}' changed: {string.Join(", ", reasons)}.",
                });
            }

            if (primaryKeyChanged && sourcePrimaryKey.Count > 0)
            {
                breakingChanges.Add(new BreakingChangeEntry
                {
                    Category = "primary_key_changed",
                    SchemaName = tableKey.SchemaName,
                    TableName = tableKey.TableName,
                    Description = $"Primary key changed from ({string.Join(", ", sourcePrimaryKey)}) to ({string.Join(", ", targetPrimaryKey)}).",
                });
            }

            var hasChanges = tableColumnsAdded.Length > 0
                || tableColumnsRemoved.Length > 0
                || tableColumnsModified.Length > 0
                || primaryKeyChanged
                || tableForeignKeysAdded.Length > 0
                || tableForeignKeysRemoved.Length > 0
                || tableIndexesAdded.Length > 0
                || tableIndexesRemoved.Length > 0;

            if (!hasChanges)
            {
                continue;
            }

            tablesModified.Add(new TableModificationDiff
            {
                SchemaName = tableKey.SchemaName,
                TableName = tableKey.TableName,
                ColumnsAdded = tableColumnsAdded,
                ColumnsRemoved = tableColumnsRemoved,
                ColumnsModified = tableColumnsModified,
                PrimaryKeyChanged = primaryKeyChanged,
                SourcePrimaryKeyColumns = sourcePrimaryKey,
                TargetPrimaryKeyColumns = targetPrimaryKey,
                ForeignKeysAdded = tableForeignKeysAdded,
                ForeignKeysRemoved = tableForeignKeysRemoved,
                IndexesAdded = tableIndexesAdded,
                IndexesRemoved = tableIndexesRemoved,
            });
        }

        foreach (var tableKey in removedKeys)
        {
            breakingChanges.Add(new BreakingChangeEntry
            {
                Category = "table_removed",
                SchemaName = tableKey.SchemaName,
                TableName = tableKey.TableName,
                Description = $"Table '{tableKey.SchemaName}.{tableKey.TableName}' was removed.",
            });
        }

        var summary = new SchemaDiffSummary
        {
            TablesAdded = tablesAdded.Length,
            TablesRemoved = tablesRemoved.Length,
            TablesModified = tablesModified.Count,
            ColumnsAdded = columnsAdded.Count,
            ColumnsRemoved = columnsRemoved.Count,
            ColumnsModified = columnsModified.Count,
            PrimaryKeysAdded = primaryKeysAdded.Count,
            PrimaryKeysRemoved = primaryKeysRemoved.Count,
            ForeignKeysAdded = foreignKeysAdded.Count,
            ForeignKeysRemoved = foreignKeysRemoved.Count,
            IndexesAdded = indexesAdded.Count,
            IndexesRemoved = indexesRemoved.Count,
            BreakingChanges = breakingChanges.Count,
        };

        return new SchemaDiffResult
        {
            ConnectionId = sourceSnapshot.ConnectionId,
            SourceSnapshotId = sourceSnapshot.Id,
            TargetSnapshotId = targetSnapshot.Id,
            SourceSnapshotName = sourceSnapshot.Name ?? sourceSnapshot.Id.ToString(),
            TargetSnapshotName = targetSnapshot.Name ?? targetSnapshot.Id.ToString(),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Summary = summary,
            TablesAdded = tablesAdded,
            TablesRemoved = tablesRemoved,
            TablesModified = tablesModified.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ToArray(),
            ColumnsAdded = columnsAdded.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.ColumnName).ToArray(),
            ColumnsRemoved = columnsRemoved.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.ColumnName).ToArray(),
            ColumnsModified = columnsModified.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.ColumnName).ToArray(),
            PrimaryKeysAdded = primaryKeysAdded.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ToArray(),
            PrimaryKeysRemoved = primaryKeysRemoved.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ToArray(),
            ForeignKeysAdded = foreignKeysAdded.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.Name).ToArray(),
            ForeignKeysRemoved = foreignKeysRemoved.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.Name).ToArray(),
            IndexesAdded = indexesAdded.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.Name).ToArray(),
            IndexesRemoved = indexesRemoved.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.Name).ToArray(),
            BreakingChanges = breakingChanges.OrderBy(item => item.SchemaName).ThenBy(item => item.TableName).ThenBy(item => item.ColumnName).ToArray(),
        };
    }

    private static Dictionary<TableKey, TableSnapshotNode> FlattenTables(DatabaseStructureSnapshot structure)
    {
        return structure.Schemas
            .SelectMany(schema => schema.Tables)
            .ToDictionary(
                table => new TableKey(table.SchemaName, table.TableName),
                table => table,
                TableKeyComparer.Instance);
    }

    private static TableDiffEntry ToTableEntry(TableKey key)
    {
        return new TableDiffEntry
        {
            SchemaName = key.SchemaName,
            TableName = key.TableName,
        };
    }

    private static ColumnDiffEntry ToColumnEntry(TableKey key, ColumnSnapshotNode column)
    {
        return new ColumnDiffEntry
        {
            SchemaName = key.SchemaName,
            TableName = key.TableName,
            ColumnName = column.Name,
            DataType = column.DataType,
            IsNullable = column.IsNullable,
        };
    }

    private static ColumnModificationDiff? BuildColumnModification(TableKey key, ColumnSnapshotNode source, ColumnSnapshotNode target)
    {
        var dataTypeChanged = !string.Equals(source.DataType.Trim(), target.DataType.Trim(), StringComparison.OrdinalIgnoreCase);
        var nullabilityChanged = source.IsNullable != target.IsNullable;

        if (!dataTypeChanged && !nullabilityChanged)
        {
            return null;
        }

        var isBreakingChange = dataTypeChanged || (source.IsNullable && !target.IsNullable);

        return new ColumnModificationDiff
        {
            SchemaName = key.SchemaName,
            TableName = key.TableName,
            ColumnName = source.Name,
            SourceDataType = source.DataType,
            TargetDataType = target.DataType,
            SourceIsNullable = source.IsNullable,
            TargetIsNullable = target.IsNullable,
            DataTypeChanged = dataTypeChanged,
            NullabilityChanged = nullabilityChanged,
            IsBreakingChange = isBreakingChange,
        };
    }

    private static ForeignKeyDiffEntry ToForeignKeyDiff(TableKey tableKey, ForeignKeySnapshotNode foreignKey)
    {
        return new ForeignKeyDiffEntry
        {
            SchemaName = tableKey.SchemaName,
            TableName = tableKey.TableName,
            Name = foreignKey.Name,
            ColumnNames = NormalizeOrdered(foreignKey.ColumnNames),
            ReferencedSchema = foreignKey.ReferencedSchema,
            ReferencedTable = foreignKey.ReferencedTable,
            ReferencedColumnNames = NormalizeOrdered(foreignKey.ReferencedColumnNames),
        };
    }

    private static IndexDiffEntry ToIndexDiff(TableKey tableKey, IndexSnapshotNode index)
    {
        return new IndexDiffEntry
        {
            SchemaName = tableKey.SchemaName,
            TableName = tableKey.TableName,
            Name = index.Name,
            ColumnNames = NormalizeOrdered(index.ColumnNames),
            IsUnique = index.IsUnique,
        };
    }

    private static string ToForeignKeySignature(ForeignKeySnapshotNode foreignKey)
    {
        var columns = string.Join(",", NormalizeOrdered(foreignKey.ColumnNames));
        var referencedColumns = string.Join(",", NormalizeOrdered(foreignKey.ReferencedColumnNames));
        return $"{foreignKey.Name}|{columns}|{foreignKey.ReferencedSchema}|{foreignKey.ReferencedTable}|{referencedColumns}";
    }

    private static string ToIndexSignature(IndexSnapshotNode index)
    {
        var columns = string.Join(",", NormalizeOrdered(index.ColumnNames));
        return $"{index.Name}|{index.IsUnique}|{columns}";
    }

    private static IReadOnlyCollection<string> NormalizeOrdered(IEnumerable<string> values)
    {
        return values
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static bool SequenceEquals(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        return left.Zip(right).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));
    }

    private readonly record struct TableKey(string SchemaName, string TableName);

    private sealed class TableKeyComparer : IEqualityComparer<TableKey>
    {
        public static readonly TableKeyComparer Instance = new();

        public bool Equals(TableKey x, TableKey y)
        {
            return string.Equals(x.SchemaName, y.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.TableName, y.TableName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(TableKey obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.SchemaName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TableName));
        }
    }
}

public sealed class MigrationService(
    IMigrationStore migrationStore,
    IMigrationExecutionStore migrationExecutionStore,
    IConnectionStore connectionStore,
    IDatabaseProviderResolver providerResolver,
    ISchemaSnapshotStore snapshotStore,
    ISchemaDiffEngine diffEngine)
{
    public Task<IReadOnlyCollection<MigrationDefinition>> ListByConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        return migrationStore.ListByConnectionAsync(connectionId, cancellationToken);
    }

    public Task<IReadOnlyCollection<MigrationExecutionRun>> ListHistoryByConnectionAsync(Guid connectionId, int limit = 200, CancellationToken cancellationToken = default)
    {
        return migrationExecutionStore.ListByConnectionAsync(connectionId, limit, cancellationToken);
    }

    public Task<IReadOnlyCollection<MigrationExecutionRun>> ListHistoryByMigrationAsync(Guid migrationId, int limit = 200, CancellationToken cancellationToken = default)
    {
        return migrationExecutionStore.ListByMigrationAsync(migrationId, limit, cancellationToken);
    }

    public async Task<MigrationDefinition> GetByIdOrThrowAsync(Guid migrationId, CancellationToken cancellationToken = default)
    {
        var migration = await migrationStore.GetAsync(migrationId, cancellationToken);
        if (migration is null)
        {
            throw new InvalidOperationException("Migration not found.");
        }

        return migration;
    }

    public async Task<MigrationDefinition> UpsertAsync(
        Guid? id,
        Guid connectionId,
        string name,
        string? description,
        string upScript,
        string downScript,
        Guid? sourceSnapshotId,
        Guid? targetSnapshotId,
        MigrationStatus? explicitStatus,
        CancellationToken cancellationToken = default)
    {
        _ = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        await ValidateOptionalSnapshotOwnershipAsync(connectionId, sourceSnapshotId, targetSnapshotId, cancellationToken);

        var existing = id.HasValue ? await migrationStore.GetAsync(id.Value, cancellationToken) : null;
        if (existing is not null && existing.ConnectionId != connectionId)
        {
            throw new InvalidOperationException("Migration does not belong to the provided connection.");
        }

        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Migration name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(upScript))
        {
            throw new ArgumentException("Up script cannot be empty.", nameof(upScript));
        }

        var now = DateTimeOffset.UtcNow;
        var calculatedChecksum = ComputeChecksum(upScript, downScript);

        var migration = new MigrationDefinition
        {
            Id = existing?.Id ?? id ?? Guid.NewGuid(),
            ConnectionId = connectionId,
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            UpScript = upScript,
            DownScript = downScript,
            Checksum = calculatedChecksum,
            SourceSnapshotId = sourceSnapshotId,
            TargetSnapshotId = targetSnapshotId,
            Status = explicitStatus ?? existing?.Status ?? InferInitialStatus(upScript, downScript),
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now,
        };

        await migrationStore.SaveAsync(migration, cancellationToken);
        return migration;
    }

    public async Task DeleteAsync(Guid migrationId, CancellationToken cancellationToken = default)
    {
        var migration = await GetByIdOrThrowAsync(migrationId, cancellationToken);
        if (migration.Status == MigrationStatus.Applied)
        {
            throw new InvalidOperationException("Applied migrations cannot be deleted. Rollback first.");
        }

        await migrationStore.DeleteAsync(migrationId, cancellationToken);
    }

    public async Task<MigrationExecutionRun> ApplyAsync(
        Guid connectionId,
        Guid migrationId,
        bool confirmDestructive,
        CancellationToken cancellationToken = default)
    {
        var migration = await EnsureMigrationBelongsToConnectionAsync(connectionId, migrationId, cancellationToken);
        if (migration.Status == MigrationStatus.Applied)
        {
            throw new InvalidOperationException("Migration is already applied.");
        }

        if (ContainsDestructiveStatements(migration.UpScript) && !confirmDestructive)
        {
            throw new InvalidOperationException("Migration contains potentially destructive SQL. Confirm explicit destructive execution.");
        }

        return await ExecuteMigrationAsync(
            connectionId,
            migration,
            MigrationDirection.Up,
            migration.UpScript,
            MigrationStatus.Applied,
            cancellationToken);
    }

    public async Task<MigrationExecutionRun> RollbackAsync(
        Guid connectionId,
        Guid migrationId,
        bool confirmDestructive,
        CancellationToken cancellationToken = default)
    {
        var migration = await EnsureMigrationBelongsToConnectionAsync(connectionId, migrationId, cancellationToken);
        if (string.IsNullOrWhiteSpace(migration.DownScript))
        {
            throw new InvalidOperationException("Migration has no down script. Rollback is unavailable.");
        }

        if (migration.Status != MigrationStatus.Applied && migration.Status != MigrationStatus.Failed)
        {
            throw new InvalidOperationException("Only applied or failed migrations can be rolled back.");
        }

        if (ContainsDestructiveStatements(migration.DownScript) && !confirmDestructive)
        {
            throw new InvalidOperationException("Rollback contains potentially destructive SQL. Confirm explicit destructive execution.");
        }

        return await ExecuteMigrationAsync(
            connectionId,
            migration,
            MigrationDirection.Down,
            migration.DownScript,
            MigrationStatus.RolledBack,
            cancellationToken);
    }

    public async Task<MigrationDefinition> CreateDraftFromDiffAsync(
        Guid connectionId,
        Guid sourceSnapshotId,
        Guid targetSnapshotId,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (sourceSnapshotId == targetSnapshotId)
        {
            throw new ArgumentException("Source and target snapshots must be different.");
        }

        var connection = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        var sourceSnapshot = await snapshotStore.GetAsync(sourceSnapshotId, cancellationToken)
            ?? throw new InvalidOperationException("Source snapshot not found.");
        var targetSnapshot = await snapshotStore.GetAsync(targetSnapshotId, cancellationToken)
            ?? throw new InvalidOperationException("Target snapshot not found.");

        if (sourceSnapshot.ConnectionId != connectionId || targetSnapshot.ConnectionId != connectionId)
        {
            throw new InvalidOperationException("Snapshots must belong to the selected connection.");
        }

        var diff = diffEngine.Compare(sourceSnapshot, targetSnapshot);
        var (upScript, downScript) = BuildScriptsFromDiff(diff, sourceSnapshot, targetSnapshot, connection.DatabaseType);

        var migrationName = string.IsNullOrWhiteSpace(name)
            ? $"diff-{DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}"
            : name.Trim();

        var migrationDescription = string.IsNullOrWhiteSpace(description)
            ? $"Generated from snapshots '{diff.SourceSnapshotName}' -> '{diff.TargetSnapshotName}'."
            : description;

        return await UpsertAsync(
            id: null,
            connectionId,
            migrationName,
            migrationDescription,
            upScript,
            downScript,
            sourceSnapshotId,
            targetSnapshotId,
            MigrationStatus.Pending,
            cancellationToken);
    }

    private async Task<MigrationExecutionRun> ExecuteMigrationAsync(
        Guid connectionId,
        MigrationDefinition migration,
        MigrationDirection direction,
        string sql,
        MigrationStatus successStatus,
        CancellationToken cancellationToken)
    {
        var connection = await GetConnectionOrThrowAsync(connectionId, cancellationToken);
        var provider = providerResolver.Resolve(connection.DatabaseType);
        var runId = Guid.NewGuid();
        var executedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var executionResult = await provider.ExecuteScriptAsync(connection, sql, cancellationToken);
            stopwatch.Stop();

            var succeededRun = new MigrationExecutionRun
            {
                MigrationRunId = runId,
                MigrationId = migration.Id,
                ConnectionId = connectionId,
                ExecutedAtUtc = executedAt,
                Direction = direction,
                Status = MigrationExecutionStatus.Succeeded,
                DurationMs = executionResult.DurationMs,
                ExecutionLog = executionResult.OutputLog,
                ErrorMessage = null,
            };

            await migrationExecutionStore.AddAsync(succeededRun, cancellationToken);
            await migrationStore.SaveAsync(CloneWithStatus(migration, successStatus), cancellationToken);

            return succeededRun;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            var failedRun = new MigrationExecutionRun
            {
                MigrationRunId = runId,
                MigrationId = migration.Id,
                ConnectionId = connectionId,
                ExecutedAtUtc = executedAt,
                Direction = direction,
                Status = MigrationExecutionStatus.Failed,
                DurationMs = stopwatch.ElapsedMilliseconds,
                ExecutionLog = null,
                ErrorMessage = exception.Message,
            };

            await migrationExecutionStore.AddAsync(failedRun, cancellationToken);
            await migrationStore.SaveAsync(CloneWithStatus(migration, MigrationStatus.Failed), cancellationToken);

            throw;
        }
    }

    private async Task<MigrationDefinition> EnsureMigrationBelongsToConnectionAsync(Guid connectionId, Guid migrationId, CancellationToken cancellationToken)
    {
        _ = await GetConnectionOrThrowAsync(connectionId, cancellationToken);

        var migration = await GetByIdOrThrowAsync(migrationId, cancellationToken);
        if (migration.ConnectionId != connectionId)
        {
            throw new InvalidOperationException("Migration does not belong to the selected connection.");
        }

        return migration;
    }

    private async Task ValidateOptionalSnapshotOwnershipAsync(
        Guid connectionId,
        Guid? sourceSnapshotId,
        Guid? targetSnapshotId,
        CancellationToken cancellationToken)
    {
        if (!sourceSnapshotId.HasValue && !targetSnapshotId.HasValue)
        {
            return;
        }

        if (sourceSnapshotId.HasValue)
        {
            var source = await snapshotStore.GetAsync(sourceSnapshotId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Source snapshot not found.");

            if (source.ConnectionId != connectionId)
            {
                throw new InvalidOperationException("Source snapshot does not belong to the selected connection.");
            }
        }

        if (targetSnapshotId.HasValue)
        {
            var target = await snapshotStore.GetAsync(targetSnapshotId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Target snapshot not found.");

            if (target.ConnectionId != connectionId)
            {
                throw new InvalidOperationException("Target snapshot does not belong to the selected connection.");
            }
        }
    }

    private static MigrationStatus InferInitialStatus(string upScript, string downScript)
    {
        if (string.IsNullOrWhiteSpace(upScript))
        {
            return MigrationStatus.Draft;
        }

        return string.IsNullOrWhiteSpace(downScript)
            ? MigrationStatus.Draft
            : MigrationStatus.Pending;
    }

    private static string ComputeChecksum(string upScript, string downScript)
    {
        var payload = $"{upScript}\n--SF-DOWN--\n{downScript}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool ContainsDestructiveStatements(string sql)
    {
        var normalized = sql.ToLowerInvariant();
        var patterns = new[]
        {
            " drop table ",
            " drop column ",
            " truncate ",
            " delete from ",
            " alter table ",
        };

        var wrapped = $" {normalized} ";
        return patterns.Any(pattern => wrapped.Contains(pattern, StringComparison.Ordinal));
    }

    private static (string UpScript, string DownScript) BuildScriptsFromDiff(
        SchemaDiffResult diff,
        SchemaSnapshot sourceSnapshot,
        SchemaSnapshot targetSnapshot,
        DatabaseType databaseType)
    {
        var sourceTables = FlattenTables(sourceSnapshot.Structure);
        var targetTables = FlattenTables(targetSnapshot.Structure);

        var upBuilder = new StringBuilder();
        var downBuilder = new StringBuilder();

        AppendHeader(upBuilder, "UP", diff);
        AppendHeader(downBuilder, "DOWN", diff);

        foreach (var table in diff.TablesAdded)
        {
            var key = BuildTableKey(table.SchemaName, table.TableName);
            if (targetTables.TryGetValue(key, out var targetTable))
            {
                upBuilder.AppendLine(BuildCreateTableSql(targetTable, databaseType));
                foreach (var foreignKey in targetTable.ForeignKeys)
                {
                    upBuilder.AppendLine(BuildAddForeignKeySql(targetTable.SchemaName, targetTable.TableName, foreignKey, databaseType));
                }

                foreach (var index in targetTable.Indexes)
                {
                    upBuilder.AppendLine(BuildAddIndexSql(targetTable.SchemaName, targetTable.TableName, index, databaseType));
                }

                upBuilder.AppendLine();
                downBuilder.AppendLine($"-- Rollback for added table {table.SchemaName}.{table.TableName}");
                downBuilder.AppendLine(BuildDropTableSql(table.SchemaName, table.TableName, databaseType));
                downBuilder.AppendLine();
            }
        }

        foreach (var table in diff.TablesRemoved)
        {
            var key = BuildTableKey(table.SchemaName, table.TableName);
            upBuilder.AppendLine($"-- Potentially destructive: removed table {table.SchemaName}.{table.TableName}");
            upBuilder.AppendLine(BuildDropTableSql(table.SchemaName, table.TableName, databaseType));
            upBuilder.AppendLine();

            if (sourceTables.TryGetValue(key, out var sourceTable))
            {
                downBuilder.AppendLine(BuildCreateTableSql(sourceTable, databaseType));
                foreach (var foreignKey in sourceTable.ForeignKeys)
                {
                    downBuilder.AppendLine(BuildAddForeignKeySql(sourceTable.SchemaName, sourceTable.TableName, foreignKey, databaseType));
                }

                foreach (var index in sourceTable.Indexes)
                {
                    downBuilder.AppendLine(BuildAddIndexSql(sourceTable.SchemaName, sourceTable.TableName, index, databaseType));
                }

                downBuilder.AppendLine();
            }
        }

        foreach (var modified in diff.TablesModified)
        {
            var key = BuildTableKey(modified.SchemaName, modified.TableName);

            foreach (var column in modified.ColumnsAdded)
            {
                if (targetTables.TryGetValue(key, out var targetTable))
                {
                    var targetColumn = targetTable.Columns.FirstOrDefault(c =>
                        string.Equals(c.Name, column.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (targetColumn is not null)
                    {
                        upBuilder.AppendLine(BuildAddColumnSql(modified.SchemaName, modified.TableName, targetColumn, databaseType));
                        upBuilder.AppendLine();

                        downBuilder.AppendLine($"-- Potentially destructive rollback for added column {modified.SchemaName}.{modified.TableName}.{targetColumn.Name}");
                        downBuilder.AppendLine(BuildDropColumnSql(modified.SchemaName, modified.TableName, targetColumn.Name, databaseType));
                        downBuilder.AppendLine();
                    }
                }
            }

            foreach (var column in modified.ColumnsRemoved)
            {
                upBuilder.AppendLine($"-- Potentially destructive: removed column {modified.SchemaName}.{modified.TableName}.{column.ColumnName}");
                upBuilder.AppendLine(BuildDropColumnSql(modified.SchemaName, modified.TableName, column.ColumnName, databaseType));
                upBuilder.AppendLine();

                if (sourceTables.TryGetValue(key, out var sourceTable))
                {
                    var sourceColumn = sourceTable.Columns.FirstOrDefault(c =>
                        string.Equals(c.Name, column.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (sourceColumn is not null)
                    {
                        downBuilder.AppendLine(BuildAddColumnSql(modified.SchemaName, modified.TableName, sourceColumn, databaseType));
                        downBuilder.AppendLine();
                    }
                }
            }

            foreach (var column in modified.ColumnsModified)
            {
                upBuilder.AppendLine(BuildAlterColumnSql(
                    modified.SchemaName,
                    modified.TableName,
                    column.ColumnName,
                    column.TargetDataType,
                    column.TargetIsNullable,
                    databaseType));
                upBuilder.AppendLine();

                downBuilder.AppendLine(BuildAlterColumnSql(
                    modified.SchemaName,
                    modified.TableName,
                    column.ColumnName,
                    column.SourceDataType,
                    column.SourceIsNullable,
                    databaseType));
                downBuilder.AppendLine();
            }

            if (modified.PrimaryKeyChanged)
            {
                if (modified.SourcePrimaryKeyColumns.Count > 0)
                {
                    upBuilder.AppendLine(BuildDropPrimaryKeySql(modified.SchemaName, modified.TableName, databaseType));
                    upBuilder.AppendLine();
                }

                if (modified.TargetPrimaryKeyColumns.Count > 0)
                {
                    upBuilder.AppendLine(BuildAddPrimaryKeySql(modified.SchemaName, modified.TableName, modified.TargetPrimaryKeyColumns, databaseType));
                    upBuilder.AppendLine();
                }

                if (modified.TargetPrimaryKeyColumns.Count > 0)
                {
                    downBuilder.AppendLine(BuildDropPrimaryKeySql(modified.SchemaName, modified.TableName, databaseType));
                    downBuilder.AppendLine();
                }

                if (modified.SourcePrimaryKeyColumns.Count > 0)
                {
                    downBuilder.AppendLine(BuildAddPrimaryKeySql(modified.SchemaName, modified.TableName, modified.SourcePrimaryKeyColumns, databaseType));
                    downBuilder.AppendLine();
                }
            }

            foreach (var fk in modified.ForeignKeysRemoved)
            {
                upBuilder.AppendLine(BuildDropForeignKeySql(modified.SchemaName, modified.TableName, fk.Name, databaseType));
                upBuilder.AppendLine();

                downBuilder.AppendLine(BuildAddForeignKeySql(modified.SchemaName, modified.TableName, fk, databaseType));
                downBuilder.AppendLine();
            }

            foreach (var fk in modified.ForeignKeysAdded)
            {
                upBuilder.AppendLine(BuildAddForeignKeySql(modified.SchemaName, modified.TableName, fk, databaseType));
                upBuilder.AppendLine();

                downBuilder.AppendLine(BuildDropForeignKeySql(modified.SchemaName, modified.TableName, fk.Name, databaseType));
                downBuilder.AppendLine();
            }

            foreach (var index in modified.IndexesRemoved)
            {
                upBuilder.AppendLine(BuildDropIndexSql(modified.SchemaName, modified.TableName, index.Name, databaseType));
                upBuilder.AppendLine();

                downBuilder.AppendLine(BuildAddIndexSql(modified.SchemaName, modified.TableName, index, databaseType));
                downBuilder.AppendLine();
            }

            foreach (var index in modified.IndexesAdded)
            {
                upBuilder.AppendLine(BuildAddIndexSql(modified.SchemaName, modified.TableName, index, databaseType));
                upBuilder.AppendLine();

                downBuilder.AppendLine(BuildDropIndexSql(modified.SchemaName, modified.TableName, index.Name, databaseType));
                downBuilder.AppendLine();
            }
        }

        EnsureScriptHasBody(upBuilder);
        EnsureScriptHasBody(downBuilder);

        return (upBuilder.ToString().Trim(), downBuilder.ToString().Trim());
    }

    private static void AppendHeader(StringBuilder builder, string direction, SchemaDiffResult diff)
    {
        builder.AppendLine($"-- Auto-generated {direction} migration from schema diff");
        builder.AppendLine($"-- Source: {diff.SourceSnapshotName} ({diff.SourceSnapshotId})");
        builder.AppendLine($"-- Target: {diff.TargetSnapshotName} ({diff.TargetSnapshotId})");
        builder.AppendLine($"-- Generated at: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine();
    }

    private static void EnsureScriptHasBody(StringBuilder builder)
    {
        if (builder.ToString().Split('\n').All(line => line.TrimStart().StartsWith("--", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(line)))
        {
            builder.AppendLine("select 1;");
        }
    }

    private static Dictionary<string, TableSnapshotNode> FlattenTables(DatabaseStructureSnapshot snapshot)
    {
        return snapshot.Schemas
            .SelectMany(schema => schema.Tables)
            .ToDictionary(table => BuildTableKey(table.SchemaName, table.TableName), table => table, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildCreateTableSql(TableSnapshotNode table, DatabaseType databaseType)
    {
        var tableName = FullName(table.SchemaName, table.TableName, databaseType);
        var columnLines = table.Columns
            .Select(column => $"    {Quote(column.Name, databaseType)} {column.DataType}{(column.IsNullable ? string.Empty : " NOT NULL")}")
            .ToList();

        if (table.PrimaryKeyColumns.Count > 0)
        {
            var pkColumns = string.Join(", ", table.PrimaryKeyColumns.Select(column => Quote(column, databaseType)));
            columnLines.Add($"    PRIMARY KEY ({pkColumns})");
        }

        var definition = string.Join(",\n", columnLines);
        return $"create table {tableName} (\n{definition}\n);";
    }

    private static string BuildDropTableSql(string schemaName, string tableName, DatabaseType databaseType)
    {
        return $"drop table if exists {FullName(schemaName, tableName, databaseType)};";
    }

    private static string BuildAddColumnSql(string schemaName, string tableName, ColumnSnapshotNode column, DatabaseType databaseType)
    {
        return $"alter table {FullName(schemaName, tableName, databaseType)} add column {Quote(column.Name, databaseType)} {column.DataType}{(column.IsNullable ? string.Empty : " NOT NULL")};";
    }

    private static string BuildDropColumnSql(string schemaName, string tableName, string columnName, DatabaseType databaseType)
    {
        return $"alter table {FullName(schemaName, tableName, databaseType)} drop column {Quote(columnName, databaseType)};";
    }

    private static string BuildAlterColumnSql(string schemaName, string tableName, string columnName, string dataType, bool isNullable, DatabaseType databaseType)
    {
        var fullName = FullName(schemaName, tableName, databaseType);
        var quotedColumn = Quote(columnName, databaseType);

        if (databaseType == DatabaseType.MySql)
        {
            return $"alter table {fullName} modify column {quotedColumn} {dataType}{(isNullable ? " null" : " not null")};";
        }

        var nullabilitySql = isNullable
            ? $"alter table {fullName} alter column {quotedColumn} drop not null;"
            : $"alter table {fullName} alter column {quotedColumn} set not null;";

        return $"alter table {fullName} alter column {quotedColumn} type {dataType};\n{nullabilitySql}";
    }

    private static string BuildAddPrimaryKeySql(string schemaName, string tableName, IReadOnlyCollection<string> columns, DatabaseType databaseType)
    {
        var columnsSql = string.Join(", ", columns.Select(column => Quote(column, databaseType)));
        return $"alter table {FullName(schemaName, tableName, databaseType)} add primary key ({columnsSql});";
    }

    private static string BuildDropPrimaryKeySql(string schemaName, string tableName, DatabaseType databaseType)
    {
        if (databaseType == DatabaseType.MySql)
        {
            return $"alter table {FullName(schemaName, tableName, databaseType)} drop primary key;";
        }

        var pkName = Quote($"{tableName}_pkey", databaseType);
        return $"alter table {FullName(schemaName, tableName, databaseType)} drop constraint if exists {pkName};";
    }

    private static string BuildAddForeignKeySql(string schemaName, string tableName, ForeignKeySnapshotNode foreignKey, DatabaseType databaseType)
    {
        var columns = string.Join(", ", foreignKey.ColumnNames.Select(column => Quote(column, databaseType)));
        var referencedColumns = string.Join(", ", foreignKey.ReferencedColumnNames.Select(column => Quote(column, databaseType)));
        return $"alter table {FullName(schemaName, tableName, databaseType)} add constraint {Quote(foreignKey.Name, databaseType)} foreign key ({columns}) references {FullName(foreignKey.ReferencedSchema, foreignKey.ReferencedTable, databaseType)} ({referencedColumns});";
    }

    private static string BuildAddForeignKeySql(string schemaName, string tableName, ForeignKeyDiffEntry foreignKey, DatabaseType databaseType)
    {
        var columns = string.Join(", ", foreignKey.ColumnNames.Select(column => Quote(column, databaseType)));
        var referencedColumns = string.Join(", ", foreignKey.ReferencedColumnNames.Select(column => Quote(column, databaseType)));
        return $"alter table {FullName(schemaName, tableName, databaseType)} add constraint {Quote(foreignKey.Name, databaseType)} foreign key ({columns}) references {FullName(foreignKey.ReferencedSchema, foreignKey.ReferencedTable, databaseType)} ({referencedColumns});";
    }

    private static string BuildDropForeignKeySql(string schemaName, string tableName, string foreignKeyName, DatabaseType databaseType)
    {
        if (databaseType == DatabaseType.MySql)
        {
            return $"alter table {FullName(schemaName, tableName, databaseType)} drop foreign key {Quote(foreignKeyName, databaseType)};";
        }

        return $"alter table {FullName(schemaName, tableName, databaseType)} drop constraint if exists {Quote(foreignKeyName, databaseType)};";
    }

    private static string BuildAddIndexSql(string schemaName, string tableName, IndexSnapshotNode index, DatabaseType databaseType)
    {
        var uniqueSql = index.IsUnique ? "unique " : string.Empty;
        var columns = string.Join(", ", index.ColumnNames.Select(column => Quote(column, databaseType)));

        if (databaseType == DatabaseType.MySql)
        {
            return $"create {uniqueSql}index {Quote(index.Name, databaseType)} on {FullName(schemaName, tableName, databaseType)} ({columns});";
        }

        return $"create {uniqueSql}index {Quote(index.Name, databaseType)} on {FullName(schemaName, tableName, databaseType)} ({columns});";
    }

    private static string BuildAddIndexSql(string schemaName, string tableName, IndexDiffEntry index, DatabaseType databaseType)
    {
        var uniqueSql = index.IsUnique ? "unique " : string.Empty;
        var columns = string.Join(", ", index.ColumnNames.Select(column => Quote(column, databaseType)));

        if (databaseType == DatabaseType.MySql)
        {
            return $"create {uniqueSql}index {Quote(index.Name, databaseType)} on {FullName(schemaName, tableName, databaseType)} ({columns});";
        }

        return $"create {uniqueSql}index {Quote(index.Name, databaseType)} on {FullName(schemaName, tableName, databaseType)} ({columns});";
    }

    private static string BuildDropIndexSql(string schemaName, string tableName, string indexName, DatabaseType databaseType)
    {
        if (databaseType == DatabaseType.MySql)
        {
            return $"drop index {Quote(indexName, databaseType)} on {FullName(schemaName, tableName, databaseType)};";
        }

        return $"drop index if exists {FullName(schemaName, indexName, databaseType)};";
    }

    private static string BuildTableKey(string schemaName, string tableName)
    {
        return $"{schemaName}.{tableName}";
    }

    private static string FullName(string schemaName, string tableName, DatabaseType databaseType)
    {
        return $"{Quote(schemaName, databaseType)}.{Quote(tableName, databaseType)}";
    }

    private static string Quote(string identifier, DatabaseType databaseType)
    {
        var escaped = databaseType == DatabaseType.MySql
            ? identifier.Replace("`", "``", StringComparison.Ordinal)
            : identifier.Replace("\"", "\"\"", StringComparison.Ordinal);

        return databaseType == DatabaseType.MySql
            ? $"`{escaped}`"
            : $"\"{escaped}\"";
    }

    private static MigrationDefinition CloneWithStatus(MigrationDefinition migration, MigrationStatus nextStatus)
    {
        return new MigrationDefinition
        {
            Id = migration.Id,
            ConnectionId = migration.ConnectionId,
            Name = migration.Name,
            Description = migration.Description,
            UpScript = migration.UpScript,
            DownScript = migration.DownScript,
            Checksum = migration.Checksum,
            SourceSnapshotId = migration.SourceSnapshotId,
            TargetSnapshotId = migration.TargetSnapshotId,
            Status = nextStatus,
            CreatedAtUtc = migration.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
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
