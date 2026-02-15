# ExamForge API Documentation
**Technical Reference for Developers**

---

## Overview

ExamForge exposes several services and APIs for integration and extension.

---

## Core Services

### **FirestoreService**

Handles all database operations with Firebase/Firestore.

#### **Methods:**

##### **GetPublishedExamAsync**
```csharp
public async Task<PublishedExam?> GetPublishedExamAsync(string examId)
```
**Purpose:** Retrieve a single published exam by ID

**Parameters:**
- `examId` (string): Unique exam identifier

**Returns:** `PublishedExam` object or null if not found

**Example:**
```csharp
var firestoreService = App.FirestoreService;
var exam = await firestoreService.GetPublishedExamAsync("exam123");
if (exam != null)
{
    Console.WriteLine($"Exam: {exam.Title}");
}
```

---

##### **GetAllPublishedExamsAsync**
```csharp
public async Task<List<PublishedExam>> GetAllPublishedExamsAsync()
```
**Purpose:** Retrieve all published exams

**Returns:** List of PublishedExam objects

**Example:**
```csharp
var exams = await firestoreService.GetAllPublishedExamsAsync();
Console.WriteLine($"Found {exams.Count} exams");
```

---

##### **SavePublishedExamAsync**
```csharp
public async Task<string> SavePublishedExamAsync(PublishedExam exam)
```
**Purpose:** Create or update a published exam

**Parameters:**
- `exam` (PublishedExam): Exam object to save

**Returns:** Exam ID (string)

**Example:**
```csharp
var newExam = new PublishedExam
{
    Title = "Midterm Exam",
    Subject = "Biology",
    StartTime = DateTime.UtcNow.AddDays(1),
    EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
    ExamDuration = 90
};

var examId = await firestoreService.SavePublishedExamAsync(newExam);
Console.WriteLine($"Saved exam: {examId}");
```

---

##### **GetExamSubmissionsAsync**
```csharp
public async Task<List<ExamSubmission>> GetExamSubmissionsAsync(string examId)
```
**Purpose:** Get all student submissions for an exam

**Parameters:**
- `examId` (string): Exam identifier

**Returns:** List of ExamSubmission objects

**Example:**
```csharp
var submissions = await firestoreService.GetExamSubmissionsAsync("exam123");
var avgScore = submissions.Average(s => s.TotalScore);
Console.WriteLine($"Average: {avgScore:F2}");
```

---

##### **SaveExamineeSubmissionAsync**
```csharp
public async Task<string> SaveExamineeSubmissionAsync(ExamSubmission submission)
```
**Purpose:** Save student exam submission

**Parameters:**
- `submission` (ExamSubmission): Submission data

**Returns:** Submission ID

---

##### **GetRunningSessionsAsync**
```csharp
public async Task<List<ExamSession>> GetRunningSessionsAsync()
```
**Purpose:** Get all currently active exam sessions

**Returns:** List of ExamSession objects

**Example:**
```csharp
var sessions = await firestoreService.GetRunningSessionsAsync();
foreach (var session in sessions)
{
    Console.WriteLine($"Session {session.Id}: {session.Participants.Count} students");
}
```

---

##### **UpdateSessionAsync**
```csharp
public async Task UpdateSessionAsync(ExamSession session)
```
**Purpose:** Update an existing session

**Parameters:**
- `session` (ExamSession): Session to update

---

##### **LogEventAsync**
```csharp
public async Task LogEventAsync(SessionEvent evt)
```
**Purpose:** Log an integrity or system event

**Parameters:**
- `evt` (SessionEvent): Event details

**Example:**
```csharp
await firestoreService.LogEventAsync(new SessionEvent
{
    SessionId = "session123",
    ExamId = "exam123",
    StudentId = "student456",
    EventType = "tab_switch",
    Severity = "Warning",
    Details = "Student switched tabs during exam"
});
```

---

##### **GetIntegrityIncidentsAsync**
```csharp
public async Task<List<IntegrityIncident>> GetIntegrityIncidentsAsync(string examId)
```
**Purpose:** Get integrity incidents for an exam

**Parameters:**
- `examId` (string): Exam identifier

**Returns:** List of IntegrityIncident objects

---

### **AnalyticsService**

Provides statistical analysis and insights.

#### **Methods:**

##### **CalculateClassOverviewAsync**
```csharp
public async Task<ClassOverviewMetrics> CalculateClassOverviewAsync(string examId)
```
**Purpose:** Calculate aggregated class metrics

**Returns:** ClassOverviewMetrics object containing:
- ClassAverage (double)
- PassRate (double)
- CompletionRate (double)
- StandardDeviation (double)
- MedianScore (double)
- ScoreDistribution (List<ScoreBin>)

