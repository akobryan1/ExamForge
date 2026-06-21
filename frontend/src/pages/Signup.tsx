import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { useAuth } from '../contexts/AuthContext';
import { Button } from '../components/Button';
import { Input } from '../components/Input';
import { pageTransition, staggerContainer, staggerItem } from '../utils/animations';
import '../styles/auth.css';

export function Signup() {
  const navigate = useNavigate();
  const { signup, loginWithGoogle, error, clearError } = useAuth();
  
  const [formData, setFormData] = useState({
    email: '',
    username: '',
    password: '',
    confirmPassword: '',
  });
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Validate form
    const errors: Record<string, string> = {};
    
    if (formData.password.length < 8) {
      errors.password = 'Password must be at least 8 characters';
    }
    
    if (formData.password !== formData.confirmPassword) {
      errors.confirmPassword = 'Passwords do not match';
    }
    
    if (formData.username.length < 3) {
      errors.username = 'Username must be at least 3 characters';
    }
    
    if (!/^[a-zA-Z0-9_]+$/.test(formData.username)) {
      errors.username = 'Username can only contain letters, numbers, and underscores';
    }

    if (Object.keys(errors).length > 0) {
      setFormErrors(errors);
      return;
    }

    try {
      setIsLoading(true);
      setFormErrors({});
      clearError();
      
      await signup({
        email: formData.email,
        username: formData.username,
        password: formData.password,
      });
      
      navigate('/dashboard', { replace: true });
    } catch (error) {
      // Error is handled by AuthContext
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleSignup = async () => {
    try {
      setIsLoading(true);
      clearError();
      await loginWithGoogle();
      navigate('/dashboard', { replace: true });
    } catch (error) {
      // Error is handled by AuthContext
    } finally {
      setIsLoading(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData(prev => ({
      ...prev,
      [e.target.name]: e.target.value,
    }));
    
    // Clear field-specific error when user starts typing
    if (formErrors[e.target.name]) {
      setFormErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[e.target.name];
        return newErrors;
      });
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
          {/* Header */}
          <motion.div className="auth-header" variants={staggerItem}>
            <h1 className="auth-title">Create Account</h1>
            <p className="auth-subtitle">Join ExamForge to get started</p>
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

          {/* Signup Form */}
          <form onSubmit={handleSubmit}>
            <Input
              type="email"
              name="email"
              label="Email Address"
              placeholder="Enter your email"
              value={formData.email}
              onChange={handleChange}
              required
              autoComplete="email"
            />

            <Input
              type="text"
              name="username"
              label="Username"
              placeholder="Choose a username"
              value={formData.username}
              onChange={handleChange}
              error={formErrors.username}
              hint="Letters, numbers, and underscores only"
              required
              autoComplete="username"
            />

            <Input
              type="password"
              name="password"
              label="Password"
              placeholder="Create a password"
              value={formData.password}
              onChange={handleChange}
              error={formErrors.password}
              hint="At least 8 characters"
              required
              autoComplete="new-password"
            />

            <Input
              type="password"
              name="confirmPassword"
              label="Confirm Password"
              placeholder="Confirm your password"
              value={formData.confirmPassword}
              onChange={handleChange}
              error={formErrors.confirmPassword}
              required
              autoComplete="new-password"
            />

            <button
              type="submit"
              className="btn btn-primary btn-lg auth-submit"
              disabled={isLoading}
            >
              {isLoading ? 'Creating Account...' : 'Create Account'}
            </button>
          </form>

          {/* Divider */}
          <motion.div className="auth-divider" variants={staggerItem}>
            <span>or continue with</span>
          </motion.div>

          {/* Google Signup */}
          <motion.div variants={staggerItem}>
            <Button
              type="button"
              variant="outline"
              size="lg"
              className="auth-google"
              onClick={handleGoogleSignup}
              disabled={isLoading}
            >
              <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
                <path d="M19.6 10.227c0-.709-.064-1.39-.182-2.045H10v3.868h5.382a4.6 4.6 0 01-1.996 3.018v2.51h3.232c1.891-1.742 2.982-4.305 2.982-7.35z" fill="#4285F4"/>
                <path d="M10 20c2.7 0 4.964-.895 6.618-2.423l-3.232-2.509c-.895.6-2.04.955-3.386.955-2.605 0-4.81-1.76-5.595-4.123H1.064v2.59A9.996 9.996 0 0010 20z" fill="#34A853"/>
                <path d="M4.405 11.9c-.2-.6-.314-1.24-.314-1.9 0-.66.114-1.3.314-1.9V5.51H1.064A9.996 9.996 0 000 10c0 1.614.386 3.14 1.064 4.49l3.34-2.59z" fill="#FBBC05"/>
                <path d="M10 3.977c1.468 0 2.786.505 3.823 1.496l2.868-2.868C14.959.991 12.696 0 10 0 6.09 0 2.71 2.24 1.064 5.51l3.34 2.59C5.19 5.736 7.396 3.977 10 3.977z" fill="#EA4335"/>
              </svg>
              Continue with Google
            </Button>
          </motion.div>

          {/* Login Link */}
          <motion.div className="auth-footer" variants={staggerItem}>
            <p>
              Already have an account?{' '}
              <Link to="/login" className="auth-link-bold">
                Sign in
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
