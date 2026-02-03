using System;
using Google.Cloud.Firestore;

namespace ExamForge.Models;

/// <summary>
/// Immutable audit log of session events (integrity violations, teacher actions)
/// </summary>
[FirestoreData]
public class SessionEvent
{
    [FirestoreProperty]
    public string Id { get; set; } = "";

    [FirestoreProperty]
    public string SessionId { get; set; } = "";

    [FirestoreProperty]
    public string ExamId { get; set; } = "";

    [FirestoreProperty]
    public string StudentId { get; set; } = "";

    [FirestoreProperty]
    public string StudentName { get; set; } = "";

    [FirestoreProperty]
    public string EventType { get; set; } = ""; 
    // Types: tab_switch, focus_loss, reconnect, disconnect, 
    //        teacher_extend_time, teacher_pause, teacher_message, 
    //        teacher_invalidate, multiple_login

    [FirestoreProperty]
    public string Severity { get; set; } = "Info"; // Info, Warning, Critical

    [FirestoreProperty]
    public string Details { get; set; } = "";

    [FirestoreProperty]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public string IpAddress { get; set; } = "";

    [FirestoreProperty]
    public string UserAgent { get; set; } = "";
}