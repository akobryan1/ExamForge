import express, { Router, Request, Response } from 'express';
import { body } from 'express-validator';
import { AIQuestionGeneratorService } from '../services/AIQuestionGeneratorService';
import { authenticate, authorize } from '../middleware/auth';
import { QuestionType } from '../types/exam';

const router: Router = express.Router();

// All AI routes require authentication and instructor role
router.use(authenticate);
router.use(authorize('instructor', 'admin'));

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

export default router;
