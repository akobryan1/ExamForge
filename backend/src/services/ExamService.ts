import { getFirestore } from '../config/firebase';
import { FieldValue, Timestamp } from '@google-cloud/firestore';
import {
  Exam,
  Question,
  ExamAttempt,
  ExamAnswer,
  CreateExamDto,
  UpdateExamDto,
  CreateQuestionDto,
  UpdateQuestionDto,
  StartExamDto,
  SubmitAnswerDto,
  SubmitExamDto,
  ExamStatus,
} from '../types';

export class ExamService {
  /**
   * Create a new exam in instructor's published_exams subcollection
   */
  static async createExam(instructorId: string, instructorName: string, data: CreateExamDto): Promise<Exam> {
    const db = getFirestore();

    const examData = {
      title: data.title,
      description: data.description,
      subject: data.subject || '',
      grade: data.grade || '',
      instructorId,
      instructorName,
      status: 'draft' as ExamStatus,
      startDate: data.startDate ? Timestamp.fromDate(new Date(data.startDate)) : null,
      endDate: data.endDate ? Timestamp.fromDate(new Date(data.endDate)) : null,
      timeLimit: data.timeLimit || null,
      passingScore: data.passingScore || 70,
      shuffleQuestions: data.shuffleQuestions ?? false,
      shuffleAnswers: data.shuffleAnswers ?? false,
      showResults: data.showResults ?? true,
      allowReview: data.allowReview ?? true,
      accessCode: data.accessCode || null,
      allowGuestAccess: data.allowGuestAccess ?? (data.accessMethod === 'guest'),
      accessMethod: data.accessMethod || 'student_login',
      sections: data.sections || [],
      tags: data.tags || [],
      retakeConfig: data.retakeConfig || null,
      lateSubmissionConfig: data.lateSubmissionConfig || null,
      proctorConfig: data.proctorConfig || null,
      customInstructions: data.customInstructions || null,
      showRulesBeforeExam: data.showRulesBeforeExam ?? false,
      allowedStudentIds: [],
      questionCount: 0,
      totalPoints: 0,
      attemptCount: 0,
      averageScore: 0,
      createdAt: FieldValue.serverTimestamp(),
      updatedAt: FieldValue.serverTimestamp(),
    };

    const examRef = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .add(examData);

    // Also write to flat exams index for fast lookups (avoids collectionGroup index)
    await db.collection('exams').doc(examRef.id).set({
      instructorId,
      status: 'draft',
      title: data.title,
      examRef: examRef.path,
    });

    const examDoc = await examRef.get();
    const savedData = examDoc.data();
    if (!savedData) {
      // Firestore eventual consistency: use the data we already constructed
      console.warn(`[createExam] Re-fetch returned empty for ${examRef.id}, using local data`);
      return this.mapExamFromDb(examRef.id, { ...examData, createdAt: new Date(), updatedAt: new Date() }, instructorId);
    }
    return this.mapExamFromDb(examRef.id, savedData, instructorId);
  }

  /**
   * Find exam across all users using collection group query
   */
  static async findExamById(examId: string): Promise<{ exam: Exam; instructorId: string } | null> {
    const db = getFirestore();

    // Use flat exams index collection (no composite index needed)
    const indexDoc = await db.collection('exams').doc(examId).get();
    if (!indexDoc.exists) {
      console.warn(`[findExamById] No index doc found for exam ${examId}`);
      return null;
    }

    const indexData = indexDoc.data()!;
    console.log(`[findExamById] Found index for exam ${examId}, instructor=${indexData.instructorId}, status=${indexData.status}`);
    const instructorId = indexData.instructorId;
    
    // Get actual exam from instructor's subcollection
    const examDoc = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .get();

    if (!examDoc.exists) {
      console.warn(`[findExamById] Exam doc not found at path: examforge_users/${instructorId}/published_exams/${examId}`);
      return null;
    }

    const examData = examDoc.data();
    console.log(`[findExamById] Exam doc found, has title:`, examData?.title ? 'yes' : 'NO - missing title!');

    return {
      exam: this.mapExamFromDb(examDoc.id, examData, instructorId),
      instructorId,
    };
  }

  /**
   * Get exam by ID from instructor's own collection (direct path, no index needed)
   */
  static async getExamByIdForInstructor(examId: string, userId: string): Promise<Exam | null> {
    return this.getExamById(examId, userId, userId);
  }

  /**
   * Get exam by ID from instructor's collection
   */
  static async getExamById(examId: string, instructorId: string, userId?: string): Promise<Exam | null> {
    const db = getFirestore();

    const examDoc = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .get();

    if (!examDoc.exists) return null;

    const examData = examDoc.data();
    if (!examData) {
      console.warn(`[getExamById] Exam ${examId} doc exists but data() is null`);
      return null;
    }
    
    // Check access: instructor owns it OR exam is published/active
    if (userId && examData.instructorId !== userId && !['published', 'active'].includes(examData.status)) {
      throw new Error('Unauthorized access to exam');
    }

    return this.mapExamFromDb(examDoc.id, examData, instructorId);
  }

  /**
   * Get all exams for an instructor
   */
  static async getInstructorExams(instructorId: string): Promise<Exam[]> {
    const db = getFirestore();

    const examsSnapshot = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .orderBy('createdAt', 'desc')
      .get();

    return examsSnapshot.docs.map(doc => 
      this.mapExamFromDb(doc.id, doc.data(), instructorId)
    );
  }

