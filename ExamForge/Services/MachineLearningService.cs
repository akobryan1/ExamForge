using System;
using System.Collections.Generic;
using System.Linq;
using ExamForge.Models;

namespace ExamForge.Services
{
    public class MachineLearningService
    {
        // Predict student performance on future exams
        public PredictionResult PredictStudentPerformance(
            List<ExamSubmission> historicalData,
            string studentId)
        {
            var studentHistory = historicalData
                .Where(s => s.StudentId == studentId)
                .OrderBy(s => s.SubmittedAt)
                .ToList();

            if (studentHistory.Count < 2)
            {
                return new PredictionResult
                {
                    PredictedScore = 0,
                    Confidence = 0,
                    Trend = "Insufficient Data",
                    Recommendation = "Need at least 2 exam submissions for predictions"
                };
            }

            var scores = studentHistory.Select(s =>
                s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0).ToList();

            var trend = CalculateTrend(scores);
            var predictedScore = PredictNextScore(scores);
            var confidence = CalculateConfidence(scores);

            string recommendation;
            if (trend == "Improving")
            {
                recommendation = "Student shows positive growth. Continue current study methods.";
            }
            else if (trend == "Declining")
            {
                recommendation = "Student performance is declining. Consider intervention or additional support.";
            }
            else if (predictedScore < 60)
            {
                recommendation = "Student at risk of failing. Immediate intervention recommended.";
            }
            else if (predictedScore >= 90)
            {
                recommendation = "Excellent performance. Consider advanced material.";
            }
            else
            {
                recommendation = "Student performance is stable. Monitor progress.";
            }

            return new PredictionResult
            {
                PredictedScore = predictedScore,
                Confidence = confidence,
                Trend = trend,
                Recommendation = recommendation,
                HistoricalScores = scores
            };
        }

        // Identify at-risk students
        public List<AtRiskStudent> IdentifyAtRiskStudents(
            List<ExamSubmission> submissions,
            List<IntegrityIncident> incidents)
        {
            var studentGroups = submissions.GroupBy(s => s.StudentId);
            var atRiskStudents = new List<AtRiskStudent>();

            foreach (var group in studentGroups)
            {
                var studentId = group.Key;
                var studentSubmissions = group.OrderBy(s => s.SubmittedAt).ToList();

                var avgScore = studentSubmissions.Average(s =>
                    s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0);

                var scores = studentSubmissions.Select(s =>
                    s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0).ToList();

                var trend = CalculateTrend(scores);
                var recentScore = scores.LastOrDefault();
                var incidentCount = incidents.Count(i => i.StudentId == studentId);

                int riskScore = 0;
                var riskFactors = new List<string>();

                if (avgScore < 60)
                {
                    riskScore += 30;
                    riskFactors.Add("Low average score");
                }
                else if (avgScore < 70)
                {
                    riskScore += 15;
                    riskFactors.Add("Below average performance");
                }

                if (trend == "Declining")
                {
                    riskScore += 25;
                    riskFactors.Add("Declining performance trend");
                }

                if (recentScore < 50)
                {
                    riskScore += 20;
                    riskFactors.Add("Recent exam failure");
                }

                if (incidentCount > 2)
                {
                    riskScore += 15;
                    riskFactors.Add("Multiple integrity incidents");
                }

                var completionRate = studentSubmissions.Count / (double)submissions.Select(s => s.ExamId).Distinct().Count();
                if (completionRate < 0.8)
                {
                    riskScore += 10;
                    riskFactors.Add("Low exam completion rate");
                }

                if (riskScore >= 40)
                {
                    atRiskStudents.Add(new AtRiskStudent
                    {
                        StudentId = studentId,
                        StudentName = studentSubmissions.FirstOrDefault()?.StudentName ?? "Unknown",
                        RiskScore = riskScore,
                        RiskLevel = riskScore >= 60 ? "High" : "Medium",
                        RiskFactors = riskFactors,
                        AverageScore = avgScore,
                        Trend = trend,
                        RecommendedActions = GenerateRecommendations(riskFactors, avgScore)
                    });
                }
            }

            return atRiskStudents.OrderByDescending(s => s.RiskScore).ToList();
        }

        // Recommend optimal exam difficulty
        public DifficultyRecommendation RecommendExamDifficulty(
            List<ExamSubmission> historicalSubmissions)
        {
            if (!historicalSubmissions.Any())
            {
                return new DifficultyRecommendation
                {
                    RecommendedDifficulty = "Medium",
                    Reasoning = "No historical data available",
                    EasyQuestions = 5,
                    MediumQuestions = 10,
                    HardQuestions = 5
                };
            }

            var avgScore = historicalSubmissions.Average(s =>
                s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0);

            string difficulty;
            int easy, medium, hard;
            string reasoning;

            if (avgScore >= 85)
            {
                difficulty = "Hard";
                easy = 2;
                medium = 8;
                hard = 10;
                reasoning = "Class average is high. Increase difficulty to better differentiate top performers.";
            }
            else if (avgScore >= 70)
            {
                difficulty = "Medium";
                easy = 5;
                medium = 10;
                hard = 5;
                reasoning = "Class average is good. Maintain balanced difficulty distribution.";
            }
            else if (avgScore >= 60)
            {
                difficulty = "Medium-Easy";
                easy = 8;
                medium = 8;
                hard = 4;
                reasoning = "Class average is below target. Consider more easy questions to build confidence.";
            }
            else
            {
                difficulty = "Easy";
                easy = 12;
                medium = 6;
                hard = 2;
                reasoning = "Class average is low. Focus on fundamentals with easier questions.";
            }

            return new DifficultyRecommendation
            {
                RecommendedDifficulty = difficulty,
                Reasoning = reasoning,
                EasyQuestions = easy,
                MediumQuestions = medium,
                HardQuestions = hard,
                ClassAverage = avgScore
            };
        }

