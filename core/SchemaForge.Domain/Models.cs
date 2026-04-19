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

public sealed class SavedQuery
{
    public required Guid Id { get; init; }
    public required Guid ConnectionId { get; init; }
    public required string Title { get; init; }
    public required string Sql { get; init; }
    public required IReadOnlyCollection<string> Tags { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public enum QueryExecutionStatus
{
    Succeeded = 1,
    Failed = 2,
}

public sealed class QueryHistoryEntry
{
    public required Guid Id { get; init; }
    public required Guid ConnectionId { get; init; }
    public required string Sql { get; init; }
    public required QueryExecutionStatus Status { get; init; }
    public required long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTimeOffset ExecutedAtUtc { get; init; }
}

public sealed class SchemaSnapshot
{
    public required Guid Id { get; init; }
    public required Guid ConnectionId { get; init; }
    public string? Name { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DatabaseStructureSnapshot Structure { get; init; }
}

public sealed class DatabaseStructureSnapshot
{
    public required string DatabaseName { get; init; }
    public required IReadOnlyCollection<SchemaSnapshotNode> Schemas { get; init; }
}

public sealed class SchemaSnapshotNode
{
    public required string SchemaName { get; init; }
    public required IReadOnlyCollection<TableSnapshotNode> Tables { get; init; }
}

public sealed class TableSnapshotNode
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyCollection<ColumnSnapshotNode> Columns { get; init; }
    public required IReadOnlyCollection<string> PrimaryKeyColumns { get; init; }
    public required IReadOnlyCollection<ForeignKeySnapshotNode> ForeignKeys { get; init; }
    public required IReadOnlyCollection<IndexSnapshotNode> Indexes { get; init; }
}

public sealed class ColumnSnapshotNode
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public required bool IsNullable { get; init; }
}

public sealed class ForeignKeySnapshotNode
{
    public required string Name { get; init; }
    public required IReadOnlyCollection<string> ColumnNames { get; init; }
    public required string ReferencedSchema { get; init; }
    public required string ReferencedTable { get; init; }
    public required IReadOnlyCollection<string> ReferencedColumnNames { get; init; }
}

public sealed class IndexSnapshotNode
{
    public required string Name { get; init; }
    public required IReadOnlyCollection<string> ColumnNames { get; init; }
    public required bool IsUnique { get; init; }
}
