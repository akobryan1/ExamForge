/**
 * TypeScript type definitions for ExamForge
 */

/* ============================================
   USER & AUTHENTICATION
   ============================================ */

export interface User {
  id: string;
  email: string;
  username?: string;
  displayName?: string;
  role: 'instructor' | 'student' | 'admin';
  createdAt: Date;
}

export interface AuthState {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;
}

export interface LoginCredentials {
  email: string;
  password: string;
}

export interface SignupCredentials {
  email: string;
  password: string;
  username: string;
  displayName?: string;
  role?: 'instructor' | 'student' | 'admin';
}

/* ============================================
   EXAM MODELS
   ============================================ */

export type QuestionType = 
  | 'MCQ' 
  | 'TrueFalse' 
  | 'Essay' 
  | 'ModifiedTrueFalse' 
  | 'Enumeration' 
  | 'Identification';

export interface ExamStructure {
  sectionName: string;
  description: string;
  points: number;
  questionCount: number;
  isComplete: boolean;
}

export interface ExamContent {
  contentId: string;
  examId: string;
  question: string;
  answer: string;
  explanation: string;
  mediaUrl?: string;
  questionType: QuestionType;
  points: number;
  options: string[];
  correctAnswer?: string;
  isComplete: boolean;
}

export interface PublishedExam {
  id: string;
  title: string;
  subject: string;
  startTime: Date;
  endTime: Date;
  examDuration: number; // in minutes
  publishedDate: Date;
  createdBy: string;
  ownerUserId: string;
  examUrl: string;
  passingScorePercentage: number;
  structures: ExamStructure[];
  contents: ExamContent[];
  loginConfig?: LoginConfig;
  antiCheat?: AntiCheatConfig;
  lifecycleStatus: 'Draft' | 'Published' | 'Active' | 'Completed';
  status: string;
  timesUsed: number;
  averageDifficulty?: number;
  passRate: number;
}

export interface LoginConfig {
  requireStudentId: boolean;
  requireName: boolean;
  requireEmail: boolean;
  requireSection: boolean;
}

export interface AntiCheatConfig {
  enabled: boolean;
  policy: 'Warning' | 'DeductPoints' | 'AutoSubmit';
  pointsDeducted?: number;
  maxViolations?: number;
}

/* ============================================
   SUBMISSIONS & GRADING
   ============================================ */

export interface ExamSubmission {
  id: string;
  examId: string;
  examTitle: string;
  studentId: string;
  studentName: string;
  studentEmail?: string;
  answers: StudentAnswer[];
  totalScore: number;
  totalPossiblePoints: number;
  percentageScore: number;
  startTime: Date;
  endTime?: Date;
  submittedAt: Date;
  status: 'InProgress' | 'Submitted' | 'Graded';
  violationCount: number;
  pointsDeducted: number;
}

export interface StudentAnswer {
  questionId: string;
  questionType: QuestionType;
  answer: string;
  pointsAwarded?: number;
  maxPoints: number;
  isCorrect?: boolean;
  feedback?: string;
}

export interface GradingQueueItem {
  id: string;
  examId: string;
  examTitle: string;
  submissionId: string;
  studentName: string;
  studentId: string;
  questionNumber: number;
  questionText: string;
  questionType: string;
  studentAnswer: string;
  maxPoints: number;
  pointsAwarded?: number;
  status: 'Pending' | 'Graded';
  submittedAt: Date;
  gradedAt?: Date;
  gradedBy?: string;
  feedback?: string;
}

export interface EssayAutoGradeResult {
  aiScore: number;
  maxScore: number;
  confidencePercent: number;
  flagForReview: boolean;
  justification: string;
  rubricBreakdown: RubricScoreRow[];
  usedDeepSeek: boolean;
  providerStatus: string;
}

export interface RubricScoreRow {
  criterion: string;
  weight: number;
  maxPoints: number;
  pointsAwarded: number;
  reason: string;
}

/* ============================================
   REAL-TIME SESSION
   ============================================ */

export interface ExamSession {
  id: string;
  examId: string;
  startedAt: Date;
  endedAt?: Date;
  status: 'Running' | 'Paused' | 'Ended';
  participants: Record<string, StudentSessionState>;
  lastHeartbeat: Date;
  totalParticipants: number;
  onlineCount: number;
}

export interface StudentSessionState {
  studentId: string;
  studentName: string;
  connectionStatus: 'Online' | 'Offline' | 'Disconnected';
  progressPercent: number;
  questionsAnswered: number;
  timeRemaining: number; // seconds
  flagCount: number;
  lastHeartbeat: Date;
  ipAddress: string;
}

export interface IntegrityIncident {
  id: string;
  examId: string;
  studentId: string;
  studentName: string;
  incidentType: 'TAB_SWITCH' | 'FOCUS_LOSS' | 'COPY_PASTE' | 'OTHER';
  severity: 'Low' | 'Medium' | 'High';
  timestamp: Date;
  details: string;
  status: 'Pending' | 'Reviewed' | 'Dismissed';
}

/* ============================================
   ANALYTICS
   ============================================ */

export interface ClassOverviewMetrics {
  classAverage: number;
  passRate: number;
  completionRate: number;
  totalStudents: number;
  passingStudents: number;
}

export interface QuestionAnalytics {
  questionId: string;
  questionText: string;
  questionType: QuestionType;
  averageScore: number;
  difficultyLevel: 'Easy' | 'Medium' | 'Hard';
  correctAnswerRate: number;
  totalAttempts: number;
}
