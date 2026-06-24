import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { useAuth } from '../contexts/AuthContext';
import { StudentService, StudentData, RegistrationFields } from '../services/StudentService';
import { Button } from '../components/Button';
import '../styles/pages/exam-form.css';
import '../styles/pages/students.css';

const STUDENT_PORTAL_URL = import.meta.env.VITE_STUDENT_PORTAL_URL || 'https://examforge-student-portal.onrender.com';

export function StudentManagementPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { examId } = useParams<{ examId?: string }>();
  const instructorId = user?.id || '';

  const [students, setStudents] = useState<StudentData[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);
  const [regLink, setRegLink] = useState<string>('');

  // Registration field config
  const [fields, setFields] = useState<RegistrationFields>({ sections: [], years: [], courses: [] });
  const [fieldInputs, setFieldInputs] = useState({ section: '', year: '', course: '' });
  const [savingFields, setSavingFields] = useState(false);

  useEffect(() => {
    if (instructorId) {
      loadData();
    }
  }, [instructorId]);

  const loadData = async () => {
    try {
      setLoading(true);
      const [studentsData, fieldsData] = await Promise.all([
        StudentService.getStudents(instructorId),
        StudentService.getRegistrationFields(instructorId),
      ]);
      setStudents(studentsData);
      setFields(fieldsData);
    } catch (err: any) {
      setError(err.message || 'Failed to load data');
    } finally {
      setLoading(false);
    }
  };

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
    if (fields[`${type}s` as keyof RegistrationFields].includes(value)) return;

    const updated = { ...fields };
    if (type === 'section') updated.sections = [...updated.sections, value];
    else if (type === 'year') updated.years = [...updated.years, value];
    else updated.courses = [...updated.courses, value];

    setFields(updated);
    setFieldInputs(prev => ({ ...prev, [type]: '' }));
  };

  const removeFieldOption = (type: 'section' | 'year' | 'course', value: string) => {
    const updated = { ...fields };
    if (type === 'section') updated.sections = fields.sections.filter(s => s !== value);
    else if (type === 'year') updated.years = fields.years.filter(y => y !== value);
    else updated.courses = fields.courses.filter(c => c !== value);
    setFields(updated);
  };

  const saveFieldOptions = async () => {
    try {
      setSavingFields(true);
      await StudentService.saveRegistrationFields(instructorId, fields);
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
              <p className="page-subtitle">Configure registration fields and manage your examinees</p>
            </div>
          </div>

          {error && (
            <div className="error-banner">
              <span>{error}</span>
              <button className="error-banner-dismiss" onClick={() => setError(null)} aria-label="Dismiss">×</button>
            </div>
          )}

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
                  {fields.sections.map(s => (
                    <span key={s} className="section-tag">{s} <button type="button" onClick={() => removeFieldOption('section', s)}>×</button></span>
                  ))}
                  {fields.sections.length === 0 && <span className="field-empty">No sections configured</span>}
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
                  {fields.years.map(y => (
                    <span key={y} className="section-tag">{y} <button type="button" onClick={() => removeFieldOption('year', y)}>×</button></span>
                  ))}
                  {fields.years.length === 0 && <span className="field-empty">No years configured</span>}
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
                  {fields.courses.map(c => (
                    <span key={c} className="section-tag">{c} <button type="button" onClick={() => removeFieldOption('course', c)}>×</button></span>
                  ))}
                  {fields.courses.length === 0 && <span className="field-empty">No courses configured</span>}
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
                      <th>Student ID</th>
                      <th>Section</th>
                      <th>Year</th>
                      <th>Course</th>
                      <th>Email</th>
                      <th>Registered</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.map(s => (
                      <tr key={s.uid}>
                        <td className="student-name-cell">{s.name}</td>
                        <td><code className="student-id-code">{s.studentId}</code></td>
                        <td>{s.section}</td>
                        <td>{s.year}</td>
                        <td>{s.course}</td>
                        <td className="student-email-cell">{s.email}</td>
                        <td className="student-date-cell">
                          {new Date(s.registeredAt).toLocaleDateString()}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

        </motion.div>
      </div>
      {successToast && <div className="toast-success">{successToast}</div>}
    </MainLayout>
  );
}
