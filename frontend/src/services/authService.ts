import apiClient from './api';
import { auth, googleProvider } from '../config/firebase';
import {
  signInWithPopup,
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
} from 'firebase/auth';
import {
  User,
  LoginCredentials,
  SignupCredentials,
} from '../types';

/**
 * Authentication API service — Firebase-only
 * Frontend handles Firebase Auth, backend stores profiles and issues JWT
 */
export class AuthAPI {
  /**
   * Sign up with email/password via Firebase
   */
  static async signup(credentials: SignupCredentials): Promise<{ user: User; accessToken: string }> {
    console.log('[AuthAPI] signup called:', { email: credentials.email, username: credentials.username });
    try {
      // 1. Create Firebase auth user
      console.log('[AuthAPI] Creating Firebase auth user...');
      const result = await createUserWithEmailAndPassword(auth, credentials.email, credentials.password);
      console.log('[AuthAPI] Firebase auth user created:', result.user.uid);

      // 2. Get ID token
      const idToken = await result.user.getIdToken();
      console.log('[AuthAPI] ID token obtained');

      // 3. Send to backend to create Firestore profile
      console.log('[AuthAPI] Sending profile data to backend...');
      const { data } = await apiClient.post('/api/auth/signup', {
        idToken,
        username: credentials.username,
        displayName: credentials.displayName || credentials.username,
        role: credentials.role || 'instructor',
      });
      console.log('[AuthAPI] Signup complete:', data.user?.email);
      return data;
    } catch (err: any) {
      console.error('[AuthAPI] Signup error:', err.code, err.message);
      if (err.response) {
        console.error('[AuthAPI] Backend response:', err.response.status, err.response.data);
      }
      throw err;
    }
  }

  /**
   * Login with email/password via Firebase
   */
  static async login(credentials: LoginCredentials): Promise<{ user: User; accessToken: string }> {
    console.log('[AuthAPI] login called:', { email: credentials.email });
    try {
      // 1. Authenticate with Firebase
      console.log('[AuthAPI] Authenticating with Firebase...');
      const result = await signInWithEmailAndPassword(auth, credentials.email, credentials.password);
      console.log('[AuthAPI] Firebase auth successful:', result.user.uid);

      // 2. Get ID token
      const idToken = await result.user.getIdToken();
      console.log('[AuthAPI] ID token obtained');

      // 3. Send to backend for JWT generation
      console.log('[AuthAPI] Sending ID token to backend...');
      const { data } = await apiClient.post('/api/auth/login', { idToken });
      console.log('[AuthAPI] Login complete:', data.user?.email);
      return data;
    } catch (err: any) {
      console.error('[AuthAPI] Login error:', err.code, err.message);
      if (err.response) {
        console.error('[AuthAPI] Backend response:', err.response.status, err.response.data);
      }
      throw err;
    }
  }

  /**
   * Login with Google OAuth
   */
  static async loginWithGoogle(): Promise<{ user: User; accessToken: string }> {
    console.log('[AuthAPI] loginWithGoogle called');
    try {
      // 1. Sign in with Firebase Google provider
      console.log('[AuthAPI] Opening Firebase Google sign-in popup...');
      const result = await signInWithPopup(auth, googleProvider);
      console.log('[AuthAPI] Firebase popup succeeded:', result.user.email);
      
      // 2. Get ID token
      console.log('[AuthAPI] Getting Firebase ID token...');
      const idToken = await result.user.getIdToken();
      console.log('[AuthAPI] ID token obtained, length:', idToken.length);

      // 3. Send to backend for verification and JWT generation
      console.log('[AuthAPI] Sending ID token to backend /api/auth/google...');
      const { data } = await apiClient.post('/api/auth/google', { idToken });
      console.log('[AuthAPI] Backend Google auth succeeded:', data.user?.email);
      return data;
    } catch (err: any) {
      console.error('[AuthAPI] Google login error:', err);
      console.error('[AuthAPI] Error code:', err.code, 'message:', err.message);
      if (err.response) {
        console.error('[AuthAPI] Backend response:', err.response.status, err.response.data);
      }
      throw err;
    }
  }

  /**
   * Refresh access token
   */
  static async refreshToken(): Promise<{ accessToken: string }> {
    const { data } = await apiClient.post('/api/auth/refresh');
    return data;
  }

  /**
   * Logout
   */
  static async logout(): Promise<void> {
    await apiClient.post('/api/auth/logout');
  }

  /**
   * Get current user
   */
  static async getCurrentUser(): Promise<User> {
    const { data } = await apiClient.get('/api/auth/me');
    return data.user;
  }
}
