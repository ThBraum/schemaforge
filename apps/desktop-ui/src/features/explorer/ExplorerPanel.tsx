import { useEffect, useState } from 'react';
import { api } from '../../api/client';
import type { TranslationKey } from '../../i18n';
import type { QueryResult, SavedConnection, SchemaSummary, TableNode, TablePreview } from '../../types';

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
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadSchema() {
      if (!connection) {
        setSchema(null);
        setSelectedTable(null);
        setPreview(null);
        return;
      }

      try {
        setError(null);
        const result = await api.getSchema(connection.id);
        setSchema(result);
        const firstTable = result.schemas.flatMap((item) => item.tables)[0] ?? null;
        setSelectedTable(firstTable);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Falha ao carregar schema');
      }
    }

    loadSchema();
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
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao executar query');
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
            <textarea value={sql} onChange={(e) => setSql(e.target.value)} placeholder={t('queryPlaceholder')} />
            <div className="row" style={{ marginTop: 12 }}>
              <button className="primary-button" onClick={handleRunQuery}>{t('runQuery')}</button>
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
        </div>
      </div>
    </div>
  );
}