  /**
   * Get available exams for a student (collection group query across all users)
   */
  static async getAvailableExams(studentId: string): Promise<Exam[]> {
    const db = getFirestore();

    // Use flat exams index (no collectionGroup index needed)
    const indexSnapshot = await db
      .collection('exams')
      .where('status', 'in', ['published', 'active'])
      .get();

    const now = new Date();
    const exams: Exam[] = [];

    for (const indexDoc of indexSnapshot.docs) {
      const { instructorId } = indexDoc.data();
      
      // Get actual exam data
      const examDoc = await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .doc(indexDoc.id)
        .get();

      if (!examDoc.exists) continue;
      const examData = examDoc.data()!;
      
      if (examData.startDate && examData.startDate.toDate() > now) continue;
      if (examData.endDate && examData.endDate.toDate() < now) continue;
      if (examData.allowedStudentIds?.length > 0 && !examData.allowedStudentIds.includes(studentId)) continue;

      exams.push(this.mapExamFromDb(indexDoc.id, examData, instructorId));
    }

    return exams;
  }

  /**
   * Update an exam
   */
  static async updateExam(examId: string, instructorId: string, data: UpdateExamDto): Promise<Exam> {
    const db = getFirestore();

    console.log(`[updateExam] Called for exam ${examId}, instructor ${instructorId}`);

    const examRef = db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId);

    const examDoc = await examRef.get();
    if (!examDoc.exists) {
      console.error(`[updateExam] Exam ${examId} not found at path: examforge_users/${instructorId}/published_exams/${examId}`);
      throw new Error('Exam not found');
    }

    const currentData = examDoc.data();
    console.log(`[updateExam] Found exam, title=${currentData?.title}, exists=${!!currentData}`);

    if (!currentData) {
      console.error(`[updateExam] Exam ${examId} doc exists but data() is null!`);
      throw new Error(`Exam data not found for ${examId}`);
    }

    const updateData: any = {
      updatedAt: FieldValue.serverTimestamp(),
    };

    if (data.title) updateData.title = data.title;
    if (data.description !== undefined) updateData.description = data.description;
    if (data.subject !== undefined) updateData.subject = data.subject;
    if (data.grade !== undefined) updateData.grade = data.grade;
    if (data.status) updateData.status = data.status;
    if (data.startDate) updateData.startDate = Timestamp.fromDate(new Date(data.startDate));
    if (data.endDate) updateData.endDate = Timestamp.fromDate(new Date(data.endDate));
    if (data.timeLimit !== undefined) updateData.timeLimit = data.timeLimit;
    if (data.passingScore !== undefined) updateData.passingScore = data.passingScore;
    if (data.shuffleQuestions !== undefined) updateData.shuffleQuestions = data.shuffleQuestions;
    if (data.shuffleAnswers !== undefined) updateData.shuffleAnswers = data.shuffleAnswers;
    if (data.showResults !== undefined) updateData.showResults = data.showResults;
    if (data.allowReview !== undefined) updateData.allowReview = data.allowReview;
    if (data.accessCode !== undefined) updateData.accessCode = data.accessCode;
    if (data.allowGuestAccess !== undefined) updateData.allowGuestAccess = data.allowGuestAccess;
    if (data.sections !== undefined) updateData.sections = data.sections;
    if (data.tags !== undefined) updateData.tags = data.tags;
    if (data.retakeConfig !== undefined) updateData.retakeConfig = data.retakeConfig;
    if (data.lateSubmissionConfig !== undefined) updateData.lateSubmissionConfig = data.lateSubmissionConfig;
    if (data.proctorConfig !== undefined) updateData.proctorConfig = data.proctorConfig;
    if (data.customInstructions !== undefined) updateData.customInstructions = data.customInstructions;
    if (data.showRulesBeforeExam !== undefined) updateData.showRulesBeforeExam = data.showRulesBeforeExam;

    await examRef.update(updateData);

    // Sync status to flat exams index
    if (data.status) {
      await db.collection('exams').doc(examId).update({ status: data.status });
    }

