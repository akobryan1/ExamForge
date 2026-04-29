import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ExamService } from '../services/ExamService';
import { Button } from '../components/Button';
import { pageTransition, stagger, fadeIn } from '../utils/animations';
import type { Exam, Question, QuestionType, DifficultyLevel } from '../types/exam';
import '../styles/pages/questions.css';

export function QuestionsPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();
  const [exam, setExam] = useState<Exam | null>(null);
  const [questions, setQuestions] = useState<Question[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showAddModal, setShowAddModal] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<Question | null>(null);

  // Form state for new/edit question
  const [formData, setFormData] = useState({
    type: 'multiple_choice' as QuestionType,
    text: '',
    description: '',
    points: 1,
    difficulty: 'medium' as DifficultyLevel,
    choices: [{ text: '', isCorrect: false }],
    correctAnswer: '',
    matchingPairs: [{ left: '', right: '' }],
    imageUrl: '',
    timeLimit: 0,
  });

  useEffect(() => {
    loadData();
  }, [examId]);

  const loadData = async () => {
    if (!examId) return;
    
    setLoading(true);
    setError('');
    try {
      const [examData, questionsData] = await Promise.all([
        ExamService.getExamById(examId),
        ExamService.getExamQuestions(examId),
      ]);
      setExam(examData);
      setQuestions(questionsData);
    } catch (err: any) {
      setError(err.message || 'Failed to load exam data');
    } finally {
      setLoading(false);
    }
  };

  const handleAddQuestion = () => {
    setEditingQuestion(null);
    setFormData({
      type: 'multiple_choice',
      text: '',
      description: '',
      points: 1,
      difficulty: 'medium',
      choices: [{ text: '', isCorrect: false }, { text: '', isCorrect: false }],
      correctAnswer: '',
      matchingPairs: [{ left: '', right: '' }],
      imageUrl: '',
      timeLimit: 0,
    });
    setShowAddModal(true);
  };

  const handleEditQuestion = (question: Question) => {
    setEditingQuestion(question);
    setFormData({
      type: question.type,
      text: question.text,
      description: question.description || '',
      points: question.points,
      difficulty: question.difficulty || 'medium',
      choices: question.choices || [{ text: '', isCorrect: false }],
      correctAnswer: typeof question.correctAnswer === 'string' ? question.correctAnswer : '',
      matchingPairs: question.matchingPairs || [{ left: '', right: '' }],
      imageUrl: question.imageUrl || '',
      timeLimit: question.timeLimit || 0,
    });
    setShowAddModal(true);
  };

  const handleDeleteQuestion = async (questionId: string) => {
    if (!examId || !confirm('Are you sure you want to delete this question?')) return;

    try {
      await ExamService.deleteQuestion(questionId, examId);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Failed to delete question');
    }
  };

  const handleSubmitQuestion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!examId) return;

    try {
      const questionData: any = {
        type: formData.type,
        text: formData.text,
        description: formData.description,
        points: formData.points,
        difficulty: formData.difficulty,
        imageUrl: formData.imageUrl || undefined,
        timeLimit: formData.timeLimit || undefined,
      };

      // Add type-specific data
      if (formData.type === 'multiple_choice') {
        questionData.choices = formData.choices.filter(c => c.text.trim());
        questionData.correctAnswer = formData.choices.findIndex(c => c.isCorrect);
      } else if (formData.type === 'true_false') {
        questionData.correctAnswer = formData.correctAnswer === 'true';
      } else if (formData.type === 'matching') {
        questionData.matchingPairs = formData.matchingPairs.filter(p => p.left && p.right);
      } else if (formData.type === 'short_answer' || formData.type === 'fill_in_blank') {
        questionData.correctAnswer = formData.correctAnswer;
      }

      if (editingQuestion) {
        await ExamService.updateQuestion(editingQuestion.id, examId, questionData);
      } else {
        await ExamService.createQuestion(examId, questionData);
      }

      setShowAddModal(false);
      await loadData();
    } catch (err: any) {
      alert(err.message || 'Failed to save question');
    }
  };

  const addChoice = () => {
    setFormData(prev => ({
      ...prev,
      choices: [...prev.choices, { text: '', isCorrect: false }],
    }));
  };

  const updateChoice = (index: number, text: string) => {
    setFormData(prev => ({
      ...prev,
      choices: prev.choices.map((c, i) => i === index ? { ...c, text } : c),
    }));
  };

  const toggleCorrectChoice = (index: number) => {
    setFormData(prev => ({
      ...prev,
      choices: prev.choices.map((c, i) => ({ ...c, isCorrect: i === index })),
    }));
  };

  const removeChoice = (index: number) => {
    setFormData(prev => ({
      ...prev,
      choices: prev.choices.filter((_, i) => i !== index),
    }));
  };

  const addMatchingPair = () => {
    setFormData(prev => ({
      ...prev,
      matchingPairs: [...prev.matchingPairs, { left: '', right: '' }],
    }));
  };

  const updateMatchingPair = (index: number, field: 'left' | 'right', value: string) => {
    setFormData(prev => ({
      ...prev,
      matchingPairs: prev.matchingPairs.map((p, i) => 
        i === index ? { ...p, [field]: value } : p
      ),
    }));
  };

  const removeMatchingPair = (index: number) => {
    setFormData(prev => ({
      ...prev,
      matchingPairs: prev.matchingPairs.filter((_, i) => i !== index),
    }));
  };

  if (loading) {
    return (
      <div className="container" style={{ paddingTop: 'var(--spacing-12)', textAlign: 'center' }}>
        <p>Loading questions...</p>
      </div>
    );
  }

  if (error || !exam) {
    return (
      <div className="container" style={{ paddingTop: 'var(--spacing-12)' }}>
        <div className="error-banner">{error || 'Exam not found'}</div>
        <Button onClick={() => navigate('/exams')}>Back to Exams</Button>
      </div>
    );
  }

  return (
    <motion.div
      className="questions-page container"
      variants={pageTransition}
      initial="initial"
      animate="animate"
      exit="exit"
    >
      <div className="page-header">
        <div>
          <Link to="/exams" className="breadcrumb">← Back to Exams</Link>
          <h1>{exam.title}</h1>
          <p className="page-subtitle">
            Manage questions • {questions.length} question{questions.length !== 1 ? 's' : ''} • {exam.totalPoints} points total
          </p>
        </div>
        <div className="header-actions">
          <Button onClick={handleAddQuestion}>Add Question</Button>
        </div>
      </div>

      {questions.length === 0 ? (
        <div className="empty-state">
          <h3>No questions yet</h3>
          <p>Add your first question to get started.</p>
          <Button onClick={handleAddQuestion}>Add Question</Button>
        </div>
      ) : (
        <motion.div className="questions-list" variants={stagger}>
          {questions.map((question, index) => (
            <motion.div key={question.id} className="question-card" variants={fadeIn}>
              <div className="question-header">
                <span className="question-number">Question {index + 1}</span>
                <div className="question-meta">
                  <span className={`question-type type-${question.type}`}>
                    {question.type.replace('_', ' ')}
                  </span>
                  <span className={`difficulty-badge difficulty-${question.difficulty}`}>
                    {question.difficulty}
                  </span>
                  <span className="points-badge">{question.points} pts</span>
                </div>
              </div>
              <div className="question-text">{question.text}</div>
              {question.description && (
                <div className="question-description">{question.description}</div>
              )}
              
              {question.type === 'multiple_choice' && question.choices && (
                <div className="choices-preview">
                  {question.choices.map((choice, i) => (
                    <div key={i} className={`choice-item ${choice.isCorrect ? 'correct' : ''}`}>
                      {String.fromCharCode(65 + i)}. {choice.text}
                      {choice.isCorrect && <span className="correct-indicator">✓</span>}
                    </div>
                  ))}
                </div>
              )}

              <div className="question-actions">
                <Button variant="outline" size="small" onClick={() => handleEditQuestion(question)}>
                  Edit
                </Button>
                <Button variant="outline" size="small" onClick={() => handleDeleteQuestion(question.id)}>
                  Delete
                </Button>
              </div>
            </motion.div>
          ))}
        </motion.div>
      )}

      {/* Add/Edit Question Modal */}
      <AnimatePresence>
        {showAddModal && (
          <motion.div
            className="modal-overlay"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setShowAddModal(false)}
          >
            <motion.div
              className="modal-content"
              initial={{ scale: 0.9, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.9, opacity: 0 }}
              onClick={(e) => e.stopPropagation()}
            >
              <div className="modal-header">
                <h2>{editingQuestion ? 'Edit Question' : 'Add Question'}</h2>
                <button className="close-btn" onClick={() => setShowAddModal(false)}>×</button>
              </div>

              <form onSubmit={handleSubmitQuestion} className="question-form">
                <div className="form-row">
                  <div className="form-group">
                    <label>Question Type *</label>
                    <select
                      value={formData.type}
                      onChange={(e) => setFormData(prev => ({ ...prev, type: e.target.value as QuestionType }))}
                      required
                    >
                      <option value="multiple_choice">Multiple Choice</option>
                      <option value="true_false">True/False</option>
                      <option value="short_answer">Short Answer</option>
                      <option value="essay">Essay</option>
                      <option value="fill_in_blank">Fill in the Blank</option>
                      <option value="matching">Matching</option>
                    </select>
                  </div>

                  <div className="form-group">
                    <label>Points *</label>
                    <input
                      type="number"
                      min="1"
                      value={formData.points}
                      onChange={(e) => setFormData(prev => ({ ...prev, points: parseInt(e.target.value) }))}
                      required
                    />
                  </div>

                  <div className="form-group">
                    <label>Difficulty</label>
                    <select
                      value={formData.difficulty}
                      onChange={(e) => setFormData(prev => ({ ...prev, difficulty: e.target.value as DifficultyLevel }))}
                    >
                      <option value="easy">Easy</option>
                      <option value="medium">Medium</option>
                      <option value="hard">Hard</option>
                    </select>
                  </div>
                </div>

                <div className="form-group">
                  <label>Question Text *</label>
                  <textarea
                    value={formData.text}
                    onChange={(e) => setFormData(prev => ({ ...prev, text: e.target.value }))}
                    placeholder="Enter your question..."
                    required
                    rows={3}
                  />
                </div>

                <div className="form-group">
                  <label>Description (optional)</label>
                  <textarea
                    value={formData.description}
                    onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
                    placeholder="Additional context or instructions..."
                    rows={2}
                  />
                </div>

                {/* Multiple Choice Options */}
                {formData.type === 'multiple_choice' && (
                  <div className="form-group">
                    <label>Answer Choices</label>
                    {formData.choices.map((choice, index) => (
                      <div key={index} className="choice-input-group">
                        <input
                          type="radio"
                          name="correctChoice"
                          checked={choice.isCorrect}
                          onChange={() => toggleCorrectChoice(index)}
                        />
                        <input
                          type="text"
                          value={choice.text}
                          onChange={(e) => updateChoice(index, e.target.value)}
                          placeholder={`Choice ${String.fromCharCode(65 + index)}`}
                          required
                        />
                        {formData.choices.length > 2 && (
                          <button type="button" onClick={() => removeChoice(index)} className="remove-btn">×</button>
                        )}
                      </div>
                    ))}
                    <Button type="button" variant="outline" size="small" onClick={addChoice}>
                      Add Choice
                    </Button>
                  </div>
                )}

                {/* True/False */}
                {formData.type === 'true_false' && (
                  <div className="form-group">
                    <label>Correct Answer</label>
                    <select
                      value={formData.correctAnswer}
                      onChange={(e) => setFormData(prev => ({ ...prev, correctAnswer: e.target.value }))}
                      required
                    >
                      <option value="">Select answer...</option>
                      <option value="true">True</option>
                      <option value="false">False</option>
                    </select>
                  </div>
                )}

                {/* Short Answer / Fill in Blank */}
                {(formData.type === 'short_answer' || formData.type === 'fill_in_blank') && (
                  <div className="form-group">
                    <label>Correct Answer (for auto-grading)</label>
                    <input
                      type="text"
                      value={formData.correctAnswer}
                      onChange={(e) => setFormData(prev => ({ ...prev, correctAnswer: e.target.value }))}
                      placeholder="Expected answer..."
                    />
                    <small>Leave blank if manual grading is required</small>
                  </div>
                )}

                {/* Matching Pairs */}
                {formData.type === 'matching' && (
                  <div className="form-group">
                    <label>Matching Pairs</label>
                    {formData.matchingPairs.map((pair, index) => (
                      <div key={index} className="matching-pair-group">
                        <input
                          type="text"
                          value={pair.left}
                          onChange={(e) => updateMatchingPair(index, 'left', e.target.value)}
                          placeholder="Left item"
                          required
                        />
                        <span>↔</span>
                        <input
                          type="text"
                          value={pair.right}
                          onChange={(e) => updateMatchingPair(index, 'right', e.target.value)}
                          placeholder="Right item"
                          required
                        />
                        {formData.matchingPairs.length > 1 && (
                          <button type="button" onClick={() => removeMatchingPair(index)} className="remove-btn">×</button>
                        )}
                      </div>
                    ))}
                    <Button type="button" variant="outline" size="small" onClick={addMatchingPair}>
                      Add Pair
                    </Button>
                  </div>
                )}

                <div className="modal-actions">
                  <Button type="button" variant="outline" onClick={() => setShowAddModal(false)}>
                    Cancel
                  </Button>
                  <Button type="submit">
                    {editingQuestion ? 'Update Question' : 'Add Question'}
                  </Button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}
