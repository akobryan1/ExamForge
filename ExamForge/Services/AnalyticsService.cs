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
                var exam = await _firestoreService.GetPublishedExamAsync(examId);

                if (!submissions.Any())
                {
                    return new ClassOverviewMetrics();
                }

                var totalStudents = submissions.Count;
                var passingThreshold = exam?.PassingScorePercentage > 0 ? exam.PassingScorePercentage : 60;

                var averageScore = submissions.Average(s => s.TotalPossiblePoints > 0 ? 
                    (s.TotalScore / s.TotalPossiblePoints) * 100.0 : 0.0);
                
                var passedSubmissions = submissions.Count(s => s.TotalPossiblePoints > 0 && 
                    (s.TotalScore / s.TotalPossiblePoints) * 100.0 >= passingThreshold);
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

}