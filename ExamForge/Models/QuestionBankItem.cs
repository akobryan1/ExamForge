using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace ExamForge.Models;

[FirestoreData]
public class QuestionBankItem
{
    [FirestoreProperty]
    public string Id { get; set; } = "";

    [FirestoreProperty]
    public string QuestionText { get; set; } = "";

    [FirestoreProperty]
    public string QuestionType { get; set; } = ""; // MultipleChoice, Essay, TrueFalse, ShortAnswer

    [FirestoreProperty]
    public List<string> Options { get; set; } = new();

    [FirestoreProperty]
    public string CorrectAnswer { get; set; } = "";

    [FirestoreProperty]
    public List<string> Tags { get; set; } = new(); // e.g., ["biology", "cell-structure", "grade-10"]

    [FirestoreProperty]
    public string Subject { get; set; } = "";

    [FirestoreProperty]
    public string Difficulty { get; set; } = "Medium"; // Easy, Medium, Hard

    [FirestoreProperty]
    public int Points { get; set; } = 1;

    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public string CreatedBy { get; set; } = "";

    [FirestoreProperty]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public List<string> VersionHistory { get; set; } = new(); // List of version IDs

    [FirestoreProperty]
    public string PublishedVersionId { get; set; } = ""; // Immutable version used in exams

    [FirestoreProperty]
    public string Status { get; set; } = "Draft"; // Draft, Published, Archived

    // Analytics (calculated from usage)
    [FirestoreProperty]
    public int TimesUsed { get; set; }

    [FirestoreProperty]
    public double AverageScore { get; set; }

    [FirestoreProperty]
    public double Discrimination { get; set; } // Item discrimination index
}