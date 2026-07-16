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
  if (error) return <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}><h1 className="auth-title" style={{ marginBottom: 12 }}>Exam not found</h1><p className="center-note">{error}</p></div></div>;
  if (!exam) return null;

  return (
    <div className="auth-container">
      <div className="auth-content" style={{ maxWidth: 600 }}>
        <div className="card" style={{ padding: 32 }}>
          <h1 className="preview-title">{exam.title}</h1>
          <p className="preview-desc">{exam.description}</p>
          
          <div className="info-grid">
            <div className="info-item"><strong>Subject</strong>{exam.subject || 'N/A'}</div>
            <div className="info-item"><strong>Questions</strong>{exam.questionCount || 0}</div>
            <div className="info-item"><strong>Points</strong>{exam.totalPoints || 0}</div>
            <div className="info-item"><strong>Time limit</strong>{exam.timeLimit ? `${exam.timeLimit} min` : 'Untimed'}</div>
            <div className="info-item"><strong>Passing score</strong>{exam.passingScore || 70}%</div>
            <div className="info-item"><strong>Status</strong><span className={`stamp ${exam.status === 'published' || exam.status === 'active' ? 'stamp-live' : 'stamp-draft'}`}>{exam.status}</span></div>
          </div>

          <div className="actions-row">
            <button className="btn btn-primary" onClick={() => navigate(`/exams/${examId}/take`)} disabled={exam.status !== 'published' && exam.status !== 'active'}>
              Take exam
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
