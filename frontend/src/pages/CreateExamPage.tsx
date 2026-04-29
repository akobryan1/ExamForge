import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ExamService } from '../services/ExamService';
import type { CreateExamFormData } from '../types/exam';
import '../styles/pages/exam-form.css';

export function CreateExamPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const [formData, setFormData] = useState<CreateExamFormData>({
    title: '',
    description: '',
    subject: '',
    grade: '',
    passingScore: 60,
    shuffleQuestions: false,
    shuffleAnswers: false,
    showResults: true,
    allowReview: true,
  });

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

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.title.trim()) {
      setError('Title is required');
      return;
    }
    
    if (!formData.description.trim()) {
      setError('Description is required');
      return;
    }
    
    try {
      setLoading(true);
      setError(null);
      
      const exam = await ExamService.createExam(formData);
      
      // Redirect to exam questions page
      navigate(`/exams/${exam.id}/questions`);
    } catch (err: any) {
      setError(err.message || 'Failed to create exam');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page-container exam-form-page">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
      >
        <div className="form-header">
          <h1>Create New Exam</h1>
          <p className="form-subtitle">Set up your exam details and configuration</p>
        </div>

        {error && (
          <div className="error-banner">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="exam-form">
          <div className="form-section">
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
                  value={formData.subject || ''}
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
                  value={formData.grade || ''}
                  onChange={handleChange}
                  placeholder="e.g., Grade 10, College"
                />
              </div>
            </div>
          </div>

          <div className="form-section">
            <h2>Exam Settings</h2>
            
            <div className="form-row">
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

              <div className="form-group">
                <label htmlFor="timeLimit">Time Limit (minutes)</label>
                <input
                  type="number"
                  id="timeLimit"
                  name="timeLimit"
                  value={formData.timeLimit || ''}
                  onChange={handleChange}
                  min="1"
                  placeholder="Optional"
                />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="accessCode">Access Code</label>
              <input
                type="text"
                id="accessCode"
                name="accessCode"
                value={formData.accessCode || ''}
                onChange={handleChange}
                placeholder="Optional - Students will need this code to start the exam"
              />
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
              </div>
            </div>
          </div>

          <div className="form-section">
            <h2>Exam Options</h2>
            
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
                <small>Students can review their answers after submission</small>
              </label>
            </div>
          </div>

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
              {loading ? 'Creating...' : 'Create Exam & Add Questions'}
            </button>
          </div>
        </form>
      </motion.div>
    </div>
  );
}
