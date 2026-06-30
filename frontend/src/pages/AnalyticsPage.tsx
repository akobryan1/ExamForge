import { useState } from 'react';
import { motion } from 'framer-motion';
import { MainLayout } from '../layouts/MainLayout';
import { useExamList, useExamAnalytics } from '../hooks/useExamQueries';
import { pageTransition, fadeIn, staggerContainer } from '../utils/animations';
import '../styles/pages/analytics.css';

interface QuestionStat {
  questionId: string;
  questionText: string;
  correctRate: number;
  averagePoints: number;
}

interface AnalyticsData {
  totalAttempts: number;
  averageScore: number;
  passRate: number;
  averageTime: number;
  questionStats: QuestionStat[];
}

export function AnalyticsPage() {
  const { data: exams = [], isLoading } = useExamList();
  const [selectedExam, setSelectedExam] = useState<string | null>(null);
  const { data: analytics } = useExamAnalytics(selectedExam || undefined);
  const a = analytics as AnalyticsData | undefined;

  if (isLoading) {
    return (
      <MainLayout>
        <div style={{ textAlign: 'center', padding: 'var(--spacing-12)' }}>
          <p>Loading analytics...</p>
        </div>
      </MainLayout>
    );
  }

  if (exams.length === 0 && !isLoading) {
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
              onChange={(e) => setSelectedExam(e.target.value || null)}
              className="exam-select"
            >
              <option value="">— Select an exam —</option>
              {exams.map((exam) => (
                <option key={exam.id} value={exam.id}>
                  {exam.title} ({exam.attemptCount || 0} attempts)
                </option>
              ))}
            </select>
          </div>
        </div>

        {!selectedExam && (
          <div className="empty-state">
            <h2>Select an Exam</h2>
            <p>Choose an exam from the dropdown above to view its analytics.</p>
          </div>
        )}

        {selectedExam && !a && (
          <div className="empty-state">
            <h2>No Data Available</h2>
            <p>No attempt data found for this exam yet. Analytics will appear once students take the exam.</p>
          </div>
        )}

        {a && (
          <motion.div variants={staggerContainer}>
            {/* Overview Cards */}
            <div className="stats-grid">
              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">📊</div>
                <div className="stat-content">
                  <div className="stat-label">Total Attempts</div>
                  <div className="stat-value">{a!.totalAttempts}</div>
                </div>
              </motion.div>

              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">📈</div>
                <div className="stat-content">
                  <div className="stat-label">Average Score</div>
                  <div className="stat-value">{a!.averageScore.toFixed(1)}%</div>
                </div>
              </motion.div>

              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">✅</div>
                <div className="stat-content">
                  <div className="stat-label">Pass Rate</div>
                  <div className="stat-value">{a!.passRate.toFixed(1)}%</div>
                </div>
              </motion.div>

              <motion.div className="stat-card" variants={fadeIn}>
                <div className="stat-icon">⏱️</div>
                <div className="stat-content">
                  <div className="stat-label">Avg. Time</div>
                  <div className="stat-value">
                    {Math.floor(a!.averageTime / 60)}m
                  </div>
                </div>
              </motion.div>
            </div>

            {/* Question Performance */}
            {a!.questionStats.length > 0 && (
              <motion.div className="analytics-section" variants={fadeIn}>
                <h2>Question Performance</h2>
                <div className="question-stats-list">
                  {a!.questionStats.map((stat, index) => (
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
