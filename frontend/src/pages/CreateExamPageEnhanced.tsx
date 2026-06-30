import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { ExamService } from '../services/ExamService';
import { useCreateExam, useUpdateExam } from '../hooks/useExamQueries';
import type { RetakeConfiguration, LateSubmissionConfiguration, ProctorConfiguration, Question, QuestionType, DifficultyLevel } from '../types/exam';
import '../styles/pages/exam-form.css';
import '../styles/pages/questions.css';

type TabType = 'basic' | 'access' | 'timing' | 'questions' | 'proctoring' | 'advanced' | 'instructions';

const TAB_ICONS: Record<TabType, string> = {
  basic: '◇',
  access: '◉',
  timing: '⏱',
  questions: '▤',
  proctoring: '△',
  advanced: '⊡',
  instructions: '◻',
};

const TAB_LABELS: Record<TabType, string> = {
  basic: 'Basic Info',
  access: 'Access Control',
  timing: 'Timing & Deadline',
  questions: 'Questions & Display',
  proctoring: 'Proctoring & Anti-Cheat',
  advanced: 'Retakes & Late Submission',
  instructions: 'Custom Instructions',
};

const TAB_ORDER: TabType[] = ['basic', 'access', 'timing', 'questions', 'proctoring', 'advanced', 'instructions'];

/** Collapsible form section */
function FormSection({ title, defaultOpen = true, children }: { title: string; defaultOpen?: boolean; children: React.ReactNode }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="form-section">
      <div className="form-section-header" onClick={() => setOpen(!open)} role="button" tabIndex={0} onKeyDown={(e) => e.key === 'Enter' && setOpen(!open)} aria-expanded={open}>
        <h3 className="form-section-title">{title}</h3>
        <span className={`form-section-chevron${open ? ' open' : ''}`} aria-hidden="true">{open ? '▾' : '▸'}</span>
      </div>
      <div className={`form-section-content${open ? '' : ' collapsed'}`}>
        {children}
      </div>
    </div>
  );
}

/** Toggle switch */
function Toggle({ name, checked, onChange, label, hint }: { name: string; checked: boolean; onChange: (e: React.ChangeEvent<HTMLInputElement>) => void; label: string; hint?: string }) {
  return (
    <div className="toggle-wrapper">
      <label className="toggle-label">
        <div>{label}</div>
        {hint && <div className="toggle-hint">{hint}</div>}
      </label>
      <label className="toggle">
        <input type="checkbox" name={name} checked={checked} onChange={onChange} />
        <span className="toggle-slider" />
      </label>
    </div>
  );
}

/** Text input */
function Field({ label, required, error, help, children }: { label: string; required?: boolean; error?: string; help?: string; children: React.ReactNode }) {
  return (
    <div className="form-group">
      <label>
        {label}
        {required && <span className="required">*</span>}
      </label>
      {children}
      {help && <p className="form-help">{help}</p>}
      {error && <p className="form-error">{error}</p>}
    </div>
  );
}

/** Conditional reveal wrapper */
function Reveal({ open, children }: { open: boolean; children: React.ReactNode }) {
  return (
    <div className={`conditional-reveal${open ? ' open' : ''}`}>
      {children}
    </div>
  );
}

