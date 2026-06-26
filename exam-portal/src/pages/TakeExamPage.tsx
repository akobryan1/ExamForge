import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ExamService } from '../services/ExamService';

export function TakeExamPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();
  const [exam, setExam] = useState<any>(null);
  const [questions, setQuestions] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [starting, setStarting] = useState(false);
  const [currentQ, setCurrentQ] = useState(0);
  const [answers, setAnswers] = useState<Record<string, any>>({});
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [timeLeft, setTimeLeft] = useState<number | null>(null);
  const [showRules, setShowRules] = useState(false);
  const [error, setError] = useState('');
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const startTimeRef = useRef<Date>(new Date());
  // Guest info
  const [guestInfo, setGuestInfo] = useState({ name: '', studentId: '', course: '', year: '' });

  useEffect(() => {
    if (!examId) return;
    loadExam();
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, [examId]);

  useEffect(() => {
    if (exam && (exam.accessMethod === 'student_login' || exam.allowGuestAccess === false)) {
      const token = localStorage.getItem('accessToken');
      if (!token) {
        navigate(`/login?redirect=/exams/${examId}/take`);
      }
    }
  }, [exam, examId, navigate]);

  useEffect(() => {
    if (attemptId && exam?.proctorConfig?.enforceFullscreen) {
      const handleFs = () => {
        if (!document.fullscreenElement && attemptId) {
          recordViolation('exit_fullscreen');
        }
      };
      document.addEventListener('fullscreenchange', handleFs);
      return () => document.removeEventListener('fullscreenchange', handleFs);
    }
  }, [attemptId, exam]);

  useEffect(() => {
    if (attemptId && exam?.proctorConfig?.detectTabSwitch) {
      const handleVis = () => {
        if (document.hidden) recordViolation('tab_switch');
      };
      document.addEventListener('visibilitychange', handleVis);
      return () => document.removeEventListener('visibilitychange', handleVis);
    }
  }, [attemptId, exam]);

  useEffect(() => {
    if (attemptId && exam?.proctorConfig?.detectCopyPaste) {
      const handleCopy = () => recordViolation('copy_attempt');
      const handlePaste = () => recordViolation('paste_attempt');
      document.addEventListener('copy', handleCopy);
      document.addEventListener('paste', handlePaste);
      return () => {
        document.removeEventListener('copy', handleCopy);
        document.removeEventListener('paste', handlePaste);
      };
    }
  }, [attemptId, exam]);

  useEffect(() => {
    if (attemptId && exam?.proctorConfig?.disableRightClick) {
      const handleCtx = (e: MouseEvent) => { e.preventDefault(); recordViolation('right_click_attempt'); };
      document.addEventListener('contextmenu', handleCtx);
      return () => document.removeEventListener('contextmenu', handleCtx);
    }
  }, [attemptId, exam]);

  const loadExam = async () => {
    try {
      const examData = await ExamService.getExamById(examId!);
      setExam(examData);
      if (examData.showRulesBeforeExam) setShowRules(true);
      const qs = await ExamService.getExamQuestions(examId!);
      setQuestions(qs);
    } catch (err: any) {
      setError(err.message || 'Failed to load exam');
    } finally { setLoading(false); }
  };

  const recordViolation = async (type: string) => {
    if (!attemptId) return;
    try { await ExamService.recordViolation(attemptId, type); } catch {}
  };

  const startExam = async () => {
    try {
      setStarting(true);
      setError('');
      const payload: any = {};
      // If guest access, include guest info
      if (exam?.accessMethod === 'guest') {
        if (!guestInfo.name.trim() || !guestInfo.studentId.trim()) {
          setError('Name and Student ID are required for guest access');
          setStarting(false);
          return;
        }
        payload.guestInfo = guestInfo;
      }
      const attempt = await ExamService.startExam(examId!, payload);
      setAttemptId(attempt.id);
      startTimeRef.current = new Date();
      if (exam?.timeLimit) setTimeLeft(exam.timeLimit * 60);
      if (exam?.proctorConfig?.enforceFullscreen) {
        try { await document.documentElement.requestFullscreen(); } catch {}
      }
    } catch (err: any) {
      console.error('[TakeExam] startExam error:', err.response?.data || err.message);
      if (err.response?.status === 403) setError('Access denied. Please check your credentials.');
      else if (err.response?.data?.error) setError(err.response.data.error);
      else setError(err.message || 'Failed to start exam');
    } finally { setStarting(false); }
  };

  // Timer
  useEffect(() => {
    if (timeLeft === null || !attemptId) return;
    timerRef.current = setInterval(() => {
      setTimeLeft(prev => {
        if (prev === null || prev <= 1) {
          if (timerRef.current) clearInterval(timerRef.current);
          handleAutoSubmit();
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
    return () => { if (timerRef.current) clearInterval(timerRef.current); };
  }, [timeLeft, attemptId]);

  const handleAutoSubmit = async () => {
    if (!attemptId) return;
    try {
      await ExamService.submitExam(attemptId);
      navigate(`/attempts/${attemptId}/results`);
    } catch {}
  };

  const handleAnswerChange = async (questionId: string, value: any) => {
    setAnswers(prev => ({ ...prev, [questionId]: value }));
    if (attemptId) {
      const timeSpent = Math.floor((new Date().getTime() - startTimeRef.current.getTime()) / 1000);
      try { await ExamService.submitAnswer(attemptId, questionId, value, timeSpent); } catch {}
    }
  };

  const handleNext = async () => {
    if (currentQ < questions.length - 1) setCurrentQ(prev => prev + 1);
  };

  const handlePrev = () => {
    if (currentQ > 0) setCurrentQ(prev => prev - 1);
  };

  const handleSubmitExam = async () => {
    if (submitting) return;
    if (!confirm('Are you sure you want to submit? You cannot change your answers after submission.')) return;
    try {
      setSubmitting(true);
      if (attemptId) await ExamService.submitExam(attemptId);
      if (document.fullscreenElement) await document.exitFullscreen();
      navigate(`/attempts/${attemptId}/results`);
    } catch (err: any) {
      setError(err.message || 'Failed to submit exam');
    } finally { setSubmitting(false); }
  };

  const formatTime = (seconds: number) => {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    if (h > 0) return `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
    return `${m}:${s.toString().padStart(2, '0')}`;
  };

  const renderAnswer = (question: any) => {
    const answer = answers[question.id] || '';
    switch (question.type) {
      case 'multiple_choice':
        return (
          <div className="answer-options">
            {question.choices?.map((choice: any, i: number) => (
              <label key={i} className="option-label">
                <input
                  type="radio"
                  name={`q_${question.id}`}
                  checked={answer === (choice.text || choice)}
                  onChange={() => handleAnswerChange(question.id, choice.text || choice)}
                />
                <span className="option-text">{String.fromCharCode(65 + i)}. {choice.text || choice}</span>
              </label>
            ))}
          </div>
        );
      case 'true_false':
        return (
          <div className="answer-options">
            {['true', 'false'].map(val => (
              <label key={val} className="option-label">
                <input
                  type="radio"
                  name={`q_${question.id}`}
                  checked={answer === val}
                  onChange={() => handleAnswerChange(question.id, val)}
                />
                <span className="option-text">{val === 'true' ? 'True' : 'False'}</span>
              </label>
            ))}
          </div>
        );
      case 'identification':
        return (
          <input
            className="answer-input"
            type="text"
            value={answer}
            onChange={e => handleAnswerChange(question.id, e.target.value)}
            placeholder="Type your answer..."
          />
        );
      case 'essay':
        return (
          <textarea
            className="answer-textarea"
            value={answer}
            onChange={e => handleAnswerChange(question.id, e.target.value)}
            placeholder="Write your essay..."
            rows={10}
          />
        );
      default:
        return <p style={{ color: 'var(--color-gray-3)' }}>Unsupported question type: {question.type}</p>;
    }
  };

  // Pre-exam screen
  if (!attemptId && !error) {
    return (
      <div className="auth-container">
        <div className="auth-content" style={{ maxWidth: 520 }}>
          <div className="card" style={{ padding: 32 }}>
            <h1 className="auth-title" style={{ marginBottom: 8 }}>{exam?.title || 'Loading...'}</h1>
            {loading ? (
              <div style={{ textAlign: 'center', padding: 24 }}>
                <div className="spinner" />
                <p style={{ marginTop: 12, color: 'var(--color-gray-3)' }}>Loading exam...</p>
              </div>
            ) : (
              <>
                <p style={{ color: 'var(--color-gray-3)', marginBottom: 24, fontSize: 14 }}>
                  {exam?.description || ''}
                </p>

                {/* Guest info form */}
                {exam?.accessMethod === 'guest' && (
                  <div style={{ marginBottom: 24, display: 'flex', flexDirection: 'column', gap: 12 }}>
                    <div>
                      <label style={{ display: 'block', fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Full name *</label>
                      <input className="input" type="text" value={guestInfo.name} onChange={e => setGuestInfo(prev => ({ ...prev, name: e.target.value }))} placeholder="Enter your full name" />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Student ID *</label>
                      <input className="input" type="text" value={guestInfo.studentId} onChange={e => setGuestInfo(prev => ({ ...prev, studentId: e.target.value }))} placeholder="Your student ID" />
                    </div>
                    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
                      <div>
                        <label style={{ display: 'block', fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Course</label>
                        <input className="input" type="text" value={guestInfo.course} onChange={e => setGuestInfo(prev => ({ ...prev, course: e.target.value }))} placeholder="e.g. BS CS" />
                      </div>
                      <div>
                        <label style={{ display: 'block', fontSize: 12, fontWeight: 500, marginBottom: 4 }}>Year</label>
                        <input className="input" type="text" value={guestInfo.year} onChange={e => setGuestInfo(prev => ({ ...prev, year: e.target.value }))} placeholder="e.g. 2nd Year" />
                      </div>
                    </div>
                  </div>
                )}

                {error && <div className="error-banner">{error}</div>}
                <button className="btn btn-primary btn-lg" style={{ width: '100%' }} onClick={startExam} disabled={starting}>
                  {starting ? 'Starting...' : showRules ? 'I understand, start exam' : 'Start exam'}
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    );
  }

  // Taking exam
  const question = questions[currentQ];
  const progress = questions.length > 0 ? ((currentQ + 1) / questions.length) * 100 : 0;

  return (
    <div className="exam-taking-container">
      <div className="exam-header">
        <div className="exam-header-left">
          <h2>{exam?.title}</h2>
          <p className="question-counter">Question {currentQ + 1} of {questions.length}</p>
        </div>
        <div className="exam-header-right">
          {timeLeft !== null && (
            <div className={`timer ${timeLeft < 300 ? 'timer-warning' : ''}`}>
              ⏱ {formatTime(timeLeft)}
            </div>
          )}
          <button className="btn btn-primary" onClick={handleSubmitExam} disabled={submitting}>
            {submitting ? 'Submitting...' : 'Submit'}
          </button>
        </div>
      </div>

      <div className="progress-bar">
        <div className="progress-fill" style={{ width: `${progress}%` }} />
      </div>

      {error && <div style={{ padding: '12px 24px' }}><div className="error-banner">{error}</div></div>}

      <div className="exam-content">
        <div className="question-panel">
          {question && (
            <div className="question-card">
              <div className="question-header">
                <span className="question-number">Question {currentQ + 1}</span>
                <div className="question-meta">
                  <span className="question-type">{question.type?.replace(/_/g, ' ')}</span>
                  <span className="question-points">{question.points} pts</span>
                </div>
              </div>
              <p className="question-text">{question.text}</p>
              {question.description && <p className="question-description">{question.description}</p>}
              <div className="answer-section">{renderAnswer(question)}</div>
              <div className="question-navigation">
                <button className="btn btn-secondary" onClick={handlePrev} disabled={currentQ === 0}>← Previous</button>
                {currentQ < questions.length - 1 ? (
                  <button className="btn btn-primary" onClick={handleNext}>Next →</button>
                ) : (
                  <button className="btn btn-primary" onClick={handleSubmitExam} disabled={submitting}>
                    {submitting ? 'Submitting...' : 'Submit exam'}
                  </button>
                )}
              </div>
            </div>
          )}
        </div>

        <div className="question-navigator">
          <h3>Navigator</h3>
          <div className="question-grid">
            {questions.map((_, i) => (
              <button
                key={i}
                className={`question-button ${i === currentQ ? 'active' : ''} ${answers[questions[i]?.id] ? 'answered' : ''}`}
                onClick={() => setCurrentQ(i)}
              >
                {i + 1}
              </button>
            ))}
          </div>
          <div className="navigator-legend">
            <div className="legend-item"><span className="legend-color answered" /><span>Answered</span></div>
            <div className="legend-item"><span className="legend-color unanswered" /><span>Unanswered</span></div>
          </div>
        </div>
      </div>
    </div>
  );
}
