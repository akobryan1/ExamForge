import jwt from 'jsonwebtoken';
import { JwtPayload, AuthTokens } from '../types';

const JWT_SECRET = process.env.JWT_SECRET || 'development-secret-key';
const JWT_REFRESH_SECRET = process.env.JWT_REFRESH_SECRET || 'development-refresh-secret';
const JWT_EXPIRES_IN = process.env.JWT_EXPIRES_IN || '7d';
const JWT_REFRESH_EXPIRES_IN = process.env.JWT_REFRESH_EXPIRES_IN || '30d';

/**
 * Generate JWT access and refresh tokens
 */
export function generateTokens(payload: JwtPayload): AuthTokens {
  const accessToken = jwt.sign(payload, JWT_SECRET, {
    expiresIn: JWT_EXPIRES_IN,
  } as jwt.SignOptions);

  const refreshToken = jwt.sign(
    { userId: payload.userId },
    JWT_REFRESH_SECRET,
    {
      expiresIn: JWT_REFRESH_EXPIRES_IN,
    } as jwt.SignOptions
  );

  // Calculate expiration time in seconds
  const decoded = jwt.decode(accessToken) as any;
  const expiresIn = decoded.exp - Math.floor(Date.now() / 1000);

  return {
    accessToken,
    refreshToken,
    expiresIn,
  };
}

/**
 * Verify JWT access token
 */
export function verifyAccessToken(token: string): JwtPayload {
  try {
    const decoded = jwt.verify(token, JWT_SECRET) as JwtPayload;
    return decoded;
  } catch (error) {
    throw new Error('Invalid or expired access token');
  }
}

/**
 * Verify JWT refresh token
 */
export function verifyRefreshToken(token: string): { userId: string } {
  try {
    const decoded = jwt.verify(token, JWT_REFRESH_SECRET) as { userId: string };
    return decoded;
  } catch (error) {
    throw new Error('Invalid or expired refresh token');
  }
}

/**
 * Extract token from Authorization header
 */
export function extractTokenFromHeader(authHeader?: string): string | null {
  if (!authHeader) return null;
  
  const parts = authHeader.split(' ');
  if (parts.length !== 2 || parts[0] !== 'Bearer') return null;
  
  return parts[1];
}
