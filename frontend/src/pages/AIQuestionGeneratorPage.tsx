import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { apiClient } from '../services/apiClient';
import { ExamService } from '../services/ExamService';
import { QuestionType, DifficultyLevel } from '../types/exam';
import '../styles/pages/ai-generator.css';

interface GeneratedQuestion {
  type: QuestionType;
  text: string;
  description?: string;
  points: number;
  choices?: string[];
  correctAnswer: string | string[];
  explanation?: string;
}

export function AIQuestionGeneratorPage() {
  const { examId } = useParams<{ examId: string }>();
  const navigate = useNavigate();

  // Step state
  const [currentStep, setCurrentStep] = useState<1 | 2 | 3>(1);

  // Step 1: Material input
  const [material, setMaterial] = useState('');
  const [topic, setTopic] = useState('');

  // Step 2: Generation config
  const [questionType, setQuestionType] = useState<QuestionType>(QuestionType.MULTIPLE_CHOICE);
  const [count, setCount] = useState(5);
  const [difficulty, setDifficulty] = useState<DifficultyLevel>(DifficultyLevel.MEDIUM);

  // Step 3: Review generated questions
  const [generatedQuestions, setGeneratedQuestions] = useState<GeneratedQuestion[]>([]);
  const [selectedQuestions, setSelectedQuestions] = useState<Set<number>>(new Set());
  const [generating, setGenerating] = useState(false);
  const [importing, setImporting] = useState(false);
  const [usingPlaceholder, setUsingPlaceholder] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleGenerateQuestions = async () => {
    setError(null);
    setGenerating(true);

    try {
      const response = await apiClient.post('/api/ai/generate-questions', {
        material,
        questionType,
        count,
        difficulty,
        topic: topic || undefined,
      });

      setGeneratedQuestions(response.data.questions);
      setUsingPlaceholder(response.data.usingPlaceholder);
      
      // Select all by default
      setSelectedQuestions(new Set(response.data.questions.map((_: any, i: number) => i)));
      
      setCurrentStep(3);
    } catch (err: any) {
      setError(err.message || 'Failed to generate questions');
    } finally {
      setGenerating(false);
    }
  };

  const handleImportQuestions = async () => {
    if (!examId) {
      setError('No exam ID provided');
      return;
    }

    setImporting(true);
    setError(null);

    try {
      const questionsToImport = generatedQuestions.filter((_, i) => selectedQuestions.has(i));

      for (const question of questionsToImport) {
        await ExamService.createQuestion(examId, {
          type: question.type,
          text: question.text,
          description: question.description,
          points: question.points,          difficulty: difficulty,          choices: question.choices?.map((c, idx) => ({ text: c, isCorrect: false, order: idx })),
          correctAnswer: question.correctAnswer,
        });
      }

      navigate(`/exams/${examId}/questions`);
    } catch (err: any) {
      setError(err.message || 'Failed to import questions');
    } finally {
      setImporting(false);
    }
  };

  const toggleQuestionSelection = (index: number) => {
    const newSelection = new Set(selectedQuestions);
    if (newSelection.has(index)) {
      newSelection.delete(index);
    } else {
      newSelection.add(index);
    }
    setSelectedQuestions(newSelection);
  };

  const selectAll = () => {
    setSelectedQuestions(new Set(generatedQuestions.map((_, i) => i)));
  };

  const deselectAll = () => {
    setSelectedQuestions(new Set());
  };

  return (
    <MainLayout>
      <div className="ai-generator-page">
        <div className="page-header">
          <h1>🤖 AI Question Generator</h1>
          <p className="page-description">Generate exam questions using AI from your study materials</p>
        </div>

        {/* Progress Steps */}
        <div className="progress-steps">
          <div className={`step ${currentStep >= 1 ? 'active' : ''}`}>
            <div className="step-number">1</div>
            <div className="step-label">Material</div>
          </div>
          <div className={`step-line ${currentStep >= 2 ? 'active' : ''}`} />
          <div className={`step ${currentStep >= 2 ? 'active' : ''}`}>
            <div className="step-number">2</div>
            <div className="step-label">Configure</div>
          </div>
          <div className={`step-line ${currentStep >= 3 ? 'active' : ''}`} />
          <div className={`step ${currentStep >= 3 ? 'active' : ''}`}>
            <div className="step-number">3</div>
            <div className="step-label">Review</div>
          </div>
        </div>

        <AnimatePresence mode="wait">
          {/* Step 1: Material Input */}
          {currentStep === 1 && (
            <motion.div
              key="step1"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="step-content"
            >
              <div className="form-card">
                <h2>Step 1: Input Study Material</h2>
                <p className="step-description">
                  Paste the text content you want to generate questions from
                </p>

                <div className="form-group">
                  <label htmlFor="topic">Topic (Optional)</label>
                  <input
                    type="text"
                    id="topic"
                    value={topic}
                    onChange={(e) => setTopic(e.target.value)}
                    placeholder="e.g., Cell Biology, World War II, Calculus"
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="material">Study Material *</label>
                  <textarea
                    id="material"
                    value={material}
                    onChange={(e) => setMaterial(e.target.value)}
                    placeholder="Paste your study material text here..."
                    rows={12}
                    required
                  />
                  <small className="form-hint">
                    Paste lecture notes, textbook excerpts, or any educational content
                  </small>
                </div>

                <div className="step-actions">
                  <Button variant="outline" onClick={() => navigate(-1)}>
                    Cancel
                  </Button>
                  <Button
                    variant="primary"
                    onClick={() => setCurrentStep(2)}
                    disabled={!material.trim()}
                  >
                    Next →
                  </Button>
                </div>
              </div>
            </motion.div>
          )}

          {/* Step 2: Configuration */}
          {currentStep === 2 && (
            <motion.div
              key="step2"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="step-content"
            >
              <div className="form-card">
                <h2>Step 2: Configure Generation</h2>
                <p className="step-description">
                  Choose the type and number of questions to generate
                </p>

                <div className="form-group">
                  <label htmlFor="questionType">Question Type</label>
                  <select
                    id="questionType"
                    value={questionType}
                    onChange={(e) => setQuestionType(e.target.value as QuestionType)}
                  >
                    <option value="multiple_choice">Multiple Choice</option>
                    <option value="true_false">True/False</option>
                    <option value="modified_true_false">Modified True/False</option>
                    <option value="essay">Essay</option>
                    <option value="identification">Identification</option>
                    <option value="enumeration">Enumeration</option>
                  </select>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label htmlFor="count">Number of Questions</label>
                    <input
                      type="number"
                      id="count"
                      value={count}
                      onChange={(e) => setCount(Math.min(20, Math.max(1, parseInt(e.target.value) || 1)))}
                      min="1"
                      max="20"
                    />
                  </div>

                  <div className="form-group">
                    <label htmlFor="difficulty">Difficulty</label>
                    <select
                      id="difficulty"
                      value={difficulty}
                      onChange={(e) => setDifficulty(e.target.value as any)}
                    >
                      <option value="easy">Easy</option>
                      <option value="medium">Medium</option>
                      <option value="hard">Hard</option>
                    </select>
                  </div>
                </div>

                <div className="step-actions">
                  <Button variant="outline" onClick={() => setCurrentStep(1)}>
                    ← Back
                  </Button>
                  <Button
                    variant="primary"
                    onClick={handleGenerateQuestions}
                    disabled={generating}
                  >
                    {generating ? 'Generating...' : 'Generate Questions →'}
                  </Button>
                </div>
              </div>
            </motion.div>
          )}

          {/* Step 3: Review & Import */}
          {currentStep === 3 && (
            <motion.div
              key="step3"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="step-content"
            >
              <div className="form-card">
                <h2>Step 3: Review & Import Questions</h2>
                <p className="step-description">
                  Review generated questions and select which ones to import
                </p>

                {usingPlaceholder && (
                  <div className="info-banner">
                    ℹ️ Using placeholder AI (demo mode). Configure OPENAI_API_KEY for real AI generation.
                  </div>
                )}

                <div className="selection-actions">
                  <Button variant="outline" size="sm" onClick={selectAll}>
                    Select All
                  </Button>
                  <Button variant="outline" size="sm" onClick={deselectAll}>
                    Deselect All
                  </Button>
                  <span className="selection-count">
                    {selectedQuestions.size} of {generatedQuestions.length} selected
                  </span>
                </div>

                <div className="questions-list">
                  {generatedQuestions.map((question, index) => (
                    <div
                      key={index}
                      className={`question-preview ${selectedQuestions.has(index) ? 'selected' : ''}`}
                      onClick={() => toggleQuestionSelection(index)}
                    >
                      <div className="question-checkbox">
                        <input
                          type="checkbox"
                          checked={selectedQuestions.has(index)}
                          onChange={() => toggleQuestionSelection(index)}
                        />
                      </div>
                      <div className="question-content">
                        <div className="question-header-preview">
                          <span className="question-number">Q{index + 1}</span>
                          <span className="question-type-badge">{question.type.replace('_', ' ')}</span>
                          <span className="question-points">{question.points} pts</span>
                        </div>
                        <p className="question-text">{question.text}</p>
                        {question.description && (
                          <p className="question-description">{question.description}</p>
                        )}
                        {question.choices && (
                          <div className="question-choices">
                            {question.choices.map((choice, i) => (
                              <div key={i} className="choice-item">
                                {choice}
                              </div>
                            ))}
                          </div>
                        )}
                        {question.explanation && (
                          <div className="question-explanation">
                            <strong>Explanation:</strong> {question.explanation}
                          </div>
                        )}
                      </div>
                    </div>
                  ))}
                </div>

                {error && <div className="error-banner">{error}</div>}

                <div className="step-actions">
                  <Button variant="outline" onClick={() => setCurrentStep(2)}>
                    ← Regenerate
                  </Button>
                  <Button
                    variant="primary"
                    onClick={handleImportQuestions}
                    disabled={importing || selectedQuestions.size === 0}
                  >
                    {importing ? 'Importing...' : `Import ${selectedQuestions.size} Questions →`}
                  </Button>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </MainLayout>
  );
}
