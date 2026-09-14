import { Router, Request, Response } from 'express';
import { body, validationResult } from 'express-validator';
import { AuthService, AuthError } from '../services/AuthService';
import { authenticate, authorize } from '../middleware/auth';

const router = Router();
const authService = new AuthService();

/**
 * Send an authentication failure without leaking internals.
 * `AuthError` carries a stable code/status/message; anything else is logged in full
 * server-side and returned to the client as a generic, sanitized message.
 */
function sendAuthError(
  res: Response,
  context: string,
  error: unknown,
  fallback: { status: number; code: string; message: string }
): void {
  if (error instanceof AuthError) {
    console.warn(`[AuthRoute] ${context} failed:`, error.code, error.message);
    res.status(error.status).json({ error: error.code, code: error.code, message: error.message });
    return;
  }
  console.error(`[AuthRoute] ${context} failed:`, error);
  res.status(fallback.status).json({ error: fallback.code, code: fallback.code, message: fallback.message });
}

/**
 * POST /api/auth/signup
 * Register via Firebase — frontend creates Firebase auth user, sends idToken + profile
 */
router.post(
  '/signup',
  [
    body('idToken').notEmpty().withMessage('Firebase ID token is required'),
    body('username')
      .isLength({ min: 3 })
      .withMessage('Username must be at least 3 characters')
      .matches(/^[a-zA-Z0-9_]+$/)
      .withMessage('Username can only contain letters, numbers, and underscores'),
  ],
  async (req: Request, res: Response): Promise<void> => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        res.status(400).json({ errors: errors.array() });
        return;
      }

      console.log('[AuthRoute] Signup request:', { username: req.body.username });
      const { user, tokens } = await authService.signupWithEmail(req.body);

      res.cookie('refreshToken', tokens.refreshToken, {
        httpOnly: true,
        secure: true,
        sameSite: 'none',
        maxAge: 30 * 24 * 60 * 60 * 1000,
      });

      res.status(201).json({ user, accessToken: tokens.accessToken, expiresIn: tokens.expiresIn });
    } catch (error) {
      sendAuthError(res, 'Signup', error, {
        status: 400,
        code: 'AUTH_SIGNUP_FAILED',
        message: 'Signup failed. Please try again.',
      });
    }
  }
);

/**
 * POST /api/auth/login
 * Login via Firebase — frontend authenticates with Firebase, sends idToken
 */
router.post(
  '/login',
  [
    body('idToken').notEmpty().withMessage('Firebase ID token is required'),
  ],
  async (req: Request, res: Response): Promise<void> => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        res.status(400).json({ errors: errors.array() });
        return;
      }

      console.log('[AuthRoute] Login request via Firebase');
      const { user, tokens } = await authService.loginWithEmail(req.body.idToken);
      console.log('[AuthRoute] Login successful:', user.email, 'role:', user.role);

      res.cookie('refreshToken', tokens.refreshToken, {
        httpOnly: true,
        secure: true,
        sameSite: 'none',
        maxAge: 30 * 24 * 60 * 60 * 1000,
      });

      res.json({ user, accessToken: tokens.accessToken, expiresIn: tokens.expiresIn });
    } catch (error) {
      sendAuthError(res, 'Login', error, {
        status: 401,
        code: 'AUTH_LOGIN_FAILED',
        message: 'Login failed. Please try again.',
      });
    }
  }
);

/**
 * POST /api/auth/google
 * Login with Google OAuth
 */
router.post(
  '/google',
  [body('idToken').notEmpty().withMessage('ID token is required')],
  async (req: Request, res: Response): Promise<void> => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        res.status(400).json({ errors: errors.array() });
        return;
      }

      const { idToken } = req.body;

      // Authenticate with Google
      const { user, tokens } = await authService.loginWithGoogle(idToken);

      // Set refresh token in httpOnly cookie
      res.cookie('refreshToken', tokens.refreshToken, {
        httpOnly: true,
        secure: true,
        sameSite: 'none',
        maxAge: 30 * 24 * 60 * 60 * 1000,
      });

      res.json({
        user,
        accessToken: tokens.accessToken,
        expiresIn: tokens.expiresIn,
      });
    } catch (error) {
      sendAuthError(res, 'Google auth', error, {
        status: 401,
        code: 'AUTH_GOOGLE_FAILED',
        message: 'Google authentication failed. Please try again.',
      });
    }
  }
);

/**
 * POST /api/auth/refresh
 * Refresh access token
 */
router.post('/refresh', async (req: Request, res: Response): Promise<void> => {
  try {
    const refreshToken = req.cookies.refreshToken || req.body.refreshToken;

    if (!refreshToken) {
      res.status(401).json({
        error: 'Unauthorized',
        message: 'No refresh token provided',
      });
      return;
    }

    const tokens = await authService.refreshAccessToken(refreshToken);

    // Update refresh token cookie
    res.cookie('refreshToken', tokens.refreshToken, {
      httpOnly: true,
      secure: true,
      sameSite: 'none',
      maxAge: 30 * 24 * 60 * 60 * 1000,
    });

    res.json({
      accessToken: tokens.accessToken,
      expiresIn: tokens.expiresIn,
    });
  } catch (error) {
    sendAuthError(res, 'Token refresh', error, {
      status: 401,
      code: 'AUTH_REFRESH_FAILED',
      message: 'Session expired. Please sign in again.',
    });
  }
});

