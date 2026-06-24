import apiClient from '../apiClient';

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

  static async getRegistrationFields(instructorId: string): Promise<RegistrationFields> {
    const response = await apiClient.get(`/api/students/fields/${instructorId}`);
    return response.data;
  }
}
