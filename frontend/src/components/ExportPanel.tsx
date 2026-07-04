import { useState, useEffect, useCallback } from 'react';
import { Button } from './Button';
import {
  PAPER_COLUMNS,
  INCIDENT_COLUMNS,
  PAPER_PRESETS,
  INCIDENT_PRESETS,
  ExportColumn,
  exportToCSV,
  exportToPDF,
} from '../utils/exportUtils';
import { ExamService } from '../services/ExamService';

interface ExportPanelProps {
  type: 'papers' | 'incidents';
}

interface FilterOptions {
  sections: string[];
  exams: { id: string; title: string }[];
  statuses?: string[];
  severities?: string[];
}

export function ExportPanel({ type }: ExportPanelProps) {
  const [open, setOpen] = useState(false);
  const [data, setData] = useState<Record<string, any>[]>([]);
  const [loading, setLoading] = useState(false);
  const [options, setOptions] = useState<FilterOptions>({ sections: [], exams: [] });

  // Preset + columns
  const presets = type === 'papers' ? PAPER_PRESETS : INCIDENT_PRESETS;
  const allColumns = type === 'papers' ? PAPER_COLUMNS : INCIDENT_COLUMNS;
  const [presetName, setPresetName] = useState(presets[0]?.name || '');
  const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set(presets[0]?.keys || []));

  // Filters
  const [filterSection, setFilterSection] = useState('');
  const [filterExam, setFilterExam] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [filterSeverity, setFilterSeverity] = useState('');
  const [filterArchived, setFilterArchived] = useState<'all' | 'active' | 'archived'>('all');

  // Load filter options on open
  useEffect(() => {
    if (open) {
      ExamService.getExportOptions().then(setOptions).catch(() => {});
    }
  }, [open]);

  // Reset on type change
  useEffect(() => {
    setPresetName(presets[0]?.name || '');
    setSelectedKeys(new Set(presets[0]?.keys || []));
    setFilterSection('');
    setFilterExam('');
    setFilterStatus('');
    setFilterSeverity('');
    setFilterArchived('all');
  }, [type]);

  // Apply preset
  const applyPreset = (name: string) => {
    const preset = presets.find(p => p.name === name);
    if (preset) {
      setPresetName(name);
      setSelectedKeys(new Set(preset.keys));
    }
  };

  const toggleKey = (key: string) => {
    setSelectedKeys(prev => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
    // Auto-switch to Custom when manually toggling
    const currentPreset = presets.find(p => p.name === presetName);
    if (currentPreset) {
      const presetKeys = new Set(currentPreset.keys);
      const newKeys = new Set(selectedKeys);
      if (selectedKeys.has(key)) newKeys.delete(key);
      else newKeys.add(key);
      if (!setsEqual(presetKeys, newKeys)) {
        setPresetName('Custom');
      }
    }
  };

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams();
      if (filterSection) params.set('sections', filterSection);
      if (filterExam) params.set('examId', filterExam);
      if (type === 'papers') {
        if (filterStatus) params.set('status', filterStatus);
        const res = await (await fetch(`/api/exams/export?${params}`, {
          headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
        })).json();
        setData(res);
      } else {
        if (filterSeverity) params.set('severity', filterSeverity);
        if (filterArchived !== 'all') params.set('archived', filterArchived === 'archived' ? 'true' : 'false');
        const res = await (await fetch(`/api/exams/export/incidents?${params}`, {
          headers: { Authorization: `Bearer ${localStorage.getItem('token')}` },
        })).json();
        setData(res);
      }
    } catch (err) {
      console.error('Export fetch error:', err);
    } finally {
      setLoading(false);
    }
  }, [type, filterSection, filterExam, filterStatus, filterSeverity, filterArchived]);

  const handleExportCSV = async () => {
    await fetchData();
    const cols = allColumns.filter(c => selectedKeys.has(c.key));
    if (cols.length === 0) return;
    const fn = `examforge_${type}_${new Date().toISOString().slice(0, 10)}`;
    exportToCSV(data, cols, fn);
  };

  const handleExportPDF = async () => {
    await fetchData();
    const cols = allColumns.filter(c => selectedKeys.has(c.key));
    if (cols.length === 0) return;
    const fn = `examforge_${type}_${new Date().toISOString().slice(0, 10)}`;
    const title = type === 'papers' ? 'Exam Papers' : 'Incident Reports';
    exportToPDF(data, cols, title, fn);
  };

  if (!open) {
    return (
      <div style={{ marginBottom: 12 }}>
        <Button variant="secondary" size="sm" onClick={() => setOpen(true)}>
          📥 Export
        </Button>
      </div>
    );
  }

  return (
    <div className="export-panel" style={{
      background: 'var(--color-surface-elevated)',
      border: '1px solid var(--color-border)',
      borderRadius: 'var(--radius-md)',
      padding: 'var(--spacing-4)',
      marginBottom: 20,
    }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
        <h3 style={{ margin: 0, fontSize: 'var(--text-lg)', fontWeight: 600 }}>
          Export {type === 'papers' ? 'Exam Papers' : 'Incident Reports'}
        </h3>
        <Button variant="outline" size="sm" onClick={() => setOpen(false)}>✕ Close</Button>
      </div>

      {/* Row layout — two columns */}
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20 }}>

        {/* LEFT: Filters */}
        <div>
          <h4 style={{ margin: '0 0 8px', fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--color-gray-2)' }}>Filters</h4>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            {(options.sections.length > 0 || options.exams.length > 0) ? (
              <>
                <select
                  value={filterSection}
                  onChange={e => setFilterSection(e.target.value)}
                  style={selectStyle}
                >
                  <option value="">All Sections</option>
                  {options.sections.map(s => (
                    <option key={s} value={s}>{s}</option>
                  ))}
                </select>
                <select
                  value={filterExam}
                  onChange={e => setFilterExam(e.target.value)}
                  style={selectStyle}
                >
                  <option value="">All Exams</option>
                  {options.exams.map(e => (
                    <option key={e.id} value={e.id}>{e.title}</option>
                  ))}
                </select>
                {type === 'papers' && (options.statuses || []).length > 0 && (
                  <select
                    value={filterStatus}
                    onChange={e => setFilterStatus(e.target.value)}
                    style={selectStyle}
                  >
                    <option value="">All Statuses</option>
                    {options.statuses!.map(s => (
                      <option key={s} value={s}>{s}</option>
                    ))}
                  </select>
                )}
                {type === 'incidents' && (
                  <>
                    <select
                      value={filterSeverity}
                      onChange={e => setFilterSeverity(e.target.value)}
                      style={selectStyle}
                    >
                      <option value="">All Severities</option>
                      {(options.severities || ['low', 'medium', 'high']).map(s => (
                        <option key={s} value={s}>{s}</option>
                      ))}
                    </select>
                    <select
                      value={filterArchived}
                      onChange={e => setFilterArchived(e.target.value as any)}
                      style={selectStyle}
                    >
                      <option value="all">All Incidents</option>
                      <option value="active">Active Only</option>
                      <option value="archived">Archived Only</option>
                    </select>
                  </>
                )}
              </>
            ) : (
              <p style={{ fontSize: 12, color: 'var(--color-gray-3)', margin: 0 }}>Loading options...</p>
            )}
          </div>
        </div>

        {/* RIGHT: Columns */}
        <div>
          <h4 style={{ margin: '0 0 8px', fontSize: 12, fontWeight: 600, textTransform: 'uppercase', color: 'var(--color-gray-2)' }}>Columns</h4>
          {/* Presets */}
          <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginBottom: 8 }}>
            {presets.map(p => (
              <button
                key={p.name}
                onClick={() => applyPreset(p.name)}
                style={{
                  padding: '3px 10px',
                  fontSize: 11,
                  fontWeight: presetName === p.name ? 600 : 400,
                  border: `1px solid ${presetName === p.name ? 'var(--color-accent-500)' : 'var(--color-border)'}`,
                  borderRadius: 'var(--radius-sm)',
                  background: presetName === p.name ? 'var(--color-accent-50)' : 'var(--color-surface)',
                  color: presetName === p.name ? 'var(--color-accent-700)' : 'var(--color-text-primary)',
                  cursor: 'pointer',
                  fontFamily: 'var(--font-body)',
                }}
              >
                {p.name}
              </button>
            ))}
          </div>
          {/* Checkboxes */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2, maxHeight: 180, overflowY: 'auto' }}>
            {allColumns.map(col => (
              <label
                key={col.key}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  fontSize: 12,
                  cursor: 'pointer',
                  padding: '2px 4px',
                  borderRadius: 'var(--radius-sm)',
                }}
              >
                <input
                  type="checkbox"
                  checked={selectedKeys.has(col.key)}
                  onChange={() => toggleKey(col.key)}
                  style={{ margin: 0 }}
                />
                {col.label}
              </label>
            ))}
          </div>
        </div>
      </div>

      {/* Export Buttons */}
      <div style={{ display: 'flex', gap: 8, marginTop: 16, paddingTop: 12, borderTop: '1px solid var(--color-border)' }}>
        <Button variant="primary" size="sm" onClick={handleExportCSV} disabled={loading || selectedKeys.size === 0}>
          {loading ? 'Loading...' : '📊 Export CSV'}
        </Button>
        <Button variant="primary" size="sm" onClick={handleExportPDF} disabled={loading || selectedKeys.size === 0}>
          {loading ? 'Loading...' : '📄 Export PDF'}
        </Button>
        <span style={{ fontSize: 11, color: 'var(--color-gray-3)', alignSelf: 'center', marginLeft: 'auto' }}>
          {data.length > 0 ? `${data.length} rows ready` : ''}
        </span>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const selectStyle: React.CSSProperties = {
  width: '100%',
  padding: '6px 10px',
  fontFamily: 'var(--font-body)',
  fontSize: 13,
  color: 'var(--color-text-primary)',
  background: 'var(--color-surface)',
  border: '1px solid var(--color-border)',
  borderRadius: 'var(--radius-sm)',
  outline: 'none',
};

function setsEqual(a: Set<string>, b: Set<string>): boolean {
  if (a.size !== b.size) return false;
  for (const v of a) if (!b.has(v)) return false;
  return true;
}
