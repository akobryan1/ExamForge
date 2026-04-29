import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { useAuth } from '../contexts/AuthContext';
import type { Exam, Question, ExamAttempt } from '../types/exam';
import '../styles/pages/take-exam.css';

export function TakeExamPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();
  const { user } = useAuth();
  
  // Exam state
  const [exam, setExam] = useState<Exam | null>(null);
  const [questions, setQuestions] = useState<Question[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  
  // Attempt state
  const [attemptId, setAttemptId] = useState<string | null>(null);
  const [currentQuestionIndex, setCurrentQuestionIndex] = useState(0);
  const [answers, setAnswers] = useState<Record<string, any>>({});
  const [timeRemaining, setTimeRemaining] = useState<number | null>(null);
  const [examStarted, setExamStarted] = useState(false);
  const [examSubmitted, setExamSubmitted] = useState(false);
  
  // Access code state
  const [accessCode, setAccessCode] = useState('');
  const [showAccessCodePrompt, setShowAccessCodePrompt] = useState(false);
  
  // Timer
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const questionStartTimeRef = useRef<Date>(new Date());

  useEffect(() => {
    if (examId) {
      loadExam();
    }
  }, [examId]);

  useEffect(() => {
    if (examStarted && timeRemaining !== null && timeRemaining > 0) {
      timerRef.current = setInterval(() => {
        setTimeRemaining((prev) => {
          if (prev === null || prev <= 0) {
            handleAutoSubmit();
            return 0;
          }
          return prev - 1;
        });
      }, 1000);

      return () => {
        if (timerRef.current) clearInterval(timerRef.current);
      };
    }
  }, [examStarted, timeRemaining]);

  // Auto-save answers
  useEffect(() => {
    if (examStarted && !examSubmitted) {
      const autoSaveInterval = setInterval(() => {
        saveCurrentAnswer();
      }, 30000); // Auto-save every 30 seconds

      return () => clearInterval(autoSaveInterval);
    }
  }, [examStarted, examSubmitted, currentQuestionIndex, answers]);

  const loadExam = async () => {
    try {
      setLoading(true);
      const [examData, questionsData] = await Promise.all([
        ExamService.getExamById(examId!),
        ExamService.getExamQuestions(examId!)
      ]);
      
      setExam(examData);
      setQuestions(questionsData);
      
      // Check if exam requires access code
      if (examData.accessCode) {
        setShowAccessCodePrompt(true);
      }
    } catch (err: any) {
      setError(err.message || 'Failed to load exam');
    } finally {
      setLoading(false);
    }
  };

  const handleStartExam = async () => {
    if (!examId) return;
    
    try {
      const attempt = await ExamService.startExam(examId, accessCode || undefined);
      setAttemptId(attempt.id);
      setExamStarted(true);
      setShowAccessCodePrompt(false);
      
      if (exam?.timeLimit) {
        setTimeRemaining(exam.timeLimit * 60); // Convert minutes to seconds
      }
      
      questionStartTimeRef.current = new Date();
    } catch (err: any) {
      alert('Failed to start exam: ' + err.message);
    }
  };

  const saveCurrentAnswer = async () => {
    if (!attemptId || !questions[currentQuestionIndex]) return;
    
    const currentQuestion = questions[currentQuestionIndex];
    const answer = answers[currentQuestion.id];
    
    if (!answer) return;

    try {
      const timeSpent = Math.floor((new Date().getTime() - questionStartTimeRef.current.getTime()) / 1000);
      await ExamService.submitAnswer(attemptId, currentQuestion.id, answer, timeSpent);
    } catch (err: any) {
      console.error('Failed to save answer:', err);
    }
  };

  const handleAnswerChange = (questionId: string, value: any) => {
    setAnswers(prev => ({
      ...prev,
      [questionId]: value
    }));
  };

  const handleNextQuestion = async () => {
    await saveCurrentAnswer();
    
    if (currentQuestionIndex < questions.length - 1) {
      setCurrentQuestionIndex(prev => prev + 1);
      questionStartTimeRef.current = new Date();
    }
  };

  const handlePreviousQuestion = async () => {
    await saveCurrentAnswer();
    
    if (currentQuestionIndex > 0) {
      setCurrentQuestionIndex(prev => prev - 1);
      questionStartTimeRef.current = new Date();
    }
  };

  const handleQuestionNavigate = async (index: number) => {
    await saveCurrentAnswer();
    setCurrentQuestionIndex(index);
    questionStartTimeRef.current = new Date();
  };

  const handleSubmitExam = async () => {
    if (!confirm('Are you sure you want to submit your exam? You cannot change your answers after submission.')) {
      return;
    }

    try {
      // Save current answer first
      await saveCurrentAnswer();
      
      // Submit exam
      await ExamService.submitExam(attemptId!);
      setExamSubmitted(true);
      
      // Clear timer
      if (timerRef.current) clearInterval(timerRef.current);
      
      // Navigate to results
      navigate(`/attempts/${attemptId}/results`);
    } catch (err: any) {
      alert('Failed to submit exam: ' + err.message);
    }
  };

  const handleAutoSubmit = async () => {
    if (examSubmitted || !attemptId) return;
    
    try {
      await saveCurrentAnswer();
      await ExamService.submitExam(attemptId);
      setExamSubmitted(true);
      alert('Time is up! Your exam has been automatically submitted.');
      navigate(`/attempts/${attemptId}/results`);
    } catch (err: any) {
      console.error('Auto-submit failed:', err);
    }
  };

  const formatTime = (seconds: number): string => {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const secs = seconds % 60;
    
    if (hours > 0) {
      return `${hours}:${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
    }
    return `${minutes}:${secs.toString().padStart(2, '0')}`;
  };

  const renderQuestionInput = (question: Question) => {
    const answer = answers[question.id] || '';

    switch (question.type) {
      case 'multiple_choice':
        return (
          <div className="answer-options">
            {question.choices?.map((choice, index) => (
              <label key={index} className="option-label">
                <input
                  type="radio"
                  name={question.id}
                  value={choice}
                  checked={answer === choice}
                  onChange={(e) => handleAnswerChange(question.id, e.target.value)}
                />
                <span className="option-text">
                  {String.fromCharCode(65 + index)}. {choice}
                </span>
              </label>
            ))}
          </div>
        );

      case 'true_false':
        return (
          <div className="answer-options">
            <label className="option-label">
              <input
                type="radio"
                name={question.id}
                value="true"
                checked={answer === 'true'}
                onChange={(e) => handleAnswerChange(question.id, e.target.value)}
              />
              <span className="option-text">True</span>
            </label>
            <label className="option-label">
              <input
                type="radio"
                name={question.id}
                value="false"
                checked={answer === 'false'}
                onChange={(e) => handleAnswerChange(question.id, e.target.value)}
              />
              <span className="option-text">False</span>
            </label>
          </div>
        );

      case 'short_answer':
      case 'fill_in_blank':
        return (
          <input
            type="text"
            className="answer-input"
            value={answer}
            onChange={(e) => handleAnswerChange(question.id, e.target.value)}
            placeholder="Type your answer here..."
          />
        );

      case 'essay':
        return (
          <textarea
            className="answer-textarea"
            value={answer}
            onChange={(e) => handleAnswerChange(question.id, e.target.value)}
            placeholder="Type your essay here..."
            rows={10}
          />
        );

      case 'matching':
        // Simple implementation - can be enhanced
        return (
          <textarea
            className="answer-textarea"
            value={answer}
            onChange={(e) => handleAnswerChange(question.id, e.target.value)}
            placeholder="Enter your matches (e.g., 1-A, 2-B, 3-C)"
            rows={5}
          />
        );

      default:
        return <p>Unsupported question type</p>;
    }
  };

  if (loading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
          <p>Loading exam...</p>
        </div>
      </MainLayout>
    );
  }

  if (error || !exam) {
    return (
      <MainLayout>
        <div style={{ padding: 'var(--spacing-12)' }}>
          <div className="error-banner">{error || 'Exam not found'}</div>
          <Button onClick={() => navigate('/exams')}>Back to Exams</Button>
        </div>
      </MainLayout>
    );
  }

  // Access code prompt
  if (showAccessCodePrompt && !examStarted) {
    return (
      <MainLayout>
        <div className="exam-start-screen">
          <div className="start-card">
            <h1>{exam.title}</h1>
            <p className="exam-description">{exam.description}</p>
            
            <div className="exam-info">
              <div className="info-item">
                <strong>Questions:</strong> {questions.length}
              </div>
              <div className="info-item">
                <strong>Total Points:</strong> {exam.totalPoints || 0}
              </div>
              {exam.timeLimit && (
                <div className="info-item">
                  <strong>Time Limit:</strong> {exam.timeLimit} minutes
                </div>
              )}
              <div className="info-item">
                <strong>Passing Score:</strong> {exam.passingScore}%
              </div>
            </div>

            {exam.accessCode && (
              <div className="form-group">
                <label htmlFor="accessCode">Access Code Required</label>
                <input
                  type="text"
                  id="accessCode"
                  value={accessCode}
                  onChange={(e) => setAccessCode(e.target.value)}
                  placeholder="Enter access code"
                  className="access-code-input"
                />
              </div>
            )}

            <div className="start-actions">
              <Button variant="outline" onClick={() => navigate('/exams')}>
                Cancel
              </Button>
              <Button variant="primary" onClick={handleStartExam}>
                Start Exam
              </Button>
            </div>
          </div>
        </div>
      </MainLayout>
    );
  }

  // Exam taking interface
  if (examStarted && !examSubmitted) {
    const currentQuestion = questions[currentQuestionIndex];
    const progress = ((currentQuestionIndex + 1) / questions.length) * 100;

    return (
      <div className="exam-taking-container">
        {/* Exam Header */}
        <div className="exam-header">
          <div className="exam-header-left">
            <h2>{exam.title}</h2>
            <p className="question-counter">
              Question {currentQuestionIndex + 1} of {questions.length}
            </p>
          </div>
          <div className="exam-header-right">
            {timeRemaining !== null && (
              <div className={`timer ${timeRemaining < 300 ? 'timer-warning' : ''}`}>
                ⏱️ {formatTime(timeRemaining)}
              </div>
            )}
            <Button variant="primary" onClick={handleSubmitExam}>
              Submit Exam
            </Button>
          </div>
        </div>

        {/* Progress Bar */}
        <div className="progress-bar">
          <div className="progress-fill" style={{ width: `${progress}%` }} />
        </div>

        {/* Question Content */}
        <div className="exam-content">
          <div className="question-panel">
            <AnimatePresence mode="wait">
              <motion.div
                key={currentQuestionIndex}
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -20 }}
                transition={{ duration: 0.2 }}
                className="question-card"
              >
                <div className="question-header">
                  <span className="question-number">Question {currentQuestionIndex + 1}</span>
                  <div className="question-meta">
                    <span className="question-type">{currentQuestion.type.replace('_', ' ')}</span>
                    <span className="question-points">{currentQuestion.points} pts</span>
                  </div>
                </div>

                <p className="question-text">{currentQuestion.text}</p>
                {currentQuestion.description && (
                  <p className="question-description">{currentQuestion.description}</p>
                )}

                <div className="answer-section">
                  {renderQuestionInput(currentQuestion)}
                </div>

                <div className="question-navigation">
                  <Button
                    variant="outline"
                    onClick={handlePreviousQuestion}
                    disabled={currentQuestionIndex === 0}
                  >
                    ← Previous
                  </Button>
                  <Button
                    variant="primary"
                    onClick={handleNextQuestion}
                    disabled={currentQuestionIndex === questions.length - 1}
                  >
                    Next →
                  </Button>
                </div>
              </motion.div>
            </AnimatePresence>
          </div>

          {/* Question Navigator */}
          <div className="question-navigator">
            <h3>Question Navigator</h3>
            <div className="question-grid">
              {questions.map((q, index) => (
                <button
                  key={q.id}
                  className={`question-button ${
                    index === currentQuestionIndex ? 'active' : ''
                  } ${answers[q.id] ? 'answered' : ''}`}
                  onClick={() => handleQuestionNavigate(index)}
                >
                  {index + 1}
                </button>
              ))}
            </div>
            <div className="navigator-legend">
              <div className="legend-item">
                <span className="legend-color answered"></span>
                <span>Answered</span>
              </div>
              <div className="legend-item">
                <span className="legend-color unanswered"></span>
                <span>Unanswered</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  // Should not reach here
  return (
    <MainLayout>
      <div style={{ padding: 'var(--spacing-12)' }}>
        <p>Loading...</p>
      </div>
    </MainLayout>
  );
}
