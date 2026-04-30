import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { ExamService } from '../services/ExamService';
import { pageTransition, fadeIn, staggerContainer, staggerItem } from '../utils/animations';
import type { Exam } from '../types/exam';
import '../styles/pages/analytics.css';

interface ExamAnalytics {
  examId: string;
  examTitle: string;
  totalAttempts: number;
  averageScore: number;
  passRate: number;
  averageTime: number;
  questionStats: {
    questionId: string;
    questionText: string;
    correctRate: number;
    averagePoints: number;
  }[];
}

export function AnalyticsPage() {
  const [exams, setExams] = useState<Exam[]>([]);
  const [selectedExam, setSelectedExam] = useState<string | null>(null);
  const [analytics, setAnalytics] = useState<ExamAnalytics | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadExams();
  }, []);

  useEffect(() => {
    if (selectedExam) {
      loadAnalytics(selectedExam);
    }
  }, [selectedExam]);

  const loadExams = async () => {
    try {
      setLoading(true);
      const examsData = await ExamService.getExams();
      // Filter to only show exams with attempts
      const examsWithAttempts = examsData.filter(
        (exam) => (exam.attemptCount || 0) > 0
      );
      setExams(examsWithAttempts);

      if (examsWithAttempts.length > 0 && !selectedExam) {
        setSelectedExam(examsWithAttempts[0].id);
      }
    } catch (error) {
      console.error('Failed to load exams:', error);
    } finally {
      setLoading(false);
    }
  };

  const loadAnalytics = async (examId: string) => {
    try {
      const analyticsData = await ExamService.getExamAnalytics(examId);
      setAnalytics(analyticsData);
    } catch (error) {
      console.error('Failed to load analytics:', error);
    }
  };

  if (loading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
          <p>Loading analytics...</p>
        </div>
      </MainLayout>
    );
  }

  if (exams.length === 0) {
    return (
      <MainLayout>
        <div className="empty-state">
          <h2>No Data Available</h2>
          <p>There are no exam attempts yet. Analytics will appear once students start taking exams.</p>
        </div>
      </MainLayout>
    );
  }

  return (
    <MainLayout>
      <motion.div
        className="analytics-page"
        variants={pageTransition}
        initial="initial"
        animate="animate"
        exit="exit"
      >
        <div className="page-header">
          <div>
            <h1>Analytics</h1>
            <p className="page-subtitle">Exam performance metrics and insights</p>
          </div>
          <div className="header-actions">
            <select
              value={selectedExam || ''}
              onChange={(e) => setSelectedExam(e.target.value)}
              className="exam-select"
            >
              {exams.map((exam) => (
                <option key={exam.id} value={exam.id}>
                  {exam.title} ({exam.attemptCount || 0} attempts)
                </option>
              ))}
            </select>
          </div>
        </div>

        {analytics && (
          <motion.div variants={staggerContainer}>
            {/* Overview Cards */}
            <div className="stats-grid">
              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">📊</div>
                <div className="stat-content">
                  <div className="stat-label">Total Attempts</div>
                  <div className="stat-value">{analytics.totalAttempts}</div>
                </div>
              </motion.div>

              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">📈</div>
                <div className="stat-content">
                  <div className="stat-label">Average Score</div>
                  <div className="stat-value">{analytics.averageScore.toFixed(1)}%</div>
                </div>
              </motion.div>

              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">✅</div>
                <div className="stat-content">
                  <div className="stat-label">Pass Rate</div>
                  <div className="stat-value">{analytics.passRate.toFixed(1)}%</div>
                </div>
              </motion.div>

              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">⏱️</div>
                <div className="stat-content">
                  <div className="stat-label">Avg. Time</div>
                  <div className="stat-value">
                    {Math.floor(analytics.averageTime / 60)}m
                  </div>
                </div>
              </motion.div>
            </div>

            {/* Question Performance */}
            {analytics.questionStats.length > 0 && (
              <motion.div className="analytics-section" variants={fadeIn}>
                <h2>Question Performance</h2>
                <div className="question-stats-list">
                  {analytics.questionStats.map((stat, index) => (
                    <div key={stat.questionId} className="question-stat-card">
                      <div className="question-stat-header">
                        <span className="question-number">Q{index + 1}</span>
                        <span className="correct-rate">
                          {stat.correctRate.toFixed(0)}% correct
                        </span>
                      </div>
                      <p className="question-stat-text">
                        {stat.questionText.substring(0, 100)}
                        {stat.questionText.length > 100 ? '...' : ''}
                      </p>
                      <div className="question-stat-bar">
                        <div
                          className="stat-bar-fill"
                          style={{ width: `${stat.correctRate}%` }}
                        />
                      </div>
                      <div className="question-stat-footer">
                        <span>Avg. Points: {stat.averagePoints.toFixed(1)}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </motion.div>
            )}

            {/* Score Distribution (Placeholder) */}
            <motion.div className="analytics-section" variants={fadeIn}>
              <h2>Score Distribution</h2>
              <div className="chart-placeholder">
                <p>Score distribution chart will be displayed here</p>
                <p className="chart-note">
                  Future implementation: Interactive histogram showing grade distribution
                </p>
              </div>
            </motion.div>

            {/* Time Analysis (Placeholder) */}
            <motion.div className="analytics-section" variants={fadeIn}>
              <h2>Time Analysis</h2>
              <div className="chart-placeholder">
                <p>Time analysis chart will be displayed here</p>
                <p className="chart-note">
                  Future implementation: Line chart showing time spent vs. score correlation
                </p>
              </div>
            </motion.div>
          </motion.div>
        )}
      </motion.div>
    </MainLayout>
  );
}
