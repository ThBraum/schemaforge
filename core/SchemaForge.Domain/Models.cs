namespace SchemaForge.Domain;

public enum DatabaseType
{
    Postgres = 1,
    MySql = 2,
}

public sealed class SavedConnection
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required DatabaseType DatabaseType { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string Database { get; init; }
    public required string Username { get; init; }
    public string? Password { get; init; }
}

public sealed class SchemaSummary
{
    public required Guid ConnectionId { get; init; }
    public required string DatabaseName { get; init; }
    public required IReadOnlyCollection<SchemaNode> Schemas { get; init; }
}

public sealed class SchemaNode
{
    public required string SchemaName { get; init; }
    public required IReadOnlyCollection<TableNode> Tables { get; init; }
}

public sealed class TableNode
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyCollection<ColumnNode> Columns { get; init; }
}

public sealed class ColumnNode
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public required bool IsNullable { get; init; }
    public string? DefaultValue { get; init; }
}

public sealed class TablePreview
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyCollection<string> Columns { get; init; }
    public required IReadOnlyCollection<Dictionary<string, object?>> Rows { get; init; }
}

public sealed class QueryResult
{
    public required IReadOnlyCollection<string> Columns { get; init; }
    public required IReadOnlyCollection<Dictionary<string, object?>> Rows { get; init; }
    public required int RowCount { get; init; }
    public required long DurationMs { get; init; }
}
