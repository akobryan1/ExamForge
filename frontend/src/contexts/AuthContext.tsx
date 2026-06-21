import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { User, LoginCredentials, SignupCredentials } from '../types';
import { AuthAPI } from '../services/authService';

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
  login: (credentials: LoginCredentials) => Promise<void>;
  signup: (credentials: SignupCredentials) => Promise<void>;
  loginWithGoogle: () => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  /**
   * Initialize auth state from localStorage
   */
  useEffect(() => {
    const initAuth = async () => {
      try {
        const storedUser = localStorage.getItem('user');
        const accessToken = localStorage.getItem('accessToken');

        console.log('[AuthContext] initAuth started, storedUser:', !!storedUser, 'accessToken:', !!accessToken);

        if (storedUser && accessToken) {
          // Verify token is still valid by fetching current user
          console.log('[AuthContext] Verifying existing token with getCurrentUser...');
          const currentUser = await AuthAPI.getCurrentUser();
          console.log('[AuthContext] Token valid, user restored:', currentUser.email);
          setUser(currentUser);
        } else {
          console.log('[AuthContext] No stored credentials found');
        }
      } catch (err) {
        // Token invalid or expired - clear storage
        console.log('[AuthContext] Token validation failed, clearing storage');
        localStorage.removeItem('user');
        localStorage.removeItem('accessToken');
      } finally {
        setIsLoading(false);
        console.log('[AuthContext] initAuth complete, isLoading:', false);
      }
    };

    initAuth();
  }, []);

  /**
   * Login with email/password
   */
  const login = async (credentials: LoginCredentials) => {
    console.log('[AuthContext] login called:', { email: credentials.email });
    try {
      setIsLoading(true);
      setError(null);

      console.log('[AuthContext] Calling AuthAPI.login...');
      const { user: loggedInUser, accessToken } = await AuthAPI.login(credentials);
      console.log('[AuthContext] Login API success:', loggedInUser.email, 'role:', loggedInUser.role);

      // Store in localStorage
      localStorage.setItem('user', JSON.stringify(loggedInUser));
      localStorage.setItem('accessToken', accessToken);
      console.log('[AuthContext] Credentials stored in localStorage');

      setUser(loggedInUser);
    } catch (err: any) {
      // Firebase errors have err.code, backend errors have err.response.data
      const data = err.response?.data;
      const firebaseCode = err.code?.replace('auth/', '');
      let errorMessage: string;
      if (data?.message) errorMessage = data.message;
      else if (firebaseCode === 'invalid-credential') errorMessage = 'Invalid email or password';
      else if (firebaseCode === 'user-not-found') errorMessage = 'No account found with this email';
      else if (firebaseCode === 'too-many-requests') errorMessage = 'Too many attempts. Try again later.';
      else if (firebaseCode) errorMessage = firebaseCode.replace(/-/g, ' ');
      else errorMessage = 'Login failed';
      
      console.error('[AuthContext] Login failed:', errorMessage, err);
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  /**
   * Sign up with email/password
   */
  const signup = async (credentials: SignupCredentials) => {
    console.log('[AuthContext] signup called:', { email: credentials.email, username: credentials.username });
    try {
      setIsLoading(true);
      setError(null);

      console.log('[AuthContext] Calling AuthAPI.signup...');
      const { user: newUser, accessToken } = await AuthAPI.signup(credentials);
      console.log('[AuthContext] Signup API success:', newUser.email);

      // Store in localStorage
      localStorage.setItem('user', JSON.stringify(newUser));
      localStorage.setItem('accessToken', accessToken);

      setUser(newUser);
    } catch (err: any) {
      const data = err.response?.data;
      const firebaseCode = err.code?.replace('auth/', '');
      let errorMessage: string;
      if (data?.message) errorMessage = data.message;
      else if (firebaseCode === 'email-already-in-use') errorMessage = 'An account with this email already exists';
      else if (firebaseCode === 'weak-password') errorMessage = 'Password must be at least 6 characters';
      else if (firebaseCode === 'invalid-email') errorMessage = 'Invalid email address';
      else if (firebaseCode) errorMessage = firebaseCode.replace(/-/g, ' ');
      else errorMessage = 'Signup failed';
      
      console.error('[AuthContext] Signup failed:', errorMessage, err);
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  /**
   * Login with Google OAuth
   */
  const loginWithGoogle = async () => {
    console.log('[AuthContext] loginWithGoogle called');
    try {
      setIsLoading(true);
      setError(null);

      console.log('[AuthContext] Calling AuthAPI.loginWithGoogle...');
      const { user: loggedInUser, accessToken } = await AuthAPI.loginWithGoogle();
      console.log('[AuthContext] Google login API success:', loggedInUser.email, 'role:', loggedInUser.role);

      // Store in localStorage
      localStorage.setItem('user', JSON.stringify(loggedInUser));
      localStorage.setItem('accessToken', accessToken);

      setUser(loggedInUser);
    } catch (err: any) {
      const errorMessage = err.response?.data?.message || err.message || 'Google login failed';
      console.error('[AuthContext] Google login ERROR:', errorMessage, err);
      setError(errorMessage);
      throw new Error(errorMessage);
    } finally {
      setIsLoading(false);
    }
  };

  /**
   * Logout
   */
  const logout = async () => {
    try {
      await AuthAPI.logout();
    } catch (err) {
      console.error('Logout error:', err);
    } finally {
      // Clear state and storage regardless of API result
      setUser(null);
      localStorage.removeItem('user');
      localStorage.removeItem('accessToken');
    }
  };

  /**
   * Clear error message
   */
  const clearError = () => {
    setError(null);
  };

  const value: AuthContextType = {
    user,
    isAuthenticated: !!user,
    isLoading,
    error,
    login,
    signup,
    loginWithGoogle,
    logout,
    clearError,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/**
 * useAuth hook - access auth context
 */
export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
