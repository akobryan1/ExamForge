import apiClient from './api';
import { supabase } from '../config/supabase';
import { auth, googleProvider } from '../config/firebase';
import { signInWithPopup } from 'firebase/auth';
import {
  User,
  LoginCredentials,
  SignupCredentials,
} from '../types';

/**
 * Authentication API service
 */
export class AuthAPI {
  /**
   * Sign up with email/password
   */
  static async signup(credentials: SignupCredentials): Promise<{ user: User; accessToken: string }> {
    console.log('[AuthAPI] signup called:', { email: credentials.email, username: credentials.username });
    const { data } = await apiClient.post('/api/auth/signup', credentials);
    console.log('[AuthAPI] signup response received');
    return data;
  }

  /**
   * Login with email/password
   */
  static async login(credentials: LoginCredentials): Promise<{ user: User; accessToken: string }> {
    console.log('[AuthAPI] login called, sending POST to /api/auth/login');
    try {
      const { data } = await apiClient.post('/api/auth/login', credentials);
      console.log('[AuthAPI] login response received:', { email: data.user?.email, hasToken: !!data.accessToken });
      return data;
    } catch (err: any) {
      console.error('[AuthAPI] login HTTP error:', err.message, err.response?.status, err.response?.data);
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

  /**
   * Check username availability
   */
  static async checkUsernameAvailability(username: string): Promise<boolean> {
    try {
      const { data } = await supabase
        .from('examforge_users')
        .select('username')
        .eq('username', username)
        .single();

      return !data; // Available if no data found
    } catch {
      return true; // Assume available on error
    }
  }
}