/**
 * POST /api/auth/logout
 * Logout user
 */
router.post('/logout', authenticate, async (req: Request, res: Response): Promise<void> => {
  try {
    if (req.user) {
      await authService.logout(req.user.userId);
    }

    // Clear refresh token cookie
    res.clearCookie('refreshToken');

    res.json({ message: 'Logged out successfully' });
  } catch (error) {
    console.error('Logout error:', error);
    res.status(500).json({
      error: 'Logout failed',
      message: error instanceof Error ? error.message : 'Unknown error',
    });
  }
});

/**
 * GET /api/auth/me
 * Get current authenticated user
 */
router.get('/me', authenticate, async (req: Request, res: Response): Promise<void> => {
  try {
    if (!req.user) {
      res.status(401).json({
        error: 'Unauthorized',
        message: 'Not authenticated',
      });
      return;
    }

    const user = await authService.getUserById(req.user.userId);

    if (!user) {
      res.status(404).json({
        error: 'Not Found',
        message: 'User not found',
      });
      return;
    }

    res.json({ user });
  } catch (error) {
    console.error('Get user error:', error);
    res.status(500).json({
      error: 'Failed to get user',
      message: error instanceof Error ? error.message : 'Unknown error',
    });
  }
});

/**
 * POST /api/auth/register/student
 * Student self-registration
 */
router.post(
  '/register/student',
  [
    body('idToken').notEmpty().withMessage('Firebase ID token is required'),
    body('studentId')
      .notEmpty()
      .withMessage('Student ID is required'),
    body('studentName')
      .notEmpty()
      .withMessage('Student name is required'),
    body('section').optional().isString(),
  ],
  async (req: Request, res: Response): Promise<void> => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        res.status(400).json({ errors: errors.array() });
        return;
      }

      const { idToken, studentId, studentName, section } = req.body;

      // Create student account via Firebase
      const { user, tokens } = await authService.signupWithEmail({
        idToken,
        username: studentId,
        displayName: studentName,
        role: 'student',
      });

      // Set refresh token in httpOnly cookie
      res.cookie('refreshToken', tokens.refreshToken, {
        httpOnly: true,
        secure: true,
        sameSite: 'none',
        maxAge: 30 * 24 * 60 * 60 * 1000, // 30 days
      });

      res.status(201).json({
        user,
        accessToken: tokens.accessToken,
        message: 'Student account created successfully',
      });
    } catch (error) {
      sendAuthError(res, 'Student registration', error, {
        status: 500,
        code: 'AUTH_STUDENT_REGISTER_FAILED',
        message: 'Registration failed. Please try again.',
      });
    }
  }
);

/**
 * POST /api/auth/invites
 * Pre-register (invite) an email address so it can sign in with Google. Instructor/admin only.
 */
router.post(
  '/invites',
  authenticate,
  authorize('instructor', 'admin'),
  [
    body('email').isEmail().withMessage('Valid email is required'),
    body('role').optional().isIn(['instructor', 'student', 'admin']).withMessage('Invalid role'),
    body('username')
      .optional()
      .isLength({ min: 3 })
      .withMessage('Username must be at least 3 characters')
      .matches(/^[a-zA-Z0-9_]+$/)
      .withMessage('Username can only contain letters, numbers, and underscores'),
  ],
  async (req: Request, res: Response): Promise<void> => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        res.status(400).json({ errors: errors.array() });
        return;
      }

      const invite = await authService.createInvite(req.body, req.user!.userId);
      res.status(201).json({ invite });
    } catch (error) {
      sendAuthError(res, 'Create invite', error, {
        status: 400,
        code: 'AUTH_INVITE_FAILED',
        message: 'Could not create invite. Please try again.',
      });
    }
  }
);

/**
 * GET /api/auth/invites
 * List invites. Instructor/admin only.
 */
router.get(
  '/invites',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response): Promise<void> => {
    try {
      const invites = await authService.listInvites();
      res.json({ invites });
    } catch (error) {
      sendAuthError(res, 'List invites', error, {
        status: 500,
        code: 'AUTH_INVITE_LIST_FAILED',
        message: 'Could not load invites. Please try again.',
      });
    }
  }
);

/**
 * DELETE /api/auth/invites/:email
 * Revoke an invite. Instructor/admin only.
 */
router.delete(
  '/invites/:email',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response): Promise<void> => {
    try {
      await authService.revokeInvite(req.params.email);
      res.json({ message: 'Invite revoked' });
    } catch (error) {
      sendAuthError(res, 'Revoke invite', error, {
        status: 500,
        code: 'AUTH_INVITE_REVOKE_FAILED',
        message: 'Could not revoke invite. Please try again.',
      });
    }
  }
);

export default router;
