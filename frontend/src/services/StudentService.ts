import apiClient from './apiClient';

export interface StudentData {
  uid: string;
  name: string;
  studentId: string;
  section: string;
  year: string;
  course: string;
  email: string;
  registeredAt: string | Date;
}

export interface RegistrationFields {
  sections: string[];
  years: string[];
  courses: string[];
}

export class StudentService {
  /**
   * Register a new examinee (public — no auth needed)
   */
  static async registerStudent(data: {
    instructorId: string;
    name: string;
    studentId: string;
    section: string;
    year: string;
    course: string;
    email: string;
    password: string;
  }): Promise<{ message: string; student: StudentData }> {
    const response = await apiClient.post(`/api/students/register/${data.instructorId}`, data);
    return response.data;
  }

  /**
   * Get all registered students (instructor only)
   */
  static async getStudents(instructorId: string): Promise<StudentData[]> {
    const response = await apiClient.get(`/api/students/${instructorId}`);
    return response.data;
  }

  /**
   * Get registration field options (public)
   */
  static async getRegistrationFields(instructorId: string): Promise<RegistrationFields> {
    const response = await apiClient.get(`/api/students/fields/${instructorId}`);
    return response.data;
  }

  /**
   * Save registration field options (instructor only)
   */
  static async saveRegistrationFields(
    instructorId: string,
    fields: RegistrationFields
  ): Promise<void> {
    await apiClient.put(`/api/students/fields/${instructorId}`, fields);
  }
}
