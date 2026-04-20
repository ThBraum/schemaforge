using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;
using System.Diagnostics;

namespace SchemaForge.Infrastructure;

public sealed class MySqlDatabaseProvider : IDatabaseProvider
{
    public DatabaseType DatabaseType => DatabaseType.MySql;

    public async Task<DatabaseStructureSnapshot> CaptureSnapshotStructureAsync(SavedConnection connection, CancellationToken cancellationToken = default)
    {
        await using var db = (MySqlConnector.MySqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        const string columnsSql = @"
select table_schema as SchemaName, table_name as TableName, column_name as ColumnName,
       column_type as DataType, (is_nullable = 'YES') as IsNullable
from information_schema.columns
where table_schema = @DatabaseName
order by table_name, ordinal_position;";

        const string primaryKeysSql = @"
select table_schema as SchemaName,
       table_name as TableName,
       column_name as ColumnName,
       seq_in_index as Position
from information_schema.statistics
where table_schema = @DatabaseName
  and index_name = 'PRIMARY'
order by table_name, seq_in_index;";

        const string foreignKeysSql = @"
select constraint_name as ConstraintName,
       table_schema as SchemaName,
       table_name as TableName,
       column_name as ColumnName,
       referenced_table_schema as ReferencedSchema,
       referenced_table_name as ReferencedTable,
       referenced_column_name as ReferencedColumnName,
       ordinal_position as Position
from information_schema.key_column_usage
where table_schema = @DatabaseName
  and referenced_table_name is not null
order by table_name, constraint_name, ordinal_position;";

        const string indexesSql = @"
select table_schema as SchemaName,
       table_name as TableName,
       index_name as IndexName,
       (non_unique = 0) as IsUnique,
       column_name as ColumnName,
       seq_in_index as Position
from information_schema.statistics
where table_schema = @DatabaseName
  and index_name <> 'PRIMARY'
order by table_name, index_name, seq_in_index;";

        var columns = (await db.QueryAsync<ColumnRow>(columnsSql, new { DatabaseName = connection.Database })).ToArray();
        var primaryKeys = (await db.QueryAsync<PrimaryKeyRow>(primaryKeysSql, new { DatabaseName = connection.Database })).ToArray();
        var foreignKeys = (await db.QueryAsync<ForeignKeyRow>(foreignKeysSql, new { DatabaseName = connection.Database })).ToArray();
        var indexes = (await db.QueryAsync<IndexRow>(indexesSql, new { DatabaseName = connection.Database })).ToArray();

        var primaryKeysByTable = primaryKeys
            .GroupBy(x => (x.SchemaName, x.TableName))
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<string>)x.OrderBy(item => item.Position).Select(item => item.ColumnName).ToArray());

