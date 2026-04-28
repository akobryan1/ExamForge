/**
 * Frontend Exam Types
 * Matches backend exam types for API communication
 */

export enum QuestionType {
  MULTIPLE_CHOICE = 'multiple_choice',
  TRUE_FALSE = 'true_false',
  SHORT_ANSWER = 'short_answer',
  ESSAY = 'essay',
  FILL_IN_BLANK = 'fill_in_blank',
  MATCHING = 'matching',
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
  matchingPairs?: { left: string; right: string }[];
  tags?: string[];
  imageUrl?: string;
  timeLimit?: number;
  createdAt: Date;
  updatedAt: Date;
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
  subject?: string;
  grade?: string;
  tags?: string[];
  questionCount: number;
  attemptCount: number;
  averageScore?: number;
  createdAt: Date;
  updatedAt: Date;
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
  startedAt: Date;
  submittedAt?: Date;
  timeSpent?: number;
  answers: ExamAnswer[];
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
  matchingPairs?: { left: string; right: string }[];
  tags?: string[];
  imageUrl?: string;
  timeLimit?: number;
}
