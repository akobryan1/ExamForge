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

          {/* Folder Tabs */}
          <div className="folder-tabs">
            <button
              className={`folder-tab ${activeTab === 'registered' ? 'active' : ''}`}
              onClick={() => setActiveTab('registered')}
            >
              <img src="/icons/tabs/registered-students.png" alt="" className="tab-icon" /> Registered Students
            </button>
            <button
              className={`folder-tab ${activeTab === 'papers' ? 'active' : ''}`}
              onClick={() => setActiveTab('papers')}
            >
              <img src="/icons/tabs/submitted-papers.png" alt="" className="tab-icon" /> Submitted Papers
            </button>
          </div>

          <div className="folder-body">

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
              <p className="card-body-description">
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
            <div className="search-bar">
              <input
                type="text"
                placeholder="Search by name, course, year, section, or ID..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>

            {loading ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--ledger-ink-soft)' }}>Loading...</div>
            ) : students.length === 0 ? (
              <div style={{ padding: 24, textAlign: 'center', color: 'var(--ledger-ink-soft)', fontSize: 'var(--text-sm)' }}>
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
                        <td colSpan={7} style={{ padding: 24, textAlign: 'center', color: 'var(--ledger-ink-soft)' }}>
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
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--ledger-ink-soft)' }}>Loading...</div>
              ) : papers.length === 0 ? (
                <div style={{ padding: 24, textAlign: 'center', color: 'var(--ledger-ink-soft)', fontSize: 'var(--text-sm)' }}>
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
                            <div className="sub-detail">{p.studentNumber || p.studentEmail}</div>
                          </td>
                          <td className="score-cell">
                            {p.score !== null && p.score !== undefined ? (
                              <span className={`stamp ${p.passed ? 'stamp-pass' : 'stamp-fail'}`}>
                                {p.score}/{p.totalPoints}
                                {p.percentage !== null && (
                                  <span className="score-pct"> ({Math.round(p.percentage)}%)</span>
                                )}
                              </span>
                            ) : (
                              <span className="dash">—</span>
                            )}
                          </td>
                          <td>
                            {p.hasIncidents ? (
                              <span className="stamp stamp-warn" title={p.incidents.map((i: any) => i.eventDetail || i.eventType).join(', ')}>
                                ⚠ {p.incidents.length}
                              </span>
                            ) : (
                              <span className="dash">None</span>
                            )}
                          </td>
                          <td>
                            <span className={`stamp stamp-${p.status === 'completed' ? 'pass' : p.status === 'pending' ? 'warn' : p.status === 'incomplete' ? 'neutral' : p.status === 'submitted' ? 'info' : 'neutral'}`}>
                              {p.status === 'completed' && <><img src="/icons/status/completed.png" alt="" className="status-icon" /> Completed</>}
                              {p.status === 'pending' && <><img src="/icons/status/pending-review.png" alt="" className="status-icon" /> Pending Review</>}
                              {p.status === 'incomplete' && (
                                <span title={p.statusDetail || ''}><img src="/icons/status/incomplete.png" alt="" className="status-icon" /> Incomplete</span>
                              )}
                              {p.status === 'submitted' && <><img src="/icons/status/submitted.png" alt="" className="status-icon" /> Submitted</>}
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
                                navigate(`/grading/review/${p.attemptId}?studentId=${p.studentId}`);
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

          </div> {/* end folder-body */}

        </motion.div>
      </div>
      {successToast && <div className="toast-success">{successToast}</div>}
    </MainLayout>
  );
}
