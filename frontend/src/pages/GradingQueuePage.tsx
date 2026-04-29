import { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { pageTransition, fadeIn } from '../utils/animations';
import { useAuth } from '../contexts/AuthContext';
import type { ExamAttempt, Question } from '../types/exam';
import '../styles/pages/grading-queue.css';

interface GradingItem {
  attemptId: string;
  studentName: string;
  studentEmail: string;
  examTitle: string;
  examId: string;
  question: Question;
  answer: string;
  submittedAt: Date;
  currentGrade?: number;
  feedback?: string;
}

export function GradingQueuePage() {
  const { user } = useAuth();
  const [items, setItems] = useState<GradingItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<'pending' | 'graded' | 'all'>('pending');
  const [selectedItem, setSelectedItem] = useState<GradingItem | null>(null);
  const [gradeValue, setGradeValue] = useState('');
  const [feedbackText, setFeedbackText] = useState('');
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    loadGradingQueue();
  }, []);

  const loadGradingQueue = async () => {
    try {
      setLoading(true);
      // TODO: Implement API call to fetch grading queue
      // For now, using mock data
      const mockItems: GradingItem[] = [];
      setItems(mockItems);
    } catch (error) {
      console.error('Failed to load grading queue:', error);
    } finally {
      setLoading(false);
    }
  };

  const handleGradeSubmit = async () => {
    if (!selectedItem || !gradeValue) return;

    try {
      setSubmitting(true);
      // TODO: Implement API call to submit grade
      // await ExamService.gradeQuestion(attemptId, questionId, grade, feedback);
      
      alert('Grade submitted successfully!');
      setSelectedItem(null);
      setGradeValue('');
      setFeedbackText('');
      loadGradingQueue();
    } catch (error: any) {
      alert('Failed to submit grade: ' + error.message);
    } finally {
      setSubmitting(false);
    }
  };

  const filteredItems = items.filter(item => {
    if (filter === 'pending') return !item.currentGrade;
    if (filter === 'graded') return item.currentGrade !== undefined;
    return true;
  });

  return (
    <MainLayout>
      <motion.div
        className="grading-queue-page"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        <div className="page-header">
          <div>
            <h1>Grading Queue</h1>
            <p className="page-subtitle">Review and grade student essay submissions</p>
          </div>
          <div className="header-actions">
            <select
              value={filter}
              onChange={(e) => setFilter(e.target.value as 'pending' | 'graded' | 'all')}
              className="filter-select"
            >
              <option value="pending">Pending</option>
              <option value="graded">Graded</option>
              <option value="all">All</option>
            </select>
          </div>
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
            <p>Loading grading queue...</p>
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="empty-state">
            <h3>No submissions to grade</h3>
            <p>
              {filter === 'pending' 
                ? 'All essay submissions have been graded!'
                : 'No submissions found.'}
            </p>
          </div>
        ) : (
          <div className="grading-content">
            {/* Queue List */}
            <div className="queue-list">
              {filteredItems.map((item) => (
                <motion.div
                  key={`${item.attemptId}-${item.question.id}`}
                  className={`queue-item ${selectedItem === item ? 'active' : ''}`}
                  onClick={() => {
                    setSelectedItem(item);
                    setGradeValue(item.currentGrade?.toString() || '');
                    setFeedbackText(item.feedback || '');
                  }}
                  variants={fadeIn}
                  whileHover={{ scale: 1.01 }}
                >
                  <div className="queue-item-header">
                    <span className="student-name">{item.studentName}</span>
                    {item.currentGrade !== undefined ? (
                      <span className="graded-badge">✓ Graded</span>
                    ) : (
                      <span className="pending-badge">Pending</span>
                    )}
                  </div>
                  <p className="exam-title">{item.examTitle}</p>
                  <p className="question-preview">
                    {item.question.text.substring(0, 100)}
                    {item.question.text.length > 100 ? '...' : ''}
                  </p>
                  <div className="queue-item-footer">
                    <span className="submitted-time">
                      {new Date(item.submittedAt).toLocaleDateString()}
                    </span>
                    <span className="points-info">{item.question.points} pts</span>
                  </div>
                </motion.div>
              ))}
            </div>

            {/* Grading Panel */}
            <AnimatePresence mode="wait">
              {selectedItem && (
                <motion.div
                  className="grading-panel"
                  initial={{ opacity: 0, x: 20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ duration: 0.2 }}
                >
                  <div className="panel-header">
                    <h2>Grade Submission</h2>
                    <button
                      className="close-btn"
                      onClick={() => setSelectedItem(null)}
                    >
                      ×
                    </button>
                  </div>

                  <div className="panel-content">
                    {/* Student Info */}
                    <div className="info-section">
                      <h3>Student Information</h3>
                      <div className="info-grid">
                        <div>
                          <strong>Name:</strong> {selectedItem.studentName}
                        </div>
                        <div>
                          <strong>Email:</strong> {selectedItem.studentEmail}
                        </div>
                        <div>
                          <strong>Exam:</strong> {selectedItem.examTitle}
                        </div>
                        <div>
                          <strong>Submitted:</strong> {new Date(selectedItem.submittedAt).toLocaleString()}
                        </div>
                      </div>
                    </div>

                    {/* Question */}
                    <div className="question-section">
                      <h3>Question</h3>
                      <p className="question-text">{selectedItem.question.text}</p>
                      {selectedItem.question.description && (
                        <p className="question-description">{selectedItem.question.description}</p>
                      )}
                      <div className="question-meta">
                        <span className="points-badge">{selectedItem.question.points} points</span>
                      </div>
                    </div>

                    {/* Student Answer */}
                    <div className="answer-section">
                      <h3>Student Answer</h3>
                      <div className="answer-content">
                        {selectedItem.answer}
                      </div>
                    </div>

                    {/* Grading Form */}
                    <div className="grading-form">
                      <div className="form-group">
                        <label htmlFor="grade">Grade (out of {selectedItem.question.points})</label>
                        <input
                          type="number"
                          id="grade"
                          min="0"
                          max={selectedItem.question.points}
                          step="0.5"
                          value={gradeValue}
                          onChange={(e) => setGradeValue(e.target.value)}
                          placeholder="Enter points"
                        />
                      </div>

                      <div className="form-group">
                        <label htmlFor="feedback">Feedback (Optional)</label>
                        <textarea
                          id="feedback"
                          value={feedbackText}
                          onChange={(e) => setFeedbackText(e.target.value)}
                          placeholder="Provide feedback to the student..."
                          rows={6}
                        />
                      </div>

                      <div className="form-actions">
                        <Button
                          variant="outline"
                          onClick={() => setSelectedItem(null)}
                          disabled={submitting}
                        >
                          Cancel
                        </Button>
                        <Button
                          variant="primary"
                          onClick={handleGradeSubmit}
                          disabled={submitting || !gradeValue}
                        >
                          {submitting ? 'Submitting...' : 'Submit Grade'}
                        </Button>
                      </div>
                    </div>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )}
      </motion.div>
    </MainLayout>
  );
}