export function CreateExamPageEnhanced() {
  const navigate = useNavigate();
  const { examId } = useParams<{ examId?: string }>();
  const isEditing = !!examId;
  const [activeTab, setActiveTab] = useState<TabType>('basic');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    subject: '',
    grade: '',
    passingScore: 60,
    timeLimit: undefined as number | undefined,
    shuffleQuestions: false,
    shuffleAnswers: false,
    showResults: true,
    allowReview: true,
    accessMethod: 'student_login' as 'guest' | 'student_login',
    sections: [] as string[],
    startDate: '' as string,
    endDate: '' as string,
    retakeEnabled: false,
    retakeMaxRetakes: undefined as number | undefined,
    retakeRequireApproval: false,
    retakeScoringMethod: 'best' as 'best' | 'latest' | 'average',
    lateSubmissionPolicy: 'disabled' as 'allowed' | 'disabled' | 'request_permission',
    lateGracePeriodMinutes: 0,
    latePenaltyPoints: 0,
    latePenaltyInterval: 'minute' as 'minute' | 'hour' | 'day',
    proctorEnabled: false,
    proctorEnforceFullscreen: false,
    proctorDetectTabSwitch: false,
    proctorDetectCopyPaste: false,
    proctorDisableRightClick: false,
    proctorPointDeductionTabSwitch: 0,
    proctorPointDeductionCopyPaste: 0,
    proctorPointDeductionRightClick: 0,
    proctorPointDeductionExitFullscreen: 0,
    proctorPointDeductionGeneral: 0,
    proctorCustomRules: '',
    customInstructions: '',
    showRulesBeforeExam: true,
  });

  const [sectionInput, setSectionInput] = useState('');
  // ── Inline question management ──
  const [questions, setQuestions] = useState<Question[]>([]);
  const [showAddModal, setShowAddModal] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<Question | null>(null);
  const [questionForm, setQuestionForm] = useState({
    type: 'multiple_choice' as QuestionType | string,
    text: '',
    description: '',
    points: 1,
    difficulty: 'medium' as DifficultyLevel | string,
    choices: [{ text: '', isCorrect: false }],
    correctAnswer: '',
    enumerationItems: [''],
    imageUrl: '',
    timeLimit: 0,
  });

  // Load existing questions when editing
  useEffect(() => {
    if (examId) {
      ExamService.getExamQuestions(examId)
        .then(setQuestions)
        .catch(() => {});
    }
  }, [examId]);

  const openAddQuestion = () => {
    setEditingQuestion(null);
    setQuestionForm({
      type: 'multiple_choice',
      text: '',
      description: '',
      points: 1,
      difficulty: 'medium',
      choices: [{ text: '', isCorrect: false }, { text: '', isCorrect: false }],
      correctAnswer: '',
      enumerationItems: [''],
      imageUrl: '',
      timeLimit: 0,
    });
    setShowAddModal(true);
  };

  const openEditQuestion = (question: Question) => {
    setEditingQuestion(question);
    setQuestionForm({
      type: question.type,
      text: question.text,
      description: question.description || '',
      points: question.points,
      difficulty: question.difficulty || 'medium',
      choices: question.choices || [{ text: '', isCorrect: false }],
      correctAnswer: typeof question.correctAnswer === 'string' ? question.correctAnswer : '',
      enumerationItems: Array.isArray(question.correctAnswer) ? question.correctAnswer as string[] : [''],
      imageUrl: question.imageUrl || '',
      timeLimit: question.timeLimit || 0,
    });
    setShowAddModal(true);
  };

  const deleteQuestion = async (questionId: string) => {
    if (!examId) {
      // Not yet saved — remove from local state
      setQuestions(prev => prev.filter(q => q.id !== questionId));
      return;
    }
    if (!confirm('Are you sure you want to delete this question?')) return;
    try {
      await ExamService.deleteQuestion(questionId, examId);
      setQuestions(prev => prev.filter(q => q.id !== questionId));
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Failed to delete question';
      alert(msg);
    }
  };

  const addChoice = () => {
    setQuestionForm(prev => ({
      ...prev,
      choices: [...prev.choices, { text: '', isCorrect: false }],
    }));
  };

  const updateChoice = (index: number, text: string) => {
    setQuestionForm(prev => ({
      ...prev,
      choices: prev.choices.map((c, i) => i === index ? { ...c, text } : c),
    }));
  };

  const toggleCorrectChoice = (index: number) => {
    setQuestionForm(prev => ({
      ...prev,
      choices: prev.choices.map((c, i) => ({ ...c, isCorrect: i === index })),
    }));
  };

  const removeChoice = (index: number) => {
    setQuestionForm(prev => ({
      ...prev,
      choices: prev.choices.filter((_, i) => i !== index),
    }));
  };

  const addEnumerationItem = () => {
    setQuestionForm(prev => ({
      ...prev,
      enumerationItems: [...prev.enumerationItems, ''],
    }));
  };

  const updateEnumerationItem = (index: number, value: string) => {
    setQuestionForm(prev => ({
      ...prev,
      enumerationItems: prev.enumerationItems.map((item, i) => i === index ? value : item),
    }));
  };

  const removeEnumerationItem = (index: number) => {
    setQuestionForm(prev => ({
      ...prev,
      enumerationItems: prev.enumerationItems.filter((_, i) => i !== index),
    }));
  };

  const handleSaveQuestion = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const questionData: Record<string, unknown> = {
        type: questionForm.type,
        text: questionForm.text,
        description: questionForm.description || undefined,
        points: questionForm.points,
        difficulty: questionForm.difficulty,
        imageUrl: questionForm.imageUrl || undefined,
        timeLimit: questionForm.timeLimit || undefined,
      };

      if (questionForm.type === 'multiple_choice') {
        questionData.choices = questionForm.choices.filter(c => c.text.trim());
        questionData.correctAnswer = questionForm.choices.findIndex(c => c.isCorrect);
      } else if (questionForm.type === 'true_false') {
        questionData.correctAnswer = questionForm.correctAnswer === 'true';
      } else if (questionForm.type === 'modified_true_false') {
        questionData.correctAnswer = questionForm.correctAnswer;
      } else if (questionForm.type === 'identification') {
        questionData.correctAnswer = questionForm.correctAnswer;
      } else if (questionForm.type === 'enumeration') {
        questionData.correctAnswer = questionForm.enumerationItems.filter(item => item.trim());
      }

      if (editingQuestion && examId) {
        const updated = await ExamService.updateQuestion(editingQuestion.id, examId, questionData);
        setQuestions(prev => prev.map(q => q.id === editingQuestion.id ? { ...q, ...updated } : q));
      } else if (editingQuestion) {
        setQuestions(prev => prev.map(q => q.id === editingQuestion.id ? { ...q, ...questionData, id: q.id } : q));
      } else if (examId) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const created = await ExamService.createQuestion(examId, questionData as any);
        setQuestions(prev => [...prev, created]);
      } else {
        const tempId = `temp_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
        setQuestions(prev => [...prev, { ...questionData, id: tempId, examId: '', createdAt: new Date(), order: prev.length + 1 } as unknown as Question]);
      }
      setShowAddModal(false);
    } catch (err) {
      const resp = (err as { response?: { data?: { error?: string } }; message?: string });
      console.error('[CreateExam] Question save failed:', resp?.response?.data || resp?.message || err);
      alert(resp?.response?.data?.error || resp?.message || 'Failed to save question');
    }
  };

  // Load existing exam data for editing
  useEffect(() => {
    if (examId) {
      setLoading(true);
      ExamService.getExamById(examId)
        .then(exam => {
          setFormData({
            title: exam.title || '',
            description: exam.description || '',
            subject: exam.subject || '',
            grade: exam.grade || '',
            passingScore: exam.passingScore || 70,
            timeLimit: exam.timeLimit || 0,
            shuffleQuestions: exam.shuffleQuestions ?? false,
            shuffleAnswers: exam.shuffleAnswers ?? false,
            showResults: exam.showResults ?? true,
            allowReview: exam.allowReview ?? true,
            accessMethod: exam.accessMethod || 'student_login',
            sections: exam.sections || [],
            startDate: exam.startDate ? new Date(exam.startDate).toISOString().slice(0, 16) : '',
            endDate: exam.endDate ? new Date(exam.endDate).toISOString().slice(0, 16) : '',
            retakeEnabled: exam.retakeConfig?.enabled ?? false,
            retakeMaxRetakes: exam.retakeConfig?.maxRetakes || 0,
            retakeRequireApproval: exam.retakeConfig?.requireApproval ?? false,
            retakeScoringMethod: exam.retakeConfig?.scoringMethod || 'latest',
            lateSubmissionPolicy: exam.lateSubmissionConfig?.policy || 'disabled',
            lateGracePeriodMinutes: exam.lateSubmissionConfig?.gracePeriodMinutes || 0,
            latePenaltyPoints: exam.lateSubmissionConfig?.penaltyPoints || 0,
            latePenaltyInterval: exam.lateSubmissionConfig?.penaltyInterval || 'minute',
            proctorEnabled: exam.proctorConfig?.enabled ?? false,
            proctorEnforceFullscreen: exam.proctorConfig?.enforceFullscreen ?? false,
            proctorDetectTabSwitch: exam.proctorConfig?.detectTabSwitch ?? false,
            proctorDetectCopyPaste: exam.proctorConfig?.detectCopyPaste ?? false,
            proctorDisableRightClick: exam.proctorConfig?.disableRightClick ?? false,
            proctorPointDeductionTabSwitch: exam.proctorConfig?.pointDeductions?.tabSwitch || 0,
            proctorPointDeductionCopyPaste: exam.proctorConfig?.pointDeductions?.copyPaste || 0,
            proctorPointDeductionRightClick: exam.proctorConfig?.pointDeductions?.rightClick || 0,
            proctorPointDeductionExitFullscreen: exam.proctorConfig?.pointDeductions?.exitFullscreen || 0,
            proctorPointDeductionGeneral: exam.proctorConfig?.pointDeductions?.generalViolation || 0,
            proctorCustomRules: exam.proctorConfig?.customRules || '',
            customInstructions: exam.customInstructions || '',
            showRulesBeforeExam: exam.showRulesBeforeExam ?? true,
          });
        })
        .catch(err => setError(err.message || 'Failed to load exam'))
        .finally(() => setLoading(false));
    }
  }, [examId]);

  const createMutation = useCreateExam();
  const updateMutation = useUpdateExam();

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    // Clear field error on change
    setFieldErrors(prev => {
      const next = { ...prev };
      delete next[name];
      return next;
    });
    if (type === 'checkbox') {
      const checked = (e.target as HTMLInputElement).checked;
      setFormData(prev => ({ ...prev, [name]: checked }));
    } else if (type === 'number') {
      setFormData(prev => ({ ...prev, [name]: parseInt(value) || 0 }));
    } else {
      setFormData(prev => ({ ...prev, [name]: value }));
    }
  };

  const addSection = () => {
    if (sectionInput.trim() && !formData.sections.includes(sectionInput.trim())) {
      setFormData(prev => ({ ...prev, sections: [...prev.sections, sectionInput.trim()] }));
      setSectionInput('');
    }
  };

  const removeSection = (section: string) => {
    setFormData(prev => ({ ...prev, sections: prev.sections.filter(s => s !== section) }));
  };

  const validate = useCallback((): boolean => {
    const errors: Record<string, string> = {};
    if (!formData.title.trim()) errors.title = 'Enter an exam title';
    if (!formData.description.trim()) errors.description = 'Enter a description';
    if (!formData.startDate) errors.startDate = 'Exam schedule is required';
    if (!formData.endDate) errors.endDate = 'Exam schedule is required';
    if (formData.startDate && formData.endDate && new Date(formData.endDate) <= new Date(formData.startDate)) {
      errors.endDate = 'End date must be after start date';
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      // Switch to the tab containing the first error
      if (errors.title || errors.description) setActiveTab('basic');
      else if (errors.startDate || errors.endDate) setActiveTab('timing');
      return false;
    }
    return true;
  }, [formData]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    
    try {
      setLoading(true);
      setError(null);
      
      const retakeConfig: RetakeConfiguration | undefined = formData.retakeEnabled ? {
        enabled: true,
        maxRetakes: formData.retakeMaxRetakes,
        requireApproval: formData.retakeRequireApproval,
        scoringMethod: formData.retakeScoringMethod,
      } : undefined;
      
      const lateSubmissionConfig: LateSubmissionConfiguration | undefined = 
        formData.lateSubmissionPolicy !== 'disabled' ? {
          policy: formData.lateSubmissionPolicy,
          gracePeriodMinutes: formData.lateGracePeriodMinutes || undefined,
          penaltyPoints: formData.latePenaltyPoints || undefined,
          penaltyInterval: formData.latePenaltyInterval,
        } : undefined;
      
      const proctorConfig: ProctorConfiguration | undefined = formData.proctorEnabled ? {
        enabled: true,
        enforceFullscreen: formData.proctorEnforceFullscreen,
        detectTabSwitch: formData.proctorDetectTabSwitch,
        detectCopyPaste: formData.proctorDetectCopyPaste,
        disableRightClick: formData.proctorDisableRightClick,
        pointDeductions: {
          tabSwitch: formData.proctorPointDeductionTabSwitch || undefined,
          copyPaste: formData.proctorPointDeductionCopyPaste || undefined,
          rightClick: formData.proctorPointDeductionRightClick || undefined,
          exitFullscreen: formData.proctorPointDeductionExitFullscreen || undefined,
          generalViolation: formData.proctorPointDeductionGeneral || undefined,
        },
        customRules: formData.proctorCustomRules || undefined,
      } : undefined;
      
      const examData = {
        title: formData.title,
        description: formData.description,
        subject: formData.subject || undefined,
        grade: formData.grade || undefined,
        passingScore: formData.passingScore,
        timeLimit: formData.timeLimit || undefined,
        shuffleQuestions: formData.shuffleQuestions,
        shuffleAnswers: formData.shuffleAnswers,
        showResults: formData.showResults,
        allowReview: formData.allowReview,
        accessMethod: formData.accessMethod,
        sections: formData.sections.length > 0 ? formData.sections : undefined,
        startDate: formData.startDate,
        endDate: formData.endDate,
        retakeConfig,
        lateSubmissionConfig,
        proctorConfig,
        customInstructions: formData.customInstructions || undefined,
        showRulesBeforeExam: formData.showRulesBeforeExam,
      };
      
      const exam = isEditing
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ? await updateMutation.mutateAsync({ examId: examId!, data: examData as any })
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        : await createMutation.mutateAsync(examData as any);
      
      const savedExamId = exam.id || examId!;
      
      // Save any unsaved (temp) questions
      const unsavedQuestions = questions.filter(q => q.id.startsWith('temp_'));
      if (unsavedQuestions.length > 0) {
        await Promise.all(
          unsavedQuestions.map(q => {
            const qData: Record<string, unknown> = {
              type: q.type,
              text: q.text,
              description: q.description,
              points: q.points || 1,
              difficulty: q.difficulty || 'medium',
              imageUrl: q.imageUrl || undefined,
              timeLimit: q.timeLimit || undefined,
            };
            if (q.type === 'multiple_choice' && q.choices) {
              qData.choices = q.choices.filter(c => c.text.trim());
              qData.correctAnswer = q.choices.findIndex(c => c.isCorrect);
            } else if (q.type === 'true_false') {
              qData.correctAnswer = q.correctAnswer === 'true';
            } else if (q.type === 'identification' || q.type === 'modified_true_false') {
              qData.correctAnswer = q.correctAnswer;
            } else if (q.type === 'enumeration') {
              qData.correctAnswer = Array.isArray(q.correctAnswer) ? q.correctAnswer : [q.correctAnswer];
            }
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            return ExamService.createQuestion(savedExamId, qData as any);
          })
        );
      }
      
      setSuccessToast(isEditing ? 'Exam updated successfully' : 'Exam created successfully');
      setTimeout(() => navigate(`/exams/${savedExamId}`), 600);
    } catch (err) {
      const errWithResp = err as { response?: { data?: { error?: string } }; message?: string };
      const serverError = errWithResp.response?.data?.error;
      const errorMsg = serverError || errWithResp.message || (isEditing ? 'Failed to update exam' : 'Failed to create exam');
      console.error('[ExamForm] Error:', errorMsg, '| Server:', serverError, '| Full:', err);
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const tabIndex = TAB_ORDER.indexOf(activeTab) + 1;

  return (
    <MainLayout>
    <div className="exam-form-page">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
      >
        <div className="form-header">
          <h1>{isEditing ? 'Edit exam' : 'Create new exam'}</h1>
          <p className="form-subtitle">Set up exam details, questions, access control, and proctoring rules — all in one place</p>
          <p className="form-step-indicator">Step {tabIndex} of 7: {TAB_LABELS[activeTab]}</p>
        </div>

        {error && (
          <div className="error-banner">
            <span>{error}</span>
            <button className="error-banner-dismiss" onClick={() => setError(null)} aria-label="Dismiss">×</button>
          </div>
        )}

        {/* Tab Navigation */}
        <div className="tab-navigation">
          {TAB_ORDER.map(tab => (
            <button
              key={tab}
              type="button"
              className={`tab-button ${activeTab === tab ? 'active' : ''}`}
              onClick={() => setActiveTab(tab)}
            >
              <span className="tab-icon">{TAB_ICONS[tab]}</span>
              <span className="tab-label">{TAB_LABELS[tab]}</span>
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} className="exam-form" ref={formRef}>
          {/* ========== Basic Info Tab ========== */}
          {activeTab === 'basic' && (
            <AnimatePresence mode="wait">
            <motion.div key="basic" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <FormSection title="Exam details" defaultOpen={true}>
                <Field label="Exam title" required error={fieldErrors.title}>
                  <input type="text" name="title" value={formData.title} onChange={handleChange} placeholder="e.g., Midterm Exam 2024" className={fieldErrors.title ? 'error' : ''} />
                </Field>
                <Field label="Description" required error={fieldErrors.description}>
                  <textarea name="description" value={formData.description} onChange={handleChange} placeholder="Describe the exam content and objectives..." rows={4} className={fieldErrors.description ? 'error' : ''} />
                </Field>
                <div className="form-row">
                  <Field label="Subject" help="Course or subject area">
                    <input type="text" name="subject" value={formData.subject} onChange={handleChange} placeholder="e.g., Biology, World History, Calculus" />
                  </Field>
                </div>
              </FormSection>

              <FormSection title="Assessment" defaultOpen={true}>
                <Field label="Passing score (%)" help="Minimum percentage required to pass">
                  <input type="number" name="passingScore" value={formData.passingScore} onChange={handleChange} min="0" max="100" />
                </Field>
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Access Control Tab ========== */}
          {activeTab === 'access' && (
            <AnimatePresence mode="wait">
            <motion.div key="access" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <FormSection title="Who can take this exam" defaultOpen={true}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 8 }}>
                  <label className="option-label" style={{ cursor: 'pointer' }}>
                    <input
                      type="radio"
                      name="accessMethod"
                      value="student_login"
                      checked={formData.accessMethod === 'student_login'}
                      onChange={handleChange}
                    />
                    <div>
                      <div style={{ fontWeight: 600, fontSize: 14 }}>Student login</div>
                      <div style={{ fontSize: 12, color: 'var(--color-gray-3)', marginTop: 2 }}>
                        Only registered students can log in and take the exam using their student portal credentials
                      </div>
                    </div>
                  </label>
                  <label className="option-label" style={{ cursor: 'pointer' }}>
                    <input
                      type="radio"
                      name="accessMethod"
                      value="guest"
                      checked={formData.accessMethod === 'guest'}
                      onChange={handleChange}
                    />
                    <div>
                      <div style={{ fontWeight: 600, fontSize: 14 }}>Guest access</div>
                      <div style={{ fontSize: 12, color: 'var(--color-gray-3)', marginTop: 2 }}>
                        Anyone with the link can take the exam — they just provide their name, student ID, course, and year
                      </div>
                    </div>
                  </label>
                </div>
              </FormSection>

              <FormSection title="Restrict to sections or classes" defaultOpen={false}>
                <Field label="Allowed sections" help="Leave empty to allow all students">
                  <div className="section-input-group">
                    <input type="text" value={sectionInput} onChange={(e) => setSectionInput(e.target.value)} placeholder="e.g., Section A, Class 10-B" onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), addSection())} />
                    <button type="button" onClick={addSection} className="btn-add">Add</button>
                  </div>
                  {formData.sections.length > 0 && (
                    <div className="section-tags">
                      {formData.sections.map(s => (
                        <span key={s} className="section-tag">{s} <button type="button" onClick={() => removeSection(s)}>×</button></span>
                      ))}
                    </div>
                  )}
                </Field>
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Timing & Deadline Tab ========== */}
          {activeTab === 'timing' && (
            <AnimatePresence mode="wait">
            <motion.div key="timing" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <FormSection title="Exam schedule" defaultOpen={true}>
                <div className="form-row">
                  <Field label="Start date & time" help="When students can start taking the exam" error={fieldErrors.startDate}>
                    <input type="datetime-local" name="startDate" value={formData.startDate} onChange={handleChange} className={fieldErrors.startDate ? 'error' : ''} />
                  </Field>
                  <Field label="End date & time" help="When the exam becomes unavailable" error={fieldErrors.endDate}>
                    <input type="datetime-local" name="endDate" value={formData.endDate} onChange={handleChange} className={fieldErrors.endDate ? 'error' : ''} />
                  </Field>
                </div>
                <Field label="Duration limit (minutes)" help="Leave blank for untimed exam">
                  <input type="number" name="timeLimit" value={formData.timeLimit || ''} onChange={handleChange} min="1" placeholder="e.g., 60" />
                </Field>
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Questions & Display Tab ========== */}
          {activeTab === 'questions' && (
            <AnimatePresence mode="wait">
            <motion.div key="questions" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <FormSection title="Question display" defaultOpen={true}>
                <Toggle name="shuffleQuestions" checked={formData.shuffleQuestions} onChange={handleChange} label="Shuffle question order" hint="Randomize question order for each student" />
                <Toggle name="shuffleAnswers" checked={formData.shuffleAnswers} onChange={handleChange} label="Shuffle answer options" hint="Randomize answer order for multiple choice questions" />
              </FormSection>

              <FormSection title="Review settings" defaultOpen={true}>
                <Toggle name="showResults" checked={formData.showResults} onChange={handleChange} label="Show results to students" hint="Allow students to see their answers after submission" />
                <Toggle name="allowReview" checked={formData.allowReview} onChange={handleChange} label="Allow review" hint="Students can retake after first submission if retakes are enabled" />
              </FormSection>

              <FormSection title={`Questions (${questions.length})`} defaultOpen={true}>
                {questions.length === 0 ? (
                  <div className="empty-state" style={{ marginTop: 8 }}>
                    <h3>No questions yet</h3>
                    <p>Add your first question to get started.</p>
                    <Button onClick={openAddQuestion}>Add Question</Button>
                  </div>
                ) : (
                  <div className="questions-list" style={{ marginTop: 8 }}>
                    {questions.map((question, index) => (
                      <div key={question.id} className="question-card" style={{ padding: 'var(--spacing-4)', marginBottom: 0 }}>
                        <div className="question-header">
                          <span className="question-number">Q{index + 1}</span>
                          <div className="question-meta">
                            <span className={`question-type type-${question.type}`}>
                              {question.type.replace('_', ' ')}
                            </span>
                            <span className={`difficulty-badge difficulty-${question.difficulty || 'medium'}`}>
                              {question.difficulty || 'medium'}
                            </span>
                            <span className="points-badge">{question.points || 1} pts</span>
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
                          <Button variant="outline" size="sm" onClick={() => openEditQuestion(question)}>Edit</Button>
                          <Button variant="outline" size="sm" onClick={() => deleteQuestion(question.id)}>Delete</Button>
                        </div>
                      </div>
                    ))}
                    <Button onClick={openAddQuestion} style={{ alignSelf: 'flex-start', marginTop: 8 }}>Add Question</Button>
                  </div>
                )}
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Proctoring & Anti-Cheat Tab ========== */}
          {activeTab === 'proctoring' && (
            <AnimatePresence mode="wait">
            <motion.div key="proctoring" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <div className="form-section" style={{ marginBottom: 16 }}>
                <Toggle name="proctorEnabled" checked={formData.proctorEnabled} onChange={handleChange} label="Enable proctoring" hint="Activate anti-cheat measures during the exam" />
              </div>

              <Reveal open={formData.proctorEnabled}>
                <div className="form-subsection">
                  <h3>Behavioral monitoring</h3>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                    <label className="option-label" style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)' }}>
                      <input type="checkbox" name="proctorDetectTabSwitch" checked={formData.proctorDetectTabSwitch} onChange={handleChange} style={{ accentColor: 'var(--color-accent-500)' }} />
                      <div>
                        <div style={{ fontWeight: 500, fontSize: 14 }}>Detect tab switching</div>
                        <div style={{ fontSize: 12, color: 'var(--color-gray-3)', marginTop: 2 }}>Record when students switch browser tabs</div>
                      </div>
                    </label>
                    <Reveal open={formData.proctorDetectTabSwitch}>
                      <Field label="Points to deduct per tab switch">
                        <input type="number" name="proctorPointDeductionTabSwitch" value={formData.proctorPointDeductionTabSwitch} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                    <label className="option-label" style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)' }}>
                      <input type="checkbox" name="proctorDetectCopyPaste" checked={formData.proctorDetectCopyPaste} onChange={handleChange} style={{ accentColor: 'var(--color-accent-500)' }} />
                      <div>
                        <div style={{ fontWeight: 500, fontSize: 14 }}>Detect copy/paste</div>
                        <div style={{ fontSize: 12, color: 'var(--color-gray-3)', marginTop: 2 }}>Record copy and paste attempts</div>
                      </div>
                    </label>
                    <Reveal open={formData.proctorDetectCopyPaste}>
                      <Field label="Points to deduct per copy/paste">
                        <input type="number" name="proctorPointDeductionCopyPaste" value={formData.proctorPointDeductionCopyPaste} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                    <label className="option-label" style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)' }}>
                      <input type="checkbox" name="proctorDisableRightClick" checked={formData.proctorDisableRightClick} onChange={handleChange} style={{ accentColor: 'var(--color-accent-500)' }} />
                      <div>
                        <div style={{ fontWeight: 500, fontSize: 14 }}>Disable right-click</div>
                        <div style={{ fontSize: 12, color: 'var(--color-gray-3)', marginTop: 2 }}>Prevent right-click context menu</div>
                      </div>
                    </label>
                    <Reveal open={formData.proctorDisableRightClick}>
                      <Field label="Points to deduct per right-click">
                        <input type="number" name="proctorPointDeductionRightClick" value={formData.proctorPointDeductionRightClick} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                    <label className="option-label" style={{ cursor: 'pointer', display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px', border: '1px solid var(--color-border)', borderRadius: 'var(--radius-md)' }}>
                      <input type="checkbox" name="proctorEnforceFullscreen" checked={formData.proctorEnforceFullscreen} onChange={handleChange} style={{ accentColor: 'var(--color-accent-500)' }} />
                      <div>
                        <div style={{ fontWeight: 500, fontSize: 14 }}>Enforce full-screen mode</div>
                        <div style={{ fontSize: 12, color: 'var(--color-gray-3)', marginTop: 2 }}>Students must stay in fullscreen mode</div>
                      </div>
                    </label>
                    <Reveal open={formData.proctorEnforceFullscreen}>
                      <Field label="Points to deduct for exiting fullscreen">
                        <input type="number" name="proctorPointDeductionExitFullscreen" value={formData.proctorPointDeductionExitFullscreen} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                  </div>
                </div>

                <Field label="General violation penalty (fallback)" help="Applied when a specific deduction is not set">
                  <input type="number" name="proctorPointDeductionGeneral" value={formData.proctorPointDeductionGeneral} onChange={handleChange} min="0" placeholder="0" />
                </Field>

                <Field label="Custom proctoring rules" help="Additional instructions for proctors">
                  <textarea name="proctorCustomRules" value={formData.proctorCustomRules} onChange={handleChange} placeholder="Add any custom rules or instructions for proctoring..." rows={3} />
                </Field>
              </Reveal>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Retakes & Late Submission Tab ========== */}
          {activeTab === 'advanced' && (
            <AnimatePresence mode="wait">
            <motion.div key="advanced" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <FormSection title="Exam retakes" defaultOpen={true}>
                <Toggle name="retakeEnabled" checked={formData.retakeEnabled} onChange={handleChange} label="Allow students to retake this exam" />
                <Reveal open={formData.retakeEnabled}>
                  <Field label="Maximum retakes" help="0 = no retakes, leave blank for unlimited">
                    <input type="number" name="retakeMaxRetakes" value={formData.retakeMaxRetakes || ''} onChange={handleChange} min="0" placeholder="Leave blank for unlimited" />
                  </Field>
                  <Toggle name="retakeRequireApproval" checked={formData.retakeRequireApproval} onChange={handleChange} label="Require instructor approval" hint="Students must request permission to retake" />
                  <Field label="Scoring method" help="Which attempt score to use for the final grade">
                    <select name="retakeScoringMethod" value={formData.retakeScoringMethod} onChange={handleChange}>
                      <option value="best">Best score</option>
                      <option value="latest">Latest score</option>
                      <option value="average">Average score</option>
                    </select>
                  </Field>
                </Reveal>
              </FormSection>

              <FormSection title="Late submission policy" defaultOpen={false}>
                <Field label="Late submission" help="Define how late submissions are handled">
                  <select name="lateSubmissionPolicy" value={formData.lateSubmissionPolicy} onChange={handleChange}>
                    <option value="disabled">Disabled (hard deadline)</option>
                    <option value="allowed">Allowed (with penalty)</option>
                    <option value="request_permission">Require permission</option>
                  </select>
                </Field>
                <Reveal open={formData.lateSubmissionPolicy !== 'disabled'}>
                  <Field label="Grace period (minutes)" help="Time after deadline with no penalty">
                    <input type="number" name="lateGracePeriodMinutes" value={formData.lateGracePeriodMinutes} onChange={handleChange} min="0" placeholder="0" />
                  </Field>
                </Reveal>
                <Reveal open={formData.lateSubmissionPolicy === 'allowed'}>
                  <div className="form-row">
                    <Field label="Penalty (points)" help="Points deducted for each interval late">
                      <input type="number" name="latePenaltyPoints" value={formData.latePenaltyPoints} onChange={handleChange} min="0" placeholder="0" />
                    </Field>
                    <Field label="Penalty interval" help="Time interval for penalty accrual">
                      <select name="latePenaltyInterval" value={formData.latePenaltyInterval} onChange={handleChange}>
                        <option value="minute">Per minute</option>
                        <option value="hour">Per hour</option>
                        <option value="day">Per day</option>
                      </select>
                    </Field>
                  </div>
                </Reveal>
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Custom Instructions Tab ========== */}
          {activeTab === 'instructions' && (
            <AnimatePresence mode="wait">
            <motion.div key="instructions" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <FormSection title="Instructions for students" defaultOpen={true}>
                <Field label="Exam instructions" help="These instructions will be displayed to students before they begin the exam">
                  <textarea name="customInstructions" value={formData.customInstructions} onChange={handleChange} placeholder="Enter custom instructions, rules, or guidelines for students taking this exam..." rows={8} />
                </Field>
              </FormSection>

              <FormSection title="Pre-exam display" defaultOpen={true}>
                <Toggle name="showRulesBeforeExam" checked={formData.showRulesBeforeExam} onChange={handleChange} label="Show rules before exam" hint="Display all exam settings and anti-cheat rules before students start" />
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          <div className="form-actions">
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => navigate('/exams')}
              disabled={loading}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn-primary"
              disabled={loading}
            >
              {loading ? (
                <><span className="spinner spinner-sm" /> Saving...</>
              ) : isEditing ? 'Update Exam' : 'Create Exam & Add Questions'}
            </button>
          </div>
        </form>
      </motion.div>
    </div>
    {successToast && <div className="toast-success">{successToast}</div>}

    {/* Question Add/Edit Modal */}
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

            <form onSubmit={handleSaveQuestion} className="question-form" style={{ padding: 'var(--spacing-6)' }}>
              <div className="form-row">
                <div className="form-group">
                  <label>Question Type *</label>
                  <select
                    value={questionForm.type}
                    onChange={(e) => setQuestionForm(prev => ({ ...prev, type: e.target.value }))}
                    required
                  >
                    <option value="multiple_choice">Multiple Choice</option>
                    <option value="true_false">True/False</option>
                    <option value="modified_true_false">Modified True/False</option>
                    <option value="essay">Essay</option>
                    <option value="identification">Identification</option>
                    <option value="enumeration">Enumeration</option>
                  </select>
                </div>
                <div className="form-group">
                  <label>Points *</label>
                  <input
                    type="number" min="1"
                    value={questionForm.points}
                    onChange={(e) => setQuestionForm(prev => ({ ...prev, points: parseInt(e.target.value) || 1 }))}
                    required
                  />
                </div>
                <div className="form-group">
                  <label>Difficulty</label>
                  <select
                    value={questionForm.difficulty}
                    onChange={(e) => setQuestionForm(prev => ({ ...prev, difficulty: e.target.value }))}
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
                  value={questionForm.text}
                  onChange={(e) => setQuestionForm(prev => ({ ...prev, text: e.target.value }))}
                  placeholder="Enter your question..." required rows={3}
                />
              </div>

              <div className="form-group">
                <label>Description (optional)</label>
                <textarea
                  value={questionForm.description}
                  onChange={(e) => setQuestionForm(prev => ({ ...prev, description: e.target.value }))}
                  placeholder="Additional context or instructions..." rows={2}
                />
              </div>

              {/* Multiple Choice */}
              {questionForm.type === 'multiple_choice' && (
                <div className="form-group">
                  <label>Answer Choices</label>
                  {questionForm.choices.map((choice, index) => (
                    <div key={index} className="choice-input-group" style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
                      <input type="radio" name="correctChoice" checked={choice.isCorrect} onChange={() => toggleCorrectChoice(index)} />
                      <input type="text" value={choice.text} onChange={(e) => updateChoice(index, e.target.value)}
                        placeholder={`Choice ${String.fromCharCode(65 + index)}`} required style={{ flex: 1 }} />
                      {questionForm.choices.length > 2 && (
                        <button type="button" onClick={() => removeChoice(index)} className="remove-btn" style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer', color: 'var(--color-error-600)' }}>×</button>
                      )}
                    </div>
                  ))}
                  <Button type="button" variant="outline" size="sm" onClick={addChoice}>Add Choice</Button>
                </div>
              )}

              {/* True/False */}
              {questionForm.type === 'true_false' && (
                <div className="form-group">
                  <label>Correct Answer</label>
                  <select value={questionForm.correctAnswer} onChange={(e) => setQuestionForm(prev => ({ ...prev, correctAnswer: e.target.value }))} required>
                    <option value="">Select answer...</option>
                    <option value="true">True</option>
                    <option value="false">False</option>
                  </select>
                </div>
              )}

              {/* Modified True/False */}
              {questionForm.type === 'modified_true_false' && (
                <div className="form-group">
                  <label>Correct Answer</label>
                  <textarea value={questionForm.correctAnswer} onChange={(e) => setQuestionForm(prev => ({ ...prev, correctAnswer: e.target.value }))}
                    placeholder="Enter 'True' or 'False'. If false, provide the correct answer." rows={2} required />
                  <small style={{ fontSize: 11, color: 'var(--color-gray-3)' }}>Format: "False. The correct answer is..." or "True"</small>
                </div>
              )}

              {/* Identification */}
              {questionForm.type === 'identification' && (
                <div className="form-group">
                  <label>Correct Answer</label>
                  <input type="text" value={questionForm.correctAnswer} onChange={(e) => setQuestionForm(prev => ({ ...prev, correctAnswer: e.target.value }))}
                    placeholder="Expected answer..." required />
                </div>
              )}

              {/* Enumeration */}
              {questionForm.type === 'enumeration' && (
                <div className="form-group">
                  <label>Expected Items (in order)</label>
                  {questionForm.enumerationItems.map((item, index) => (
                    <div key={index} style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 6 }}>
                      <span style={{ fontWeight: 600, fontSize: 13 }}>{index + 1}.</span>
                      <input type="text" value={item} onChange={(e) => updateEnumerationItem(index, e.target.value)}
                        placeholder={`Item ${index + 1}`} required style={{ flex: 1 }} />
                      {questionForm.enumerationItems.length > 1 && (
                        <button type="button" onClick={() => removeEnumerationItem(index)} style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer', color: 'var(--color-error-600)' }}>×</button>
                      )}
                    </div>
                  ))}
                  <Button type="button" variant="outline" size="sm" onClick={addEnumerationItem}>Add Item</Button>
                </div>
              )}

              <div className="modal-actions" style={{ display: 'flex', gap: 12, justifyContent: 'flex-end', marginTop: 24, paddingTop: 16, borderTop: '1px solid var(--color-border)' }}>
                <Button type="button" variant="outline" onClick={() => setShowAddModal(false)}>Cancel</Button>
                <Button type="submit">{editingQuestion ? 'Update Question' : 'Add Question'}</Button>
              </div>
            </form>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
    </MainLayout>
  );
}
