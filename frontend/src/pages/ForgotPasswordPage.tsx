import { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { auth } from '../config/firebase';
import { sendPasswordResetEmail } from 'firebase/auth';
import { Button } from '../components/Button';
import { Input } from '../components/Input';
import { pageTransition, staggerContainer, staggerItem } from '../utils/animations';
import '../styles/auth.css';

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) {
      setError('Please enter your email address');
      return;
    }

    try {
      setIsLoading(true);
      setError(null);
      await sendPasswordResetEmail(auth, email.trim());
      setSent(true);
    } catch (err: any) {
      console.error('[ForgotPassword] Error:', err.code, err.message);
      const code = err.code;
      if (code === 'auth/user-not-found') {
        setError('No account found with this email address');
      } else if (code === 'auth/invalid-email') {
        setError('Invalid email format');
      } else if (code === 'auth/too-many-requests') {
        setError('Too many requests. Please try again later.');
      } else if (code === 'auth/invalid-api-key') {
        setError('Configuration error. Please contact support.');
      } else {
        setError(err.message || 'Failed to send reset email');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <motion.div
        className="auth-content"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        <motion.div
          className="auth-card"
          variants={staggerContainer}
          initial="initial"
          animate="animate"
        >
          <div className="seal-emblem">EF</div>

          {/* Header */}
          <motion.div className="auth-header" variants={staggerItem}>
            <h1 className="auth-title">Reset password</h1>
            <p className="auth-subtitle">
              {sent
                ? 'Check your inbox for the reset link'
                : 'Enter your email and we\'ll send you a reset link'}
            </p>
          </motion.div>

          {/* Error Message */}
          {error && (
            <motion.div
              className="auth-error"
              initial={{ opacity: 0, y: -10 }}
              animate={{ opacity: 1, y: 0 }}
            >
              {error}
            </motion.div>
          )}

          {/* Success Message */}
          {sent && (
            <motion.div
              className="auth-success"
              initial={{ opacity: 0, y: -10 }}
              animate={{ opacity: 1, y: 0 }}
            >
              <img src="/icons/status/correct.png" alt="" className="inline-icon" /> Reset link sent! Check your email (including spam folder).
            </motion.div>
          )}

          {/* Form */}
          {!sent && (
            <form onSubmit={handleSubmit}>
              <Input
                type="email"
                name="email"
                label="Email Address"
                placeholder="Enter your email"
                value={email}
                onChange={(e) => { setEmail(e.target.value); setError(null); }}
                required
                autoComplete="email"
              />

              <Button
                type="submit"
                variant="primary"
                size="lg"
                className="auth-submit"
                disabled={isLoading}
              >
                {isLoading ? 'Sending...' : 'Send reset link'}
              </Button>
            </form>
          )}

          {/* Back to Login */}
          <motion.div className="auth-footer" variants={staggerItem}>
            <p>
              <Link to="/login" className="auth-link-bold">
                &larr; Back to sign in
              </Link>
            </p>
          </motion.div>
        </motion.div>
      </motion.div>

      {/* Background Decoration */}
      <div className="auth-background">
        <div className="auth-gradient auth-gradient-1" />
        <div className="auth-gradient auth-gradient-2" />
      </div>
    </div>
  );
}
