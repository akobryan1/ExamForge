/**
 * TypeScript type definitions for backend
 */

export interface User {
  id: string;
  email: string;
  username?: string;
  displayName?: string;
  role: 'instructor' | 'student' | 'admin';
  createdAt: Date;
}

export interface JwtPayload {
  userId: string;
  email: string;
  role: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface SignupRequest {
  email: string;
  password: string;
  username: string;
  displayName?: string;
}

export interface GoogleAuthRequest {
  idToken: string;
}

// Export exam types
export * from './exam';
