import { Router, Request, Response } from 'express';
import { body, validationResult } from 'express-validator';
import { AuthService } from '../services/AuthService';
import { authenticate } from '../middleware/auth';

const router = Router();
const authService = new AuthService();

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
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 30 * 24 * 60 * 60 * 1000,
      });

      res.status(201).json({ user, accessToken: tokens.accessToken, expiresIn: tokens.expiresIn });
    } catch (error) {
      console.error('[AuthRoute] Signup error:', error);
      res.status(400).json({
        error: 'Signup failed',
        message: error instanceof Error ? error.message : 'Unknown error',
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
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 30 * 24 * 60 * 60 * 1000,
      });

      res.json({ user, accessToken: tokens.accessToken, expiresIn: tokens.expiresIn });
    } catch (error) {
      console.error('[AuthRoute] Login error:', error);
      res.status(401).json({
        error: 'Login failed',
        message: error instanceof Error ? error.message : 'Invalid credentials',
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
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 30 * 24 * 60 * 60 * 1000,
      });

      res.json({
        user,
        accessToken: tokens.accessToken,
        expiresIn: tokens.expiresIn,
      });
    } catch (error) {
      console.error('Google auth error:', error);
      res.status(401).json({
        error: 'Google authentication failed',
        message: error instanceof Error ? error.message : 'Unknown error',
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
      secure: process.env.NODE_ENV === 'production',
      sameSite: 'strict',
      maxAge: 30 * 24 * 60 * 60 * 1000,
    });

    res.json({
      accessToken: tokens.accessToken,
      expiresIn: tokens.expiresIn,
    });
  } catch (error) {
    console.error('Token refresh error:', error);
    res.status(401).json({
      error: 'Token refresh failed',
      message: error instanceof Error ? error.message : 'Invalid refresh token',
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
        secure: process.env.NODE_ENV === 'production',
        sameSite: 'strict',
        maxAge: 30 * 24 * 60 * 60 * 1000, // 30 days
      });

      res.status(201).json({
        user,
        accessToken: tokens.accessToken,
        message: 'Student account created successfully',
      });
    } catch (error) {
      console.error('Student registration error:', error);
      res.status(500).json({
        error: 'Registration failed',
        message: error instanceof Error ? error.message : 'Unknown error',
      });
    }
  }
);

export default router;
