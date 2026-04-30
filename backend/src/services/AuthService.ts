import { getAuth, getFirestore } from '../config/firebase';
import { User, JwtPayload, AuthTokens, LoginRequest, SignupRequest } from '../types';
import { generateTokens } from '../utils/jwt';
import bcrypt from 'bcrypt';

const SALT_ROUNDS = 10;

/**
 * Authentication service using Firebase/Firestore
 */
export class AuthService {
  /**
   * Firebase email/password signup
   */
  async signupWithEmail(data: SignupRequest): Promise<{ user: User; tokens: AuthTokens }> {
    try {
      const auth = getAuth();
      const db = getFirestore();

      // 1. Check if user already exists
      const usersRef = db.collection('examforge_users');
      const emailQuery = await usersRef.where('email', '==', data.email).get();
      
      if (!emailQuery.empty) {
        throw new Error('Email already registered');
      }

      const usernameQuery = await usersRef.where('username', '==', data.username).get();
      if (!usernameQuery.empty) {
        throw new Error('Username already taken');
      }

      // 2. Hash password
      const passwordHash = await bcrypt.hash(data.password, SALT_ROUNDS);

      // 3. Create user in Firebase Auth
      const userRecord = await auth.createUser({
        email: data.email,
        password: data.password,
        displayName: data.displayName || data.username,
      });

      // 4. Create user profile in Firestore
      const role = data.role || 'instructor';
      await db.collection('examforge_users').doc(userRecord.uid).set({
        username: data.username,
        email: data.email,
        displayName: data.displayName || data.username,
        role: role,
        passwordHash: passwordHash,
        createdAt: new Date(),
        updatedAt: new Date(),
      });

      // 5. Create user object
      const user: User = {
        id: userRecord.uid,
        email: data.email,
        username: data.username,
        displayName: data.displayName || data.username,
        role: role,
        createdAt: new Date(),
      };

      // 6. Generate JWT tokens
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
   * Firebase email/password login
   */
  async loginWithEmail(data: LoginRequest): Promise<{ user: User; tokens: AuthTokens }> {
    try {
      const db = getFirestore();

      // 1. Find user by email
      const usersRef = db.collection('examforge_users');
      const snapshot = await usersRef.where('email', '==', data.email).get();

      if (snapshot.empty) {
        throw new Error('Invalid email or password');
      }

      const userDoc = snapshot.docs[0];
      const userData = userDoc.data();

      // 2. Verify password
      const passwordMatch = await bcrypt.compare(data.password, userData.passwordHash);
      if (!passwordMatch) {
        throw new Error('Invalid email or password');
      }

      // 3. Create user object
      const user: User = {
        id: userDoc.id,
        email: userData.email,
        username: userData.username,
        displayName: userData.displayName || userData.username,
        role: userData.role || 'instructor',
        createdAt: userData.createdAt?.toDate() || new Date(),
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
      const db = getFirestore();

      // 1. Verify Google ID token
      const decodedToken = await auth.verifyIdToken(idToken);
      
      // 2. Get or create Firebase user
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

      // 3. Get or create Firestore user profile
      const userDoc = await db.collection('examforge_users').doc(firebaseUser.uid).get();
      
      if (!userDoc.exists) {
        // Create new user profile
        await db.collection('examforge_users').doc(firebaseUser.uid).set({
          email: firebaseUser.email,
          displayName: firebaseUser.displayName,
          username: firebaseUser.email?.split('@')[0] || firebaseUser.uid,
          role: 'instructor',
          createdAt: new Date(),
          updatedAt: new Date(),
        });
      }

      const userData = userDoc.exists ? userDoc.data() : {
        email: firebaseUser.email,
        displayName: firebaseUser.displayName,
        username: firebaseUser.email?.split('@')[0],
        role: 'instructor',
      };

      // 4. Create user object
      const user: User = {
        id: firebaseUser.uid,
        email: firebaseUser.email || decodedToken.email!,
        username: userData?.username,
        displayName: firebaseUser.displayName || decodedToken.name,
        role: userData?.role || 'instructor',
        createdAt: new Date(firebaseUser.metadata.creationTime),
      };

      // 5. Generate JWT tokens
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
   * Refresh access token using refresh token (JWT-based)
   */
  async refreshAccessToken(refreshToken: string): Promise<AuthTokens> {
    try {
      const db = getFirestore();
      
      // For JWT-based refresh, you would verify the refresh token
      // and generate new tokens. This is a simplified version.
      // In production, you should verify the refresh token signature
      
      throw new Error('Refresh token not implemented - please login again');
    } catch (error) {
      console.error('Token refresh error:', error);
      throw error;
    }
  }

  /**
   * Logout user (Firebase)
   */
  async logout(userId: string): Promise<void> {
    try {
      // With JWT tokens, logout is handled client-side by removing tokens
      // No server-side action needed for Firebase Auth
      console.log(`User ${userId} logged out`);
    } catch (error) {
      console.error('Logout error:', error);
      // Don't throw - logout should always succeed client-side
    }
  }

  /**
   * Get user by ID from Firestore
   */
  async getUserById(userId: string): Promise<User | null> {
    try {
      const db = getFirestore();

      const userDoc = await db.collection('examforge_users').doc(userId).get();

      if (!userDoc.exists) {
        return null;
      }

      const userData = userDoc.data()!;

      return {
        id: userDoc.id,
        email: userData.email,
        username: userData.username,
        displayName: userData.displayName || userData.username,
        role: userData.role || 'instructor',
        createdAt: userData.createdAt?.toDate() || new Date(),
      };
    } catch (error) {
      console.error('Get user error:', error);
      return null;
    }
  }
}
