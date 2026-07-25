import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { pageTransition, fadeIn } from '../utils/animations';
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
    
    // Restriction: no questions
    if (questions.length === 0) {
      setError('Cannot publish — this exam has no questions. Add at least one question first.');
      return;
    }
    
    // Warning: no schedule
    const hasSchedule = exam?.startDate || exam?.endDate;
    if (!hasSchedule && !confirm('This exam has no schedule set. Students can take it at any time. Publish anyway?')) {
      return;
    }
    
    // Warning: no time limit
    if (!exam?.timeLimit && !confirm('This exam has no time limit. Publish anyway?')) {
      return;
    }
    
    try {
      await ExamService.publishExam(examId);
      setError(null);
      navigate('/exams');
    } catch (err: any) {
      setError(err.response?.data?.error || err.message || 'Failed to publish exam');
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

  const handleClone = async () => {
    if (!examId) return;
    try {
      const cloned = await ExamService.cloneExam(examId);
      navigate(`/exams/${cloned.id}/edit`);
    } catch (err: any) {
      alert('Failed to clone exam: ' + err.message);
    }
  };

  if (loading) {
    return (
      <MainLayout>
        <div className="center-state">
          <p>Loading exam...</p>
        </div>
      </MainLayout>
    );
  }

  if (!exam) {
    return (
      <MainLayout>
        <div className="center-state">
          <p>Exam not found.</p>
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
        {/* Error Banner */}
        {error && (
          <div className="error-banner">
            ⚠️ {error}
          </div>
        )}
        
        {/* Warnings for draft exams */}
        {exam.status === 'draft' && (
          <div style={{ marginBottom: 'var(--spacing-4)' }}>
            {questions.length === 0 && (
              <div className="warning-banner">
                ⚠️ <strong>No questions added.</strong> You need at least one question before publishing.
              </div>
            )}
            {!exam.startDate && !exam.endDate && (
              <div className="warning-banner">
                📅 <strong>No schedule set.</strong> Without dates, this exam will be available immediately upon publish.
              </div>
            )}
            {!exam.timeLimit && (
              <div className="warning-banner">
                <img src="/icons/status/avg-time.png" alt="" className="inline-icon" /> <strong>No time limit set.</strong> Students will have unlimited time to complete this exam.
              </div>
            )}
          </div>
        )}
        
        {/* Header */}
        <div className="preview-header">
          <Link to="/exams" className="breadcrumb">← Back to Exams</Link>
          <div className="header-content">
            <div>
              <h1>{exam.title}</h1>
              <p className="exam-subtitle">{exam.description}</p>
              <div className="status-row">
                <span className={`stamp stamp-${exam.status === 'published' ? 'scheduled' : exam.status === 'active' ? 'active' : exam.status}`}>
                  {exam.status?.toUpperCase()}
                </span>
                <span className="meta-text">
                  {questions.length} Questions • {exam.totalPoints || 0} Points
                  {exam.timeLimit && ` • ${exam.timeLimit} min`}
                </span>
              </div>
            </div>
            <div className="header-actions">
              <Link to={`/exams/${examId}/edit`}>
                <Button variant="outline">Edit Exam</Button>
              </Link>
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
              <Button variant="outline" onClick={handleClone}>
                <img src="/icons/tabs/clone.png" alt="" className="inline-icon" /> Clone
              </Button>
              <Button variant="outline" onClick={handleDelete} className="danger">
                Delete
              </Button>
            </div>
          </div>
        </div>

        {/* Student Exam Link — shown when published or active */}
        {(exam.status === 'published' || exam.status === 'active') && (
          <div className="link-callout">
            <h3>📎 Student Exam Link</h3>
            <p>Share this link with your students so they can take the exam:</p>
            <div className="link-row">
              <input
                type="text"
                readOnly
                value={`${import.meta.env.VITE_EXAM_PORTAL_URL || window.location.origin}/exams/${examId}/take`}
                className="link-input"
                onClick={(e) => (e.target as HTMLInputElement).select()}
              />
              <Button
                variant="primary"
                size="sm"
                onClick={() => {
                  navigator.clipboard.writeText(`${import.meta.env.VITE_EXAM_PORTAL_URL || window.location.origin}/exams/${examId}/take`);
                  alert('Link copied to clipboard!');
                }}
              >
                📋 Copy
              </Button>
            </div>
            {exam.accessCode && (
              <p className="access-code-note">
                🔑 Access code required: <strong>{exam.accessCode}</strong>
              </p>
            )}
          </div>
        )}

        {/* Exam Details */}
        <motion.div className="preview-section" variants={fadeIn}>
          <h2>Exam Details</h2>
          <div className="details-grid">
            {exam.subject && (
              <div className="detail-item">
              <strong>Subject</strong>{exam.subject}
            </div>
            )}
            {exam.grade && (
              <div className="detail-item">
              <strong>Grade Level</strong>{exam.grade}
            </div>
            )}
            <div className="detail-item">
              <strong>Passing Score</strong>{exam.passingScore}%
            </div>
            {exam.timeLimit && (
              <div className="detail-item">
                <strong>Time Limit</strong>{exam.timeLimit} minutes
              </div>
            )}
            {exam.accessCode && (
              <div className="detail-item">
                <strong>Access Code</strong>{exam.accessCode}
              </div>
            )}
            {exam.startDate && (
              <div className="detail-item">
                <strong>Start Date</strong>{new Date(exam.startDate).toLocaleString()}
              </div>
            )}
            {exam.endDate && (
              <div className="detail-item">
                <strong>End Date</strong>{new Date(exam.endDate).toLocaleString()}
              </div>
            )}
          </div>

          <h3>Exam Options</h3>
          <div className="options-list">
            <div className="option-item">
              <span className={`opt-mark ${exam.shuffleQuestions ? 'enabled' : 'disabled'}`}>
                {exam.shuffleQuestions ? <img src="/icons/status/correct.png" alt="" className="opt-mark-img" /> : <img src="/icons/status/incorrect.png" alt="" className="opt-mark-img" />}
              </span>
              Shuffle Questions
            </div>
            <div className="option-item">
              <span className={`opt-mark ${exam.shuffleAnswers ? 'enabled' : 'disabled'}`}>
                {exam.shuffleAnswers ? <img src="/icons/status/correct.png" alt="" className="opt-mark-img" /> : <img src="/icons/status/incorrect.png" alt="" className="opt-mark-img" />}
              </span>
              Shuffle Answer Choices
            </div>
            <div className="option-item">
              <span className={`opt-mark ${exam.showResults ? 'enabled' : 'disabled'}`}>
                {exam.showResults ? <img src="/icons/status/correct.png" alt="" className="opt-mark-img" /> : <img src="/icons/status/incorrect.png" alt="" className="opt-mark-img" />}
              </span>
              Show Results to Students
            </div>
            <div className="option-item">
              <span className={`opt-mark ${exam.allowReview ? 'enabled' : 'disabled'}`}>
                {exam.allowReview ? <img src="/icons/status/correct.png" alt="" className="opt-mark-img" /> : <img src="/icons/status/incorrect.png" alt="" className="opt-mark-img" />}
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
              <div className="empty-stamp">Nothing filed</div>
              <p>No questions added yet</p>
              <Link to={`/exams/${examId}/questions`}>
                <Button variant="primary">Add Questions</Button>
              </Link>
            </div>
          ) : (
            <div className="questions-preview-list">
              {questions.map((question, index) => (
                <div key={question.id} className="question-preview-card">
                  <div className="points-seal">{question.points}<span>pt</span></div>
                  <div className="question-preview-header">
                    <span className="question-number">#{index + 1}</span>
                    <div className="question-badges">
                      <span className="stamp stamp-type">
                        {question.type.replace('_', ' ')}
                      </span>
                      <span className={`stamp stamp-${question.difficulty}`}>
                        {question.difficulty}
                      </span>
                    </div>
                  </div>
                  <p className="question-text">{question.text}</p>
                  {question.type === 'multiple_choice' && question.choices && (
                    <div className="choices-preview">
                      {question.choices.map((choice, i) => {
                        const choiceText = typeof choice === 'object' ? choice.text : choice;
                        const isCorrect = typeof choice === 'object'
                          ? choice.isCorrect
                          : choice === question.correctAnswer;
                        return (
                          <div key={i} className={`choice-preview ${isCorrect ? 'correct' : ''}`}>
                            <span className="choice-bubble">{String.fromCharCode(65 + i)}</span>
                            <span className="choice-text">{choiceText}</span>
                          </div>
                        );
                      })}
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
