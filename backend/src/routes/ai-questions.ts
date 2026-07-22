import express, { Router, Request, Response } from 'express';
import { body } from 'express-validator';
import multer from 'multer';
import path from 'path';
import fs from 'fs';
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
      'application/msword',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'text/plain',
    ];
    if (allowed.includes(file.mimetype)) {
      cb(null, true);
    } else {
      cb(new Error('Only PDF, Word (.doc/.docx), and text files are allowed.'));
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
    return doc.data()?.settings?.apiKey || undefined;
  } catch {
    return undefined;
  }
}

/** Helper that reads uploaded file content, then deletes it */
function readAndCleanUp(filePath: string, ext: string): string {
  let content = '';
  if (ext === '.txt') {
    content = fs.readFileSync(filePath, 'utf-8');
  } else {
    content = `[Content extracted from uploaded file]\n\n`;
    content += fs.readFileSync(filePath, 'utf-8');
  }
  fs.unlink(filePath, () => {});
  return content.slice(0, 50000);
}

/** Build the response body for generated questions */
function questionResponse(questions: unknown[], apiKey?: string) {
  return {
    success: true,
    questions,
    usingPlaceholder: !apiKey || apiKey === 'sk-placeholder-key-replace-in-production',
  };
}

/** Common handler for both text and file-based generation */
async function generateWithUserKey(
  userId: string,
  params: {
    material: string;
    questionType: QuestionType;
    count: number;
    difficulty?: string;
    topic?: string;
    customPrompt?: string;
  }
) {
  const apiKey = await getUserApiKey(userId);
  const questions = await AIQuestionGeneratorService.generateQuestionsFromMaterial(params as any, apiKey);
  return { questions, apiKey };
}

/**
 * POST /api/ai/generate-questions - Generate questions from material
 */
router.post(
  '/generate-questions',
  [
    body('material').trim().notEmpty().withMessage('Material text is required'),
    body('questionType').isIn([
      'multiple_choice',
      'true_false',
      'modified_true_false',
      'essay',
      'identification',
      'enumeration',
    ]).withMessage('Invalid question type'),
    body('count').isInt({ min: 1, max: 20 }).withMessage('Count must be between 1 and 20'),
    body('difficulty').optional().isIn(['easy', 'medium', 'hard']),
    body('topic').optional().trim(),
    body('customPrompt').optional().trim(),
  ],
  async (req: Request, res: Response) => {
    try {
      const { material, questionType, count, difficulty, topic, customPrompt } = req.body;

      const { questions, apiKey } = await generateWithUserKey(req.user!.userId, {
        material,
        questionType: questionType as QuestionType,
        count,
        difficulty,
        topic,
        customPrompt,
      });

      return res.json(questionResponse(questions, apiKey));
    } catch (error: any) {
      return res.status(500).json({ error: error.message });
    }
  }
);

/**
 * POST /api/ai/generate-from-file - Upload a file and generate questions
 */
router.post(
  '/generate-from-file',
  upload.single('file'),
  async (req: Request, res: Response) => {
    try {
      if (!req.file) {
        return res.status(400).json({ error: 'No file uploaded' });
      }

      const { questionType, count, difficulty, topic, customPrompt } = req.body;
      const filePath = req.file.path;
      const ext = path.extname(req.file.originalname).toLowerCase();
      const material = readAndCleanUp(filePath, ext);

      if (!material.trim()) {
        return res.status(400).json({ error: 'Could not extract text from the uploaded file' });
      }

      const { questions, apiKey } = await generateWithUserKey(req.user!.userId, {
        material,
        questionType: (questionType as QuestionType) || QuestionType.MULTIPLE_CHOICE,
        count: Math.min(20, Math.max(1, parseInt(count) || 5)),
        difficulty: difficulty || 'medium',
        topic: topic || undefined,
        customPrompt: customPrompt || undefined,
      });

      return res.json(questionResponse(questions, apiKey));
    } catch (error: any) {
      return res.status(500).json({ error: error.message });
    }
  }
);

export default router;
