import { useAuth } from '../contexts/AuthContext';
import { motion } from 'framer-motion';
import { Button } from '../components/Button';
import { pageTransition } from '../utils/animations';

export function Dashboard() {
  const { user, logout } = useAuth();

  return (
    <motion.div
      className="container"
      style={{ paddingTop: 'var(--spacing-12)' }}
      variants={pageTransition}
      initial="initial"
      animate="animate"
      exit="exit"
    >
      <div className="flex justify-between items-center" style={{ marginBottom: 'var(--spacing-8)' }}>
        <div>
          <h1 style={{ fontFamily: 'var(--font-display)', marginBottom: 'var(--spacing-2)' }}>
            Dashboard
          </h1>
          <p className="lead">Welcome back, {user?.displayName || user?.username || user?.email}</p>
        </div>
        <Button variant="outline" onClick={logout}>
          Logout
        </Button>
      </div>

      <div className="grid grid-cols-3 gap-6">
        <div className="card">
          <h3 className="card-title" style={{ fontSize: 'var(--text-xl)' }}>Total Exams</h3>
          <p style={{ fontSize: 'var(--text-4xl)', fontWeight: 'var(--font-bold)', color: 'var(--color-primary-600)' }}>
            0
          </p>
        </div>
        <div className="card">
          <h3 className="card-title" style={{ fontSize: 'var(--text-xl)' }}>Active Sessions</h3>
          <p style={{ fontSize: 'var(--text-4xl)', fontWeight: 'var(--font-bold)', color: 'var(--color-success-600)' }}>
            0
          </p>
        </div>
        <div className="card">
          <h3 className="card-title" style={{ fontSize: 'var(--text-xl)' }}>Pending Reviews</h3>
          <p style={{ fontSize: 'var(--text-4xl)', fontWeight: 'var(--font-bold)', color: 'var(--color-warning-600)' }}>
            0
          </p>
        </div>
      </div>

      <div className="card" style={{ marginTop: 'var(--spacing-8)' }}>
        <h2 className="card-title">Getting Started</h2>
        <div className="card-body">
          <p>Phase 2 authentication system is now complete! You can:</p>
          <ul style={{ marginLeft: 'var(--spacing-6)', marginTop: 'var(--spacing-4)' }}>
            <li>Login with email/password (Supabase)</li>
            <li>Login with Google (Firebase)</li>
            <li>Automatic token refresh</li>
            <li>Protected routes</li>
          </ul>
          <p style={{ marginTop: 'var(--spacing-4)' }}>
            Next steps: Implement exam management, real-time monitoring, and grading features.
          </p>
        </div>
      </div>
    </motion.div>
  );
}
