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
  ],
  async (req: Request, res: Response) => {
    try {
      const { material, questionType, count, difficulty, topic } = req.body;

      const questions = await AIQuestionGeneratorService.generateQuestionsFromMaterial({
        material,
        questionType: questionType as QuestionType,
        count,
        difficulty,
        topic,
      });

      return res.json({
        success: true,
        questions,
        usingPlaceholder: process.env.OPENAI_API_KEY === undefined || 
                         process.env.OPENAI_API_KEY === 'sk-placeholder-key-replace-in-production',
      });
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

      const { questionType, count, difficulty, topic } = req.body;
      const filePath = req.file.path;

      // Read file content
      let material = '';
      const ext = path.extname(req.file.originalname).toLowerCase();

      if (ext === '.txt') {
        material = fs.readFileSync(filePath, 'utf-8');
      } else {
        // For PDF/DOCX, return a hint that text extraction needs a package
        // In production, use pdf-parse or mammoth
        material = `[Content extracted from ${req.file.originalname}]\n\n`;
        material += fs.readFileSync(filePath, 'utf-8');
      }

      // Clean up: remove uploaded file after reading
      fs.unlink(filePath, () => {});

      if (!material.trim()) {
        return res.status(400).json({ error: 'Could not extract text from the uploaded file' });
      }

      const questions = await AIQuestionGeneratorService.generateQuestionsFromMaterial({
        material: material.slice(0, 50000), // limit to 50k chars
        questionType: (questionType as QuestionType) || QuestionType.MULTIPLE_CHOICE,
        count: Math.min(20, Math.max(1, parseInt(count) || 5)),
        difficulty: difficulty || 'medium',
        topic: topic || undefined,
      });

      return res.json({
        success: true,
        questions,
        usingPlaceholder: process.env.OPENAI_API_KEY === undefined || 
                         process.env.OPENAI_API_KEY === 'sk-placeholder-key-replace-in-production',
      });
    } catch (error: any) {
      return res.status(500).json({ error: error.message });
    }
  }
);

export default router;
