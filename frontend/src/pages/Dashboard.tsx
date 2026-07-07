import { useAuth } from '../contexts/AuthContext';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Button } from '../components/Button';
import { MainLayout } from '../layouts/MainLayout';
import { useExamList } from '../hooks/useExamQueries';
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
    case 'active':
    case 'published': return 'stamp stamp-active';
    case 'draft': return 'stamp stamp-draft';
    case 'completed': return 'stamp stamp-completed';
    case 'archived': return 'stamp stamp-archived';
    default: return 'stamp stamp-draft';
  }
}

function getStatusColor(status: string): string {
  switch (status) {
    case 'active':
    case 'published': return '#B9791E';
    case 'draft': return '#6B6459';
    case 'completed': return '#3E6B47';
    case 'archived': return '#5A6472';
    default: return '#6B6459';
  }
}

const today = new Date();
const dateStr = today.toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' });
const dayAbbr = today.toLocaleDateString('en-US', { weekday: 'short' }).toUpperCase().slice(0, 3);
const monthAbbr = today.toLocaleDateString('en-US', { month: 'short' }).toUpperCase();
const dayNum = today.getDate();

export function Dashboard() {
  const { user } = useAuth();
  const { data: exams = [], isLoading: loading } = useExamList();

  const activeCount = exams.filter(e => e.status === 'active' || e.status === 'published').length;
  const draftCount = exams.filter(e => e.status === 'draft').length;
  const toGradeCount = exams.filter(e => e.status === 'completed').length;
  const recentExams = exams.slice(0, 5);
  const name = user?.displayName || user?.username || user?.email?.split('@')[0] || 'there';

  return (
    <MainLayout>
      <div className="dashboard-page">
        <div className="page-header" style={{ display: 'flex', alignItems: 'flex-end', gap: '22px', marginBottom: 40 }}>
          <div className="stamp-date">
            <span>{dayAbbr}</span>
            <span>{monthAbbr}·{dayNum}</span>
          </div>
          <div>
            <h1>{getDayGreeting()}, {name}.</h1>
            <p className="page-subtitle">{dateStr} · <strong>{activeCount}</strong> exam{activeCount !== 1 ? 's' : ''} active today</p>
          </div>
        </div>

        {/* Stats Grid */}
        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-tab">Active</div>
            <div className="stat-value amber">{loading ? '...' : activeCount}</div>
            <div className="stat-delta">↑ {activeCount} live now</div>
          </div>
          <div className="stat-card">
            <div className="stat-tab">To grade</div>
            <div className="stat-value red">{loading ? '...' : toGradeCount}</div>
            <div className="stat-delta">{toGradeCount > 0 ? `${toGradeCount} pending` : 'None pending'}</div>
          </div>
          <div className="stat-card">
            <div className="stat-tab">Draft</div>
            <div className="stat-value green">{loading ? '...' : draftCount}</div>
            <div className="stat-delta">{draftCount > 0 ? `${draftCount} not published` : 'All published'}</div>
          </div>
          <div className="stat-card">
            <div className="stat-tab">Total</div>
            <div className="stat-value blue">{loading ? '...' : exams.length}</div>
            <div className="stat-delta">all time</div>
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
              <div className="ledger">
                {recentExams.map((exam) => (
                  <Link key={exam.id} to={`/exams/${exam.id}/questions`} className="exam-row">
                    <div className="exam-info">
                      <div className="exam-name">{exam.title}</div>
                      <div className="exam-meta">{exam.subject || 'General'} · {exam.questionCount || 0} questions</div>
                    </div>
                    <span className={getStatusBadgeClass(exam.status)}>{exam.status}</span>
                  </Link>
                ))}
              </div>
            ) : (
              <div className="empty-state-ledger">
                {loading ? 'Loading...' : 'No exams yet'}
              </div>
            )}
          </div>

          {/* Quick actions card — note style */}
          <div className="card note-card">
            <div className="card-header">
              <div className="card-title">Quick actions</div>
            </div>
            <div style={{ padding: '18px 20px 20px', display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <Link to="/exams" style={{ display: 'block', textDecoration: 'none' }}>
                <Button variant="secondary" style={{ width: '100%' }}>View all exams</Button>
              </Link>
              {user?.role === 'instructor' && (
                <Link to="/exams/create" style={{ display: 'block', textDecoration: 'none' }}>
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
