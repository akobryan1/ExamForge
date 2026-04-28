import { getSupabaseClient } from '../config/supabase';
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
   * Create a new exam
   */
  static async createExam(instructorId: string, instructorName: string, data: CreateExamDto): Promise<Exam> {
    const supabase = getSupabaseClient();

    const { data: exam, error } = await supabase
      .from('exams')
      .insert({
        ...data,
        instructor_id: instructorId,
        instructor_name: instructorName,
        status: 'draft',
        start_date: data.startDate,
        end_date: data.endDate,
        time_limit: data.timeLimit,
        passing_score: data.passingScore,
        shuffle_questions: data.shuffleQuestions ?? false,
        shuffle_answers: data.shuffleAnswers ?? false,
        show_results: data.showResults ?? true,
        allow_review: data.allowReview ?? true,
        access_code: data.accessCode,
      })
      .select()
      .single();

    if (error) throw new Error(`Failed to create exam: ${error.message}`);
    return this.mapExamFromDb(exam);
  }

  /**
   * Get exam by ID
   */
  static async getExamById(examId: string, userId: string): Promise<Exam | null> {
    const supabase = getSupabaseClient();

    const { data: exam, error } = await supabase
      .from('exams')
      .select('*')
      .eq('id', examId)
      .single();

    if (error) return null;
    
    // Check access: instructor owns it OR exam is published/active
    if (exam.instructor_id !== userId && !['published', 'active'].includes(exam.status)) {
      throw new Error('Unauthorized access to exam');
    }

    return this.mapExamFromDb(exam);
  }

  /**
   * Get all exams for an instructor
   */
  static async getInstructorExams(instructorId: string): Promise<Exam[]> {
    const supabase = getSupabaseClient();

    const { data: exams, error } = await supabase
      .from('exams')
      .select('*')
      .eq('instructor_id', instructorId)
      .order('created_at', { ascending: false });

    if (error) throw new Error(`Failed to fetch exams: ${error.message}`);
    return exams.map(this.mapExamFromDb);
  }

  /**
   * Get available exams for a student
   */
  static async getAvailableExams(studentId: string): Promise<Exam[]> {
    const supabase = getSupabaseClient();

    const { data: exams, error } = await supabase
      .from('exams')
      .select('*')
      .in('status', ['published', 'active'])
      .order('created_at', { ascending: false });

    if (error) throw new Error(`Failed to fetch available exams: ${error.message}`);
    
    // Filter exams based on scheduling and access control
    const now = new Date();
    return exams
      .filter(exam => {
        // Check if exam is within scheduled time
        if (exam.start_date && new Date(exam.start_date) > now) return false;
        if (exam.end_date && new Date(exam.end_date) < now) return false;
        
        // Check if student is in allowed list (if specified)
        if (exam.allowed_student_ids && !exam.allowed_student_ids.includes(studentId)) return false;
        
        return true;
      })
      .map(this.mapExamFromDb);
  }

  /**
   * Update an exam
   */
  static async updateExam(examId: string, instructorId: string, data: UpdateExamDto): Promise<Exam> {
    const supabase = getSupabaseClient();

    // Verify ownership
    const exam = await this.getExamById(examId, instructorId);
    if (!exam || exam.instructorId !== instructorId) {
      throw new Error('Unauthorized to update this exam');
    }

    const updateData: any = { ...data };
    if (data.startDate) updateData.start_date = data.startDate;
    if (data.endDate) updateData.end_date = data.endDate;
    if (data.timeLimit !== undefined) updateData.time_limit = data.timeLimit;
    if (data.passingScore !== undefined) updateData.passing_score = data.passingScore;
    if (data.shuffleQuestions !== undefined) updateData.shuffle_questions = data.shuffleQuestions;
    if (data.shuffleAnswers !== undefined) updateData.shuffle_answers = data.shuffleAnswers;
    if (data.showResults !== undefined) updateData.show_results = data.showResults;
    if (data.allowReview !== undefined) updateData.allow_review = data.allowReview;
    if (data.accessCode !== undefined) updateData.access_code = data.accessCode;

    const { data: updatedExam, error } = await supabase
      .from('exams')
      .update(updateData)
      .eq('id', examId)
      .select()
      .single();

    if (error) throw new Error(`Failed to update exam: ${error.message}`);
    return this.mapExamFromDb(updatedExam);
  }

  /**
   * Delete an exam
   */
  static async deleteExam(examId: string, instructorId: string): Promise<void> {
    const supabase = getSupabaseClient();

    const { error } = await supabase
      .from('exams')
      .delete()
      .eq('id', examId)
      .eq('instructor_id', instructorId);

    if (error) throw new Error(`Failed to delete exam: ${error.message}`);
  }

  /**
   * Create a question for an exam
   */
  static async createQuestion(instructorId: string, data: CreateQuestionDto): Promise<Question> {
    const supabase = getSupabaseClient();

    // Verify exam ownership
    const exam = await this.getExamById(data.examId, instructorId);
    if (!exam || exam.instructorId !== instructorId) {
      throw new Error('Unauthorized to add questions to this exam');
    }

    // Get current max order
    const { data: maxOrderData } = await supabase
      .from('questions')
      .select('order_index')
      .eq('exam_id', data.examId)
      .order('order_index', { ascending: false })
      .limit(1)
      .single();

    const nextOrder = maxOrderData ? maxOrderData.order_index + 1 : 0;

    const { data: question, error } = await supabase
      .from('questions')
      .insert({
        exam_id: data.examId,
        type: data.type,
        text: data.text,
        description: data.description,
        points: data.points,
        difficulty: data.difficulty,
        order_index: nextOrder,
        choices: data.choices ? JSON.stringify(data.choices) : null,
        correct_answer: data.correctAnswer ? JSON.stringify(data.correctAnswer) : null,
        matching_pairs: data.matchingPairs ? JSON.stringify(data.matchingPairs) : null,
        tags: data.tags,
        image_url: data.imageUrl,
        time_limit: data.timeLimit,
      })
      .select()
      .single();

    if (error) throw new Error(`Failed to create question: ${error.message}`);
    return this.mapQuestionFromDb(question);
  }

  /**
   * Get all questions for an exam
   */
  static async getExamQuestions(examId: string, userId: string): Promise<Question[]> {
    const supabase = getSupabaseClient();

    // Verify access
    await this.getExamById(examId, userId);

    const { data: questions, error } = await supabase
      .from('questions')
      .select('*')
      .eq('exam_id', examId)
      .order('order_index', { ascending: true });

    if (error) throw new Error(`Failed to fetch questions: ${error.message}`);
    return questions.map(this.mapQuestionFromDb);
  }

  /**
   * Update a question
   */
  static async updateQuestion(questionId: string, instructorId: string, data: UpdateQuestionDto): Promise<Question> {
    const supabase = getSupabaseClient();

    // Get question to verify ownership
    const { data: question } = await supabase
      .from('questions')
      .select('*, exams!inner(instructor_id)')
      .eq('id', questionId)
      .single();

    if (!question || question.exams.instructor_id !== instructorId) {
      throw new Error('Unauthorized to update this question');
    }

    const updateData: any = {};
    if (data.text) updateData.text = data.text;
    if (data.description !== undefined) updateData.description = data.description;
    if (data.points !== undefined) updateData.points = data.points;
    if (data.difficulty) updateData.difficulty = data.difficulty;
    if (data.choices) updateData.choices = JSON.stringify(data.choices);
    if (data.correctAnswer) updateData.correct_answer = JSON.stringify(data.correctAnswer);
    if (data.matchingPairs) updateData.matching_pairs = JSON.stringify(data.matchingPairs);
    if (data.tags) updateData.tags = data.tags;
    if (data.imageUrl !== undefined) updateData.image_url = data.imageUrl;
    if (data.timeLimit !== undefined) updateData.time_limit = data.timeLimit;

    const { data: updatedQuestion, error } = await supabase
      .from('questions')
      .update(updateData)
      .eq('id', questionId)
      .select()
      .single();

    if (error) throw new Error(`Failed to update question: ${error.message}`);
    return this.mapQuestionFromDb(updatedQuestion);
  }

  /**
   * Delete a question
   */
  static async deleteQuestion(questionId: string, instructorId: string): Promise<void> {
    const supabase = getSupabaseClient();

    const { data: question } = await supabase
      .from('questions')
      .select('*, exams!inner(instructor_id)')
      .eq('id', questionId)
      .single();

    if (!question || question.exams.instructor_id !== instructorId) {
      throw new Error('Unauthorized to delete this question');
    }

    const { error } = await supabase
      .from('questions')
      .delete()
      .eq('id', questionId);

    if (error) throw new Error(`Failed to delete question: ${error.message}`);
  }

  /**
   * Start an exam attempt
   */
  static async startExamAttempt(studentId: string, studentName: string, data: StartExamDto): Promise<ExamAttempt> {
    const supabase = getSupabaseClient();

    // Get and verify exam
    const exam = await this.getExamById(data.examId, studentId);
    if (!exam) throw new Error('Exam not found');
    if (!['published', 'active'].includes(exam.status)) throw new Error('Exam is not available');

    // Verify access code if required
    if (exam.accessCode && exam.accessCode !== data.accessCode) {
      throw new Error('Invalid access code');
    }

    // Check if student already has an active attempt
    const { data: existingAttempt } = await supabase
      .from('exam_attempts')
      .select('*')
      .eq('exam_id', data.examId)
      .eq('student_id', studentId)
      .eq('status', 'in_progress')
      .single();

    if (existingAttempt) {
      return this.mapAttemptFromDb(existingAttempt);
    }

    // Create new attempt
    const { data: attempt, error } = await supabase
      .from('exam_attempts')
      .insert({
        exam_id: data.examId,
        student_id: studentId,
        student_name: studentName,
        status: 'in_progress',
      })
      .select()
      .single();

    if (error) throw new Error(`Failed to start exam attempt: ${error.message}`);

    // Update exam attempt count
    await supabase.rpc('increment', {
      table_name: 'exams',
      row_id: data.examId,
      column_name: 'attempt_count',
    });

    return this.mapAttemptFromDb(attempt);
  }

  /**
   * Submit an answer for a question
   */
  static async submitAnswer(studentId: string, data: SubmitAnswerDto): Promise<ExamAnswer> {
    const supabase = getSupabaseClient();

    // Verify attempt ownership
    const { data: attempt } = await supabase
      .from('exam_attempts')
      .select('*')
      .eq('id', data.attemptId)
      .eq('student_id', studentId)
      .single();

    if (!attempt) throw new Error('Unauthorized access to this attempt');
    if (attempt.status !== 'in_progress') throw new Error('Cannot submit answer for completed attempt');

    // Get question to determine correctness
    const { data: question } = await supabase
      .from('questions')
      .select('*')
      .eq('id', data.questionId)
      .single();

    if (!question) throw new Error('Question not found');

    // Auto-grade if possible
    let isCorrect: boolean | undefined;
    let pointsEarned: number | undefined;

    if (['multiple_choice', 'true_false'].includes(question.type)) {
      const correctAnswer = JSON.parse(question.correct_answer);
      isCorrect = JSON.stringify(data.answer) === JSON.stringify(correctAnswer);
      pointsEarned = isCorrect ? question.points : 0;
    }

    // Upsert answer
    const { data: answer, error } = await supabase
      .from('exam_answers')
      .upsert({
        attempt_id: data.attemptId,
        question_id: data.questionId,
        answer: JSON.stringify(data.answer),
        is_correct: isCorrect,
        points_earned: pointsEarned,
        time_spent: data.timeSpent,
      })
      .select()
      .single();

    if (error) throw new Error(`Failed to submit answer: ${error.message}`);
    return this.mapAnswerFromDb(answer);
  }

  /**
   * Submit exam (complete attempt)
   */
  static async submitExam(studentId: string, data: SubmitExamDto): Promise<ExamAttempt> {
    const supabase = getSupabaseClient();

    // Verify attempt ownership
    const { data: attempt } = await supabase
      .from('exam_attempts')
      .select('*')
      .eq('id', data.attemptId)
      .eq('student_id', studentId)
      .single();

    if (!attempt) throw new Error('Unauthorized access to this attempt');
    if (attempt.status !== 'in_progress') throw new Error('Attempt already submitted');

    // Calculate score from auto-graded answers
    const { data: answers } = await supabase
      .from('exam_answers')
      .select('points_earned')
      .eq('attempt_id', data.attemptId);

    const totalScore = answers?.reduce((sum, ans) => sum + (ans.points_earned || 0), 0) || 0;

    // Get exam to calculate percentage
    const { data: exam } = await supabase
      .from('exams')
      .select('total_points, passing_score')
      .eq('id', attempt.exam_id)
      .single();

    const percentage = exam?.total_points ? (totalScore / exam.total_points) * 100 : 0;
    const passed = percentage >= (exam?.passing_score || 60);

    // Check if all questions are auto-graded
    const { data: unansweredQuestions } = await supabase
      .from('exam_answers')
      .select('*')
      .eq('attempt_id', data.attemptId)
      .is('is_correct', null);

    const status = unansweredQuestions && unansweredQuestions.length > 0 ? 'submitted' : 'graded';

    // Update attempt
    const timeSpent = Math.floor((Date.now() - new Date(attempt.started_at).getTime()) / 1000);

    const { data: updatedAttempt, error } = await supabase
      .from('exam_attempts')
      .update({
        status,
        score: totalScore,
        percentage,
        passed,
        submitted_at: new Date().toISOString(),
        time_spent: timeSpent,
      })
      .eq('id', data.attemptId)
      .select()
      .single();

    if (error) throw new Error(`Failed to submit exam: ${error.message}`);
    return this.mapAttemptFromDb(updatedAttempt);
  }

  /**
   * Get exam attempt with answers
   */
  static async getExamAttempt(attemptId: string, userId: string): Promise<ExamAttempt & { answers: ExamAnswer[] }> {
    const supabase = getSupabaseClient();

    const { data: attempt, error: attemptError } = await supabase
      .from('exam_attempts')
      .select('*, exams!inner(instructor_id)')
      .eq('id', attemptId)
      .single();

    if (attemptError) throw new Error('Attempt not found');

    // Verify access
    if (attempt.student_id !== userId && attempt.exams.instructor_id !== userId) {
      throw new Error('Unauthorized access to this attempt');
    }

    const { data: answers, error: answersError } = await supabase
      .from('exam_answers')
      .select('*')
      .eq('attempt_id', attemptId);

    if (answersError) throw new Error('Failed to fetch answers');

    return {
      ...this.mapAttemptFromDb(attempt),
      answers: answers.map(this.mapAnswerFromDb),
    };
  }

  // Helper mapping functions
  private static mapExamFromDb(data: any): Exam {
    return {
      id: data.id,
      title: data.title,
      description: data.description,
      instructorId: data.instructor_id,
      instructorName: data.instructor_name,
      status: data.status as ExamStatus,
      totalPoints: data.total_points,
      passingScore: data.passing_score,
      timeLimit: data.time_limit,
      shuffleQuestions: data.shuffle_questions,
      shuffleAnswers: data.shuffle_answers,
      showResults: data.show_results,
      allowReview: data.allow_review,
      startDate: data.start_date ? new Date(data.start_date) : undefined,
      endDate: data.end_date ? new Date(data.end_date) : undefined,
      accessCode: data.access_code,
      allowedStudentIds: data.allowed_student_ids,
      subject: data.subject,
      grade: data.grade,
      tags: data.tags,
      questionCount: data.question_count,
      attemptCount: data.attempt_count,
      averageScore: data.average_score,
      createdAt: new Date(data.created_at),
      updatedAt: new Date(data.updated_at),
    };
  }

  private static mapQuestionFromDb(data: any): Question {
    return {
      id: data.id,
      examId: data.exam_id,
      type: data.type,
      text: data.text,
      description: data.description,
      points: data.points,
      difficulty: data.difficulty,
      order: data.order_index,
      choices: data.choices ? JSON.parse(data.choices) : undefined,
      correctAnswer: data.correct_answer ? JSON.parse(data.correct_answer) : undefined,
      matchingPairs: data.matching_pairs ? JSON.parse(data.matching_pairs) : undefined,
      tags: data.tags,
      imageUrl: data.image_url,
      timeLimit: data.time_limit,
      createdAt: new Date(data.created_at),
      updatedAt: new Date(data.updated_at),
    };
  }

  private static mapAttemptFromDb(data: any): ExamAttempt {
    return {
      id: data.id,
      examId: data.exam_id,
      studentId: data.student_id,
      studentName: data.student_name,
      status: data.status,
      score: data.score,
      percentage: data.percentage,
      passed: data.passed,
      startedAt: new Date(data.started_at),
      submittedAt: data.submitted_at ? new Date(data.submitted_at) : undefined,
      timeSpent: data.time_spent,
      ipAddress: data.ip_address,
      userAgent: data.user_agent,
      answers: [],
      createdAt: new Date(data.created_at),
      updatedAt: new Date(data.updated_at),
    };
  }

  private static mapAnswerFromDb(data: any): ExamAnswer {
    return {
      id: data.id,
      attemptId: data.attempt_id,
      questionId: data.question_id,
      answer: JSON.parse(data.answer),
      isCorrect: data.is_correct,
      pointsEarned: data.points_earned,
      feedback: data.feedback,
      gradedBy: data.graded_by,
      gradedAt: data.graded_at ? new Date(data.graded_at) : undefined,
      timeSpent: data.time_spent,
      createdAt: new Date(data.created_at),
      updatedAt: new Date(data.updated_at),
    };
  }
}
