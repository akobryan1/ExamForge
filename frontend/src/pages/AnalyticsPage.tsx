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

interface ScoreBucket {
  range: string;
  count: number;
}

interface TimeScorePoint {
  timeSpent: number;
  score: number;
}

interface AnalyticsData {
  totalAttempts: number;
  averageScore: number;
  passRate: number;
  averageTime: number;
  questionStats: QuestionStat[];
  scoreDistribution: ScoreBucket[];
  timeVsScore: TimeScorePoint[];
}

export function AnalyticsPage() {
  const { data: exams = [], isLoading } = useExamList();
  const [selectedExam, setSelectedExam] = useState<string | null>(null);
  const { data: analytics } = useExamAnalytics(selectedExam || undefined);
  const a = analytics as AnalyticsData | undefined;

  // ── SVG chart dimensions ──
  const CHART_W = 600;
  const CHART_H = 260;
  const PAD = { top: 20, right: 20, bottom: 40, left: 50 };

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

            {/* Score Distribution — Histogram */}
            <motion.div className="analytics-section" variants={fadeIn}>
              <h2>Score Distribution</h2>
              {a!.scoreDistribution && a!.scoreDistribution.length > 0 ? (
                <HistogramChart data={a!.scoreDistribution} totalAttempts={a!.totalAttempts} />
              ) : (
                <div className="chart-placeholder"><p>No distribution data available.</p></div>
              )}
            </motion.div>

            {/* Time Analysis — Scatter / Line */}
            <motion.div className="analytics-section" variants={fadeIn}>
              <h2>Time vs. Score</h2>
              {a!.timeVsScore && a!.timeVsScore.length > 1 ? (
                <TimeScoreChart data={a!.timeVsScore} />
              ) : (
                <div className="chart-placeholder"><p>Need at least 2 attempts to show time correlation.</p></div>
              )}
            </motion.div>
          </motion.div>
        )}
      </motion.div>
    </MainLayout>
  );
}

/* ─────────────────────────────────────────────
   Histogram: Grade Distribution
   ───────────────────────────────────────────── */
function HistogramChart({ data, totalAttempts }: { data: ScoreBucket[]; totalAttempts: number }) {
  const W = 600, H = 260, P = { top: 20, right: 20, bottom: 40, left: 50 };
  const maxCount = Math.max(...data.map(d => d.count), 1);
  const innerW = W - P.left - P.right;
  const innerH = H - P.top - P.bottom;
  const barW = Math.max(innerW / data.length - 6, 18);
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);

  const yTicks = [0, Math.round(maxCount / 2) || 1, maxCount];

  return (
    <div className="chart-container">
      <svg viewBox={`0 0 ${W} ${H}`} className="chart-svg">
        {/* Grid lines */}
        {yTicks.map(v => (
          <g key={v}>
            <line x1={P.left} y1={P.top + innerH - (v / maxCount) * innerH} x2={W - P.right} y2={P.top + innerH - (v / maxCount) * innerH} stroke="var(--ledger-line-soft)" strokeWidth="1" />
            <text x={P.left - 8} y={P.top + innerH - (v / maxCount) * innerH + 4} textAnchor="end" className="chart-axis-label">{v}</text>
          </g>
        ))}

        {/* Bars */}
        {data.map((d, i) => {
          const barH = (d.count / maxCount) * innerH;
          const x = P.left + (i / data.length) * innerW + (innerW / data.length - barW) / 2;
          const y = P.top + innerH - barH;
          const isHover = hoverIdx === i;
          return (
            <g key={d.range}>
              <motion.rect
                x={x} y={P.top + innerH} width={barW} height={0}
                initial={{ height: 0, y: P.top + innerH }}
                animate={{ height: barH, y }}
                transition={{ duration: 0.5, delay: i * 0.03, ease: 'easeOut' }}
                rx={3} ry={3}
                fill={d.count === 0 ? 'var(--ledger-line-soft)' : 'var(--ledger-ink-blue)'}
                opacity={isHover ? 1 : 0.78}
                onMouseEnter={() => setHoverIdx(i)}
                onMouseLeave={() => setHoverIdx(null)}
                style={{ cursor: 'pointer', transition: 'opacity 0.15s' }}
              />
              {/* X-axis label */}
              <text
                x={P.left + (i / data.length) * innerW + innerW / data.length / 2}
                y={H - 8}
                textAnchor="end"
                transform={`rotate(-35, ${P.left + (i / data.length) * innerW + innerW / data.length / 2}, ${H - 8})`}
                className="chart-axis-label"
                fontSize="9"
              >{d.range}</text>
              {/* Tooltip */}
              {isHover && (
                <g>
                  <rect x={x + barW / 2 + 6} y={y - 30} width={80} height={26} rx={4} fill="var(--ledger-ink)" opacity={0.9} />
                  <text x={x + barW / 2 + 46} y={y - 13} textAnchor="middle" fill="white" fontSize="11" fontWeight="600">
                    {d.count} student{d.count !== 1 ? 's' : ''}
                  </text>
                </g>
              )}
            </g>
          );
        })}

        {/* Axes */}
        <line x1={P.left} y1={P.top} x2={P.left} y2={H - P.bottom} stroke="var(--ledger-line)" strokeWidth="1" />
        <line x1={P.left} y1={H - P.bottom} x2={W - P.right} y2={H - P.bottom} stroke="var(--ledger-line)" strokeWidth="1" />
      </svg>
      <div className="chart-footer">
        <span className="chart-footer-label">Score range</span>
        <span className="chart-footer-value">{totalAttempts} total attempt{totalAttempts !== 1 ? 's' : ''}</span>
      </div>
    </div>
  );
}