**Example:**
```csharp
var analyticsService = new AnalyticsService();
var metrics = await analyticsService.CalculateClassOverviewAsync("exam123");
Console.WriteLine($"Class Average: {metrics.ClassAverage:F2}%");
Console.WriteLine($"Pass Rate: {metrics.PassRate:F2}%");
```

---

##### **CalculateItemAnalysisAsync**
```csharp
public async Task<List<ItemAnalysisData>> CalculateItemAnalysisAsync(
    string examId,
    List<ExamSubmission> submissions)
```
**Purpose:** Analyze each question's effectiveness

**Returns:** List of ItemAnalysisData with:
- DifficultyPercent (double): % who got it correct
- Discrimination (double): Upper 27% vs Lower 27%
- PointBiserial (double): Correlation with total score
- QualityIndicator (string): Excellent/Good/Fair/Poor

**Example:**
```csharp
var submissions = await firestoreService.GetExamSubmissionsAsync("exam123");
var itemAnalysis = await analyticsService.CalculateItemAnalysisAsync("exam123", submissions);

foreach (var item in itemAnalysis)
{
    if (item.QualityIndicator == "Poor")
    {
        Console.WriteLine($"Question {item.QuestionNumber} needs review!");
    }
}
```

---

### **ExportService**

Handles data export to various formats.

#### **Methods:**

##### **ExportGradesToExcelAsync**
```csharp
public async Task<string> ExportGradesToExcelAsync(string examId, string examTitle)
```
**Purpose:** Export gradebook to Excel

**Returns:** File path to exported Excel file

**Example:**
```csharp
var exportService = new ExportService();
var path = await exportService.ExportGradesToExcelAsync("exam123", "Midterm");
Console.WriteLine($"Exported to: {path}");
```

---

##### **ExportStudentReportAsync**
```csharp
public async Task<string> ExportStudentReportAsync(
    string studentName,
    string examTitle,
    ExamSubmission submission,
    PublishedExam exam)
```
**Purpose:** Export individual student report (CSV)

**Returns:** File path

---

##### **ExportIntegrityReportAsync**
```csharp
public async Task<string> ExportIntegrityReportAsync(string examId, string examTitle)
```
**Purpose:** Export integrity incidents report

**Returns:** File path to CSV

---

### **PdfExportService**

PDF generation for professional reports.

#### **Methods:**

##### **ExportStudentReportToPdfAsync**
```csharp
public async Task<string> ExportStudentReportToPdfAsync(
    string studentName,
    string examTitle,
    ExamSubmission submission,
    PublishedExam exam)
```
**Purpose:** Generate professional PDF student report

**Returns:** File path to PDF

**Example:**
```csharp
var pdfService = new PdfExportService();
var path = await pdfService.ExportStudentReportToPdfAsync(
    "John Smith",
    "Final Exam",
    submission,
    exam
);
```

---

##### **ExportGradebookToPdfAsync**
```csharp
public async Task<string> ExportGradebookToPdfAsync(
    string examId,
    string examTitle,
    List<ExamSubmission> submissions)
```
**Purpose:** Generate gradebook PDF

**Returns:** File path

---

### **MachineLearningService**

AI-powered predictions and insights.

#### **Methods:**

##### **PredictStudentPerformance**
```csharp
public PredictionResult PredictStudentPerformance(
    List<ExamSubmission> historicalData,
    string studentId)
```
**Purpose:** Predict student's next exam score

**Returns:** PredictionResult with:
- PredictedScore (double)
- Confidence (double)
- Trend (string): "Improving" / "Declining" / "Stable"
- Recommendation (string)

**Example:**
```csharp
var mlService = new MachineLearningService();
var allSubmissions = await GetAllSubmissionsForStudent("student123");
var prediction = mlService.PredictStudentPerformance(allSubmissions, "student123");

Console.WriteLine($"Predicted: {prediction.PredictedScore:F1}%");
Console.WriteLine($"Trend: {prediction.Trend}");
Console.WriteLine($"Recommendation: {prediction.Recommendation}");
```

---

##### **IdentifyAtRiskStudents**
```csharp
public List<AtRiskStudent> IdentifyAtRiskStudents(
    List<ExamSubmission> submissions,
    List<IntegrityIncident> incidents)
```
**Purpose:** Identify students needing intervention

**Returns:** List of AtRiskStudent with:
- RiskScore (int): 0-100
- RiskLevel (string): "High" / "Medium"
- RiskFactors (List<string>)
- RecommendedActions (List<string>)

