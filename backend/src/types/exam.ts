/**
 * Exam Management Types
 * Defines the core data structures for exams, questions, and related entities
 */

export enum QuestionType {
  MULTIPLE_CHOICE = 'multiple_choice',
  TRUE_FALSE = 'true_false',
  MODIFIED_TRUE_FALSE = 'modified_true_false',
  ESSAY = 'essay',
  IDENTIFICATION = 'identification',
  ENUMERATION = 'enumeration',
}

export enum ExamStatus {
  DRAFT = 'draft',
  PUBLISHED = 'published',
  ACTIVE = 'active',
  COMPLETED = 'completed',
  ARCHIVED = 'archived',
}

export enum DifficultyLevel {
  EASY = 'easy',
  MEDIUM = 'medium',
  HARD = 'hard',
}

// Answer choice for multiple choice questions
export interface AnswerChoice {
  id: string;
  text: string;
  isCorrect: boolean;
  order: number;
}

// Question entity
export interface Question {
  id: string;
  examId: string;
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  difficulty: DifficultyLevel;
  order: number;
  
  // Question type specific fields
  choices?: AnswerChoice[]; // For multiple choice
  correctAnswer?: string | string[]; // For true/false, modified_true_false, identification, enumeration
  
  // Metadata
  tags?: string[];
  imageUrl?: string;
  timeLimit?: number; // Time limit in seconds for this question
  
  // Timestamps
  createdAt: Date;
  updatedAt: Date;
}

// Retake configuration options
export interface RetakeConfiguration {
  enabled: boolean;
  maxRetakes?: number; // undefined = unlimited, 0 = no retakes, 1+ = specific limit
  requireApproval: boolean; // If true, students must request retake permission
  scoringMethod: 'best' | 'latest' | 'average'; // Which attempt to count
}

// Late submission configuration
export interface LateSubmissionConfiguration {
  policy: 'allowed' | 'disabled' | 'request_permission';
  gracePeriodMinutes?: number; // Grace period after deadline
  penaltyPoints?: number; // Point deduction (not percentage)
  penaltyInterval?: 'minute' | 'hour' | 'day'; // Penalty applied per interval
}

// Point deduction for proctoring violations
export interface ProctorPointDeductions {
  tabSwitch?: number;
  copyPaste?: number;
  rightClick?: number;
  exitFullscreen?: number;
  multipleDevices?: number;
  suspiciousBehavior?: number;
  generalViolation?: number; // Fallback for any violation
}

// Proctoring configuration
export interface ProctorConfiguration {
  enabled: boolean;
  enforceFullscreen: boolean;
  detectTabSwitch: boolean;
  detectCopyPaste: boolean;
  disableRightClick: boolean;
  pointDeductions?: ProctorPointDeductions;
  customRules?: string; // Custom proctoring instructions
}

// Exam entity
export interface Exam {
  id: string;
  title: string;
  description: string;
  instructorId: string;
  instructorName: string;
  
  // Exam settings
  status: ExamStatus;
  totalPoints: number;
  passingScore: number;
  timeLimit?: number; // Total time limit in minutes
  shuffleQuestions: boolean;
  shuffleAnswers: boolean;
  showResults: boolean;
  allowReview: boolean;
  
  // Scheduling
  startDate?: Date;
  endDate?: Date;
  
  // Access control
  accessCode?: string;
  allowedStudentIds?: string[];
  allowGuestAccess?: boolean; // Allow non-registered students
  sections?: string[]; // Class/section filtering
  
  // Retake configuration
  retakeConfig?: RetakeConfiguration;
  
  // Late submission
  lateSubmissionConfig?: LateSubmissionConfiguration;
  
  // Proctoring
  proctorConfig?: ProctorConfiguration;
  
  // Custom instructions
  customInstructions?: string; // User-defined exam rules/instructions
  showRulesBeforeExam?: boolean; // Display rules and anti-cheat settings before starting
  
  // Metadata
  subject?: string;
  grade?: string;
  tags?: string[];
  
  // Statistics
  questionCount: number;
  attemptCount: number;
  averageScore?: number;
  
  // Timestamps
  createdAt: Date;
  updatedAt: Date;
}

// Student exam attempt
export interface ExamAttempt {
  id: string;
  examId: string;
  studentId: string;
  studentName: string;
  
  // Attempt details
  status: 'in_progress' | 'submitted' | 'graded';
  score?: number;
  percentage?: number;
  passed?: boolean;
  
