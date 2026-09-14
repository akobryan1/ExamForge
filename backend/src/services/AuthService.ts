import { getAuth, getFirestore } from '../config/firebase';
import { User, AuthTokens } from '../types';
import { generateTokens, verifyRefreshToken } from '../utils/jwt';

/**
 * Error carrying a stable, client-safe code.
 * Full detail is always logged server-side; only `code`, `message` and `status` reach the client.
 */
export class AuthError extends Error {
  readonly code: string;
  readonly status: number;

  constructor(code: string, message: string, status = 401) {
    super(message);
    this.name = 'AuthError';
    this.code = code;
    this.status = status;
  }
}

/**
 * Normalize a `createdAt` value into a JS Date.
 * Firestore Timestamps expose `.toDate()`, while locally constructed values are plain `Date`s.
 */
function toDate(value: unknown): Date {
  if (value instanceof Date) return value;
  if (value && typeof (value as { toDate?: unknown }).toDate === 'function') {
    return (value as { toDate: () => Date }).toDate();
  }
  return new Date();
}

/** Log the raw dependency failure and return a client-safe AuthError. */
function backendUnavailable(context: string, error: unknown): AuthError {
  console.error(`[AuthService] ${context}:`, error);
  return new AuthError(
    'AUTH_BACKEND_UNAVAILABLE',
    'Authentication service is temporarily unavailable. Please try again shortly.',
    503
  );
}

/** Get the Firebase Auth client, converting init failures into a client-safe AuthError. */
function getAuthOrThrow(context: string) {
  try {
    return getAuth();
  } catch (error) {
    throw backendUnavailable(`${context} auth init failed`, error);
  }
}

/** Verify a Firebase ID token, converting verification failures into a client-safe AuthError. */
async function verifyIdTokenOrThrow(idToken: string, context: string) {
  const auth = getAuthOrThrow(context);
  try {
    return await auth.verifyIdToken(idToken);
  } catch (error) {
    console.error(`[AuthService] ${context} token verification failed:`, error);
    throw new AuthError('AUTH_TOKEN_INVALID', 'Sign-in token is invalid or expired. Please try again.');
  }
}

/**
 * Turn a pending admin invite into a profile for `uid`.
 * Returns the created profile, or null when there is no usable invite.
 */
