using Microsoft.AspNetCore.SignalR;
using Google.Cloud.Firestore;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace ExamForge.SignalRServer.Hubs;

public class SessionHub : Hub
{
    private static readonly object _fsLock = new();
    private static FirestoreDb? _firestoreDb;

    private static FirestoreDb GetFirestoreDb()
    {
        if (_firestoreDb != null) return _firestoreDb;
        lock (_fsLock)
        {
            if (_firestoreDb != null) return _firestoreDb;

            var projectId = Environment.GetEnvironmentVariable("FIRESTORE_PROJECT_ID") ?? "examforge-201e8";
            var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "firebase-adminsdk.json";
            try
            {
                _firestoreDb = FirestoreDb.Create(projectId);
            }
            catch (Exception ex)
            {
                // Swallow - Firestore may not be configured in local dev
                Console.WriteLine($"Failed to initialize Firestore in SessionHub: {ex.Message}");
                _firestoreDb = null;
            }

            return _firestoreDb;
        }
    }

    private async Task LogSessionEventAsync(string sessionId, string studentId, string studentName, string eventType, string details, string severity = "Info")
    {
        try
        {
            var db = GetFirestoreDb();
            if (db == null) return;

            var docRef = db.Collection("session_events").Document();
            var payload = new Dictionary<string, object>
            {
                ["SessionId"] = sessionId,
                ["StudentId"] = studentId,
                ["StudentName"] = studentName,
                ["EventType"] = eventType,
                ["Details"] = details,
                ["Severity"] = severity,
                ["Timestamp"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["IpAddress"] = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "",
                ["ConnectionId"] = Context.ConnectionId
            };

            await docRef.SetAsync(payload);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write session event: {ex.Message}");
        }
    }
    public async Task JoinSession(string sessionId, string studentId, string studentName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        await Clients.Group($"monitor_{sessionId}").SendAsync("StudentJoined", new
        {
            StudentId = studentId,
            StudentName = studentName,
            ConnectionId = Context.ConnectionId,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task SendHeartbeat(string sessionId, string studentId, object payload)
    {
        await Clients.Group($"monitor_{sessionId}").SendAsync("StudentHeartbeat", new
        {
            StudentId = studentId,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task ReportEvent(string sessionId, string studentId, string studentName, string eventType, string details)
    {
        var severity = eventType switch
        {
            "multiple_login" => "Critical",
            "tab_switch" => "Warning",
            "disconnect" => "Warning",
            _ => "Info"
        };

        await Clients.Group($"monitor_{sessionId}").SendAsync("IntegrityEvent", new
        {
            StudentId = studentId,
            StudentName = studentName,
            EventType = eventType,
            Severity = severity,
            Details = details,
            Timestamp = DateTime.UtcNow
        });

        // Log to Firestore for auditing
        _ = LogSessionEventAsync(sessionId, studentId, studentName, eventType, details, severity);
    }

    public async Task MonitorSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"monitor_{sessionId}");
    }

    public async Task SendCommand(string sessionId, string studentId, string command, object data)
    {
        // Broadcast to the session; clients can filter by studentId
        await Clients.Group(sessionId).SendAsync("ReceiveCommand", new
        {
            StudentId = studentId,
            Command = command,
            Data = data,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastMessage(string sessionId, string message)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveMessage", new
        {
            Message = message,
            From = "Teacher",
            Timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Optionally notify monitors of disconnects
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Save session progress to Firestore incident_reports collection
    /// </summary>
    public async Task SaveSessionProgress(string examId, object sessionData, string studentId, string studentName, string studentEmail)
    {
        try
        {
            // For now, we'll use a simplified approach - store with examId as key
            // In production, you should implement proper user ID lookup
            var db = GetFirestoreDb();
            if (db == null)
            {
                Console.WriteLine("? Firestore not initialized, cannot save session");
                return;
            }

            // Save to a temporary collection that can be queried by examId and studentId
            var collectionPath = $"temp_sessions/{examId}/students";
            var docRef = db.Collection(collectionPath).Document(studentId);
            
            var sessionDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                System.Text.Json.JsonSerializer.Serialize(sessionData)
            ) ?? new Dictionary<string, object>();

            var reportData = new Dictionary<string, object>
            {
                ["ExamId"] = examId,
                ["StudentId"] = studentId,
                ["StudentName"] = studentName,
                ["StudentEmail"] = studentEmail,
                ["SessionData"] = sessionDict,
                ["EventType"] = "session_saved",
                ["Timestamp"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["Status"] = "active"
            };

            await docRef.SetAsync(reportData);
            Console.WriteLine($"? Session saved for student {studentName} in exam {examId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// Check for active session (cross-device support)
    /// </summary>
    public async Task<object?> CheckForActiveSession(string examId, string studentId, string? studentEmail)
    {
        try
        {
            var db = GetFirestoreDb();
            if (db == null) return null;

            var collectionPath = $"temp_sessions/{examId}/students";
            var docRef = db.Collection(collectionPath).Document(studentId);
            
            var snapshot = await docRef.GetSnapshotAsync();
            
            if (!snapshot.Exists)
            {
                Console.WriteLine($"No saved session found for student {studentId}");
                return null;
            }

            var data = snapshot.ToDictionary();
            Console.WriteLine($"? Found saved session for student {studentId}");
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Failed to check for active session: {ex.Message}");
            return null;
        }
    }
}
