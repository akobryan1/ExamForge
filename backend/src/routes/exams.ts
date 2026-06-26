import express, { Router, Request, Response } from 'express';
import { body, param } from 'express-validator';
import { ExamService } from '../services/ExamService';
import { authenticate, authorize, optionalAuth } from '../middleware/auth';

const router: Router = express.Router();

// NOTE: No global authenticate — public routes (take exam, view exam) must work for guests

/**
 * POST /api/exams - Create a new exam (Instructor only)
 */
router.post(
  '/',
  authenticate,
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
router.get('/', optionalAuth, async (req: Request, res: Response) => {
  try {
    if (!req.user) {
      // Guest — show published/active exams only
      const exams = await ExamService.getAvailableExams('guest');
      return res.json(exams);
    }
    const exams = req.user.role === 'instructor' || req.user.role === 'admin'
      ? await ExamService.getInstructorExams(req.user.userId)
      : await ExamService.getAvailableExams(req.user.userId);
    
    return res.json(exams);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * GET /api/exams/:id - Get exam by ID
 */
router.get('/:id', optionalAuth, async (req: Request, res: Response) => {
  try {
    const userId = req.user?.userId;
    const isInstructor = req.user?.role === 'instructor' || req.user?.role === 'admin';
    
    // For instructors: try direct path first
    if (isInstructor && userId) {
      const exam = await ExamService.getExamByIdForInstructor(req.params.id, userId);
      if (exam) return res.json(exam);
    }
    
    // For students / guests: use collection group query
    const result = await ExamService.findExamById(req.params.id);
    if (!result) {
      return res.status(404).json({ error: 'Exam not found' });
    }

    const { exam } = result;
    
    if (exam.instructorId !== userId && !['published', 'active'].includes(exam.status)) {
      return res.status(403).json({ error: 'Unauthorized access to exam' });
    }

    return res.json(exam);
  } catch (error: any) {
    console.error('[Exams] GET /:id error:', error);
    return res.status(400).json({ error: error.message });
  }
});

/**
 * PUT /api/exams/:id - Update exam (Instructor only)
 */
router.put(
  '/:id',
  authenticate,
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
  authenticate,
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
router.get('/:id/questions', optionalAuth, async (req: Request, res: Response) => {
  try {
    const userId = req.user?.userId;
    const isInstructor = req.user?.role === 'instructor' || req.user?.role === 'admin';
    
    if (isInstructor && userId) {
      const exam = await ExamService.getExamByIdForInstructor(req.params.id, userId);
      if (!exam) return res.status(404).json({ error: 'Exam not found' });
      const questions = await ExamService.getExamQuestions(req.params.id, userId);
      return res.json(questions);
    }
    
    const result = await ExamService.findExamById(req.params.id);
    if (!result) return res.status(404).json({ error: 'Exam not found' });
    const questions = await ExamService.getExamQuestions(req.params.id, result.instructorId);
    return res.json(questions);
  } catch (error: any) {
    console.error('[Exams] GET questions error:', error);
    return res.status(400).json({ error: error.message });
  }
});

/**
 * POST /api/exams/:id/questions - Create a question (Instructor only)
 */
router.post(
  '/:id/questions',
  authenticate,
  authorize('instructor', 'admin'),
  [
    body('type').isIn(['multiple_choice', 'true_false', 'modified_true_false', 'essay', 'identification', 'enumeration']),
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
  optionalAuth,
  async (req: Request, res: Response) => {
    try {
      console.log(`[StartExam] userId=${req.user?.userId || 'guest'}, email=${req.user?.email || 'guest@anonymous.com'}, role=${req.user?.role || 'none'}, examId=${req.params.id}`);
      const attempt = await ExamService.startExamAttempt(
        req.user?.userId || 'guest',
        req.user?.email || 'guest@anonymous.com',
        {
          examId: req.params.id,
          accessCode: req.body.accessCode,
          guestInfo: req.body.guestInfo,
        }
      );
      return res.status(201).json(attempt);
    } catch (error: any) {
      console.error(`[StartExam] Error for exam ${req.params.id}:`, error.message);
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/attempts/:id/answers - Submit an answer (Student or Guest)
 */
router.post(
  '/attempts/:id/answers',
  optionalAuth,
  [
    body('questionId').notEmpty().withMessage('Question ID is required'),
    body('answer').notEmpty().withMessage('Answer is required'),
  ],
  async (req: Request, res: Response) => {
    try {
      const userId = req.user?.userId || `guest_${req.params.id}`;
      const answer = await ExamService.submitAnswer(
        userId,
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
 * POST /api/attempts/:id/submit - Submit exam attempt (Student or Guest)
 */
router.post(
  '/attempts/:id/submit',
  optionalAuth,
  async (req: Request, res: Response) => {
    try {
      const userId = req.user?.userId || `guest_${req.params.id}`;
      const attempt = await ExamService.submitExam(
        userId,
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
router.get('/attempts/:id', optionalAuth, async (req: Request, res: Response) => {
  try {
    const userId = req.user?.userId || `guest_${req.params.id}`;
    const attempt = await ExamService.getExamAttempt(
      req.params.id,
      userId
    );
    return res.json(attempt);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * POST /api/attempts/:id/violations - Record a proctoring violation (Student or Guest)
 */
router.post(
  '/attempts/:id/violations',
  optionalAuth,
  [
    body('type').trim().notEmpty().withMessage('Violation type is required'),
    body('timestamp').isNumeric().withMessage('Timestamp is required'),
  ],
  async (req: Request, res: Response) => {
    try {
      const userId = req.user?.userId || `guest_${req.params.id}`;
      await ExamService.recordViolation(
        userId,
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
