using Google.Cloud.Firestore;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using ExamForge.Models;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace ExamForge.Services;

public class FirestoreService
{
    private readonly FirestoreDb _firestoreDb;
    private readonly string _userId;
    private static FirestoreService? _instance;
    private static readonly object _lock = new();

    // Top-level users collection name (matches Supabase table / your screenshot)
    private const string UsersRoot = "examforge_users";
    // Standard per-user subcollection names
    private const string PublishedExamsCollection = "published_exams";
    private const string ExamSessionsCollection = "exam_sessions";
    private const string ExamineeDataCollection = "examinee_data";
    private const string SessionEventsCollection = "session_events";
    private const string IncidentReportsCollection = "incident_reports";

    public string UserId => _userId;

    // Constructor with userId for user-scoped collections
    public FirestoreService(string userId)
    {
        _userId = userId;

        // Allow overriding via environment variables (useful on Render)
        var projectId = Environment.GetEnvironmentVariable("FIRESTORE_PROJECT_ID") ?? "examforge-201e8";
        var credentialEnv = Environment.GetEnvironmentVariable("SERVICE_ACCOUNT_JSON");
        var credentialPath = "firebase-adminsdk.json";

        if (!string.IsNullOrWhiteSpace(credentialEnv))
        {
            // If a JSON key is provided via env var, write it to the expected file path so
            // existing code that calls FromFile continues to work.
            try
            {
                System.IO.File.WriteAllText(credentialPath, credentialEnv);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write service account JSON to file: {ex.Message}");
            }
        }

        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialPath)
            });
        }

        // Ensure GOOGLE_APPLICATION_CREDENTIALS is set for any libraries that rely on it
        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
        _firestoreDb = FirestoreDb.Create(projectId);
    }

    // Legacy static instance method (kept for compatibility)
    private FirestoreService(string projectId, string credentialPath)
    {
        _userId = "default"; // Legacy default user

        if (FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(credentialPath)
            });
        }

        Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialPath);
        _firestoreDb = FirestoreDb.Create(projectId);
    }

    public static FirestoreService GetInstance(string projectId, string credentialPath)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new FirestoreService(projectId, credentialPath);
            }
        }
        return _instance;
    }

    // User-scoped collection paths
    private string GetUserPath(string collection) => $"{UsersRoot}/{_userId}/{collection}";

    // Save published exam (user-scoped)
    public async Task<string> SavePublishedExamAsync(PublishedExam exam)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(PublishedExamsCollection)).Document(exam.Id);
            await docRef.SetAsync(exam);
            return exam.Id;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save exam to Firestore: {ex.Message}", ex);
        }
    }

    // Get published exam by ID
    public async Task<PublishedExam?> GetPublishedExamAsync(string examId)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(PublishedExamsCollection)).Document(examId);
            var snapshot = await docRef.GetSnapshotAsync();
            
            if (!snapshot.Exists) return null;

            try
            {
                return snapshot.ConvertTo<PublishedExam>();
            }
            catch (Exception)
            {
                // Fallback conversion for nested maps
                return ConvertSnapshotToPublishedExam(snapshot);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get exam from Firestore: {ex.Message}", ex);
        }
    }

    // Get all published exams
    public async Task<List<PublishedExam>> GetAllPublishedExamsAsync()
    {
        try
        {
            var collection = _firestoreDb.Collection(GetUserPath(PublishedExamsCollection));
            var snapshot = await collection.GetSnapshotAsync();
            
            var exams = new List<PublishedExam>();
            
            foreach (var doc in snapshot.Documents)
            {
                try
                {
                    var exam = doc.ConvertTo<PublishedExam>();
                    exams.Add(exam);
                }
                catch (Exception docEx)
                {
                    // Log which specific document and field is causing issues
                    System.Diagnostics.Debug.WriteLine($"❌ Failed to convert document {doc.Id}: {docEx.Message}");
                    
                    // Try to create a minimal PublishedExam with just basic fields
                    exams.Add(ConvertSnapshotToPublishedExam(doc));
                }
            }
            
            return exams;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get exams from Firestore: {ex.Message}", ex);
        }
    }

    // Save examinee submission - ✅ FIXED: Now uses examinee_data
    public async Task<string> SaveExamineeSubmissionAsync(ExamSubmission submission)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(ExamineeDataCollection)).Document();
            submission.Id = docRef.Id;
            await docRef.SetAsync(submission);
            return submission.Id;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save submission to Firestore: {ex.Message}", ex);
        }
    }

    // Get submissions for an exam - ✅ FIXED: Now uses examinee_data
    public async Task<List<ExamSubmission>> GetExamSubmissionsAsync(string examId)
    {
        try
        {
            var query = _firestoreDb.Collection(GetUserPath(ExamineeDataCollection))
                .WhereEqualTo("ExamId", examId);
            var snapshot = await query.GetSnapshotAsync();
            
            return snapshot.Documents
                .Select(doc => doc.ConvertTo<ExamSubmission>())
                .ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get submissions from Firestore: {ex.Message}", ex);
        }
    }

    // Delete published exam
    public async Task DeletePublishedExamAsync(string examId)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(PublishedExamsCollection)).Document(examId);
            await docRef.DeleteAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to delete exam from Firestore: {ex.Message}", ex);
        }
    }

    #region ExamSession Methods

    public async Task<string> CreateSessionAsync(ExamSession session)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(ExamSessionsCollection)).Document();
            session.Id = docRef.Id;
            await docRef.SetAsync(session);
            return session.Id;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create session: {ex.Message}", ex);
        }
    }

    public async Task<ExamSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(ExamSessionsCollection)).Document(sessionId);
            var snapshot = await docRef.GetSnapshotAsync();
            return snapshot.Exists ? snapshot.ConvertTo<ExamSession>() : null;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get session: {ex.Message}", ex);
        }
    }

    public async Task UpdateSessionAsync(ExamSession session)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(ExamSessionsCollection)).Document(session.Id);
            await docRef.SetAsync(session, SetOptions.MergeAll);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to update session: {ex.Message}", ex);
        }
    }

    public async Task<List<ExamSession>> GetActiveSessionsAsync(string examId)
    {
        try
        {
            var query = _firestoreDb.Collection(GetUserPath(ExamSessionsCollection))
                .WhereEqualTo("ExamId", examId)
                .WhereEqualTo("Status", "Running");
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<ExamSession>()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get active sessions: {ex.Message}", ex);
        }
    }

    public async Task<List<ExamSession>> GetRunningSessionsAsync()
    {
        try
        {
            var query = _firestoreDb.Collection(GetUserPath(ExamSessionsCollection))
                .WhereEqualTo("Status", "Running");
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<ExamSession>()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get active sessions: {ex.Message}", ex);
        }
    }

    #endregion

    #region SessionEvent Methods

    public async Task<string> LogEventAsync(SessionEvent evt)
    {
        try
        {
            var docRef = _firestoreDb.Collection(GetUserPath(SessionEventsCollection)).Document();
            evt.Id = docRef.Id;
            await docRef.SetAsync(evt);
            return evt.Id;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to log event: {ex.Message}", ex);
        }
    }

    public async Task<List<SessionEvent>> GetSessionEventsAsync(string sessionId, int limit = 100)
    {
        try
        {
            var query = _firestoreDb.Collection(GetUserPath(SessionEventsCollection))
                .WhereEqualTo("SessionId", sessionId)
                .OrderByDescending("Timestamp")
                .Limit(limit);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<SessionEvent>()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get session events: {ex.Message}", ex);
        }
    }

    #endregion

    #region Deprecated Collections + Incident Mapping

    public Task<string> SaveQuestionAsync(QuestionBankItem question)
    {
        throw new NotSupportedException("question_bank collection is deprecated and no longer used.");
    }

    public Task<List<QuestionBankItem>> SearchQuestionsAsync(
        string? subject = null,
        string? difficulty = null,
        List<string>? tags = null)
    {
        return Task.FromResult(new List<QuestionBankItem>());
    }

    public async Task<List<IntegrityIncident>> GetIntegrityIncidentsAsync(string examId)
    {
        try
        {
            var query = _firestoreDb.Collection(GetUserPath(IncidentReportsCollection))
                .WhereEqualTo("ExamId", examId)
                .OrderByDescending("Timestamp");
                
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(ConvertIncidentReportToIntegrityIncident).ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get integrity incidents: {ex.Message}");
            return new List<IntegrityIncident>();
        }
    }

    public async Task<List<ExamSession>> GetActiveExamSessionsAsync()
    {
        try
        {
            var query = _firestoreDb.Collection(GetUserPath(ExamSessionsCollection))
                .WhereEqualTo("IsActive", true)
                .OrderBy("StartTime");
                
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<ExamSession>()).ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get active exam sessions: {ex.Message}");
            return new List<ExamSession>();
        }
    }

    public async Task<List<IntegrityIncident>> GetRecentIntegrityIncidentsAsync(TimeSpan timeSpan)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.Subtract(timeSpan);
            var query = _firestoreDb.Collection(GetUserPath(IncidentReportsCollection))
                .WhereGreaterThan("Timestamp", Timestamp.FromDateTime(cutoffTime))
                .OrderByDescending("Timestamp")
                .Limit(50);
                
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(ConvertIncidentReportToIntegrityIncident).ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get recent integrity incidents: {ex.Message}");
            return new List<IntegrityIncident>();
        }
    }

    /// <summary>
    /// Create default subcollections for a new user by writing a small metadata document to each collection.
    /// Firestore creates collections when a document is written, so this ensures the collections exist.
    /// </summary>
    public async Task InitializeUserCollectionsAsync()
    {
        try
        {
            var userDocRef = _firestoreDb.Collection(UsersRoot).Document(_userId);
            await userDocRef.SetAsync(new Dictionary<string, object>
            {
                ["user_id"] = _userId,
                ["initialized_at"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["initialized"] = true
            }, SetOptions.MergeAll);

            var collections = new[]
            {
                ExamSessionsCollection,
                PublishedExamsCollection,
                ExamineeDataCollection,
                IncidentReportsCollection,
                SessionEventsCollection
            };

            foreach (var col in collections)
            {
                var collRef = _firestoreDb.Collection(GetUserPath(col));
                var metaRef = collRef.Document("_meta");
                var data = new Dictionary<string, object>
                {
                    { "created_at", Timestamp.FromDateTime(DateTime.UtcNow) },
                    { "initialized", true }
                };
                await metaRef.SetAsync(data);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize user collections: {ex.Message}");
            throw;
        }
    }


    #endregion

    #region Grading Queue Methods (Deprecated)

    public Task<List<GradingQueueItem>> GetGradingQueueItemsAsync(string? examId = null)
    {
        return Task.FromResult(new List<GradingQueueItem>());
    }

    public Task UpdateGradingQueueItemAsync(string itemId, int pointsAwarded, string? feedback = null, string? gradedBy = null)
    {
        Debug.WriteLine("grading_queue is deprecated and updates are skipped.");
        return Task.CompletedTask;
    }

    public Task RemoveGradingQueueItemAsync(string itemId)
    {
        Debug.WriteLine("grading_queue is deprecated and removals are skipped.");
        return Task.CompletedTask;
    }

    #endregion

    #region Incident Reports Methods

    /// <summary>
    /// Save session progress to incident_reports collection (auto-creates if doesn't exist)
    /// </summary>
    public async Task<string> SaveIncidentReportAsync(string examId, Dictionary<string, object> sessionData, string studentId, string studentName, string? studentEmail = null)
    {
        try
        {
            var collectionPath = GetUserPath(IncidentReportsCollection);
            var docRef = _firestoreDb.Collection(collectionPath).Document();
            
            var reportData = new Dictionary<string, object>
            {
                ["Id"] = docRef.Id,
                ["ExamId"] = examId,
                ["StudentId"] = studentId,
                ["StudentName"] = studentName,
                ["StudentEmail"] = studentEmail ?? "",
                ["SessionData"] = sessionData,
                ["EventType"] = "session_saved",
                ["Timestamp"] = Timestamp.FromDateTime(DateTime.UtcNow),
                ["Status"] = "active"
            };

            await docRef.SetAsync(reportData);
            Debug.WriteLine($"✅ Incident report saved: {docRef.Id}");
            return docRef.Id;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save incident report: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Retrieve active session for a student (cross-device support)
    /// </summary>
    public async Task<Dictionary<string, object>?> GetActiveSessionForStudentAsync(string examId, string studentId, string? studentEmail = null)
    {
        try
        {
            var collectionPath = GetUserPath(IncidentReportsCollection);
            Query query = _firestoreDb.Collection(collectionPath)
                .WhereEqualTo("ExamId", examId)
                .WhereEqualTo("Status", "active")
                .OrderByDescending("Timestamp")
                .Limit(1);

            // Match by student ID or email
            if (!string.IsNullOrEmpty(studentEmail))
            {
                query = query.WhereEqualTo("StudentEmail", studentEmail);
            }
            else
            {
                query = query.WhereEqualTo("StudentId", studentId);
            }

            var snapshot = await query.GetSnapshotAsync();
            
            if (snapshot.Documents.Count == 0)
                return null;

            var doc = snapshot.Documents[0];
            var data = doc.ToDictionary();
            
            // Check if session is still valid (exam hasn't ended)
            var exam = await GetPublishedExamAsync(examId);
            if (exam != null && DateTime.UtcNow > exam.EndTime)
            {
                // Exam ended, mark session as expired and delete
                await doc.Reference.UpdateAsync("Status", "expired");
                await doc.Reference.DeleteAsync();
                Debug.WriteLine($"⏰ Session expired for student {studentId}");
                return null;
            }

            return data;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get active session: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Update session status (e.g., mark as resumed or expired)
    /// </summary>
    public async Task UpdateSessionStatusAsync(string reportId, string status)
    {
        try
        {
            var collectionPath = GetUserPath(IncidentReportsCollection);
            var docRef = _firestoreDb.Collection(collectionPath).Document(reportId);
            
            await docRef.UpdateAsync(new Dictionary<string, object>
            {
                ["Status"] = status,
                ["UpdatedAt"] = Timestamp.FromDateTime(DateTime.UtcNow)
            });
            
            Debug.WriteLine($"✅ Session {reportId} status updated to: {status}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to update session status: {ex.Message}");
        }
    }

    /// <summary>
    /// Clean up expired sessions for an exam
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(string examId)
    {
        try
        {
            var exam = await GetPublishedExamAsync(examId);
            if (exam == null || DateTime.UtcNow <= exam.EndTime)
                return; // Exam still active

            var collectionPath = GetUserPath(IncidentReportsCollection);
            var query = _firestoreDb.Collection(collectionPath)
                .WhereEqualTo("ExamId", examId)
                .WhereEqualTo("Status", "active");

            var snapshot = await query.GetSnapshotAsync();
            
            foreach (var doc in snapshot.Documents)
            {
                await doc.Reference.UpdateAsync("Status", "expired");
                await doc.Reference.DeleteAsync();
            }

            Debug.WriteLine($"🧹 Cleaned up {snapshot.Documents.Count} expired sessions for exam {examId}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to cleanup expired sessions: {ex.Message}");
        }
    }

    #endregion

    private static IntegrityIncident ConvertIncidentReportToIntegrityIncident(DocumentSnapshot doc)
    {
        var data = doc.ToDictionary();

        return new IntegrityIncident
        {
            Id = doc.Id,
            ExamId = data.TryGetValue("ExamId", out var examId) ? examId?.ToString() ?? "" : "",
            StudentId = data.TryGetValue("StudentId", out var studentId) ? studentId?.ToString() ?? "" : "",
            StudentName = data.TryGetValue("StudentName", out var studentName) ? studentName?.ToString() ?? "" : "",
            IncidentType = data.TryGetValue("IncidentType", out var incidentType)
                ? incidentType?.ToString() ?? ""
                : data.TryGetValue("EventType", out var eventType) ? eventType?.ToString() ?? "unknown" : "unknown",
            Severity = data.TryGetValue("Severity", out var severity) ? severity?.ToString() ?? "Info" : "Info",
            Timestamp = data.TryGetValue("Timestamp", out var ts) && ts is Timestamp timestamp
                ? timestamp.ToDateTime()
                : DateTime.UtcNow,
            Details = data.TryGetValue("Details", out var details) ? details?.ToString() ?? "" : "",
            Status = data.TryGetValue("Status", out var status) ? status?.ToString() ?? "Pending" : "Pending"
        };
    }

    private PublishedExam ConvertSnapshotToPublishedExam(DocumentSnapshot doc)
    {
        var data = doc.ToDictionary();

        var exam = new PublishedExam
        {
            Id = doc.Id,
            Title = data.TryGetValue("Title", out var titleVal) ? titleVal?.ToString() ?? "Unknown" : "Unknown",
            Subject = data.TryGetValue("Subject", out var subjVal) ? subjVal?.ToString() ?? "General" : "General",
            StartTime = data.TryGetValue("StartTime", out var stVal) && stVal is Timestamp ts1 ? ts1.ToDateTime() : DateTime.Now,
            EndTime = data.TryGetValue("EndTime", out var etVal) && etVal is Timestamp ts2 ? ts2.ToDateTime() : DateTime.Now.AddHours(1),
            ExamDuration = data.TryGetValue("ExamDuration", out var durVal) && int.TryParse(durVal?.ToString(), out var duration) ? duration : 60,
            PublishedDate = data.TryGetValue("PublishedDate", out var pdVal) && pdVal is Timestamp ts3 ? ts3.ToDateTime() : DateTime.Now,
            CreatedBy = data.TryGetValue("CreatedBy", out var cbVal) ? cbVal?.ToString() ?? "Unknown" : "Unknown",
            ExamUrl = data.TryGetValue("ExamUrl", out var urlVal) ? urlVal?.ToString() ?? "" : "",
            Status = data.TryGetValue("Status", out var statusVal) ? statusVal?.ToString() ?? "Draft" : "Draft",
            LifecycleStatus = data.TryGetValue("LifecycleStatus", out var lifeVal) ? lifeVal?.ToString() ?? "Draft" : "Draft",
            PassingScorePercentage = data.TryGetValue("PassingScorePercentage", out var passVal) && int.TryParse(passVal?.ToString(), out var passInt) ? passInt : 60,
            PassRate = data.TryGetValue("PassRate", out var passRateVal) && double.TryParse(passRateVal?.ToString(), out var passRate) ? passRate : 0,
            TimesUsed = data.TryGetValue("TimesUsed", out var timesVal) && int.TryParse(timesVal?.ToString(), out var timesInt) ? timesInt : 0,
            AverageDifficulty = data.TryGetValue("AverageDifficulty", out var diffVal) && double.TryParse(diffVal?.ToString(), out var diff) ? diff : null
        };

        // LoginConfig as nested map
        if (data.TryGetValue("LoginConfig", out var loginVal) && loginVal is Dictionary<string, object> loginMap)
        {
            exam.LoginConfig = new LoginConfigState
            {
                IsGoogleSignIn = loginMap.TryGetValue("IsGoogleSignIn", out var g) && Convert.ToBoolean(g),
                RequireFullName = loginMap.TryGetValue("RequireFullName", out var f) && Convert.ToBoolean(f),
                RequireYearSection = loginMap.TryGetValue("RequireYearSection", out var ys) && Convert.ToBoolean(ys),
                RequireStudentNumber = loginMap.TryGetValue("RequireStudentNumber", out var sn) && Convert.ToBoolean(sn)
            };
        }

        // Structures
        if (data.TryGetValue("Structures", out var structuresVal) && structuresVal is IEnumerable<object> structList)
        {
            exam.Structures = structList
                .OfType<Dictionary<string, object>>()
                .Select(s => new ExamStructure
                {
                    SectionName = s.TryGetValue("SectionName", out var name) ? name?.ToString() ?? "" : "",
                    Description = s.TryGetValue("Description", out var desc) ? desc?.ToString() ?? "" : "",
                    Points = s.TryGetValue("Points", out var pts) && int.TryParse(pts?.ToString(), out var p) ? p : 0,
                    QuestionCount = s.TryGetValue("QuestionCount", out var qc) && int.TryParse(qc?.ToString(), out var q) ? q : 0,
                    IsComplete = s.TryGetValue("IsComplete", out var comp) && Convert.ToBoolean(comp)
                }).ToList();
        }

        // Contents
        if (data.TryGetValue("Contents", out var contentsVal) && contentsVal is IEnumerable<object> contentList)
        {
            exam.Contents = contentList
                .OfType<Dictionary<string, object>>()
                .Select(c => new ExamContent
                {
                    ContentId = c.TryGetValue("ContentId", out var cid) ? cid?.ToString() ?? "" : c.TryGetValue("Id", out var idVal) ? idVal?.ToString() ?? "" : "",
                    ExamId = c.TryGetValue("ExamId", out var exId) ? exId?.ToString() ?? "" : "",
                    Question = c.TryGetValue("Question", out var q) ? q?.ToString() ?? "" : "",
                    Answer = c.TryGetValue("Answer", out var a) ? a?.ToString() ?? "" : "",
                    Explanation = c.TryGetValue("Explanation", out var exp) ? exp?.ToString() ?? "" : "",
                    MediaUrl = c.TryGetValue("MediaUrl", out var media) ? media?.ToString() ?? "" : "",
                    IsComplete = c.TryGetValue("IsComplete", out var comp) && Convert.ToBoolean(comp),
                    QuestionType = c.TryGetValue("QuestionType", out var qt) ? qt?.ToString() ?? "MCQ" : "MCQ",
                    Points = c.TryGetValue("Points", out var pts) && int.TryParse(pts?.ToString(), out var p) ? p : 1,
                    Options = c.TryGetValue("Options", out var opts) && opts is IEnumerable<object> optList ? optList.Select(o => o?.ToString() ?? "").ToList() : new List<string>()
                }).ToList();
        }

        System.Diagnostics.Debug.WriteLine($"✅ Added fallback exam: {exam.Title}");
        return exam;
    }
}
