using Microsoft.AspNetCore.SignalR;
using Google.Cloud.Firestore;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System;

namespace ExamForge.SignalRServer.Hubs;

public class SessionHub : Hub
{
    private static readonly object _fsLock = new();
    private static FirestoreDb? _firestoreDb;
    private static readonly ConcurrentDictionary<string, string> _examOwnerCache = new();

    private const string UsersRoot = "examforge_users";
    private const string PublishedExamsCollection = "published_exams";
    private const string SessionEventsCollection = "session_events";
    private const string IncidentReportsCollection = "incident_reports";
    private const string ExamSessionsCollection = "exam_sessions";

    private static FirestoreDb GetFirestoreDb()
    {
        if (_firestoreDb != null) return _firestoreDb;
        lock (_fsLock)
        {
            if (_firestoreDb != null) return _firestoreDb;

            var projectId = Environment.GetEnvironmentVariable("FIRESTORE_PROJECT_ID") ?? "examforge-201e8";
            
            try
            {
                // Check if credentials are in environment variable (Render deployment)
                var credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS_JSON");
                
                if (!string.IsNullOrEmpty(credentialsJson))
                {
                    // Use credentials from environment variable
                    var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credentialsJson);
                    var builder = new FirestoreDbBuilder
                    {
                        ProjectId = projectId,
                        Credential = credential
                    };
                    _firestoreDb = builder.Build();
                    Console.WriteLine("? Firestore initialized from environment credentials");
                }
                else
                {
                    // Fallback to file-based credentials (local development)
                    var credentialPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS") ?? "firebase-adminsdk.json";
                    _firestoreDb = FirestoreDb.Create(projectId);
                    Console.WriteLine("? Firestore initialized from file");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Failed to initialize Firestore: {ex.Message}");
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

            var ownerUserId = await ResolveExamOwnerUserIdAsync(sessionId);
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                Console.WriteLine($"[SessionHub] Unable to resolve owner path for exam {sessionId}; skipping session event log.");
                return;
            }

            var docRef = db.Collection(UsersRoot)
                .Document(ownerUserId)
                .Collection(SessionEventsCollection)
                .Document();
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

    private async Task<string?> ResolveExamOwnerUserIdAsync(string examId)
    {
        if (string.IsNullOrWhiteSpace(examId)) return null;

        if (!_examOwnerCache.TryGetValue(examId, out var ownerUserId))
        {
            var db = GetFirestoreDb();
            if (db == null) return null;

            var examSnapshot = await db.CollectionGroup(PublishedExamsCollection)
                .WhereEqualTo(FieldPath.DocumentId, examId)
                .Limit(1)
                .GetSnapshotAsync();

            var examDoc = examSnapshot.Documents.FirstOrDefault();
            if (examDoc == null)
            {
                Console.WriteLine($"[SessionHub] Could not resolve exam owner for examId={examId}");
                return null;
            }

            var pathSegments = examDoc.Reference.Path.Split('/');
            if (pathSegments.Length < 4 || !string.Equals(pathSegments[0], UsersRoot, StringComparison.Ordinal))
            {
                Console.WriteLine($"[SessionHub] Unexpected exam path format: {examDoc.Reference.Path}");
                return null;
            }

            ownerUserId = pathSegments[1];
            _examOwnerCache[examId] = ownerUserId;
        }

        return ownerUserId;
    }

    private async Task<DocumentReference?> GetExamSessionDocumentAsync(string examId)
    {
        var db = GetFirestoreDb();
        if (db == null) return null;

        var ownerUserId = await ResolveExamOwnerUserIdAsync(examId);
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            Console.WriteLine($"[SessionHub] Unable to resolve owner path for exam {examId}; skipping session update.");
            return null;
        }

        return db.Collection(UsersRoot)
            .Document(ownerUserId)
            .Collection(ExamSessionsCollection)
            .Document(examId);
    }

    private static int TryGetIntFromPayload(Dictionary<string, object> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value == null) continue;

            if (value is int i) return i;
            if (value is long l && l <= int.MaxValue && l >= int.MinValue) return (int)l;
            if (value is double d && d <= int.MaxValue && d >= int.MinValue) return (int)d;
            if (value is float f && f <= int.MaxValue && f >= int.MinValue) return (int)f;
            if (value is string s && int.TryParse(s, out var parsed)) return parsed;
            if (value is System.Text.Json.JsonElement je)
            {
                if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var jeInt))
                    return jeInt;

