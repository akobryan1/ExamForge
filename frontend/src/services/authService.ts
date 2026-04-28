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
    const { data } = await apiClient.post('/api/auth/signup', credentials);
    return data;
  }

  /**
   * Login with email/password
   */
  static async login(credentials: LoginCredentials): Promise<{ user: User; accessToken: string }> {
    const { data } = await apiClient.post('/api/auth/login', credentials);
    return data;
  }

  /**
   * Login with Google OAuth
   */
  static async loginWithGoogle(): Promise<{ user: User; accessToken: string }> {
    // 1. Sign in with Firebase Google provider
    const result = await signInWithPopup(auth, googleProvider);
    
    // 2. Get ID token
    const idToken = await result.user.getIdToken();

    // 3. Send to backend for verification and JWT generation
    const { data } = await apiClient.post('/api/auth/google', { idToken });
    return data;
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
