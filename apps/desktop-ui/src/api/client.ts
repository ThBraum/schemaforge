import type { QueryResult, SavedConnection, SchemaSummary, TablePreview } from '../types';

const API_BASE = 'http://127.0.0.1:5051/api';

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || 'Request failed');
  }

  return response.json() as Promise<T>;
}

export const api = {
  listConnections: () => request<SavedConnection[]>('/connections'),
  saveConnection: (payload: SavedConnection) =>
    request<SavedConnection>('/connections', { method: 'POST', body: JSON.stringify(payload) }),
  getSchema: (connectionId: string) => request<SchemaSummary>(`/explorer/schema/${connectionId}`),
  previewTable: (connectionId: string, schemaName: string, tableName: string, limit = 100) =>
    request<TablePreview>(`/explorer/preview/${connectionId}?schemaName=${encodeURIComponent(schemaName)}&tableName=${encodeURIComponent(tableName)}&limit=${limit}`),
  runQuery: (connectionId: string, sql: string) =>
    request<QueryResult>(`/query/run/${connectionId}`, { method: 'POST', body: JSON.stringify({ sql }) }),
};
