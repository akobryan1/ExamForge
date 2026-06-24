import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ExamService } from '../services/ExamService';

export function ExamResultsPage() {
  const { attemptId } = useParams<{ attemptId: string }>();
  const navigate = useNavigate();
  const [attempt, setAttempt] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!attemptId) return;
    ExamService.getExamAttempt(attemptId)
      .then(data => setAttempt(data))
      .catch(err => setError(err.message || 'Failed to load results'))
      .finally(() => setLoading(false));
  }, [attemptId]);

  if (loading) return <div className="loading-container"><div className="spinner" /> <span>Loading results...</span></div>;
  if (error) return <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}><p style={{ color: 'var(--color-error-600)' }}>{error}</p></div></div>;
  if (!attempt) return null;

  return (
    <div className="auth-container">
      <div className="auth-content" style={{ maxWidth: 600 }}>
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <h1 className="auth-title" style={{ marginBottom: 8 }}>
            {attempt.passed ? 'Congratulations!' : 'Exam completed'}
          </h1>
          <p style={{ color: 'var(--color-gray-3)', marginBottom: 24, fontSize: 14 }}>
            {attempt.passed ? 'You passed the exam' : 'Review your results below'}
          </p>

          <div style={{ fontSize: 56, fontWeight: 700, fontFamily: 'var(--font-display)', color: attempt.passed ? 'var(--color-success-600)' : 'var(--color-primary-900)', marginBottom: 8 }}>
            {attempt.percentage?.toFixed(0) || 0}%
          </div>
          <p style={{ color: 'var(--color-gray-2)', fontSize: 14, marginBottom: 24 }}>
            {attempt.score} out of {attempt.totalPoints || 100} points
          </p>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 24, padding: 16, background: 'var(--color-surface)', borderRadius: 'var(--radius-md)', textAlign: 'left' }}>
            <div><strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Exam</strong><p style={{ marginTop: 4, fontSize: 14 }}>{attempt.examTitle}</p></div>
            <div><strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Status</strong><p style={{ marginTop: 4, fontSize: 14 }}><span className={`badge ${attempt.passed ? 'badge-completed' : 'badge-draft'}`}>{attempt.passed ? 'Passed' : 'Failed'}</span></p></div>
          </div>

          <button className="btn btn-primary" onClick={() => navigate('/')}>Back to home</button>
        </div>
      </div>
    </div>
  );
}
