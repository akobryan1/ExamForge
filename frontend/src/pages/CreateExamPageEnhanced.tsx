import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { Button } from '../components/Button';
import { ExamService } from '../services/ExamService';
import { StudentService } from '../services/StudentService';
import { apiClient } from '../services/apiClient';
import { useCreateExam, useUpdateExam } from '../hooks/useExamQueries';
import { useAuth } from '../contexts/AuthContext';
import type { RetakeConfiguration, LateSubmissionConfiguration, ProctorConfiguration, Question, QuestionType, DifficultyLevel } from '../types/exam';
import '../styles/pages/exam-form.css';
import '../styles/pages/questions.css';

type TabType = 'basic' | 'access' | 'timing' | 'questions' | 'proctoring' | 'advanced' | 'instructions';

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

const STEP_LABELS = ['Basics', 'Rules & Access', 'Questions'];
const STEP_ORDER = [1, 2, 3] as const;

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
  const { examId: urlExamId } = useParams<{ examId?: string }>();
  const [savedExamId, setSavedExamId] = useState<string | undefined>(undefined);
  const effectiveExamId = urlExamId || savedExamId;
  const isEditing = !!effectiveExamId;
  const [activeTab, setActiveTab] = useState<TabType>('basic');
  const [currentStep, setCurrentStep] = useState<1 | 2 | 3>(1);
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [availableSections, setAvailableSections] = useState<string[]>([]);
  const { user } = useAuth();
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
    retakeMaxRetakes: 0,
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

  // ── Inline question management ──
  const [questions, setQuestions] = useState<Question[]>([]);
  const [showAddModal, setShowAddModal] = useState(false);
  const [editingQuestion, setEditingQuestion] = useState<Question | null>(null);

  /* [AI GENERATION REMOVED — see git history for Generate with AI feature]
  const [modalMode, setModalMode] = useState<'manual' | 'ai'>('manual');
  const [modalAiFile, setModalAiFile] = useState<File | null>(null);
  const [modalAiMaterial, setModalAiMaterial] = useState('');
  const [modalAiTotalItems, setModalAiTotalItems] = useState(10);
  const [modalAiTypeCount, setModalAiTypeCount] = useState(1);
  const [modalAiTypeConfigs, setModalAiTypeConfigs] = useState<Array<{ type: string; count: number; points: number }>>([
    { type: 'multiple_choice', count: 5, points: 1 },
  ]);
  const [showAIPanel, setShowAIPanel] = useState(false);
  const [aiMaterial, setAiMaterial] = useState('');
  const [aiFile, setAiFile] = useState<File | null>(null);
  const [aiTopic, setAiTopic] = useState('');
  const [aiTotalItems, setAiTotalItems] = useState(10);
  const [aiTypeCount, setAiTypeCount] = useState(1);
  const [aiTypeConfigs, setAiTypeConfigs] = useState<Array<{ type: string; count: number; points: number }>>([
    { type: 'multiple_choice', count: 5, points: 1 },
  ]);
  const [aiGenerating, setAiGenerating] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);
  */

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
    modelAnswer: '',
    keyPoints: '',
  });

  // Load existing questions when editing
  useEffect(() => {
    if (effectiveExamId) {
      ExamService.getExamQuestions(effectiveExamId)
        .then(setQuestions)
        .catch(() => {});
    }
  }, [effectiveExamId]);

  // Fetch available sections for dropdown
  useEffect(() => {
    if (user?.id) {
      StudentService.getRegistrationFields(user.id)
        .then(fields => setAvailableSections(fields.sections || []))
        .catch(() => {});
    }
  }, [user]);

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
      modelAnswer: '',
      keyPoints: '',
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
      modelAnswer: question.modelAnswer || '',
      keyPoints: question.keyPoints || '',
    });
    setShowAddModal(true);
  };

  const deleteQuestion = async (questionId: string) => {
    if (!effectiveExamId) {
      setQuestions(prev => prev.filter(q => q.id !== questionId));
      return;
    }
    if (!confirm('Are you sure you want to delete this question?')) return;
    try {
      await ExamService.deleteQuestion(questionId, effectiveExamId);
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
        modelAnswer: questionForm.type === 'essay' ? questionForm.modelAnswer || undefined : undefined,
        keyPoints: questionForm.type === 'essay' ? questionForm.keyPoints || undefined : undefined,
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

      if (editingQuestion && effectiveExamId) {
        const updated = await ExamService.updateQuestion(editingQuestion.id, effectiveExamId, questionData);
        setQuestions(prev => prev.map(q => q.id === editingQuestion.id ? { ...q, ...updated } : q));
      } else if (editingQuestion) {
        setQuestions(prev => prev.map(q => q.id === editingQuestion.id ? { ...q, ...questionData, id: q.id } : q));
      } else if (effectiveExamId) {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const created = await ExamService.createQuestion(effectiveExamId, questionData as any);
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
    if (effectiveExamId) {
      setLoading(true);
      ExamService.getExamById(effectiveExamId)
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
  }, [effectiveExamId]);

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

  const handleSectionChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const selected = Array.from(e.target.selectedOptions, opt => opt.value);
    setFormData(prev => ({ ...prev, sections: selected }));
  };

  const validate = useCallback((step?: 1 | 2 | 3): boolean => {
    const stepNum = step || currentStep;
    const errors: Record<string, string> = {};
    if (stepNum === 1) {
      if (!formData.title.trim()) errors.title = 'Enter an exam title';
      if (!formData.description.trim()) errors.description = 'Enter a description';
    }
    if (stepNum === 2) {
      if (!formData.startDate) errors.startDate = 'Exam schedule is required';
      if (!formData.endDate) errors.endDate = 'Exam schedule is required';
      if (formData.startDate && formData.endDate && new Date(formData.endDate) <= new Date(formData.startDate)) {
        errors.endDate = 'End date must be after start date';
      }
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }, [formData, currentStep]);

  const buildExamData = useCallback(() => {
    const retakeConfig: RetakeConfiguration | undefined = formData.retakeMaxRetakes > 0 ? {
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

    return {
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
  }, [formData]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    // Only allow submission from Step 3 (explicit user action)
    if (currentStep !== 3) return;
    if (!validate(currentStep)) return;
    
    try {
      setLoading(true);
      setError(null);
      
      const examData = buildExamData();

      const exam = isEditing
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        ? await updateMutation.mutateAsync({ examId: effectiveExamId!, data: examData as any })
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        : await createMutation.mutateAsync(examData as any);
      
      const newExamId = exam.id || effectiveExamId!;
      
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
            return ExamService.createQuestion(newExamId, qData as any);
          })
        );
      }
      
      setSuccessToast(isEditing ? 'Exam updated successfully' : 'Exam created successfully');
      // Update examId so subsequent operations use the real ID
      if (!effectiveExamId && exam?.id) {
        window.history.replaceState(null, '', `/exams/${exam.id}/edit`);
      }
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

  const canProceedToStep2 = formData.title.trim().length > 0 && formData.description.trim().length > 0;

  const handleNextStep = () => {
    if (currentStep === 1 && !canProceedToStep2) {
      validate(1);
      return;
    }
    if (currentStep === 2) {
      // Validate schedule fields before proceeding
      if (formData.startDate && formData.endDate && new Date(formData.endDate) <= new Date(formData.startDate)) {
        setFieldErrors({ endDate: 'End date must be after start date' });
        return;
      }
    }
    setCurrentStep(prev => (prev + 1) as 1 | 2 | 3);
  };

  const handlePrevStep = () => {
    setCurrentStep(prev => (prev - 1) as 1 | 2 | 3);
  };

  const difficultyStampClass = (d: string | undefined) => {
    const map: Record<string, string> = { easy: 'stamp-easy', medium: 'stamp-medium', hard: 'stamp-hard' };
    return map[d || 'medium'] || 'stamp-medium';
  };

  const addGeneratedQuestions = (generated: any[]) => {
    const tempQuestions = generated.map((q: any, i: number) => ({
      id: `temp_${Date.now()}_${i}`,
      examId: effectiveExamId || '',
      type: q.type,
      text: q.text,
      description: q.description || '',
      points: q.points || 1,
      difficulty: q.difficulty || 'medium',
      choices: q.choices?.map((c: string) => ({ text: c, isCorrect: false })) || [],
      correctAnswer: q.correctAnswer,
      createdAt: new Date(),
      order: questions.length + i + 1,
    })) as Question[];
    setQuestions(prev => [...prev, ...tempQuestions]);
  };

  /* [AI GENERATION REMOVED — see git history for Generate with AI feature]

  const buildAICustomPrompt = (configs: Array<{ type: string; count: number; points: number }>) => {
    return configs
      .filter(c => c.count > 0)
      .map(c => `${c.count} ${c.type.replace('_', ' ')} question${c.count > 1 ? 's' : ''} worth ${c.points} point${c.points > 1 ? 's' : ''} each`)
      .join(', ');
  };

  const handleModalGenerateWithAI = async () => {
    try {
      setLoading(true);
      if (!modalAiMaterial.trim() && !modalAiFile) {
        setError('Upload a file or paste your material.');
        setLoading(false);
        return;
      }

      const totalConfigured = modalAiTypeConfigs.reduce((sum, c) => sum + c.count, 0);
      if (totalConfigured === 0) {
        setError('Set at least one question type with a count > 0.');
        setLoading(false);
        return;
      }

      const customPrompt = buildAICustomPrompt(modalAiTypeConfigs);

      if (modalAiFile) {
        const formData = new FormData();
        formData.append('file', modalAiFile);
        formData.append('customPrompt', customPrompt);
        formData.append('count', String(totalConfigured));

        const response = await apiClient.post('/api/ai/generate-from-file', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        });
        addGeneratedQuestions(response.data.questions || []);
        if (response.data.usingPlaceholder) {
          setError('Using sample questions — configure your API key in Settings for real AI-generated questions.');
        }
      } else {
        const response = await apiClient.post('/api/ai/generate-questions', {
          material: modalAiMaterial,
          customPrompt,
          questionType: 'multiple_choice',
          count: totalConfigured,
        });
        addGeneratedQuestions(response.data.questions || []);
        if (response.data.usingPlaceholder) {
          setError('Using sample questions — configure your API key in Settings for real AI-generated questions.');
        }
      }

      setModalMode('manual');
      setModalAiFile(null);
      setModalAiMaterial('');
      setShowAddModal(false);
    } catch (err: any) {
      setError(err.response?.data?.error || err.message || 'Failed to generate questions');
    } finally {
      setLoading(false);
    }
  };

  const handleGenerateWithAI = async () => {
    setAiError(null);
    setAiGenerating(true);
    try {
      let material = aiMaterial;

      const totalConfigured = aiTypeConfigs.reduce((sum, c) => sum + c.count, 0);
      if (totalConfigured === 0) {
        setAiError('Set at least one question type with a count > 0.');
        setAiGenerating(false);
        return;
      }

      const customPrompt = buildAICustomPrompt(aiTypeConfigs);

      // If a file was selected, upload it to the backend for text extraction
      if (aiFile) {
        const formData = new FormData();
        formData.append('file', aiFile);
        formData.append('customPrompt', customPrompt);
        formData.append('count', String(totalConfigured));

        const response = await apiClient.post('/api/ai/generate-from-file', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        });
        addGeneratedQuestions(response.data.questions || []);
        if (response.data.usingPlaceholder) {
          setAiError('Using sample questions — configure your API key in Settings for real AI-generated questions.');
        }
        setShowAIPanel(false);
        setAiFile(null);
        setAiMaterial('');
        return;
      }

      // Text-based generation
      if (!material.trim()) {
        setAiError('Paste your material text or upload a file.');
        setAiGenerating(false);
        return;
      }

      const response = await apiClient.post('/api/ai/generate-questions', {
        material,
        customPrompt,
        questionType: 'multiple_choice',
        count: totalConfigured,
      });
      addGeneratedQuestions(response.data.questions || []);
      if (response.data.usingPlaceholder) {
        setAiError('Using sample questions — configure your API key in Settings for real AI-generated questions.');
      }
      setShowAIPanel(false);
      setAiMaterial('');
    } catch (err: any) {
      setAiError(err.response?.data?.error || err.message || 'Failed to generate questions');
    } finally {
      setAiGenerating(false);
    }
  };
  */

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
          <p className="form-subtitle">Three steps — set the basics, choose your rules, add questions.</p>
        </div>

        {/* Stepper */}
        <div className="progress-steps" style={{ marginBottom: 24, padding: '16px 0' }}>
          {STEP_ORDER.map((stepNum, idx) => {
            const els = [];
            if (idx > 0) els.push(<div key={`line-${stepNum}`} className={`step-line${stepNum <= currentStep ? ' active' : ''}`} />);
            els.push(
              <div key={`step-${stepNum}`} className={`step${stepNum === currentStep ? ' active' : ''}${stepNum < currentStep ? ' complete' : ''}`}>
                <div className="step-number">{stepNum < currentStep ? '✓' : stepNum}</div>
                <div className="step-label">{STEP_LABELS[idx]}</div>
              </div>
            );
            return els;
          })}
        </div>

        {error && (
          <div className="error-banner">
            <span>{error}</span>
            <button className="error-banner-dismiss" onClick={() => setError(null)} aria-label="Dismiss">×</button>
          </div>
        )}

        <form onSubmit={handleSubmit} className="exam-form" ref={formRef}>
          {/* ========== STEP 1: BASICS ========== */}
          {currentStep === 1 && (
            <AnimatePresence mode="wait">
            <motion.div key="step1" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>
              <h2 className="step-panel-title">Exam basics</h2>
              <p className="step-panel-desc">Title, subject, and description for your exam.</p>

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

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== STEP 2: RULES & ACCESS ========== */}
          {currentStep === 2 && (
            <AnimatePresence mode="wait">
            <motion.div key="step2" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>
              <h2 className="step-panel-title">Rules & access</h2>
              <p className="step-panel-desc">Everyday settings only — proctoring and other advanced rules are tucked away below.</p>

              <div className="form-section">
                <h3 className="form-section-title" style={{ marginBottom: 12 }}>Passing score</h3>
                <div className="score-control">
                  <div className="score-slider">
                    <input
                      type="range"
                      min="0"
                      max="100"
                      value={formData.passingScore}
                      onChange={(e) => setFormData(prev => ({ ...prev, passingScore: parseInt(e.target.value) || 60 }))}
                    />
                  </div>
                  <div className="score-value">{formData.passingScore}%</div>
                </div>
              </div>

              <div className="form-section" style={{ marginTop: 24 }}>
                <h3 className="form-section-title" style={{ marginBottom: 12 }}>Who can take this exam</h3>
                <div className="option-group">
                  <label className={`option-label ${formData.accessMethod === 'student_login' ? 'selected' : ''}`}>
                    <input
                      type="radio"
                      name="accessMethod"
                      value="student_login"
                      checked={formData.accessMethod === 'student_login'}
                      onChange={handleChange}
                    />
                    <div>
                      <div className="option-title">Student login</div>
                      <div className="option-desc">
                        Only registered students can log in and take the exam using their student portal credentials
                      </div>
                    </div>
                  </label>
                  <label className={`option-label ${formData.accessMethod === 'guest' ? 'selected' : ''}`}>
                    <input
                      type="radio"
                      name="accessMethod"
                      value="guest"
                      checked={formData.accessMethod === 'guest'}
                      onChange={handleChange}
                    />
                    <div>
                      <div className="option-title">Guest access</div>
                      <div className="option-desc">
                        Anyone with the link can take the exam — they just provide their name, student ID, course, and year
                      </div>
                    </div>
                  </label>
                </div>
              </div>

              <div className="form-row" style={{ marginTop: 24 }}>
                <Field label="Duration (minutes)" help="Leave blank for untimed">
                  <input type="number" name="timeLimit" value={formData.timeLimit || ''} onChange={handleChange} min="1" placeholder="e.g., 60" />
                </Field>
                <Field label="Retakes allowed" help="0 = no retakes">
                  <input type="number" name="retakeMaxRetakes" value={formData.retakeMaxRetakes} onChange={(e) => setFormData(prev => ({ ...prev, retakeMaxRetakes: parseInt(e.target.value) || 0 }))} min="0" placeholder="0" />
                </Field>
              </div>

              <div className="form-section" style={{ marginTop: 8 }}>
                <h3 className="form-section-title" style={{ marginBottom: 12 }}>Display & review</h3>
                <Toggle name="shuffleQuestions" checked={formData.shuffleQuestions} onChange={handleChange} label="Shuffle question order" hint="Randomize question order for each student" />
                <Toggle name="shuffleAnswers" checked={formData.shuffleAnswers} onChange={handleChange} label="Shuffle answer choices" hint="Randomize answer order for multiple choice questions" />
                <Toggle name="showResults" checked={formData.showResults} onChange={handleChange} label="Show results to students after submission" hint="Allow students to see their answers after submission" />
                <Toggle name="allowReview" checked={formData.allowReview} onChange={handleChange} label="Allow students to review their answers" hint="Students can retake after first submission if retakes are enabled" />
              </div>

              <div className="form-section" style={{ marginTop: 8 }}>
                <h3 className="form-section-title" style={{ marginBottom: 12 }}>Restrict to sections</h3>
                <Field label="Allowed sections" help="Leave blank to allow all sections">
                  <select multiple value={formData.sections} onChange={handleSectionChange} style={{ minHeight: 90 }}>
                    {availableSections.length === 0 && <option disabled>No sections registered yet</option>}
                    {availableSections.map(s => (
                      <option key={s} value={s}>{s}</option>
                    ))}
                  </select>
                </Field>
                {formData.sections.length > 0 && (
                  <div className="section-tags">
                    {formData.sections.map(s => (
                      <span key={s} className="section-tag">{s}</span>
                    ))}
                  </div>
                )}
              </div>

              <div className="form-row" style={{ marginTop: 8 }}>
                <Field label="Start date & time" help="When students can start" error={fieldErrors.startDate}>
                  <input type="datetime-local" name="startDate" value={formData.startDate} onChange={handleChange} className={fieldErrors.startDate ? 'error' : ''} />
                </Field>
                <Field label="End date & time" help="When the exam becomes unavailable" error={fieldErrors.endDate}>
                  <input type="datetime-local" name="endDate" value={formData.endDate} onChange={handleChange} className={fieldErrors.endDate ? 'error' : ''} />
                </Field>
              </div>

              {/* Advanced Settings Disclosure */}
              <div className="advanced-toggle" onClick={() => setAdvancedOpen(!advancedOpen)}>
                <span className="advanced-toggle-label">Advanced settings <span className="advanced-toggle-hint">— proctoring, late submissions, custom instructions</span></span>
                <span className={`advanced-chevron${advancedOpen ? ' open' : ''}`}>▾</span>
              </div>
              <div className={`advanced-panel${advancedOpen ? ' open' : ''}`}>
                <div className="advanced-panel-inner">

                  <div>
                    <h3 className="advanced-section-title" style={{ marginBottom: 12 }}>Proctoring & anti-cheat</h3>
                    <Toggle name="proctorEnabled" checked={formData.proctorEnabled} onChange={handleChange} label="Enable proctoring" hint="Activate anti-cheat measures during the exam" />
                    <Reveal open={formData.proctorEnabled}>
                      <div className="form-subsection">
                        <h3>Behavioral monitoring</h3>
                        <div className="option-group">
                          <label className={`option-label ${formData.proctorDetectTabSwitch ? 'selected' : ''}`}>
                            <input type="checkbox" name="proctorDetectTabSwitch" checked={formData.proctorDetectTabSwitch} onChange={handleChange} />
                            <div>
                              <div className="option-title">Detect tab switching</div>
                              <div className="option-desc">Record when students switch browser tabs</div>
                            </div>
                          </label>
                          <Reveal open={formData.proctorDetectTabSwitch}>
                            <Field label="Points to deduct per tab switch">
                              <input type="number" name="proctorPointDeductionTabSwitch" value={formData.proctorPointDeductionTabSwitch} onChange={handleChange} min="0" placeholder="0" />
                            </Field>
                          </Reveal>
                          <label className={`option-label ${formData.proctorDetectCopyPaste ? 'selected' : ''}`}>
                            <input type="checkbox" name="proctorDetectCopyPaste" checked={formData.proctorDetectCopyPaste} onChange={handleChange} />
                            <div>
                              <div className="option-title">Detect copy/paste</div>
                              <div className="option-desc">Record copy and paste attempts</div>
                            </div>
                          </label>
                          <Reveal open={formData.proctorDetectCopyPaste}>
                            <Field label="Points to deduct per copy/paste">
                              <input type="number" name="proctorPointDeductionCopyPaste" value={formData.proctorPointDeductionCopyPaste} onChange={handleChange} min="0" placeholder="0" />
                            </Field>
                          </Reveal>
                          <label className={`option-label ${formData.proctorDisableRightClick ? 'selected' : ''}`}>
                            <input type="checkbox" name="proctorDisableRightClick" checked={formData.proctorDisableRightClick} onChange={handleChange} />
                            <div>
                              <div className="option-title">Disable right-click</div>
                              <div className="option-desc">Prevent right-click context menu</div>
                            </div>
                          </label>
                          <Reveal open={formData.proctorDisableRightClick}>
                            <Field label="Points to deduct per right-click">
                              <input type="number" name="proctorPointDeductionRightClick" value={formData.proctorPointDeductionRightClick} onChange={handleChange} min="0" placeholder="0" />
                            </Field>
                          </Reveal>
                          <label className={`option-label ${formData.proctorEnforceFullscreen ? 'selected' : ''}`}>
                            <input type="checkbox" name="proctorEnforceFullscreen" checked={formData.proctorEnforceFullscreen} onChange={handleChange} />
                            <div>
                              <div className="option-title">Enforce full-screen mode</div>
                              <div className="option-desc">Students must stay in fullscreen mode</div>
                            </div>
                          </label>
                          <Reveal open={formData.proctorEnforceFullscreen}>
                            <Field label="Points to deduct for exiting fullscreen">
                              <input type="number" name="proctorPointDeductionExitFullscreen" value={formData.proctorPointDeductionExitFullscreen} onChange={handleChange} min="0" placeholder="0" />
                            </Field>
                          </Reveal>
                        </div>
                        <Field label="General violation penalty (fallback)" help="Applied when a specific deduction is not set">
                          <input type="number" name="proctorPointDeductionGeneral" value={formData.proctorPointDeductionGeneral} onChange={handleChange} min="0" placeholder="0" />
                        </Field>
                        <Field label="Custom proctoring rules" help="Additional instructions for proctors">
                          <textarea name="proctorCustomRules" value={formData.proctorCustomRules} onChange={handleChange} placeholder="Add any custom rules or instructions for proctoring..." rows={3} />
                        </Field>
                      </div>
                    </Reveal>
                  </div>

                  <div>
                    <h3 className="advanced-section-title" style={{ marginBottom: 12 }}>Retake configuration</h3>
                    <Reveal open={formData.retakeMaxRetakes > 0}>
                      <Toggle name="retakeRequireApproval" checked={formData.retakeRequireApproval} onChange={handleChange} label="Require instructor approval" hint="Students must request permission to retake" />
                      <Field label="Scoring method" help="Which attempt score to use for the final grade">
                        <select name="retakeScoringMethod" value={formData.retakeScoringMethod} onChange={handleChange}>
                          <option value="best">Best score</option>
                          <option value="latest">Latest score</option>
                          <option value="average">Average score</option>
                        </select>
                      </Field>
                    </Reveal>
                  </div>

                  <div>
                    <h3 className="advanced-section-title" style={{ marginBottom: 12 }}>Late submissions</h3>
                    <Field label="Late submission policy" help="Define how late submissions are handled">
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
                  </div>

                  <div>
                    <h3 className="advanced-section-title" style={{ marginBottom: 12 }}>Custom instructions</h3>
                    <Field label="Exam instructions" help="These instructions will be displayed to students before they begin the exam">
                      <textarea name="customInstructions" value={formData.customInstructions} onChange={handleChange} placeholder="Enter custom instructions, rules, or guidelines for students taking this exam..." rows={8} />
                    </Field>
                    <Toggle name="showRulesBeforeExam" checked={formData.showRulesBeforeExam} onChange={handleChange} label="Show rules before exam" hint="Display all exam settings and anti-cheat rules before students start" />
                  </div>

                </div>
              </div>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== STEP 3: QUESTIONS ========== */}
          {currentStep === 3 && (
            <AnimatePresence mode="wait">
            <motion.div key="step3" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>
              <h2 className="step-panel-title">Questions</h2>
              <p className="step-panel-desc">{questions.length === 0 ? 'Choose how to add questions to your exam.' : `${questions.length} question${questions.length !== 1 ? 's' : ''} added.`}</p>

              {questions.length === 0 ? (
                <>
                  <div className="path-cards">
                    <div className="path-card" onClick={openAddQuestion}>
                      <div className="path-icon">📝</div>
                      <div className="path-title">Add manually</div>
                      <div className="path-desc">Write questions one by one with the question editor.</div>
                    </div>
                    <div className="path-card" onClick={() => {
                      /* Intentional no-op — user submits via the button below */
                    }}>
                      <div className="path-icon">⏭</div>
                      <div className="path-title">Finish without questions</div>
                      <div className="path-desc">Create the exam now and add questions later from the Questions page.</div>
                    </div>
                  </div>

                  {/* [AI GENERATION REMOVED — see git history for Generate with AI feature] */}
                </>
              ) : (
                <div className="questions-list" style={{ marginTop: 8 }}>
                  <Button onClick={openAddQuestion} style={{ marginBottom: 12 }}>Add Question</Button>
                  {questions.map((question, index) => (
                    <div key={question.id} className="question-card">
                      <div className="points-seal">{question.points || 1}<span>pt</span></div>
                      <div className="question-header">
                        <span className="question-number">Q{index + 1}</span>
                        <div className="question-meta">
                          <span className="stamp stamp-type">
                            {question.type.replace('_', ' ')}
                          </span>
                          <span className={`stamp ${difficultyStampClass(question.difficulty)}`}>
                            {question.difficulty || 'medium'}
                          </span>
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
                              <span className="choice-bubble">{String.fromCharCode(65 + i)}</span>
                              <span className="choice-text">{choice.text}</span>
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
                </div>
              )}

            </motion.div>
            </AnimatePresence>
          )}

          {/* Step Actions Footer */}
          <div className="step-actions">
            {currentStep === 1 ? (
              <>
                <span className="skip-note" style={{ fontFamily: 'var(--font-mono-ledger)', fontSize: '10.5px', color: 'var(--ledger-ink-soft)' }}>
                  <span className="required" style={{ color: 'var(--ledger-red)' }}>*</span> required fields
                </span>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => navigate('/exams')}
                  disabled={loading}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={handleNextStep}
                  disabled={!canProceedToStep2}
                  style={{ width: 'auto' }}
                >
                  Next →
                </button>
              </>
            ) : currentStep === 2 ? (
              <>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={handlePrevStep}
                  style={{ width: 'auto' }}
                >
                  ← Back
                </button>
                <div style={{ flex: 1 }} />
                <button
                  type="button"
                  className="btn btn-primary"
                  onClick={handleNextStep}
                  style={{ width: 'auto' }}
                >
                  Continue to Questions →
                </button>
              </>
            ) : (
              <>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={handlePrevStep}
                  style={{ width: 'auto' }}
                >
                  ← Back
                </button>
                <div style={{ flex: 1 }} />
                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={loading}
                  style={{ width: 'auto' }}
                >
                  {loading ? (
                    <><span className="spinner spinner-sm" /> Saving...</>
                  ) : questions.length > 0 ? 'Create Exam' : 'Create Exam — Add Questions Later'}
                </button>
              </>
            )}
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

            {/* [AI GENERATION MODE REMOVED — see git history for Generate with AI feature] */}

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
                      <input type="radio" name="correctChoice" className="bubble-radio" checked={choice.isCorrect} onChange={() => toggleCorrectChoice(index)} />
                      <input type="text" value={choice.text} onChange={(e) => updateChoice(index, e.target.value)}
                        placeholder={`Choice ${String.fromCharCode(65 + index)}`} required style={{ flex: 1 }} />
                      {questionForm.choices.length > 2 && (
                        <button type="button" onClick={() => removeChoice(index)} className="remove-btn" style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer', color: 'var(--ledger-red)' }}>×</button>
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

              {/* Essay */}
              {questionForm.type === 'essay' && (
                <div className="form-group">
                  <label>Model Answer (optional)</label>
                  <textarea
                    value={questionForm.modelAnswer}
                    onChange={(e) => setQuestionForm(prev => ({ ...prev, modelAnswer: e.target.value }))}
                    placeholder="Provide a model or reference answer that the AI will use for grading..."
                    rows={4}
                  />
                  <p className="form-help">The AI will compare student answers against this model answer during auto-evaluation.</p>
                </div>
              )}
              {questionForm.type === 'essay' && (
                <div className="form-group">
                  <label>Key Points (optional)</label>
                  <textarea
                    value={questionForm.keyPoints}
                    onChange={(e) => setQuestionForm(prev => ({ ...prev, keyPoints: e.target.value }))}
                    placeholder="List the key points or criteria that should be covered in the answer..."
                    rows={3}
                  />
                  <p className="form-help">The AI will check if the student answer covers these key points.</p>
                </div>
              )}

              {/* Modified True/False */}
              {questionForm.type === 'modified_true_false' && (
                <div className="form-group">
                  <label>Correct Answer</label>
                  <textarea value={questionForm.correctAnswer} onChange={(e) => setQuestionForm(prev => ({ ...prev, correctAnswer: e.target.value }))}
                    placeholder="Enter 'True' or 'False'. If false, provide the correct answer." rows={2} required />
                  <small style={{ fontSize: 11, color: 'var(--ledger-ink-soft)' }}>Format: "False. The correct answer is..." or "True"</small>
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
                        <button type="button" onClick={() => removeEnumerationItem(index)} style={{ background: 'none', border: 'none', fontSize: 20, cursor: 'pointer', color: 'var(--ledger-red)' }}>×</button>
                      )}
                    </div>
                  ))}
                  <Button type="button" variant="outline" size="sm" onClick={addEnumerationItem}>Add Item</Button>
                </div>
              )}

              <div className="modal-actions" style={{ display: 'flex', gap: 12, justifyContent: 'flex-end', marginTop: 24, paddingTop: 16, borderTop: '1px solid var(--ledger-line)' }}>
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
