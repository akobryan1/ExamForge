import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { pageTransition } from '../utils/animations';
import { useSettings, applyThemeSettings, cacheSettingsLocally } from '../hooks/useSettings';
import '../styles/pages/settings.css';

const AI_MODELS = [
  { value: 'deepseek-chat', label: 'DeepSeek — deepseek-chat (Flash)' },
];

const FONT_PRESETS = [
  { id: 'editorial', name: 'Editorial', display: 'Fraunces', body: 'Inter', mono: 'JetBrains Mono', sample: 'Aa' },
  { id: 'scholarly', name: 'Scholarly', display: 'Crimson Pro', body: 'Lora', mono: 'JetBrains Mono', sample: 'Aa' },
  { id: 'modern', name: 'Modern', display: 'DM Sans', body: 'Inter', mono: 'JetBrains Mono', sample: 'Aa' },
  { id: 'classic', name: 'Classic', display: 'Playfair Display', body: 'Source Sans 3', mono: 'SF Mono', sample: 'Aa' },
  { id: 'clean', name: 'Clean', display: 'Inter', body: 'Inter', mono: 'JetBrains Mono', sample: 'Aa' },
];

const FONT_LINKS: Record<string, string> = {
  editorial: 'https://fonts.googleapis.com/css2?family=Fraunces:opsz,wght@9..144,400;9..144,500;9..144,600;9..144,700&family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;700&display=swap',
  scholarly: 'https://fonts.googleapis.com/css2?family=Crimson+Pro:wght@400;500;600;700&family=Lora:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;700&display=swap',
  modern: 'https://fonts.googleapis.com/css2?family=DM+Sans:opsz,wght@9..40,400;9..40,500;9..40,600;9..40,700&family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;700&display=swap',
  classic: 'https://fonts.googleapis.com/css2?family=Playfair+Display:wght@400;500;600;700&family=Source+Sans+3:wght@400;500;600;700&display=swap',
  clean: 'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;700&display=swap',
};

const FONT_VARS: Record<string, { display: string; body: string; mono: string }> = {
  editorial: { display: "'Fraunces', serif", body: "'Inter', system-ui, sans-serif", mono: "'JetBrains Mono', monospace" },
  scholarly: { display: "'Crimson Pro', Georgia, serif", body: "'Lora', Georgia, serif", mono: "'JetBrains Mono', monospace" },
  modern: { display: "'DM Sans', system-ui, sans-serif", body: "'Inter', system-ui, sans-serif", mono: "'JetBrains Mono', monospace" },
  classic: { display: "'Playfair Display', Georgia, serif", body: "'Source Sans 3', 'Segoe UI', sans-serif", mono: "'SF Mono', 'Monaco', monospace" },
  clean: { display: "'Inter', system-ui, sans-serif", body: "'Inter', system-ui, sans-serif", mono: "'JetBrains Mono', monospace" },
};

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000';

type Tab = 'appearance' | 'ai' | 'activity';

