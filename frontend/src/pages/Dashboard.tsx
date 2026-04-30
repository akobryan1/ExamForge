import { useAuth } from '../contexts/AuthContext';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Button } from '../components/Button';
import { MainLayout } from '../layouts/MainLayout';
import { pageTransition, fadeIn, staggerContainer, staggerItem } from '../utils/animations';
import { useEffect, useState } from 'react';
import { ExamService } from '../services/ExamService';
import { Exam } from '../types/exam';

export function Dashboard() {
  const { user } = useAuth();
  const [exams, setExams] = useState<Exam[]>([]);
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState({
    totalExams: 0,
    activeExams: 0,
    draftExams: 0,
    completedExams: 0
  });

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    try {
      setLoading(true);
      const examsData = await ExamService.getExams();
      setExams(examsData);
      
      // Calculate statistics
      setStats({
        totalExams: examsData.length,
        activeExams: examsData.filter(e => e.status === 'active' || e.status === 'published').length,
        draftExams: examsData.filter(e => e.status === 'draft').length,
        completedExams: examsData.filter(e => e.status === 'completed').length
      });
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
    } finally {
      setLoading(false);
    }
  };

  const recentExams = exams.slice(0, 5);

  return (
    <MainLayout>
      <motion.div
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        <div className="page-header">
          <h1>Dashboard</h1>
          <p className="page-subtitle">Welcome back, {user?.displayName || user?.username || user?.email}</p>
        </div>

        {/* Statistics Cards */}
        <motion.div 
          className="stats-grid"
          variants={staggerContainer}
          initial="initial"
          animate="animate"
        >
          <motion.div className="stat-card" variants={fadeIn}>
            <p className="stat-label">Total Exams</p>
            <p className="stat-value">{loading ? '...' : stats.totalExams}</p>
          </motion.div>
          <motion.div className="stat-card" variants={fadeIn}>
            <p className="stat-label">Active Exams</p>
            <p className="stat-value" style={{ color: 'var(--success-color)' }}>
              {loading ? '...' : stats.activeExams}
            </p>
          </motion.div>
          <motion.div className="stat-card" variants={fadeIn}>
            <p className="stat-label">Draft Exams</p>
            <p className="stat-value" style={{ color: 'var(--warning-color)' }}>
              {loading ? '...' : stats.draftExams}
            </p>
          </motion.div>
          <motion.div className="stat-card" variants={fadeIn}>
            <p className="stat-label">Completed</p>
            <p className="stat-value" style={{ color: 'var(--text-secondary)' }}>
              {loading ? '...' : stats.completedExams}
            </p>
          </motion.div>
        </motion.div>

        {/* Quick Actions */}
        <div className="section">
          <div className="section-header">
            <h2 className="section-title">Quick Actions</h2>
          </div>
          <div style={{ display: 'flex', gap: 'var(--spacing-4)', flexWrap: 'wrap' }}>
            <Link to="/exams">
              <Button>View All Exams</Button>
            </Link>
            {user?.role === 'instructor' && (
              <Link to="/exams/create">
                <Button variant="primary">Create New Exam</Button>
              </Link>
            )}
          </div>
        </div>

        {/* Recent Exams */}
        {recentExams.length > 0 && (
          <div className="section">
            <div className="section-header">
              <h2 className="section-title">Recent Exams</h2>
              <Link to="/exams">
                <Button variant="outline" size="sm">View All</Button>
              </Link>
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--spacing-3)' }}>
              {recentExams.map((exam) => (
                <Link
                  key={exam.id}
                  to={`/exams/${exam.id}/questions`}
                  style={{ textDecoration: 'none' }}
                >
                  <div className="card" style={{ padding: 'var(--spacing-4)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start' }}>
                      <div style={{ flex: 1 }}>
                        <h3 style={{ margin: 0, marginBottom: 'var(--spacing-1)', color: 'var(--text-primary)' }}>
                          {exam.title}
                        </h3>
                        <p style={{ margin: 0, color: 'var(--text-secondary)', fontSize: 'var(--text-sm)' }}>
                          {exam.subject} • {exam.questionCount || 0} questions • {exam.totalPoints || 0} points
                        </p>
                      </div>
                      <span
                        className="status-badge"
                        style={{
                          padding: '0.25rem 0.75rem',
                          borderRadius: '9999px',
                          fontSize: 'var(--text-xs)',
                          fontWeight: '600',
                          textTransform: 'capitalize',
                          background: exam.status === 'published' || exam.status === 'active' ? '#d1fae5' : 
                                     exam.status === 'draft' ? '#fef3c7' : '#fee2e2',
                          color: exam.status === 'published' || exam.status === 'active' ? '#065f46' :
                                 exam.status === 'draft' ? '#92400e' : '#991b1b'
                        }}
                      >
                        {exam.status}
                      </span>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          </div>
        )}
      </motion.div>
    </MainLayout>
  );
}
