import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../contexts/AuthContext';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import type { Exam, ExamStatus } from '../types/exam';
import '../styles/pages/exams.css';

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'draft': return 'badge-draft';
    case 'published': return 'badge-scheduled';
    case 'active': return 'badge-live';
    case 'completed': return 'badge-completed';
    case 'archived': return 'badge-closed';
    default: return 'badge-draft';
  }
}

function getStatusLabel(status: string): string {
  return status.charAt(0).toUpperCase() + status.slice(1);
}

function getStatusColor(status: string): string {
  switch (status) {
    case 'active': return '#F59E0B';
    case 'draft': return '#A8A29E';
    case 'completed': return '#16A34A';
    case 'archived': return '#78716C';
    default: return '#A8A29E';
  }
}

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

  if (loading) {
    return (
      <MainLayout>
        <div className="loading-spinner">Loading exams...</div>
      </MainLayout>
    );
  }

  if (error) {
    return (
      <MainLayout>
        <div className="error-message">
          <h2>Error loading exams</h2>
          <p>{error}</p>
          <Button variant="primary" onClick={loadExams}>Try again</Button>
        </div>
      </MainLayout>
    );
  }

  return (
    <MainLayout>
      <div className="exams-page">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
      >
        <div className="page-header">
          <div>
            <h1>{isInstructor ? 'My exams' : 'Available exams'}</h1>
            <p className="page-subtitle">
              {isInstructor
                ? 'Create and manage your exams'
                : 'Browse and take available exams'}
            </p>
          </div>
          
          {isInstructor && (
            <Link to="/exams/create">
              <Button variant="primary">Create exam</Button>
            </Link>
          )}
        </div>

        {isInstructor && (
          <div className="filter-bar">
            <div className="filter-group">
              <label>Status:</label>
              <select
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as ExamStatus | 'all')}
              >
                <option value="all">All</option>
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
            <h2>No exams found</h2>
            <p>
              {isInstructor
                ? 'Create your first exam to get started'
                : 'No exams are currently available'}
            </p>
            {isInstructor && (
              <Link to="/exams/create">
                <Button variant="primary">Create exam</Button>
              </Link>
            )}
          </div>
        ) : (
          <div className="exams-grid">
            {filteredExams.map((exam, index) => (
              <motion.div
                key={exam.id}
                className="exam-card"
                initial={{ opacity: 0, y: 12 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.25, delay: index * 0.04 }}
              >
                <div className="exam-indicator" style={{ background: getStatusColor(exam.status) }} />
                <div className="exam-card-header">
                  <h3>{exam.title}</h3>
                  <span className={`badge ${getStatusBadgeClass(exam.status)}`}>{getStatusLabel(exam.status)}</span>
                </div>
                
                {exam.description && (
                  <p className="exam-description">{exam.description}</p>
                )}

                <div className="exam-meta">
                  {exam.subject && (
                    <span className="meta-item">{exam.subject}</span>
                  )}
                  <span className="meta-item">{exam.questionCount || 0} questions</span>
                  <span className="meta-item">{exam.totalPoints || 0} points</span>
                  {exam.timeLimit && (
                    <span className="meta-item">{exam.timeLimit} min</span>
                  )}
                </div>

                {isInstructor && (
                  <div className="exam-stats">
                    <span>Attempts: {exam.attemptCount || 0}</span>
                    {exam.averageScore != null && (
                      <span>Avg score: {exam.averageScore.toFixed(1)}%</span>
                    )}
                  </div>
                )}

                <div className="exam-card-actions">
                  {isInstructor ? (
                    <>
                      <Link to={`/exams/${exam.id}`} style={{ flex: 1 }}>
                        <Button variant="secondary" style={{ width: '100%' }}>Manage</Button>
                      </Link>
                      <Link to={`/exams/${exam.id}/questions`} style={{ flex: 1 }}>
                        <Button variant="outline" style={{ width: '100%' }}>Questions</Button>
                      </Link>
                    </>
                  ) : (
                    <>
                      <Link to={`/exams/${exam.id}`} style={{ flex: 1 }}>
                        <Button variant="secondary" style={{ width: '100%' }}>View details</Button>
                      </Link>
                      <a href={`${import.meta.env.VITE_EXAM_PORTAL_URL || ''}/exams/${exam.id}/take`} target="_blank" rel="noopener noreferrer" style={{ flex: 1, textDecoration: 'none' }}>
                        <Button variant="primary" style={{ width: '100%' }}>Take exam</Button>
                      </a>
                    </>
                  )}
                </div>
              </motion.div>
            ))}
          </div>
        )}
      </motion.div>
    </div>
    </MainLayout>
  );
}
