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
