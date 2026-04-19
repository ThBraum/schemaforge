export type DatabaseType = 'postgres' | 'mysql';
export type Language = 'en' | 'pt-BR';
export type ThemeMode = 'dark' | 'light';

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

export type QueryExecutionStatus = 'succeeded' | 'failed';

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
