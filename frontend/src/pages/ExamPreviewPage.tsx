import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { pageTransition, stagger, fadeIn } from '../utils/animations';
import type { Exam, Question } from '../types/exam';
import '../styles/pages/exam-preview.css';

export function ExamPreviewPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();
  const [exam, setExam] = useState<Exam | null>(null);
  const [questions, setQuestions] = useState<Question[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (examId) {
      loadExamData();
    }
  }, [examId]);

  const loadExamData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [examData, questionsData] = await Promise.all([
        ExamService.getExamById(examId!),
        ExamService.getExamQuestions(examId!)
      ]);
      setExam(examData);
      setQuestions(questionsData);
    } catch (err: any) {
      setError(err.message || 'Failed to load exam data');
    } finally {
      setLoading(false);
    }
  };

  const handlePublish = async () => {
    if (!examId) return;
    try {
      await ExamService.publishExam(examId);
      alert('Exam published successfully!');
      navigate('/exams');
    } catch (err: any) {
      alert('Failed to publish exam: ' + err.message);
    }
  };

  const handleArchive = async () => {
    if (!examId || !confirm('Are you sure you want to archive this exam?')) return;
    try {
      await ExamService.archiveExam(examId);
      alert('Exam archived successfully!');
      navigate('/exams');
    } catch (err: any) {
      alert('Failed to archive exam: ' + err.message);
    }
  };

  const handleDelete = async () => {
    if (!examId || !confirm('Are you sure you want to delete this exam? This action cannot be undone.')) return;
    try {
      await ExamService.deleteExam(examId);
      alert('Exam deleted successfully!');
      navigate('/exams');
    } catch (err: any) {
      alert('Failed to delete exam: ' + err.message);
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
        <div style={{ paddingTop: 'var(--spacing-12)' }}>
          <div className="error-banner">{error || 'Exam not found'}</div>
          <Button onClick={() => navigate('/exams')}>Back to Exams</Button>
        </div>
      </MainLayout>
    );
  }

  return (
    <MainLayout>
      <motion.div
        className="exam-preview-page"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        {/* Header */}
        <div className="preview-header">
          <Link to="/exams" className="breadcrumb">← Back to Exams</Link>
          <div className="header-content">
            <div>
              <h1>{exam.title}</h1>
              <p className="exam-subtitle">{exam.description}</p>
              <div className="status-row">
                <span className={`status-badge status-${exam.status}`}>
                  {exam.status?.toUpperCase()}
                </span>
                <span className="meta-text">
                  {questions.length} Questions • {exam.totalPoints || 0} Points
                  {exam.timeLimit && ` • ${exam.timeLimit} min`}
                </span>
              </div>
            </div>
            <div className="header-actions">
              <Link to={`/exams/${examId}/questions`}>
                <Button variant="outline">Edit Questions</Button>
              </Link>
              {exam.status === 'draft' && (
                <Button variant="primary" onClick={handlePublish}>
                  Publish Exam
                </Button>
              )}
              {exam.status !== 'archived' && (
                <Button variant="outline" onClick={handleArchive}>
                  Archive
                </Button>
              )}
              <Button variant="outline" onClick={handleDelete} style={{ color: 'var(--danger-color)' }}>
                Delete
              </Button>
            </div>
          </div>
        </div>

        {/* Exam Details */}
        <motion.div className="preview-section" variants={fadeIn}>
          <h2>Exam Details</h2>
          <div className="details-grid">
            {exam.subject && (
              <div className="detail-item">
                <strong>Subject:</strong> {exam.subject}
              </div>
            )}
            {exam.grade && (
              <div className="detail-item">
                <strong>Grade Level:</strong> {exam.grade}
              </div>
            )}
            <div className="detail-item">
              <strong>Passing Score:</strong> {exam.passingScore}%
            </div>
            {exam.timeLimit && (
              <div className="detail-item">
                <strong>Time Limit:</strong> {exam.timeLimit} minutes
              </div>
            )}
            {exam.accessCode && (
              <div className="detail-item">
                <strong>Access Code:</strong> {exam.accessCode}
              </div>
            )}
            {exam.startDate && (
              <div className="detail-item">
                <strong>Start Date:</strong> {new Date(exam.startDate).toLocaleString()}
              </div>
            )}
            {exam.endDate && (
              <div className="detail-item">
                <strong>End Date:</strong> {new Date(exam.endDate).toLocaleString()}
              </div>
            )}
          </div>

          <h3 style={{ marginTop: 'var(--spacing-6)' }}>Exam Options</h3>
          <div className="options-list">
            <div className="option-item">
              <span className={exam.shuffleQuestions ? 'enabled' : 'disabled'}>
                {exam.shuffleQuestions ? '✓' : '✗'}
              </span>
              Shuffle Questions
            </div>
            <div className="option-item">
              <span className={exam.shuffleAnswers ? 'enabled' : 'disabled'}>
                {exam.shuffleAnswers ? '✓' : '✗'}
              </span>
              Shuffle Answer Choices
            </div>
            <div className="option-item">
              <span className={exam.showResults ? 'enabled' : 'disabled'}>
                {exam.showResults ? '✓' : '✗'}
              </span>
              Show Results to Students
            </div>
            <div className="option-item">
              <span className={exam.allowReview ? 'enabled' : 'disabled'}>
                {exam.allowReview ? '✓' : '✗'}
              </span>
              Allow Review After Submission
            </div>
          </div>
        </motion.div>

        {/* Questions Preview */}
        <motion.div className="preview-section" variants={fadeIn}>
          <div className="section-header">
            <h2>Questions ({questions.length})</h2>
            <Link to={`/exams/${examId}/questions`}>
              <Button variant="outline" size="sm">+ Add Questions</Button>
            </Link>
          </div>

          {questions.length === 0 ? (
            <div className="empty-state">
              <p>No questions added yet</p>
              <Link to={`/exams/${examId}/questions`}>
                <Button variant="primary">Add Questions</Button>
              </Link>
            </div>
          ) : (
            <div className="questions-preview-list">
              {questions.map((question, index) => (
                <div key={question.id} className="question-preview-card">
                  <div className="question-preview-header">
                    <span className="question-number">#{index + 1}</span>
                    <div className="question-badges">
                      <span className={`type-badge type-${question.type}`}>
                        {question.type.replace('_', ' ')}
                      </span>
                      <span className={`difficulty-badge difficulty-${question.difficulty}`}>
                        {question.difficulty}
                      </span>
                      <span className="points-badge">{question.points} pts</span>
                    </div>
                  </div>
                  <p className="question-text">{question.text}</p>
                  {question.type === 'multiple_choice' && question.choices && (
                    <div className="choices-preview">
                      {question.choices.map((choice, i) => (
                        <div
                          key={i}
                          className={`choice-preview ${choice === question.correctAnswer ? 'correct' : ''}`}
                        >
                          {String.fromCharCode(65 + i)}. {choice}
                          {choice === question.correctAnswer && <span className="correct-mark">✓</span>}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </motion.div>

        {/* Statistics (if exam has attempts) */}
        {exam.attemptCount && exam.attemptCount > 0 && (
          <motion.div className="preview-section" variants={fadeIn}>
            <h2>Statistics</h2>
            <div className="stats-grid">
              <div className="stat-item">
                <div className="stat-value">{exam.attemptCount}</div>
                <div className="stat-label">Total Attempts</div>
              </div>
              {exam.averageScore !== undefined && (
                <div className="stat-item">
                  <div className="stat-value">{exam.averageScore.toFixed(1)}%</div>
                  <div className="stat-label">Average Score</div>
                </div>
              )}
            </div>
          </motion.div>
        )}
      </motion.div>
    </MainLayout>
  );
}
