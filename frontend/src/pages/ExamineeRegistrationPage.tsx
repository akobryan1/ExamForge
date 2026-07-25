import { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { StudentService, RegistrationFields } from '../services/StudentService';
import { Button } from '../components/Button';
import '../styles/auth.css';
import '../styles/pages/register.css';

export function ExamineeRegistrationPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const instructorId = searchParams.get('instructor') || '';

  const [fields, setFields] = useState<RegistrationFields>({ sections: [], years: [], courses: [] });
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const [formData, setFormData] = useState({
    name: '',
    studentId: '',
    section: '',
    year: '',
    course: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!instructorId) {
      setError('Invalid registration link. No instructor specified.');
      setLoading(false);
      return;
    }

    StudentService.getRegistrationFields(instructorId)
      .then(data => {
        setFields(data);
        setLoading(false);
      })
      .catch(() => {
        // Fields not configured yet — that's OK, student can type manually
        setLoading(false);
      });
  }, [instructorId]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFieldErrors(prev => {
      const next = { ...prev };
      delete next[name];
      return next;
    });
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const validate = (): boolean => {
    const errors: Record<string, string> = {};
    if (!formData.name.trim()) errors.name = 'Name is required';
    if (!formData.studentId.trim()) errors.studentId = 'Student ID is required';
    if (!formData.section.trim()) errors.section = 'Section is required';
    if (!formData.year.trim()) errors.year = 'Year is required';
    if (!formData.course.trim()) errors.course = 'Course is required';
    if (!formData.email.trim()) errors.email = 'Email is required';
    else if (!/\S+@\S+\.\S+/.test(formData.email)) errors.email = 'Enter a valid email address';
    if (!formData.password) errors.password = 'Password is required';
    else if (formData.password.length < 6) errors.password = 'Password must be at least 6 characters';
    if (formData.password !== formData.confirmPassword) errors.confirmPassword = 'Passwords do not match';

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;

    try {
      setSubmitting(true);
      setError(null);

      await StudentService.registerStudent({
        instructorId,
        name: formData.name,
        studentId: formData.studentId,
        section: formData.section,
        year: formData.year,
        course: formData.course,
        email: formData.email,
        password: formData.password,
      });

      setSuccess(true);
    } catch (err: any) {
      const msg = err.response?.data?.error || err.message || 'Registration failed';
      setError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="auth-container">
        <div className="auth-content loading-text">
          Loading registration form...
        </div>
      </div>
    );
  }

  if (!instructorId) {
    return (
      <div className="auth-container">
        <div className="auth-content center-state">
          <h1 className="auth-title" style={{ marginBottom: 12 }}>Invalid link</h1>
          <p className="center-text">
            This registration link is missing required information.
          </p>
          <Button variant="primary" onClick={() => navigate('/')}>Go home</Button>
        </div>
      </div>
    );
  }

  if (success) {
    return (
      <div className="auth-container">
        <div className="auth-content center-state">
          <div className="auth-card">
            <div className="success-seal"><span className="success-seal-check"><img src="/icons/status/completed.png" alt="" className="seal-icon" /></span></div>
            <div className="success-label">Enrollment confirmed</div>
            <h1 className="auth-title" style={{ marginBottom: 8 }}>Registration successful</h1>
            <p className="center-text" style={{ marginBottom: 24 }}>
              Your account has been created. You can now log in with your student ID and password.
            </p>
            <Button variant="primary" onClick={() => navigate('/login')}>Go to login</Button>
          </div>
        </div>
      </div>
    );
  }

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
              <input
                type="text"
                name="name"
                value={formData.name}
                onChange={handleChange}
                placeholder="Enter your full name"
                className={fieldErrors.name ? 'input error' : 'input'}
              />
              {fieldErrors.name && <p className="form-error">{fieldErrors.name}</p>}
            </div>

            <div className="form-group">
              <label>Student ID <span className="required">*</span></label>
              <input
                type="text"
                name="studentId"
                value={formData.studentId}
                onChange={handleChange}
                placeholder="Your student identification number"
                className={fieldErrors.studentId ? 'input error' : 'input'}
              />
              {fieldErrors.studentId && <p className="form-error">{fieldErrors.studentId}</p>}
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Section <span className="required">*</span></label>
                {fields.sections.length > 0 ? (
                  <select
                    name="section"
                    value={formData.section}
                    onChange={handleChange}
                    className={fieldErrors.section ? 'input error' : 'input'}
                  >
                    <option value="">Select section</option>
                    {fields.sections.map(s => <option key={s} value={s}>{s}</option>)}
                  </select>
                ) : (
                  <input
                    type="text"
                    name="section"
                    value={formData.section}
                    onChange={handleChange}
                    placeholder="e.g., Section A"
                    className={fieldErrors.section ? 'input error' : 'input'}
                  />
                )}
                {fieldErrors.section && <p className="form-error">{fieldErrors.section}</p>}
              </div>

              <div className="form-group">
                <label>Year <span className="required">*</span></label>
                {fields.years.length > 0 ? (
                  <select
                    name="year"
                    value={formData.year}
                    onChange={handleChange}
                    className={fieldErrors.year ? 'input error' : 'input'}
                  >
                    <option value="">Select year</option>
                    {fields.years.map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                ) : (
                  <input
                    type="text"
                    name="year"
                    value={formData.year}
                    onChange={handleChange}
                    placeholder="e.g., 1st Year"
                    className={fieldErrors.year ? 'input error' : 'input'}
                  />
                )}
                {fieldErrors.year && <p className="form-error">{fieldErrors.year}</p>}
              </div>
            </div>

            <div className="form-group">
              <label>Course <span className="required">*</span></label>
              {fields.courses.length > 0 ? (
                <select
                  name="course"
                  value={formData.course}
                  onChange={handleChange}
                  className={fieldErrors.course ? 'input error' : 'input'}
                >
                  <option value="">Select course</option>
                  {fields.courses.map(c => <option key={c} value={c}>{c}</option>)}
                </select>
              ) : (
                <input
                  type="text"
                  name="course"
                  value={formData.course}
                  onChange={handleChange}
                  placeholder="e.g., BS Computer Science"
                  className={fieldErrors.course ? 'input error' : 'input'}
                />
              )}
              {fieldErrors.course && <p className="form-error">{fieldErrors.course}</p>}
            </div>

            <div className="form-group">
              <label>Email <span className="required">*</span></label>
              <input
                type="email"
                name="email"
                value={formData.email}
                onChange={handleChange}
                placeholder="your@email.com"
                className={fieldErrors.email ? 'input error' : 'input'}
              />
              {fieldErrors.email && <p className="form-error">{fieldErrors.email}</p>}
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Password <span className="required">*</span></label>
                <input
                  type="password"
                  name="password"
                  value={formData.password}
                  onChange={handleChange}
                  placeholder="Min. 6 characters"
                  className={fieldErrors.password ? 'input error' : 'input'}
                />
                {fieldErrors.password && <p className="form-error">{fieldErrors.password}</p>}
              </div>

              <div className="form-group">
                <label>Confirm password <span className="required">*</span></label>
                <input
                  type="password"
                  name="confirmPassword"
                  value={formData.confirmPassword}
                  onChange={handleChange}
                  placeholder="Repeat password"
                  className={fieldErrors.confirmPassword ? 'input error' : 'input'}
                />
                {fieldErrors.confirmPassword && <p className="form-error">{fieldErrors.confirmPassword}</p>}
              </div>
            </div>

            <button
              type="submit"
              className="btn btn-primary auth-submit"
              disabled={submitting}
            >
              {submitting ? 'Creating account...' : 'Register'}
            </button>
          </form>

          <div className="auth-footer">
            <p>
              Already have an account?{' '}
              <a href="/login" className="auth-link-bold">Sign in</a>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