                if (je.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(je.GetString(), out var jeParsed))
                    return jeParsed;
            }
        }

        return 0;
    }

    private static Dictionary<string, object> ToDictionary(object payload)
    {
        if (payload is Dictionary<string, object> dict) return dict;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private async Task UpsertExamSessionParticipantAsync(string examId, string studentId, string studentName, string connectionStatus, object? heartbeatPayload = null)
    {
        try
        {
            var sessionDoc = await GetExamSessionDocumentAsync(examId);
            if (sessionDoc == null) return;

            var snapshot = await sessionDoc.GetSnapshotAsync();
            var now = Timestamp.FromDateTime(DateTime.UtcNow);

            var participants = new Dictionary<string, object>();
            if (snapshot.Exists && snapshot.TryGetValue("Participants", out Dictionary<string, object> existingParticipants) && existingParticipants != null)
            {
                participants = existingParticipants;
            }

            var participantData = new Dictionary<string, object>
            {
                ["StudentId"] = studentId,
                ["StudentName"] = studentName,
                ["ConnectionStatus"] = connectionStatus,
                ["ProgressPercent"] = 0,
                ["QuestionsAnswered"] = 0,
                ["TimeRemaining"] = 0,
                ["FlagCount"] = 0,
                ["LastHeartbeat"] = now,
                ["IpAddress"] = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? ""
            };

            if (participants.TryGetValue(studentId, out var existingParticipantObj) && existingParticipantObj is Dictionary<string, object> existingParticipant)
            {
                foreach (var kv in existingParticipant)
                {
                    participantData[kv.Key] = kv.Value;
                }

                participantData["StudentId"] = studentId;
                participantData["StudentName"] = studentName;
                participantData["ConnectionStatus"] = connectionStatus;
                participantData["LastHeartbeat"] = now;
                participantData["IpAddress"] = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "";
            }

            if (heartbeatPayload != null)
            {
                var payloadMap = ToDictionary(heartbeatPayload);
                var progress = TryGetIntFromPayload(payloadMap, "ProgressPercent", "progressPercent", "progress");
                var answered = TryGetIntFromPayload(payloadMap, "QuestionsAnswered", "questionsAnswered", "answered");
                var remaining = TryGetIntFromPayload(payloadMap, "TimeRemaining", "timeRemaining", "remainingSeconds");

                if (progress > 0) participantData["ProgressPercent"] = progress;
                if (answered > 0) participantData["QuestionsAnswered"] = answered;
                if (remaining > 0) participantData["TimeRemaining"] = remaining;
            }

            participants[studentId] = participantData;

            var onlineCount = participants.Values
                .OfType<Dictionary<string, object>>()
                .Count(p => p.TryGetValue("ConnectionStatus", out var statusObj)
                         && string.Equals(statusObj?.ToString(), "Online", StringComparison.OrdinalIgnoreCase));

            var sessionData = new Dictionary<string, object>
            {
                ["Id"] = examId,
                ["ExamId"] = examId,
                ["Status"] = "Running",
                ["LastHeartbeat"] = now,
                ["Participants"] = participants,
                ["TotalParticipants"] = participants.Count,
                ["OnlineCount"] = onlineCount,
                ["StartedAt"] = snapshot.Exists && snapshot.TryGetValue("StartedAt", out Timestamp existingStartedAt)
                    ? existingStartedAt
                    : now
            };

            await sessionDoc.SetAsync(sessionData, SetOptions.MergeAll);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHub] Failed to update exam session state: {ex.Message}");
        }
    }

    private async Task IncrementParticipantFlagAsync(string examId, string studentId)
    {
        try
        {
            var sessionDoc = await GetExamSessionDocumentAsync(examId);
            if (sessionDoc == null) return;

            var snapshot = await sessionDoc.GetSnapshotAsync();
            if (!snapshot.Exists || !snapshot.TryGetValue("Participants", out Dictionary<string, object> participants))
                return;

            if (!participants.TryGetValue(studentId, out var participantObj) || participantObj is not Dictionary<string, object> participant)
                return;

            var currentFlags = 0;
            if (participant.TryGetValue("FlagCount", out var currentFlagObj))
            {
                if (currentFlagObj is long l && l <= int.MaxValue) currentFlags = (int)l;
                else if (currentFlagObj is int i) currentFlags = i;
                else if (int.TryParse(currentFlagObj?.ToString(), out var parsed)) currentFlags = parsed;
            }

            participant["FlagCount"] = currentFlags + 1;
            participant["LastHeartbeat"] = Timestamp.FromDateTime(DateTime.UtcNow);
            participants[studentId] = participant;

            await sessionDoc.UpdateAsync(new Dictionary<string, object>
            {
                ["Participants"] = participants,
                ["LastHeartbeat"] = Timestamp.FromDateTime(DateTime.UtcNow)
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SessionHub] Failed to increment participant flag count: {ex.Message}");
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

    public async Task JoinExamSession(string examId, string studentId, string studentName, string email)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, examId);
        Console.WriteLine($"[SessionHub] Student joined: {studentName} ({studentId}) for exam {examId}");
        await UpsertExamSessionParticipantAsync(examId, studentId, studentName, "Online");
        
        await Clients.Group($"monitor_{examId}").SendAsync("StudentJoined", new
        {
            StudentId = studentId,
            StudentName = studentName,
            Email = email,
            ConnectionId = Context.ConnectionId,
            Timestamp = DateTime.UtcNow
        });

        await LogSessionEventAsync(examId, studentId, studentName, "STUDENT_JOINED", 
            $"Student {studentName} joined exam session", "Info");
    }

    public async Task LeaveExamSession(string examId, string studentId, string studentName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, examId);
        Console.WriteLine($"[SessionHub] Student left: {studentName} ({studentId}) from exam {examId}");
        await UpsertExamSessionParticipantAsync(examId, studentId, studentName, "Disconnected");
        
        await Clients.Group($"monitor_{examId}").SendAsync("StudentLeft", new
        {
            StudentId = studentId,
            StudentName = studentName,
            ConnectionId = Context.ConnectionId,
            Timestamp = DateTime.UtcNow
        });

        await LogSessionEventAsync(examId, studentId, studentName, "STUDENT_LEFT", 
            $"Student {studentName} left exam session", "Info");
    }

    public async Task SendHeartbeat(string sessionId, string studentId, object payload)
    {
        var studentName = "Unknown";
        var payloadMap = ToDictionary(payload);
        if (payloadMap.TryGetValue("StudentName", out var nameObj) && !string.IsNullOrWhiteSpace(nameObj?.ToString()))
            studentName = nameObj!.ToString()!;
        else if (payloadMap.TryGetValue("studentName", out var lowerNameObj) && !string.IsNullOrWhiteSpace(lowerNameObj?.ToString()))
            studentName = lowerNameObj!.ToString()!;

        await UpsertExamSessionParticipantAsync(sessionId, studentId, studentName, "Online", payload);

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
            "tab_switch" or "TAB_SWITCH" => "Warning",
            "disconnect" => "Warning",
            "AUTO_SUBMIT" => "Critical",
            "RETAKE_LIMIT_EXCEEDED" => "Critical",
            "SCREENSHOT_ATTEMPT" => "Warning",
            "COPY_ATTEMPT" or "PASTE_ATTEMPT" => "Warning",
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

        // Log to Firestore incident_reports collection
        await IncrementParticipantFlagAsync(sessionId, studentId);
        _ = LogIncidentToFirestoreAsync(sessionId, studentId, studentName, eventType, details, severity);
    }

    private async Task LogIncidentToFirestoreAsync(string examId, string studentId, string studentName, string eventType, string details, string severity)
    {
        try
        {
            var db = GetFirestoreDb();
            if (db == null)
            {
                Console.WriteLine("? Firestore not initialized, cannot log incident");
                return;
            }

            var ownerUserId = await ResolveExamOwnerUserIdAsync(examId);
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                Console.WriteLine($"[SessionHub] Unable to resolve owner path for exam {examId}; skipping incident log.");
                return;
            }

            var docRef = db.Collection(UsersRoot)
                .Document(ownerUserId)
                .Collection(IncidentReportsCollection)
                .Document();
            var incident = new Dictionary<string, object>
            {
                ["ExamId"] = examId,
                ["StudentId"] = studentId,
                ["StudentName"] = studentName,
                ["EventType"] = eventType,
                ["Details"] = details,
                ["Severity"] = severity,
                ["Timestamp"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["IpAddress"] = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown",
                ["ConnectionId"] = Context.ConnectionId,
                ["Resolved"] = false
            };

            await docRef.SetAsync(incident);
            Console.WriteLine($"? Incident logged: {eventType} for {studentName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Failed to log incident: {ex.Message}");
        }
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

            var ownerUserId = await ResolveExamOwnerUserIdAsync(examId);
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                Console.WriteLine($"[SessionHub] Unable to resolve owner path for exam {examId}; skipping session save.");
                return;
            }

            var docRef = db.Collection(UsersRoot)
                .Document(ownerUserId)
                .Collection(IncidentReportsCollection)
                .Document($"{examId}_{studentId}");
            
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

            var ownerUserId = await ResolveExamOwnerUserIdAsync(examId);
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                Console.WriteLine($"[SessionHub] Unable to resolve owner path for exam {examId}; cannot check active session.");
                return null;
            }

            var docRef = db.Collection(UsersRoot)
                .Document(ownerUserId)
                .Collection(IncidentReportsCollection)
                .Document($"{examId}_{studentId}");
            
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