  // Retake tracking
  attemptNumber?: number; // 1 for first attempt, 2+ for retakes
  isRetake?: boolean;
  retakeRequested?: boolean;
  retakeApproved?: boolean;
  retakeApprovedBy?: string;
  
  // Late submission
  isLateSubmission?: boolean;
  latePenaltyApplied?: number; // Points deducted for late submission
  minutesLate?: number;
  
  // Proctoring violations
  violations?: ProctorViolation[];
  violationPenalty?: number; // Total points deducted for violations
  
  // Timing
  startedAt: Date;
  submittedAt?: Date;
  timeSpent?: number; // Time spent in seconds
  
  // Answers
  answers: ExamAnswer[];
  
  // Metadata
  ipAddress?: string;
  userAgent?: string;
  isGuest?: boolean; // True if taken by guest user
  
  // Timestamps
  createdAt: Date;
  updatedAt: Date;
}

// Proctoring violation record
export interface ProctorViolation {
  id: string;
  type: 'tab_switch' | 'copy_paste' | 'right_click' | 'exit_fullscreen' | 'multiple_devices' | 'suspicious_behavior' | 'general';
  timestamp: Date;
  description?: string;
  pointsDeducted?: number;
  severity: 'low' | 'medium' | 'high';
}

// Student answer to a question
export interface ExamAnswer {
  id: string;
  attemptId: string;
  questionId: string;
  
  // Answer data
  answer: string | string[]; // Student's answer
  isCorrect?: boolean;
  pointsEarned?: number;
  
  // Grading (for essays and short answers)
  feedback?: string;
  gradedBy?: string;
  gradedAt?: Date;
  
  // Timing
  timeSpent?: number; // Time spent on this question in seconds
  
  // Timestamps
  createdAt: Date;
  updatedAt: Date;
}

// DTOs for API requests/responses
export interface CreateExamDto {
  title: string;
  description: string;
  subject?: string;
  grade?: string;
  timeLimit?: number;
  passingScore: number;
  shuffleQuestions?: boolean;
  shuffleAnswers?: boolean;
  showResults?: boolean;
  allowReview?: boolean;
  startDate?: Date;
  endDate?: Date;
  accessCode?: string;
  allowGuestAccess?: boolean;
  sections?: string[];
  tags?: string[];
  retakeConfig?: RetakeConfiguration;
  lateSubmissionConfig?: LateSubmissionConfiguration;
  proctorConfig?: ProctorConfiguration;
  customInstructions?: string;
  showRulesBeforeExam?: boolean;
}

export interface UpdateExamDto extends Partial<CreateExamDto> {
  status?: ExamStatus;
}

export interface CreateQuestionDto {
  examId: string;
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  difficulty: DifficultyLevel;
  choices?: Omit<AnswerChoice, 'id'>[];
  correctAnswer?: string | string[];
  tags?: string[];
  imageUrl?: string;
  timeLimit?: number;
}

export interface UpdateQuestionDto extends Partial<Omit<CreateQuestionDto, 'examId'>> {}

export interface SubmitAnswerDto {
  attemptId: string;
  questionId: string;
  answer: string | string[];
  timeSpent?: number;
}

export interface StartExamDto {
  examId: string;
  accessCode?: string;
}

export interface SubmitExamDto {
  attemptId: string;
}

// ====================================
// Notification System Types
// ====================================
export enum NotificationType {
  EXAM_PUBLISHED = 'exam_published',
  GRADE_RELEASED = 'grade_released',
  RETAKE_APPROVED = 'retake_approved',
  RETAKE_DENIED = 'retake_denied',
  EXAM_REMINDER = 'exam_reminder',
  CUSTOM = 'custom',
}

export interface Notification {
  id: string;
  type: NotificationType;
  recipientId: string; // Student ID
  senderId: string; // Instructor ID
  title: string;
  message: string;
  examId?: string;
  read: boolean;
  createdAt: Date;
}

export interface CreateNotificationDto {
  type: NotificationType;
  recipients: string[]; // List of student IDs or 'all' or section names
  title: string;
  message: string;
  examId?: string;
}

