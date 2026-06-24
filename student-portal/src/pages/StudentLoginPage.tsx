import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { getAuth, signInWithEmailAndPassword } from 'firebase/auth';
import { initializeApp, getApps } from 'firebase/app';

let authInstance: ReturnType<typeof getAuth> | null = null;
function getAuthInstance() {
  if (!authInstance) {
    const firebaseConfig = {
      apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
      authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN || 'examforge-201e8.firebaseapp.com',
      projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID || 'examforge-201e8',
      storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET || 'examforge-201e8.firebasestorage.app',
      messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
      appId: import.meta.env.VITE_FIREBASE_APP_ID,
    };
    const app = getApps().length === 0 ? initializeApp(firebaseConfig) : getApps()[0];
    authInstance = getAuth(app);
  }
  return authInstance;
}

export function StudentLoginPage() {
  const navigate = useNavigate();
  const auth = getAuthInstance();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) { setError('Email and password are required'); return; }
    try {
      setLoading(true); setError('');
      await signInWithEmailAndPassword(auth, email, password);
      // After login, redirect to home (or stored redirect path)
      const params = new URLSearchParams(window.location.search);
      const redirect = params.get('redirect') || '/';
      navigate(redirect, { replace: true });
    } catch (err: any) {
      console.error('[Login] Firebase error:', err.code, err.message);
      const code = err.code;
      if (code === 'auth/user-not-found' || code === 'auth/wrong-password' || code === 'auth/invalid-credential') {
        setError('Invalid email or password');
      } else if (code === 'auth/invalid-email') {
        setError('Invalid email format');
      } else if (code === 'auth/invalid-api-key') {
        setError('Configuration error. Please contact support.');
      } else {
        setError(err.message || 'Login failed');
      }
    } finally { setLoading(false); }
  };

  return (
    <div className="auth-container">
      <div className="auth-content">
        <div className="auth-card">
          <div className="auth-header">
            <h1 className="auth-title">ExamForge</h1>
            <p className="auth-subtitle">Sign in to access your exams</p>
          </div>
          {error && <div className="auth-error">{error}</div>}
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label>Email</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="your@email.com" className="input" />
            </div>
            <div className="form-group">
              <label>Password</label>
              <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder="Enter your password" className="input" />
            </div>
            <button type="submit" className="btn btn-primary auth-submit" disabled={loading}>
              {loading ? 'Signing in...' : 'Sign in'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
