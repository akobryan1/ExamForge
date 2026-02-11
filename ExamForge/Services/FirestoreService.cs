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
    private static FirestoreService? _instance;
    private static readonly object _lock = new();

    private FirestoreService(string projectId, string credentialPath)
    {
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

    // Save published exam
    public async Task<string> SavePublishedExamAsync(PublishedExam exam)
    {
        try
        {
            var docRef = _firestoreDb.Collection("published_exams").Document(exam.Id);
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
            var docRef = _firestoreDb.Collection("published_exams").Document(examId);
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
            var collection = _firestoreDb.Collection("published_exams");
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
            var docRef = _firestoreDb.Collection("examinee_data").Document();
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
            var query = _firestoreDb.Collection("examinee_data")
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
            var docRef = _firestoreDb.Collection("published_exams").Document(examId);
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
            var docRef = _firestoreDb.Collection("exam_sessions").Document();
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
            var docRef = _firestoreDb.Collection("exam_sessions").Document(sessionId);
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
            var docRef = _firestoreDb.Collection("exam_sessions").Document(session.Id);
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
            var query = _firestoreDb.Collection("exam_sessions")
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
            var query = _firestoreDb.Collection("exam_sessions")
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
            var docRef = _firestoreDb.Collection("session_events").Document();
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
            var query = _firestoreDb.Collection("session_events")
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

    #region QuestionBank Methods

    public async Task<string> SaveQuestionAsync(QuestionBankItem question)
    {
        try
        {
            var docRef = _firestoreDb.Collection("question_bank").Document();
            question.Id = docRef.Id;
            await docRef.SetAsync(question);
            return question.Id;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save question: {ex.Message}", ex);
        }
    }

    public async Task<List<QuestionBankItem>> SearchQuestionsAsync(
        string? subject = null,
        string? difficulty = null,
        List<string>? tags = null)
    {
        try
        {
            Query query = _firestoreDb.Collection("question_bank");

            if (!string.IsNullOrEmpty(subject))
                query = query.WhereEqualTo("Subject", subject);

            if (!string.IsNullOrEmpty(difficulty))
                query = query.WhereEqualTo("Difficulty", difficulty);

            if (tags != null && tags.Count > 0)
                query = query.WhereArrayContainsAny("Tags", tags);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<QuestionBankItem>()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to search questions: {ex.Message}", ex);
        }
    }

    public async Task<List<IntegrityIncident>> GetIntegrityIncidentsAsync(string examId)
    {
        try
        {
            var query = _firestoreDb.Collection("integrity_incidents")
                .WhereEqualTo("ExamId", examId)
                .OrderByDescending("Timestamp");
                
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<IntegrityIncident>()).ToList();
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
            var query = _firestoreDb.Collection("exam_sessions")
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
            var query = _firestoreDb.Collection("integrity_incidents")
                .WhereGreaterThan("Timestamp", Timestamp.FromDateTime(cutoffTime))
                .OrderByDescending("Timestamp")
                .Limit(50);
                
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<IntegrityIncident>()).ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get recent integrity incidents: {ex.Message}");
            return new List<IntegrityIncident>();
        }
    }


    #endregion

    #region Grading Queue Methods

    public async Task<List<GradingQueueItem>> GetGradingQueueItemsAsync(string? examId = null)
    {
        try
        {
            Query query = _firestoreDb.Collection("grading_queue");
            
            if (!string.IsNullOrEmpty(examId))
            {
                query = query.WhereEqualTo("ExamId", examId);
            }
            
            query = query.OrderBy("SubmittedAt");
            
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(d => d.ConvertTo<GradingQueueItem>()).ToList();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to get grading queue items: {ex.Message}", ex);
        }
    }

    public async Task UpdateGradingQueueItemAsync(string itemId, int pointsAwarded, string? feedback = null, string? gradedBy = null)
    {
        try
        {
            var docRef = _firestoreDb.Collection("grading_queue").Document(itemId);
            
            var updates = new Dictionary<string, object>
            {
                { "PointsAwarded", pointsAwarded },
                { "Status", "Graded" },
                { "GradedAt", Timestamp.GetCurrentTimestamp() }
            };
            
            if (!string.IsNullOrEmpty(feedback))
            {
                updates["Feedback"] = feedback;
            }
            
            if (!string.IsNullOrEmpty(gradedBy))
            {
                updates["GradedBy"] = gradedBy;
            }
            
            await docRef.UpdateAsync(updates);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to update grading queue item: {ex.Message}", ex);
        }
    }

    #endregion

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
