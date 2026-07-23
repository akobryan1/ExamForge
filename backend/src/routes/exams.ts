import express, { Router, Request, Response } from 'express';
import { body, param } from 'express-validator';
import { ExamService } from '../services/ExamService';
import { authenticate, authorize, optionalAuth } from '../middleware/auth';
import { getOrSet, invalidatePrefix } from '../utils/cache';

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
      invalidatePrefix('exams_list_');
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
      const exams = await ExamService.getAvailableExams('guest');
      return res.json(exams);
    }
    const user = req.user;
    const cacheKey = `exams_list_${user.userId}`;
    const exams = await getOrSet(cacheKey, 120, () =>
      user.role === 'instructor' || user.role === 'admin'
        ? ExamService.getInstructorExams(user.userId)
        : ExamService.getAvailableExams(user.userId)
    );
    
    return res.json(exams);
  } catch (error: any) {
    return res.status(400).json({ error: error.message });
  }
});

/**
 * GET /api/exams/grading/queue - Get grading queue for essay questions (Instructor only)
 */
router.get(
  '/grading/queue',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const cacheKey = `grading_${req.user!.userId}`;
      const queue = await getOrSet(cacheKey, 60, () => ExamService.getGradingQueue(req.user!.userId));
      return res.json(queue);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/exams/grading/submit - Submit grade for a question (Instructor only)
 */
router.post(
  '/grading/submit',
  authenticate,
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
      invalidatePrefix('grading_');
      return res.json({ success: true, message: 'Grade submitted successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * POST /api/exams/grading/ai-grade - Auto-grade an essay using AI (Instructor only)
 */
router.post(
  '/grading/ai-grade',
  authenticate,
  authorize('instructor', 'admin'),
  [
    body('questionText').trim().notEmpty().withMessage('Question text is required'),
    body('studentAnswer').trim().notEmpty().withMessage('Student answer is required'),
    body('maxPoints').isNumeric().withMessage('Max points is required'),
    body('model').trim().notEmpty().withMessage('AI model is required'),
    body('keyPoints').optional().isString(),
    body('modelAnswer').optional().isString(),
  ],
  async (req: Request, res: Response) => {
    try {
      const { questionText, studentAnswer, maxPoints, model, keyPoints, modelAnswer } = req.body;

      // Read API key from env var first (set in Render dashboard), fallback to Firestore
      let apiKey = process.env.DEEPSEEK_API_KEY || '';
      if (!apiKey) {
        const { getFirestore } = await import('firebase-admin/firestore');
        const db = getFirestore();
        const instructorDoc = await db.collection('examforge_users').doc(req.user!.userId).get();
        const settings = instructorDoc.data()?.settings;
        apiKey = settings?.apiKey || '';
      }
      if (!apiKey) {
        return res.status(400).json({ error: 'No API key configured. Please save your API key in Settings first.' });
      }

      console.log('[AIGrade] Using saved API key:', apiKey.slice(0, 8) + '...');
      console.log('[AIGrade] Sending to DeepSeek API with model:', model);

      // Build system prompt with grading criteria
      let systemPrompt = `You are an expert essay grader. Your role is to evaluate the student's essay based on clarity and content in relevance to the essay question.`;
      systemPrompt += `\nThe essay has a maximum of ${maxPoints} points. You must assign a score between 0 and ${maxPoints}, without exceeding the maximum.`;

      if (modelAnswer?.trim()) {
        systemPrompt += `\n\nModel/Reference Answer:\n${modelAnswer}`;
      }
      if (keyPoints?.trim()) {
        systemPrompt += `\n\nKey Points the answer should cover:\n${keyPoints}`;
      }

      systemPrompt += `\n\nReturn ONLY a JSON object with:
- "score" (number out of ${maxPoints})
- "feedback" (string with brief, constructive explanation for the student)
- "justification" (string explaining WHY you awarded this score, referencing specific parts of the answer in relation to clarity, content, and the model answer/key points)

Be fair, consistent, and thorough. Score must be between 0 and ${maxPoints}.`;

      const userContent = `Question: ${questionText}\n\nStudent Answer: ${studentAnswer}`;

      // Call DeepSeek API directly
      const response = await fetch('https://api.deepseek.com/v1/chat/completions', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${apiKey}`,
        },
        body: JSON.stringify({
          model,
          messages: [
            { role: 'system', content: systemPrompt },
            { role: 'user', content: userContent },
          ],
          temperature: 0.3,
          max_tokens: 800,
        }),
      });

      if (!response.ok) {
        const errBody = await response.text();
        console.error('[AIGrade] API error:', response.status, errBody);
        let detail = response.statusText;
        try {
          const parsed = JSON.parse(errBody);
          detail = parsed.error?.message || parsed.error || detail;
        } catch { /* use statusText */ }
        return res.status(502).json({ error: `AI grading failed: ${detail}` });
      }

      const data = await response.json() as { choices?: { message?: { content?: string } }[] };
      const content = data.choices?.[0]?.message?.content || '{}';
      
      let result: { score: number; feedback: string; justification?: string };
      try {
        result = JSON.parse(content);
      } catch {
        return res.status(502).json({ error: 'AI returned invalid JSON response' });
      }

      if (typeof result.score !== 'number' || result.score < 0 || result.score > maxPoints) {
        return res.status(502).json({ error: 'AI returned invalid score' });
      }

      return res.json({
        score: Math.round(result.score * 2) / 2,
        feedback: result.feedback || '',
        justification: result.justification || '',
      });
    } catch (error: any) {
      console.error('[AIGrade] Error:', error.message);
      return res.status(502).json({ error: error.message || 'AI grading failed' });
    }
  }
);

/**
 * GET /api/exams/analytics/:examId - Get analytics for an exam (Instructor only)
 */
router.get(
  '/analytics/:examId',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const cacheKey = `analytics_${req.params.examId}_${req.user!.userId}`;
      const analytics = await getOrSet(cacheKey, 300, () => ExamService.getExamAnalytics(req.params.examId, req.user!.userId));
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
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const cacheKey = `incidents_${req.user!.userId}`;
      const incidents = await getOrSet(cacheKey, 60, () => ExamService.getIncidentReports(req.user!.userId));
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
  authenticate,
  authorize('instructor', 'admin'),
  [body('incidents').isArray().withMessage('incidents must be an array')],
  async (req: Request, res: Response) => {
    try {
      await ExamService.archiveIncidents(req.body.incidents);
      invalidatePrefix('incidents_');
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
  authenticate,
  authorize('instructor', 'admin'),
  [body('incidents').isArray().withMessage('incidents must be an array')],
  async (req: Request, res: Response) => {
    try {
      await ExamService.unarchiveIncidents(req.body.incidents);
      invalidatePrefix('incidents_');
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
  authenticate,
  authorize('instructor', 'admin'),
  [body('incidents').isArray().withMessage('incidents must be an array')],
  async (req: Request, res: Response) => {
    try {
      await ExamService.deleteIncidents(req.body.incidents);
      invalidatePrefix('incidents_');
      return res.json({ success: true, message: 'Incidents deleted successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/export - Export submitted papers with optional filters (Instructor only)
 */
router.get(
  '/export',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const { sections, examId, status } = req.query as Record<string, string | undefined>;
      const papers = await ExamService.exportPapers(req.user!.userId, { sections, examId, status });
      return res.json(papers);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/export/incidents - Export incident reports with optional filters (Instructor only)
 */
router.get(
  '/export/incidents',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const { sections, examId, severity, archived } = req.query as Record<string, string | undefined>;
      const incidents = await ExamService.exportIncidents(req.user!.userId, { sections, examId, severity, archived });
      return res.json(incidents);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/export/options - Get available filter options for export (Instructor only)
 */
router.get(
  '/export/options',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const options = await ExamService.getExportOptions(req.user!.userId);
      return res.json(options);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/submitted-papers - Get all submitted exam papers (Instructor only)
 */
router.get(
  '/submitted-papers',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const cacheKey = `submitted_papers_${req.user!.userId}`;
      const papers = await getOrSet(cacheKey, 120, () => ExamService.getSubmittedPapers(req.user!.userId));
      return res.json(papers);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/exams/:id - Get exam by ID
 */
router.get('/:id', optionalAuth, async (req: Request, res: Response) => {
  try {
    const isInstructor = req.user?.role === 'instructor' || req.user?.role === 'admin';
    
    // For instructors: try direct path first
    if (isInstructor) {
      const exam = await ExamService.getExamByIdForInstructor(req.params.id, req.user!.userId);
      if (exam) return res.json(exam);
    }
    
    // For students / guests: use collection group query
    const result = await ExamService.findExamById(req.params.id);
    if (!result) {
      return res.status(404).json({ error: 'Exam not found' });
    }

    const { exam } = result;

    // Instructors/admins can always view any exam
    if (isInstructor) {
      return res.json(exam);
    }

    // For students/guests: must be published or active
    if (!['published', 'active'].includes(exam.status)) {
      console.log(`[ExamGET] 403 status_check fail: exam.status="${exam.status}"`);
      return res.status(403).json({ error: 'This exam is not currently available' });
    }

    // Check date range
    const now = new Date();
    if (exam.startDate) {
      const start = new Date(exam.startDate);
      console.log(`[ExamGET] startDate="${exam.startDate}" parsed="${start}" now="${now}"`);
      if (start > now) {
        console.log(`[ExamGET] 403 startDate future: start=${start.toISOString()} now=${now.toISOString()}`);
        return res.status(403).json({ error: 'This exam has not started yet' });
      }
    }
    if (exam.endDate) {
      const end = new Date(exam.endDate);
      console.log(`[ExamGET] endDate="${exam.endDate}" parsed="${end}" now="${now}"`);
      if (end < now) {
        console.log(`[ExamGET] 403 endDate past: end=${end.toISOString()} now=${now.toISOString()}`);
        // Auto-mark as completed
        try {
          const db = (await import('../config/firebase')).getFirestore();
          await db
            .collection('examforge_users')
            .doc(exam.instructorId)
            .collection('published_exams')
            .doc(req.params.id)
            .update({ status: 'completed' });
          await db.collection('exams').doc(req.params.id).update({ status: 'completed' });
        } catch { /* best-effort */ }
        return res.status(403).json({ error: 'This exam has already ended' });
      }
    }

    return res.json(exam);
  } catch (error: any) {
    console.error('[Exams] GET /:id error:', error);
    return res.status(400).json({ error: error.message });
  }
});

/**
 * POST /api/exams/:id/clone - Clone an exam as a new draft (Instructor only)
 */
router.post(
  '/:id/clone',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const cloned = await ExamService.cloneExam(req.params.id, req.user!.userId);
      return res.status(201).json(cloned);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * PUT /api/exams/:id/republish - Republish a completed/archived exam (Instructor only)
 */
router.put(
  '/:id/republish',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const exam = await ExamService.republishExam(req.params.id, req.user!.userId);
      invalidatePrefix('exams_list_');
      return res.json(exam);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * PUT /api/exams/:id/complete - Manually complete an exam (Instructor only)
 */
router.put(
  '/:id/complete',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      const exam = await ExamService.completeExam(req.params.id, req.user!.userId);
      invalidatePrefix('exams_list_');
      return res.json(exam);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

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
      invalidatePrefix('exams_list_');
      return res.json(exam);
    } catch (error: any) {
      console.error('[Exams] PUT /:id error:', error.message, error.stack);
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
      invalidatePrefix('exams_list_');
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
  authenticate,
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
  authenticate,
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
 * Optional query param: ?studentId=xxx for direct path lookup (instructor reviewing a student's paper)
 */
router.get('/attempts/:id', optionalAuth, async (req: Request, res: Response) => {
  try {
    const userId = req.user?.userId || `guest_${req.params.id}`;
    const studentId = req.query.studentId as string | undefined;
    const attempt = await ExamService.getExamAttempt(
      req.params.id,
      userId,
      studentId
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

export default router;