        var foreignKeysByTable = foreignKeys
            .GroupBy(x => (x.SchemaName, x.TableName, x.ConstraintName))
            .Select(group => new
            {
                group.Key.SchemaName,
                group.Key.TableName,
                ForeignKey = new ForeignKeySnapshotNode
                {
                    Name = group.Key.ConstraintName,
                    ColumnNames = group.OrderBy(item => item.Position).Select(item => item.ColumnName).ToArray(),
                    ReferencedSchema = group.First().ReferencedSchema,
                    ReferencedTable = group.First().ReferencedTable,
                    ReferencedColumnNames = group.OrderBy(item => item.Position).Select(item => item.ReferencedColumnName).ToArray(),
                },
            })
            .GroupBy(x => (x.SchemaName, x.TableName))
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<ForeignKeySnapshotNode>)x.Select(item => item.ForeignKey).ToArray());

        var indexesByTable = indexes
            .GroupBy(x => (x.SchemaName, x.TableName, x.IndexName, x.IsUnique))
            .Select(group => new
            {
                group.Key.SchemaName,
                group.Key.TableName,
                Index = new IndexSnapshotNode
                {
                    Name = group.Key.IndexName,
                    IsUnique = group.Key.IsUnique,
                    ColumnNames = group.OrderBy(item => item.Position).Select(item => item.ColumnName).ToArray(),
                },
            })
            .GroupBy(x => (x.SchemaName, x.TableName))
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyCollection<IndexSnapshotNode>)x.Select(item => item.Index).ToArray());

        var schemas = columns
            .GroupBy(x => x.SchemaName)
            .Select(schemaGroup => new SchemaSnapshotNode
            {
                SchemaName = schemaGroup.Key,
                Tables = schemaGroup
                    .GroupBy(x => x.TableName)
                    .Select(tableGroup =>
                    {
                        var key = (schemaGroup.Key, tableGroup.Key);
                        return new TableSnapshotNode
                        {
                            SchemaName = schemaGroup.Key,
                            TableName = tableGroup.Key,
                            Columns = tableGroup.Select(column => new ColumnSnapshotNode
                            {
                                Name = column.ColumnName,
                                DataType = column.DataType,
                                IsNullable = column.IsNullable,
                            }).ToArray(),
                            PrimaryKeyColumns = primaryKeysByTable.TryGetValue(key, out var primaryKeyColumns)
                                ? primaryKeyColumns
                                : Array.Empty<string>(),
                            ForeignKeys = foreignKeysByTable.TryGetValue(key, out var tableForeignKeys)
                                ? tableForeignKeys
                                : Array.Empty<ForeignKeySnapshotNode>(),
                            Indexes = indexesByTable.TryGetValue(key, out var tableIndexes)
                                ? tableIndexes
                                : Array.Empty<IndexSnapshotNode>(),
                        };
                    })
                    .ToArray(),
            })
            .ToArray();

        return new DatabaseStructureSnapshot
        {
            DatabaseName = connection.Database,
            Schemas = schemas,
        };
    }

    public async Task<SchemaSummary> GetSchemaAsync(SavedConnection connection, CancellationToken cancellationToken = default)
    {
        await using var db = (MySqlConnector.MySqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        const string sql = @"
select table_schema as SchemaName, table_name as TableName, column_name as ColumnName,
       column_type as DataType, (is_nullable = 'YES') as IsNullable, column_default as DefaultValue
from information_schema.columns
where table_schema = @DatabaseName
order by table_name, ordinal_position;";

        var rows = await db.QueryAsync<ColumnRow>(sql, new { DatabaseName = connection.Database });

        var schemas = rows
            .GroupBy(x => x.SchemaName)
            .Select(schemaGroup => new SchemaNode
            {
                SchemaName = schemaGroup.Key,
                Tables = schemaGroup
                    .GroupBy(x => x.TableName)
                    .Select(tableGroup => new TableNode
                    {
                        SchemaName = schemaGroup.Key,
                        TableName = tableGroup.Key,
                        Columns = tableGroup.Select(column => new ColumnNode
                        {
                            Name = column.ColumnName,
                            DataType = column.DataType,
                            IsNullable = column.IsNullable,
                            DefaultValue = column.DefaultValue,
                        }).ToArray(),
                    })
                    .ToArray(),
            })
            .ToArray();

        return new SchemaSummary
        {
            ConnectionId = connection.Id,
            DatabaseName = connection.Database,
            Schemas = schemas,
        };
    }

    public async Task<TablePreview> PreviewTableAsync(SavedConnection connection, string schemaName, string tableName, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = (MySqlConnector.MySqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        var sql = $"select * from `{schemaName}`.`{tableName}` limit @Limit";
        var rows = await db.QueryAsync(sql, new { Limit = limit });
        return ToPreview(schemaName, tableName, rows);
    }

    public async Task<QueryResult> RunQueryAsync(SavedConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        await using var db = (MySqlConnector.MySqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        var sw = Stopwatch.StartNew();
        var rows = await db.QueryAsync(sql);
        sw.Stop();

        var list = rows.Select(row => (IDictionary<string, object?>)row).Select(dict => dict.ToDictionary(k => k.Key, v => v.Value)).ToArray();
        var columns = list.FirstOrDefault()?.Keys.ToArray() ?? Array.Empty<string>();

        return new QueryResult
        {
            Columns = columns,
            Rows = list,
            RowCount = list.Length,
            DurationMs = sw.ElapsedMilliseconds,
        };
    }

    public async Task<ScriptExecutionResult> ExecuteScriptAsync(SavedConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        await using var db = (MySqlConnector.MySqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var affectedRows = await db.ExecuteAsync(new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        stopwatch.Stop();

        return new ScriptExecutionResult
        {
            DurationMs = stopwatch.ElapsedMilliseconds,
            AffectedRows = affectedRows,
            OutputLog = $"Script executed successfully. Affected rows: {affectedRows}.",
        };
    }

    private static TablePreview ToPreview(string schemaName, string tableName, IEnumerable<dynamic> rows)
    {
        var list = rows.Select(row => (IDictionary<string, object?>)row).Select(dict => dict.ToDictionary(k => k.Key, v => v.Value)).ToArray();
        var columns = list.FirstOrDefault()?.Keys.ToArray() ?? Array.Empty<string>();

        return new TablePreview
        {
            SchemaName = schemaName,
            TableName = tableName,
            Columns = columns,
            Rows = list,
        };
    }

    private sealed record ColumnRow(string SchemaName, string TableName, string ColumnName, string DataType, bool IsNullable, string? DefaultValue);
    private sealed record PrimaryKeyRow(string SchemaName, string TableName, string ColumnName, int Position);
    private sealed record ForeignKeyRow(string ConstraintName, string SchemaName, string TableName, string ColumnName, string ReferencedSchema, string ReferencedTable, string ReferencedColumnName, int Position);
    private sealed record IndexRow(string SchemaName, string TableName, string IndexName, bool IsUnique, string ColumnName, int Position);
}
