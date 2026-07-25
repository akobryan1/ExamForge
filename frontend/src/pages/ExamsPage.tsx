import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../contexts/AuthContext';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { useExamList, useCloneExam, useRepublishExam, useCompleteExam, useDeleteExam } from '../hooks/useExamQueries';
import type { Exam } from '../types/exam';
import '../styles/pages/exams.css';

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'draft': return 'stamp stamp-draft';
    case 'published': return 'stamp stamp-scheduled';
    case 'active': return 'stamp stamp-active';
    case 'completed': return 'stamp stamp-completed';
    case 'archived': return 'stamp stamp-archived';
    default: return 'stamp stamp-draft';
  }
}

function getSpineClass(status: string): string {
  switch (status) {
    case 'active':
    case 'published': return 'spine-active';
    case 'draft': return 'spine-draft';
    case 'completed': return 'spine-completed';
    case 'archived': return 'spine-archived';
    default: return 'spine-draft';
  }
}

function getStatusLabel(status: string): string {
  return status.charAt(0).toUpperCase() + status.slice(1);
}

export function ExamsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const { data: exams = [], isLoading, error, refetch } = useExamList();
  const cloneMutation = useCloneExam();
  const republishMutation = useRepublishExam();
  const completeMutation = useCompleteExam();
  const deleteMutation = useDeleteExam();

  const [activeTab, setActiveTab] = useState<'active' | 'past'>('active');

  const isInstructor = user?.role === 'instructor' || user?.role === 'admin';

  const activeExams = exams.filter((e: Exam) =>
    ['draft', 'published', 'active'].includes(e.status)
  );
  const pastExams = exams.filter((e: Exam) =>
    ['completed', 'archived'].includes(e.status)
  );

  const handleClone = async (examId: string) => {
    try {
      const cloned = await cloneMutation.mutateAsync(examId);
      navigate(`/exams/${cloned.id}/edit`);
    } catch (err: any) {
      alert(err.message || 'Failed to clone exam');
    }
  };

  const handleRepublish = async (examId: string) => {
    try {
      await republishMutation.mutateAsync(examId);
    } catch (err: any) {
      alert(err.message || 'Failed to republish exam');
    }
  };

  const handleComplete = async (examId: string) => {
    if (!confirm('Mark this exam as completed? Students will no longer be able to access it.')) return;
    try {
      await completeMutation.mutateAsync(examId);
    } catch (err: any) {
      alert(err.message || 'Failed to complete exam');
    }
  };

  const handleDelete = async (examId: string) => {
    if (!confirm('Delete this exam permanently? This cannot be undone.')) return;
    try {
      await deleteMutation.mutateAsync(examId);
    } catch (err: any) {
      alert(err.message || 'Failed to delete exam');
    }
  };

  if (isLoading) {
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
          <p>{error instanceof Error ? error.message : 'Failed to load exams'}</p>
          <Button variant="primary" onClick={() => refetch()}>Try again</Button>
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
          <div className="folder-tabs">
            <button
              className={`folder-tab ${activeTab === 'active' ? 'active' : ''}`}
              onClick={() => setActiveTab('active')}
            >
              <img src="/icons/tabs/active-exams.png" alt="" className="tab-icon" /> Active Exams ({activeExams.length})
            </button>
            <button
              className={`folder-tab ${activeTab === 'past' ? 'active' : ''}`}
              onClick={() => setActiveTab('past')}
            >
              <img src="/icons/tabs/past-exams.png" alt="" className="tab-icon" /> Past Exams ({pastExams.length})
            </button>
          </div>
        )}

        <div className="folder-body">
        {activeTab === 'active' && (activeExams.length === 0 ? (
          <div className="empty-state">
            <h2>No active exams</h2>
            <p>{isInstructor ? 'Create your first exam to get started' : 'No exams are currently available'}</p>
            {isInstructor && <Link to="/exams/create"><Button variant="primary">Create exam</Button></Link>}
          </div>
        ) : (
          <div className="exams-grid">
            {activeExams.map((exam: Exam, index: number) => (
              <motion.div key={exam.id} className="exam-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.25, delay: index * 0.04 }}>
                <div className={`exam-indicator ${getSpineClass(exam.status)}`} />
                <div className="exam-card-header">
                  <h3>{exam.title}</h3>
                  <span className={getStatusBadgeClass(exam.status)}>{getStatusLabel(exam.status)}</span>
                </div>
                {exam.description && <p className="exam-description">{exam.description}</p>}
                <div className="exam-meta">
                  {exam.subject && <span className="meta-item">{exam.subject}</span>}
                  <span className="meta-item">{exam.questionCount || 0} questions</span>
                  <span className="meta-item">{exam.totalPoints || 0} points</span>
                  {exam.timeLimit && <span className="meta-item">{exam.timeLimit} min</span>}
                </div>
                {isInstructor && (
                  <div className="exam-stats">
                    <span>Attempts: <b>{exam.attemptCount || 0}</b></span>
                    {exam.averageScore != null && <span>Avg score: <b>{exam.averageScore.toFixed(1)}%</b></span>}
                  </div>
                )}
                <div className="exam-card-actions">
                  {isInstructor ? (
                    <>
                      <Link to={`/exams/${exam.id}`} style={{ flex: 1 }}><Button variant="secondary" style={{ width: '100%' }}>Manage</Button></Link>
                      <Link to={`/exams/${exam.id}/questions`} style={{ flex: 1 }}><Button variant="outline" style={{ width: '100%' }}>Questions</Button></Link>
                      {exam.status !== 'archived' && exam.status !== 'draft' && exam.status !== 'completed' && (
                        <Button variant="text" size="sm" onClick={() => handleComplete(exam.id)} style={{ padding: '8px' }} title="End exam">⏹</Button>
                      )}
                    </>
                  ) : (
                    <>
                      <Link to={`/exams/${exam.id}`} style={{ flex: 1 }}><Button variant="secondary" style={{ width: '100%' }}>View details</Button></Link>
                      <a href={`${import.meta.env.VITE_EXAM_PORTAL_URL || ''}/exams/${exam.id}/take`} target="_blank" rel="noopener noreferrer" style={{ flex: 1, textDecoration: 'none' }}>
                        <Button variant="primary" style={{ width: '100%' }}>Take exam</Button>
                      </a>
                    </>
                  )}
                </div>
              </motion.div>
            ))}
          </div>
        ))}

        {activeTab === 'past' && isInstructor && (pastExams.length === 0 ? (
          <div className="empty-state">
            <h2>No past exams</h2>
            <p>Completed and archived exams will appear here. You can clone, republish, or permanently delete them.</p>
          </div>
        ) : (
          <div className="exams-grid">
            {pastExams.map((exam: Exam, index: number) => (
              <motion.div key={exam.id} className="exam-card past-exam-card" initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.25, delay: index * 0.04 }}>
                <div className={`exam-indicator spine-archived`} />
                <div className="exam-card-header">
                  <h3>{exam.title}</h3>
                  <span className={getStatusBadgeClass(exam.status)}>{getStatusLabel(exam.status)}</span>
                </div>
                {exam.description && <p className="exam-description">{exam.description}</p>}
                <div className="exam-meta">
                  <span className="meta-item">{exam.questionCount || 0} questions</span>
                  <span className="meta-item">{exam.totalPoints || 0} points</span>
                  {exam.attemptCount > 0 && <span className="meta-item"><b>{exam.attemptCount}</b> attempts</span>}
                  {exam.averageScore != null && <span className="meta-item">Avg: <b>{exam.averageScore.toFixed(1)}%</b></span>}
                </div>
                <div className="exam-card-actions" style={{ flexWrap: 'wrap' }}>
                  <Button variant="secondary" size="sm" onClick={() => handleClone(exam.id)} style={{ flex: 1, minWidth: 80 }}>
                    <img src="/icons/tabs/clone.png" alt="" className="btn-icon" /> Clone
                  </Button>
                  {exam.status === 'completed' && (
                    <Button variant="primary" size="sm" onClick={() => handleRepublish(exam.id)} style={{ flex: 1, minWidth: 80 }}>
                      📤 Republish
                    </Button>
                  )}
                  <Link to={`/exams/${exam.id}`} style={{ flex: 1, minWidth: 80 }}>
                    <Button variant="outline" size="sm" style={{ width: '100%' }}>👁 View</Button>
                  </Link>
                  <Button variant="text" size="sm" onClick={() => handleDelete(exam.id)} style={{ color: 'var(--ledger-red)', padding: '8px' }} title="Delete permanently">
                    🗑
                  </Button>
                </div>
              </motion.div>
            ))}
          </div>
        ))}
        </div>{/* end folder-body */}
      </motion.div>
    </div>
    </MainLayout>
  );
}
