import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { apiClient } from '../services/apiClient';
import { pageTransition } from '../utils/animations';
import '../styles/pages/examinee-review.css';

export function ExamineeReviewPage() {
  const { attemptId } = useParams<{ attemptId: string }>();
  const navigate = useNavigate();
  const [attempt, setAttempt] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!attemptId) return;
    const token = localStorage.getItem('accessToken');
    fetch(`${import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'}/api/exams/attempts/${attemptId}`, {
      headers: { 'Authorization': token ? `Bearer ${token}` : '' }
    })
      .then(r => { if (!r.ok) throw new Error('Failed to load attempt'); return r.json(); })
      .then(data => setAttempt(data))
      .catch(err => setError(err.message))
      .finally(() => setLoading(false));
  }, [attemptId]);

  if (loading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)', color: 'var(--ledger-ink-soft)' }}>
          <p>Loading examinee paper...</p>
        </div>
      </MainLayout>
    );
  }

  if (error || !attempt) {
    return (
      <MainLayout>
        <div className="page-header">
          <Button variant="outline" onClick={() => navigate('/students')}>← Back to Students</Button>
        </div>
        <div className="empty-state">
          <h3>Attempt not found</h3>
          <p>{error || 'The requested exam attempt could not be loaded.'}</p>
        </div>
      </MainLayout>
    );
  }

  const passed = attempt.passed || (attempt.percentage || 0) >= 60;

  return (
    <MainLayout>
      <motion.div className="examinee-review-page" variants={pageTransition} initial="initial" animate="animate" exit="exit">
        <div className="page-header">
          <div>
            <Button variant="outline" size="sm" onClick={() => navigate('/students')}>← Back to Students</Button>
            <h1>Examinee Paper Review</h1>
          </div>
        </div>

        {/* Student & Exam Info */}
        <div className="review-summary">
          <div className="summary-card">
            <div className="summary-grid">
              <div>
                <strong>Student</strong>
                <span>{attempt.studentName || 'Unknown'}</span>
              </div>
              <div>
                <strong>Exam</strong>
                <span>{attempt.examId || '—'}</span>
              </div>
              <div>
                <strong>Status</strong>
                <span className={`stamp ${attempt.status === 'graded' ? 'stamp-graded' : 'stamp-pending'}`}>
                  {attempt.status}
                </span>
              </div>
              <div>
                <strong>Submitted</strong>
                <span>{attempt.submittedAt ? new Date(attempt.submittedAt).toLocaleString() : '—'}</span>
              </div>
              <div>
                <strong>Score</strong>
                <span className={passed ? 'score-pass' : 'score-fail'}>
                  {attempt.score ?? '—'} / {attempt.totalPoints || '—'}
                </span>
              </div>
              <div>
                <strong>Percentage</strong>
                <span className={passed ? 'score-pass' : 'score-fail'}>
                  {attempt.percentage?.toFixed(1) ?? '—'}%
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Per-Question Breakdown */}
        <div className="review-answers">
          <h2>Answer Breakdown</h2>
          {(!attempt.answers || attempt.answers.length === 0) ? (
            <div className="empty-state">
              <p>No answers recorded for this attempt.</p>
            </div>
          ) : (
            <div className="answers-list">
              {attempt.answers.map((answer: any, index: number) => {
                const isAutoGraded = answer.pointsEarned !== null;
                const isCorrect = answer.isCorrect;
                const isEssayType = answer.questionType === 'essay' || answer.questionType === 'short_answer';

                return (
                  <motion.div
                    key={answer.id || index}
                    className={`answer-card ${isAutoGraded ? (isCorrect ? 'card-correct' : 'card-incorrect') : 'card-pending'}`}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <div className="answer-header">
                      <span className="question-num">Question {index + 1}</span>
                      <span className="stamp stamp-type">{answer.questionType?.replace('_', ' ') || 'unknown'}</span>
                      <span className="points-badge">
                        {isAutoGraded ? `${answer.pointsEarned} / ${answer.maxPoints}` : 'Pending'}
                      </span>
                    </div>

                    <div className="answer-body">
                      <div className="q-text">
                        <strong>Question:</strong> {answer.questionText || '—'}
                      </div>

                      <div className="a-text">
                        <strong>Student Answer:</strong>
                        <div className="answer-content-box">{answer.answer || '—'}</div>
                      </div>

                      {answer.correctAnswer !== null && answer.correctAnswer !== undefined && (
                        <div className="correct-text">
                          <strong>Correct Answer:</strong>
                          <div className="correct-content-box">
                            {Array.isArray(answer.correctAnswer)
                              ? answer.correctAnswer.join(', ')
                              : String(answer.correctAnswer)}
                          </div>
                        </div>
                      )}

                      {isAutoGraded && (
                        <div className={`verdict ${isCorrect ? 'verdict-pass' : 'verdict-fail'}`}>
                          {isCorrect ? '✓ Correct' : '✗ Incorrect'}
                        </div>
                      )}

                      {isEssayType && !isAutoGraded && (
                        <div className="verdict verdict-pending">
                          ⏳ Awaiting manual grading
                        </div>
                      )}

                      {answer.feedback && (
                        <div className="feedback-box">
                          <strong>Feedback:</strong> {answer.feedback}
                        </div>
                      )}
                    </div>
                  </motion.div>
                );
              })}
            </div>
          )}
        </div>
      </motion.div>
    </MainLayout>
  );
}