        // Identify learning gaps
        public List<LearningGap> IdentifyLearningGaps(
            List<ExamSubmission> submissions,
            PublishedExam exam)
        {
            var gaps = new List<LearningGap>();
            var questionGroups = exam.Contents.GroupBy(c => c.QuestionType ?? "General");

            foreach (var group in questionGroups)
            {
                var topic = group.Key;
                var topicQuestions = group.ToList();

                int totalAttempts = 0;
                int correctAttempts = 0;

                foreach (var question in topicQuestions)
                {
                    var responses = submissions
                        .SelectMany(s => s.Responses)
                        .Where(r => r.QuestionId == question.ContentId)
                        .ToList();

                    totalAttempts += responses.Count;
                    correctAttempts += responses.Count(r => r.IsCorrect);
                }

                if (totalAttempts > 0)
                {
                    var successRate = (correctAttempts * 100.0) / totalAttempts;

                    if (successRate < 60)
                    {
                        gaps.Add(new LearningGap
                        {
                            Topic = topic,
                            SuccessRate = successRate,
                            Severity = successRate < 40 ? "Critical" : "Moderate",
                            AffectedStudents = CalculateAffectedStudents(submissions, topicQuestions),
                            Recommendation = GenerateTopicRecommendation(topic, successRate)
                        });
                    }
                }
            }

            return gaps.OrderBy(g => g.SuccessRate).ToList();
        }

        private string CalculateTrend(List<double> scores)
        {
            if (scores.Count < 2) return "Stable";

            var recentScores = scores.TakeLast(3).ToList();
            if (recentScores.Count < 2) return "Stable";

            var firstHalf = recentScores.Take(recentScores.Count / 2).Average();
            var secondHalf = recentScores.Skip(recentScores.Count / 2).Average();

            if (secondHalf > firstHalf + 5)
                return "Improving";
            else if (secondHalf < firstHalf - 5)
                return "Declining";
            else
                return "Stable";
        }

        private double PredictNextScore(List<double> scores)
        {
            if (scores.Count < 2) return scores.FirstOrDefault();

            var n = scores.Count;
            var sumX = 0.0;
            var sumY = 0.0;
            var sumXY = 0.0;
            var sumX2 = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += scores[i];
                sumXY += i * scores[i];
                sumX2 += i * i;
            }

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            var intercept = (sumY - slope * sumX) / n;

            return Math.Max(0, Math.Min(100, intercept + slope * n));
        }

        private double CalculateConfidence(List<double> scores)
        {
            var stdDev = CalculateStandardDeviation(scores);
            return Math.Max(0, Math.Min(100, 100 - (stdDev * 2)));
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count == 0) return 0;
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private List<string> GenerateRecommendations(List<string> riskFactors, double avgScore)
        {
            var recommendations = new List<string>();

            if (riskFactors.Contains("Low average score"))
                recommendations.Add("Schedule 1-on-1 tutoring sessions");

            if (riskFactors.Contains("Declining performance trend"))
                recommendations.Add("Review recent study habits and time management");

            if (riskFactors.Contains("Multiple integrity incidents"))
                recommendations.Add("Discuss academic integrity expectations");

            if (avgScore < 60)
                recommendations.Add("Consider course withdrawal or audit option");

            recommendations.Add("Provide additional practice materials");
            recommendations.Add("Connect with academic advisor");

            return recommendations;
        }

        private string GenerateTopicRecommendation(string topic, double successRate)
        {
            if (successRate < 40)
                return $"Critical gap in {topic}. Consider dedicated review session and additional resources.";
            else if (successRate < 50)
                return $"Significant weakness in {topic}. Allocate more practice time.";
            else
                return $"Below-average performance in {topic}. Brief review recommended.";
        }

        private int CalculateAffectedStudents(List<ExamSubmission> submissions, List<ExamContent> questions)
        {
            var studentIds = new HashSet<string>();

            foreach (var submission in submissions)
            {
                var correctCount = submission.Responses
                    .Count(r => questions.Any(q => q.ContentId == r.QuestionId) && r.IsCorrect);

                var totalCount = questions.Count;
                var successRate = totalCount > 0 ? (correctCount * 100.0) / totalCount : 0;

                if (successRate < 60)
                    studentIds.Add(submission.StudentId);
            }

            return studentIds.Count;
        }
    }

    // Supporting classes
    public class PredictionResult
    {
        public double PredictedScore { get; set; }
        public double Confidence { get; set; }
        public string Trend { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public List<double> HistoricalScores { get; set; } = new();
    }

    public class AtRiskStudent
    {
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = "";
        public List<string> RiskFactors { get; set; } = new();
        public double AverageScore { get; set; }
        public string Trend { get; set; } = "";
        public List<string> RecommendedActions { get; set; } = new();
    }

    public class DifficultyRecommendation
    {
        public string RecommendedDifficulty { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public int EasyQuestions { get; set; }
        public int MediumQuestions { get; set; }
        public int HardQuestions { get; set; }
        public double ClassAverage { get; set; }
    }

    public class LearningGap
    {
        public string Topic { get; set; } = "";
        public double SuccessRate { get; set; }
        public string Severity { get; set; } = "";
        public int AffectedStudents { get; set; }
        public string Recommendation { get; set; } = "";
    }
}
