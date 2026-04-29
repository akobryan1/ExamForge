import { apiClient } from './apiClient';
import type {
  Exam,
  Question,
  ExamAttempt,
  ExamAnswer,
  CreateExamFormData,
  CreateQuestionFormData,
} from '../types/exam';

class ExamServiceClass {
  /**
   * Get all exams (instructor: their exams, student: available exams)
   */
  async getExams(): Promise<Exam[]> {
    const response = await apiClient.get('/api/exams');
    return response.data;
  }

  /**
   * Get exam by ID
   */
  async getExamById(examId: string): Promise<Exam> {
    const response = await apiClient.get(`/api/exams/${examId}`);
    return response.data;
  }

  /**
   * Create a new exam (Instructor only)
   */
  async createExam(data: CreateExamFormData): Promise<Exam> {
    const response = await apiClient.post('/api/exams', data);
    return response.data;
  }

  /**
   * Update an exam (Instructor only)
   */
  async updateExam(examId: string, data: Partial<CreateExamFormData>): Promise<Exam> {
    const response = await apiClient.put(`/api/exams/${examId}`, data);
    return response.data;
  }

  /**
   * Delete an exam (Instructor only)
   */
  async deleteExam(examId: string): Promise<void> {
    await apiClient.delete(`/api/exams/${examId}`);
  }

  /**
   * Publish an exam (Instructor only)
   */
  async publishExam(examId: string): Promise<Exam> {
    return this.updateExam(examId, { status: 'published' } as any);
  }

  /**
   * Archive an exam (Instructor only)
   */
  async archiveExam(examId: string): Promise<Exam> {
    return this.updateExam(examId, { status: 'archived' } as any);
  }

  /**
   * Get questions for an exam
   */
  async getExamQuestions(examId: string): Promise<Question[]> {
    const response = await apiClient.get(`/api/exams/${examId}/questions`);
    return response.data;
  }

  /**
   * Create a question (Instructor only)
   */
  async createQuestion(examId: string, data: CreateQuestionFormData): Promise<Question> {
    const response = await apiClient.post(`/api/exams/${examId}/questions`, data);
    return response.data;
  }

  /**
   * Update a question (Instructor only)
   */
  async updateQuestion(questionId: string, examId: string, data: Partial<CreateQuestionFormData>): Promise<Question> {
    const response = await apiClient.put(`/api/exams/questions/${questionId}`, { ...data, examId });
    return response.data;
  }

  /**
   * Delete a question (Instructor only)
   */
  async deleteQuestion(questionId: string, examId: string): Promise<void> {
    await apiClient.delete(`/api/exams/questions/${questionId}?examId=${examId}`);
  }

  /**
   * Start an exam attempt (Student)
   */
  async startExam(examId: string, accessCode?: string): Promise<ExamAttempt> {
    const response = await apiClient.post(`/api/exams/${examId}/start`, { accessCode });
    return response.data;
  }

  /**
   * Submit an answer (Student)
   */
  async submitAnswer(
    attemptId: string,
    questionId: string,
    answer: string | string[],
    timeSpent?: number
  ): Promise<ExamAnswer> {
    const response = await apiClient.post(`/api/exams/attempts/${attemptId}/answers`, {
      questionId,
      answer,
      timeSpent,
    });
    return response.data;
  }

  /**
   * Submit exam (complete attempt) (Student)
   */
  async submitExam(attemptId: string): Promise<ExamAttempt> {
    const response = await apiClient.post(`/api/exams/attempts/${attemptId}/submit`);
    return response.data;
  }

  /**
   * Get exam attempt with answers
   */
  async getExamAttempt(attemptId: string): Promise<ExamAttempt & { answers: ExamAnswer[] }> {
    const response = await apiClient.get(`/api/exams/attempts/${attemptId}`);
    return response.data;
  }

  /**
   * Get grading queue for essay questions (Instructor only)
   */
  async getGradingQueue(): Promise<any[]> {
    const response = await apiClient.get('/api/exams/grading/queue');
    return response.data;
  }

  /**
   * Submit grade for a question (Instructor only)
   */
  async gradeQuestion(
    attemptId: string,
    questionId: string,
    earnedPoints: number,
    feedback?: string
  ): Promise<void> {
    await apiClient.post('/api/exams/grading/submit', {
      attemptId,
      questionId,
      earnedPoints,
      feedback,
    });
  }

  /**
   * Get analytics for an exam (Instructor only)
   */
  async getExamAnalytics(examId: string): Promise<any> {
    const response = await apiClient.get(`/api/exams/analytics/${examId}`);
    return response.data;
  }
}

export const ExamService = new ExamServiceClass();
