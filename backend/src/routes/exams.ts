import express, { Router, Request, Response } from 'express';
import { body, param } from 'express-validator';
import { ExamService } from '../services/ExamService';
import { authenticate, authorize } from '../middleware/auth';

const router: Router = express.Router();

// All exam routes require authentication
router.use(authenticate);

/**
 * POST /api/exams - Create a new exam (Instructor only)
 */
router.post(
  '/',
  authorize('instructor', 'admin'),
  [
    body('title').trim().notEmpty().withMessage('Title is required'),
    body('description').trim().notEmpty().withMessage('Description is required'),
    body('passingScore').isInt({ min: 0, max: 100 }).withMessage('Passing score must be between 0 and 100'),
  ],
  async (req: Request, res: Response) => {
    try {
      const exam = await ExamService.createExam(
        req.user!.userId,
        req.user!.email,
        req.body
      );
      return res.status(201).json(exam);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams - Get exams (instructor: their exams, student: available exams)
 */
router.get('/', async (req: Request, res: Response) => {
  try {
    const exams = req.user!.role === 'instructor' || req.user!.role === 'admin'
      ? await ExamService.getInstructorExams(req.user!.userId)
      : await ExamService.getAvailableExams(req.user!.userId);
    
    return res.json(exams);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * GET /api/exams/:id - Get exam by ID
 */
router.get('/:id', async (req: Request, res: Response) => {
  try {
    // Find exam across all users
    const result = await ExamService.findExamById(req.params.id);
    if (!result) {
      return res.status(404).json({ error: 'Exam not found' });
    }

    const { exam, instructorId } = result;
    
    // Check access: instructor owns it OR exam is published/active
    if (exam.instructorId !== req.user!.userId && !['published', 'active'].includes(exam.status)) {
      return res.status(403).json({ error: 'Unauthorized access to exam' });
    }

    return res.json(exam);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * PUT /api/exams/:id - Update exam (Instructor only)
 */
router.put(
  '/:id',
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const exam = await ExamService.updateExam(
        req.params.id,
        req.user!.userId,
        req.body
      );
      return res.json(exam);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * DELETE /api/exams/:id - Delete exam (Instructor only)
 */
router.delete(
  '/:id',
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      await ExamService.deleteExam(req.params.id, req.user!.userId);
      return res.json({ message: 'Exam deleted successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/:id/questions - Get all questions for an exam
 */
router.get('/:id/questions', async (req: Request, res: Response) => {
  try {
    // Find exam to get instructorId
    const result = await ExamService.findExamById(req.params.id);
    if (!result) {
      return res.status(404).json({ error: 'Exam not found' });
    }

    const { instructorId } = result;
    const questions = await ExamService.getExamQuestions(
      req.params.id,
      instructorId
    );
    return res.json(questions);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * POST /api/exams/:id/questions - Create a question (Instructor only)
 */
router.post(
  '/:id/questions',
  authorize('instructor', 'admin'),
  [
    body('type').isIn(['multiple_choice', 'true_false', 'short_answer', 'essay', 'fill_in_blank', 'matching']),
    body('text').trim().notEmpty().withMessage('Question text is required'),
    body('points').isInt({ min: 1 }).withMessage('Points must be at least 1'),
    body('difficulty').isIn(['easy', 'medium', 'hard']),
  ],
  async (req: Request, res: Response) => {
    try {
      const question = await ExamService.createQuestion(
        req.user!.userId,
        {
          ...req.body,
          examId: req.params.id,
        }
      );
      return res.status(201).json(question);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * PUT /api/questions/:id - Update a question (Instructor only)
 */
router.put(
  '/questions/:id',
  authorize('instructor', 'admin'),
  [body('examId').notEmpty().withMessage('Exam ID is required')],
  async (req: Request, res: Response) => {
    try {
      const question = await ExamService.updateQuestion(
        req.params.id,
        req.user!.userId,
        req.body.examId,
        req.body
      );
      return res.json(question);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * DELETE /api/questions/:id - Delete a question (Instructor only)
 * Note: examId should be passed in query string
 */
router.delete(
  '/questions/:id',
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const examId = req.query.examId as string;
      if (!examId) {
        return res.status(400).json({ error: 'examId query parameter is required' });
      }
      await ExamService.deleteQuestion(req.params.id, req.user!.userId, examId);
      return res.json({ message: 'Question deleted successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/exams/:id/start - Start an exam attempt (Student)
 */
router.post(
  '/:id/start',
  authorize('student'),
  async (req: Request, res: Response) => {
    try {
      const attempt = await ExamService.startExamAttempt(
        req.user!.userId,
        req.user!.email,
        {
          examId: req.params.id,
          accessCode: req.body.accessCode,
        }
      );
      return res.status(201).json(attempt);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/attempts/:id/answers - Submit an answer (Student)
 */
router.post(
  '/attempts/:id/answers',
  authorize('student'),
  [
    body('questionId').notEmpty().withMessage('Question ID is required'),
    body('answer').notEmpty().withMessage('Answer is required'),
  ],
  async (req: Request, res: Response) => {
    try {
      const answer = await ExamService.submitAnswer(
        req.user!.userId,
        {
          attemptId: req.params.id,
          questionId: req.body.questionId,
          answer: req.body.answer,
          timeSpent: req.body.timeSpent,
        }
      );
      return res.json(answer);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/attempts/:id/submit - Submit exam attempt (Student)
 */
router.post(
  '/attempts/:id/submit',
  authorize('student'),
  async (req: Request, res: Response) => {
    try {
      const attempt = await ExamService.submitExam(
        req.user!.userId,
        { attemptId: req.params.id }
      );
      return res.json(attempt);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/attempts/:id - Get exam attempt with answers
 */
router.get('/attempts/:id', async (req: Request, res: Response) => {
  try {
    const attempt = await ExamService.getExamAttempt(
      req.params.id,
      req.user!.userId
    );
    return res.json(attempt);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * POST /api/attempts/:id/violations - Record a proctoring violation (Student)
 */
router.post(
  '/attempts/:id/violations',
  authorize('student'),
  [
    body('type').trim().notEmpty().withMessage('Violation type is required'),
    body('timestamp').isNumeric().withMessage('Timestamp is required'),
  ],
  async (req: Request, res: Response) => {
    try {
      await ExamService.recordViolation(
        req.user!.userId,
        req.params.id,
        req.body.type,
        req.body.timestamp
      );
      return res.status(201).json({ message: 'Violation recorded' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/grading/queue - Get grading queue for essay questions (Instructor only)
 */
router.get(
  '/grading/queue',
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const queue = await ExamService.getGradingQueue(req.user!.userId);
      return res.json(queue);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/grading/submit - Submit grade for a question (Instructor only)
 */
router.post(
  '/grading/submit',
  authorize('instructor', 'admin'),
  [
    body('attemptId').notEmpty().withMessage('Attempt ID is required'),
    body('questionId').notEmpty().withMessage('Question ID is required'),
    body('earnedPoints').isNumeric().withMessage('Earned points must be a number'),
    body('feedback').optional().isString()
  ],
  async (req: Request, res: Response) => {
    try {
      const { attemptId, questionId, earnedPoints, feedback } = req.body;
      await ExamService.gradeQuestion(attemptId, questionId, earnedPoints, feedback);
      return res.json({ success: true, message: 'Grade submitted successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/analytics/:examId - Get analytics for an exam (Instructor only)
 */
router.get(
  '/analytics/:examId',
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const analytics = await ExamService.getExamAnalytics(req.params.examId, req.user!.userId);
      return res.json(analytics);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/incidents - Get incident reports (Instructor only)
 */
router.get(
  '/incidents',
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const incidents = await ExamService.getIncidentReports(req.user!.userId);
      return res.json(incidents);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/exams/incidents/archive - Archive incidents (Instructor only)
 */
router.post(
  '/incidents/archive',
  authorize('instructor', 'admin'),
  [body('incidentIds').isArray().withMessage('incidentIds must be an array')],
  async (req: Request, res: Response) => {
    try {
      await ExamService.archiveIncidents(req.body.incidentIds);
      return res.json({ success: true, message: 'Incidents archived successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/exams/incidents/unarchive - Unarchive incidents (Instructor only)
 */
router.post(
  '/incidents/unarchive',
  authorize('instructor', 'admin'),
  [body('incidentIds').isArray().withMessage('incidentIds must be an array')],
  async (req: Request, res: Response) => {
    try {
      await ExamService.unarchiveIncidents(req.body.incidentIds);
      return res.json({ success: true, message: 'Incidents unarchived successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/exams/incidents/delete - Delete incidents (Instructor only)
 */
router.post(
  '/incidents/delete',
  authorize('instructor', 'admin'),
  [body('incidentIds').isArray().withMessage('incidentIds must be an array')],
  async (req: Request, res: Response) => {
    try {
      await ExamService.deleteIncidents(req.body.incidentIds);
      return res.json({ success: true, message: 'Incidents deleted successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

export default router;
