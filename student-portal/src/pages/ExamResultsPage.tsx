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
  if (error) return <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}><p style={{ color: 'var(--ledger-red)' }}>{error}</p></div></div>;
  if (!attempt) return null;

  return (
    <div className="auth-container">
      <div className="auth-content" style={{ maxWidth: 600 }}>
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <h1 className="auth-title" style={{ marginBottom: 8 }}>
            {attempt.passed ? 'Congratulations!' : 'Exam completed'}
          </h1>
          <p className="center-note" style={{ marginBottom: 24 }}>
            {attempt.passed ? 'You passed the exam' : 'Review your results below'}
          </p>

          <div className="grade-circle-big">{attempt.percentage?.toFixed(0) || 0}%</div>
          <div className="verdict-stamp" style={{ marginBottom: 24 }}>{attempt.passed ? '✓ Passed' : 'Keep Practicing'}</div>
          <p style={{ color: 'var(--ledger-ink-soft)', fontSize: 14, marginBottom: 24 }}>
            {attempt.score} out of {attempt.totalPoints || 100} points
          </p>

          <div className="info-grid" style={{ textAlign: 'left' }}>
            <div className="info-item"><strong>Exam</strong>{attempt.examTitle}</div>
            <div className="info-item"><strong>Status</strong><span className={`stamp ${attempt.passed ? 'stamp-completed' : 'stamp-draft'}`}>{attempt.passed ? 'Passed' : 'Failed'}</span></div>
          </div>

          <div style={{ marginTop: 24 }}>
            <button className="btn btn-primary" onClick={() => navigate('/')}>Back to home</button>
          </div>
        </div>
      </div>
    </div>
  );
}
