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

public sealed class ScriptExecutionResult
{
    public required long DurationMs { get; init; }
    public required int AffectedRows { get; init; }
    public string? OutputLog { get; init; }
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

public enum MigrationStatus
{
    Draft = 1,
    Pending = 2,
    Applied = 3,
    Failed = 4,
    RolledBack = 5,
}

public enum MigrationDirection
{
    Up = 1,
    Down = 2,
}

public enum MigrationExecutionStatus
{
    Succeeded = 1,
    Failed = 2,
}

public sealed class MigrationDefinition
{
    public required Guid Id { get; init; }
    public required Guid ConnectionId { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string UpScript { get; init; }
    public required string DownScript { get; init; }
    public string? Checksum { get; init; }
    public Guid? SourceSnapshotId { get; init; }
    public Guid? TargetSnapshotId { get; init; }
    public required MigrationStatus Status { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed class MigrationExecutionRun
{
    public required Guid MigrationRunId { get; init; }
    public required Guid MigrationId { get; init; }
    public required Guid ConnectionId { get; init; }
    public required DateTimeOffset ExecutedAtUtc { get; init; }
    public required MigrationDirection Direction { get; init; }
    public required MigrationExecutionStatus Status { get; init; }
    public required long DurationMs { get; init; }
    public string? ExecutionLog { get; init; }
    public string? ErrorMessage { get; init; }
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

public sealed class SchemaDiffResult
{
    public required Guid ConnectionId { get; init; }
    public required Guid SourceSnapshotId { get; init; }
    public required Guid TargetSnapshotId { get; init; }
    public required string SourceSnapshotName { get; init; }
    public required string TargetSnapshotName { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required SchemaDiffSummary Summary { get; init; }
    public required IReadOnlyCollection<TableDiffEntry> TablesAdded { get; init; }
    public required IReadOnlyCollection<TableDiffEntry> TablesRemoved { get; init; }
    public required IReadOnlyCollection<TableModificationDiff> TablesModified { get; init; }
    public required IReadOnlyCollection<ColumnDiffEntry> ColumnsAdded { get; init; }
    public required IReadOnlyCollection<ColumnDiffEntry> ColumnsRemoved { get; init; }
    public required IReadOnlyCollection<ColumnModificationDiff> ColumnsModified { get; init; }
    public required IReadOnlyCollection<PrimaryKeyDiffEntry> PrimaryKeysAdded { get; init; }
    public required IReadOnlyCollection<PrimaryKeyDiffEntry> PrimaryKeysRemoved { get; init; }
    public required IReadOnlyCollection<ForeignKeyDiffEntry> ForeignKeysAdded { get; init; }
    public required IReadOnlyCollection<ForeignKeyDiffEntry> ForeignKeysRemoved { get; init; }
    public required IReadOnlyCollection<IndexDiffEntry> IndexesAdded { get; init; }
    public required IReadOnlyCollection<IndexDiffEntry> IndexesRemoved { get; init; }
    public required IReadOnlyCollection<BreakingChangeEntry> BreakingChanges { get; init; }
}

public sealed class SchemaDiffSummary
{
    public required int TablesAdded { get; init; }
    public required int TablesRemoved { get; init; }
    public required int TablesModified { get; init; }
    public required int ColumnsAdded { get; init; }
    public required int ColumnsRemoved { get; init; }
    public required int ColumnsModified { get; init; }
    public required int PrimaryKeysAdded { get; init; }
    public required int PrimaryKeysRemoved { get; init; }
    public required int ForeignKeysAdded { get; init; }
    public required int ForeignKeysRemoved { get; init; }
    public required int IndexesAdded { get; init; }
    public required int IndexesRemoved { get; init; }
    public required int BreakingChanges { get; init; }
}

public sealed class TableDiffEntry
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
}

public sealed class ColumnDiffEntry
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public string? DataType { get; init; }
    public bool? IsNullable { get; init; }
}

public sealed class ColumnModificationDiff
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public required string SourceDataType { get; init; }
    public required string TargetDataType { get; init; }
    public required bool SourceIsNullable { get; init; }
    public required bool TargetIsNullable { get; init; }
    public required bool DataTypeChanged { get; init; }
    public required bool NullabilityChanged { get; init; }
    public required bool IsBreakingChange { get; init; }
}

public sealed class PrimaryKeyDiffEntry
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyCollection<string> ColumnNames { get; init; }
}

public sealed class ForeignKeyDiffEntry
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyCollection<string> ColumnNames { get; init; }
    public required string ReferencedSchema { get; init; }
    public required string ReferencedTable { get; init; }
    public required IReadOnlyCollection<string> ReferencedColumnNames { get; init; }
}

public sealed class IndexDiffEntry
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyCollection<string> ColumnNames { get; init; }
    public required bool IsUnique { get; init; }
}

public sealed class TableModificationDiff
{
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public required IReadOnlyCollection<ColumnDiffEntry> ColumnsAdded { get; init; }
    public required IReadOnlyCollection<ColumnDiffEntry> ColumnsRemoved { get; init; }
    public required IReadOnlyCollection<ColumnModificationDiff> ColumnsModified { get; init; }
    public required bool PrimaryKeyChanged { get; init; }
    public required IReadOnlyCollection<string> SourcePrimaryKeyColumns { get; init; }
    public required IReadOnlyCollection<string> TargetPrimaryKeyColumns { get; init; }
    public required IReadOnlyCollection<ForeignKeyDiffEntry> ForeignKeysAdded { get; init; }
    public required IReadOnlyCollection<ForeignKeyDiffEntry> ForeignKeysRemoved { get; init; }
    public required IReadOnlyCollection<IndexDiffEntry> IndexesAdded { get; init; }
    public required IReadOnlyCollection<IndexDiffEntry> IndexesRemoved { get; init; }
}

public sealed class BreakingChangeEntry
{
    public required string Category { get; init; }
    public required string SchemaName { get; init; }
    public required string TableName { get; init; }
    public string? ColumnName { get; init; }
    public required string Description { get; init; }
}