/* ─────────────────────────────────────────────
   Scatter / Line Chart: Time Spent vs Score
   ───────────────────────────────────────────── */
function TimeScoreChart({ data }: { data: TimeScorePoint[] }) {
  const W = 600, H = 260, P = { top: 20, right: 20, bottom: 40, left: 50 };
  const maxTime = Math.max(...data.map(d => d.timeSpent), 1);
  const innerW = W - P.left - P.right;
  const innerH = H - P.top - P.bottom;
  const [hoverIdx, setHoverIdx] = useState<number | null>(null);

  // Sort by time spent for the line
  const sorted = [...data].sort((a, b) => a.timeSpent - b.timeSpent);

  const xVal = (v: number) => P.left + (v / maxTime) * innerW;
  const yVal = (v: number) => P.top + innerH - (v / 100) * innerH;

  const xTicks = [0, Math.round(maxTime / 2), maxTime];
  const yTicks = [0, 25, 50, 75, 100];

  // ── Linear regression (line of best fit) ──
  const n = data.length;
  const sumX = data.reduce((s, d) => s + d.timeSpent, 0);
  const sumY = data.reduce((s, d) => s + d.score, 0);
  const sumXY = data.reduce((s, d) => s + d.timeSpent * d.score, 0);
  const sumX2 = data.reduce((s, d) => s + d.timeSpent * d.timeSpent, 0);
  const slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
  const intercept = (sumY - slope * sumX) / n;

  const trendStart = { x: 0, y: intercept };
  const trendEnd = { x: maxTime, y: slope * maxTime + intercept };

  return (
    <div className="chart-container">
      <svg viewBox={`0 0 ${W} ${H}`} className="chart-svg">
        {/* Grid */}
        {yTicks.map(v => (
          <g key={`y${v}`}>
            <line x1={P.left} y1={yVal(v)} x2={W - P.right} y2={yVal(v)} stroke="var(--ledger-line-soft)" strokeWidth="1" />
            <text x={P.left - 8} y={yVal(v) + 4} textAnchor="end" className="chart-axis-label">{v}%</text>
          </g>
        ))}
        {xTicks.map(v => (
          <g key={`x${v}`}>
            <text x={xVal(v)} y={H - 8} textAnchor="middle" className="chart-axis-label">{v}m</text>
          </g>
        ))}

        {/* Trend line */}
        <line
          x1={xVal(trendStart.x)} y1={yVal(Math.max(0, Math.min(100, trendStart.y)))}
          x2={xVal(trendEnd.x)} y2={yVal(Math.max(0, Math.min(100, trendEnd.y)))}
          stroke="var(--ledger-amber)" strokeWidth="2" strokeDasharray="4 3" opacity={0.7}
        />

        {/* Dots */}
        {data.map((d, i) => {
          const cx = xVal(d.timeSpent);
          const cy = yVal(d.score);
          const isHover = hoverIdx === i;
          return (
            <g key={i}>
              <circle
                cx={cx} cy={cy} r={isHover ? 7 : 5}
                fill={d.score >= 60 ? 'var(--ledger-green)' : 'var(--ledger-red)'}
                opacity={isHover ? 1 : 0.7}
                stroke={isHover ? 'var(--ledger-ink)' : 'none'}
                strokeWidth={isHover ? 2 : 0}
                onMouseEnter={() => setHoverIdx(i)}
                onMouseLeave={() => setHoverIdx(null)}
                style={{ cursor: 'pointer', transition: 'r 0.15s, opacity 0.15s' }}
              />
              {isHover && (
                <g>
                  <rect x={cx + 10} y={cy - 20} width={120} height={32} rx={4} fill="var(--ledger-ink)" opacity={0.9} />
                  <text x={cx + 70} y={cy - 7} textAnchor="middle" fill="white" fontSize="10" fontWeight="600">{d.timeSpent}m · {d.score}%</text>
                  <text x={cx + 70} y={cy + 5} textAnchor="middle" fill="rgba(255,255,255,0.7)" fontSize="9">{d.score >= 60 ? 'Passed' : 'Failed'}</text>
                </g>
              )}
            </g>
          );
        })}

        {/* Axes */}
        <line x1={P.left} y1={P.top} x2={P.left} y2={H - P.bottom} stroke="var(--ledger-line)" strokeWidth="1" />
        <line x1={P.left} y1={H - P.bottom} x2={W - P.right} y2={H - P.bottom} stroke="var(--ledger-line)" strokeWidth="1" />
      </svg>
      <div className="chart-footer">
        <span className="chart-footer-label">Time spent (minutes)</span>
        <span className="chart-footer-value">
          {data.length} attempt{data.length !== 1 ? 's' : ''}
          {slope !== 0 && (
            <span className="trend-hint">
              {' · '}{slope > 0 ? '↑' : '↓'} Trend: {Math.abs(slope).toFixed(1)}% per min
            </span>
          )}
        </span>
      </div>
    </div>
  );
}
