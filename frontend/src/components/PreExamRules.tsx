import { motion } from 'framer-motion';
import type { Exam } from '../types/exam';
import { Button } from '../components/Button';
import './pre-exam-rules.css';

interface PreExamRulesProps {
  exam: Exam;
  onAccept: () => void;
  onCancel: () => void;
}

export function PreExamRules({ exam, onAccept, onCancel }: PreExamRulesProps) {
  const hasTimeLimit = exam.timeLimit && exam.timeLimit > 0;
  const proctorEnabled = exam.proctorConfig?.enabled;
  const retakesAllowed = exam.retakeConfig?.enabled;
  const lateSubmissionAllowed = exam.lateSubmissionConfig?.policy !== 'disabled';

  return (
    <motion.div
      className="pre-exam-rules-overlay"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
    >
      <motion.div
        className="pre-exam-rules-card"
        initial={{ scale: 0.9, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        exit={{ scale: 0.9, opacity: 0 }}
      >
        <div className="rules-header">
          <h2>Exam Rules & Guidelines</h2>
          <h3>{exam.title}</h3>
          <p className="rules-subtitle">Please read carefully before proceeding</p>
        </div>

        <div className="rules-content">
          {/* Custom Instructions */}
          {exam.customInstructions && (
            <div className="rules-section custom-instructions">
              <h4>📋 Instructions</h4>
              <p className="instruction-text">{exam.customInstructions}</p>
            </div>
          )}

          {/* Exam Details */}
          <div className="rules-section">
            <h4>📝 Exam Details</h4>
            <ul className="rules-list">
              <li>
                <strong>Questions:</strong> {exam.questionCount} question{exam.questionCount !== 1 ? 's' : ''}
              </li>
              <li>
                <strong>Total Points:</strong> {exam.totalPoints}
              </li>
              <li>
                <strong>Passing Score:</strong> {exam.passingScore}%
              </li>
              {hasTimeLimit && (
                <li>
                  <strong>Time Limit:</strong> {exam.timeLimit} minutes
                  <span className="rule-warning"> (Auto-submit when time expires)</span>
                </li>
              )}
              {exam.shuffleQuestions && (
                <li>Questions will be presented in random order</li>
              )}
              {exam.shuffleAnswers && (
                <li>Answer choices will be randomized</li>
              )}
            </ul>
          </div>

          {/* Proctoring & Anti-Cheat */}
          {proctorEnabled && (
            <div className="rules-section proctor-section">
              <h4>👁️ Anti-Cheat Measures</h4>
              <p className="warning-text">
                This exam is proctored. The following behaviors are monitored:
              </p>
              <ul className="rules-list">
                {exam.proctorConfig?.enforceFullscreen && (
                  <li>
                    <strong>Fullscreen Required:</strong> You must stay in fullscreen mode
                    {exam.proctorConfig?.pointDeductions?.exitFullscreen ? 
                      ` (${exam.proctorConfig.pointDeductions.exitFullscreen} points deducted per violation)` : ''}
                  </li>
                )}
                {exam.proctorConfig?.detectTabSwitch && (
                  <li>
                    <strong>No Tab Switching:</strong> Switching tabs will be recorded
                    {exam.proctorConfig?.pointDeductions?.tabSwitch ? 
                      ` (${exam.proctorConfig.pointDeductions.tabSwitch} points deducted per violation)` : ''}
                  </li>
                )}
                {exam.proctorConfig?.detectCopyPaste && (
                  <li>
                    <strong>No Copy/Paste:</strong> Copy and paste actions will be detected
                    {exam.proctorConfig?.pointDeductions?.copyPaste ? 
                      ` (${exam.proctorConfig.pointDeductions.copyPaste} points deducted per violation)` : ''}
                  </li>
                )}
                {exam.proctorConfig?.disableRightClick && (
                  <li>Right-click context menu is disabled</li>
                )}
              </ul>
              {exam.proctorConfig?.customRules && (
                <p className="custom-rules">{exam.proctorConfig.customRules}</p>
              )}
            </div>
          )}

          {/* Retake Policy */}
          {retakesAllowed && (
            <div className="rules-section">
              <h4>🔄 Retake Policy</h4>
              <ul className="rules-list">
                <li>
                  <strong>Retakes Allowed:</strong>{' '}
                  {exam.retakeConfig?.maxRetakes === undefined 
                    ? 'Unlimited' 
                    : exam.retakeConfig.maxRetakes === 0 
                    ? 'None' 
                    : exam.retakeConfig.maxRetakes}
                </li>
                {exam.retakeConfig?.requireApproval && (
                  <li>Retakes require instructor approval</li>
                )}
                <li>
                  <strong>Score Used:</strong>{' '}
                  {exam.retakeConfig?.scoringMethod === 'best' && 'Best score from all attempts'}
                  {exam.retakeConfig?.scoringMethod === 'latest' && 'Latest attempt score'}
                  {exam.retakeConfig?.scoringMethod === 'average' && 'Average of all attempts'}
                </li>
              </ul>
            </div>
          )}

          {/* Late Submission */}
          {lateSubmissionAllowed && (
            <div className="rules-section">
              <h4>⏰ Late Submission Policy</h4>
              <ul className="rules-list">
                {exam.lateSubmissionConfig?.policy === 'allowed' && (
                  <>
                    <li>Late submissions are accepted with penalty</li>
                    {exam.lateSubmissionConfig.gracePeriodMinutes && exam.lateSubmissionConfig.gracePeriodMinutes > 0 && (
                      <li>
                        <strong>Grace Period:</strong> {exam.lateSubmissionConfig.gracePeriodMinutes} minutes (no penalty)
                      </li>
                    )}
                    {exam.lateSubmissionConfig.penaltyPoints && (
                      <li>
                        <strong>Penalty:</strong> {exam.lateSubmissionConfig.penaltyPoints} points per{' '}
                        {exam.lateSubmissionConfig.penaltyInterval}
                      </li>
                    )}
                  </>
                )}
                {exam.lateSubmissionConfig?.policy === 'request_permission' && (
                  <li>Late submissions require instructor permission</li>
                )}
              </ul>
            </div>
          )}

          {/* Submission Rules */}
          <div className="rules-section">
            <h4>✓ After Submission</h4>
            <ul className="rules-list">
              {exam.showResults ? (
                <li>You will see your score immediately after submission</li>
              ) : (
                <li>Results will be available after instructor review</li>
              )}
              {exam.allowReview ? (
                <li>You can review your answers and correct answers</li>
              ) : (
                <li>Answer review is not available for this exam</li>
              )}
            </ul>
          </div>

          {/* Important Notice */}
          <div className="rules-section important-notice">
            <h4>⚠️ Important</h4>
            <ul className="rules-list">
              <li>Ensure you have a stable internet connection</li>
              <li>Your answers are auto-saved every 30 seconds</li>
              <li>Do not refresh the page during the exam</li>
              <li>Contact your instructor if you experience technical issues</li>
            </ul>
          </div>
        </div>

        <div className="rules-footer">
          <p className="agreement-text">
            By clicking "I Understand & Accept", you agree to follow all exam rules and guidelines.
          </p>
          <div className="rules-actions">
            <Button variant="outline" onClick={onCancel}>
              Cancel
            </Button>
            <Button onClick={onAccept}>
              I Understand & Accept
            </Button>
          </div>
        </div>
      </motion.div>
    </motion.div>
  );
}
