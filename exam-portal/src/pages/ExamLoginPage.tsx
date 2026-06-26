import { useState, useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { getAuth, signInWithEmailAndPassword } from 'firebase/auth';
import { initializeApp, getApps } from 'firebase/app';
import apiClient from '../apiClient';

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

export function ExamLoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const redirect = searchParams.get('redirect') || '/';
  const auth = getAuthInstance();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState(false);

  // Clear any existing tokens on mount (fresh login required each session)
  useEffect(() => {
    localStorage.removeItem('accessToken');
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) { setError('Email and password are required'); return; }
    try {
      setLoading(true);
      setError('');

      // 1. Sign in with Firebase
      const userCredential = await signInWithEmailAndPassword(auth, email, password);

      // 2. Get Firebase ID token
      const idToken = await userCredential.user.getIdToken();

      // 3. Exchange Firebase ID token for backend JWT via /api/auth/login
      console.log('[ExamLogin] Exchanging Firebase token for backend JWT...');
      const { data } = await apiClient.post('/api/auth/login', { idToken });

      // 4. Store backend-issued accessToken
      localStorage.setItem('accessToken', data.accessToken);
      
      console.log('[ExamLogin] Login successful. Redirecting to:', redirect);
      setSuccess(true);
      setTimeout(() => navigate(redirect, { replace: true }), 500);
    } catch (err: any) {
      console.error('[ExamLogin] Error:', err.code || err.message);
      console.error('[ExamLogin] Full error:', err);
      if (err.response) {
        console.error('[ExamLogin] Response data:', err.response.data);
        console.error('[ExamLogin] Response status:', err.response.status);
      }
      // Firebase auth errors
      const code = err.code;
      if (code === 'auth/user-not-found' || code === 'auth/wrong-password' || code === 'auth/invalid-credential') {
        setError('Invalid email or password');
      } else if (code === 'auth/invalid-email') {
        setError('Invalid email format');
      } else if (err.response?.data?.error) {
        setError(err.response.data.error);
      } else if (err.response?.data?.message) {
        setError(err.response.data.message);
      } else {
        setError(err.message || 'Login failed');
      }
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-content">
        <div className="auth-card">
          <div className="auth-header">
            <h1 className="auth-title">ExamForge</h1>
            <p className="auth-subtitle">Sign in to access your exam</p>
          </div>
          {error && <div className="auth-error">{error}</div>}
          <form onSubmit={handleSubmit}>
            <div className="form-group">
              <label>Email</label>
              <input type="email" value={email} onChange={e => setEmail(e.target.value)} placeholder="your@email.com" className="input" disabled={loading || success} />
            </div>
            <div className="form-group">
              <label>Password</label>
              <input type="password" value={password} onChange={e => setPassword(e.target.value)} placeholder="Enter your password" className="input" disabled={loading || success} />
            </div>
            {success ? (
              <div className="auth-success" style={{ padding: 'var(--spacing-3)', background: 'rgba(22, 163, 74, 0.1)', color: 'var(--color-success-700)', borderRadius: 'var(--radius-md)', textAlign: 'center', fontSize: 14, fontWeight: 500 }}>
                ✓ Login successful! Redirecting...
              </div>
            ) : (
              <button type="submit" className="btn btn-primary auth-submit" disabled={loading}>
                {loading ? 'Signing in...' : 'Sign in'}
              </button>
            )}
          </form>
        </div>
      </div>
    </div>
  );
}