async function consumeInvite(
  email: string,
  uid: string,
  displayName?: string
): Promise<Record<string, any> | null> {
  const db = getFirestore();
  const normalizedEmail = email.trim().toLowerCase();
  const inviteRef = db.collection('examforge_invites').doc(normalizedEmail);
  const inviteDoc = await inviteRef.get();
  if (!inviteDoc.exists) return null;

  const invite = inviteDoc.data()!;
  if (invite.status !== 'pending') return null;

  const profile = {
    username: invite.username || normalizedEmail.split('@')[0],
    email: normalizedEmail,
    displayName: displayName || invite.displayName || invite.username || normalizedEmail,
    role: invite.role || 'instructor',
    createdAt: new Date(),
    updatedAt: new Date(),
  };

  await db.collection('examforge_users').doc(uid).set(profile);
  await inviteRef.update({ status: 'accepted', acceptedBy: uid, acceptedAt: new Date() });
  return profile;
}

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

      const decodedToken = await verifyIdTokenOrThrow(data.idToken, 'Signup');
      const uid = decodedToken.uid;
      const email = decodedToken.email;
      if (!email) {
        throw new AuthError('AUTH_EMAIL_MISSING', 'Your account did not provide an email address.', 400);
      }
      console.log('[AuthService] Firebase token verified:', { uid, email });

      const existingDoc = await db.collection('examforge_users').doc(uid).get();
      if (existingDoc.exists) {
        throw new AuthError('AUTH_EMAIL_IN_USE', 'An account with this email already exists', 409);
      }

      const usernameQuery = await db.collection('examforge_users')
        .where('username', '==', data.username).get();
      if (!usernameQuery.empty) {
        await auth.deleteUser(uid);
        throw new AuthError('AUTH_USERNAME_TAKEN', 'Username already taken', 409);
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
      if (error instanceof AuthError) throw error;
      throw backendUnavailable('Signup profile operation failed', error);
    }
  }

  /**
   * Email/password login (Firebase handles auth, we return profile)
   */
  async loginWithEmail(idToken: string): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] loginWithEmail called');

    const decodedToken = await verifyIdTokenOrThrow(idToken, 'Login');
    const uid = decodedToken.uid;
    const email = decodedToken.email;
    if (!email) {
      throw new AuthError('AUTH_EMAIL_MISSING', 'Your account did not provide an email address.', 400);
    }
    console.log('[AuthService] Firebase token verified:', { uid, email });

    let profile: Record<string, any>;
    try {
      console.log('[AuthService] Looking up Firestore profile...');
      const db = getFirestore();
      const userDoc = await db.collection('examforge_users').doc(uid).get();
      if (!userDoc.exists) {
        throw new AuthError('AUTH_PROFILE_NOT_FOUND', 'User profile not found. Please sign up first.', 404);
      }

      profile = userDoc.data()!;
      console.log('[AuthService] Profile found:', profile.username);
    } catch (error) {
      if (error instanceof AuthError) throw error;
      throw backendUnavailable('Login profile lookup failed', error);
    }

    const user: User = {
      id: uid, email, username: profile.username,
      displayName: profile.displayName || profile.username,
      role: profile.role || 'instructor',
      createdAt: toDate(profile.createdAt),
    };

    const tokens = generateTokens({ userId: user.id, email: user.email, role: user.role });
    console.log('[AuthService] Login successful');
    return { user, tokens };
  }

  /**
   * Google OAuth login (Firebase handles auth, we return/create profile)
   */
  async loginWithGoogle(idToken: string): Promise<{ user: User; tokens: AuthTokens }> {
    console.log('[AuthService] loginWithGoogle called');

    const decodedToken = await verifyIdTokenOrThrow(idToken, 'Google');
    const uid = decodedToken.uid;
    const email = decodedToken.email;
    if (!email) {
      throw new AuthError('AUTH_EMAIL_MISSING', 'Your Google account did not provide an email address.', 400);
    }
    console.log('[AuthService] Google token verified:', { uid, email });

    let profile: Record<string, any>;
    try {
      const db = getFirestore();
      const userDoc = await db.collection('examforge_users').doc(uid).get();

      if (userDoc.exists) {
        profile = userDoc.data()!;
        console.log('[AuthService] Existing profile found');
      } else {
        // Fail closed: Google sign-in never self-provisions access. Only a pending
        // admin invite can create a profile for a new Google account.
        const invited = await consumeInvite(email, uid, decodedToken.name);
        if (!invited) {
          console.log('[AuthService] No profile or invite for Google user:', email);
          throw new AuthError(
            'AUTH_GOOGLE_NOT_REGISTERED',
            'No ExamForge account is linked to this Google account. Please sign up first, or ask an administrator to invite you.',
            403
          );
        }
        profile = invited;
        console.log('[AuthService] Profile created from invite for Google user');
      }
    } catch (error) {
      if (error instanceof AuthError) throw error;
      throw backendUnavailable('Google profile lookup/create failed', error);
    }

    // `profile.createdAt` is a Firestore Timestamp for existing profiles but a plain
    // JS Date for freshly created ones — toDate() accepts both.
    const user: User = {
      id: uid, email, username: profile.username,
      displayName: profile.displayName || decodedToken.name,
      role: profile.role || 'instructor',
      createdAt: toDate(profile.createdAt),
    };

    const tokens = generateTokens({ userId: user.id, email: user.email, role: user.role });
    console.log('[AuthService] Google login successful');
    return { user, tokens };
  }

  /**
   * Refresh access token (JWT-based)
   */
  async refreshAccessToken(refreshToken: string): Promise<AuthTokens> {
    console.log('[AuthService] refreshAccessToken called');
    try {
      const decoded = verifyRefreshToken(refreshToken);
      const user = await this.getUserById(decoded.userId);
      if (!user) {
        throw new AuthError('AUTH_PROFILE_NOT_FOUND', 'User not found', 404);
      }

      const tokens = generateTokens({ userId: decoded.userId, email: decoded.email, role: user.role });
      console.log('[AuthService] Token refreshed for:', decoded.userId);
      return tokens;
    } catch (error) {
      if (error instanceof AuthError) throw error;
      console.error('[AuthService] Token refresh error:', error);
      throw new AuthError('AUTH_REFRESH_INVALID', 'Invalid or expired refresh token');
    }
  }

  /**
   * Logout (Firebase sessions are client-side)
   */
  async logout(userId: string): Promise<void> {
    console.log('[AuthService] logout:', userId);
  }

  /**
   * Pre-register (invite) an email so it can sign in with Google.
   */
  async createInvite(
    data: {
      email: string;
      role?: 'instructor' | 'student' | 'admin';
      username?: string;
      displayName?: string;
    },
    invitedBy: string
  ) {
    try {
      const db = getFirestore();
      const email = data.email.trim().toLowerCase();
      const username = data.username || email.split('@')[0];

      const existingUsername = await db
        .collection('examforge_users')
        .where('username', '==', username)
        .limit(1)
        .get();
      if (!existingUsername.empty) {
        throw new AuthError('AUTH_USERNAME_TAKEN', 'Username already taken', 409);
      }

      const invite = {
        email,
        role: data.role || 'instructor',
        username,
        displayName: data.displayName || username,
        status: 'pending',
        invitedBy,
        createdAt: new Date(),
        updatedAt: new Date(),
      };

      await db.collection('examforge_invites').doc(email).set(invite, { merge: true });
      console.log('[AuthService] Invite created for:', email);
      return invite;
    } catch (error) {
      if (error instanceof AuthError) throw error;
      throw backendUnavailable('Create invite failed', error);
    }
  }

  /**
   * List invites (most recent first).
   */
  async listInvites() {
    try {
      const db = getFirestore();
      const snapshot = await db
        .collection('examforge_invites')
        .orderBy('createdAt', 'desc')
        .limit(200)
        .get();

      return snapshot.docs.map((doc) => {
        const data = doc.data();
        return { id: doc.id, ...data, createdAt: toDate(data.createdAt) };
      });
    } catch (error) {
      throw backendUnavailable('List invites failed', error);
    }
  }

  /**
   * Revoke an invite by email.
   */
  async revokeInvite(email: string): Promise<void> {
    try {
      const db = getFirestore();
      const normalized = email.trim().toLowerCase();
      await db.collection('examforge_invites').doc(normalized).delete();
      console.log('[AuthService] Invite revoked for:', normalized);
    } catch (error) {
      throw backendUnavailable('Revoke invite failed', error);
    }
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
        createdAt: toDate(data.createdAt),
      };
    } catch (error) {
      console.error('[AuthService] getUserById error:', error);
      return null;
    }
  }
}
