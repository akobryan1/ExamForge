import { Router, Request, Response } from 'express';
import { body, validationResult } from 'express-validator';
import { StudentService } from '../services/StudentService';
import { authenticate, authorize } from '../middleware/auth';
import { getOrSet, invalidatePrefix } from '../utils/cache';
import { logActivity } from '../utils/activityLogger';

const router = Router();

/**
 * POST /api/students/register/:instructorId
 * Register a new examinee (public — called from registration form link)
 */
router.post(
  '/register/:instructorId',
  [
    body('name').notEmpty().withMessage('Name is required'),
    body('studentId').notEmpty().withMessage('Student ID is required'),
    body('section').notEmpty().withMessage('Section is required'),
    body('year').notEmpty().withMessage('Year is required'),
    body('course').notEmpty().withMessage('Course is required'),
    body('email').isEmail().withMessage('Valid email is required'),
    body('password').isLength({ min: 6 }).withMessage('Password must be at least 6 characters'),
  ],
  async (req: Request, res: Response) => {
    try {
      const errors = validationResult(req);
      if (!errors.isEmpty()) {
        return res.status(400).json({ error: errors.array()[0].msg });
      }

      const student = await StudentService.registerStudent({
        instructorId: req.params.instructorId,
        name: req.body.name,
        studentId: req.body.studentId,
        section: req.body.section,
        year: req.body.year,
        course: req.body.course,
        email: req.body.email,
        password: req.body.password,
      });

      await logActivity(req.params.instructorId, 'student.registered', `Student "${req.body.name}" (${req.body.studentId}) registered`, { studentId: req.body.studentId, studentName: req.body.name });
      return res.status(201).json({ message: 'Registration successful', student });
    } catch (error: any) {
      console.error('[Students] Register error:', error);

      if (error.code === 'auth/email-already-exists') {
        return res.status(409).json({ error: 'This email is already registered' });
      }
      if (error.code === 'auth/invalid-email') {
        return res.status(400).json({ error: 'Invalid email address' });
      }

      return res.status(400).json({ error: error.message || 'Registration failed' });
    }
  }
);

/**
 * GET /api/students/:instructorId
 * Get all registered students (instructor only)
 */
router.get(
  '/:instructorId',
  authenticate,
  authorize('instructor', 'admin'),
  async (req: Request, res: Response) => {
    try {
      // Ensure the instructor can only view their own students
      if (req.user!.userId !== req.params.instructorId && req.user!.role !== 'admin') {
        return res.status(403).json({ error: 'Unauthorized' });
      }

      const cacheKey = `students_${req.params.instructorId}`;
      const students = await getOrSet(cacheKey, 120, () => StudentService.getStudents(req.params.instructorId));
      return res.json(students);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * GET /api/students/fields/:instructorId
 * Get registration field options (public — used by registration form)
 */
router.get(
  '/fields/:instructorId',
  async (req: Request, res: Response) => {
    try {
      const fields = await StudentService.getRegistrationFields(req.params.instructorId);
      return res.json(fields);
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

/**
 * PUT /api/students/fields/:instructorId
 * Save registration field options (instructor only)
 */
router.put(
  '/fields/:instructorId',
  authenticate,
  authorize('instructor', 'admin'),
  [
    body('sections').isArray().withMessage('Sections must be an array'),
    body('years').isArray().withMessage('Years must be an array'),
    body('courses').isArray().withMessage('Courses must be an array'),
  ],
  async (req: Request, res: Response) => {
    try {
      if (req.user!.userId !== req.params.instructorId && req.user!.role !== 'admin') {
        return res.status(403).json({ error: 'Unauthorized' });
      }

      await StudentService.saveRegistrationFields(req.params.instructorId, {
        sections: req.body.sections,
        years: req.body.years,
        courses: req.body.courses,
      });

      invalidatePrefix(`students_${req.params.instructorId}`);
      return res.json({ message: 'Fields saved successfully' });
    } catch (error: any) {
      return res.status(400).json({ error: error.message });
    }
  }
);

export default router;
