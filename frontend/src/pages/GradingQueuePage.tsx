import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { useGradingQueue, useGradeQuestion } from '../hooks/useExamQueries';
import { apiClient } from '../services/apiClient';
import { pageTransition, fadeIn } from '../utils/animations';
import type { Question } from '../types/exam';
import '../styles/pages/grading-queue.css';

const AI_MODELS = [
  { value: 'openai/gpt-oss-120b:free', label: 'OpenRouter — openai/gpt-oss-120b:free' },
  { value: 'openai/gpt-4o-mini', label: 'OpenAI — GPT-4o Mini' },
  { value: 'google/gemini-2.0-flash-001', label: 'Google — Gemini 2.0 Flash' },
  { value: 'deepseek/deepseek-chat', label: 'DeepSeek — DeepSeek Chat' },
  { value: 'anthropic/claude-3.5-haiku', label: 'Anthropic — Claude 3.5 Haiku' },
  { value: 'other', label: 'Other (custom API key)' },
];

interface GradingItem {
  attemptId: string;
  studentName: string;
  studentEmail: string;
  examTitle: string;
  examId: string;
  question: Question;
  answer: string;
  submittedAt: Date;
  currentGrade?: number;
  feedback?: string;
}

export function GradingQueuePage() {
  const { data: items = [], isLoading: loading } = useGradingQueue();
  const gradeMutation = useGradeQuestion();
  const [filter, setFilter] = useState<'pending' | 'graded' | 'all'>('pending');
  const [selectedItem, setSelectedItem] = useState<any | null>(null);
  const [gradeValue, setGradeValue] = useState('');
  const [feedbackText, setFeedbackText] = useState('');

  // AI grading state
  const [aiModel, setAiModel] = useState('openai/gpt-oss-120b:free');
  const [aiApiKey, setAiApiKey] = useState('');
  const [aiGrading, setAiGrading] = useState(false);

  const handleAiGrade = async () => {
    if (!selectedItem || !aiApiKey) return;
    try {
      setAiGrading(true);
      const { data } = await apiClient.post('/api/exams/grading/ai-grade', {
        questionText: selectedItem.question.text,
        studentAnswer: selectedItem.answer,
        maxPoints: selectedItem.question.points,
        model: aiModel === 'other' ? '' : aiModel,
        apiKey: aiApiKey,
      });
      setGradeValue(data.score.toString());
      setFeedbackText(data.feedback || '');
    } catch (err: any) {
      alert('AI grading failed: ' + (err.response?.data?.error || err.message));
    } finally {
      setAiGrading(false);
    }
  };

  const handleGradeSubmit = async () => {
    if (!selectedItem || !gradeValue) return;

    try {
      await gradeMutation.mutateAsync({
        attemptId: selectedItem.attemptId,
        questionId: selectedItem.question.id,
        earnedPoints: parseFloat(gradeValue),
        feedback: feedbackText || undefined,
      });
      
      alert('Grade submitted successfully!');
      setSelectedItem(null);
      setGradeValue('');
      setFeedbackText('');
    } catch (error: any) {
      alert('Failed to submit grade: ' + error.message);
    }
  };

  const filteredItems = items.filter((item: any) => {
    if (filter === 'pending') return !item.currentGrade;
    if (filter === 'graded') return item.currentGrade !== undefined;
    return true;
  });

  return (
    <MainLayout>
      <motion.div
        className="grading-queue-page"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        <div className="page-header">
          <div>
            <h1>Grading Queue</h1>
            <p className="page-subtitle">Review and grade student essay submissions</p>
          </div>
          <div className="header-actions">
            <select
              value={filter}
              onChange={(e) => setFilter(e.target.value as 'pending' | 'graded' | 'all')}
              className="filter-select"
            >
              <option value="pending">Pending</option>
              <option value="graded">Graded</option>
              <option value="all">All</option>
            </select>
          </div>
        </div>

        {loading ? (
          <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
            <p>Loading grading queue...</p>
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="empty-state">
            <h3>No submissions to grade</h3>
            <p>
              {filter === 'pending' 
                ? 'All essay submissions have been graded!'
                : 'No submissions found.'}
            </p>
          </div>
        ) : (
          <div className="grading-content">
            {/* Queue List */}
            <div className="queue-list">
              {filteredItems.map((item) => (
                <motion.div
                  key={`${item.attemptId}-${item.question.id}`}
                  className={`queue-item ${selectedItem === item ? 'active' : ''}`}
                  onClick={() => {
                    setSelectedItem(item);
                    setGradeValue(item.currentGrade?.toString() || '');
                    setFeedbackText(item.feedback || '');
                  }}
                  variants={fadeIn}
                  whileHover={{ scale: 1.01 }}
                >
                  <div className="queue-item-header">
                    <span className="student-name">{item.studentName}</span>
                    {item.currentGrade !== undefined ? (
                      <span className="graded-badge">✓ Graded</span>
                    ) : (
                      <span className="pending-badge">Pending</span>
                    )}
                  </div>
                  <p className="exam-title">{item.examTitle}</p>
                  <p className="question-preview">
                    {item.question.text.substring(0, 100)}
                    {item.question.text.length > 100 ? '...' : ''}
                  </p>
                  <div className="queue-item-footer">
                    <span className="submitted-time">
                      {new Date(item.submittedAt).toLocaleDateString()}
                    </span>
                    <span className="points-info">{item.question.points} pts</span>
                  </div>
                </motion.div>
              ))}
            </div>

            {/* Grading Panel */}
            <AnimatePresence mode="wait">
              {selectedItem && (
                <motion.div
                  className="grading-panel"
                  initial={{ opacity: 0, x: 20 }}
                  animate={{ opacity: 1, x: 0 }}
                  exit={{ opacity: 0, x: 20 }}
                  transition={{ duration: 0.2 }}
                >
                  <div className="panel-header">
                    <h2>Grade Submission</h2>
                    <button
                      className="close-btn"
                      onClick={() => setSelectedItem(null)}
                    >
                      ×
                    </button>
                  </div>

                  <div className="panel-content">
                    {/* Student Info */}
                    <div className="info-section">
                      <h3>Student Information</h3>
                      <div className="info-grid">
                        <div>
                          <strong>Name:</strong> {selectedItem.studentName}
                        </div>
                        <div>
                          <strong>Email:</strong> {selectedItem.studentEmail}
                        </div>
                        <div>
                          <strong>Exam:</strong> {selectedItem.examTitle}
                        </div>
                        <div>
                          <strong>Submitted:</strong> {new Date(selectedItem.submittedAt).toLocaleString()}
                        </div>
                      </div>
                    </div>

                    {/* Question */}
                    <div className="question-section">
                      <h3>Question</h3>
                      <p className="question-text">{selectedItem.question.text}</p>
                      {selectedItem.question.description && (
                        <p className="question-description">{selectedItem.question.description}</p>
                      )}
                      <div className="question-meta">
                        <span className="points-badge">{selectedItem.question.points} points</span>
                      </div>
                    </div>

                    {/* Student Answer */}
                    <div className="answer-section">
                      <h3>Student Answer</h3>
                      <div className="answer-content">
                        {selectedItem.answer}
                      </div>
                    </div>

                    {/* AI Auto-Grading */}
                    <div className="grading-form">
                      <h3>Auto-grade with AI</h3>
                      <div className="form-group">
                        <label>AI Model</label>
                        <select value={aiModel} onChange={(e) => setAiModel(e.target.value)}>
                          {AI_MODELS.map(m => (
                            <option key={m.value} value={m.value}>{m.label}</option>
                          ))}
                        </select>
                      </div>
                      <div className="form-group">
                        <label>{aiModel === 'openai/gpt-oss-120b:free' ? 'OpenRouter API Key' : 'API Key'}</label>
                        <input
                          type="password"
                          value={aiApiKey}
                          onChange={(e) => setAiApiKey(e.target.value)}
                          placeholder={aiModel === 'openai/gpt-oss-120b:free' ? 'sk-or-v1-...' : 'Enter your API key'}
                        />
                        <p className="form-help">
                          {aiModel === 'openai/gpt-oss-120b:free'
                            ? 'Get your free key at openrouter.ai/keys'
                            : aiModel === 'other'
                              ? 'Enter any OpenAI-compatible API key'
                              : `Key for ${aiModel.split('/')[0]}`}
                        </p>
                      </div>
                      <Button
                        variant="accent"
                        onClick={handleAiGrade}
                        disabled={aiGrading || !aiApiKey}
                        style={{ width: '100%', marginBottom: 16 }}
                      >
                        {aiGrading ? 'Grading...' : '🤖 Auto-grade with AI'}
                      </Button>
                    </div>

                    {/* Manual Grading Form */}
                    <div className="grading-form">
                      <div className="form-group">
                        <label htmlFor="grade">Grade (out of {selectedItem.question.points})</label>
                        <input
                          type="number"
                          id="grade"
                          min="0"
                          max={selectedItem.question.points}
                          step="0.5"
                          value={gradeValue}
                          onChange={(e) => setGradeValue(e.target.value)}
                          placeholder="Enter points"
                        />
                      </div>

                      <div className="form-group">
                        <label htmlFor="feedback">Feedback (Optional)</label>
                        <textarea
                          id="feedback"
                          value={feedbackText}
                          onChange={(e) => setFeedbackText(e.target.value)}
                          placeholder="Provide feedback to the student..."
                          rows={6}
                        />
                      </div>

                      <div className="form-actions">
                        <Button
                          variant="outline"
                          onClick={() => setSelectedItem(null)}
                          disabled={gradeMutation.isPending}
                        >
                          Cancel
                        </Button>
                        <Button
                          variant="primary"
                          onClick={handleGradeSubmit}
                          disabled={gradeMutation.isPending || !gradeValue}
                        >
                          {gradeMutation.isPending ? 'Submitting...' : 'Submit Grade'}
                        </Button>
                      </div>
                    </div>
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )}
      </motion.div>
    </MainLayout>
  );
}
