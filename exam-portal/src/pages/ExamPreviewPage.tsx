import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ExamService } from '../services/ExamService';

export function ExamPreviewPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();
  const [exam, setExam] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!examId) return;
    ExamService.getExamById(examId)
      .then(data => setExam(data))
      .catch(err => setError(err.message || 'Exam not found'))
      .finally(() => setLoading(false));
  }, [examId]);

  if (loading) return <div className="loading-container"><div className="spinner" /> <span>Loading exam...</span></div>;
  if (error) return (
    <div className="auth-container">
      <div className="auth-content" style={{ textAlign: 'center' }}>
        <div className="card" style={{ padding: 32 }}>
          <div style={{ fontSize: 48, marginBottom: 12 }}>📝</div>
          <h1 className="auth-title" style={{ marginBottom: 8 }}>Exam not found</h1>
          <p style={{ color: 'var(--color-gray-3)', marginBottom: 24 }}>{error}</p>
        </div>
      </div>
    </div>
  );
  if (!exam) return null;

  const canTake = exam.status === 'published' || exam.status === 'active';

  return (
    <div className="auth-container">
      <div className="auth-content" style={{ maxWidth: 600 }}>
        <div className="card" style={{ padding: 32 }}>
          <h1 className="auth-title" style={{ marginBottom: 8 }}>{exam.title}</h1>
          <p style={{ color: 'var(--color-gray-3)', marginBottom: 24, fontSize: 14 }}>{exam.description || 'No description provided.'}</p>

          <div style={{
            display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 24,
            padding: 16, background: 'var(--color-surface)', borderRadius: 'var(--radius-md)'
          }}>
            <div>
              <strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Subject</strong>
              <p style={{ marginTop: 4, fontSize: 14 }}>{exam.subject || 'N/A'}</p>
            </div>
            <div>
              <strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Questions</strong>
              <p style={{ marginTop: 4, fontSize: 14 }}>{exam.questionCount || 0}</p>
            </div>
            <div>
              <strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Points</strong>
              <p style={{ marginTop: 4, fontSize: 14 }}>{exam.totalPoints || 0}</p>
            </div>
            <div>
              <strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Time limit</strong>
              <p style={{ marginTop: 4, fontSize: 14 }}>{exam.timeLimit ? `${exam.timeLimit} min` : 'Untimed'}</p>
            </div>
            <div>
              <strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Passing score</strong>
              <p style={{ marginTop: 4, fontSize: 14 }}>{exam.passingScore || 70}%</p>
            </div>
            <div>
              <strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase', letterSpacing: '0.06em' }}>Status</strong>
              <p style={{ marginTop: 4, fontSize: 14 }}>
                <span className={`badge ${canTake ? 'badge-live' : 'badge-draft'}`}>
                  {exam.status}
                </span>
              </p>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
            <button
              className="btn btn-primary btn-lg"
              onClick={() => navigate(`/exams/${examId}/take`)}
              disabled={!canTake}
              style={{ width: '100%' }}
            >
              {canTake ? 'Start exam' : 'Exam not available'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
