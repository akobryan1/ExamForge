using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace ExamForge.Models;

[FirestoreData]
public class Rubric
{
    [FirestoreProperty]
    public string Id { get; set; } = "";

    [FirestoreProperty]
    public string QuestionId { get; set; } = "";

    [FirestoreProperty]
    public string Title { get; set; } = "";

    [FirestoreProperty]
    public List<RubricCriterion> Criteria { get; set; } = new();

    [FirestoreProperty]
    public int TotalPoints { get; set; }
}

[FirestoreData]
public class RubricCriterion
{
    [FirestoreProperty]
    public string Name { get; set; } = "";

    [FirestoreProperty]
    public string Description { get; set; } = "";

    [FirestoreProperty]
    public int MaxPoints { get; set; }

    [FirestoreProperty]
    public List<string> Levels { get; set; } = new(); // e.g., ["Excellent", "Good", "Fair", "Poor"]
}