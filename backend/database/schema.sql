-- ExamForge Database Schema for Supabase
-- This script creates the tables for exam management

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Exams table
CREATE TABLE IF NOT EXISTS exams (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  title VARCHAR(255) NOT NULL,
  description TEXT,
  instructor_id UUID NOT NULL REFERENCES examforge_users(id) ON DELETE CASCADE,
  instructor_name VARCHAR(255),
  
  -- Exam settings
  status VARCHAR(20) NOT NULL DEFAULT 'draft',
  total_points INTEGER NOT NULL DEFAULT 0,
  passing_score INTEGER NOT NULL DEFAULT 60,
  time_limit INTEGER, -- Time limit in minutes
  shuffle_questions BOOLEAN DEFAULT FALSE,
  shuffle_answers BOOLEAN DEFAULT FALSE,
  show_results BOOLEAN DEFAULT TRUE,
  allow_review BOOLEAN DEFAULT TRUE,
  
  -- Scheduling
  start_date TIMESTAMPTZ,
  end_date TIMESTAMPTZ,
  
  -- Access control
  access_code VARCHAR(50),
  allowed_student_ids UUID[],
  
  -- Metadata
  subject VARCHAR(100),
  grade VARCHAR(50),
  tags TEXT[],
  
  -- Statistics
  question_count INTEGER DEFAULT 0,
  attempt_count INTEGER DEFAULT 0,
  average_score DECIMAL(5,2),
  
  -- Timestamps
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW(),
  
  CHECK (status IN ('draft', 'published', 'active', 'completed', 'archived')),
  CHECK (passing_score >= 0 AND passing_score <= 100),
  CHECK (total_points >= 0)
);

-- Questions table
CREATE TABLE IF NOT EXISTS questions (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  exam_id UUID NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
  type VARCHAR(30) NOT NULL,
  text TEXT NOT NULL,
  description TEXT,
  points INTEGER NOT NULL DEFAULT 1,
  difficulty VARCHAR(20) NOT NULL DEFAULT 'medium',
  order_index INTEGER NOT NULL DEFAULT 0,
  
  -- Question type specific data (stored as JSONB for flexibility)
  choices JSONB, -- For multiple choice: [{ id, text, isCorrect, order }]
  correct_answer JSONB, -- For various question types
  matching_pairs JSONB, -- For matching questions: [{ left, right }]
  
  -- Metadata
  tags TEXT[],
  image_url VARCHAR(500),
  time_limit INTEGER, -- Time limit in seconds for this question
  
  -- Timestamps
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW(),
  
  CHECK (type IN ('multiple_choice', 'true_false', 'short_answer', 'essay', 'fill_in_blank', 'matching')),
  CHECK (difficulty IN ('easy', 'medium', 'hard')),
  CHECK (points > 0)
);

-- Exam attempts table
CREATE TABLE IF NOT EXISTS exam_attempts (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  exam_id UUID NOT NULL REFERENCES exams(id) ON DELETE CASCADE,
  student_id UUID NOT NULL REFERENCES examforge_users(id) ON DELETE CASCADE,
  student_name VARCHAR(255),
  
  -- Attempt details
  status VARCHAR(20) NOT NULL DEFAULT 'in_progress',
  score INTEGER,
  percentage DECIMAL(5,2),
  passed BOOLEAN,
  
  -- Timing
  started_at TIMESTAMPTZ DEFAULT NOW(),
  submitted_at TIMESTAMPTZ,
  time_spent INTEGER, -- Time spent in seconds
  
  -- Metadata
  ip_address VARCHAR(45),
  user_agent TEXT,
  
  -- Timestamps
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW(),
  
  CHECK (status IN ('in_progress', 'submitted', 'graded')),
  CHECK (score >= 0),
  CHECK (percentage >= 0 AND percentage <= 100)
);

-- Exam answers table
CREATE TABLE IF NOT EXISTS exam_answers (
  id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
  attempt_id UUID NOT NULL REFERENCES exam_attempts(id) ON DELETE CASCADE,
  question_id UUID NOT NULL REFERENCES questions(id) ON DELETE CASCADE,
  
  -- Answer data
  answer JSONB NOT NULL, -- Can be string, array, or object depending on question type
  is_correct BOOLEAN,
  points_earned DECIMAL(5,2),
  
  -- Grading (for essays and short answers)
  feedback TEXT,
  graded_by UUID REFERENCES examforge_users(id),
  graded_at TIMESTAMPTZ,
  
  -- Timing
  time_spent INTEGER, -- Time spent on this question in seconds
  
  -- Timestamps
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW(),
  
  UNIQUE(attempt_id, question_id)
);

-- Indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_exams_instructor ON exams(instructor_id);
CREATE INDEX IF NOT EXISTS idx_exams_status ON exams(status);
CREATE INDEX IF NOT EXISTS idx_exams_created_at ON exams(created_at DESC);

CREATE INDEX IF NOT EXISTS idx_questions_exam ON questions(exam_id);
CREATE INDEX IF NOT EXISTS idx_questions_order ON questions(exam_id, order_index);

CREATE INDEX IF NOT EXISTS idx_attempts_exam ON exam_attempts(exam_id);
CREATE INDEX IF NOT EXISTS idx_attempts_student ON exam_attempts(student_id);
CREATE INDEX IF NOT EXISTS idx_attempts_status ON exam_attempts(status);

CREATE INDEX IF NOT EXISTS idx_answers_attempt ON exam_answers(attempt_id);
CREATE INDEX IF NOT EXISTS idx_answers_question ON exam_answers(question_id);

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Triggers to auto-update updated_at
CREATE TRIGGER update_exams_updated_at BEFORE UPDATE ON exams
  FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_questions_updated_at BEFORE UPDATE ON questions
  FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_exam_attempts_updated_at BEFORE UPDATE ON exam_attempts
  FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_exam_answers_updated_at BEFORE UPDATE ON exam_answers
  FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Function to update exam statistics when questions are added/removed
CREATE OR REPLACE FUNCTION update_exam_stats()
RETURNS TRIGGER AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    UPDATE exams 
    SET 
      question_count = (SELECT COUNT(*) FROM questions WHERE exam_id = OLD.exam_id),
      total_points = (SELECT COALESCE(SUM(points), 0) FROM questions WHERE exam_id = OLD.exam_id)
    WHERE id = OLD.exam_id;
    RETURN OLD;
  ELSE
    UPDATE exams 
    SET 
      question_count = (SELECT COUNT(*) FROM questions WHERE exam_id = NEW.exam_id),
      total_points = (SELECT COALESCE(SUM(points), 0) FROM questions WHERE exam_id = NEW.exam_id)
    WHERE id = NEW.exam_id;
    RETURN NEW;
  END IF;
END;
$$ LANGUAGE plpgsql;

-- Trigger to update exam stats when questions change
CREATE TRIGGER update_exam_stats_trigger
AFTER INSERT OR UPDATE OR DELETE ON questions
FOR EACH ROW EXECUTE FUNCTION update_exam_stats();

-- Row Level Security (RLS) Policies
ALTER TABLE exams ENABLE ROW LEVEL SECURITY;
ALTER TABLE questions ENABLE ROW LEVEL SECURITY;
ALTER TABLE exam_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE exam_answers ENABLE ROW LEVEL SECURITY;

-- Exams: Instructors can manage their own exams, students can view published exams
CREATE POLICY exams_instructor_policy ON exams
  FOR ALL
  USING (instructor_id = auth.uid());

CREATE POLICY exams_student_view_policy ON exams
  FOR SELECT
  USING (status IN ('published', 'active'));

-- Questions: Linked to exam access
CREATE POLICY questions_policy ON questions
  FOR ALL
  USING (
    EXISTS (
      SELECT 1 FROM exams 
      WHERE exams.id = questions.exam_id 
      AND (exams.instructor_id = auth.uid() OR exams.status IN ('published', 'active'))
    )
  );

-- Exam attempts: Students can manage their own attempts
CREATE POLICY attempts_student_policy ON exam_attempts
  FOR ALL
  USING (student_id = auth.uid());

CREATE POLICY attempts_instructor_view_policy ON exam_attempts
  FOR SELECT
  USING (
    EXISTS (
      SELECT 1 FROM exams 
      WHERE exams.id = exam_attempts.exam_id 
      AND exams.instructor_id = auth.uid()
    )
  );

-- Exam answers: Linked to attempt access
CREATE POLICY answers_policy ON exam_answers
  FOR ALL
  USING (
    EXISTS (
      SELECT 1 FROM exam_attempts 
      WHERE exam_attempts.id = exam_answers.attempt_id 
      AND (
        exam_attempts.student_id = auth.uid() 
        OR EXISTS (
          SELECT 1 FROM exams 
          WHERE exams.id = exam_attempts.exam_id 
          AND exams.instructor_id = auth.uid()
        )
      )
    )
  );
