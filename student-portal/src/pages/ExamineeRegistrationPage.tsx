import { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { StudentService, RegistrationFields } from '../services/StudentService';

export function ExamineeRegistrationPage() {
  console.log('[RegisterPage] Component rendering');
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const instructorId = searchParams.get('instructor') || '';
  console.log('[RegisterPage] instructorId:', instructorId);

  const [fields, setFields] = useState<RegistrationFields>({ sections: [], years: [], courses: [] });
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const [formData, setFormData] = useState({
    name: '', studentId: '', section: '', year: '', course: '',
    email: '', password: '', confirmPassword: '',
  });
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!instructorId) { setError('Invalid registration link.'); setLoading(false); return; }
    StudentService.getRegistrationFields(instructorId)
      .then(data => { setFields(data); setLoading(false); })
      .catch(() => setLoading(false));
  }, [instructorId]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFieldErrors(prev => { const n = { ...prev }; delete n[name]; return n; });
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const validate = () => {
    const errors: Record<string, string> = {};
    if (!formData.name.trim()) errors.name = 'Name is required';
    if (!formData.studentId.trim()) errors.studentId = 'Student ID is required';
    if (!formData.section.trim()) errors.section = 'Section is required';
    if (!formData.year.trim()) errors.year = 'Year is required';
    if (!formData.course.trim()) errors.course = 'Course is required';
    if (!formData.email.trim()) errors.email = 'Email is required';
    else if (!/\S+@\S+\.\S+/.test(formData.email)) errors.email = 'Enter a valid email';
    if (!formData.password) errors.password = 'Password is required';
    else if (formData.password.length < 6) errors.password = 'At least 6 characters';
    if (formData.password !== formData.confirmPassword) errors.confirmPassword = 'Passwords do not match';
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    try {
      setSubmitting(true); setError(null);
      await StudentService.registerStudent({ instructorId, ...formData });
      setSuccess(true);
    } catch (err: any) {
      setError(err.response?.data?.error || err.message || 'Registration failed');
    } finally { setSubmitting(false); }
  };

  if (loading) return <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center', color: 'var(--color-gray-3)' }}>Loading...</div></div>;
  if (!instructorId) return (
    <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}>
      <div className="auth-card" style={{ maxWidth: 420 }}>
        <div style={{ fontSize: 48, marginBottom: 16 }}>📝</div>
        <h1 className="auth-title" style={{ marginBottom: 8 }}>Student Registration</h1>
        <p style={{ color: 'var(--color-gray-3)', marginBottom: 24, lineHeight: 1.6 }}>
          To register, please use the registration link provided by your instructor or school administrator.
        </p>
        <p style={{ color: 'var(--color-gray-3)', marginBottom: 24, fontSize: 'var(--font-size-sm)' }}>
          If you already have an account, <a href="/login" className="auth-link-bold">sign in here</a>.
        </p>
      </div>
    </div></div>
  );
  if (success) return (
    <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}>
      <div className="auth-card">
        <div style={{ fontSize: 48, marginBottom: 16 }}>✓</div>
        <h1 className="auth-title" style={{ marginBottom: 8 }}>Registration successful</h1>
        <p style={{ color: 'var(--color-gray-3)', marginBottom: 24 }}>Your account has been created. You can now log in.</p>
        <button className="btn btn-primary" onClick={() => navigate('/login')}>Go to login</button>
      </div>
    </div></div>
  );

  return (
    <div className="auth-container">
      <div className="auth-content">
        <div className="auth-card">
          <div className="auth-header">
            <h1 className="auth-title">Student registration</h1>
            <p className="auth-subtitle">Create your account to access exams</p>
          </div>
          {error && <div className="auth-error">{error}</div>}
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label>Full name <span className="required">*</span></label>
              <input type="text" name="name" value={formData.name} onChange={handleChange} placeholder="Enter your full name" className={`input${fieldErrors.name ? ' error' : ''}`} />
              {fieldErrors.name && <p className="form-error">{fieldErrors.name}</p>}
            </div>
            <div className="form-group">
              <label>Student ID <span className="required">*</span></label>
              <input type="text" name="studentId" value={formData.studentId} onChange={handleChange} placeholder="Your student ID" className={`input${fieldErrors.studentId ? ' error' : ''}`} />
              {fieldErrors.studentId && <p className="form-error">{fieldErrors.studentId}</p>}
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Section <span className="required">*</span></label>
                {fields.sections.length > 0 ? (
                  <select name="section" value={formData.section} onChange={handleChange} className={`input${fieldErrors.section ? ' error' : ''}`}>
                    <option value="">Select</option>
                    {fields.sections.map(s => <option key={s} value={s}>{s}</option>)}
                  </select>
                ) : (
                  <input type="text" name="section" value={formData.section} onChange={handleChange} placeholder="Section A" className={`input${fieldErrors.section ? ' error' : ''}`} />
                )}
                {fieldErrors.section && <p className="form-error">{fieldErrors.section}</p>}
              </div>
              <div className="form-group">
                <label>Year <span className="required">*</span></label>
                {fields.years.length > 0 ? (
                  <select name="year" value={formData.year} onChange={handleChange} className={`input${fieldErrors.year ? ' error' : ''}`}>
                    <option value="">Select</option>
                    {fields.years.map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                ) : (
                  <input type="text" name="year" value={formData.year} onChange={handleChange} placeholder="1st Year" className={`input${fieldErrors.year ? ' error' : ''}`} />
                )}
                {fieldErrors.year && <p className="form-error">{fieldErrors.year}</p>}
              </div>
            </div>
            <div className="form-group">
              <label>Course <span className="required">*</span></label>
              {fields.courses.length > 0 ? (
                <select name="course" value={formData.course} onChange={handleChange} className={`input${fieldErrors.course ? ' error' : ''}`}>
                  <option value="">Select</option>
                  {fields.courses.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              ) : (
                <input type="text" name="course" value={formData.course} onChange={handleChange} placeholder="BS Computer Science" className={`input${fieldErrors.course ? ' error' : ''}`} />
              )}
              {fieldErrors.course && <p className="form-error">{fieldErrors.course}</p>}
            </div>
            <div className="form-group">
              <label>Email <span className="required">*</span></label>
              <input type="email" name="email" value={formData.email} onChange={handleChange} placeholder="your@email.com" className={`input${fieldErrors.email ? ' error' : ''}`} />
              {fieldErrors.email && <p className="form-error">{fieldErrors.email}</p>}
            </div>
            <div className="form-row">
              <div className="form-group">
                <label>Password <span className="required">*</span></label>
                <input type="password" name="password" value={formData.password} onChange={handleChange} placeholder="Min. 6 chars" className={`input${fieldErrors.password ? ' error' : ''}`} />
                {fieldErrors.password && <p className="form-error">{fieldErrors.password}</p>}
              </div>
              <div className="form-group">
                <label>Confirm <span className="required">*</span></label>
                <input type="password" name="confirmPassword" value={formData.confirmPassword} onChange={handleChange} placeholder="Repeat" className={`input${fieldErrors.confirmPassword ? ' error' : ''}`} />
                {fieldErrors.confirmPassword && <p className="form-error">{fieldErrors.confirmPassword}</p>}
              </div>
            </div>
            <button type="submit" className="btn btn-primary auth-submit" disabled={submitting}>
              {submitting ? 'Creating account...' : 'Register'}
            </button>
          </form>
          <div className="auth-footer"><p>Already have an account? <a href="/login" className="auth-link-bold">Sign in</a></p></div>
        </div>
      </div>
    </div>
  );
}
