import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { pageTransition, fadeIn } from '../utils/animations';
import type { ExamAttempt, Exam, Question } from '../types/exam';
import '../styles/pages/exam-results.css';

export function ExamResultsPage() {
  const { attemptId } = useParams<{ attemptId: string }>();
  const navigate = useNavigate();
  
  const [attempt, setAttempt] = useState<ExamAttempt | null>(null);
  const [exam, setExam] = useState<Exam | null>(null);
  const [questions, setQuestions] = useState<Question[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (attemptId) {
      loadResults();
    }
  }, [attemptId]);

  const loadResults = async () => {
    try {
      setLoading(true);
      const attemptData = await ExamService.getExamAttempt(attemptId!);
      setAttempt(attemptData);
      
      if (attemptData.examId) {
        const [examData, questionsData] = await Promise.all([
          ExamService.getExamById(attemptData.examId),
          ExamService.getExamQuestions(attemptData.examId)
        ]);
        setExam(examData);
        setQuestions(questionsData);
      }
    } catch (err: any) {
      setError(err.message || 'Failed to load results');
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
          <p>Loading results...</p>
        </div>
      </MainLayout>
    );
  }

  if (error || !attempt || !exam) {
    return (
      <MainLayout>
        <div style={{ padding: 'var(--spacing-12)' }}>
          <div className="error-banner">{error || 'Results not found'}</div>
          <Button onClick={() => navigate('/exams')}>Back to Exams</Button>
        </div>
      </MainLayout>
    );
  }

  const score = attempt.score || 0;
  const totalPoints = exam.totalPoints || 100;
  const percentage = (score / totalPoints) * 100;
  const passed = percentage >= (exam.passingScore || 60);

  return (
    <MainLayout>
      <motion.div
        className="exam-results-page"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        {/* Results Header */}
        <div className={`results-header ${passed ? 'passed' : 'failed'}`}>
          <div className="results-icon">
            {passed ? '🎉' : '📝'}
          </div>
          <h1>{passed ? 'Congratulations!' : 'Exam Completed'}</h1>
          <p className="results-message">
            {passed 
              ? 'You have successfully passed the exam!' 
              : 'Keep practicing! You can do better next time.'}
          </p>
        </div>

        {/* Score Card */}
        <motion.div className="score-card" variants={fadeIn}>
          <div className="score-main">
            <div className="score-value">{percentage.toFixed(1)}%</div>
            <div className="score-details">
              {score} out of {totalPoints} points
            </div>
          </div>
          
          <div className="score-breakdown">
            <div className="breakdown-item">
              <span className="breakdown-label">Exam:</span>
              <span className="breakdown-value">{exam.title}</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Submitted:</span>
              <span className="breakdown-value">
                {attempt.submittedAt ? new Date(attempt.submittedAt).toLocaleString() : 'N/A'}
              </span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Time Taken:</span>
              <span className="breakdown-value">
                {attempt.timeSpent ? `${Math.floor(attempt.timeSpent / 60)} minutes` : 'N/A'}
              </span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Passing Score:</span>
              <span className="breakdown-value">{exam.passingScore}%</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Status:</span>
              <span className={`status-badge ${passed ? 'passed' : 'failed'}`}>
                {passed ? 'PASSED' : 'FAILED'}
              </span>
            </div>
          </div>
        </motion.div>

        {/* Question Review (if allowed) */}
        {exam.allowReview && attempt.answers && questions.length > 0 && (
          <motion.div className="review-section" variants={fadeIn}>
            <h2>Question Review</h2>
            <div className="questions-review">
              {questions.map((question, index) => {
                const studentAnswer = attempt.answers?.[question.id];
                const isCorrect = studentAnswer?.isCorrect;
                const earnedPoints = studentAnswer?.earnedPoints || 0;

                return (
                  <div key={question.id} className="review-question-card">
                    <div className="review-question-header">
                      <span className="review-question-number">Question {index + 1}</span>
                      <div className="review-question-meta">
                        <span className={`review-result ${isCorrect ? 'correct' : 'incorrect'}`}>
                          {isCorrect ? '✓ Correct' : '✗ Incorrect'}
                        </span>
                        <span className="review-points">
                          {earnedPoints} / {question.points} pts
                        </span>
                      </div>
                    </div>

                    <p className="review-question-text">{question.text}</p>

                    <div className="review-answer-section">
                      <div className="review-answer">
                        <strong>Your Answer:</strong>
                        <span>{studentAnswer?.answer || 'Not answered'}</span>
                      </div>
                      {!isCorrect && question.correctAnswer && (
                        <div className="review-correct-answer">
                          <strong>Correct Answer:</strong>
                          <span>{question.correctAnswer}</span>
                        </div>
                      )}
                    </div>

                    {studentAnswer?.feedback && (
                      <div className="review-feedback">
                        <strong>Feedback:</strong>
                        <p>{studentAnswer.feedback}</p>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </motion.div>
        )}

        {/* Actions */}
        <div className="results-actions">
          <Link to="/exams">
            <Button variant="primary">Back to Exams</Button>
          </Link>
          {!passed && exam.status === 'active' && (
            <Link to={`/exams/${exam.id}/take`}>
              <Button variant="outline">Retake Exam</Button>
            </Link>
          )}
        </div>
      </motion.div>
    </MainLayout>
  );
}
