import { getSupabase, getSupabaseAdmin } from '../config/supabase';
import { getAuth } from '../config/firebase';
import { User, JwtPayload, AuthTokens, LoginRequest, SignupRequest } from '../types';
import { generateTokens } from '../utils/jwt';
import bcrypt from 'bcrypt';

const SALT_ROUNDS = 10;

/**
 * Authentication service handling multiple auth providers
 */
export class AuthService {
  /**
   * Supabase email/password signup
   */
  async signupWithEmail(data: SignupRequest): Promise<{ user: User; tokens: AuthTokens }> {
    try {
      const supabase = getSupabase();

      // 1. Create auth user in Supabase
      const { data: authData, error: signUpError } = await supabase.auth.signUp({
        email: data.email,
        password: data.password,
        options: {
          data: {
            username: data.username,
            display_name: data.displayName || data.username,
          },
        },
      });

      if (signUpError || !authData.user) {
        throw new Error(signUpError?.message || 'Signup failed');
      }

      // 2. Create user profile in examforge_users table
      const { error: profileError } = await supabase
        .from('examforge_users')
        .insert({
          userid: authData.user.id,
          username: data.username,
          email: data.email,
          created_at: new Date().toISOString(),
        });

      if (profileError) {
        // If profile creation fails, clean up auth user
        await supabase.auth.admin.deleteUser(authData.user.id);
        throw new Error('Failed to create user profile: ' + profileError.message);
      }

      // 3. Create user object
      const user: User = {
        id: authData.user.id,
        email: data.email,
        username: data.username,
        displayName: data.displayName || data.username,
        role: 'instructor', // Default role
        createdAt: new Date(),
      };

      // 4. Generate JWT tokens
      const tokens = generateTokens({
        userId: user.id,
        email: user.email,
        role: user.role,
      });

      return { user, tokens };
    } catch (error) {
      console.error('Signup error:', error);
      throw error;
    }
  }

  /**
   * Supabase email/password login
   */
  async loginWithEmail(data: LoginRequest): Promise<{ user: User; tokens: AuthTokens }> {
    try {
      const supabase = getSupabase();

      // 1. Authenticate with Supabase
      const { data: authData, error: signInError } = await supabase.auth.signInWithPassword({
        email: data.email,
        password: data.password,
      });

      if (signInError || !authData.user) {
        throw new Error('Invalid email or password');
      }

      // 2. Get user profile
      const { data: profile, error: profileError } = await supabase
        .from('examforge_users')
        .select('*')
        .eq('userid', authData.user.id)
        .single();

      if (profileError) {
        throw new Error('User profile not found');
      }

      // 3. Create user object
      const user: User = {
        id: authData.user.id,
        email: authData.user.email || data.email,
        username: profile.username,
        displayName: authData.user.user_metadata?.display_name || profile.username,
        role: 'instructor', // Could be stored in profile
        createdAt: new Date(profile.created_at),
      };

      // 4. Generate JWT tokens
      const tokens = generateTokens({
        userId: user.id,
        email: user.email,
        role: user.role,
      });

      return { user, tokens };
    } catch (error) {
      console.error('Login error:', error);
      throw error;
    }
  }

  /**
   * Firebase Google OAuth authentication
   */
  async loginWithGoogle(idToken: string): Promise<{ user: User; tokens: AuthTokens }> {
    try {
      const auth = getAuth();

      // 1. Verify Google ID token
      const decodedToken = await auth.verifyIdToken(idToken);
      
      // 2. Get or create user
      let firebaseUser;
      try {
        firebaseUser = await auth.getUser(decodedToken.uid);
      } catch {
        // User doesn't exist, create new user
        firebaseUser = await auth.createUser({
          uid: decodedToken.uid,
          email: decodedToken.email,
          displayName: decodedToken.name,
          photoURL: decodedToken.picture,
        });
      }

      // 3. Create user object
      const user: User = {
        id: firebaseUser.uid,
        email: firebaseUser.email || decodedToken.email!,
        displayName: firebaseUser.displayName || decodedToken.name,
        role: 'instructor',
        createdAt: new Date(firebaseUser.metadata.creationTime),
      };

      // 4. Generate JWT tokens
      const tokens = generateTokens({
        userId: user.id,
        email: user.email,
        role: user.role,
      });

      return { user, tokens };
    } catch (error) {
      console.error('Google auth error:', error);
      throw new Error('Google authentication failed');
    }
  }

  /**
   * Refresh access token using refresh token
   */
  async refreshAccessToken(refreshToken: string): Promise<AuthTokens> {
    try {
      const supabase = getSupabase();

      // Refresh session with Supabase
      const { data, error } = await supabase.auth.refreshSession({
        refresh_token: refreshToken,
      });

      if (error || !data.user) {
        throw new Error('Invalid refresh token');
      }

      // Generate new JWT tokens
      const tokens = generateTokens({
        userId: data.user.id,
        email: data.user.email!,
        role: 'instructor', // Should be fetched from profile
      });

      return tokens;
    } catch (error) {
      console.error('Token refresh error:', error);
      throw error;
    }
  }

  /**
   * Logout user
   */
  async logout(userId: string): Promise<void> {
    try {
      const supabase = getSupabase();
      await supabase.auth.signOut();
    } catch (error) {
      console.error('Logout error:', error);
      // Don't throw - logout should always succeed client-side
    }
  }

  /**
   * Get user by ID
   */
  async getUserById(userId: string): Promise<User | null> {
    try {
      const supabase = getSupabase();

      const { data: profile, error } = await supabase
        .from('examforge_users')
        .select('*')
        .eq('userid', userId)
        .single();

      if (error || !profile) {
        return null;
      }

      return {
        id: profile.userid,
        email: profile.email,
        username: profile.username,
        displayName: profile.username,
        role: 'instructor',
        createdAt: new Date(profile.created_at),
      };
    } catch (error) {
      console.error('Get user error:', error);
      return null;
    }
  }
}
