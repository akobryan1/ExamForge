import { useState, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { useGradingQueue, useGradeQuestion } from '../hooks/useExamQueries';
import { apiClient } from '../services/apiClient';
import { pageTransition, fadeIn } from '../utils/animations';
import type { Question } from '../types/exam';
import '../styles/pages/grading-queue.css';

const AI_GRADE_URL = () => {
  const workerUrl = import.meta.env.VITE_AI_GRADE_WORKER_URL;
  return workerUrl ? workerUrl + '/api/exams/grading/ai-grade' : '/api/exams/grading/ai-grade';
};

const AI_MODELS = [
  { value: 'deepseek-chat', label: 'DeepSeek — deepseek-chat (Flash)' },
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
  const [aiModel, setAiModel] = useState('deepseek-chat');
  const [aiGrading, setAiGrading] = useState(false);
  const [aiResult, setAiResult] = useState<{ score: number; feedback: string; justification: string } | null>(null);
  const [manualJustification, setManualJustification] = useState('');

  // Grade All state
  const [gradingAll, setGradingAll] = useState(false);
  const [gradingAllProgress, setGradingAllProgress] = useState({ done: 0, total: 0, failed: 0 });
  const [gradingAllSummary, setGradingAllSummary] = useState<string | null>(null);

  // Cancel refs
  const aiAbortRef = useRef<AbortController | null>(null);
  const gradeAllCancelledRef = useRef(false);

  const handleAiGrade = async () => {
    if (!selectedItem) return;
    try {
      // Create abort controller for this request
      aiAbortRef.current?.abort();
      aiAbortRef.current = new AbortController();

      setAiGrading(true);
      setAiResult(null);
      const { data } = await apiClient.post(AI_GRADE_URL(), {
        questionText: selectedItem.question.text,
        studentAnswer: selectedItem.answer,
        maxPoints: selectedItem.question.points,
        model: aiModel === 'other' ? '' : aiModel,
        keyPoints: selectedItem.question.keyPoints || undefined,
        modelAnswer: selectedItem.question.modelAnswer || undefined,
      }, {
        signal: aiAbortRef.current.signal,
      });
      // Auto-fill the grade fields with AI result
      setAiResult(data);
      setGradeValue(data.score.toString());
      setFeedbackText(data.feedback || '');
      setManualJustification(data.justification || '');
    } catch (err: any) {
      if (err?.name === 'CanceledError' || err?.code === 'ERR_CANCELED') return;
      alert('AI grading failed: ' + (err.response?.data?.error || err.message));
    } finally {
      setAiGrading(false);
      aiAbortRef.current = null;
    }
  };

  const cancelAiGrade = () => {
    aiAbortRef.current?.abort();
  };

  const handleGradeSubmit = async () => {
    if (!selectedItem || !gradeValue) return;

    try {
      const feedback = manualJustification
        ? (feedbackText ? `${feedbackText}\n\nJustification: ${manualJustification}` : `Justification: ${manualJustification}`)
        : feedbackText;
      await gradeMutation.mutateAsync({
        attemptId: selectedItem.attemptId,
        questionId: selectedItem.question.id,
        earnedPoints: parseFloat(gradeValue),
        feedback: feedback || undefined,
      });
      
      alert('Grade submitted successfully!');
      setSelectedItem(null);
      setGradeValue('');
      setFeedbackText('');
      setManualJustification('');
      setAiResult(null);
    } catch (error: any) {
      alert('Failed to submit grade: ' + error.message);
    }
  };

  const handleGradeAll = async () => {
    const pending = items.filter((item: any) => !item.isGraded);
    if (pending.length === 0) return;

    const DELAY_MS = 12000; // 12s between requests = 5/min, stays under rate limit
    gradeAllCancelledRef.current = false;

    setGradingAll(true);
    setGradingAllSummary(null);
    setGradingAllProgress({ done: 0, total: pending.length, failed: 0 });

    let failed = 0;

    for (let i = 0; i < pending.length; i++) {
      if (gradeAllCancelledRef.current) break;

      const item = pending[i];
      try {
        const { data: aiData } = await apiClient.post(AI_GRADE_URL(), {
          questionText: item.question.text,
          studentAnswer: item.answer,
          maxPoints: item.question.points,
          model: aiModel,
          keyPoints: item.question.keyPoints || undefined,
          modelAnswer: item.question.modelAnswer || undefined,
        });

        await gradeMutation.mutateAsync({
          attemptId: item.attemptId,
          questionId: item.question.id,
          earnedPoints: aiData.score,
          feedback: aiData.feedback || undefined,
        });

        setGradingAllProgress(prev => ({ ...prev, done: prev.done + 1 }));
      } catch {
        failed++;
        setGradingAllProgress(prev => ({ ...prev, done: prev.done + 1, failed }));
      }

      // Throttle: wait 12s between requests to stay within 5 req/60s rate limit
      if (i < pending.length - 1 && !gradeAllCancelledRef.current) {
        await new Promise(r => setTimeout(r, DELAY_MS));
      }
    }

    setGradingAll(false);

    if (gradeAllCancelledRef.current) {
      setGradingAllSummary(
        `Cancelled. ${gradingAllProgress.done} graded, ${pending.length - gradingAllProgress.done} remaining.`
      );
      gradeAllCancelledRef.current = false;
      return;
    }

    const succeeded = pending.length - failed;
    setGradingAllSummary(
      failed === 0
        ? `All ${succeeded} essays graded successfully.`
        : `${succeeded} graded, ${failed} failed. Check failed items and grade manually.`
    );
  };

  const cancelGradeAll = () => {
    gradeAllCancelledRef.current = true;
  };

  const filteredItems = items.filter((item: any) => {
    if (filter === 'pending') return !item.isGraded;
    if (filter === 'graded') return !!item.isGraded;
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
            {items.some((i: any) => !i.isGraded) && (
              <Button
                variant="accent"
                onClick={handleGradeAll}
                disabled={gradingAll || aiGrading}
              >
                {gradingAll ? (
                  <><span className="grade-all-spinner" /> Grading {gradingAllProgress.done + 1} of {gradingAllProgress.total}…</>
                ) : '🤖 Grade All Pending'}
              </Button>
            )}
          </div>
        </div>

        {/* Grade All Progress Banner */}
        {gradingAll && (
          <motion.div
            className="grade-all-banner"
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
          >
            <div className="grade-all-progress-track">
              <div
                className="grade-all-progress-fill grade-all-progress-active"
                style={{ width: `${gradingAllProgress.total > 0 ? (gradingAllProgress.done / gradingAllProgress.total) * 100 : 0}%` }}
              />
            </div>
            <div className="grade-all-banner-row">
              <span className="grade-all-progress-text">
                <span className="grade-all-spinner grade-all-spinner-sm" />
                Grading essays: {gradingAllProgress.done} of {gradingAllProgress.total}
                {' · '}~{Math.max(0, Math.ceil(((gradingAllProgress.total - gradingAllProgress.done) * 12) / 60))} min remaining
                {gradingAllProgress.failed > 0 && ` · ${gradingAllProgress.failed} failed`}
              </span>
              <Button variant="outline" onClick={cancelGradeAll} className="grade-all-cancel-btn">
                Cancel
              </Button>
            </div>
          </motion.div>
        )}

        {/* Grade All Summary Banner */}
        {gradingAllSummary && !gradingAll && (
          <motion.div
            className={`grade-all-summary ${gradingAllSummary.includes('failed') ? 'grade-all-summary-err' : 'grade-all-summary-ok'}`}
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
          >
            <span>{gradingAllSummary}</span>
            <button className="grade-all-dismiss" onClick={() => setGradingAllSummary(null)}>×</button>
          </motion.div>
        )}

        {loading ? (
          <div style={{ textAlign: 'center', padding: 'var(--spacing-12)', color: 'var(--ledger-ink-soft)' }}>
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
                  className={`queue-item ${selectedItem === item ? 'active' : ''} ${(aiGrading || gradingAll) ? 'queue-item-disabled' : ''}`}
                  onClick={() => {
                    if (aiGrading || gradingAll) return;
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
                      <span className="stamp stamp-graded"><img src="/icons/status/correct.png" alt="" className="stamp-icon" /> Graded</span>
                    ) : (
                      <span className="stamp stamp-pending">Pending</span>
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
                      className="panel-close-btn"
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
                          <strong>Name</strong>
                          <span>{selectedItem.studentName}</span>
                        </div>
                        <div>
                          <strong>Email</strong>
                          <span>{selectedItem.studentEmail}</span>
                        </div>
                        <div>
                          <strong>Exam</strong>
                          <span>{selectedItem.examTitle}</span>
                        </div>
                        <div>
                          <strong>Submitted</strong>
                          <span>{new Date(selectedItem.submittedAt).toLocaleString()}</span>
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
                        <div className="points-seal">{selectedItem.question.points}<span>pt</span></div>
                      </div>
                    </div>

                    {/* Student Answer */}
                    <div className="answer-section">
                      <h3>Student Answer</h3>
                      <div className="answer-content">
                        {selectedItem.answer}
                      </div>
                    </div>

                    {/* Model Answer & Key Points (from exam question data) */}
                    {(selectedItem.question.modelAnswer || selectedItem.question.keyPoints) && (
                      <div className="reference-section">
                        <h3>Reference <span className="ai-chip">Exam Data</span></h3>
                        {selectedItem.question.modelAnswer && (
                          <div className="form-group">
                            <label>Model Answer</label>
                            <div className="answer-content reference-text">
                              {selectedItem.question.modelAnswer}
                            </div>
                          </div>
                        )}
                        {selectedItem.question.keyPoints && (
                          <div className="form-group">
                            <label>Key Points</label>
                            <div className="answer-content reference-text">
                              {selectedItem.question.keyPoints}
                            </div>
                          </div>
                        )}
                      </div>
                    )}

                    {/* AI Auto-Grading */}
                    <div className="grading-form">
                      <h3>Auto-grade with AI <span className="ai-chip">AI Assist</span></h3>
                      <div className="form-group">
                        <label>AI Model</label>
                        <select value={aiModel} onChange={(e) => setAiModel(e.target.value)}>
                          {AI_MODELS.map(m => (
                            <option key={m.value} value={m.value}>{m.label}</option>
                          ))}
                        </select>
                      </div>
                      <Button
                        variant="accent"
                        onClick={handleAiGrade}
                        disabled={aiGrading || gradingAll}
                        style={{ width: '100%', marginBottom: aiGrading ? 8 : 16 }}
                      >
                        {aiGrading ? (
                          <><span className="grade-all-spinner" /> Grading…</>
                        ) : '🤖 Auto-grade with AI'}
                      </Button>

                      {aiGrading && (
                        <Button
                          variant="outline"
                          onClick={cancelAiGrade}
                          style={{ width: '100%', marginBottom: 16 }}
                        >
                          ✕ Cancel
                        </Button>
                      )}

                      {aiResult && (
                        <div className="ai-result">
                          <div style={{ marginBottom: 8 }}>
                            <span className="ai-label">AI Score</span>
                            <span className="ai-score">{aiResult.score}</span>
                            <span className="ai-score-total"> / {selectedItem.question.points}</span>
                          </div>
                          <div style={{ marginBottom: 8 }}>
                            <span className="ai-label">Feedback</span>
                            <p className="ai-feedback">{aiResult.feedback}</p>
                          </div>
                          {aiResult.justification && (
                            <div style={{ marginBottom: 8 }}>
                              <span className="ai-label">Justification</span>
                              <p className="ai-justification">{aiResult.justification}</p>
                            </div>
                          )}

                        </div>
                      )}
                    </div>

                    {/* Manual Grading Form */}
                    <div className="grading-form">
                      <h3>Manual Grade</h3>
                      <div className="form-group">
                        <label htmlFor="grade">Grade</label>
                        <div className="grade-fraction">
                          <input
                            type="number"
                            id="grade"
                            min="0"
                            max={selectedItem.question.points}
                            step="0.5"
                            value={gradeValue}
                            onChange={(e) => setGradeValue(e.target.value)}
                            placeholder="Enter points"
                            disabled={aiGrading || gradingAll}
                          />
                          <span>/ {selectedItem.question.points} points</span>
                        </div>
                      </div>

                      <div className="form-group">
                        <label htmlFor="feedback">Feedback (Optional)</label>
                        <textarea
                          id="feedback"
                          value={feedbackText}
                          onChange={(e) => setFeedbackText(e.target.value)}
                          placeholder="Provide feedback to the student..."
                          rows={4}
                          disabled={aiGrading || gradingAll}
                        />
                      </div>

                      <div className="form-group">
                        <label htmlFor="justification">Justification (Optional)</label>
                        <textarea
                          id="justification"
                          value={manualJustification}
                          onChange={(e) => setManualJustification(e.target.value)}
                          placeholder="Explain why this score was given..."
                          rows={3}
                          disabled={aiGrading || gradingAll}
                        />
                        <p className="form-help">Visible to the student to explain the reasoning</p>
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
                          disabled={gradeMutation.isPending || !gradeValue || aiGrading || gradingAll}
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
