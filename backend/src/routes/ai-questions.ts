import express, { Router, Request, Response } from 'express';
import { body } from 'express-validator';
import multer from 'multer';
import path from 'path';
import fs from 'fs';
// @ts-ignore - pdf-parse v1 is CommonJS, no types
import pdfParse from 'pdf-parse';
import { AIQuestionGeneratorService } from '../services/AIQuestionGeneratorService';
import { authenticate, authorize } from '../middleware/auth';
import { QuestionType } from '../types/exam';

const router: Router = express.Router();

// All AI routes require authentication and instructor role
router.use(authenticate);
router.use(authorize('instructor', 'admin'));

// Multer config for file uploads
const uploadsDir = path.join(__dirname, '../../uploads');
if (!fs.existsSync(uploadsDir)) {
  fs.mkdirSync(uploadsDir, { recursive: true });
}

const upload = multer({
  storage: multer.diskStorage({
    destination: (req, file, cb) => cb(null, uploadsDir),
    filename: (req, file, cb) => {
      const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1E9);
      cb(null, `ai-${uniqueSuffix}${path.extname(file.originalname)}`);
    }
  }),
  fileFilter: (req, file, cb) => {
    const allowed = [
      'application/pdf',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    ];
    if (allowed.includes(file.mimetype)) {
      cb(null, true);
    } else {
      cb(new Error('Only PDF and DOCX files are allowed.'));
    }
  },
  limits: { fileSize: 25 * 1024 * 1024 },
});

/** Look up the user's saved API key from Firestore settings */
async function getUserApiKey(userId: string): Promise<string | undefined> {
  try {
    const { getFirestore } = await import('firebase-admin/firestore');
    const db = getFirestore();
    const doc = await db.collection('examforge_users').doc(userId).get();
    const apiKey = doc.data()?.settings?.apiKey || undefined;
    console.log('[AI-DEBUG] getUserApiKey for', userId, '-> found:', !!apiKey, 'starts_with:', apiKey?.slice(0, 10));
    return apiKey;
  } catch (err) {
    console.error('[AI-DEBUG] getUserApiKey error:', err);
    return undefined;
  }
}

/** Detect actual file type from magic bytes — returns 'pdf', 'docx', or null */
function detectFileType(buffer: Buffer): 'pdf' | 'docx' | null {
  // PDF: starts with %PDF
  if (buffer[0] === 0x25 && buffer[1] === 0x50 && buffer[2] === 0x44 && buffer[3] === 0x46) {
    return 'pdf';
  }
  // DOCX (ZIP): starts with PK\x03\x04
  if (buffer[0] === 0x50 && buffer[1] === 0x4B && buffer[2] === 0x03 && buffer[3] === 0x04) {
    return 'docx';
  }
  return null;
}

/** Max chars to send to the AI (50k chars ≈ 12k tokens) */
const MAX_MATERIAL_LENGTH = 50000;
/** Max file size: 25 MB (already enforced by multer) */

/*
 * [AI QUESTION GENERATION ROUTES REMOVED — see git history for Generate with AI feature]
 * 
 * Routes that were here:
 *   POST /api/ai/generate-questions
 *   POST /api/ai/generate-from-file
 * 
 * Supporting functions:
 *   getUserApiKey, readAndCleanUp, detectFileType, questionResponse, generateWithUserKey
 * 
 * Dependencies kept: pdf-parse, mammoth, multer imports
 */

export default router;
