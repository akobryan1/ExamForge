import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) { setError('Email and password are required'); return; }
    try {
      setLoading(true);
      setError('');
      const userCredential = await signInWithEmailAndPassword(auth, email, password);
      
      // Get Firebase ID token and store it for API requests
      const idToken = await userCredential.user.getIdToken();
      localStorage.setItem('accessToken', idToken);
      
      console.log('[ExamLogin] Login successful, token stored. Redirecting to:', redirect);
      setSuccess(true);
      setTimeout(() => navigate(redirect, { replace: true }), 500);
    } catch (err: any) {
      console.error('[ExamLogin] Error:', err.code);
      const code = err.code;
      if (code === 'auth/user-not-found' || code === 'auth/wrong-password' || code === 'auth/invalid-credential') {
        setError('Invalid email or password');
      } else if (code === 'auth/invalid-email') {
        setError('Invalid email format');
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
