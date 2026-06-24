import apiClient from '../apiClient';

export class ExamService {
  static async getExamById(examId: string): Promise<any> {
    const response = await apiClient.get(`/api/exams/${examId}`);
    return response.data;
  }

  static async startExam(examId: string, accessCode?: string): Promise<any> {
    const response = await apiClient.post(`/api/exams/${examId}/start`, { accessCode });
    return response.data;
  }

  static async getExamAttempt(attemptId: string): Promise<any> {
    const response = await apiClient.get(`/api/exams/attempts/${attemptId}`);
    return response.data;
  }

  static async submitAnswer(attemptId: string, questionId: string, answer: string | string[], timeSpent?: number): Promise<any> {
    const response = await apiClient.post(`/api/exams/attempts/${attemptId}/answers`, { questionId, answer, timeSpent });
    return response.data;
  }

  static async submitExam(attemptId: string): Promise<any> {
    const response = await apiClient.post(`/api/exams/attempts/${attemptId}/submit`);
    return response.data;
  }

  static async getExamQuestions(examId: string): Promise<any[]> {
    const response = await apiClient.get(`/api/exams/${examId}/questions`);
    return response.data;
  }
}
