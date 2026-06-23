import { useAuth } from '../contexts/AuthContext';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Button } from '../components/Button';
import { MainLayout } from '../layouts/MainLayout';
import { useEffect, useState } from 'react';
import { ExamService } from '../services/ExamService';
import { Exam } from '../types/exam';
import '../styles/pages/dashboard.css';

function getDayGreeting(): string {
  const h = new Date().getHours();
  if (h < 12) return 'Good morning';
  if (h < 18) return 'Good afternoon';
  return 'Good evening';
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'live':
    case 'active':
    case 'published': return 'badge-live';
    case 'draft': return 'badge-draft';
    case 'grading': return 'badge-grading';
    case 'completed':
    case 'submitted': return 'badge-completed';
    case 'closed':
    case 'archived': return 'badge-closed';
    default: return 'badge-draft';
  }
}

function getStatusColor(status: string): string {
  switch (status) {
    case 'live':
    case 'active': return '#F59E0B';
    case 'draft': return '#A8A29E';
    case 'grading': return '#D97706';
    case 'completed':
    case 'submitted': return '#16A34A';
    case 'closed': return '#78716C';
    default: return '#A8A29E';
  }
}

const today = new Date();
const dateStr = today.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' });

export function Dashboard() {
  const { user } = useAuth();
  const [exams, setExams] = useState<Exam[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadDashboardData();
  }, []);

  const loadDashboardData = async () => {
    try {
      setLoading(true);
      const examsData = await ExamService.getExams();
      setExams(examsData);
    } catch (error) {
      console.error('Failed to load dashboard data:', error);
    } finally {
      setLoading(false);
    }
  };

  const activeCount = exams.filter(e => e.status === 'active' || e.status === 'published' || e.status === 'live').length;
  const draftCount = exams.filter(e => e.status === 'draft').length;
  const toGradeCount = exams.filter(e => e.status === 'grading').length;
  const recentExams = exams.slice(0, 5);
  const name = user?.displayName || user?.username || user?.email?.split('@')[0] || 'there';

  return (
    <MainLayout>
      <div className="dashboard-page">
        <div className="page-header">
          <h1>{getDayGreeting()}, {name}.</h1>
          <p className="page-subtitle">{dateStr} · {activeCount} exam{activeCount !== 1 ? 's' : ''} active today</p>
        </div>

        {/* Stats Grid */}
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Active exams</div>
            <div className="stat-value amber">{loading ? '...' : activeCount}</div>
            <div className="stat-delta up">↑ {activeCount} active{activeCount !== 1 ? 's' : ''}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">To grade</div>
            <div className="stat-value">{loading ? '...' : toGradeCount}</div>
            <div className="stat-delta down">{toGradeCount > 0 ? `${toGradeCount} pending` : 'None pending'}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Draft exams</div>
            <div className="stat-value green">{loading ? '...' : draftCount}</div>
            <div className="stat-delta up">{draftCount > 0 ? `${draftCount} not published` : 'All published'}</div>
          </div>
          <div className="stat-card">
            <div className="stat-label">Total exams</div>
            <div className="stat-value">{loading ? '...' : exams.length}</div>
            <div className="stat-delta">{exams.length} total</div>
          </div>
        </div>

        {/* Recent Exams */}
        <div className="content-row">
          <div className="card">
            <div className="card-header">
              <div className="card-title">Recent exams</div>
              <Link to="/exams" className="card-link">View all →</Link>
            </div>
            {recentExams.length > 0 ? (
              recentExams.map((exam) => (
                <Link key={exam.id} to={`/exams/${exam.id}/questions`} style={{ textDecoration: 'none', display: 'block' }}>
                  <div className="exam-row">
                    <div className="exam-indicator" style={{ background: getStatusColor(exam.status) }} />
                    <div className="exam-info">
                      <div className="exam-name">{exam.title}</div>
                      <div className="exam-meta">{exam.subject || 'General'} · {exam.questionCount || 0} questions</div>
                    </div>
                    <span className={`badge ${getStatusBadgeClass(exam.status)}`}>{exam.status}</span>
                  </div>
                </Link>
              ))
            ) : (
              <div style={{ padding: '24px 16px', textAlign: 'center', color: 'var(--color-gray-3)', fontSize: 'var(--text-sm)' }}>
                {loading ? 'Loading...' : 'No exams yet'}
              </div>
            )}
          </div>

          {/* Quick actions card */}
          <div className="card">
            <div className="card-header">
              <div className="card-title">Quick actions</div>
            </div>
            <div style={{ padding: '16px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <Link to="/exams" style={{ display: 'block' }}>
                <Button variant="secondary" style={{ width: '100%' }}>View all exams</Button>
              </Link>
              {user?.role === 'instructor' && (
                <Link to="/exams/create" style={{ display: 'block' }}>
                  <Button variant="primary" style={{ width: '100%' }}>Create new exam</Button>
                </Link>
              )}
            </div>
          </div>
        </div>
      </div>
    </MainLayout>
  );
}
