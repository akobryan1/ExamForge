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
  if (error) return (
    <div className="auth-container">
      <div className="auth-content" style={{ textAlign: 'center' }}>
        <div className="card" style={{ padding: 32 }}>
          <p style={{ color: 'var(--color-error-600)' }}>{error}</p>
        </div>
      </div>
    </div>
  );
  if (!attempt) return null;

  const passed = attempt.passed || (attempt.percentage || 0) >= (attempt.passingScore || 60);

  return (
    <div className="auth-container">
      <div className="auth-content" style={{ maxWidth: 520 }}>
        <div className={`results-header ${passed ? 'passed' : 'failed'}`}>
          <div className="results-icon">{passed ? '🎉' : '📝'}</div>
          <h1>{passed ? 'Congratulations!' : 'Exam completed'}</h1>
          <p className="results-message">{passed ? 'You passed the exam!' : 'Review your results below.'}</p>
        </div>

        <div className="card score-card">
          <div className="score-main">
            <div className="score-value">{attempt.percentage?.toFixed(0) || 0}%</div>
            <div className="score-details">{attempt.score} out of {attempt.totalPoints || 100} points</div>
          </div>
          <div className="score-breakdown">
            <div className="breakdown-item">
              <span className="breakdown-label">Exam</span>
              <span className="breakdown-value">{attempt.examTitle || 'N/A'}</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Status</span>
              <span className="breakdown-value">
                <span className={`badge ${passed ? 'badge-completed' : 'badge-draft'}`}>
                  {passed ? 'Passed' : 'Failed'}
                </span>
              </span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Submitted</span>
              <span className="breakdown-value">
                {attempt.submittedAt ? new Date(attempt.submittedAt).toLocaleString() : 'N/A'}
              </span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Time taken</span>
              <span className="breakdown-value">
                {attempt.timeSpent ? `${Math.floor(attempt.timeSpent / 60)} min` : 'N/A'}
              </span>
            </div>
          </div>
        </div>

        <div className="results-actions">
          <button className="btn btn-primary btn-lg" onClick={() => navigate('/')} style={{ width: '100%' }}>
            Back to home
          </button>
        </div>
      </div>
    </div>
  );
}
