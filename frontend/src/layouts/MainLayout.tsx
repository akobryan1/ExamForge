import { ReactNode, Fragment, useState, useEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
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
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Close sidebar on navigation (mobile)
  useEffect(() => {
    setSidebarOpen(false);
  }, [location.pathname]);

  // Close sidebar on Escape key
  useEffect(() => {
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setSidebarOpen(false);
    };
    window.addEventListener('keydown', handleEsc);
    return () => window.removeEventListener('keydown', handleEsc);
  }, []);

  const isActive = (path: string) => {
    if (path === '/dashboard') {
      return location.pathname === path;
    }
    return location.pathname.startsWith(path);
  };

  const navItems = [
    { path: '/dashboard', label: 'Dashboard', icon: '/icons/nav/dashboard.png' },
    { path: '/exams', label: 'Exams', icon: '/icons/nav/exams.png' },
    ...(user?.role === 'instructor' ? [
      { path: '/exams/create', label: 'Create Exam', icon: '/icons/nav/create-exam.png' },
      { path: '/students', label: 'Students', icon: '/icons/nav/students.png' },
      { path: '/grading', label: 'Grading Queue', icon: '/icons/nav/grading-queue.png' },
      { path: '/analytics', label: 'Analytics', icon: '/icons/nav/analytics.png' },
      { path: '/incidents', label: 'Incident Reports', icon: '/icons/nav/incidents.png' },
    ] : []),
    { path: '/settings', label: 'Settings', icon: '/icons/nav/settings.png' },
    ...(user?.role === 'student' ? [
      { path: '/my-exams', label: 'My Exams', icon: '/icons/nav/my-exams.png' },
      { path: '/results', label: 'Results', icon: '/icons/nav/results.png' },
    ] : []),
  ];

  return (
    <div className="main-layout">
      {/* Mobile hamburger */}
      <button className="sidebar-toggle" onClick={() => setSidebarOpen(prev => !prev)} aria-label="Toggle navigation menu">
        <span className={`hamburger ${sidebarOpen ? 'open' : ''}`}>
          <span /><span /><span />
        </span>
      </button>

      {/* Mobile backdrop */}
      <AnimatePresence>
        {sidebarOpen && (
          <motion.div
            className="sidebar-backdrop"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setSidebarOpen(false)}
          />
        )}
      </AnimatePresence>

      {/* Sidebar */}
      <aside className={`sidebar ${sidebarOpen ? 'open' : ''}`}>
        <div className="sidebar-header">
          <h1 className="sidebar-logo">ExamForge</h1>
          <p className="sidebar-subtitle">Exam Management</p>
        </div>

        <nav className="sidebar-nav">
          {navItems.map((item, idx) => (
            <Fragment key={item.path}>
              {/* Insert "Instructor" label before the first instructor-only item */}
              {idx === 2 && user?.role === 'instructor' && (
                <div className="nav-section-label">Instructor</div>
              )}
              <Link
                to={item.path}
                className={`nav-item ${isActive(item.path) ? 'active' : ''}`}
              >
                <span className="nav-icon">
                  <img src={item.icon} alt="" className="nav-icon-img" />
                </span>
                <span className="nav-label">{item.label}</span>
              </Link>
            </Fragment>
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
      </aside>

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
