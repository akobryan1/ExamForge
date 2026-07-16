import { useState, useEffect } from 'react';
import { useParams, useSearchParams, useNavigate, Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { useAuth } from '../contexts/AuthContext';
import { pageTransition, fadeIn } from '../utils/animations';
import type { ExamAttempt, Exam, Question } from '../types/exam';
import '../styles/pages/exam-results.css';

export function ExamResultsPage() {
  const { attemptId } = useParams<{ attemptId: string }>();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { user } = useAuth();
  const isInstructor = user?.role === 'instructor' || user?.role === 'admin';
  const viewingStudentId = searchParams.get('studentId');
  
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
        <div className="center-state">
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
        {/* Paper Hero — Returned graded paper */}
        <div className="paper-hero">
          <p className="eyebrow">{exam.title} — {isInstructor ? `Reviewing ${attempt.studentName || 'Student'}'s paper` : 'Graded Paper'}</p>
          <div className="grade-circle-big">{percentage.toFixed(1)}%</div>
          <div className="score-details">{score} out of {totalPoints} points</div>
          <div className={`verdict-stamp ${passed ? 'passed' : 'failed'}`}>
            {passed ? '✓ Passed' : 'Keep Practicing'}
          </div>
          <p className="results-message">
            {isInstructor
              ? `Reviewing ${attempt.studentName || 'student'}'s submission for "${exam.title}"`
              : (passed
                ? 'You have successfully passed the exam!'
                : 'Keep practicing! You can do better next time.')}
          </p>
        </div>

        {/* Breakdown Card */}
        <div className="breakdown-card">
          <h3>Score Breakdown</h3>
          <div className="breakdown-grid">
            <div className="breakdown-item">
              <span className="breakdown-label">Exam</span>
              <span>{exam.title}</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Student</span>
              <span>{attempt.studentName || 'Unknown'}</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Submitted</span>
              <span>{attempt.submittedAt ? new Date(attempt.submittedAt).toLocaleString() : 'N/A'}</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Time Taken</span>
              <span>{attempt.timeSpent ? `${Math.floor(attempt.timeSpent / 60)} minutes` : 'N/A'}</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Passing Score</span>
              <span>{exam.passingScore}%</span>
            </div>
            <div className="breakdown-item">
              <span className="breakdown-label">Status</span>
              <span className={`stamp stamp-${passed ? 'pass' : 'fail'}`}>{passed ? 'Passed' : 'Not passed'}</span>
            </div>
          </div>
        </div>

        {/* Question Review (if allowed) */}
        {exam.allowReview && attempt.answers && questions.length > 0 && (
          <motion.div className="review-section" variants={fadeIn}>
            <h2>Question Review</h2>
            <div className="questions-review">
              {questions.map((question, index) => {
                const studentAnswer: any = Array.isArray(attempt.answers)
                  ? attempt.answers.find((a: any) => a.questionId === question.id)
                  : Object.values(attempt.answers || {}).find((a: any) => a.questionId === question.id);
                const isCorrect = studentAnswer?.isCorrect;
                const earnedPoints = studentAnswer?.pointsEarned ?? null;
                const notAnswered = !studentAnswer;
                const isUngraded = studentAnswer && earnedPoints === null && studentAnswer?.gradedBy === null;
                const resultLabel = notAnswered ? '❌ Not Answered' : (isUngraded ? '⏳ Pending' : (isCorrect ? '✓ Correct' : '✗ Incorrect'));
                const resultClass = notAnswered ? 'incorrect' : (isUngraded ? 'pending' : (isCorrect ? 'correct' : 'incorrect'));

                return (
                  <div key={question.id} className="review-question-card">
                    <div className="review-question-header">
                      <span className="review-question-number">Question {index + 1}</span>
                      <div className="review-question-meta">
                        <span className={`stamp review-result ${resultClass}`}>
                          {resultLabel}
                        </span>
                        <span className="review-points">
                          {isUngraded ? 'Pending' : `${earnedPoints ?? 0}/${question.points} pts`}
                        </span>
                      </div>
                    </div>

                    <p className="review-question-text">{question.text}</p>

                    <div className="review-answer-section">
                      <div className="review-answer">
                        <strong>Your Answer:</strong>
                        <span>{studentAnswer?.answer || 'Not answered'}</span>
                      </div>
                      {!isCorrect && !isUngraded && question.correctAnswer && (
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