**Example:**
```csharp
var incidents = await firestoreService.GetIntegrityIncidentsAsync("exam123");
var submissions = await firestoreService.GetExamSubmissionsAsync("exam123");

var atRisk = mlService.IdentifyAtRiskStudents(submissions, incidents);
foreach (var student in atRisk)
{
    Console.WriteLine($"{student.StudentName}: Risk {student.RiskScore}");
    foreach (var action in student.RecommendedActions)
    {
        Console.WriteLine($"  - {action}");
    }
}
```

---

##### **RecommendExamDifficulty**
```csharp
public DifficultyRecommendation RecommendExamDifficulty(
    List<ExamSubmission> historicalSubmissions)
```
**Purpose:** Suggest optimal exam difficulty distribution

**Returns:** DifficultyRecommendation with:
- RecommendedDifficulty (string)
- Reasoning (string)
- EasyQuestions (int)
- MediumQuestions (int)
- HardQuestions (int)

---

##### **IdentifyLearningGaps**
```csharp
public List<LearningGap> IdentifyLearningGaps(
    List<ExamSubmission> submissions,
    PublishedExam exam)
```
**Purpose:** Find topics where class struggles

**Returns:** List of LearningGap with:
- Topic (string)
- SuccessRate (double)
- Severity (string)
- AffectedStudents (int)
- Recommendation (string)

---

### **LmsIntegrationService**

LMS synchronization capabilities.

#### **Methods:**

##### **SyncToCanvasAsync**
```csharp
public async Task<bool> SyncToCanvasAsync(
    string canvasApiUrl,
    string accessToken,
    string courseId,
    ExamSubmission submission)
```
**Purpose:** Sync grade to Canvas LMS

**Parameters:**
- `canvasApiUrl`: Canvas instance URL
- `accessToken`: Canvas API token
- `courseId`: Canvas course ID
- `submission`: ExamForge submission

**Returns:** true if successful

**Example:**
```csharp
var lmsService = new LmsIntegrationService();
var success = await lmsService.SyncToCanvasAsync(
    "https://canvas.school.edu",
    "your-api-token",
    "course123",
    submission
);
```

---

##### **SyncToBlackboardAsync**
```csharp
public async Task<bool> SyncToBlackboardAsync(
    string blackboardUrl,
    string accessToken,
    string courseId,
    ExamSubmission submission)
```
**Purpose:** Sync grade to Blackboard Learn

---

##### **SyncToMoodleAsync**
```csharp
public async Task<bool> SyncToMoodleAsync(
    string moodleUrl,
    string token,
    string courseId,
    ExamSubmission submission)
```
**Purpose:** Sync grade to Moodle

---

##### **ExportCommonCartridgeAsync**
```csharp
public async Task<string> ExportCommonCartridgeAsync(
    string examId,
    string examTitle,
    List<ExamSubmission> submissions)
```
**Purpose:** Export in IMS Common Cartridge format

**Returns:** File path to XML

---

##### **ImportRosterFromCanvasAsync**
```csharp
public async Task<List<StudentRoster>> ImportRosterFromCanvasAsync(
    string canvasApiUrl,
    string accessToken,
    string courseId)
```
**Purpose:** Import student roster from Canvas

**Returns:** List of StudentRoster

---

### **SignalRService**

Real-time communication with exam sessions.

#### **Methods:**

##### **ConnectAsync**
```csharp
public async Task ConnectAsync()
```
**Purpose:** Establish SignalR connection

---

##### **DisconnectAsync**
```csharp
public async Task DisconnectAsync()
```
**Purpose:** Close SignalR connection

---

##### **JoinSessionAsync**
```csharp
public async Task JoinSessionAsync(string sessionId)
```
**Purpose:** Join a specific session group

---

##### **LeaveSessionAsync**
```csharp
public async Task LeaveSessionAsync(string sessionId)
```
**Purpose:** Leave session group

---

##### **BroadcastMessageAsync**
```csharp
public async Task BroadcastMessageAsync(string sessionId, string message)
```
**Purpose:** Send message to all participants in session

**Example:**
```csharp
var signalRService = new SignalRService("https://signalr-server.com/sessionHub");
await signalRService.ConnectAsync();
await signalRService.JoinSessionAsync("session123");
await signalRService.BroadcastMessageAsync("session123", "5 minutes remaining!");
```

---

## Data Models

### **PublishedExam**

```csharp
public class PublishedExam
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Subject { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExamDuration { get; set; } // minutes
    public DateTime PublishedDate { get; set; }
    public string CreatedBy { get; set; }
    public string ExamUrl { get; set; }
    public List<ExamStructure> Structure { get; set; }
    public List<ExamContent> Contents { get; set; }
    public string Status { get; set; } // "Published", "Closed", "Unpublished"
    public string LifecycleStatus { get; set; }
}
```

