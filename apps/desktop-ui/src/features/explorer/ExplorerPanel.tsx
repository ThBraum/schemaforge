import { useEffect, useState } from 'react';
import { api } from '../../api/client';
import type { TranslationKey } from '../../i18n';
import { MigrationWorkspace } from '../migrations/MigrationWorkspace';
import type {
  QueryHistoryItem,
  QueryResult,
  SavedConnection,
  SavedQueryItem,
  SchemaDiffResult,
  SchemaSnapshot,
  SchemaSnapshotSummary,
  SchemaSummary,
  TableNode,
  TablePreview,
} from '../../types';

interface Props {
  connection: SavedConnection | null;
  t: (key: TranslationKey) => string;
}

export function ExplorerPanel({ connection, t }: Props) {
  const [schema, setSchema] = useState<SchemaSummary | null>(null);
  const [selectedTable, setSelectedTable] = useState<TableNode | null>(null);
  const [preview, setPreview] = useState<TablePreview | null>(null);
  const [sql, setSql] = useState('select 1 as status;');
  const [queryResult, setQueryResult] = useState<QueryResult | null>(null);
  const [savedQueries, setSavedQueries] = useState<SavedQueryItem[]>([]);
  const [savedQueryTitle, setSavedQueryTitle] = useState('');
  const [savedQueryTags, setSavedQueryTags] = useState('');
  const [editingSavedQueryId, setEditingSavedQueryId] = useState<string | null>(null);
  const [queryHistory, setQueryHistory] = useState<QueryHistoryItem[]>([]);
  const [snapshots, setSnapshots] = useState<SchemaSnapshotSummary[]>([]);
  const [snapshotName, setSnapshotName] = useState('');
  const [selectedSnapshot, setSelectedSnapshot] = useState<SchemaSnapshot | null>(null);
  const [sourceSnapshotId, setSourceSnapshotId] = useState('');
  const [targetSnapshotId, setTargetSnapshotId] = useState('');
  const [schemaDiff, setSchemaDiff] = useState<SchemaDiffResult | null>(null);
  const [isCapturingSnapshot, setIsCapturingSnapshot] = useState(false);
  const [isComparingSnapshots, setIsComparingSnapshots] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadInitialState() {
      if (!connection) {
        setSchema(null);
        setSelectedTable(null);
        setPreview(null);
        setSavedQueries([]);
        setQueryHistory([]);
        setSnapshots([]);
        setSelectedSnapshot(null);
        setSourceSnapshotId('');
        setTargetSnapshotId('');
        setSchemaDiff(null);
        setEditingSavedQueryId(null);
        setSavedQueryTitle('');
        setSavedQueryTags('');
        return;
      }

      try {
        setError(null);
        const [schemaResult, savedQueriesResult, historyResult, snapshotsResult] = await Promise.all([
          api.getSchema(connection.id),
          api.listSavedQueries(connection.id),
          api.listQueryHistory(connection.id, 50),
          api.listSnapshots(connection.id),
        ]);

        setSchema(schemaResult);
        setSavedQueries(savedQueriesResult);
        setQueryHistory(historyResult);
        setSnapshots(snapshotsResult);
        setSchemaDiff(null);

        const latestSnapshot = snapshotsResult[0];
        const previousSnapshot = snapshotsResult[1];
        setTargetSnapshotId(latestSnapshot?.id ?? '');
        setSourceSnapshotId(previousSnapshot?.id ?? '');

        const firstTable = schemaResult.schemas.flatMap((item) => item.tables)[0] ?? null;
        setSelectedTable(firstTable);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Falha ao carregar schema');
      }
    }

    loadInitialState();
  }, [connection]);

  useEffect(() => {
    async function loadPreview() {
      if (!connection || !selectedTable) {
        setPreview(null);
        return;
      }

      try {
        const result = await api.previewTable(connection.id, selectedTable.schemaName, selectedTable.tableName);
        setPreview(result);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Falha ao carregar preview');
      }
    }

    loadPreview();
  }, [connection, selectedTable]);

  async function handleRunQuery() {
    if (!connection) return;
    try {
      setError(null);
      const result = await api.runQuery(connection.id, sql);
      setQueryResult(result);
      const history = await api.listQueryHistory(connection.id, 50);
      setQueryHistory(history);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao executar query');
    }
  }

  async function handleSaveCurrentQuery() {
    if (!connection) return;

    try {
      setError(null);

      const tags = savedQueryTags
        .split(',')
        .map((item) => item.trim())
        .filter(Boolean);

      const title = savedQueryTitle.trim() || `Query ${new Date().toLocaleString()}`;
      if (editingSavedQueryId) {
        await api.updateSavedQuery(editingSavedQueryId, { connectionId: connection.id, title, sql, tags });
      } else {
        await api.saveSavedQuery({ connectionId: connection.id, title, sql, tags });
      }

      const result = await api.listSavedQueries(connection.id);
      setSavedQueries(result);
      setEditingSavedQueryId(null);
      setSavedQueryTitle('');
      setSavedQueryTags('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao salvar query');
    }
  }

  async function handleDeleteSavedQuery(id: string) {
    if (!connection) return;

    try {
      setError(null);
      await api.deleteSavedQuery(id);
      const result = await api.listSavedQueries(connection.id);
      setSavedQueries(result);

      if (editingSavedQueryId === id) {
        setEditingSavedQueryId(null);
        setSavedQueryTitle('');
        setSavedQueryTags('');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao excluir query');
    }
  }

  function handleEditSavedQuery(query: SavedQueryItem) {
    setEditingSavedQueryId(query.id);
    setSavedQueryTitle(query.title);
    setSavedQueryTags(query.tags.join(', '));
    setSql(query.sql);
  }

  function handleOpenSavedQuery(query: SavedQueryItem) {
    setSql(query.sql);
  }

  async function handleCaptureSnapshot() {
    if (!connection) return;

    try {
      setError(null);
      setIsCapturingSnapshot(true);

      const snapshot = await api.captureSnapshot(connection.id, snapshotName.trim() || undefined);
      const [allSnapshots, snapshotDetails] = await Promise.all([
        api.listSnapshots(connection.id),
        api.getSnapshot(snapshot.id),
      ]);

      setSnapshots(allSnapshots);
      setSourceSnapshotId((current) => current || allSnapshots[1]?.id || '');
      setTargetSnapshotId(allSnapshots[0]?.id || snapshot.id);
      setSelectedSnapshot(snapshotDetails);
      setSnapshotName('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao capturar snapshot');
    } finally {
      setIsCapturingSnapshot(false);
    }
  }

  async function handleViewSnapshot(snapshotId: string) {
    try {
      setError(null);
      const snapshot = await api.getSnapshot(snapshotId);
      setSelectedSnapshot(snapshot);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao carregar snapshot');
    }
  }

  async function handleCompareSnapshots() {
    if (!sourceSnapshotId || !targetSnapshotId || sourceSnapshotId === targetSnapshotId) {
      setError(t('schemaDiffSelectDifferentSnapshotsError'));
      return;
    }

    try {
      setError(null);
      setIsComparingSnapshots(true);
      const diff = await api.compareSnapshots(sourceSnapshotId, targetSnapshotId);
      setSchemaDiff(diff);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao comparar snapshots');
    } finally {
      setIsComparingSnapshots(false);
    }
  }

  async function handleExportSchemaDiff(format: 'json' | 'html') {
    if (!sourceSnapshotId || !targetSnapshotId) {
      setError(t('schemaDiffSelectSnapshotsForExportError'));
      return;
    }

    try {
      setError(null);
      const blob = format === 'json'
        ? await api.exportSchemaDiffJson(sourceSnapshotId, targetSnapshotId)
        : await api.exportSchemaDiffHtml(sourceSnapshotId, targetSnapshotId);

      const fileName = `schema-diff-${sourceSnapshotId}-${targetSnapshotId}.${format}`;
      const url = window.URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      window.setTimeout(() => {
        window.URL.revokeObjectURL(url);
      }, 0);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao exportar schema diff');
    }
  }

  if (!connection) {
    return (
      <div className="card empty-state">
        <p className="eyebrow">{t('explorerTitle')}</p>
        <h2>{t('noConnectionSelected')}</h2>
        <p className="small">{t('connectionsEmptyHint')}</p>
      </div>
    );
  }

  return (
    <div className="stack">
      <div className="card hero-card">
        <div className="hero-copy">
          <p className="eyebrow">{t('explorerTitle')}</p>
          <h2>{connection.name}</h2>
          <p className="small">{t('connectedTo')} <strong>{connection.databaseType}</strong> · {connection.host}:{connection.port}</p>
        </div>
        <div className="hero-badges">
          <span className="badge">{connection.database}</span>
          <span className="badge subtle">{connection.username}</span>
        </div>
        {error ? <p className="error-banner">{error}</p> : null}
      </div>

      <div className="grid-2">
        <div className="card panel-card">
          <div className="card-header">
            <div>
              <p className="eyebrow">{t('schemasTitle')}</p>
              <h3>{t('schemasTitle')}</h3>
            </div>
            <p className="small">{schema?.databaseName ?? t('schemaEmpty')}</p>
          </div>
          <div className="list">
            {schema?.schemas.map((schemaNode) => (
              <div key={schemaNode.schemaName}>
                <div className="schema-label">{schemaNode.schemaName}</div>
                {schemaNode.tables.map((table) => (
                  <button
                    key={`${table.schemaName}.${table.tableName}`}
                    className={`table-item ${selectedTable?.tableName === table.tableName && selectedTable.schemaName === table.schemaName ? 'active' : ''}`}
                    onClick={() => setSelectedTable(table)}
                  >
                    {table.tableName}
                  </button>
                ))}
              </div>
            ))}
          </div>
        </div>

        <div className="stack">
          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('structureTitle')}</p>
                <h3>{t('structureTitle')}</h3>
              </div>
              <p className="small">{selectedTable ? `${selectedTable.schemaName}.${selectedTable.tableName}` : t('selectedTableEmpty')}</p>
            </div>
            {!selectedTable ? <p>{t('selectedTableEmpty')}</p> : (
              <>
                <table className="table">
                  <thead>
                    <tr>
                      <th>Coluna</th>
                      <th>Tipo</th>
                      <th>Nulo</th>
                      <th>Default</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedTable.columns.map((column) => (
                      <tr key={column.name}>
                        <td>{column.name}</td>
                        <td>{column.dataType}</td>
                        <td>{column.isNullable ? t('yes') : t('no')}</td>
                        <td>{column.defaultValue ?? t('nullLabel')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            )}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('previewTitle')}</p>
                <h3>{t('previewTitle')}</h3>
              </div>
              {preview ? <p className="small">{preview.rows.length} {t('rowsLabel')}</p> : null}
            </div>
            {!preview ? <p>{t('previewEmpty')}</p> : (
              <table className="table">
                <thead>
                  <tr>{preview.columns.map((column) => <th key={column}>{column}</th>)}</tr>
                </thead>
                <tbody>
                  {preview.rows.map((row, index) => (
                    <tr key={index}>
                      {preview.columns.map((column) => <td key={column}>{String(row[column] ?? '')}</td>)}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('queryRunnerTitle')}</p>
                <h3>{t('queryRunnerTitle')}</h3>
              </div>
              <p className="small">SQL</p>
            </div>

            <div className="row">
              <input
                value={savedQueryTitle}
                onChange={(event) => setSavedQueryTitle(event.target.value)}
                placeholder={t('savedQueryTitleLabel')}
                style={{ flex: 1 }}
              />
              <input
                value={savedQueryTags}
                onChange={(event) => setSavedQueryTags(event.target.value)}
                placeholder={t('savedQueryTagsLabel')}
                style={{ flex: 1 }}
              />
            </div>

            <textarea value={sql} onChange={(e) => setSql(e.target.value)} placeholder={t('queryPlaceholder')} />
            <div className="row" style={{ marginTop: 12 }}>
              <button className="primary-button" onClick={handleRunQuery}>{t('runQuery')}</button>
              <button onClick={handleSaveCurrentQuery}>{t('saveCurrentQuery')}</button>
              <button
                onClick={() => {
                  setEditingSavedQueryId(null);
                  setSavedQueryTitle('');
                  setSavedQueryTags('');
                }}
              >
                {t('newSavedQuery')}
              </button>
            </div>
            {queryResult ? (
              <>
                <p className="small">{queryResult.rowCount} {t('rowsLabel')} · {queryResult.durationMs} {t('msLabel')}</p>
                <table className="table">
                  <thead>
                    <tr>{queryResult.columns.map((column) => <th key={column}>{column}</th>)}</tr>
                  </thead>
                  <tbody>
                    {queryResult.rows.map((row, index) => (
                      <tr key={index}>
                        {queryResult.columns.map((column) => <td key={column}>{String(row[column] ?? '')}</td>)}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </>
            ) : null}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('savedQueriesTitle')}</p>
                <h3>{t('savedQueriesTitle')}</h3>
              </div>
              <span className="badge subtle">{savedQueries.length}</span>
            </div>

            {savedQueries.length === 0 ? (
              <p className="small">{t('savedQueriesEmpty')}</p>
            ) : (
              <div className="list">
                {savedQueries.map((query) => (
                  <div key={query.id} className="list-item-card">
                    <div>
                      <strong>{query.title}</strong>
                      <p className="small">{query.tags.join(', ') || '-'}</p>
                    </div>
                    <div className="row">
                      <button onClick={() => handleOpenSavedQuery(query)}>{t('openSavedQuery')}</button>
                      <button onClick={() => handleEditSavedQuery(query)}>{t('editSavedQuery')}</button>
                      <button onClick={() => handleDeleteSavedQuery(query.id)}>{t('deleteSavedQuery')}</button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('queryHistoryTitle')}</p>
                <h3>{t('queryHistoryTitle')}</h3>
              </div>
              <span className="badge subtle">{queryHistory.length}</span>
            </div>

            {queryHistory.length === 0 ? (
              <p className="small">{t('queryHistoryEmpty')}</p>
            ) : (
              <div className="list">
                {queryHistory.map((item) => (
                  <div key={item.id} className="list-item-card">
                    <div>
                      <strong>{item.status}</strong>
                      <p className="small">{item.durationMs} {t('msLabel')} · {new Date(item.executedAtUtc).toLocaleString()}</p>
                      <p className="small truncate-line">{item.sql}</p>
                    </div>
                    <div className="row">
                      <button onClick={() => setSql(item.sql)}>{t('reopenQuery')}</button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('snapshotTitle')}</p>
                <h3>{t('snapshotTitle')}</h3>
              </div>
              <span className="badge subtle">{snapshots.length}</span>
            </div>

            <div className="row">
              <input
                value={snapshotName}
                onChange={(event) => setSnapshotName(event.target.value)}
                placeholder={t('snapshotNamePlaceholder')}
                style={{ flex: 1 }}
              />
              <button className="primary-button" onClick={handleCaptureSnapshot} disabled={isCapturingSnapshot}>
                {isCapturingSnapshot ? '...' : t('captureSnapshot')}
              </button>
            </div>

            {snapshots.length === 0 ? (
              <p className="small">{t('snapshotsEmpty')}</p>
            ) : (
              <div className="list">
                {snapshots.map((item) => (
                  <div key={item.id} className="list-item-card">
                    <div>
                      <strong>{item.name || item.id}</strong>
                      <p className="small">{new Date(item.createdAtUtc).toLocaleString()}</p>
                      <p className="small">{item.schemaCount} schemas · {item.tableCount} {t('schemasTitle').toLowerCase()}</p>
                    </div>
                    <div className="row">
                      <button onClick={() => handleViewSnapshot(item.id)}>{t('viewSnapshot')}</button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('snapshotDetailsTitle')}</p>
                <h3>{t('snapshotDetailsTitle')}</h3>
              </div>
            </div>

            {!selectedSnapshot ? (
              <p className="small">{t('snapshotsEmpty')}</p>
            ) : (
              <div className="list">
                <p className="small"><strong>{selectedSnapshot.name || selectedSnapshot.id}</strong> · {new Date(selectedSnapshot.createdAtUtc).toLocaleString()}</p>
                {selectedSnapshot.structure.schemas.map((schemaNode) => (
                  <details key={schemaNode.schemaName} className="snapshot-details">
                    <summary>{schemaNode.schemaName} ({schemaNode.tables.length})</summary>
                    <div className="list">
                      {schemaNode.tables.map((table) => (
                        <div key={`${table.schemaName}.${table.tableName}`} className="list-item-card compact">
                          <strong>{table.tableName}</strong>
                          <p className="small">Cols: {table.columns.length} · PK: {table.primaryKeyColumns.join(', ') || '-'}</p>
                          <p className="small">FK: {table.foreignKeys.length} · IDX: {table.indexes.length}</p>
                        </div>
                      ))}
                    </div>
                  </details>
                ))}
              </div>
            )}
          </div>

          <div className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t('schemaDiffTitle')}</p>
                <h3>{t('schemaDiffTitle')}</h3>
              </div>
              <div className="row">
                <button onClick={() => handleExportSchemaDiff('json')} disabled={!sourceSnapshotId || !targetSnapshotId}>{t('exportJson')}</button>
                <button onClick={() => handleExportSchemaDiff('html')} disabled={!sourceSnapshotId || !targetSnapshotId}>{t('exportHtml')}</button>
              </div>
            </div>

            <div className="row">
              <div className="field" style={{ flex: 1 }}>
                <label>{t('sourceSnapshotLabel')}</label>
                <select value={sourceSnapshotId} onChange={(event) => setSourceSnapshotId(event.target.value)}>
                  <option value="">-</option>
                  {snapshots.map((snapshot) => (
                    <option key={`source-${snapshot.id}`} value={snapshot.id}>
                      {(snapshot.name || snapshot.id)} · {new Date(snapshot.createdAtUtc).toLocaleString()}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field" style={{ flex: 1 }}>
                <label>{t('targetSnapshotLabel')}</label>
                <select value={targetSnapshotId} onChange={(event) => setTargetSnapshotId(event.target.value)}>
                  <option value="">-</option>
                  {snapshots.map((snapshot) => (
                    <option key={`target-${snapshot.id}`} value={snapshot.id}>
                      {(snapshot.name || snapshot.id)} · {new Date(snapshot.createdAtUtc).toLocaleString()}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="row">
              <button className="primary-button" onClick={handleCompareSnapshots} disabled={isComparingSnapshots || !sourceSnapshotId || !targetSnapshotId || sourceSnapshotId === targetSnapshotId}>
                {isComparingSnapshots ? '...' : t('compareSnapshots')}
              </button>
            </div>

            {!schemaDiff ? (
              <p className="small">{t('schemaDiffEmpty')}</p>
            ) : (
              <>
                <div className="card diff-summary-card">
                  <p className="eyebrow">{t('schemaDiffSummaryTitle')}</p>
                  <div className="diff-summary-grid">
                    <div><strong>{t('tablesAddedLabel')}:</strong> {schemaDiff.summary.tablesAdded}</div>
                    <div><strong>{t('tablesRemovedLabel')}:</strong> {schemaDiff.summary.tablesRemoved}</div>
                    <div><strong>{t('tablesModifiedLabel')}:</strong> {schemaDiff.summary.tablesModified}</div>
                    <div><strong>{t('columnsAddedLabel')}:</strong> {schemaDiff.summary.columnsAdded}</div>
                    <div><strong>{t('columnsRemovedLabel')}:</strong> {schemaDiff.summary.columnsRemoved}</div>
                    <div><strong>{t('columnsModifiedLabel')}:</strong> {schemaDiff.summary.columnsModified}</div>
                    <div><strong>{t('breakingChangesLabel')}:</strong> {schemaDiff.summary.breakingChanges}</div>
                  </div>
                </div>

                <details className="snapshot-details" open>
                  <summary>{t('tablesAddedLabel')} ({schemaDiff.tablesAdded.length})</summary>
                  <div className="list">
                    {schemaDiff.tablesAdded.length === 0 ? <p className="small">-</p> : schemaDiff.tablesAdded.map((item) => (
                      <div key={`added-${item.schemaName}.${item.tableName}`} className="list-item-card compact">
                        <strong>{item.schemaName}.{item.tableName}</strong>
                      </div>
                    ))}
                  </div>
                </details>

                <details className="snapshot-details" open>
                  <summary>{t('tablesRemovedLabel')} ({schemaDiff.tablesRemoved.length})</summary>
                  <div className="list">
                    {schemaDiff.tablesRemoved.length === 0 ? <p className="small">-</p> : schemaDiff.tablesRemoved.map((item) => (
                      <div key={`removed-${item.schemaName}.${item.tableName}`} className="list-item-card compact">
                        <strong>{item.schemaName}.{item.tableName}</strong>
                      </div>
                    ))}
                  </div>
                </details>

                <details className="snapshot-details" open>
                  <summary>{t('modifiedTablesLabel')} ({schemaDiff.tablesModified.length})</summary>
                  <div className="list">
                    {schemaDiff.tablesModified.length === 0 ? <p className="small">-</p> : schemaDiff.tablesModified.map((table) => (
                      <details key={`modified-${table.schemaName}.${table.tableName}`} className="snapshot-details">
                        <summary>{table.schemaName}.{table.tableName}</summary>
                        <div className="list">
                          <p className="small">{t('columnsAddedLabel')}: {table.columnsAdded.length} · {t('columnsRemovedLabel')}: {table.columnsRemoved.length} · {t('columnsModifiedLabel')}: {table.columnsModified.length}</p>
                          <p className="small">{t('constraintsChangedLabel')}: {table.primaryKeyChanged ? 1 : 0} + {table.foreignKeysAdded.length + table.foreignKeysRemoved.length}</p>
                          <p className="small">{t('indexesChangedLabel')}: {table.indexesAdded.length + table.indexesRemoved.length}</p>

                          {table.columnsModified.map((column) => (
                            <div key={`${table.schemaName}.${table.tableName}.${column.columnName}`} className={`list-item-card compact ${column.isBreakingChange ? 'breaking-change' : ''}`}>
                              <strong>{column.columnName}</strong>
                              <p className="small">{t('beforeLabel')}: {column.sourceDataType} · {column.sourceIsNullable ? t('yes') : t('no')}</p>
                              <p className="small">{t('afterLabel')}: {column.targetDataType} · {column.targetIsNullable ? t('yes') : t('no')}</p>
                            </div>
                          ))}

                          {table.primaryKeyChanged ? (
                            <div className="list-item-card compact">
                              <strong>PK</strong>
                              <p className="small">{t('beforeLabel')}: {table.sourcePrimaryKeyColumns.join(', ') || '-'}</p>
                              <p className="small">{t('afterLabel')}: {table.targetPrimaryKeyColumns.join(', ') || '-'}</p>
                            </div>
                          ) : null}
                        </div>
                      </details>
                    ))}
                  </div>
                </details>

                <details className="snapshot-details">
                  <summary>{t('breakingChangesLabel')} ({schemaDiff.breakingChanges.length})</summary>
                  <div className="list">
                    {schemaDiff.breakingChanges.length === 0 ? <p className="small">-</p> : schemaDiff.breakingChanges.map((item) => (
                      <div key={`breaking-${item.category}-${item.schemaName}-${item.tableName}-${item.columnName ?? 'none'}-${item.description}`} className="list-item-card compact breaking-change">
                        <strong>{item.schemaName}.{item.tableName}{item.columnName ? `.${item.columnName}` : ''}</strong>
                        <p className="small">{item.description}</p>
                      </div>
                    ))}
                  </div>
                </details>
              </>
            )}
          </div>
        </div>
      </div>

      <MigrationWorkspace connectionId={connection.id} t={t} />
    </div>
  );
}
