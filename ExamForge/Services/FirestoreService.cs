using Google.Cloud.Firestore;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using ExamForge.Models;

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
            
            if (snapshot.Exists)
            {
                return snapshot.ConvertTo<PublishedExam>();
            }
            return null;
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
                    var data = doc.ToDictionary();
                    var fallbackExam = new PublishedExam
                    {
                        Id = doc.Id,
                        Title = data.ContainsKey("Title") ? data["Title"]?.ToString() ?? "Unknown" : "Unknown",
                        StartTime = data.ContainsKey("StartTime") && data["StartTime"] is Timestamp ts1 ? ts1.ToDateTime() : DateTime.Now,
                        EndTime = data.ContainsKey("EndTime") && data["EndTime"] is Timestamp ts2 ? ts2.ToDateTime() : DateTime.Now.AddHours(1),
                        ExamDuration = data.ContainsKey("ExamDuration") && int.TryParse(data["ExamDuration"]?.ToString(), out int duration) ? duration : 60,
                        PublishedDate = data.ContainsKey("PublishedDate") && data["PublishedDate"] is Timestamp ts3 ? ts3.ToDateTime() : DateTime.Now,
                        CreatedBy = data.ContainsKey("CreatedBy") ? data["CreatedBy"]?.ToString() ?? "Unknown" : "Unknown",
                        ExamUrl = data.ContainsKey("ExamUrl") ? data["ExamUrl"]?.ToString() ?? "" : "",
                        Status = data.ContainsKey("Status") ? data["Status"]?.ToString() ?? "Draft" : "Draft",
                        LifecycleStatus = data.ContainsKey("LifecycleStatus") ? data["LifecycleStatus"]?.ToString() ?? "Draft" : "Draft",
                        LoginConfig = data.ContainsKey("LoginConfig") ? data["LoginConfig"]?.ToString() ?? "" : "",
                        // Skip complex objects for now to avoid serialization issues
                        Structures = new List<ExamStructure>(),
                        Contents = new List<ExamContent>()
                    };
                    exams.Add(fallbackExam);
                    System.Diagnostics.Debug.WriteLine($"✅ Added fallback exam: {fallbackExam.Title}");
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
        // TODO: Implement Firestore query
        return new List<IntegrityIncident>();
    }


    #endregion
}