import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { apiClient } from '../services/apiClient';
import { pageTransition, fadeIn } from '../utils/animations';
import '../styles/pages/settings.css';

const AI_MODELS = [
  { value: 'deepseek/deepseek-chat', label: 'DeepSeek — deepseek-chat (Flash)' },
];

export function SettingsPage() {
  const [apiKey, setApiKey] = useState('');
  const [model, setModel] = useState('deepseek/deepseek-chat');
  const [hasSavedKey, setHasSavedKey] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    apiClient.get('/api/settings')
      .then(({ data }) => {
        // Show the masked key so user knows a key is saved
        setApiKey(data.apiKey || '');
        setHasSavedKey(!!data.apiKey);
        setModel(data.model || 'deepseek/deepseek-chat');
      })
      .catch(() => setMessage({ type: 'error', text: 'Failed to load settings' }))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const payload: Record<string, string> = { model };
      // Send key if it doesn't look like the masked placeholder
      if (apiKey.trim() && !apiKey.includes('••••')) {
        payload.apiKey = apiKey;
      }
      const res = await apiClient.put('/api/settings', payload);
      console.log('[Settings] Save response:', res.status, res.data);
      setHasSavedKey(!!apiKey);
      setMessage({ type: 'success', text: 'Settings saved successfully' });
    } catch (err: any) {
      console.error('[Settings] Save error:', err.response?.status, err.response?.data || err.message);
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
                type="password"
                id="apiKey"
                value={apiKey}
                onFocus={() => { if (hasSavedKey) setApiKey(''); }}
                onChange={(e) => setApiKey(e.target.value)}
                placeholder={hasSavedKey ? 'Enter new key to replace the saved one' : 'sk-or-v1-...'}
              />
              {hasSavedKey && (
                <p className="form-hint">A key is already saved. Type a new value to change it.</p>
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
