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
