import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../contexts/AuthContext';
import { ExamService } from '../services/ExamService';
import type { Exam, ExamStatus } from '../types/exam';
import '../styles/pages/exams.css';

export function ExamsPage() {
  const { user } = useAuth();
  const [exams, setExams] = useState<Exam[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<ExamStatus | 'all'>('all');

  const isInstructor = user?.role === 'instructor' || user?.role === 'admin';

  useEffect(() => {
    loadExams();
  }, []);

  async function loadExams() {
    try {
      setLoading(true);
      setError(null);
      const data = await ExamService.getExams();
      setExams(data);
    } catch (err: any) {
      setError(err.message || 'Failed to load exams');
    } finally {
      setLoading(false);
    }
  }

  const filteredExams = statusFilter === 'all'
    ? exams
    : exams.filter(exam => exam.status === statusFilter);

  const getStatusBadge = (status: ExamStatus) => {
    const badges = {
      draft: { label: 'Draft', className: 'status-draft' },
      published: { label: 'Published', className: 'status-published' },
      active: { label: 'Active', className: 'status-active' },
      completed: { label: 'Completed', className: 'status-completed' },
      archived: { label: 'Archived', className: 'status-archived' },
    };
    const badge = badges[status];
    return <span className={`status-badge ${badge.className}`}>{badge.label}</span>;
  };

  if (loading) {
    return (
      <div className="page-container">
        <div className="loading-spinner">Loading exams...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="page-container">
        <div className="error-message">
          <h2>Error Loading Exams</h2>
          <p>{error}</p>
          <button onClick={loadExams}>Try Again</button>
        </div>
      </div>
    );
  }

  return (
    <div className="page-container exams-page">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
      >
        <div className="page-header">
          <div>
            <h1>{isInstructor ? 'My Exams' : 'Available Exams'}</h1>
            <p className="page-subtitle">
              {isInstructor
                ? 'Create and manage your exams'
                : 'Browse and take available exams'}
            </p>
          </div>
          
          {isInstructor && (
            <Link to="/exams/create" className="btn btn-primary">
              + Create Exam
            </Link>
          )}
        </div>

        {isInstructor && (
          <div className="filter-bar">
            <div className="filter-group">
              <label>Filter by status:</label>
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as ExamStatus | 'all')}
              >
                <option value="all">All Exams</option>
                <option value="draft">Draft</option>
                <option value="published">Published</option>
                <option value="active">Active</option>
                <option value="completed">Completed</option>
                <option value="archived">Archived</option>
              </select>
            </div>
          </div>
        )}

        {filteredExams.length === 0 ? (
          <div className="empty-state">
            <h2>No Exams Found</h2>
            <p>
              {isInstructor
                ? 'Create your first exam to get started'
                : 'No exams are currently available'}
            </p>
            {isInstructor && (
              <Link to="/exams/create" className="btn btn-primary">
                Create Exam
              </Link>
            )}
          </div>
        ) : (
          <div className="exams-grid">
            {filteredExams.map((exam, index) => (
              <motion.div
                key={exam.id}
                className="exam-card"
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.3, delay: index * 0.05 }}
              >
                <div className="exam-card-header">
                  <h3>{exam.title}</h3>
                  {getStatusBadge(exam.status)}
                </div>
                
                <p className="exam-description">{exam.description}</p>

                <div className="exam-meta">
                  {exam.subject && (
                    <span className="meta-item">
                      <strong>Subject:</strong> {exam.subject}
                    </span>
                  )}
                  {exam.grade && (
                    <span className="meta-item">
                      <strong>Grade:</strong> {exam.grade}
                    </span>
                  )}
                  <span className="meta-item">
                    <strong>Questions:</strong> {exam.questionCount}
                  </span>
                  <span className="meta-item">
                    <strong>Points:</strong> {exam.totalPoints}
                  </span>
                  {exam.timeLimit && (
                    <span className="meta-item">
                      <strong>Time:</strong> {exam.timeLimit} min
                    </span>
                  )}
                </div>

                {isInstructor && (
                  <div className="exam-stats">
                    <span>Attempts: {exam.attemptCount}</span>
                    {exam.averageScore && (
                      <span>Avg Score: {exam.averageScore.toFixed(1)}%</span>
                    )}
                  </div>
                )}

                <div className="exam-card-actions">
                  {isInstructor ? (
                    <>
                      <Link to={`/exams/${exam.id}`} className="btn btn-secondary">
                        Manage
                      </Link>
                      <Link to={`/exams/${exam.id}/questions`} className="btn btn-outline">
                        Questions
                      </Link>
                    </>
                  ) : (
                    <>
                      <Link to={`/exams/${exam.id}`} className="btn btn-secondary">
                        View Details
                      </Link>
                      <Link to={`/exams/${exam.id}/take`} className="btn btn-primary">
                        Take Exam
                      </Link>
                    </>
                  )}
                </div>
              </motion.div>
            ))}
          </div>
        )}
      </motion.div>
    </div>
  );
}
