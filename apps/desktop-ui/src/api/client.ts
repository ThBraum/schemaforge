import type {
	MigrationExecutionRun,
	MigrationItem,
	QueryHistoryItem,
	QueryResult,
	SavedConnection,
	SavedQueryItem,
	SchemaDiffResult,
	SchemaSnapshot,
	SchemaSnapshotSummary,
	SchemaSummary,
	TablePreview,
} from "../types";

const API_BASE = "http://127.0.0.1:5051/api";

async function request<T>(path: string, options?: RequestInit): Promise<T> {
	const response = await fetch(`${API_BASE}${path}`, {
		headers: { "Content-Type": "application/json" },
		...options,
	});

	if (!response.ok) {
		const text = await response.text();
		throw new Error(text || "Request failed");
	}

	if (response.status === 204) {
		return undefined as T;
	}

	return response.json() as Promise<T>;
}

export const api = {
	listConnections: () => request<SavedConnection[]>("/connections"),
	saveConnection: (payload: SavedConnection) =>
		request<SavedConnection>("/connections", { method: "POST", body: JSON.stringify(payload) }),
	deleteConnection: (id: string) => request<void>(`/connections/${id}`, { method: "DELETE" }),
	getSchema: (connectionId: string) => request<SchemaSummary>(`/explorer/schema/${connectionId}`),
	previewTable: (connectionId: string, schemaName: string, tableName: string, limit = 100) =>
		request<TablePreview>(
			`/explorer/preview/${connectionId}?schemaName=${encodeURIComponent(schemaName)}&tableName=${encodeURIComponent(tableName)}&limit=${limit}`,
		),
	runQuery: (connectionId: string, sql: string) =>
		request<QueryResult>(`/query/run/${connectionId}`, {
			method: "POST",
			body: JSON.stringify({ sql }),
		}),
	listSavedQueries: (connectionId: string) =>
		request<SavedQueryItem[]>(`/saved-queries/${connectionId}`),
	saveSavedQuery: (payload: {
		id?: string;
		connectionId: string;
		title: string;
		sql: string;
		tags: string[];
	}) =>
		request<SavedQueryItem>("/saved-queries", { method: "POST", body: JSON.stringify(payload) }),
	updateSavedQuery: (
		id: string,
		payload: { connectionId: string; title: string; sql: string; tags: string[] },
	) =>
		request<SavedQueryItem>(`/saved-queries/${id}`, {
			method: "PUT",
			body: JSON.stringify(payload),
		}),
	deleteSavedQuery: (id: string) => request<void>(`/saved-queries/${id}`, { method: "DELETE" }),
	listQueryHistory: (connectionId: string, limit = 100) =>
		request<QueryHistoryItem[]>(`/query/history/${connectionId}?limit=${limit}`),
	captureSnapshot: (connectionId: string, name?: string) =>
		request<SchemaSnapshot>(`/snapshots/capture/${connectionId}`, {
			method: "POST",
			body: JSON.stringify({ name }),
		}),
	listSnapshots: (connectionId: string) =>
		request<SchemaSnapshotSummary[]>(`/snapshots/${connectionId}`),
	getSnapshot: (snapshotId: string) => request<SchemaSnapshot>(`/snapshots/item/${snapshotId}`),
	compareSnapshots: (sourceSnapshotId: string, targetSnapshotId: string) =>
		request<SchemaDiffResult>("/schema-diff/compare", {
			method: "POST",
			body: JSON.stringify({ sourceSnapshotId, targetSnapshotId }),
		}),
	getSchemaDiffDetails: (sourceSnapshotId: string, targetSnapshotId: string) =>
		request<SchemaDiffResult>(
			`/schema-diff/details?sourceSnapshotId=${encodeURIComponent(sourceSnapshotId)}&targetSnapshotId=${encodeURIComponent(targetSnapshotId)}`,
		),
	exportSchemaDiffJson: async (sourceSnapshotId: string, targetSnapshotId: string) => {
		const response = await fetch(
			`${API_BASE}/schema-diff/export/json?sourceSnapshotId=${encodeURIComponent(sourceSnapshotId)}&targetSnapshotId=${encodeURIComponent(targetSnapshotId)}`,
		);

		if (!response.ok) {
			throw new Error((await response.text()) || "Failed to export diff as JSON");
		}

		return response.blob();
	},
	exportSchemaDiffHtml: async (sourceSnapshotId: string, targetSnapshotId: string) => {
		const response = await fetch(
			`${API_BASE}/schema-diff/export/html?sourceSnapshotId=${encodeURIComponent(sourceSnapshotId)}&targetSnapshotId=${encodeURIComponent(targetSnapshotId)}`,
		);

		if (!response.ok) {
			throw new Error((await response.text()) || "Failed to export diff as HTML");
		}

		return response.blob();
	},
	listMigrations: (connectionId: string) =>
		request<MigrationItem[]>(`/migrations/${connectionId}`),
	getMigration: (migrationId: string) =>
		request<MigrationItem>(`/migrations/item/${migrationId}`),
	saveMigration: (payload: {
		id?: string;
		connectionId: string;
		name: string;
		description?: string;
		upScript: string;
		downScript: string;
		status?: string;
		sourceSnapshotId?: string;
		targetSnapshotId?: string;
	}) =>
		request<MigrationItem>("/migrations", { method: "POST", body: JSON.stringify(payload) }),
	updateMigration: (
		id: string,
		payload: {
			connectionId: string;
			name: string;
			description?: string;
			upScript: string;
			downScript: string;
			status?: string;
			sourceSnapshotId?: string;
			targetSnapshotId?: string;
		},
	) =>
		request<MigrationItem>(`/migrations/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
	deleteMigration: (id: string) => request<void>(`/migrations/${id}`, { method: "DELETE" }),
	generateMigrationFromDiff: (payload: {
		connectionId: string;
		sourceSnapshotId: string;
		targetSnapshotId: string;
		name?: string;
		description?: string;
	}) =>
		request<MigrationItem>("/migrations/generate-from-diff", {
			method: "POST",
			body: JSON.stringify(payload),
		}),
	applyMigration: (connectionId: string, migrationId: string, confirmDestructive: boolean) =>
		request<MigrationExecutionRun>(`/migrations/${connectionId}/${migrationId}/apply`, {
			method: "POST",
			body: JSON.stringify({ confirmDestructive }),
		}),
	rollbackMigration: (connectionId: string, migrationId: string, confirmDestructive: boolean) =>
		request<MigrationExecutionRun>(`/migrations/${connectionId}/${migrationId}/rollback`, {
			method: "POST",
			body: JSON.stringify({ confirmDestructive }),
		}),
	listMigrationHistoryByConnection: (connectionId: string, limit = 200) =>
		request<MigrationExecutionRun[]>(`/migrations/${connectionId}/history?limit=${limit}`),
	listMigrationHistoryByMigration: (migrationId: string, limit = 200) =>
		request<MigrationExecutionRun[]>(`/migrations/history/${migrationId}?limit=${limit}`),
};
