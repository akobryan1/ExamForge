/**
 * Exam Management Types
 * Defines the core data structures for exams, questions, and related entities
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
  correctAnswer?: string | string[]; // For short answer, true/false, fill-in-blank
  matchingPairs?: { left: string; right: string }[]; // For matching
  
  // Metadata
  tags?: string[];
  imageUrl?: string;
  timeLimit?: number; // Time limit in seconds for this question
  
  // Timestamps
  createdAt: Date;
  updatedAt: Date;
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
  
  // Timing
  startedAt: Date;
  submittedAt?: Date;
  timeSpent?: number; // Time spent in seconds
  
  // Answers
  answers: ExamAnswer[];
  
  // Metadata
  ipAddress?: string;
  userAgent?: string;
  
  // Timestamps
  createdAt: Date;
  updatedAt: Date;
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
  matchingPairs?: { left: string; right: string }[];
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
