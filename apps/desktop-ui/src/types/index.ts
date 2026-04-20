export type DatabaseType = "postgres" | "mysql";
export type Language = "en" | "pt-BR";
export type ThemeMode = "dark" | "light";

export interface SavedConnection {
	id: string;
	name: string;
	databaseType: DatabaseType;
	host: string;
	port: number;
	database: string;
	username: string;
	password?: string;
}

export interface SchemaSummary {
	connectionId: string;
	databaseName: string;
	schemas: SchemaNode[];
}

export interface SchemaNode {
	schemaName: string;
	tables: TableNode[];
}

export interface TableNode {
	schemaName: string;
	tableName: string;
	rowCountEstimate?: number;
	columns: ColumnNode[];
}

export interface ColumnNode {
	name: string;
	dataType: string;
	isNullable: boolean;
	defaultValue?: string | null;
}

export interface TablePreview {
	tableName: string;
	schemaName: string;
	columns: string[];
	rows: Array<Record<string, unknown>>;
}

export interface QueryResult {
	columns: string[];
	rows: Array<Record<string, unknown>>;
	rowCount: number;
	durationMs: number;
}

export interface SavedQueryItem {
	id: string;
	connectionId: string;
	title: string;
	sql: string;
	tags: string[];
	createdAtUtc: string;
	updatedAtUtc: string;
}

export type QueryExecutionStatus = "succeeded" | "failed";

export interface QueryHistoryItem {
	id: string;
	connectionId: string;
	sql: string;
	status: QueryExecutionStatus;
	durationMs: number;
	errorMessage?: string | null;
	executedAtUtc: string;
}

export interface SchemaSnapshotSummary {
	id: string;
	connectionId: string;
	name?: string | null;
	createdAtUtc: string;
	schemaCount: number;
	tableCount: number;
}

export interface SchemaSnapshot {
	id: string;
	connectionId: string;
	name?: string | null;
	createdAtUtc: string;
	structure: DatabaseStructureSnapshot;
}

export interface DatabaseStructureSnapshot {
	databaseName: string;
	schemas: SnapshotSchemaNode[];
}

export interface SnapshotSchemaNode {
	schemaName: string;
	tables: SnapshotTableNode[];
}

export interface SnapshotTableNode {
	schemaName: string;
	tableName: string;
	columns: SnapshotColumnNode[];
	primaryKeyColumns: string[];
	foreignKeys: SnapshotForeignKeyNode[];
	indexes: SnapshotIndexNode[];
}

export interface SnapshotColumnNode {
	name: string;
	dataType: string;
	isNullable: boolean;
}

export interface SnapshotForeignKeyNode {
	name: string;
	columnNames: string[];
	referencedSchema: string;
	referencedTable: string;
	referencedColumnNames: string[];
}

export interface SnapshotIndexNode {
	name: string;
	columnNames: string[];
	isUnique: boolean;
}

export interface SchemaDiffSummary {
	tablesAdded: number;
	tablesRemoved: number;
	tablesModified: number;
	columnsAdded: number;
	columnsRemoved: number;
	columnsModified: number;
	primaryKeysAdded: number;
	primaryKeysRemoved: number;
	foreignKeysAdded: number;
	foreignKeysRemoved: number;
	indexesAdded: number;
	indexesRemoved: number;
	breakingChanges: number;
}

export interface TableDiffEntry {
	schemaName: string;
	tableName: string;
}

export interface ColumnDiffEntry {
	schemaName: string;
	tableName: string;
	columnName: string;
	dataType?: string | null;
	isNullable?: boolean | null;
}

export interface ColumnModificationDiff {
	schemaName: string;
	tableName: string;
	columnName: string;
	sourceDataType: string;
	targetDataType: string;
	sourceIsNullable: boolean;
	targetIsNullable: boolean;
	dataTypeChanged: boolean;
	nullabilityChanged: boolean;
	isBreakingChange: boolean;
}

export interface PrimaryKeyDiffEntry {
	schemaName: string;
	tableName: string;
	columnNames: string[];
}

export interface ForeignKeyDiffEntry {
	schemaName: string;
	tableName: string;
	name: string;
	columnNames: string[];
	referencedSchema: string;
	referencedTable: string;
	referencedColumnNames: string[];
}

export interface IndexDiffEntry {
	schemaName: string;
	tableName: string;
	name: string;
	columnNames: string[];
	isUnique: boolean;
}

export interface TableModificationDiff {
	schemaName: string;
	tableName: string;
	columnsAdded: ColumnDiffEntry[];
	columnsRemoved: ColumnDiffEntry[];
	columnsModified: ColumnModificationDiff[];
	primaryKeyChanged: boolean;
	sourcePrimaryKeyColumns: string[];
	targetPrimaryKeyColumns: string[];
	foreignKeysAdded: ForeignKeyDiffEntry[];
	foreignKeysRemoved: ForeignKeyDiffEntry[];
	indexesAdded: IndexDiffEntry[];
	indexesRemoved: IndexDiffEntry[];
}

export interface BreakingChangeEntry {
	category: string;
	schemaName: string;
	tableName: string;
	columnName?: string | null;
	description: string;
}

export interface SchemaDiffResult {
	connectionId: string;
	sourceSnapshotId: string;
	targetSnapshotId: string;
	sourceSnapshotName: string;
	targetSnapshotName: string;
	generatedAtUtc: string;
	summary: SchemaDiffSummary;
	tablesAdded: TableDiffEntry[];
	tablesRemoved: TableDiffEntry[];
	tablesModified: TableModificationDiff[];
	columnsAdded: ColumnDiffEntry[];
	columnsRemoved: ColumnDiffEntry[];
	columnsModified: ColumnModificationDiff[];
	primaryKeysAdded: PrimaryKeyDiffEntry[];
	primaryKeysRemoved: PrimaryKeyDiffEntry[];
	foreignKeysAdded: ForeignKeyDiffEntry[];
	foreignKeysRemoved: ForeignKeyDiffEntry[];
	indexesAdded: IndexDiffEntry[];
	indexesRemoved: IndexDiffEntry[];
	breakingChanges: BreakingChangeEntry[];
}
