import { getAuth, getFirestore } from '../config/firebase';
import { FieldValue } from 'firebase-admin/firestore';

export interface StudentDocument {
  uid: string;
  name: string;
  studentId: string;
  section: string;
  year: string;
  course: string;
  email: string;
  registeredAt: Date;
}

export interface RegistrationFieldDoc {
  sections: string[];
  years: string[];
  courses: string[];
  updatedAt: Date;
}

export class StudentService {
  /**
   * Create a Firebase Auth user and store profile in instructor's students subcollection
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
  }): Promise<StudentDocument> {
    const auth = getAuth();
    const db = getFirestore();

    // 1. Create Firebase Auth user
    const userRecord = await auth.createUser({
      email: data.email,
      password: data.password,
      displayName: data.name,
    });

    const uid = userRecord.uid;

    // 2. Write profile to instructor's students subcollection
    const studentData = {
      uid,
      name: data.name,
      studentId: data.studentId,
      section: data.section,
      year: data.year,
      course: data.course,
      email: data.email,
      registeredAt: FieldValue.serverTimestamp(),
    };

    await db
      .collection('examforge_users')
      .doc(data.instructorId)
      .collection('students')
      .doc(uid)
      .set(studentData);

    // 3. Also write a student profile doc so they can log in and be recognized globally
    await db.collection('examforge_users').doc(uid).set({
      username: data.studentId,
      email: data.email,
      displayName: data.name,
      role: 'student',
      instructorId: data.instructorId,
      studentId: data.studentId,
      section: data.section,
      year: data.year,
      course: data.course,
      createdAt: new Date(),
      updatedAt: new Date(),
    });

    return {
      uid,
      name: data.name,
      studentId: data.studentId,
      section: data.section,
      year: data.year,
      course: data.course,
      email: data.email,
      registeredAt: new Date(),
    };
  }

  /**
   * Get all registered students for an instructor
   */
  static async getStudents(instructorId: string): Promise<StudentDocument[]> {
    const db = getFirestore();

    const snapshot = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('students')
      .orderBy('registeredAt', 'desc')
      .get();

    return snapshot.docs.map(doc => {
      const data = doc.data();
      return {
        uid: doc.id,
        name: data.name || '',
        studentId: data.studentId || '',
        section: data.section || '',
        year: data.year || '',
        course: data.course || '',
        email: data.email || '',
        registeredAt: data.registeredAt?.toDate() || new Date(),
      } as StudentDocument;
    });
  }

  /**
   * Save registration field options for an instructor
   */
  static async saveRegistrationFields(
    instructorId: string,
    fields: { sections: string[]; years: string[]; courses: string[] }
  ): Promise<void> {
    const db = getFirestore();

    await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('registration_config')
      .doc('fields')
      .set({
        sections: fields.sections,
        years: fields.years,
        courses: fields.courses,
        updatedAt: FieldValue.serverTimestamp(),
      });
  }

  /**
   * Get registration field options for an instructor
   */
  static async getRegistrationFields(instructorId: string): Promise<RegistrationFieldDoc> {
    const db = getFirestore();

    const doc = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('registration_config')
      .doc('fields')
      .get();

    if (!doc.exists) {
      return { sections: [], years: [], courses: [], updatedAt: new Date() };
    }

    const data = doc.data()!;
    return {
      sections: data.sections || [],
      years: data.years || [],
      courses: data.courses || [],
      updatedAt: data.updatedAt?.toDate() || new Date(),
    };
  }

  /**
   * Get instructor ID from a student's profile (for login lookup)
   */
  static async getStudentInstructor(studentUid: string): Promise<string | null> {
    const db = getFirestore();

    const doc = await db.collection('examforge_users').doc(studentUid).get();
    if (!doc.exists) return null;

    const data = doc.data();
    return data?.instructorId || null;
  }
}
