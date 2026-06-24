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
  if (error) return <div className="auth-container"><div className="auth-content" style={{ textAlign: 'center' }}><p style={{ color: 'var(--color-error-600)' }}>{error}</p></div></div>;

  // Start screen
  if (!attemptId && exam) {
    return (
      <div className="auth-container">
        <div className="auth-content" style={{ maxWidth: 600 }}>
          <div className="card" style={{ padding: 32 }}>
            <h1 className="auth-title" style={{ marginBottom: 8 }}>{exam.title}</h1>
            <p style={{ color: 'var(--color-gray-3)', marginBottom: 24, fontSize: 14 }}>{exam.description}</p>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 24, padding: 16, background: 'var(--color-surface)', borderRadius: 'var(--radius-md)' }}>
              <div><strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase' }}>Questions</strong><p style={{ marginTop: 4, fontSize: 14 }}>{questions.length}</p></div>
              <div><strong style={{ fontSize: 11, color: 'var(--color-gray-2)', textTransform: 'uppercase' }}>Time limit</strong><p style={{ marginTop: 4, fontSize: 14 }}>{exam.timeLimit ? `${exam.timeLimit} min` : 'Untimed'}</p></div>
            </div>
            <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
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
    <div style={{ minHeight: '100vh', background: 'var(--color-surface)', display: 'flex', flexDirection: 'column' }}>
      {/* Minimal header */}
      <div style={{ background: 'white', borderBottom: '1px solid var(--color-border)', padding: '12px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h2 style={{ fontFamily: 'var(--font-display)', fontSize: 16, margin: 0 }}>{exam?.title}</h2>
          <span style={{ fontSize: 12, color: 'var(--color-gray-2)' }}>Question {currentQ + 1} of {questions.length}</span>
        </div>
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: 18, fontWeight: 600, color: 'var(--color-primary-900)', padding: '8px 16px', background: 'var(--color-surface)', borderRadius: 'var(--radius-md)' }}>
          {questions.length - currentQ} left
        </div>
      </div>

      {/* Question area */}
      <div style={{ flex: 1, display: 'flex', justifyContent: 'center', padding: 24 }}>
        <div style={{ maxWidth: 700, width: '100%' }}>
          {question.type === 'multiple_choice' && question.choices && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
              <p style={{ fontSize: 16, fontWeight: 500, marginBottom: 16, lineHeight: 1.6 }}>{question.questionText}</p>
              {question.choices.map((choice: any, i: number) => (
                <label key={i} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: 14, border: `2px solid ${answers[question.id] === choice.text ? 'var(--color-accent-500)' : 'var(--color-border)'}`, borderRadius: 'var(--radius-md)', cursor: 'pointer', transition: 'border-color 0.15s' }}>
                  <input type="radio" name={`q-${question.id}`} checked={answers[question.id] === choice.text} onChange={() => handleAnswer(question.id, choice.text)} style={{ width: 18, height: 18, accentColor: 'var(--color-accent-500)' }} />
                  <span style={{ fontSize: 14 }}>{choice.text}</span>
                </label>
              ))}
            </div>
          )}
          {question.type === 'true_false' && (
            <div>
              <p style={{ fontSize: 16, fontWeight: 500, marginBottom: 16, lineHeight: 1.6 }}>{question.questionText}</p>
              <div style={{ display: 'flex', gap: 12 }}>
                {['True', 'False'].map(val => (
                  <button key={val} className={`btn ${answers[question.id] === val.toLowerCase() ? 'btn-primary' : 'btn-secondary'}`} onClick={() => handleAnswer(question.id, val.toLowerCase())} style={{ flex: 1, padding: '14px 24px' }}>
                    {val}
                  </button>
                ))}
              </div>
            </div>
          )}
          {(question.type === 'essay' || question.type === 'short_answer') && (
            <div>
              <p style={{ fontSize: 16, fontWeight: 500, marginBottom: 16, lineHeight: 1.6 }}>{question.questionText}</p>
              <textarea className="input" rows={question.type === 'essay' ? 10 : 4} value={answers[question.id] || ''} onChange={(e) => handleAnswer(question.id, e.target.value)} placeholder="Type your answer here..." />
            </div>
          )}

          {/* Navigation */}
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 32, paddingTop: 24, borderTop: '1px solid var(--color-border)' }}>
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
