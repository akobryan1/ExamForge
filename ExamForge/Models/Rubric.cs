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

    /// <summary>
    /// Creates a well-defined default rubric for evaluating and grading essays.
    /// Professors can use this as a starting point and customize it.
    /// </summary>
    public static Rubric CreateDefaultEssayRubric()
    {
        return new Rubric
        {
            Id = Guid.NewGuid().ToString(),
            Title = "Standard Essay Evaluation Rubric",
            TotalPoints = 100,
            Criteria = new List<RubricCriterion>
            {
                new RubricCriterion
                {
                    Name = "Thesis & Argument",
                    Description = "Clarity, strength, and originality of the central thesis and supporting arguments.",
                    MaxPoints = 25,
                    Levels = new List<string>
                    {
                        "Excellent (23-25): Thesis is clear, original, and compelling. Arguments are well-developed with strong logical reasoning.",
                        "Good (18-22): Thesis is clear and supported. Arguments are logical with minor gaps.",
                        "Fair (12-17): Thesis is present but vague. Arguments lack depth or coherence.",
                        "Poor (0-11): Thesis is missing or unclear. Arguments are weak or absent."
                    }
                },
                new RubricCriterion
                {
                    Name = "Evidence & Support",
                    Description = "Use of relevant evidence, examples, and references to support claims.",
                    MaxPoints = 25,
                    Levels = new List<string>
                    {
                        "Excellent (23-25): Evidence is abundant, relevant, and well-integrated. Sources are credible and properly cited.",
                        "Good (18-22): Evidence is adequate and mostly relevant. Some sources may lack proper citation.",
                        "Fair (12-17): Evidence is limited or partially relevant. Citations are inconsistent.",
                        "Poor (0-11): Little or no evidence provided. Claims are unsupported."
                    }
                },
                new RubricCriterion
                {
                    Name = "Organization & Structure",
                    Description = "Logical flow, paragraph structure, transitions, and overall coherence.",
                    MaxPoints = 20,
                    Levels = new List<string>
                    {
                        "Excellent (18-20): Well-organized with clear introduction, body, and conclusion. Transitions are smooth and logical.",
                        "Good (14-17): Generally organized with identifiable structure. Transitions are present but occasionally awkward.",
                        "Fair (10-13): Some organizational structure but lacks clear flow. Transitions are weak or missing.",
                        "Poor (0-9): No clear organization. Ideas are scattered without logical progression."
                    }
                },
                new RubricCriterion
                {
                    Name = "Language & Style",
                    Description = "Grammar, vocabulary, sentence variety, tone, and academic writing conventions.",
                    MaxPoints = 15,
                    Levels = new List<string>
                    {
                        "Excellent (14-15): Writing is polished with varied sentence structure. Tone is appropriate and engaging.",
                        "Good (11-13): Writing is generally clear with minor grammatical errors. Tone is appropriate.",
                        "Fair (7-10): Noticeable grammatical errors that occasionally impede clarity. Limited vocabulary.",
                        "Poor (0-6): Frequent errors that significantly impede understanding. Inappropriate tone or style."
                    }
                },
                new RubricCriterion
                {
                    Name = "Critical Thinking & Depth",
                    Description = "Depth of analysis, consideration of counterarguments, and original insight.",
                    MaxPoints = 15,
                    Levels = new List<string>
                    {
                        "Excellent (14-15): Demonstrates insightful analysis with consideration of multiple perspectives and counterarguments.",
                        "Good (11-13): Shows good analytical thinking with some consideration of alternative viewpoints.",
                        "Fair (7-10): Analysis is surface-level. Limited engagement with complexity of the topic.",
                        "Poor (0-6): No meaningful analysis. Merely restates facts without interpretation."
                    }
                }
            }
        };
    }

    /// <summary>
    /// Formats the rubric into a plain-text representation for LLM prompts.
    /// </summary>
    public string ToPromptText()
    {
        var lines = new List<string>
        {
            $"Rubric: {Title}",
            $"Total Points: {TotalPoints}",
            ""
        };

        foreach (var criterion in Criteria)
        {
            lines.Add($"Criterion: {criterion.Name} (Max {criterion.MaxPoints} points)");
            lines.Add($"Description: {criterion.Description}");
            lines.Add("Scoring Levels:");
            foreach (var level in criterion.Levels)
            {
                lines.Add($"  - {level}");
            }
            lines.Add("");
        }

        return string.Join("\n", lines);
    }
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

/// <summary>
/// Holds the result of an LLM-based essay evaluation.
/// </summary>
public class EssayEvaluationResult
{
    public int TotalScore { get; set; }
    public int MaxScore { get; set; }
    public string LetterGrade { get; set; } = "";
    public List<CriterionScore> CriterionScores { get; set; } = new();
    public string OverallFeedback { get; set; } = "";
    public List<string> Strengths { get; set; } = new();
    public List<string> AreasForImprovement { get; set; } = new();
}

public class CriterionScore
{
    public string CriterionName { get; set; } = "";
    public int Score { get; set; }
    public int MaxPoints { get; set; }
    public string Level { get; set; } = "";
    public string Feedback { get; set; } = "";
}