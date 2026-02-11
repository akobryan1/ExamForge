using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace ExamForge.Models
{
    [FirestoreData]
    public class PublishedExam
    {
        [FirestoreProperty]
        public string Id { get; set; } = "";
        
        [FirestoreProperty]
        public string Title { get; set; } = "";
        
        [FirestoreProperty]
        public DateTime StartTime { get; set; }
        
        [FirestoreProperty]
        public DateTime EndTime { get; set; }
        
        [FirestoreProperty]
        public int ExamDuration { get; set; } // in minutes
        
        [FirestoreProperty]
        public DateTime PublishedDate { get; set; }
        
        [FirestoreProperty]
        public string CreatedBy { get; set; } = "";
        
        [FirestoreProperty]
        public string ExamUrl { get; set; } = "";
        
        [FirestoreProperty]
        public List<ExamStructure> Structures { get; set; } = new();
        
        [FirestoreProperty]
        public List<ExamContent> Contents { get; set; } = new();
        
        [FirestoreProperty]
        public int TimesUsed { get; set; }
        
        [FirestoreProperty]
        public double? AverageDifficulty { get; set; }
        
        // Changed to int to match ViewModel
        [FirestoreProperty]
        public int PassingScorePercentage { get; set; } = 60;
        
        [FirestoreProperty]
        public double PassRate { get; set; }
        
        // LoginConfig is a string representing JSON or config state
        [FirestoreProperty]
        public string LoginConfig { get; set; } = "";
        
        // LifecycleStatus is a string, not an object
        [FirestoreProperty]
        public string LifecycleStatus { get; set; } = "Draft";
        
        [FirestoreProperty]
        public string Status { get; set; } = "Draft";
    }

    public class PublishedExamViewModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }        
        public int ExamDuration { get; set; }
        public DateTime PublishedDate { get; set; }
        public string CreatedBy { get; set; } = "";
        public string ExamUrl { get; set; } = "";
        public string Status { get; set; } = "";
        public string ScheduleText { get; set; } = "";
        public string DurationText { get; set; } = "";
        public string CreatedByText { get; set; } = "";
        public string StatusColor { get; set; } = "";
        public int TimesUsed { get; set; }
        public double AverageDifficulty { get; set; }
        public int PassingScorePercentage { get; set; } = 60;
        public double PassRate { get; set; }
        public string LoginConfig { get; set; } = "";
        public string LifecycleStatus { get; set; } = "";
    }

    public class StudentRosterItem
    {
        public string StudentId { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string ConnectionStatus { get; set; } = "";
        public string ProgressPercent { get; set; } = "";
        public string TimeRemaining { get; set; } = "";
        public string FlagCount { get; set; } = "";
    }

    [FirestoreData]
    public class ExamStructure
    {
        [FirestoreProperty]
        public string SectionName { get; set; } = "";
        
        [FirestoreProperty]
        public string Description { get; set; } = "";
        
        [FirestoreProperty]
        public int Points { get; set; }
        
        [FirestoreProperty]
        public int QuestionCount { get; set; }
        
        [FirestoreProperty]
        public bool IsComplete { get; set; }
    }

    [FirestoreData]
    public class ExamContent
    {
        [FirestoreProperty]
        public string ContentId { get; set; } = "";

        [FirestoreProperty]
        public string ExamId { get; set; } = "";

        [FirestoreProperty]
        public string Question { get; set; } = "";

        [FirestoreProperty]
        public string Answer { get; set; } = "";

        [FirestoreProperty]
        public string Explanation { get; set; } = "";

        [FirestoreProperty]
        public string MediaUrl { get; set; } = "";

        [FirestoreProperty]
        public bool IsComplete { get; set; }

        // --- Non-Firestore properties (aliases for compatibility) ---
        public string Id
        {
            get => ContentId;
            set => ContentId = value;
        }

        public string QuestionText
        {
            get => Question;
            set => Question = value;
        }

        [FirestoreProperty]
        public string QuestionType { get; set; } = "MCQ";

        [FirestoreProperty]
        public int Points { get; set; } = 1;

        [FirestoreProperty]
        public List<string> Options { get; set; } = new();

        public string CorrectAnswer
        {
            get => Answer;
            set => Answer = value;
        }
    }

}