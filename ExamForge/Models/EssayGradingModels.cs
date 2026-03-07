using Google.Cloud.Firestore;
using System;
using System.Collections.Generic;

namespace ExamForge.Models;

[FirestoreData]
public class EssayGradeRecord
{
    [FirestoreProperty]
    public string QuestionId { get; set; } = "";

    [FirestoreProperty]
    public int QuestionNumber { get; set; }

    [FirestoreProperty]
    public string StudentAnswer { get; set; } = "";

    [FirestoreProperty]
    public double AiScore { get; set; }

    [FirestoreProperty]
    public double MaxScore { get; set; }

    [FirestoreProperty]
    public double FinalScore { get; set; }

    [FirestoreProperty]
    public double ConfidencePercent { get; set; }

    [FirestoreProperty]
    public bool FlagForReview { get; set; }

    [FirestoreProperty]
    public string Justification { get; set; } = "";

    [FirestoreProperty]
    public string InstructorAdjustmentNote { get; set; } = "";

    [FirestoreProperty]
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public List<EssayRubricScoreRow> RubricBreakdown { get; set; } = new();
}

[FirestoreData]
public class EssayRubricScoreRow
{
    [FirestoreProperty]
    public string Criterion { get; set; } = "";

    [FirestoreProperty]
    public double Weight { get; set; }

    [FirestoreProperty]
    public double MaxPoints { get; set; }

    [FirestoreProperty]
    public double PointsAwarded { get; set; }

    [FirestoreProperty]
    public string Reason { get; set; } = "";
}

public class EssayCheckerQueueItem
{
    public string SubmissionId { get; set; } = "";
    public string ExamId { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public string Subject { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string StudentId { get; set; } = "";
    public int QuestionNumber { get; set; }
    public string QuestionId { get; set; } = "";
    public string EssayQuestion { get; set; } = "";
    public string EssayAnswer { get; set; } = "";
    public string RubricText { get; set; } = "";
    public string ModelAnswer { get; set; } = "";
    public string KeyPoints { get; set; } = "";
    public double MaxPoints { get; set; }
    public double ThesisWeight { get; set; } = 33;
    public double EvidenceWeight { get; set; } = 34;
    public double ClarityWeight { get; set; } = 33;
}

public class EssayAutoGradeResult
{
    public double AiScore { get; set; }
    public double MaxScore { get; set; }
    public double ConfidencePercent { get; set; }
    public bool FlagForReview { get; set; }
    public string Justification { get; set; } = "";
    public List<EssayRubricScoreRow> RubricBreakdown { get; set; } = new();
    public bool UsedDeepSeek { get; set; }
    public string ProviderStatus { get; set; } = "";
}
