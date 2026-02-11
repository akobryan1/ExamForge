using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExamForge.Models;

namespace ExamForge.Services
{
    public class AnalyticsService
    {
        private readonly FirestoreService _firestoreService;

        public AnalyticsService()
        {
            _firestoreService = App.FirestoreService ?? throw new InvalidOperationException("Firestore service not initialized");
        }

        /// <summary>
        /// Calculate class overview metrics for an exam
        /// </summary>
        public async Task<ClassOverviewMetrics> CalculateClassOverviewAsync(string examId)
        {
            try
            {
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);
                if (!submissions.Any())
                {
                    return new ClassOverviewMetrics();
                }

                var scores = submissions.Select(s => (double)s.TotalScore / s.TotalPossiblePoints * 100).ToList();
                var completionRate = (double)submissions.Count / submissions.Count * 100; // This would need total enrolled students
                var passRate = scores.Count(s => s >= 60) / (double)scores.Count * 100; // Assuming 60% pass threshold

                return new ClassOverviewMetrics
                {
                    ClassAverage = scores.Average(),
                    PassRate = passRate,
                    CompletionRate = completionRate,
                    TotalStudents = submissions.Count,
                    PassingStudents = scores.Count(s => s >= 60),
                    Mean = scores.Average(),
                    Median = CalculateMedian(scores),
                    StandardDeviation = CalculateStandardDeviation(scores),
                    MinScore = scores.Min(),
                    MaxScore = scores.Max()
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to calculate class overview: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Analyze item difficulty and discrimination for exam questions
        /// </summary>
        public async Task<List<ItemAnalysisResult>> AnalyzeExamItemsAsync(string examId)
        {
            try
            {
                var exam = await _firestoreService.GetPublishedExamAsync(examId);
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);

                if (exam == null || !submissions.Any())
                {
                    return new List<ItemAnalysisResult>();
                }

                var results = new List<ItemAnalysisResult>();

                foreach (var question in exam.Contents)
                {
                    var responses = submissions
                        .SelectMany(s => s.Responses ?? new List<SubmissionResponse>())
                        .Where(r => r.QuestionId == question.Id)
                        .ToList();

                    if (!responses.Any()) continue;

                    var correctResponses = responses.Count(r => r.IsCorrect);
                    var totalResponses = responses.Count;
                    var difficultyPercent = (double)correctResponses / totalResponses * 100;

                    // Calculate discrimination index (simple version)
                    var discrimination = CalculateDiscrimination(responses, submissions);
                    
                    // Calculate point-biserial correlation
                    var pointBiserial = CalculatePointBiserial(responses, submissions);

                    // No response rate
                    var noResponseCount = submissions.Count - totalResponses;
                    var noResponsePercent = (double)noResponseCount / submissions.Count * 100;

                    results.Add(new ItemAnalysisResult
                    {
                        QuestionId = question.Id,
                        QuestionNumber = question.Id, // Assuming ID contains question number
                        QuestionType = question.QuestionType,
                        DifficultyPercent = difficultyPercent,
                        Discrimination = discrimination,
                        PointBiserial = pointBiserial,
                        NoResponsePercent = noResponsePercent,
                        QualityIndicator = DetermineQualityIndicator(difficultyPercent, discrimination)
                    });
                }

                return results.OrderBy(r => r.QuestionNumber).ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to analyze exam items: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get detailed performance data for a specific student
        /// </summary>
        public async Task<StudentDetailedPerformance> GetStudentPerformanceAsync(string examId, string studentId)
        {
            try
            {
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);
                var studentSubmission = submissions.FirstOrDefault(s => s.StudentId == studentId);

                if (studentSubmission == null)
                {
                    throw new InvalidOperationException("Student submission not found");
                }

                var allScores = submissions.Select(s => (double)s.TotalScore / s.TotalPossiblePoints * 100).OrderByDescending(s => s).ToList();
                var studentScore = (double)studentSubmission.TotalScore / studentSubmission.TotalPossiblePoints * 100;
                var rank = allScores.IndexOf(studentScore) + 1;

                var masteryBreakdown = CalculateMasteryByTopic(studentSubmission);
                var missedItems = GetMissedItems(studentSubmission);

                return new StudentDetailedPerformance
                {
                    StudentId = studentId,
                    StudentName = studentSubmission.StudentName,
                    TotalScore = (int)studentSubmission.TotalScore,
                    Percentage = studentScore,
                    Rank = rank,
                    TimeTaken = studentSubmission.EndTime.HasValue && studentSubmission.StartTime.HasValue 
                        ? studentSubmission.EndTime.Value - studentSubmission.StartTime.Value 
                        : TimeSpan.Zero,
                    MasteryBreakdown = masteryBreakdown,
                    MissedItems = missedItems
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get student performance: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get integrity incidents for analysis
        /// </summary>
        public async Task<List<IntegrityIncident>> GetIntegrityIncidentsAsync(string examId)
        {
            try
            {
                return await _firestoreService.GetIntegrityIncidentsAsync(examId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get integrity incidents: {ex.Message}", ex);
            }
        }

        #region Helper Methods

        private double CalculateMedian(List<double> scores)
        {
            var sortedScores = scores.OrderBy(s => s).ToList();
            var count = sortedScores.Count;
            
            if (count % 2 == 0)
            {
                return (sortedScores[count / 2 - 1] + sortedScores[count / 2]) / 2;
            }
            else
            {
                return sortedScores[count / 2];
            }
        }

        private double CalculateStandardDeviation(List<double> scores)
        {
            var mean = scores.Average();
            var squaredDifferences = scores.Select(s => Math.Pow(s - mean, 2));
            var variance = squaredDifferences.Average();
            return Math.Sqrt(variance);
        }

        private double CalculateDiscrimination(List<SubmissionResponse> responses, List<ExamSubmission> allSubmissions)
        {
            // Simplified discrimination calculation
            // In a real implementation, you'd compare high vs low performers
            var correctRate = responses.Count(r => r.IsCorrect) / (double)responses.Count;
            
            // This is a placeholder - real discrimination would compare upper and lower groups
            return Math.Min(correctRate * 2 - 1, 1.0);
        }

        private double CalculatePointBiserial(List<SubmissionResponse> responses, List<ExamSubmission> allSubmissions)
        {
            // Simplified point-biserial correlation
            // In a real implementation, you'd calculate the actual correlation between item scores and total scores
            var correctRate = responses.Count(r => r.IsCorrect) / (double)responses.Count;
            return correctRate > 0.5 ? 0.4 + (correctRate - 0.5) : 0.2 + correctRate * 0.4;
        }

        private string DetermineQualityIndicator(double difficulty, double discrimination)
        {
            if (difficulty < 30 || difficulty > 90)
            {
                return "Review";
            }
            
            if (discrimination > 0.4)
            {
                return "Excellent";
            }
            else if (discrimination > 0.3)
            {
                return "Good";
            }
            else
            {
                return "Review";
            }
        }

        private List<TopicMastery> CalculateMasteryByTopic(ExamSubmission submission)
        {
            // This would group responses by topic/tag and calculate mastery
            // For now, returning sample data
            return new List<TopicMastery>
            {
                new TopicMastery { Topic = "Algebra", Percentage = 85 },
                new TopicMastery { Topic = "Geometry", Percentage = 75 },
                new TopicMastery { Topic = "Statistics", Percentage = 60 }
            };
        }

        private List<MissedItemDetail> GetMissedItems(ExamSubmission submission)
        {
            return submission.Responses?
                .Where(r => !r.IsCorrect)
                .Select(r => new MissedItemDetail
                {
                    QuestionId = r.QuestionId,
                    QuestionNumber = r.QuestionNumber,
                    StudentAnswer = r.Answer ?? "No answer",
                    PointsLost = (int)(r.PointsPossible - r.PointsEarned)
                })
                .ToList() ?? new List<MissedItemDetail>();
        }

        #endregion
    }

    #region Analytics Result Models

    public class ClassOverviewMetrics
    {
        public double ClassAverage { get; set; }
        public double PassRate { get; set; }
        public double CompletionRate { get; set; }
        public int TotalStudents { get; set; }
        public int PassingStudents { get; set; }
        public double Mean { get; set; }
        public double Median { get; set; }
        public double StandardDeviation { get; set; }
        public double MinScore { get; set; }
        public double MaxScore { get; set; }
    }

    public class ItemAnalysisResult
    {
        public string QuestionId { get; set; } = "";
        public string QuestionNumber { get; set; } = "";
        public string QuestionType { get; set; } = "";
        public double DifficultyPercent { get; set; }
        public double Discrimination { get; set; }
        public double PointBiserial { get; set; }
        public double NoResponsePercent { get; set; }
        public string QualityIndicator { get; set; } = "";
    }

    public class StudentDetailedPerformance
    {
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public int TotalScore { get; set; }
        public double Percentage { get; set; }
        public int Rank { get; set; }
        public TimeSpan TimeTaken { get; set; }
        public List<TopicMastery> MasteryBreakdown { get; set; } = new();
        public List<MissedItemDetail> MissedItems { get; set; } = new();
    }

    public class TopicMastery
    {
        public string Topic { get; set; } = "";
        public double Percentage { get; set; }
    }

    public class MissedItemDetail
    {
        public string QuestionId { get; set; } = "";
        public int QuestionNumber { get; set; }
        public string StudentAnswer { get; set; } = "";
        public int PointsLost { get; set; }
    }

    #endregion
}