---

### **ExamContent**

```csharp
public class ExamContent
{
    public string ContentId { get; set; }
    public string ExamId { get; set; }
    public string Question { get; set; }
    public string Answer { get; set; }
    public string Explanation { get; set; }
    public string QuestionType { get; set; } // "MCQ", "TF", "Essay"
    public int Points { get; set; }
    public List<string> Options { get; set; } // For MCQ
}
```

---

### **ExamSubmission**

```csharp
public class ExamSubmission
{
    public string Id { get; set; }
    public string ExamId { get; set; }
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<SubmissionResponse> Responses { get; set; }
    public double TotalScore { get; set; }
    public double TotalPossiblePoints { get; set; }
    public string Status { get; set; } // "Submitted", "Graded", "Released"
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}
```

---

### **SubmissionResponse**

```csharp
public class SubmissionResponse
{
    public string QuestionId { get; set; }
    public int QuestionNumber { get; set; }
    public string Answer { get; set; }
    public double PointsEarned { get; set; }
    public double PointsPossible { get; set; }
    public bool IsCorrect { get; set; }
    public string Feedback { get; set; }
}
```

---

### **ExamSession**

```csharp
public class ExamSession
{
    public string Id { get; set; }
    public string ExamId { get; set; }
    public string Status { get; set; } // "Running", "Paused", "Ended"
    public DateTime StartedAt { get; set; }
    public Dictionary<string, SessionParticipant> Participants { get; set; }
}
```

---

### **SessionParticipant**

```csharp
public class SessionParticipant
{
    public string StudentId { get; set; }
    public string StudentName { get; set; }
    public string ConnectionStatus { get; set; } // "Online", "Disconnected"
    public int ProgressPercent { get; set; }
    public int TimeRemaining { get; set; } // seconds
    public int FlagCount { get; set; }
    public DateTime LastHeartbeat { get; set; }
}
```

---

## Error Handling

All async methods can throw exceptions:

```csharp
try
{
    var exam = await firestoreService.GetPublishedExamAsync("exam123");
}
catch (FirebaseException ex)
{
    // Firebase-specific error
    Console.WriteLine($"Firebase error: {ex.Message}");
}
catch (Exception ex)
{
    // General error
    Console.WriteLine($"Error: {ex.Message}");
}
```

---

## Authentication

Services use Firebase Authentication:

```csharp
// Check if user is authenticated
if (App.FirestoreService == null)
{
    throw new InvalidOperationException("User not authenticated");
}

// Get current user
var currentUser = Firebase.Auth.CurrentUser;
Console.WriteLine($"Logged in as: {currentUser.Email}");
```

---

## Rate Limiting

Firebase has quotas:
- **Reads:** 50,000/day (free tier)
- **Writes:** 20,000/day (free tier)
- **Storage:** 1 GB (free tier)

Consider caching frequently accessed data.

---

## Best Practices

### **1. Always Dispose Resources**
```csharp
using var pdfService = new PdfExportService();
var path = await pdfService.ExportGradebookToPdfAsync(...);
```

### **2. Handle Null Returns**
```csharp
var exam = await firestoreService.GetPublishedExamAsync("exam123");
if (exam == null)
{
    // Handle not found
    return;
}
```

### **3. Use Async/Await Properly**
```csharp
// ? Good
await firestoreService.SavePublishedExamAsync(exam);

// ? Bad
firestoreService.SavePublishedExamAsync(exam).Wait(); // Can deadlock
```

### **4. Batch Operations**
```csharp
// ? Better
var submissions = await firestoreService.GetExamSubmissionsAsync("exam123");
foreach (var submission in submissions)
{
    // Process
}

// ? Worse
for (int i = 0; i < 100; i++)
{
    var submission = await firestoreService.GetSubmissionAsync(ids[i]);
}
```

---

## Extension Points

### **Custom Analytics**

Extend AnalyticsService:

```csharp
public class CustomAnalyticsService : AnalyticsService
{
    public async Task<MyCustomMetric> CalculateCustomMetricAsync(string examId)
    {
        var submissions = await GetExamSubmissionsAsync(examId);
        // Your custom calculation
        return new MyCustomMetric { ... };
    }
}
```

### **Custom Export Formats**

Extend ExportService:

```csharp
public async Task<string> ExportToCustomFormatAsync(...)
{
    // Your export logic
}
```

---

## Support

**API Questions:**
- GitHub Issues: https://github.com/yourusername/ExamForge/issues
- Email: api-support@yourdomain.com

**SDK Updates:**
- Check NuGet for latest packages
- Review changelog for breaking changes

---

**API Version:** 1.0.0  
**Last Updated:** 2024-01-15
