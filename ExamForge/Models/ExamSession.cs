using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace ExamForge.Models;

/// <summary>
/// Represents a live exam session with real-time participant tracking
/// </summary>
[FirestoreData]
public class ExamSession
{
    [FirestoreProperty]
    public string Id { get; set; } = "";

    [FirestoreProperty]
    public string ExamId { get; set; } = "";

    [FirestoreProperty]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public DateTime? EndedAt { get; set; }

    [FirestoreProperty]
    public string Status { get; set; } = "Running"; // Running, Paused, Ended

    /// <summary>
    /// Dictionary: StudentId -> Connection state
    /// </summary>
    [FirestoreProperty]
    public Dictionary<string, StudentSessionState> Participants { get; set; } = new();

    [FirestoreProperty]
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public int TotalParticipants { get; set; }

    [FirestoreProperty]
    public int OnlineCount { get; set; }
}

[FirestoreData]
public class StudentSessionState
{
    [FirestoreProperty]
    public string StudentId { get; set; } = "";

    [FirestoreProperty]
    public string StudentName { get; set; } = "";

    [FirestoreProperty]
    public string ConnectionStatus { get; set; } = "Online"; // Online, Offline, Disconnected

    [FirestoreProperty]
    public int ProgressPercent { get; set; }

    [FirestoreProperty]
    public int QuestionsAnswered { get; set; }

    [FirestoreProperty]
    public int TimeRemaining { get; set; } // seconds

    [FirestoreProperty]
    public int FlagCount { get; set; }

    [FirestoreProperty]
    public DateTime LastHeartbeat { get; set; }

    [FirestoreProperty]
    public string IpAddress { get; set; } = "";
}