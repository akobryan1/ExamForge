import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import type { RetakeConfiguration, LateSubmissionConfiguration, ProctorConfiguration } from '../types/exam';
import '../styles/pages/exam-form.css';

type TabType = 'basic' | 'access' | 'timing' | 'questions' | 'proctoring' | 'advanced' | 'instructions';

export function CreateExamPageEnhanced() {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabType>('basic');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const [formData, setFormData] = useState({
    // Basic Info
    title: '',
    description: '',
    subject: '',
    grade: '',
    
    // Exam Settings
    passingScore: 60,
    timeLimit: undefined as number | undefined,
    shuffleQuestions: false,
    shuffleAnswers: false,
    showResults: true,
    allowReview: true,
    
    // Access Control
    accessCode: '',
    allowGuestAccess: false,
    sections: [] as string[],
    
    // Scheduling
    startDate: undefined as Date | undefined,
    endDate: undefined as Date | undefined,
    
    // Retake Configuration
    retakeEnabled: false,
    retakeMaxRetakes: undefined as number | undefined,
    retakeRequireApproval: false,
    retakeScoringMethod: 'best' as 'best' | 'latest' | 'average',
    
    // Late Submission
    lateSubmissionPolicy: 'disabled' as 'allowed' | 'disabled' | 'request_permission',
    lateGracePeriodMinutes: 0,
    latePenaltyPoints: 0,
    latePenaltyInterval: 'minute' as 'minute' | 'hour' | 'day',
    
    // Proctoring
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
    
    // Custom Instructions
    customInstructions: '',
    showRulesBeforeExam: true,
  });

  const [sectionInput, setSectionInput] = useState('');

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    
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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.title.trim()) {
      setError('Title is required');
      setActiveTab('basic');
      return;
    }
    
    if (!formData.description.trim()) {
      setError('Description is required');
      setActiveTab('basic');
      return;
    }
    
    try {
      setLoading(true);
      setError(null);
      
      // Build retake config
      const retakeConfig: RetakeConfiguration | undefined = formData.retakeEnabled ? {
        enabled: true,
        maxRetakes: formData.retakeMaxRetakes,
        requireApproval: formData.retakeRequireApproval,
        scoringMethod: formData.retakeScoringMethod,
      } : undefined;
      
      // Build late submission config
      const lateSubmissionConfig: LateSubmissionConfiguration | undefined = 
        formData.lateSubmissionPolicy !== 'disabled' ? {
          policy: formData.lateSubmissionPolicy,
          gracePeriodMinutes: formData.lateGracePeriodMinutes || undefined,
          penaltyPoints: formData.latePenaltyPoints || undefined,
          penaltyInterval: formData.latePenaltyInterval,
        } : undefined;
      
      // Build proctor config
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
        accessCode: formData.accessCode || undefined,
        allowGuestAccess: formData.allowGuestAccess,
        sections: formData.sections.length > 0 ? formData.sections : undefined,
        startDate: formData.startDate,
        endDate: formData.endDate,
        retakeConfig,
        lateSubmissionConfig,
        proctorConfig,
        customInstructions: formData.customInstructions || undefined,
        showRulesBeforeExam: formData.showRulesBeforeExam,
      };
      
      const exam = await ExamService.createExam(examData);
      
      // Redirect to exam questions page
      navigate(`/exams/${exam.id}/questions`);
    } catch (err: any) {
      const serverError = err.response?.data?.error;
      const errorMsg = serverError || err.message || 'Failed to create exam';
      console.error('[CreateExam] Error:', errorMsg, '| Server:', serverError, '| Full:', err);
      setError(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const tabs = [
    { id: 'basic' as TabType, label: 'Basic Info', icon: '📝' },
    { id: 'access' as TabType, label: 'Access Control', icon: '🔒' },
    { id: 'timing' as TabType, label: 'Timing & Schedule', icon: '⏰' },
    { id: 'questions' as TabType, label: 'Questions & Display', icon: '❓' },
    { id: 'proctoring' as TabType, label: 'Proctoring & Anti-Cheat', icon: '👁️' },
    { id: 'advanced' as TabType, label: 'Retakes & Late Submission', icon: '🔄' },
    { id: 'instructions' as TabType, label: 'Custom Instructions', icon: '📋' },
  ];

  return (
    <MainLayout>
    <div className="exam-form-page enhanced">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
      >
        <div className="form-header">
          <h1>Create New Exam</h1>
          <p className="form-subtitle">Configure all exam settings using the tabs below</p>
        </div>

        {error && (
          <div className="error-banner">
            {error}
          </div>
        )}

        {/* Tab Navigation */}
        <div className="tab-navigation">
          {tabs.map(tab => (
            <button
              key={tab.id}
              type="button"
              className={`tab-button ${activeTab === tab.id ? 'active' : ''}`}
              onClick={() => setActiveTab(tab.id)}
            >
              <span className="tab-icon">{tab.icon}</span>
              <span className="tab-label">{tab.label}</span>
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} className="exam-form tabbed">
          {/* Basic Info Tab */}
          {activeTab === 'basic' && (
            <motion.div
              key="basic"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Basic Information</h2>
              
              <div className="form-group">
                <label htmlFor="title">Exam Title *</label>
                <input
                  type="text"
                  id="title"
                  name="title"
                  value={formData.title}
                  onChange={handleChange}
                  placeholder="e.g., Midterm Exam - Chapter 1-5"
                  required
                />
              </div>

              <div className="form-group">
                <label htmlFor="description">Description *</label>
                <textarea
                  id="description"
                  name="description"
                  value={formData.description}
                  onChange={handleChange}
                  placeholder="Provide details about the exam content and objectives..."
                  rows={4}
                  required
                />
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="subject">Subject</label>
                  <input
                    type="text"
                    id="subject"
                    name="subject"
                    value={formData.subject}
                    onChange={handleChange}
                    placeholder="e.g., Mathematics, Science"
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="grade">Grade Level</label>
                  <input
                    type="text"
                    id="grade"
                    name="grade"
                    value={formData.grade}
                    onChange={handleChange}
                    placeholder="e.g., Grade 10, College"
                  />
                </div>
              </div>

              <div className="form-group">
                <label htmlFor="passingScore">Passing Score (%)</label>
                <input
                  type="number"
                  id="passingScore"
                  name="passingScore"
                  value={formData.passingScore}
                  onChange={handleChange}
                  min="0"
                  max="100"
                  required
                />
              </div>
            </motion.div>
          )}

          {/* Access Control Tab */}
          {activeTab === 'access' && (
            <motion.div
              key="access"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Access Control</h2>
              
              <div className="form-group">
                <label htmlFor="accessCode">Access Code</label>
                <input
                  type="text"
                  id="accessCode"
                  name="accessCode"
                  value={formData.accessCode}
                  onChange={handleChange}
                  placeholder="Optional - Students will need this code to start the exam"
                />
                <small>Leave blank for no access code requirement</small>
              </div>

              <div className="checkbox-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="allowGuestAccess"
                    checked={formData.allowGuestAccess}
                    onChange={handleChange}
                  />
                  <span>Allow Guest Access</span>
                  <small>Non-registered students can take the exam</small>
                </label>
              </div>

              <div className="form-group">
                <label>Allowed Sections/Classes</label>
                <div className="section-input-group">
                  <input
                    type="text"
                    value={sectionInput}
                    onChange={(e) => setSectionInput(e.target.value)}
                    placeholder="Enter section name (e.g., Section A, Class 10-B)"
                    onKeyPress={(e) => e.key === 'Enter' && (e.preventDefault(), addSection())}
                  />
                  <button type="button" onClick={addSection} className="btn-add">Add</button>
                </div>
                {formData.sections.length > 0 && (
                  <div className="section-tags">
                    {formData.sections.map(section => (
                      <span key={section} className="section-tag">
                        {section}
                        <button type="button" onClick={() => removeSection(section)}>×</button>
                      </span>
                    ))}
                  </div>
                )}
                <small>Leave empty to allow all students</small>
              </div>
            </motion.div>
          )}

          {/* Timing & Schedule Tab */}
          {activeTab === 'timing' && (
            <motion.div
              key="timing"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Timing & Schedule</h2>
              
              <div className="form-group">
                <label htmlFor="timeLimit">Time Limit (minutes)</label>
                <input
                  type="number"
                  id="timeLimit"
                  name="timeLimit"
                  value={formData.timeLimit || ''}
                  onChange={handleChange}
                  min="1"
                  placeholder="Optional - Leave blank for no time limit"
                />
                <small>Students will be auto-submitted when time runs out</small>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="startDate">Start Date & Time</label>
                  <input
                    type="datetime-local"
                    id="startDate"
                    name="startDate"
                    value={formData.startDate ? new Date(formData.startDate).toISOString().slice(0, 16) : ''}
                    onChange={(e) => setFormData(prev => ({ 
                      ...prev, 
                      startDate: e.target.value ? new Date(e.target.value) : undefined 
                    }))}
                  />
                  <small>When students can start taking the exam</small>
                </div>

                <div className="form-group">
                  <label htmlFor="endDate">End Date & Time</label>
                  <input
                    type="datetime-local"
                    id="endDate"
                    name="endDate"
                    value={formData.endDate ? new Date(formData.endDate).toISOString().slice(0, 16) : ''}
                    onChange={(e) => setFormData(prev => ({ 
                      ...prev, 
                      endDate: e.target.value ? new Date(e.target.value) : undefined 
                    }))}
                  />
                  <small>When the exam becomes unavailable</small>
                </div>
              </div>
            </motion.div>
          )}

          {/* Questions & Display Tab */}
          {activeTab === 'questions' && (
            <motion.div
              key="questions"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Questions & Display Options</h2>
              
              <div className="checkbox-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="shuffleQuestions"
                    checked={formData.shuffleQuestions}
                    onChange={handleChange}
                  />
                  <span>Shuffle Questions</span>
                  <small>Randomize question order for each student</small>
                </label>

                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="shuffleAnswers"
                    checked={formData.shuffleAnswers}
                    onChange={handleChange}
                  />
                  <span>Shuffle Answer Choices</span>
                  <small>Randomize answer order for multiple choice questions</small>
                </label>

                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="showResults"
                    checked={formData.showResults}
                    onChange={handleChange}
                  />
                  <span>Show Results to Students</span>
                  <small>Students can see their score after submitting</small>
                </label>

                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="allowReview"
                    checked={formData.allowReview}
                    onChange={handleChange}
                  />
                  <span>Allow Review</span>
                  <small>Students can review their answers and correct answers after submission</small>
                </label>
              </div>
            </motion.div>
          )}

          {/* Proctoring & Anti-Cheat Tab */}
          {activeTab === 'proctoring' && (
            <motion.div
              key="proctoring"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Proctoring & Anti-Cheat Measures</h2>
              
              <div className="checkbox-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="proctorEnabled"
                    checked={formData.proctorEnabled}
                    onChange={handleChange}
                  />
                  <span>Enable Proctoring</span>
                  <small>Activate anti-cheat measures during the exam</small>
                </label>
              </div>

              {formData.proctorEnabled && (
                <>
                  <div className="form-subsection">
                    <h3>Detection Settings</h3>
                    <div className="checkbox-group">
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          name="proctorEnforceFullscreen"
                          checked={formData.proctorEnforceFullscreen}
                          onChange={handleChange}
                        />
                        <span>Enforce Fullscreen</span>
                        <small>Students must stay in fullscreen mode</small>
                      </label>

                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          name="proctorDetectTabSwitch"
                          checked={formData.proctorDetectTabSwitch}
                          onChange={handleChange}
                        />
                        <span>Detect Tab Switching</span>
                        <small>Record when students switch browser tabs</small>
                      </label>

                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          name="proctorDetectCopyPaste"
                          checked={formData.proctorDetectCopyPaste}
                          onChange={handleChange}
                        />
                        <span>Detect Copy/Paste</span>
                        <small>Record copy and paste attempts</small>
                      </label>

                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          name="proctorDisableRightClick"
                          checked={formData.proctorDisableRightClick}
                          onChange={handleChange}
                        />
                        <span>Disable Right-Click</span>
                        <small>Prevent right-click context menu</small>
                      </label>
                    </div>
                  </div>

                  <div className="form-subsection">
                    <h3>Point Deductions</h3>
                    <p className="subsection-description">
                      Points deducted from the exam score for each violation. Leave at 0 for no deduction.
                    </p>
                    
                    <div className="form-row">
                      <div className="form-group">
                        <label htmlFor="proctorPointDeductionTabSwitch">Tab Switch</label>
                        <input
                          type="number"
                          id="proctorPointDeductionTabSwitch"
                          name="proctorPointDeductionTabSwitch"
                          value={formData.proctorPointDeductionTabSwitch}
                          onChange={handleChange}
                          min="0"
                          placeholder="0"
                        />
                      </div>

                      <div className="form-group">
                        <label htmlFor="proctorPointDeductionCopyPaste">Copy/Paste</label>
                        <input
                          type="number"
                          id="proctorPointDeductionCopyPaste"
                          name="proctorPointDeductionCopyPaste"
                          value={formData.proctorPointDeductionCopyPaste}
                          onChange={handleChange}
                          min="0"
                          placeholder="0"
                        />
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label htmlFor="proctorPointDeductionRightClick">Right-Click</label>
                        <input
                          type="number"
                          id="proctorPointDeductionRightClick"
                          name="proctorPointDeductionRightClick"
                          value={formData.proctorPointDeductionRightClick}
                          onChange={handleChange}
                          min="0"
                          placeholder="0"
                        />
                      </div>

                      <div className="form-group">
                        <label htmlFor="proctorPointDeductionExitFullscreen">Exit Fullscreen</label>
                        <input
                          type="number"
                          id="proctorPointDeductionExitFullscreen"
                          name="proctorPointDeductionExitFullscreen"
                          value={formData.proctorPointDeductionExitFullscreen}
                          onChange={handleChange}
                          min="0"
                          placeholder="0"
                        />
                      </div>
                    </div>

                    <div className="form-group">
                      <label htmlFor="proctorPointDeductionGeneral">General Violation (Fallback)</label>
                      <input
                        type="number"
                        id="proctorPointDeductionGeneral"
                        name="proctorPointDeductionGeneral"
                        value={formData.proctorPointDeductionGeneral}
                        onChange={handleChange}
                        min="0"
                        placeholder="0"
                      />
                      <small>Applied when a specific deduction is not set</small>
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="proctorCustomRules">Custom Proctoring Rules</label>
                    <textarea
                      id="proctorCustomRules"
                      name="proctorCustomRules"
                      value={formData.proctorCustomRules}
                      onChange={handleChange}
                      placeholder="Add any custom rules or instructions for proctoring..."
                      rows={3}
                    />
                  </div>
                </>
              )}
            </motion.div>
          )}

          {/* Retakes & Late Submission Tab */}
          {activeTab === 'advanced' && (
            <motion.div
              key="advanced"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Retakes & Late Submission</h2>
              
              <div className="form-subsection">
                <h3>Retake Configuration</h3>
                <div className="checkbox-group">
                  <label className="checkbox-label">
                    <input
                      type="checkbox"
                      name="retakeEnabled"
                      checked={formData.retakeEnabled}
                      onChange={handleChange}
                    />
                    <span>Allow Retakes</span>
                    <small>Students can retake this exam</small>
                  </label>
                </div>

                {formData.retakeEnabled && (
                  <>
                    <div className="form-group">
                      <label htmlFor="retakeMaxRetakes">Maximum Retakes</label>
                      <input
                        type="number"
                        id="retakeMaxRetakes"
                        name="retakeMaxRetakes"
                        value={formData.retakeMaxRetakes || ''}
                        onChange={handleChange}
                        min="0"
                        placeholder="Leave blank for unlimited"
                      />
                      <small>0 = no retakes, blank = unlimited</small>
                    </div>

                    <div className="checkbox-group">
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          name="retakeRequireApproval"
                          checked={formData.retakeRequireApproval}
                          onChange={handleChange}
                        />
                        <span>Require Instructor Approval</span>
                        <small>Students must request permission to retake</small>
                      </label>
                    </div>

                    <div className="form-group">
                      <label htmlFor="retakeScoringMethod">Scoring Method</label>
                      <select
                        id="retakeScoringMethod"
                        name="retakeScoringMethod"
                        value={formData.retakeScoringMethod}
                        onChange={handleChange}
                      >
                        <option value="best">Best Score</option>
                        <option value="latest">Latest Score</option>
                        <option value="average">Average Score</option>
                      </select>
                      <small>Which attempt score to use for the final grade</small>
                    </div>
                  </>
                )}
              </div>

              <div className="form-subsection">
                <h3>Late Submission Policy</h3>
                <div className="form-group">
                  <label htmlFor="lateSubmissionPolicy">Late Submission</label>
                  <select
                    id="lateSubmissionPolicy"
                    name="lateSubmissionPolicy"
                    value={formData.lateSubmissionPolicy}
                    onChange={handleChange}
                  >
                    <option value="disabled">Disabled (Hard Deadline)</option>
                    <option value="allowed">Allowed (With Penalty)</option>
                    <option value="request_permission">Require Permission</option>
                  </select>
                </div>

                {formData.lateSubmissionPolicy !== 'disabled' && (
                  <>
                    <div className="form-group">
                      <label htmlFor="lateGracePeriodMinutes">Grace Period (minutes)</label>
                      <input
                        type="number"
                        id="lateGracePeriodMinutes"
                        name="lateGracePeriodMinutes"
                        value={formData.lateGracePeriodMinutes}
                        onChange={handleChange}
                        min="0"
                        placeholder="0"
                      />
                      <small>Time after deadline with no penalty</small>
                    </div>

                    {formData.lateSubmissionPolicy === 'allowed' && (
                      <>
                        <div className="form-row">
                          <div className="form-group">
                            <label htmlFor="latePenaltyPoints">Penalty (points)</label>
                            <input
                              type="number"
                              id="latePenaltyPoints"
                              name="latePenaltyPoints"
                              value={formData.latePenaltyPoints}
                              onChange={handleChange}
                              min="0"
                              placeholder="0"
                            />
                          </div>

                          <div className="form-group">
                            <label htmlFor="latePenaltyInterval">Per</label>
                            <select
                              id="latePenaltyInterval"
                              name="latePenaltyInterval"
                              value={formData.latePenaltyInterval}
                              onChange={handleChange}
                            >
                              <option value="minute">Minute</option>
                              <option value="hour">Hour</option>
                              <option value="day">Day</option>
                            </select>
                          </div>
                        </div>
                        <small>Points deducted for every time interval late</small>
                      </>
                    )}
                  </>
                )}
              </div>
            </motion.div>
          )}

          {/* Custom Instructions Tab */}
          {activeTab === 'instructions' && (
            <motion.div
              key="instructions"
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -20 }}
              className="tab-content"
            >
              <h2>Custom Instructions</h2>
              
              <div className="form-group">
                <label htmlFor="customInstructions">Exam Instructions</label>
                <textarea
                  id="customInstructions"
                  name="customInstructions"
                  value={formData.customInstructions}
                  onChange={handleChange}
                  placeholder="Enter custom instructions, rules, or guidelines for students taking this exam..."
                  rows={8}
                />
                <small>These instructions will be displayed to students before they begin the exam</small>
              </div>

              <div className="checkbox-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    name="showRulesBeforeExam"
                    checked={formData.showRulesBeforeExam}
                    onChange={handleChange}
                  />
                  <span>Show Rules Before Exam</span>
                  <small>Display all exam rules and anti-cheat settings before students start</small>
                </label>
              </div>
            </motion.div>
          )}

          <div className="form-actions sticky">
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
              {loading ? 'Creating...' : 'Create Exam & Add Questions'}
            </button>
          </div>
        </form>
      </motion.div>
    </div>
    </MainLayout>
  );
}
