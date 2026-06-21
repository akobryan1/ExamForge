import { getAuth, getFirestore } from '../config/firebase';
import { User, AuthTokens } from '../types';
import { generateTokens, verifyRefreshToken } from '../utils/jwt';

/**
 * Authentication service - Firebase-only (email/password + Google OAuth)
 */
export class AuthService {
  /**
   * Email/password signup (Firebase handles auth, we store profile)
   */
  async signupWithEmail(data: {
    idToken: string;
    username: string;
    displayName?: string;
    role?: 'instructor' | 'student' | 'admin';
  }): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] signupWithEmail called:', { username: data.username });
    try {
      const auth = getAuth();
      const db = getFirestore();

      console.log('[AuthService] Verifying Firebase ID token...');
      const decodedToken = await auth.verifyIdToken(data.idToken);
      const uid = decodedToken.uid;
      const email = decodedToken.email!;
      console.log('[AuthService] Firebase token verified:', { uid, email });

      const existingDoc = await db.collection('examforge_users').doc(uid).get();
      if (existingDoc.exists) {
        throw new Error('An account with this email already exists');
      }

      const usernameQuery = await db.collection('examforge_users')
        .where('username', '==', data.username).get();
      if (!usernameQuery.empty) {
        await auth.deleteUser(uid);
        throw new Error('Username already taken');
      }

      const role = data.role || 'instructor';
      const displayName = data.displayName || data.username;
      await db.collection('examforge_users').doc(uid).set({
        username: data.username, email, displayName, role,
        createdAt: new Date(), updatedAt: new Date(),
      });

      console.log('[AuthService] Firestore profile created:', uid);

      const user: User = { id: uid, email, username: data.username, displayName, role, createdAt: new Date() };
      const tokens = generateTokens({ userId: user.id, email: user.email, role: user.role });
      console.log('[AuthService] Signup successful');
      return { user, tokens };
    } catch (error) {
      console.error('[AuthService] Signup error:', error);
      throw error;
    }
  }

  /**
   * Email/password login (Firebase handles auth, we return profile)
   */
  async loginWithEmail(idToken: string): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] loginWithEmail called');
    try {
      const auth = getAuth();
      const db = getFirestore();

      console.log('[AuthService] Verifying Firebase ID token...');
      const decodedToken = await auth.verifyIdToken(idToken);
      const uid = decodedToken.uid;
      const email = decodedToken.email!;
      console.log('[AuthService] Firebase token verified:', { uid, email });

      console.log('[AuthService] Looking up Firestore profile...');
      const userDoc = await db.collection('examforge_users').doc(uid).get();
      if (!userDoc.exists) {
        throw new Error('User profile not found. Please sign up first.');
      }

      const profile = userDoc.data()!;
      console.log('[AuthService] Profile found:', profile.username);

      const user: User = {
        id: uid, email, username: profile.username,
        displayName: profile.displayName || profile.username,
        role: profile.role || 'instructor',
        createdAt: profile.createdAt?.toDate() || new Date(),
      };

      const tokens = generateTokens({ userId: user.id, email: user.email, role: user.role });
      console.log('[AuthService] Login successful');
      return { user, tokens };
    } catch (error) {
      console.error('[AuthService] Login error:', error);
      throw error;
    }
  }

  /**
   * Google OAuth login (Firebase handles auth, we return/create profile)
   */
  async loginWithGoogle(idToken: string): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] loginWithGoogle called');
    try {
      const auth = getAuth();
      const db = getFirestore();

      console.log('[AuthService] Verifying Google ID token...');
      const decodedToken = await auth.verifyIdToken(idToken);
      const uid = decodedToken.uid;
      const email = decodedToken.email!;
      console.log('[AuthService] Google token verified:', { uid, email });

      const userDoc = await db.collection('examforge_users').doc(uid).get();
      let profile;

      if (userDoc.exists) {
        profile = userDoc.data()!;
        console.log('[AuthService] Existing profile found');
      } else {
        profile = {
          username: email.split('@')[0], email,
          displayName: decodedToken.name || email,
          role: 'instructor', createdAt: new Date(), updatedAt: new Date(),
        };
        await db.collection('examforge_users').doc(uid).set(profile);
        console.log('[AuthService] New profile created for Google user');
      }

      const user: User = {
        id: uid, email, username: profile.username,
        displayName: profile.displayName || decodedToken.name,
        role: profile.role || 'instructor',
        createdAt: profile.createdAt?.toDate() || new Date(),
      };

      const tokens = generateTokens({ userId: user.id, email: user.email, role: user.role });
      console.log('[AuthService] Google login successful');
      return { user, tokens };
    } catch (error) {
      console.error('[AuthService] Google auth error:', error);
      throw new Error('Google authentication failed');
    }
  }

  /**
   * Refresh access token (JWT-based)
   */
  async refreshAccessToken(refreshToken: string): Promise<AuthTokens> {
    console.log('[AuthService] refreshAccessToken called');
    try {
      const decoded = verifyRefreshToken(refreshToken);
      const user = await this.getUserById(decoded.userId);
      if (!user) throw new Error('User not found');

      const tokens = generateTokens({ userId: decoded.userId, email: decoded.email, role: user.role });
      console.log('[AuthService] Token refreshed for:', decoded.userId);
      return tokens;
    } catch (error) {
      console.error('[AuthService] Token refresh error:', error);
      throw new Error('Invalid or expired refresh token');
    }
  }

  /**
   * Logout (Firebase sessions are client-side)
   */
  async logout(userId: string): Promise<void> {
    console.log('[AuthService] logout:', userId);
  }

  /**
   * Get user profile from Firestore
   */
  async getUserById(userId: string): Promise<User | null> {
    console.log('[AuthService] getUserById:', userId);
    try {
      const db = getFirestore();
      const userDoc = await db.collection('examforge_users').doc(userId).get();
      if (!userDoc.exists) {
        console.log('[AuthService] No profile found for:', userId);
        return null;
      }
      const data = userDoc.data()!;
      return {
        id: userDoc.id, email: data.email,
        username: data.username,
        displayName: data.displayName || data.username,
        role: data.role || 'instructor',
        createdAt: data.createdAt?.toDate() || new Date(),
      };
    } catch (error) {
      console.error('[AuthService] getUserById error:', error);
      return null;
    }
  }
}
