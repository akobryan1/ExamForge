using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;
using ExamForge; // for AntiCheatState

namespace ExamForge.Models;

public class ExamReviewData
{
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "General"; // ✅ Added Subject field
    public string OwnerUserId { get; set; } = "";
    public List<ExamStructure> Structures { get; set; } = new();
    public List<ExamContent> Contents { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExamDuration { get; set; }
    public LoginConfigState? LoginConfig { get; set; }
    // Optional anti-cheat configuration saved from the builder
    public AntiCheatState? AntiCheat { get; set; }
}

[FirestoreData]
public class LoginConfigState
{
    [FirestoreProperty]
    public bool IsGoogleSignIn { get; set; }
    
    [FirestoreProperty]
    public bool RequireFullName { get; set; }
    
    [FirestoreProperty]
    public bool RequireYearSection { get; set; }
    
    [FirestoreProperty]
    public bool RequireStudentNumber { get; set; }
}

[FirestoreData]
public class SubmissionResponse
{
    [FirestoreProperty]
    public string QuestionId { get; set; } = "";

    [FirestoreProperty]
    public int QuestionNumber { get; set; }

    [FirestoreProperty]
    public string Answer { get; set; } = "";

    [FirestoreProperty]
    public double PointsEarned { get; set; }

    [FirestoreProperty]
    public double PointsPossible { get; set; }

    [FirestoreProperty]
    public bool IsCorrect { get; set; }
}

[FirestoreData]
public class ExamSubmission
{
    [FirestoreProperty]
    public string Id { get; set; } = "";

    [FirestoreProperty]
    public string ExamId { get; set; } = "";

    [FirestoreProperty]
    public string StudentName { get; set; } = "";

    [FirestoreProperty]
    public string StudentEmail { get; set; } = "";

    [FirestoreProperty]
    public string StudentId { get; set; } = "";

    [FirestoreProperty]
    public string YearSection { get; set; } = "";

    [FirestoreProperty]
    public DateTime SubmittedAt { get; set; }

    // ✅ REAL responses, not a dictionary
    [FirestoreProperty]
    public List<SubmissionResponse> Responses { get; set; } = new();

    [FirestoreProperty]
    public double TotalScore { get; set; }

    [FirestoreProperty]
    public double TotalPossiblePoints { get; set; }

    [FirestoreProperty]
    public double RawTotalScore { get; set; }

    [FirestoreProperty]
    public double DeductedPoints { get; set; }

    [FirestoreProperty]
    public string Status { get; set; } = "Submitted";

    [FirestoreProperty]
    public Dictionary<string, EssayGradeRecord> EssayGrades { get; set; } = new();

    // ✅ Nullable because your logic treats them as optional
    [FirestoreProperty]
    public DateTime? StartTime { get; set; }

    [FirestoreProperty]
    public DateTime? EndTime { get; set; }
}