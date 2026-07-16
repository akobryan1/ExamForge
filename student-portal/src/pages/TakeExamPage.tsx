import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ExamService } from '../services/ExamService';

export function TakeExamPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();
  const [exam, setExam] = useState<any>(null);
  const [questions, setQuestions] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [currentQ, setCurrentQ] = useState(0);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!examId) return;
    loadExam();
  }, [examId]);

  const loadExam = async () => {
    try {
      const examData = await ExamService.getExamById(examId!);
      setExam(examData);
      const qs = await ExamService.getExamQuestions(examId!);
      setQuestions(qs);
    } catch (err: any) {
      setError(err.message || 'Failed to load exam');
    } finally { setLoading(false); }
  };

  const startExam = async () => {
    try {
      setLoading(true);
      const attempt = await ExamService.startExam(examId!);
      setAttemptId(attempt.id);
    } catch (err: any) {
      setError(err.message || 'Failed to start exam');
    } finally { setLoading(false); }
  };

  const handleAnswer = async (questionId: string, answer: string) => {
    setAnswers(prev => ({ ...prev, [questionId]: answer }));
    if (attemptId) {
      try {
        await ExamService.submitAnswer(attemptId, questionId, answer);
      } catch {}
    }
  };

  const finishExam = async () => {
    try {
      setSubmitting(true);
      if (attemptId) await ExamService.submitExam(attemptId);
      navigate(`/attempts/${attemptId}/results`);
    } catch (err: any) {
      setError(err.message || 'Failed to submit');
    } finally { setSubmitting(false); }
  };

  if (loading && !attemptId) return <div className="loading-container"><div className="spinner" /> <span>Loading exam...</span></div>;
  if (error) return <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}><p style={{ color: 'var(--ledger-red)' }}>{error}</p></div></div>;

  // Start screen
  if (!attemptId && exam) {
    return (
      <div className="auth-container">
        <div className="auth-content" style={{ maxWidth: 600 }}>
          <div className="card" style={{ padding: 32 }}>
            <h1 className="preview-title">{exam.title}</h1>
            <p className="preview-desc">{exam.description}</p>
            <div className="info-grid">
              <div className="info-item"><strong>Questions</strong>{questions.length}</div>
              <div className="info-item"><strong>Time limit</strong>{exam.timeLimit ? `${exam.timeLimit} min` : 'Untimed'}</div>
            </div>
            <div className="actions-row">
              <button className="btn btn-secondary" onClick={() => navigate(`/exams/${examId}`)}>Back</button>
              <button className="btn btn-primary" onClick={startExam}>Start exam</button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // Exam taking view
  const question = questions[currentQ];
  if (!question) return <div className="loading-container"><span>Loading questions...</span></div>;

  return (
    <div className="take-shell">
      {/* Minimal header */}
      <div className="take-header">
        <div>
          <h2>{exam?.title}</h2>
          <span className="counter">Question {currentQ + 1} of {questions.length}</span>
        </div>
        <div className="remaining-chip">
          {questions.length - currentQ} left
        </div>
      </div>

      {/* Question area */}
      <div className="take-body">
        <div className="take-question-wrap">
          {question.type === 'multiple_choice' && question.choices && (
            <div className="mc-options">
              <p className="take-question-text">{question.questionText}</p>
              {question.choices.map((choice: any, i: number) => (
                <label key={i} className="option-label">
                  <input type="radio" name={`q-${question.id}`} checked={answers[question.id] === choice.text} onChange={() => handleAnswer(question.id, choice.text)} />
                  <span className="option-bubble"></span>
                  <span className="option-text">{choice.text}</span>
                </label>
              ))}
            </div>
          )}
          {question.type === 'true_false' && (
            <div>
              <p className="take-question-text">{question.questionText}</p>
              <div className="tf-buttons">
                {['True', 'False'].map(val => (
                  <button key={val} className={`tf-btn ${answers[question.id] === val.toLowerCase() ? 'selected' : ''}`} onClick={() => handleAnswer(question.id, val.toLowerCase())}>
                    {val}
                  </button>
                ))}
              </div>
            </div>
          )}
          {(question.type === 'essay' || question.type === 'short_answer') && (
            <div>
              <p className="take-question-text">{question.questionText}</p>
              <textarea className="take-textarea" rows={question.type === 'essay' ? 10 : 4} value={answers[question.id] || ''} onChange={(e) => handleAnswer(question.id, e.target.value)} placeholder="Type your answer here..." />
            </div>
          )}

          {/* Navigation */}
          <div className="take-nav">
            <button className="btn btn-secondary" onClick={() => setCurrentQ(Math.max(0, currentQ - 1))} disabled={currentQ === 0}>Previous</button>
            {currentQ < questions.length - 1 ? (
              <button className="btn btn-primary" onClick={() => setCurrentQ(currentQ + 1)}>Next</button>
            ) : (
              <button className="btn btn-primary" onClick={finishExam} disabled={submitting}>
                {submitting ? 'Submitting...' : 'Submit exam'}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