export function SettingsPage() {
  const { query, mutation } = useSettings();
  const settings = query.data;

  const [activeTab, setActiveTab] = useState<Tab>('appearance');

  // Appearance state
  const [themeColor, setThemeColor] = useState('#1B2540');
  const [fontPreset, setFontPreset] = useState('editorial');
  const [appearanceDirty, setAppearanceDirty] = useState(false);

  // AI state
  const [model, setModel] = useState('deepseek-chat');
  const [aiDirty, setAiDirty] = useState(false);

  // Activity state
  const [activityEntries, setActivityEntries] = useState<any[]>([]);
  const [activityLoading, setActivityLoading] = useState(false);
  const [activityCursor, setActivityCursor] = useState<string | null>(null);
  const [activityHasMore, setActivityHasMore] = useState(false);

  // Message state
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // Load settings from backend
  useEffect(() => {
    if (settings) {
      setThemeColor(settings.themeColor || '#1B2540');
      setFontPreset(settings.fontPreset || 'editorial');
      setModel(settings.model || 'deepseek/deepseek-chat');
      // Apply cached theme on load
      applyThemeSettings({ themeColor: settings.themeColor || '#1B2540' });
      // Load font if preset is set
      if (settings.fontPreset && settings.fontPreset !== 'editorial') {
        loadFont(settings.fontPreset);
      }
    }
  }, [settings]);

  // Load activity log when tab becomes active
  useEffect(() => {
    if (activeTab === 'activity') {
      loadActivity(true);
    }
  }, [activeTab]);

  const loadFont = (presetId: string) => {
    const linkId = `gf-${presetId}`;
    if (!document.getElementById(linkId)) {
      const link = document.createElement('link');
      link.id = linkId;
      link.rel = 'stylesheet';
      link.href = FONT_LINKS[presetId];
      document.head.appendChild(link);
    }
    const vars = FONT_VARS[presetId];
    if (vars) {
      const root = document.documentElement;
      root.style.setProperty('--font-display-ledger', vars.display);
      root.style.setProperty('--font-body', vars.body);
      root.style.setProperty('--font-mono-ledger', vars.mono);
      root.style.setProperty('--font-display', vars.display);
    }
  };

  const loadActivity = async (reset = false) => {
    setActivityLoading(true);
    try {
      const cursor = reset ? null : activityCursor;
      const params = new URLSearchParams({ limit: '50' });
      if (cursor) params.set('cursor', cursor);

      const token = localStorage.getItem('accessToken');
      const res = await fetch(`${API_BASE}/api/activity?${params}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!res.ok) throw new Error('Failed to load activity');
      const data = await res.json();

      if (reset) {
        setActivityEntries(data.entries);
      } else {
        setActivityEntries((prev) => [...prev, ...data.entries]);
      }
      setActivityCursor(data.nextCursor);
      setActivityHasMore(data.hasMore);
    } catch (err: any) {
      console.error('[Settings] Activity load error:', err);
    } finally {
      setActivityLoading(false);
    }
  };

  // Handlers

  const handleColorChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newColor = e.target.value;
    setThemeColor(newColor);
    setAppearanceDirty(true);
    applyThemeSettings({ themeColor: newColor });
  };

  const handleFontSelect = (presetId: string) => {
    setFontPreset(presetId);
    setAppearanceDirty(true);
    loadFont(presetId);
  };

  const handleSaveAppearance = async () => {
    setMessage(null);
    try {
      await mutation.mutateAsync({ themeColor, fontPreset });
      cacheSettingsLocally({ themeColor, fontPreset });
      setAppearanceDirty(false);
      setMessage({ type: 'success', text: 'Appearance saved successfully' });
    } catch {
      setMessage({ type: 'error', text: 'Failed to save appearance' });
    }
  };

  const handleSaveAI = async () => {
    setMessage(null);
    try {
      await mutation.mutateAsync({ model });
      setAiDirty(false);
      setMessage({ type: 'success', text: 'AI settings saved successfully' });
    } catch {
      setMessage({ type: 'error', text: 'Failed to save AI settings' });
    }
  };

  const actionIcon = (action: string): string => {
    if (action.startsWith('exam.created')) return '📝';
    if (action.startsWith('exam.published')) return '📢';
    if (action.startsWith('exam.completed')) return '✅';
    if (action.startsWith('exam.archived')) return '📦';
    if (action.startsWith('exam.deleted')) return '🗑️';
    if (action.startsWith('exam.cloned')) return '📋';
    if (action.startsWith('exam.republished')) return '🔄';
    if (action.startsWith('exam.updated')) return '✏️';
    if (action.startsWith('question.created')) return '❓';
    if (action.startsWith('question.deleted')) return '❌';
    if (action.startsWith('grade.submitted')) return '📊';
    if (action.startsWith('grade.ai_graded')) return '🤖';
    if (action.startsWith('incident')) return '⚠️';
    if (action.startsWith('student.registered')) return '👤';
    if (action.startsWith('settings.updated')) return '⚙️';
    return '📌';
  };

  const formatTime = (iso: string): string => {
    const d = new Date(iso);
    const now = new Date();
    const diffMs = now.getTime() - d.getTime();
    const diffMin = Math.floor(diffMs / 60000);
    if (diffMin < 1) return 'Just now';
    if (diffMin < 60) return `${diffMin}m ago`;
    const diffHr = Math.floor(diffMin / 60);
    if (diffHr < 24) return `${diffHr}h ago`;
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  };

  return (
    <MainLayout>
      <motion.div className="settings-page" variants={pageTransition} initial="initial" animate="animate" exit="exit">
        <div className="page-header">
          <div>
            <h1>Settings</h1>
            <p className="page-subtitle">Configure your preferences</p>
          </div>
        </div>

        {/* Tab navigation */}
        <div className="settings-tabs">
          <button
            className={`settings-tab ${activeTab === 'appearance' ? 'active' : ''}`}
            onClick={() => setActiveTab('appearance')}
          >
            🎨 Appearance
          </button>
          <button
            className={`settings-tab ${activeTab === 'ai' ? 'active' : ''}`}
            onClick={() => setActiveTab('ai')}
          >
            🤖 AI Config
          </button>
          <button
            className={`settings-tab ${activeTab === 'activity' ? 'active' : ''}`}
            onClick={() => setActiveTab('activity')}
          >
            📋 Activity Log
          </button>
        </div>

        {/* Message banner */}
        {message && (
          <div className={`settings-message ${message.type}`}>
            {message.type === 'success' ? '✓' : '✕'} {message.text}
          </div>
        )}

        {/* ── Appearance Tab ── */}
        {activeTab === 'appearance' && (
          <div className="settings-card">
            <div className="settings-section">
              <h2>Theme Color</h2>
              <p className="settings-description">
                Pick the main color for the sidebar and accent areas. The gradient is derived automatically.
              </p>

              <div className="color-picker-wrapper">
                <div className="color-preview" style={{ background: `linear-gradient(165deg, ${themeColor}, ${themeColor})` }} />
                <div className="color-input-group">
                  <label htmlFor="themeColor">Sidebar Color</label>
                  <div className="color-input-row">
                    <input
                      type="color"
                      id="themeColor"
                      value={themeColor}
                      onChange={handleColorChange}
                      className="color-picker-native"
                    />
                    <input
                      type="text"
                      value={themeColor}
                      onChange={(e) => {
                        const val = e.target.value;
                        if (/^#[0-9a-fA-F]{6}$/.test(val)) {
                          setThemeColor(val);
                          setAppearanceDirty(true);
                          applyThemeSettings({ themeColor: val });
                        } else {
                          setThemeColor(val);
                        }
                      }}
                      className="color-hex-input"
                      placeholder="#1B2540"
                    />
                  </div>
                </div>
              </div>

              <div className="color-swatches">
                {['#1B2540', '#1B3A2D', '#2D1B4E', '#3D1B24', '#1E293B', '#4A2C2A', '#1A3A3A'].map((c) => (
                  <button
                    key={c}
                    className={`color-swatch ${themeColor.toLowerCase() === c.toLowerCase() ? 'active' : ''}`}
                    style={{ background: c }}
                    onClick={() => {
                      setThemeColor(c);
                      setAppearanceDirty(true);
                      applyThemeSettings({ themeColor: c });
                    }}
                    title={c}
                  />
                ))}
              </div>
            </div>

            <div className="settings-section" style={{ marginTop: 32 }}>
              <h2>Font Style</h2>
              <p className="settings-description">
                Choose a font pairing for headings, body text, and code.
              </p>

              <div className="font-presets">
                {FONT_PRESETS.map((preset) => (
                  <button
                    key={preset.id}
                    className={`font-preset-card ${fontPreset === preset.id ? 'active' : ''}`}
                    onClick={() => handleFontSelect(preset.id)}
                  >
                    <div className="font-preset-sample">{preset.sample}</div>
                    <div className="font-preset-name">{preset.name}</div>
                    <div className="font-preset-detail">
                      {preset.display} · {preset.body} · {preset.mono}
                    </div>
                  </button>
                ))}
              </div>
            </div>

            <div className="settings-actions">
              <Button variant="primary" onClick={handleSaveAppearance} disabled={!appearanceDirty || mutation.isPending}>
                {mutation.isPending ? 'Saving...' : 'Save Appearance'}
              </Button>
            </div>
          </div>
        )}

        {/* ── AI Config Tab ── */}
        {activeTab === 'ai' && (
          <div className="settings-card">
            <div className="settings-section">
              <h2>AI Configuration</h2>
              <p className="settings-description">
                The API key is configured via the <code>DEEPSEEK_API_KEY</code> environment variable on the server.
                Only the AI model can be changed here.
              </p>

              <div className="form-group">
                <label htmlFor="model">AI Model</label>
                <select id="model" value={model} onChange={(e) => { setModel(e.target.value); setAiDirty(true); }}>
                  {AI_MODELS.map((m) => (
                    <option key={m.value} value={m.value}>{m.label}</option>
                  ))}
                </select>
              </div>

              <div className="api-key-status">
                {settings?.apiKey ? (
                  <span className="key-saved">✓ API key is configured via environment variable</span>
                ) : (
                  <span className="key-missing">✗ No API key set in environment variables</span>
                )}
              </div>

              <div className="settings-actions">
                <Button variant="primary" onClick={handleSaveAI} disabled={!aiDirty || mutation.isPending}>
                  {mutation.isPending ? 'Saving...' : 'Save AI Settings'}
                </Button>
              </div>
            </div>
          </div>
        )}

        {/* ── Activity Log Tab ── */}
        {activeTab === 'activity' && (
          <div className="settings-card">
            <div className="settings-section">
              <h2>Activity Log</h2>
              <p className="settings-description">
                A chronological record of actions you've performed in ExamForge.
              </p>

              {activityLoading && activityEntries.length === 0 ? (
                <div className="activity-loading">Loading activity...</div>
              ) : activityEntries.length === 0 ? (
                <div className="activity-empty">
                  <p>No activity yet. Your actions will appear here as you use the platform.</p>
                </div>
              ) : (
                <>
                  <div className="activity-list">
                    {activityEntries.map((entry: any) => (
                      <div key={entry.id} className="activity-item">
                        <span className="activity-icon">{actionIcon(entry.action)}</span>
                        <div className="activity-body">
                          <div className="activity-description">{entry.description}</div>
                          <div className="activity-meta">
                            <span className="activity-action-tag">{entry.action}</span>
                            <span className="activity-time">{formatTime(entry.createdAt)}</span>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>

                  {activityHasMore && (
                    <div className="activity-load-more">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => loadActivity(false)}
                        disabled={activityLoading}
                      >
                        {activityLoading ? 'Loading...' : 'Load more'}
                      </Button>
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        )}
      </motion.div>
    </MainLayout>
  );
}
