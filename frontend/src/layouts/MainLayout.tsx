import { ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../contexts/AuthContext';
import { Button } from '../components/Button';
import { NotificationBell } from '../components/NotificationBell';
import '../styles/layouts/main-layout.css';

interface MainLayoutProps {
  children: ReactNode;
}

export function MainLayout({ children }: MainLayoutProps) {
  const { user, logout } = useAuth();
  const location = useLocation();

  const isActive = (path: string) => {
    if (path === '/dashboard') {
      return location.pathname === path;
    }
    return location.pathname.startsWith(path);
  };

  const navItems = [
    { path: '/dashboard', label: 'Dashboard', icon: '📊' },
    { path: '/exams', label: 'Exams', icon: '📝' },
    ...(user?.role === 'instructor' ? [
      { path: '/exams/create', label: 'Create Exam', icon: '➕' },
      { path: '/students', label: 'Students', icon: '👥' },
      { path: '/grading', label: 'Grading Queue', icon: '✓' },
      { path: '/analytics', label: 'Analytics', icon: '📈' },
      { path: '/incidents', label: 'Incident Reports', icon: '👁️' },
    ] : []),
    ...(user?.role === 'student' ? [
      { path: '/my-exams', label: 'My Exams', icon: '📚' },
      { path: '/results', label: 'Results', icon: '🎯' },
    ] : []),
  ];

  return (
    <div className="main-layout">
      {/* Sidebar */}
      <motion.aside
        className="sidebar"
        initial={{ x: -280 }}
        animate={{ x: 0 }}
        transition={{ type: 'spring', stiffness: 300, damping: 30 }}
      >
        <div className="sidebar-header">
          <h1 className="sidebar-logo">ExamForge</h1>
          <p className="sidebar-subtitle">Exam Management</p>
        </div>

        <nav className="sidebar-nav">
          {navItems.map((item) => (
            <Link
              key={item.path}
              to={item.path}
              className={`nav-item ${isActive(item.path) ? 'active' : ''}`}
            >
              <span className="nav-icon">{item.icon}</span>
              <span className="nav-label">{item.label}</span>
            </Link>
          ))}
        </nav>

        <div className="sidebar-footer">
          <div className="user-info">
            <div className="user-avatar">
              {(user?.displayName || user?.username || user?.email || 'U')[0].toUpperCase()}
            </div>
            <div className="user-details">
              <p className="user-name">{user?.displayName || user?.username || 'User'}</p>
              <p className="user-role">{user?.role || 'Student'}</p>
            </div>
          </div>
          <Button 
            variant="outline" 
            size="sm" 
            onClick={logout}
            style={{ width: '100%', marginTop: 'var(--spacing-3)' }}
          >
            Logout
          </Button>
        </div>
      </motion.aside>

      {/* Main Content */}
      <main className="main-content">
        <div className="content-header">
          <div className="header-spacer" />
          <NotificationBell />
        </div>
        <div className="content-container">
          {children}
        </div>
      </main>
    </div>
  );
}
