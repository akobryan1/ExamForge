import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import type { RetakeConfiguration, LateSubmissionConfiguration, ProctorConfiguration } from '../types/exam';
import '../styles/pages/exam-form.css';

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
  const [collapsedSections, setCollapsedSections] = useState<Record<string, boolean>>({});

  const toggleSection = (key: string) => {
    setCollapsedSections(prev => ({ ...prev, [key]: !prev[key] }));
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
    if (formData.startDate && formData.endDate && new Date(formData.endDate) <= new Date(formData.startDate)) {
      errors.endDate = 'End date must be after start date';
    }
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) {
      // Switch to the tab containing the first error
      if (errors.title || errors.description) setActiveTab('basic');
      else if (errors.endDate) setActiveTab('timing');
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
      
      const examData: any = {
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
        ? await ExamService.updateExam(examId!, examData)
        : await ExamService.createExam(examData);
      
      setSuccessToast(isEditing ? 'Exam updated successfully' : 'Exam created successfully');
      setTimeout(() => navigate(`/exams/${exam.id}/questions`), 600);
    } catch (err: any) {
      const serverError = err.response?.data?.error;
      const errorMsg = serverError || err.message || (isEditing ? 'Failed to update exam' : 'Failed to create exam');
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
          <p className="form-subtitle">Set up exam details, access control, and proctoring rules</p>
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
                  <Field label="Start date & time" help="When students can start taking the exam">
                    <input type="datetime-local" name="startDate" value={formData.startDate} onChange={handleChange} />
                  </Field>
                  <Field label="End date & time" help="When the exam becomes unavailable" error={fieldErrors.endDate}>
                    <input type="datetime-local" name="endDate" value={formData.endDate} onChange={handleChange} className={fieldErrors.endDate ? 'error' : ''} />
                  </Field>
                </div>
                <Field label="Duration limit (minutes)" help="Leave blank for untimed exam">
                  <input type="number" name="timeLimit" value={formData.timeLimit || ''} onChange={handleChange} min="1" placeholder="e.g., 60" />
                </Field>
              </FormSection>

              <FormSection title="Question display" defaultOpen={false}>
                <Toggle name="shuffleQuestions" checked={formData.shuffleQuestions} onChange={handleChange} label="Shuffle question order" hint="Randomize question order for each student" />
                <Toggle name="shuffleAnswers" checked={formData.shuffleAnswers} onChange={handleChange} label="Shuffle answer options" hint="Randomize answer order for multiple choice questions" />
              </FormSection>

            </motion.div>
            </AnimatePresence>
          )}

          {/* ========== Questions & Display Tab ========== */}
          {activeTab === 'questions' && (
            <AnimatePresence mode="wait">
            <motion.div key="questions" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.15 }}>

              <div className="form-note">Add and manage questions from the next screen after creating the exam.</div>

              <FormSection title="Display settings" defaultOpen={true}>
                <Toggle name="showResults" checked={formData.showResults} onChange={handleChange} label="Show results to students" hint="Allow students to see their answers after submission" />
                <Toggle name="allowReview" checked={formData.allowReview} onChange={handleChange} label="Allow review" hint="Students can retake after first submission if retakes are enabled" />
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
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                    <div className="toggle-wrapper">
                      <Toggle name="proctorDetectTabSwitch" checked={formData.proctorDetectTabSwitch} onChange={handleChange} label="Detect tab switching" hint="Record when students switch browser tabs" />
                    </div>
                    <Reveal open={formData.proctorDetectTabSwitch}>
                      <Field label="Points to deduct per tab switch">
                        <input type="number" name="proctorPointDeductionTabSwitch" value={formData.proctorPointDeductionTabSwitch} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                    <div className="toggle-wrapper">
                      <Toggle name="proctorDetectCopyPaste" checked={formData.proctorDetectCopyPaste} onChange={handleChange} label="Detect copy/paste" hint="Record copy and paste attempts" />
                    </div>
                    <Reveal open={formData.proctorDetectCopyPaste}>
                      <Field label="Points to deduct per copy/paste">
                        <input type="number" name="proctorPointDeductionCopyPaste" value={formData.proctorPointDeductionCopyPaste} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                    <div className="toggle-wrapper">
                      <Toggle name="proctorDisableRightClick" checked={formData.proctorDisableRightClick} onChange={handleChange} label="Disable right-click" hint="Prevent right-click context menu" />
                    </div>
                    <Reveal open={formData.proctorDisableRightClick}>
                      <Field label="Points to deduct per right-click">
                        <input type="number" name="proctorPointDeductionRightClick" value={formData.proctorPointDeductionRightClick} onChange={handleChange} min="0" placeholder="0" />
                      </Field>
                    </Reveal>
                    <div className="toggle-wrapper">
                      <Toggle name="proctorEnforceFullscreen" checked={formData.proctorEnforceFullscreen} onChange={handleChange} label="Enforce full-screen mode" hint="Students must stay in fullscreen mode" />
                    </div>
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
    </MainLayout>
  );
}