// ====================================
// Question Bank Types
// ====================================
export interface QuestionBankItem {
  id: string;
  instructorId: string;
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  difficulty: DifficultyLevel;
  choices?: AnswerChoice[];
  correctAnswer?: string | string[];
  category?: string;
  tags?: string[];
  imageUrl?: string;
  usageCount: number; // How many times used in exams
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateQuestionBankItemDto {
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  difficulty: DifficultyLevel;
  choices?: Omit<AnswerChoice, 'id'>[];
  correctAnswer?: string | string[];
  category?: string;
  tags?: string[];
  imageUrl?: string;
}

// ====================================
// Exam Template Types
// ====================================
export interface ExamTemplate {
  id: string;
  instructorId: string;
  name: string;
  description: string;
  category?: string;
  settings: {
    timeLimit?: number;
    passingScore: number;
    shuffleQuestions: boolean;
    shuffleAnswers: boolean;
    showResults: boolean;
    allowReview: boolean;
    retakeConfig?: RetakeConfiguration;
    lateSubmissionConfig?: LateSubmissionConfiguration;
    proctorConfig?: ProctorConfiguration;
  };
  questions: Omit<Question, 'id' | 'examId' | 'createdAt' | 'updatedAt'>[]; // Template questions
  usageCount: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateExamTemplateDto {
  name: string;
  description: string;
  category?: string;
  examId?: string; // Optional: create from existing exam
}

// ====================================
// Student Groups/Cohorts Types
// ====================================
export interface StudentGroup {
  id: string;
  instructorId: string;
  name: string;
  description?: string;
  section?: string;
  studentIds: string[];
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateStudentGroupDto {
  name: string;
  description?: string;
  section?: string;
  studentIds: string[];
}

export interface BulkEnrollDto {
  groupId: string;
  examId: string;
}

// ====================================
// Retake Request Types
// ====================================
export interface RetakeRequest {
  id: string;
  examId: string;
  studentId: string;
  studentName: string;
  attemptId: string; // The attempt they want to retake
  reason: string;
  status: 'pending' | 'approved' | 'denied';
  reviewedBy?: string;
  reviewedAt?: Date;
  reviewNotes?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateRetakeRequestDto {
  examId: string;
  attemptId: string;
  reason: string;
}

export interface ReviewRetakeRequestDto {
  requestId: string;
  status: 'approved' | 'denied';
  reviewNotes?: string;
}

// ====================================
// Enhanced Grading Types
// ====================================
export interface GradingRubric {
  id: string;
  instructorId: string;
  name: string;
  description?: string;
  criteria: RubricCriterion[];
  totalPoints: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface RubricCriterion {
  id: string;
  name: string;
  description: string;
  points: number;
  levels?: RubricLevel[];
}

export interface RubricLevel {
  name: string;
  description: string;
  points: number;
}

export interface PartialCreditRule {
  questionId: string;
  keywords: string[]; // Keywords to match for partial credit
  partialPoints: number; // Points awarded if keywords match
}

export interface AIGradingSuggestion {
  id: string;
  attemptId: string;
  questionId: string;
  suggestedScore: number;
  rubricBreakdown?: { criterion: string; points: number }[];
  strengths: string[];
  weaknesses: string[];
  feedback: string;
  confidence: number; // 0-1, how confident the AI is
  approved: boolean;
  reviewedBy?: string;
  reviewedAt?: Date;
  createdAt: Date;
}

export interface CreateAIGradingSuggestionDto {
  attemptId: string;
  questionId: string;
  apiKey: string; // OpenAI or Claude API key
}

// ====================================
// Analytics Types
// ====================================
export interface ItemAnalysis {
  questionId: string;
  questionText: string;
  difficulty: number; // Percentage who got it wrong
  discrimination: number; // How well it separates high/low performers
  distractorAnalysis?: { choice: string; count: number }[];
}

export interface ExamAnalyticsExtended {
  examId: string;
  examTitle: string;
  totalAttempts: number;
  averageScore: number;
  medianScore: number;
  highestScore: number;
  lowestScore: number;
  passRate: number;
  averageTimeSpent: number;
  scoreDistribution: { range: string; count: number }[];
  questionPerformance: ItemAnalysis[];
  timeAnalysis: { questionId: string; averageTime: number }[];
  commonMistakes: { questionId: string; incorrectAnswers: { answer: string; count: number }[] }[];
}

// ====================================
// Student Registration Types
// ====================================
export interface StudentRegistrationDto {
  email: string;
  studentId: string; // Unique student ID number
  studentName: string;
  section?: string;
  password: string;
}

export interface GuestAccessDto {
  examId: string;
  guestName: string;
  guestEmail?: string;
  accessCode?: string;
}
