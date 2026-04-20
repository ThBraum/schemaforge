import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/client';
import { ConnectionForm } from '../components/ConnectionForm';
import { getTranslation, languages } from '../i18n';
import { ExplorerPanel } from '../features/explorer/ExplorerPanel';
import type { Language, SavedConnection, ThemeMode } from '../types';

const THEME_STORAGE_KEY = 'schemaforge.theme';
const LANGUAGE_STORAGE_KEY = 'schemaforge.language';

const languageDetails: Record<Language, { emoji: string; shortLabel: string; menuLabel: string }> = {
  en: { emoji: '🇺🇸', shortLabel: 'EN(US)', menuLabel: 'English' },
  'pt-BR': { emoji: '🇧🇷', shortLabel: 'PT(BR)', menuLabel: 'Português' },
};

export function App() {
  const [connections, setConnections] = useState<SavedConnection[]>([]);
  const [selectedConnectionId, setSelectedConnectionId] = useState<string | null>(null);
  const [theme, setTheme] = useState<ThemeMode>('dark');
  const [language, setLanguage] = useState<Language>('en');
  const [isLanguageMenuOpen, setIsLanguageMenuOpen] = useState(false);
  const languageMenuRef = useRef<HTMLDivElement>(null);
  const selectedConnection = connections.find((item) => item.id === selectedConnectionId) ?? null;
  const t = useMemo(() => getTranslation(language), [language]);

  async function loadConnections(nextSelectedId?: string | null) {
    const result = await api.listConnections();
    setConnections(result);
    setSelectedConnectionId((current) => {
      if (nextSelectedId) return nextSelectedId;
      if (current && result.some((item) => item.id === current)) return current;
      return result[0]?.id ?? null;
    });
  }

  useEffect(() => {
    const savedTheme = window.localStorage.getItem(THEME_STORAGE_KEY) as ThemeMode | null;
    const savedLanguage = window.localStorage.getItem(LANGUAGE_STORAGE_KEY) as Language | null;

    if (savedTheme === 'dark' || savedTheme === 'light') {
      setTheme(savedTheme);
    }

    if (savedLanguage === 'en' || savedLanguage === 'pt-BR') {
      setLanguage(savedLanguage);
    }

    loadConnections().catch(console.error);
  }, []);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    window.localStorage.setItem(THEME_STORAGE_KEY, theme);
  }, [theme]);

  useEffect(() => {
    document.documentElement.lang = language;
    window.localStorage.setItem(LANGUAGE_STORAGE_KEY, language);
  }, [language]);

  useEffect(() => {
    function handlePointerDown(event: PointerEvent) {
      if (!languageMenuRef.current?.contains(event.target as Node)) {
        setIsLanguageMenuOpen(false);
      }
    }

    document.addEventListener('pointerdown', handlePointerDown);
    return () => document.removeEventListener('pointerdown', handlePointerDown);
  }, []);

  async function handleSave(connection: SavedConnection) {
    await api.saveConnection(connection);
    await loadConnections(connection.id);
  }

  async function handleDeleteConnection(connectionId: string) {
    if (!window.confirm(t.deleteConnectionConfirm)) {
      return;
    }

    await api.deleteConnection(connectionId);
    const nextSelectedId = selectedConnectionId === connectionId ? null : selectedConnectionId;
    await loadConnections(nextSelectedId);
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">SchemaForge</p>
          <h1>{t.appTitle}</h1>
          <p className="small">{t.appSubtitle}</p>
        </div>
        <div className="topbar-controls">
          <div className="control-group">
            <span className="control-label">{t.themeLabel}</span>
            <button
              className="toggle-button"
              type="button"
              aria-pressed={theme === 'light'}
              onClick={() => setTheme((current) => (current === 'dark' ? 'light' : 'dark'))}
            >
              <span className="toggle-track">
                <span className={`toggle-thumb ${theme}`}>{theme === 'dark' ? '◐' : '◑'}</span>
              </span>
              <span>{theme === 'dark' ? 'Dark' : 'Light'}</span>
            </button>
          </div>
          <div className="control-group language-menu" ref={languageMenuRef}>
            <span className="control-label">{t.languageLabel}</span>
            <button
              className="language-button"
              type="button"
              aria-haspopup="menu"
              aria-expanded={isLanguageMenuOpen}
              onClick={() => setIsLanguageMenuOpen((current) => !current)}
            >
              <span className="language-button-content">
                <span className="language-flag" aria-hidden="true">{languageDetails[language].emoji}</span>
                <span>{languageDetails[language].shortLabel}</span>
              </span>
              <span className="language-caret">▾</span>
            </button>
            {isLanguageMenuOpen ? (
              <div className="language-popover" role="menu" aria-label={t.languageLabel}>
                {languages.map((item) => {
                  const details = languageDetails[item.value];
                  const active = item.value === language;

                  return (
                    <button
                      key={item.value}
                      className={`language-option ${active ? 'active' : ''}`}
                      type="button"
                      role="menuitemradio"
                      aria-checked={active}
                      onClick={() => {
                        setLanguage(item.value);
                        setIsLanguageMenuOpen(false);
                      }}
                    >
                      <div>
                        <strong>{details.menuLabel}</strong>
                        <span>{details.shortLabel}</span>
                      </div>
                      <span className="language-flag" aria-hidden="true">{details.emoji}</span>
                    </button>
                  );
                })}
              </div>
            ) : null}
          </div>
        </div>
      </header>

      <div className="app-layout">
        <aside className="sidebar">
          <ConnectionForm onSave={handleSave} t={(key) => t[key]} />
          <section className="card panel-card">
            <div className="card-header">
              <div>
                <p className="eyebrow">{t.connectionsTitle}</p>
                <h3>{t.connectionsTitle}</h3>
              </div>
              <span className="badge subtle">{connections.length}</span>
            </div>
            <div className="list">
              {connections.length === 0 ? (
                <div className="empty-inline">
                  <strong>{t.connectionsEmpty}</strong>
                  <p className="small">{t.connectionsEmptyHint}</p>
                </div>
              ) : connections.map((connection) => (
                <div key={connection.id} className={`connection-item ${selectedConnectionId === connection.id ? 'active' : ''}`}>
                  <button
                    className="connection-main"
                    type="button"
                    onClick={() => setSelectedConnectionId(connection.id)}
                  >
                    <span className="connection-name">{connection.name}</span>
                    <span className="small">{connection.databaseType} · {connection.host}:{connection.port}</span>
                  </button>
                  <button
                    className="danger-ghost"
                    type="button"
                    onClick={() => handleDeleteConnection(connection.id)}
                    aria-label={t.deleteConnection}
                    title={t.deleteConnection}
                  >
                    {t.deleteConnection}
                  </button>
                </div>
              ))}
            </div>
          </section>
        </aside>
        <main className="content">
          <ExplorerPanel connection={selectedConnection} t={(key) => t[key]} />
        </main>
      </div>
    </div>
  );
}
