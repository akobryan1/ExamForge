import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { pageTransition } from '../utils/animations';
import '../styles/pages/settings.css';

const AI_MODELS = [
  { value: 'deepseek/deepseek-chat', label: 'DeepSeek — deepseek-chat (Flash)' },
];

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

export function SettingsPage() {
  const [apiKey, setApiKey] = useState('');
  const [model, setModel] = useState('deepseek/deepseek-chat');
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
        setApiKey(data.apiKey || '');
        setModel(data.model || 'deepseek/deepseek-chat');
      })
      .catch(() => setMessage({ type: 'error', text: 'Failed to load settings' }))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      // Use raw fetch instead of axios to avoid transformRequest issues
      const token = localStorage.getItem('accessToken');
      const bodyStr = JSON.stringify({ apiKey, model });
      console.log('[Settings] apiKey at fetch:', JSON.stringify(apiKey), 'length:', apiKey.length);
      console.log('[Settings] Full body string:', bodyStr);
      const res = await fetch(`${API_BASE}/api/settings`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': token ? `Bearer ${token}` : '',
        },
        body: bodyStr,
      });
      const data = await res.json();
      console.log('[Settings] Backend response:', data);
      if (data.savedKeyPrefix && data.savedKeyPrefix !== apiKey.slice(0, 8)) {
        console.warn('[Settings] KEY MISMATCH! Sent prefix:', apiKey.slice(0, 8), 'saved prefix:', data.savedKeyPrefix);
      }
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
            <p className="page-subtitle">Configure your AI and API preferences</p>
          </div>
        </div>

        <div className="settings-card">
          <div className="settings-section">
            <h2>AI Question Generation</h2>
            <p className="settings-description">
              Configure the AI model and API key used for generating exam questions and auto-grading.
              Get a free API key at <a href="https://openrouter.ai/keys" target="_blank" rel="noopener noreferrer">openrouter.ai/keys</a>.
            </p>

            <div className="form-group">
              <label htmlFor="model">AI Model</label>
              <select id="model" value={model} onChange={(e) => setModel(e.target.value)}>
                {AI_MODELS.map(m => (
                  <option key={m.value} value={m.value}>{m.label}</option>
                ))}
              </select>
            </div>

            <div className="form-group">
              <label htmlFor="apiKey">API Key</label>
              <input
                type="text"
                id="apiKey"
                value={apiKey}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder="sk-or-v1-..."
              />
              <p className="form-hint">
                {apiKey.includes('••••')
                  ? 'A key is saved. Edit the field to change it.'
                  : apiKey
                    ? 'Key will be saved when you click Save.'
                    : 'No key saved. Paste your OpenRouter API key above.'}
              </p>
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
