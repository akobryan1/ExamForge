using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace ExamForge.Models;

public class ExamReviewData
{
    public string Title { get; set; } = "";
    public List<ExamStructure> Structures { get; set; } = new();
    public List<ExamContent> Contents { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExamDuration { get; set; }
    public LoginConfigState? LoginConfig { get; set; }
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
    
    [FirestoreProperty]
    public Dictionary<string, string> Answers { get; set; } = new();
    
    [FirestoreProperty]
    public int TotalScore { get; set; }
    
    [FirestoreProperty]
    public int MaxScore { get; set; }
    
    [FirestoreProperty]
    public string Status { get; set; } = "Submitted";
}