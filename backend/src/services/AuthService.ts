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
    console.log('[AuthService] signupWithEmail called:', { email: data.email, username: data.username });
    try {
      const supabase = getSupabase();
      console.log('[AuthService] Supabase client obtained');

      // 1. Create auth user in Supabase
      console.log('[AuthService] Attempting Supabase auth.signUp...');
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

      if (signUpError) {
        console.error('[AuthService] Supabase signUp error:', signUpError.message);
      }
      if (!authData?.user) {
        console.error('[AuthService] No user returned from Supabase signUp');
      }

      if (signUpError || !authData.user) {
        throw new Error(signUpError?.message || 'Signup failed');
      }

      console.log('[AuthService] Supabase auth user created:', { id: authData.user.id, email: authData.user.email });

      // 2. Create user profile in examforge_users table
      console.log('[AuthService] Creating user profile in examforge_users table...');
      const { error: profileError } = await supabase
        .from('examforge_users')
        .insert({
          userid: authData.user.id,
          username: data.username,
          email: data.email,
          created_at: new Date().toISOString(),
        });

      if (profileError) {
        console.error('[AuthService] Profile creation failed:', profileError.message);
        // If profile creation fails, clean up auth user
        await supabase.auth.admin.deleteUser(authData.user.id);
        throw new Error('Failed to create user profile: ' + profileError.message);
      }

      console.log('[AuthService] User profile created successfully');

      // 3. Create user object
      const user: User = {
        id: authData.user.id,
        email: data.email,
        username: data.username,
        displayName: data.displayName || data.username,
        role: data.role || 'instructor',
        createdAt: new Date(),
      };

      // 4. Generate JWT tokens
      const tokens = generateTokens({
        userId: user.id,
        email: user.email,
        role: user.role,
      });

      console.log('[AuthService] Signup successful, tokens generated');
      return { user, tokens };
    } catch (error) {
      console.error('[AuthService] Signup error:', error);
      throw error;
    }
  }

  /**
   * Supabase email/password login
   */
  async loginWithEmail(data: LoginRequest): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] loginWithEmail called:', { email: data.email });
    try {
      const supabase = getSupabase();
      console.log('[AuthService] Supabase client obtained');

      // 1. Authenticate with Supabase
      console.log('[AuthService] Attempting Supabase auth.signInWithPassword...');
      const { data: authData, error: signInError } = await supabase.auth.signInWithPassword({
        email: data.email,
        password: data.password,
      });

      if (signInError) {
        console.error('[AuthService] Supabase signIn error:', signInError.message, '(code:', signInError.code, ')');
      }
      if (!authData?.user) {
        console.error('[AuthService] No user returned from Supabase signIn');
      }

      if (signInError || !authData.user) {
        // Use the actual Supabase error message instead of a generic one
        const message = signInError?.message || 'Invalid email or password';
        throw new Error(message);
      }

      console.log('[AuthService] Supabase auth successful:', { id: authData.user.id, email: authData.user.email });
      console.log('[AuthService] User metadata:', JSON.stringify(authData.user.user_metadata, null, 2));

      // 2. Get user profile
      console.log('[AuthService] Fetching user profile from examforge_users...');
      const { data: profile, error: profileError } = await supabase
        .from('examforge_users')
        .select('*')
        .eq('userid', authData.user.id)
        .single();

      if (profileError) {
        console.error('[AuthService] Profile fetch error:', profileError.message);
        throw new Error('User profile not found');
      }

      console.log('[AuthService] User profile fetched:', JSON.stringify(profile, null, 2));

      // 3. Create user object
      const user: User = {
        id: authData.user.id,
        email: authData.user.email || data.email,
        username: profile.username,
        displayName: authData.user.user_metadata?.display_name || profile.username,
        role: profile.role || 'instructor',
        createdAt: new Date(profile.created_at),
      };

      console.log('[AuthService] User object created:', JSON.stringify(user, null, 2));

      // 4. Generate JWT tokens
      const tokens = generateTokens({
        userId: user.id,
        email: user.email,
        role: user.role,
      });

      console.log('[AuthService] Login successful, tokens generated');
      return { user, tokens };
    } catch (error) {
      console.error('[AuthService] Login error:', error);
      throw error;
    }
  }

  /**
   * Firebase Google OAuth authentication
   */
  async loginWithGoogle(idToken: string): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] loginWithGoogle called');
    try {
      const auth = getAuth();
      console.log('[AuthService] Firebase Auth obtained');

      // 1. Verify Google ID token
      console.log('[AuthService] Verifying Google ID token...');
      const decodedToken = await auth.verifyIdToken(idToken);
      console.log('[AuthService] Google token verified:', { uid: decodedToken.uid, email: decodedToken.email });
      
      // 2. Get or create user
      let firebaseUser;
      try {
        firebaseUser = await auth.getUser(decodedToken.uid);
        console.log('[AuthService] Existing Firebase user found');
      } catch {
        console.log('[AuthService] Creating new Firebase user...');
        firebaseUser = await auth.createUser({
          uid: decodedToken.uid,
          email: decodedToken.email,
          displayName: decodedToken.name,
          photoURL: decodedToken.picture,
        });
        console.log('[AuthService] New Firebase user created:', firebaseUser.uid);
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

      console.log('[AuthService] Google login successful');
      return { user, tokens };
    } catch (error) {
      console.error('[AuthService] Google auth error:', error);
      throw new Error('Google authentication failed');
    }
  }

  /**
   * Refresh access token using refresh token
   */
  async refreshAccessToken(refreshToken: string): Promise<AuthTokens> {
    console.log('[AuthService] refreshAccessToken called');
    try {
      const supabase = getSupabase();
      console.log('[AuthService] Refreshing Supabase session...');

      // Refresh session with Supabase
      const { data, error } = await supabase.auth.refreshSession({
        refresh_token: refreshToken,
      });

      if (error) {
        console.error('[AuthService] Supabase refresh error:', error.message);
      }
      if (!data?.user) {
        console.error('[AuthService] No user from Supabase refresh');
      }

      if (error || !data.user) {
        throw new Error('Invalid refresh token');
      }

      console.log('[AuthService] Session refreshed for user:', data.user.id);

      // Generate new JWT tokens
      const tokens = generateTokens({
        userId: data.user.id,
        email: data.user.email!,
        role: 'instructor', // Should be fetched from profile
      });

      return tokens;
    } catch (error) {
      console.error('[AuthService] Token refresh error:', error);
      throw error;
    }
  }

  /**
   * Logout user
   */
  async logout(userId: string): Promise<void> {
    console.log('[AuthService] logout called for user:', userId);
    try {
      const supabase = getSupabase();
      await supabase.auth.signOut();
      console.log('[AuthService] Logout successful');
    } catch (error) {
      console.error('[AuthService] Logout error:', error);
      // Don't throw - logout should always succeed client-side
    }
  }

  /**
   * Get user by ID
   */
  async getUserById(userId: string): Promise<User | null> {
    console.log('[AuthService] getUserById called:', userId);
    try {
      const supabase = getSupabase();

      const { data: profile, error } = await supabase
        .from('examforge_users')
        .select('*')
        .eq('userid', userId)
        .single();

      if (error) {
        console.error('[AuthService] getUserById error:', error.message);
      }
      if (!profile) {
        console.log('[AuthService] No profile found for user:', userId);
        return null;
      }

      console.log('[AuthService] Profile found:', profile.username);

      return {
        id: profile.userid,
        email: profile.email,
        username: profile.username,
        displayName: profile.username,
        role: profile.role || 'instructor',
        createdAt: new Date(profile.created_at),
      };
    } catch (error) {
      console.error('[AuthService] Get user error:', error);
      return null;
    }
  }
}
