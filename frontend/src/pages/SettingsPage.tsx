import { useState, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { apiClient } from '../services/apiClient';
import { pageTransition } from '../utils/animations';
import '../styles/pages/settings.css';

const AI_MODELS = [
  { value: 'deepseek/deepseek-chat', label: 'DeepSeek — deepseek-chat (Flash)' },
];

export function SettingsPage() {
  const apiKeyRef = useRef<HTMLInputElement>(null);
  const [model, setModel] = useState('deepseek/deepseek-chat');
  const [hasKey, setHasKey] = useState(false);
  const [saving, setSaving] = useState(false);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    apiClient.get('/api/settings')
      .then(({ data }) => {
        setHasKey(!!data.apiKey);
        setModel(data.model || 'deepseek/deepseek-chat');
      })
      .catch(() => setMessage({ type: 'error', text: 'Failed to load settings' }))
      .finally(() => setLoading(false));
  }, []);

  const handleSave = async () => {
    setSaving(true);
    setMessage(null);
    try {
      const keyValue = apiKeyRef.current?.value || '';
      console.log('[Settings] Read from DOM:', { keyValueLength: keyValue.length, first10: keyValue.slice(0, 10) });
      await apiClient.put('/api/settings', { apiKey: keyValue || undefined, model });
      if (apiKeyRef.current) apiKeyRef.current.value = '';
      setHasKey(!!keyValue);
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
              <label>API Key</label>
              <div className="api-key-status" style={{ marginBottom: 6 }}>
                {hasKey ? (
                  <span className="key-saved">✓ A key is saved</span>
                ) : (
                  <span className="key-missing">✗ No key saved</span>
                )}
              </div>
              <input
                type="text"
                id="apiKey"
                ref={apiKeyRef}
                placeholder="sk-or-v1-..."
              />
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
