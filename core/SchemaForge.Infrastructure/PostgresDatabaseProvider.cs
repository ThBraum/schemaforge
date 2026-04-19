using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;
using System.Data;
using System.Diagnostics;

namespace SchemaForge.Infrastructure;

public sealed class PostgresDatabaseProvider : IDatabaseProvider
{
    public DatabaseType DatabaseType => DatabaseType.Postgres;

    public async Task<SchemaSummary> GetSchemaAsync(SavedConnection connection, CancellationToken cancellationToken = default)
    {
        await using var db = (Npgsql.NpgsqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        const string sql = @"
select table_schema as SchemaName, table_name as TableName, column_name as ColumnName,
       data_type as DataType, (is_nullable = 'YES') as IsNullable, column_default as DefaultValue
from information_schema.columns
where table_schema not in ('information_schema', 'pg_catalog')
order by table_schema, table_name, ordinal_position;";

        var rows = await db.QueryAsync<ColumnRow>(sql);

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
        await using var db = (Npgsql.NpgsqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
        await db.OpenAsync(cancellationToken);

        var sql = $"select * from \"{schemaName}\".\"{tableName}\" limit @Limit";
        var rows = await db.QueryAsync(sql, new { Limit = limit });
        return ToPreview(schemaName, tableName, rows);
    }

    public async Task<QueryResult> RunQueryAsync(SavedConnection connection, string sql, CancellationToken cancellationToken = default)
    {
        await using var db = (Npgsql.NpgsqlConnection)ConnectionStringFactory.CreateRemoteConnection(connection);
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
}
