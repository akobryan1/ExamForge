import { useState, useEffect, useRef, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { useAuth } from '../contexts/AuthContext';
import { useStudents, useRegistrationFields, useSaveRegistrationFields } from '../hooks/useStudentQueries';
import { useSubmittedPapers } from '../hooks/useExamQueries';
import { Button } from '../components/Button';
import { ExportPanel } from '../components/ExportPanel';
import '../styles/pages/exam-form.css';
import '../styles/pages/students.css';

const STUDENT_PORTAL_URL = import.meta.env.VITE_STUDENT_PORTAL_URL || 'https://examforge-student-portal.onrender.com';

export function StudentManagementPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { examId } = useParams<{ examId?: string }>();
  const instructorId = user?.id || '';
  const { data: students = [], isLoading: studentsLoading } = useStudents(instructorId || undefined);
  const { data: fields = { sections: [], years: [], courses: [] }, isLoading: fieldsLoading } = useRegistrationFields(instructorId || undefined);
  const saveFieldsMutation = useSaveRegistrationFields();
  const loading = studentsLoading || fieldsLoading;

  const [error, setError] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);
  const [regLink, setRegLink] = useState<string>('');
  const [fieldInputs, setFieldInputs] = useState({ section: '', year: '', course: '' });
  const [savingFields, setSavingFields] = useState(false);
  const [activeTab, setActiveTab] = useState<'registered' | 'papers'>('registered');

  const { data: papers = [], isLoading: papersLoading } = useSubmittedPapers();

  // Local copy of fields for editing
  const [localFields, setLocalFields] = useState<{ sections: string[]; years: string[]; courses: string[] }>({ sections: [], years: [], courses: [] });
  useEffect(() => {
    if (fields.sections.length > 0 && localFields.sections.length === 0) {
      setLocalFields({ sections: [...fields.sections], years: [...fields.years], courses: [...fields.courses] });
    }
  }, [fields, localFields.sections.length]);

  // Search with 1.2s debounce
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const searchTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    searchTimerRef.current = setTimeout(() => {
      setDebouncedSearch(searchQuery);
    }, 1200);
    return () => {
      if (searchTimerRef.current) clearTimeout(searchTimerRef.current);
    };
  }, [searchQuery]);

  const filteredStudents = useMemo(() => {
    if (!debouncedSearch.trim()) return students;
    const q = debouncedSearch.toLowerCase().trim();
    return students.filter(s =>
      (s.name?.toLowerCase() || '').includes(q) ||
      (s.course?.toLowerCase() || '').includes(q) ||
      (s.year?.toLowerCase() || '').includes(q) ||
      (s.section?.toLowerCase() || '').includes(q) ||
      (s.studentId?.toLowerCase() || '').includes(q) ||
      (s.email?.toLowerCase() || '').includes(q)
    );
  }, [students, debouncedSearch]);

  const generateLink = () => {
    const base = examId ? `/register/exam/${examId}` : '/register/exam';
    const link = `${STUDENT_PORTAL_URL}${base}?instructor=${instructorId}`;
    setRegLink(link);
  };

  const copyLink = async () => {
    try {
      await navigator.clipboard.writeText(regLink);
      setSuccessToast('Link copied to clipboard');
      setTimeout(() => setSuccessToast(null), 3000);
    } catch {
      // Fallback for older browsers
      const textarea = document.createElement('textarea');
      textarea.value = regLink;
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
      setSuccessToast('Link copied to clipboard');
      setTimeout(() => setSuccessToast(null), 3000);
    }
  };

  const addFieldOption = (type: 'section' | 'year' | 'course') => {
    const value = fieldInputs[type].trim();
    if (!value) return;
    const arr = type === 'section' ? localFields.sections : type === 'year' ? localFields.years : localFields.courses;
    if (arr.includes(value)) return;

    const updated = { ...localFields };
    if (type === 'section') updated.sections = [...updated.sections, value];
    else if (type === 'year') updated.years = [...updated.years, value];
    else updated.courses = [...updated.courses, value];

    setLocalFields(updated);
    setFieldInputs(prev => ({ ...prev, [type]: '' }));
  };

  const removeFieldOption = (type: 'section' | 'year' | 'course', value: string) => {
    const updated = { ...localFields };
    if (type === 'section') updated.sections = localFields.sections.filter(s => s !== value);
    else if (type === 'year') updated.years = localFields.years.filter(y => y !== value);
    else updated.courses = localFields.courses.filter(c => c !== value);
    setLocalFields(updated);
  };

  const saveFieldOptions = async () => {
    try {
      setSavingFields(true);
      await saveFieldsMutation.mutateAsync({ instructorId, fields: localFields });
      setSuccessToast('Registration fields saved');
      setTimeout(() => setSuccessToast(null), 3000);
    } catch (err: any) {
      setError(err.message || 'Failed to save fields');
    } finally {
      setSavingFields(false);
    }
  };

  return (
    <MainLayout>
      <div className="students-page">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }}>
          
          <div className="page-header">
            <div>
              <h1>Student registration</h1>
              <p className="page-subtitle">Manage your examinees and review submitted papers</p>
            </div>
          </div>

          {/* Subtab Navigation */}
          <div className="subtab-nav" style={{ display: 'flex', gap: 4, marginBottom: 20, borderBottom: '2px solid var(--color-border)', paddingBottom: 0 }}>
            <button
              className={`subtab-btn ${activeTab === 'registered' ? 'active' : ''}`}
              onClick={() => setActiveTab('registered')}
              style={{
                padding: '8px 18px',
                fontSize: 'var(--text-sm)',
                fontWeight: activeTab === 'registered' ? 600 : 400,
                color: activeTab === 'registered' ? 'var(--color-accent-500)' : 'var(--color-gray-3)',
                background: 'none',
                border: 'none',
                borderBottom: activeTab === 'registered' ? '2px solid var(--color-accent-500)' : '2px solid transparent',
                marginBottom: -2,
                cursor: 'pointer',
                transition: 'all 150ms',
                fontFamily: 'var(--font-body)',
              }}
            >
              👥 Registered Students
            </button>
            <button
              className={`subtab-btn ${activeTab === 'papers' ? 'active' : ''}`}
              onClick={() => setActiveTab('papers')}
              style={{
                padding: '8px 18px',
                fontSize: 'var(--text-sm)',
                fontWeight: activeTab === 'papers' ? 600 : 400,
                color: activeTab === 'papers' ? 'var(--color-accent-500)' : 'var(--color-gray-3)',
                background: 'none',
                border: 'none',
                borderBottom: activeTab === 'papers' ? '2px solid var(--color-accent-500)' : '2px solid transparent',
                marginBottom: -2,
                cursor: 'pointer',
                transition: 'all 150ms',
                fontFamily: 'var(--font-body)',
              }}
            >
              📝 Submitted Papers
            </button>
          </div>

          {error && (
            <div className="error-banner">
              <span>{error}</span>
              <button className="error-banner-dismiss" onClick={() => setError(null)} aria-label="Dismiss">×</button>
            </div>
          )}

          {activeTab === 'registered' && (
            <>

          {/* Registration Link Generator */}
          <div className="card" style={{ marginBottom: 24 }}>
            <div className="card-header">
              <div className="card-title">Registration link</div>
            </div>
            <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 12 }}>
              <p style={{ margin: 0, fontSize: 'var(--text-sm)', color: 'var(--color-gray-3)' }}>
                Generate a registration link to send to your students. They will use this to create their accounts.
              </p>
              <div style={{ display: 'flex', gap: 8 }}>
                <Button variant="primary" onClick={generateLink}>Generate registration form</Button>
              </div>
              {regLink && (
                <div className="reg-link-box">
                  <code className="reg-link-code">{regLink}</code>
                  <Button variant="secondary" size="sm" onClick={copyLink}>Copy link</Button>
                </div>
              )}
            </div>
          </div>

          {/* Registration Field Options */}
          <div className="card" style={{ marginBottom: 24 }}>
            <div className="card-header">
              <div className="card-title">Registration fields</div>
              <Button variant="secondary" size="sm" onClick={saveFieldOptions} disabled={savingFields}>
                {savingFields ? 'Saving...' : 'Save fields'}
              </Button>
            </div>
            <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 20 }}>

              {/* Sections */}
              <div className="form-section" style={{ margin: 0 }}>
                <h3 className="form-section-title" style={{ marginBottom: 8 }}>Sections</h3>
                <div className="section-input-group" style={{ marginBottom: 8 }}>
                  <input
                    type="text"
                    value={fieldInputs.section}
                    onChange={(e) => setFieldInputs(prev => ({ ...prev, section: e.target.value }))}
                    placeholder="e.g., Section A"
                    onKeyDown={(e) => e.key === 'Enter' && addFieldOption('section')}
                  />
                  <button type="button" className="btn-add" onClick={() => addFieldOption('section')}>Add</button>
                </div>
                <div className="section-tags">
                  {localFields.sections.map(s => (
                    <span key={s} className="section-tag">{s} <button type="button" onClick={() => removeFieldOption('section', s)}>×</button></span>
                  ))}
                  {localFields.sections.length === 0 && <span className="field-empty">No sections configured</span>}
                </div>
              </div>

              {/* Years */}
              <div className="form-section" style={{ margin: 0 }}>
                <h3 className="form-section-title" style={{ marginBottom: 8 }}>Years</h3>
                <div className="section-input-group" style={{ marginBottom: 8 }}>
                  <input
                    type="text"
                    value={fieldInputs.year}
                    onChange={(e) => setFieldInputs(prev => ({ ...prev, year: e.target.value }))}
                    placeholder="e.g., 1st Year"
                    onKeyDown={(e) => e.key === 'Enter' && addFieldOption('year')}
                  />
                  <button type="button" className="btn-add" onClick={() => addFieldOption('year')}>Add</button>
                </div>
                <div className="section-tags">
                    {localFields.years.map(y => (
                    <span key={y} className="section-tag">{y} <button type="button" onClick={() => removeFieldOption('year', y)}>×</button></span>
                  ))}
                    {localFields.years.length === 0 && <span className="field-empty">No years configured</span>}
                </div>
              </div>

              {/* Courses */}
              <div className="form-section" style={{ margin: 0 }}>
                <h3 className="form-section-title" style={{ marginBottom: 8 }}>Courses</h3>
                <div className="section-input-group" style={{ marginBottom: 8 }}>
                  <input
                    type="text"
                    value={fieldInputs.course}
                    onChange={(e) => setFieldInputs(prev => ({ ...prev, course: e.target.value }))}
                    placeholder="e.g., BS Computer Science"
                    onKeyDown={(e) => e.key === 'Enter' && addFieldOption('course')}
                  />
                  <button type="button" className="btn-add" onClick={() => addFieldOption('course')}>Add</button>
                </div>
                <div className="section-tags">
                    {localFields.courses.map(c => (
                    <span key={c} className="section-tag">{c} <button type="button" onClick={() => removeFieldOption('course', c)}>×</button></span>
                  ))}
                    {localFields.courses.length === 0 && <span className="field-empty">No courses configured</span>}
                </div>
              </div>

            </div>
          </div>

          {/* Registered Students */}
          <div className="card">
            <div className="card-header">
              <div className="card-title">Registered students</div>
              <span className="count-badge">{students.length}</span>
            </div>

            {/* Search bar */}
            <div style={{ padding: '12px 16px', borderBottom: '1px solid var(--color-border)' }}>
              <input
                type="text"
                placeholder="Search by name, course, year, section, or ID..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  fontFamily: 'var(--font-body)',
                  fontSize: '14px',
                  color: 'var(--color-text-primary)',
                  background: 'var(--color-surface-elevated)',
                  border: '1px solid var(--color-border)',
                  borderRadius: 'var(--radius-md)',
                  outline: 'none',
                  transition: 'border-color 150ms',
                  boxSizing: 'border-box',
                }}
                onFocus={(e) => e.target.style.borderColor = 'var(--color-accent-500)'}
                onBlur={(e) => e.target.style.borderColor = 'var(--color-border)'}
              />
            </div>

            {loading ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-gray-3)' }}>Loading...</div>
            ) : students.length === 0 ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-gray-3)', fontSize: 'var(--text-sm)' }}>
                No students registered yet. Generate a registration link to get started.
              </div>
            ) : (
              <div className="students-table-wrapper">
                <table className="students-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Course</th>
                      <th>Year</th>
                      <th>Section</th>
                      <th>Student ID</th>
                      <th>Email</th>
                      <th>Registered</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredStudents.length === 0 ? (
                      <tr>
                        <td colSpan={7} style={{ padding: 24, textAlign: 'center', color: 'var(--color-gray-3)' }}>
                          No students match your search.
                        </td>
                      </tr>
                    ) : (filteredStudents.map(s => (
                      <tr key={s.uid}>
                        <td className="student-name-cell">{s.name}</td>
                        <td>{s.course}</td>
                        <td>{s.year}</td>
                        <td>{s.section}</td>
                        <td><code className="student-id-code">{s.studentId}</code></td>
                        <td className="student-email-cell">{s.email}</td>
                        <td className="student-date-cell">
                          {new Date(s.registeredAt).toLocaleDateString()}
                        </td>
                      </tr>
                    )))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

            </>
          )}

          {activeTab === 'papers' && (
            <div className="card">
              <div className="card-header">
                <div className="card-title">Submitted exam papers</div>
                <span className="count-badge">{papers.length}</span>
              </div>

              <ExportPanel type="papers" />

              {papersLoading ? (
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-gray-3)' }}>Loading...</div>
              ) : papers.length === 0 ? (
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--color-gray-3)', fontSize: 'var(--text-sm)' }}>
                  No submitted exam papers yet.
                </div>
              ) : (
                <div className="students-table-wrapper">
                  <table className="students-table papers-table">
                    <thead>
                      <tr>
                        <th>Exam</th>
                        <th>Examinee</th>
                        <th>Score</th>
                        <th>Incidents</th>
                        <th>Status</th>
                        <th>Submitted</th>
                        <th style={{ textAlign: 'center' }}>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {papers.map((p: any) => (
                        <tr key={p.attemptId}>
                          <td className="student-name-cell" title={p.examTitle}>
                            {p.examTitle?.length > 25 ? p.examTitle.substring(0, 25) + '…' : p.examTitle}
                          </td>
                          <td>
                            <div style={{ fontWeight: 500, fontSize: 'var(--text-sm)' }}>{p.studentName}</div>
                            <div style={{ fontSize: '11px', color: 'var(--color-gray-3)' }}>{p.studentNumber || p.studentEmail}</div>
                          </td>
                          <td className="score-cell">
                            {p.score !== null && p.score !== undefined ? (
                              <span className={`score-badge ${p.passed ? 'passed' : 'failed'}`}>
                                {p.score}/{p.totalPoints}
                                {p.percentage !== null && (
                                  <span className="score-pct"> ({Math.round(p.percentage)}%)</span>
                                )}
                              </span>
                            ) : (
                              <span style={{ color: 'var(--color-gray-3)', fontSize: 'var(--text-sm)' }}>—</span>
                            )}
                          </td>
                          <td>
                            {p.hasIncidents ? (
                              <span className="incident-badge has-incidents" title={p.incidents.map((i: any) => i.eventDetail || i.eventType).join(', ')}>
                                ⚠ {p.incidents.length}
                              </span>
                            ) : (
                              <span style={{ color: 'var(--color-gray-3)', fontSize: 'var(--text-sm)' }}>None</span>
                            )}
                          </td>
                          <td>
                            <span className={`status-badge status-${p.status}`}>
                              {p.status === 'completed' && '✅ Completed'}
                              {p.status === 'pending' && '⏳ Pending Review'}
                              {p.status === 'incomplete' && (
                                <span title={p.statusDetail || ''}>❓ Incomplete</span>
                              )}
                              {p.status === 'submitted' && '📥 Submitted'}
                              {p.status !== 'completed' && p.status !== 'pending' && p.status !== 'incomplete' && p.status !== 'submitted' && p.status}
                            </span>
                            {p.statusDetail && (
                              <div className="status-detail" title={p.statusDetail}>
                                {p.statusDetail.length > 30 ? p.statusDetail.substring(0, 30) + '…' : p.statusDetail}
                              </div>
                            )}
                          </td>
                          <td className="student-date-cell">
                            {p.submittedAt ? new Date(p.submittedAt).toLocaleDateString() + ' ' + new Date(p.submittedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '—'}
                          </td>
                          <td style={{ textAlign: 'center' }}>
                            <button
                              className="btn-view-paper"
                              onClick={() => {
                                navigate(`/attempts/${p.attemptId}/results?studentId=${p.studentId}`);
                              }}
                              style={{
                                padding: '5px 12px',
                                fontSize: '12px',
                                fontWeight: 600,
                                color: 'var(--color-accent-500)',
                                background: 'var(--color-accent-50)',
                                border: '1px solid var(--color-accent-200)',
                                borderRadius: 'var(--radius-sm)',
                                cursor: 'pointer',
                                transition: 'all 150ms',
                                fontFamily: 'var(--font-body)',
                              }}
                            >
                              Review
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

        </motion.div>
      </div>
      {successToast && <div className="toast-success">{successToast}</div>}
    </MainLayout>
  );
}
