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

                var totalStudents = submissions.Count;
                var averageScore = submissions.Average(s => s.TotalPossiblePoints > 0 ? 
                    (s.TotalScore / s.TotalPossiblePoints) * 100 : 0);
                
                var passedSubmissions = submissions.Count(s => s.TotalPossiblePoints > 0 && 
                    (s.TotalScore / s.TotalPossiblePoints) * 100 >= 60);
                var passRate = totalStudents > 0 ? (passedSubmissions * 100.0) / totalStudents : 0;

                // Count completed submissions (those with EndTime)
                var completedSubmissions = submissions.Count(s => s.EndTime.HasValue);
                var completionRate = totalStudents > 0 ? (completedSubmissions * 100.0) / totalStudents : 0;

                return new ClassOverviewMetrics
                {
                    ClassAverage = averageScore,
                    PassRate = passRate,
                    CompletionRate = completionRate,
                    TotalStudents = totalStudents,
                    PassingStudents = passedSubmissions
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error calculating class overview: {ex.Message}");
                return new ClassOverviewMetrics();
            }
        }

        /// <summary>
        /// Analyze items/questions in an exam for quality metrics
        /// </summary>
        public async Task<List<ItemAnalysisResult>> AnalyzeExamItemsAsync(string examId)
        {
            try
            {
                var submissions = await _firestoreService.GetExamSubmissionsAsync(examId);
                var exam = await _firestoreService.GetPublishedExamAsync(examId);
                
                if (!submissions.Any() || exam?.Contents == null)
                {
                    return new List<ItemAnalysisResult>();
                }

                var itemResults = new List<ItemAnalysisResult>();

                foreach (var question in exam.Contents)
                {
                    var questionResponses = submissions
                        .SelectMany(s => s.Responses)
                        .Where(r => r.QuestionId == question.Id)
                        .ToList();

                    if (!questionResponses.Any()) continue;

                    var totalResponses = questionResponses.Count;
                    var correctResponses = questionResponses.Count(r => r.IsCorrect);
                    var difficultyPercent = totalResponses > 0 ? (correctResponses * 100.0) / totalResponses : 0;

                    // Calculate discrimination index (simplified)
                    var discrimination = CalculateDiscrimination(questionResponses, submissions);
                    
                    // Point-biserial correlation (simplified approximation)
                    var pointBiserial = CalculatePointBiserial(questionResponses, submissions);
                    
                    // No response rate
                    var noResponseCount = submissions.Count - totalResponses;
                    var noResponsePercent = submissions.Count > 0 ? (noResponseCount * 100.0) / submissions.Count : 0;

                    // Quality indicator based on discrimination and difficulty
                    var qualityIndicator = GetQualityIndicator(discrimination, difficultyPercent);

                    itemResults.Add(new ItemAnalysisResult
                    {
                        QuestionId = question.Id,
                        QuestionNumber = (itemResults.Count + 1).ToString(),
                        QuestionType = question.QuestionType,
                        DifficultyPercent = difficultyPercent,
                        Discrimination = discrimination,
                        PointBiserial = pointBiserial,
                        NoResponsePercent = noResponsePercent,
                        QualityIndicator = qualityIndicator
                    });
                }

                return itemResults.OrderBy(r => int.Parse(r.QuestionNumber)).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error analyzing exam items: {ex.Message}");
                return new List<ItemAnalysisResult>();
            }
        }

        private double CalculateDiscrimination(List<SubmissionResponse> questionResponses, List<ExamSubmission> allSubmissions)
        {
            // Simplified discrimination calculation
            try
            {
                // Get top 27% and bottom 27% performers
                var sortedSubmissions = allSubmissions
                    .OrderByDescending(s => s.TotalPossiblePoints > 0 ? (s.TotalScore / s.TotalPossiblePoints) * 100 : 0)
                    .ToList();
                
                var topCount = Math.Max(1, (int)(sortedSubmissions.Count * 0.27));
                var topPerformers = sortedSubmissions.Take(topCount).Select(s => s.Id).ToHashSet();
                var bottomPerformers = sortedSubmissions.TakeLast(topCount).Select(s => s.Id).ToHashSet();

                var topCorrect = questionResponses.Count(r => topPerformers.Contains(GetSubmissionIdFromResponse(r, allSubmissions)) && r.IsCorrect);
                var bottomCorrect = questionResponses.Count(r => bottomPerformers.Contains(GetSubmissionIdFromResponse(r, allSubmissions)) && r.IsCorrect);

                var discrimination = topCount > 0 ? ((double)(topCorrect - bottomCorrect)) / topCount : 0;
                return Math.Max(-1, Math.Min(1, discrimination)); // Clamp between -1 and 1
            }
            catch
            {
                return 0;
            }
        }

        private string GetSubmissionIdFromResponse(SubmissionResponse response, List<ExamSubmission> allSubmissions)
        {
            // Find the submission that contains this response
            var submission = allSubmissions.FirstOrDefault(s => s.Responses.Any(r => r.QuestionId == response.QuestionId));
            return submission?.Id ?? "";
        }

        private double CalculatePointBiserial(List<SubmissionResponse> questionResponses, List<ExamSubmission> allSubmissions)
        {
            // Simplified point-biserial correlation approximation
            try
            {
                var correctCount = questionResponses.Count(r => r.IsCorrect);
                var totalCount = questionResponses.Count;
                
                if (totalCount == 0) return 0;
                
                var proportion = (double)correctCount / totalCount;
                
                // Simplified calculation - in practice you'd need full statistical calculation
                return Math.Max(-1, Math.Min(1, (proportion - 0.5) * 2));
            }
            catch
            {
                return 0;
            }
        }

        private string GetQualityIndicator(double discrimination, double difficultyPercent)
        {
            if (discrimination >= 0.3 && difficultyPercent >= 20 && difficultyPercent <= 80)
                return "Excellent";
            else if (discrimination >= 0.2 && difficultyPercent >= 15 && difficultyPercent <= 85)
                return "Good";
            else if (discrimination >= 0.1 && difficultyPercent >= 10 && difficultyPercent <= 90)
                return "Fair";
            else
                return "Poor";
        }
    }

    // Data models for analytics
    public class ClassOverviewMetrics
    {
        public double ClassAverage { get; set; } = 0;
        public double PassRate { get; set; } = 0;
        public double CompletionRate { get; set; } = 0;
        public int TotalStudents { get; set; } = 0;
        public int PassingStudents { get; set; } = 0;
    }

    public class ItemAnalysisResult
    {
        public string QuestionId { get; set; } = "";
        public string QuestionNumber { get; set; } = "";
        public string QuestionType { get; set; } = "";
        public double DifficultyPercent { get; set; } = 0;
        public double Discrimination { get; set; } = 0;
        public double PointBiserial { get; set; } = 0;
        public double NoResponsePercent { get; set; } = 0;
        public string QualityIndicator { get; set; } = "";
    }
}