    const updatedDoc = await examRef.get();
    const updatedData = updatedDoc.data();
    if (!updatedData) {
      console.error(`[updateExam] Exam ${examId} data() is null after update!`);
      // Return the original exam data as fallback
      return currentData ? this.mapExamFromDb(examDoc.id, currentData, instructorId) : null as any;
    }
    return this.mapExamFromDb(updatedDoc.id, updatedData, instructorId);
  }

  /**
   * Delete an exam
   */
  static async deleteExam(examId: string, instructorId: string): Promise<void> {
    const db = getFirestore();

    // Delete all questions first
    const questionsSnapshot = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .collection('questions')
      .get();

    const batch = db.batch();
    questionsSnapshot.docs.forEach(doc => batch.delete(doc.ref));
    await batch.commit();

    // Delete the exam
    await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .delete();

    // Clean up flat index
    await db.collection('exams').doc(examId).delete();
  }


  /**
   * Create a question for an exam (subcollection)
   */
  static async createQuestion(instructorId: string, data: CreateQuestionDto): Promise<Question> {
    const db = getFirestore();

    // Get current question count for ordering
    const questionsSnapshot = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(data.examId)
      .collection('questions')
      .orderBy('order', 'desc')
      .limit(1)
      .get();

    const nextOrder = questionsSnapshot.empty ? 0 : questionsSnapshot.docs[0].data().order + 1;

    const questionData = {
      examId: data.examId,
      type: data.type,
      text: data.text,
      description: data.description || '',
      points: data.points,
      difficulty: data.difficulty || 'medium',
      order: nextOrder,
      choices: data.choices || [],
      correctAnswer: data.correctAnswer || null,
      tags: data.tags || [],
      imageUrl: data.imageUrl || null,
      timeLimit: data.timeLimit || null,
      createdAt: FieldValue.serverTimestamp(),
      updatedAt: FieldValue.serverTimestamp(),
    };

    const questionRef = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(data.examId)
      .collection('questions')
      .add(questionData);

    // Update exam's question count and total points
    const examRef = db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(data.examId);

    await examRef.update({
      questionCount: FieldValue.increment(1),
      totalPoints: FieldValue.increment(data.points),
      updatedAt: FieldValue.serverTimestamp(),
    });

    const questionDoc = await questionRef.get();
    return this.mapQuestionFromDb(questionRef.id, questionDoc.data()!);
  }

  /**
   * Get all questions for an exam
   */
  static async getExamQuestions(examId: string, instructorId: string): Promise<Question[]> {
    const db = getFirestore();

    const questionsSnapshot = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .collection('questions')
      .orderBy('order', 'asc')
      .get();

    return questionsSnapshot.docs.map(doc => 
      this.mapQuestionFromDb(doc.id, doc.data())
    );
  }

  /**
   * Update a question
   */
  static async updateQuestion(questionId: string, instructorId: string, examId: string, data: UpdateQuestionDto): Promise<Question> {
    const db = getFirestore();

    const questionRef = db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .collection('questions')
      .doc(questionId);

    const questionDoc = await questionRef.get();
    if (!questionDoc.exists) {
      throw new Error('Question not found');
    }

    const oldPoints = questionDoc.data()!.points;
    const updateData: any = {
      updatedAt: FieldValue.serverTimestamp(),
    };

    if (data.text) updateData.text = data.text;
    if (data.description !== undefined) updateData.description = data.description;
    if (data.points !== undefined) updateData.points = data.points;
    if (data.difficulty) updateData.difficulty = data.difficulty;
    if (data.choices) updateData.choices = data.choices;
    if (data.correctAnswer !== undefined) updateData.correctAnswer = data.correctAnswer;
    if (data.tags) updateData.tags = data.tags;
    if (data.imageUrl !== undefined) updateData.imageUrl = data.imageUrl;
    if (data.timeLimit !== undefined) updateData.timeLimit = data.timeLimit;

    await questionRef.update(updateData);

    // Update exam's total points if points changed
    if (data.points !== undefined && data.points !== oldPoints) {
      const pointsDiff = data.points - oldPoints;
      await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .doc(examId)
        .update({
          totalPoints: FieldValue.increment(pointsDiff),
          updatedAt: FieldValue.serverTimestamp(),
        });
    }

    const updatedDoc = await questionRef.get();
    return this.mapQuestionFromDb(updatedDoc.id, updatedDoc.data()!);
  }

  /**
   * Delete a question
   */
  static async deleteQuestion(questionId: string, instructorId: string, examId: string): Promise<void> {
    const db = getFirestore();

    const questionRef = db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .collection('questions')
      .doc(questionId);

    const questionDoc = await questionRef.get();
    if (!questionDoc.exists) {
      throw new Error('Question not found');
    }

    const points = questionDoc.data()!.points;

    await questionRef.delete();

    // Update exam's question count and total points
    await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .update({
        questionCount: FieldValue.increment(-1),
        totalPoints: FieldValue.increment(-points),
        updatedAt: FieldValue.serverTimestamp(),
      });
  }


  /**
   * Start an exam attempt (creates session in student's exam_sessions)
   */
  static async startExamAttempt(studentId: string, studentName: string, data: StartExamDto): Promise<ExamAttempt> {
    const db = getFirestore();

    // First, we need to find the exam across all users using collection group
    let examsSnapshot;
    try {
      examsSnapshot = await db
        .collectionGroup('published_exams')
        .where('__name__', '==', data.examId)
        .limit(1)
        .get();
    } catch (err) {
      console.warn(`[startExamAttempt] Collection group query failed (known Firestore limitation), trying flat index:`, err instanceof Error ? err.message : err);
      examsSnapshot = { empty: true, docs: [] } as any;
    }

    if (!examsSnapshot || examsSnapshot.empty) {
      // Fallback: try the flat exams index
      console.warn(`[startExamAttempt] Collection group found no exam ${data.examId}, trying index...`);
      const indexDoc = await db.collection('exams').doc(data.examId).get();
      const indexDoc = await db.collection('exams').doc(data.examId).get();
      if (!indexDoc.exists) {
        throw new Error('Exam not found');
      }
      const indexData = indexDoc.data()!;
      const instructorId = indexData.instructorId;
      const examDoc = await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .doc(data.examId)
        .get();
      if (!examDoc.exists) {
        throw new Error('Exam not found');
      }
      examsSnapshot = { docs: [examDoc], empty: false } as any;
    }

    const examDoc = examsSnapshot.docs[0];
    const examData = examDoc.data();
    if (!examData) {
      console.error(`[startExamAttempt] Exam ${data.examId} data() is null`);
      throw new Error('Exam data not found');
    }
    const instructorId = examData.instructorId;
    if (!instructorId) {
      console.error(`[startExamAttempt] Exam ${data.examId} has no instructorId`);
      throw new Error('Exam configuration error');
    }

    // Verify exam status
    if (!['published', 'active'].includes(examData.status)) {
      throw new Error('Exam is not available');
    }

    // Check access method
    const isGuest = studentId === 'guest' || !studentId || studentId === 'guest@anonymous.com';
    const allowGuest = examData.allowGuestAccess === true || examData.accessMethod === 'guest';

    if (isGuest && !allowGuest) {
      throw new Error('This exam requires student login. Please sign in first.');
    }

    // Verify access code if required
    if (examData.accessCode && examData.accessCode !== data.accessCode) {
      throw new Error('Invalid access code');
    }

    // Use proper student ID for guests (create a unique guest session ID)
    const effectiveStudentId = (isGuest && data.guestInfo?.studentId)
      ? `guest_${data.guestInfo.studentId}_${data.examId}`
      : studentId;
    const effectiveStudentName = isGuest && data.guestInfo?.name
      ? data.guestInfo.name
      : studentName;

    // Check if student already has an active attempt
    const existingSessionsSnapshot = await db
      .collection('examforge_users')
      .doc(effectiveStudentId)
      .collection('exam_sessions')
      .where('examId', '==', data.examId)
      .where('status', '==', 'in_progress')
      .limit(1)
      .get();

    if (!existingSessionsSnapshot.empty) {
      const existingSession = existingSessionsSnapshot.docs[0];
      return this.mapAttemptFromDb(existingSession.id, existingSession.data());
    }

    // Build session data
    const sessionData: Record<string, unknown> = {
      examId: data.examId,
      instructorId,
      studentId: effectiveStudentId,
      originalStudentId: isGuest ? null : studentId,
      studentName: effectiveStudentName,
      examTitle: examData.title,
      status: 'in_progress',
      score: 0,
      percentage: 0,
      passed: false,
      startedAt: FieldValue.serverTimestamp(),
      submittedAt: null,
      timeSpent: 0,
      ipAddress: null,
      userAgent: null,
      createdAt: FieldValue.serverTimestamp(),
      updatedAt: FieldValue.serverTimestamp(),
      guestInfo: (isGuest && data.guestInfo) ? {
        name: data.guestInfo.name,
        studentId: data.guestInfo.studentId,
        course: data.guestInfo.course || '',
        year: data.guestInfo.year || '',
      } : null,
      accessMethod: (isGuest && data.guestInfo) ? 'guest' : 'student_login',
    };

    const sessionRef = await db
      .collection('examforge_users')
      .doc(effectiveStudentId)
      .collection('exam_sessions')
      .add(sessionData);

    // Increment exam attempt count
    await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(data.examId)
      .update({
        attemptCount: FieldValue.increment(1),
        updatedAt: FieldValue.serverTimestamp(),
      });

    const sessionDoc = await sessionRef.get();
    return this.mapAttemptFromDb(sessionRef.id, sessionDoc.data()!);
  }

  /**
   * Find the session doc for a given attemptId across all users
   */
  private static async findSessionByAttemptId(attemptId: string): Promise<{ studentId: string; doc: any } | null> {
    const db = getFirestore();
    const groupsSnapshot = await db
      .collectionGroup('exam_sessions')
      .where('__name__', '==', attemptId)
      .limit(1)
      .get();
    if (groupsSnapshot.empty) return null;
    const doc = groupsSnapshot.docs[0];
    const pathParts = doc.ref.path.split('/');
    const studentId = pathParts[1]; // examforge_users/{studentId}/exam_sessions/{id}
    return { studentId, doc };
  }

  /**
   * Submit an answer for a question (stores in student's examinee_data)
   */
  static async submitAnswer(studentId: string, data: SubmitAnswerDto): Promise<ExamAnswer> {
    const db = getFirestore();

    // Resolve the session by attemptId to get the real studentId
    const sessionLookup = await this.findSessionByAttemptId(data.attemptId);
    if (!sessionLookup) {
      throw new Error('Session not found');
    }
    const effectiveStudentId = sessionLookup.studentId;
    const sessionData = sessionLookup.doc.data()!;

    if (sessionData.status !== 'in_progress') {
      throw new Error('Cannot submit answer for completed session');
    }

    // Get question to determine correctness
    const instructorId = sessionData.instructorId;
    const examId = sessionData.examId;

    const questionDoc = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .collection('questions')
      .doc(data.questionId)
      .get();

    if (!questionDoc.exists) {
      throw new Error('Question not found');
    }

    const questionData = questionDoc.data()!;

    // Auto-grade if possible
    let isCorrect: boolean | null = null;
    let pointsEarned: number | null = null;

    if (['multiple_choice', 'true_false'].includes(questionData.type)) {
      isCorrect = JSON.stringify(data.answer) === JSON.stringify(questionData.correctAnswer);
      pointsEarned = isCorrect ? questionData.points : 0;
    }

    const answerData = {
      attemptId: data.attemptId,
      questionId: data.questionId,
      examId,
      answer: data.answer,
      isCorrect,
      pointsEarned,
      feedback: null,
      gradedBy: null,
      gradedAt: null,
      timeSpent: data.timeSpent || 0,
      createdAt: FieldValue.serverTimestamp(),
      updatedAt: FieldValue.serverTimestamp(),
    };

    // Check if answer already exists
    const existingAnswersSnapshot = await db
      .collection('examforge_users')
      .doc(effectiveStudentId)
      .collection('examinee_data')
      .where('attemptId', '==', data.attemptId)
      .where('questionId', '==', data.questionId)
      .limit(1)
      .get();

    let answerRef;
    if (!existingAnswersSnapshot.empty) {
      // Update existing answer
      answerRef = existingAnswersSnapshot.docs[0].ref;
      await answerRef.update({
        ...answerData,
        updatedAt: FieldValue.serverTimestamp(),
      });
    } else {
      // Create new answer
      answerRef = await db
        .collection('examforge_users')
        .doc(effectiveStudentId)
        .collection('examinee_data')
        .add(answerData);
    }

    const answerDoc = await answerRef.get();
    return this.mapAnswerFromDb(answerRef.id, answerDoc.data()!);
  }

  /**
   * Submit exam (complete attempt)
   */
  static async submitExam(studentId: string, data: SubmitExamDto): Promise<ExamAttempt> {
    const db = getFirestore();

    // Resolve session by attemptId
    const sessionLookup = await this.findSessionByAttemptId(data.attemptId);
    if (!sessionLookup) {
      throw new Error('Session not found');
    }
    const effectiveStudentId = sessionLookup.studentId;
    const sessionRef = sessionLookup.doc.ref;

    const sessionData = sessionLookup.doc.data()!;
    if (sessionData.status !== 'in_progress') {
      throw new Error('Session already submitted');
    }

    // Get all answers for this attempt
    const answersSnapshot = await db
      .collection('examforge_users')
      .doc(effectiveStudentId)
      .collection('examinee_data')
      .where('attemptId', '==', data.attemptId)
      .get();

    // Calculate score from auto-graded answers
    let totalScore = 0;
    let hasUngradedAnswers = false;

    answersSnapshot.docs.forEach(doc => {
      const answerData = doc.data();
      if (answerData.pointsEarned !== null) {
        totalScore += answerData.pointsEarned;
      } else {
        hasUngradedAnswers = true;
      }
    });

    // Get exam to calculate percentage
    const instructorId = sessionData.instructorId;
    const examId = sessionData.examId;

    const examDoc = await db
      .collection('examforge_users')
      .doc(instructorId)
      .collection('published_exams')
      .doc(examId)
      .get();

    const examData = examDoc.data()!;
    const percentage = examData.totalPoints > 0 ? (totalScore / examData.totalPoints) * 100 : 0;
    const passed = percentage >= (examData.passingScore || 60);

    const status = hasUngradedAnswers ? 'submitted' : 'graded';

    // Calculate time spent
    const startedAt = sessionData.startedAt?.toDate() || new Date();
    const timeSpent = Math.floor((Date.now() - startedAt.getTime()) / 1000);

    // Update session
    await sessionRef.update({
      status,
      score: totalScore,
      percentage,
      passed,
      submittedAt: FieldValue.serverTimestamp(),
      timeSpent,
      updatedAt: FieldValue.serverTimestamp(),
    });

    const updatedSession = await sessionRef.get();
    return this.mapAttemptFromDb(updatedSession.id, updatedSession.data()!);
  }

  /**
   * Get exam attempt with answers
   */
  static async getExamAttempt(attemptId: string, userId: string): Promise<ExamAttempt & { answers: ExamAnswer[] }> {
    const db = getFirestore();

    // Resolve session by attemptId
    const sessionLookup = await this.findSessionByAttemptId(attemptId);
    if (!sessionLookup) {
      throw new Error('Attempt not found');
    }
    const effectiveStudentId = sessionLookup.studentId;
    const sessionData = sessionLookup.doc.data()!;

    // Get answers
    const answersSnapshot = await db
      .collection('examforge_users')
      .doc(effectiveStudentId)
      .collection('examinee_data')
      .where('attemptId', '==', attemptId)
      .get();

    const answers = answersSnapshot.docs.map(doc => 
      this.mapAnswerFromDb(doc.id, doc.data())
    );

    return {
      ...this.mapAttemptFromDb(attemptId, sessionData),
      answers,
    };
  }


  // Helper mapping functions for Firestore documents
  private static mapExamFromDb(id: string, data: any, instructorId: string): Exam {
    if (!data) {
      console.error(`[mapExamFromDb] FATAL: data is undefined for exam ${id}, instructor ${instructorId}`);
      throw new Error(`Exam data not found for ${id}`);
    }
    return {
      id,
      title: data.title,
      description: data.description,
      instructorId: data.instructorId || instructorId,
      instructorName: data.instructorName,
      status: data.status as ExamStatus,
      totalPoints: data.totalPoints || 0,
      passingScore: data.passingScore || 70,
      timeLimit: data.timeLimit,
      shuffleQuestions: data.shuffleQuestions ?? false,
      shuffleAnswers: data.shuffleAnswers ?? false,
      showResults: data.showResults ?? true,
      allowReview: data.allowReview ?? true,
      startDate: data.startDate?.toDate(),
      endDate: data.endDate?.toDate(),
      accessCode: data.accessCode,
      allowGuestAccess: data.allowGuestAccess ?? false,
      allowedStudentIds: data.allowedStudentIds || [],
      sections: data.sections || [],
      tags: data.tags || [],
      retakeConfig: data.retakeConfig || undefined,
      lateSubmissionConfig: data.lateSubmissionConfig || undefined,
      proctorConfig: data.proctorConfig || undefined,
      customInstructions: data.customInstructions || undefined,
      showRulesBeforeExam: data.showRulesBeforeExam ?? false,
      accessMethod: data.accessMethod || 'student_login',
      subject: data.subject || '',
      grade: data.grade || '',
      questionCount: data.questionCount || 0,
      attemptCount: data.attemptCount || 0,
      averageScore: data.averageScore || 0,
      createdAt: data.createdAt?.toDate() || new Date(),
      updatedAt: data.updatedAt?.toDate() || new Date(),
    };
  }

  private static mapQuestionFromDb(id: string, data: any): Question {
    return {
      id,
      examId: data.examId,
      type: data.type,
      text: data.text,
      description: data.description || '',
      points: data.points,
      difficulty: data.difficulty || 'medium',
      order: data.order || 0,
      choices: data.choices || [],
      correctAnswer: data.correctAnswer,
      tags: data.tags || [],
      imageUrl: data.imageUrl,
      timeLimit: data.timeLimit,
      createdAt: data.createdAt?.toDate() || new Date(),
      updatedAt: data.updatedAt?.toDate() || new Date(),
    };
  }

  private static mapAttemptFromDb(id: string, data: any): ExamAttempt {
    return {
      id,
      examId: data.examId,
      studentId: data.studentId,
      studentName: data.studentName,
      status: data.status,
      score: data.score || 0,
      percentage: data.percentage || 0,
      passed: data.passed || false,
      startedAt: data.startedAt?.toDate() || new Date(),
      submittedAt: data.submittedAt?.toDate(),
      timeSpent: data.timeSpent || 0,
      ipAddress: data.ipAddress,
      userAgent: data.userAgent,
      answers: [],
      createdAt: data.createdAt?.toDate() || new Date(),
      updatedAt: data.updatedAt?.toDate() || new Date(),
    };
  }

  private static mapAnswerFromDb(id: string, data: any): ExamAnswer {
    return {
      id,
      attemptId: data.attemptId,
      questionId: data.questionId,
      answer: data.answer,
      isCorrect: data.isCorrect,
      pointsEarned: data.pointsEarned,
      feedback: data.feedback,
      gradedBy: data.gradedBy,
      gradedAt: data.gradedAt?.toDate(),
      timeSpent: data.timeSpent || 0,
      createdAt: data.createdAt?.toDate() || new Date(),
      updatedAt: data.updatedAt?.toDate() || new Date(),
    };
  }

  /**
   * Get grading queue for essay questions
   */
  static async getGradingQueue(instructorId: string): Promise<any[]> {
    const db = getFirestore();
    const queue: any[] = [];

    try {
      // Get all instructor's exams
      const examsSnapshot = await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .get();

      // For each exam, find essay questions that need grading
      for (const examDoc of examsSnapshot.docs) {
        const examData = examDoc.data();
        
        // Get questions for this exam
        const questionsSnapshot = await db
          .collection('examforge_users')
          .doc(instructorId)
          .collection('published_exams')
          .doc(examDoc.id)
          .collection('questions')
          .where('type', 'in', ['essay', 'short_answer'])
          .get();

        // Get attempts for this exam
        const attemptsSnapshot = await db
          .collectionGroup('exam_sessions')
          .where('examId', '==', examDoc.id)
          .where('status', '==', 'completed')
          .get();

        // For each attempt, check for essay answers
        for (const attemptDoc of attemptsSnapshot.docs) {
          const attemptData = attemptDoc.data();
          
          // Get answers for this attempt
          const answersSnapshot = await db
            .collection('examforge_users')
            .doc(attemptData.studentId)
            .collection('examinee_data')
            .where('attemptId', '==', attemptDoc.id)
            .get();

          for (const answerDoc of answersSnapshot.docs) {
            const answerData = answerDoc.data();
            const question = questionsSnapshot.docs.find(q => q.id === answerData.questionId);
            
            if (question) {
              const questionData = question.data();
              
              queue.push({
                attemptId: attemptDoc.id,
                questionId: question.id,
                examId: examDoc.id,
                examTitle: examData.title,
                studentId: attemptData.studentId,
                studentName: attemptData.studentName || 'Unknown',
                studentEmail: attemptData.studentEmail || '',
                question: {
                  id: question.id,
                  text: questionData.text,
                  description: questionData.description,
                  type: questionData.type,
                  points: questionData.points,
                },
                answer: answerData.answer,
                currentGrade: answerData.pointsEarned,
                feedback: answerData.feedback,
                submittedAt: attemptData.submittedAt?.toDate() || new Date(),
                isGraded: answerData.gradedBy != null,
              });
            }
          }
        }
      }

      return queue;
    } catch (error) {
      throw new Error(`Failed to fetch grading queue: ${error}`);
    }
  }

  /**
   * Grade a specific question in an attempt
   */
  static async gradeQuestion(
    attemptId: string,
    questionId: string,
    earnedPoints: number,
    feedback?: string
  ): Promise<void> {
    const db = getFirestore();

    try {
      // Find the attempt
      const attemptsSnapshot = await db
        .collectionGroup('exam_sessions')
        .where('__name__', '==', attemptId)
        .get();

      if (attemptsSnapshot.empty) {
        throw new Error('Attempt not found');
      }

      const attemptDoc = attemptsSnapshot.docs[0];
      const attemptData = attemptDoc.data();
      const studentId = attemptData.studentId;

      // Find and update the answer
      const answersSnapshot = await db
        .collection('examforge_users')
        .doc(studentId)
        .collection('examinee_data')
        .where('attemptId', '==', attemptId)
        .where('questionId', '==', questionId)
        .get();

      if (answersSnapshot.empty) {
        throw new Error('Answer not found');
      }

      const answerDoc = answersSnapshot.docs[0];
      await answerDoc.ref.update({
        pointsEarned: earnedPoints,
        feedback: feedback || null,
        gradedBy: 'instructor', // TODO: Use actual instructor ID
        gradedAt: FieldValue.serverTimestamp(),
        updatedAt: FieldValue.serverTimestamp(),
      });

      // Recalculate total score for the attempt
      const allAnswersSnapshot = await db
        .collection('examforge_users')
        .doc(studentId)
        .collection('examinee_data')
        .where('attemptId', '==', attemptId)
        .get();

      let totalScore = 0;
      allAnswersSnapshot.docs.forEach(doc => {
        const data = doc.data();
        totalScore += data.pointsEarned || 0;
      });

      // Update attempt with new total score
      await attemptDoc.ref.update({
        score: totalScore,
        updatedAt: FieldValue.serverTimestamp(),
      });

    } catch (error) {
      throw new Error(`Failed to grade question: ${error}`);
    }
  }

  /**
   * Get analytics for an exam
   */
  static async getExamAnalytics(examId: string, instructorId: string): Promise<any> {
    const db = getFirestore();

    try {
      // Get exam
      const examDoc = await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .doc(examId)
        .get();

      if (!examDoc.exists) {
        throw new Error('Exam not found');
      }

      const examData = examDoc.data()!;

      // Get all attempts for this exam
      const attemptsSnapshot = await db
        .collectionGroup('exam_sessions')
        .where('examId', '==', examId)
        .where('status', '==', 'completed')
        .get();

      const attempts = attemptsSnapshot.docs.map(doc => doc.data());
      const totalAttempts = attempts.length;

      if (totalAttempts === 0) {
        return {
          examId,
          examTitle: examData.title,
          totalAttempts: 0,
          averageScore: 0,
          passRate: 0,
          averageTime: 0,
          questionStats: [],
        };
      }

      // Calculate statistics
      const totalScore = attempts.reduce((sum, a) => sum + (a.score || 0), 0);
      const averageScore = (totalScore / totalAttempts / (examData.totalPoints || 100)) * 100;

      const passedCount = attempts.filter(a => a.passed).length;
      const passRate = (passedCount / totalAttempts) * 100;

      const totalTime = attempts.reduce((sum, a) => sum + (a.timeSpent || 0), 0);
      const averageTime = totalTime / totalAttempts;

      // Get question statistics
      const questionsSnapshot = await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .doc(examId)
        .collection('questions')
        .get();

      const questionStats = [];

      for (const questionDoc of questionsSnapshot.docs) {
        const questionData = questionDoc.data();
        let correctCount = 0;
        let totalPoints = 0;
        let answerCount = 0;

        // Get all answers for this question across all attempts
        for (const attempt of attempts) {
          const answersSnapshot = await db
            .collection('examforge_users')
            .doc(attempt.studentId)
            .collection('examinee_data')
            .where('attemptId', '==', attempt.attemptId)
            .where('questionId', '==', questionDoc.id)
            .get();

          answersSnapshot.docs.forEach(answerDoc => {
            const answerData = answerDoc.data();
            if (answerData.isCorrect) correctCount++;
            totalPoints += answerData.pointsEarned || 0;
            answerCount++;
          });
        }

        questionStats.push({
          questionId: questionDoc.id,
          questionText: questionData.text,
          correctRate: answerCount > 0 ? (correctCount / answerCount) * 100 : 0,
          averagePoints: answerCount > 0 ? totalPoints / answerCount : 0,
          maxPoints: questionData.points,
        });
      }

      return {
        examId,
        examTitle: examData.title,
        totalAttempts,
        averageScore,
        passRate,
        averageTime,
        questionStats,
      };

    } catch (error) {
      throw new Error(`Failed to get exam analytics: ${error}`);
    }
  }

  /**
   * Get incident reports for an instructor
   */
  static async getIncidentReports(instructorId: string): Promise<any[]> {
    const db = getFirestore();

    try {
      // Get all exams for this instructor
      const examsSnapshot = await db
        .collection('examforge_users')
        .doc(instructorId)
        .collection('published_exams')
        .get();

      const incidents: any[] = [];

      for (const examDoc of examsSnapshot.docs) {
        const examData = examDoc.data();

        // Get all sessions for this exam
        const sessionsSnapshot = await db
          .collectionGroup('exam_sessions')
          .where('examId', '==', examDoc.id)
          .get();

        for (const sessionDoc of sessionsSnapshot.docs) {
          const sessionData = sessionDoc.data();

          // Get session events (incidents)
          const eventsSnapshot = await db
            .collection('examforge_users')
            .doc(sessionData.studentId)
            .collection('session_events')
            .where('attemptId', '==', sessionData.attemptId)
            .get();

          eventsSnapshot.docs.forEach(eventDoc => {
            const eventData = eventDoc.data();
            incidents.push({
              id: eventDoc.id,
              studentId: sessionData.studentId,
              studentName: sessionData.studentName,
              examId: examDoc.id,
              examTitle: examData.title,
              eventType: eventData.eventType,
              eventDetail: eventData.eventDetail || '',
              timestamp: eventData.timestamp,
              severity: eventData.severity || 'medium',
              archived: eventData.archived || false,
            });
          });
        }
      }

      // Sort by timestamp descending (most recent first)
      incidents.sort((a, b) => b.timestamp - a.timestamp);

      return incidents;
    } catch (error) {
      throw new Error(`Failed to get incident reports: ${error}`);
    }
  }

  /**
   * Archive incidents
   */
  static async archiveIncidents(incidentIds: string[]): Promise<void> {
    const db = getFirestore();

    try {
      const batch = db.batch();

      for (const incidentId of incidentIds) {
        // Find the incident across all users' session_events
        const eventsSnapshot = await db.collectionGroup('session_events').where('__name__', '==', incidentId).get();

        eventsSnapshot.docs.forEach(doc => {
          batch.update(doc.ref, { archived: true });
        });
      }

      await batch.commit();
    } catch (error) {
      throw new Error(`Failed to archive incidents: ${error}`);
    }
  }

  /**
   * Unarchive incidents
   */
  static async unarchiveIncidents(incidentIds: string[]): Promise<void> {
    const db = getFirestore();

    try {
      const batch = db.batch();

      for (const incidentId of incidentIds) {
        const eventsSnapshot = await db.collectionGroup('session_events').where('__name__', '==', incidentId).get();

        eventsSnapshot.docs.forEach(doc => {
          batch.update(doc.ref, { archived: false });
        });
      }

      await batch.commit();
    } catch (error) {
      throw new Error(`Failed to unarchive incidents: ${error}`);
    }
  }

  /**
   * Delete incidents
   */
  static async deleteIncidents(incidentIds: string[]): Promise<void> {
    const db = getFirestore();

    try {
      const batch = db.batch();

      for (const incidentId of incidentIds) {
        const eventsSnapshot = await db.collectionGroup('session_events').where('__name__', '==', incidentId).get();

        eventsSnapshot.docs.forEach(doc => {
          batch.delete(doc.ref);
        });
      }

      await batch.commit();
    } catch (error) {
      throw new Error(`Failed to delete incidents: ${error}`);
    }
  }

  /**
   * Record a proctoring violation for an exam attempt
   */
  static async recordViolation(
    userId: string,
    attemptId: string,
    violationType: string,
    timestamp: number
  ): Promise<void> {
    const db = getFirestore();

    try {
      // Get the attempt to find exam details and student
      const attemptSnapshot = await db.collectionGroup('exam_sessions')
        .where('__name__', '==', attemptId)
        .get();

      if (attemptSnapshot.empty) {
        throw new Error('Exam attempt not found');
      }

      const attemptDoc = attemptSnapshot.docs[0];
      const attemptData = attemptDoc.data();
      const examId = attemptData.examId;
      const effectiveUserId = attemptData.studentId || userId;

      // Get exam to determine point deduction
      const examSnapshot = await db.collectionGroup('published_exams')
        .where('__name__', '==', examId)
        .get();

      let pointsDeducted = 0;
      let severity: 'low' | 'medium' | 'high' = 'low';

      if (!examSnapshot.empty) {
        const examData = examSnapshot.docs[0].data();
        const proctorConfig = examData.proctorConfig;

        if (proctorConfig?.pointDeductions) {
          // Map violation type to deduction field
          const deductionMap: Record<string, string> = {
            'tab_switch': 'tabSwitch',
            'copy_attempt': 'copyPaste',
            'paste_attempt': 'copyPaste',
            'right_click_attempt': 'rightClick',
            'exit_fullscreen': 'exitFullscreen',
          };

          const deductionField = deductionMap[violationType];
          if (deductionField && proctorConfig.pointDeductions[deductionField]) {
            pointsDeducted = proctorConfig.pointDeductions[deductionField];
          }
        }

        // Determine severity based on points deducted
        if (pointsDeducted >= 10) severity = 'high';
        else if (pointsDeducted >= 5) severity = 'medium';
        else severity = 'low';
      }

      // Create violation event in user's session_events collection
      const eventRef = db.collection(`examforge_users/${effectiveUserId}/session_events`).doc();
      await eventRef.set({
        attemptId,
        examId,
        eventType: 'proctoring_violation',
        eventDetail: violationType,
        timestamp: new Date(timestamp),
        severity,
        pointsDeducted,
        archived: false,
      });

      // Update attempt with violation
      await attemptDoc.ref.update({
        violations: FieldValue.arrayUnion({
          id: eventRef.id,
          type: violationType,
          timestamp: new Date(timestamp),
          pointsDeducted,
          severity,
        }),
        violationPenalty: FieldValue.increment(pointsDeducted),
      });
    } catch (error) {
      throw new Error(`Failed to record violation: ${error}`);
    }
  }
}
