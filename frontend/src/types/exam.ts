/**
 * Frontend Exam Types
 * Matches backend exam types for API communication
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

export interface AnswerChoice {
  id: string;
  text: string;
  isCorrect: boolean;
  order: number;
}

export interface Question {
  id: string;
  examId: string;
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  difficulty: DifficultyLevel;
  order: number;
  choices?: AnswerChoice[];
  correctAnswer?: string | string[];
  tags?: string[];
  imageUrl?: string;
  timeLimit?: number;
  createdAt: Date;
  updatedAt: Date;
}

// Retake configuration
export interface RetakeConfiguration {
  enabled: boolean;
  maxRetakes?: number;
  requireApproval: boolean;
  scoringMethod: 'best' | 'latest' | 'average';
}

// Late submission configuration
export interface LateSubmissionConfiguration {
  policy: 'allowed' | 'disabled' | 'request_permission';
  gracePeriodMinutes?: number;
  penaltyPoints?: number;
  penaltyInterval?: 'minute' | 'hour' | 'day';
}

// Point deduction for proctoring
export interface ProctorPointDeductions {
  tabSwitch?: number;
  copyPaste?: number;
  rightClick?: number;
  exitFullscreen?: number;
  multipleDevices?: number;
  suspiciousBehavior?: number;
  generalViolation?: number;
}

// Proctoring configuration
export interface ProctorConfiguration {
  enabled: boolean;
  enforceFullscreen: boolean;
  detectTabSwitch: boolean;
  detectCopyPaste: boolean;
  disableRightClick: boolean;
  pointDeductions?: ProctorPointDeductions;
  customRules?: string;
}

export interface Exam {
  id: string;
  title: string;
  description: string;
  instructorId: string;
  instructorName: string;
  status: ExamStatus;
  totalPoints: number;
  passingScore: number;
  timeLimit?: number;
  shuffleQuestions: boolean;
  shuffleAnswers: boolean;
  showResults: boolean;
  allowReview: boolean;
  startDate?: Date;
  endDate?: Date;
  accessCode?: string;
  allowedStudentIds?: string[];
  allowGuestAccess?: boolean;
  sections?: string[];
  retakeConfig?: RetakeConfiguration;
  lateSubmissionConfig?: LateSubmissionConfiguration;
  proctorConfig?: ProctorConfiguration;
  customInstructions?: string;
  showRulesBeforeExam?: boolean;
  subject?: string;
  grade?: string;
  tags?: string[];
  questionCount: number;
  attemptCount: number;
  averageScore?: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface ProctorViolation {
  id: string;
  type: 'tab_switch' | 'copy_paste' | 'right_click' | 'exit_fullscreen' | 'multiple_devices' | 'suspicious_behavior' | 'general';
  timestamp: Date;
  description?: string;
  pointsDeducted?: number;
  severity: 'low' | 'medium' | 'high';
}

export interface ExamAttempt {
  id: string;
  examId: string;
  studentId: string;
  studentName: string;
  status: 'in_progress' | 'submitted' | 'graded';
  score?: number;
  percentage?: number;
  passed?: boolean;
  attemptNumber?: number;
  isRetake?: boolean;
  retakeRequested?: boolean;
  retakeApproved?: boolean;
  retakeApprovedBy?: string;
  isLateSubmission?: boolean;
  latePenaltyApplied?: number;
  minutesLate?: number;
  violations?: ProctorViolation[];
  violationPenalty?: number;
  startedAt: Date;
  submittedAt?: Date;
  timeSpent?: number;
  answers: ExamAnswer[];
  isGuest?: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface ExamAnswer {
  id: string;
  attemptId: string;
  questionId: string;
  answer: string | string[];
  isCorrect?: boolean;
  pointsEarned?: number;
  feedback?: string;
  gradedBy?: string;
  gradedAt?: Date;
  timeSpent?: number;
  createdAt: Date;
  updatedAt: Date;
}

// Form data types
export interface CreateExamFormData {
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
}

export interface CreateQuestionFormData {
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
