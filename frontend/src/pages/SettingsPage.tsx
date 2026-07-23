import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { pageTransition } from '../utils/animations';
import '../styles/pages/settings.css';

const AI_MODELS = [
  { value: 'deepseek-chat', label: 'DeepSeek — deepseek-chat (Flash)' },
];

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

export function SettingsPage() {
  const [model, setModel] = useState('deepseek-chat');
  const [hasKey, setHasKey] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    fetch(`${API_BASE}/api/settings`, {
      headers: { 'Authorization': token ? `Bearer ${token}` : '' }
    })
      .then(r => r.json())
      .then((data) => {
        setHasKey(!!data.apiKey);
        setModel(data.model || 'deepseek-chat');
      })
      .catch(() => setMessage({ type: 'error', text: 'Failed to load settings' }))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const token = localStorage.getItem('accessToken');
      await fetch(`${API_BASE}/api/settings`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': token ? `Bearer ${token}` : '',
        },
        body: JSON.stringify({ model }),
      });
      setMessage({ type: 'success', text: 'Settings saved successfully' });
    } catch (err: any) {
      console.error('[Settings] Save error:', err);
      setMessage({ type: 'error', text: 'Failed to save settings' });
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)', color: 'var(--ledger-ink-soft)' }}>
          <p>Loading settings...</p>
        </div>
      </MainLayout>
    );
  }

  return (
    <MainLayout>
      <motion.div className="settings-page" variants={pageTransition} initial="initial" animate="animate" exit="exit">
        <div className="page-header">
          <div>
            <h1>Settings</h1>
            <p className="page-subtitle">Configure your AI preferences</p>
          </div>
        </div>

        <div className="settings-card">
          <div className="settings-section">
            <h2>AI Configuration</h2>
            <p className="settings-description">
              The API key is configured via the <code>DEEPSEEK_API_KEY</code> environment variable on the server.
              Only the AI model can be changed here.
            </p>

            <div className="form-group">
              <label htmlFor="model">AI Model</label>
              <select id="model" value={model} onChange={(e) => setModel(e.target.value)}>
                {AI_MODELS.map(m => (
                  <option key={m.value} value={m.value}>{m.label}</option>
                ))}
              </select>
            </div>

            <div className="api-key-status" style={{ margin: '12px 0' }}>
              {hasKey ? (
                <span className="key-saved">✓ API key is configured via environment variable</span>
              ) : (
                <span className="key-missing">✗ No API key set in environment variables</span>
              )}
            </div>

            {message && (
              <div className={`settings-message ${message.type}`}>
                {message.type === 'success' ? '✓' : '✕'} {message.text}
              </div>
            )}

            <div className="settings-actions">
              <Button variant="primary" onClick={handleSave} disabled={saving}>
                {saving ? 'Saving...' : 'Save Settings'}
              </Button>
            </div>
          </div>
        </div>
      </motion.div>
    </MainLayout>
  );
}
