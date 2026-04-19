import { useState } from 'react';
import type { TranslationKey } from '../i18n';
import type { SavedConnection } from '../types';

const emptyForm: SavedConnection = {
  id: '',
  name: '',
  databaseType: 'postgres',
  host: 'localhost',
  port: 5432,
  database: '',
  username: '',
  password: '',
};

interface Props {
  onSave: (connection: SavedConnection) => Promise<void>;
  t: (key: TranslationKey) => string;
}

export function ConnectionForm({ onSave, t }: Props) {
  const [form, setForm] = useState<SavedConnection>(emptyForm);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSaving(true);
    try {
      await onSave({ ...form, id: crypto.randomUUID() });
      setForm(emptyForm);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="card connection-form" onSubmit={handleSubmit}>
      <div className="card-header">
        <div>
          <p className="eyebrow">{t('newConnectionTitle')}</p>
          <h3>{t('newConnectionTitle')}</h3>
        </div>
        <p className="small">{t('connectionFormHint')}</p>
      </div>
      <div className="field">
        <label>{t('nameLabel')}</label>
        <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
      </div>
      <div className="row">
        <div className="field" style={{ flex: 1 }}>
          <label>{t('databaseLabel')}</label>
          <select
            value={form.databaseType}
            onChange={(e) => {
              const databaseType = e.target.value as SavedConnection['databaseType'];
              setForm({
                ...form,
                databaseType,
                port: databaseType === 'postgres' ? 5432 : 3306,
              });
            }}
          >
            <option value="postgres">PostgreSQL</option>
            <option value="mysql">MySQL</option>
          </select>
        </div>
        <div className="field" style={{ width: 120 }}>
          <label>{t('portLabel')}</label>
          <input type="number" value={form.port} onChange={(e) => setForm({ ...form, port: Number(e.target.value) })} required />
        </div>
      </div>
      <div className="field">
        <label>{t('hostLabel')}</label>
        <input value={form.host} onChange={(e) => setForm({ ...form, host: e.target.value })} required />
      </div>
      <div className="field">
        <label>{t('databaseNameLabel')}</label>
        <input value={form.database} onChange={(e) => setForm({ ...form, database: e.target.value })} required />
      </div>
      <div className="field">
        <label>{t('usernameLabel')}</label>
        <input value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} required />
      </div>
      <div className="field">
        <label>{t('passwordLabel')}</label>
        <input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
      </div>
      <button className="primary-button" type="submit" disabled={saving}>{saving ? t('savingConnection') : t('saveConnection')}</button>
    </form>
  );
}
