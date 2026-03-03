# SignalR Hub Fixes - Anti-Cheat Integration ?

## Issue Identified from Console Logs

```
HubConnection.ts:421 Uncaught (in promise) Error: Failed to invoke 'JoinExamSession' due to an error on the server. HubException: Method does not exist.
HubConnection.ts:421 Uncaught (in promise) Error: Failed to invoke 'LeaveExamSession' due to an error on the server. HubException: Method does not exist.
```

**Root Cause:** The exam HTML was calling `JoinExamSession` and `LeaveExamSession`, but the SignalR Hub only had `JoinSession`.

---

## ? Fixes Applied to ExamForge.SignalRServer\Hubs\SessionHub.cs

### 1. Added `JoinExamSession` Method

```csharp
public async Task JoinExamSession(string examId, string studentId, string studentName, string email)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, examId);
    Console.WriteLine($"[SessionHub] Student joined: {studentName} ({studentId}) for exam {examId}");
    
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
```

**What it does:**
- Adds student to SignalR group for the exam
- Notifies monitors (teacher dashboard) that student joined
- Logs join event to Firestore `session_events` collection
- Console logs for debugging

---

### 2. Added `LeaveExamSession` Method

```csharp
public async Task LeaveExamSession(string examId, string studentId, string studentName)
{
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, examId);
    Console.WriteLine($"[SessionHub] Student left: {studentName} ({studentId}) from exam {examId}");
    
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
```

**What it does:**
- Removes student from SignalR group
- Notifies monitors that student left (submitted or disconnected)
- Logs leave event to Firestore
- Console logs for debugging

---

### 3. Enhanced `ReportEvent` Method

Updated severity mapping to include all anti-cheat event types:

```csharp
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
```

**Changed logging method:**
- Now calls `LogIncidentToFirestoreAsync` instead of `LogSessionEventAsync`
- Saves to `integrity_incidents` collection (not `session_events`)

---

### 4. Added `LogIncidentToFirestoreAsync` Method

```csharp
private async Task LogIncidentToFirestoreAsync(string examId, string studentId, string studentName, string eventType, string details, string severity)
{
    var db = GetFirestoreDb();
    if (db == null) return;

    var docRef = db.Collection("integrity_incidents").Document();
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
```

**What it logs:**
- All anti-cheat violations
- Student identification
- IP address
- Timestamp
- Resolution status (for instructor follow-up)

---

## ?? Firestore Collections Updated

### 1. `session_events` Collection
**Purpose:** General session tracking  
**Used for:**
- STUDENT_JOINED
- STUDENT_LEFT
- Heartbeats
- Session metadata

**Document Structure:**
```json
{
  "SessionId": "exam-guid",
  "StudentId": "12345",
  "StudentName": "John Doe",
  "EventType": "STUDENT_JOINED",
  "Details": "Student joined exam session",
  "Severity": "Info",
  "Timestamp": "2026-03-03T14:07:59.574Z",
  "IpAddress": "203.0.113.42",
  "ConnectionId": "abc123"
}
```

---

### 2. `integrity_incidents` Collection
**Purpose:** Anti-cheat violations  
**Used for:**
- TAB_SWITCH
- COPY_ATTEMPT / PASTE_ATTEMPT
- SCREENSHOT_ATTEMPT
- RETAKE_ATTEMPT / RETAKE_LIMIT_EXCEEDED
- AUTO_SUBMIT

**Document Structure:**
```json
{
  "ExamId": "exam-guid",
  "StudentId": "12345",
  "StudentName": "John Doe",
  "EventType": "TAB_SWITCH",
  "Details": "{\"count\":3,\"timestamp\":\"...\"}",
  "Severity": "Warning",
  "Timestamp": "2026-03-03T14:08:15.123Z",
  "IpAddress": "203.0.113.42",
  "ConnectionId": "abc123",
  "Resolved": false
}
```

---

### 3. `temp_sessions` Collection
**Purpose:** Auto-resume session data  
**Path:** `temp_sessions/{examId}/students/{studentId}`

**Document Structure:**
```json
{
  "ExamId": "exam-guid",
  "StudentId": "12345",
  "StudentName": "John Doe",
  "StudentEmail": "john@example.com",
  "SessionData": {
    "examId": "exam-guid",
    "studentInfo": {...},
    "answers": {...},
    "timeRemaining": 3420,
    "currentQuestionIndex": 2,
    "tabSwitchCount": 1,
    "deductedPoints": 5,
    "timestamp": "2026-03-03T14:08:00.000Z"
  },
  "EventType": "session_saved",
  "Timestamp": "2026-03-03T14:08:30.456Z",
  "Status": "active"
}
```

---

## ?? SignalR Flow Diagram

### Student Joins Exam:
1. Student opens exam URL
2. Enters info ? `startExam()` or `handleGoogleSignIn()`
3. JavaScript calls: `signalRConnection.invoke('JoinExamSession', examId, studentId, name, email)`
4. Hub adds to group: `examId`
5. Notifies monitors: `StudentJoined` event
6. Logs to Firestore: `session_events` collection

### Student Violates Anti-Cheat:
1. Tab switch / copy / screenshot detected
2. JavaScript calls: `reportViolation(eventType, details)`
3. SignalR invokes: `ReportEvent(examId, studentId, name, eventType, detailsJson)`
4. Hub broadcasts to monitors: `IntegrityEvent`
5. Logs to Firestore: `integrity_incidents` collection

### Student Submits Exam:
1. Form submitted ? `submitExam()`
2. Before page changes, JavaScript calls: `signalRConnection.invoke('LeaveExamSession', examId, studentId, name)`
3. Hub removes from group
4. Notifies monitors: `StudentLeft` event
5. Logs to Firestore: `session_events` collection

### Auto-Save Progress (every 30s):
1. Timer triggers ? `saveSessionProgress()`
2. SignalR invokes: `SaveSessionProgress(examId, sessionData, studentId, name, email)`
3. Hub saves to: `temp_sessions/{examId}/students/{studentId}`
4. Console logs confirmation

---

## ? Testing Commands

After deploying the SignalR server to Render.com, test with:

### 1. Check Hub Methods Available:
Open browser console on published exam:
```javascript
console.log(window.signalRConnection.connection.methods);
```

Expected output:
```
{
  "JoinExamSession": ...,
  "LeaveExamSession": ...,
  "ReportEvent": ...,
  "SaveSessionProgress": ...,
  ...
}
```

### 2. Test Join Method:
```javascript
await window.signalRConnection.invoke('JoinExamSession', 'test-exam-id', 'student123', 'Test Student', 'test@example.com');
```

Expected: No errors, console shows "Student joined"

### 3. Test Report Event:
```javascript
await window.signalRConnection.invoke('ReportEvent', 'test-exam-id', 'student123', 'Test Student', 'TAB_SWITCH', '{"count":1}');
```

Expected: Incident appears in Firestore `integrity_incidents`

---

## ?? Deployment Steps for SignalR Server

### Option 1: Redeploy to Render.com
```bash
# From ExamForge.SignalRServer directory
git add .
git commit -m "Add JoinExamSession and LeaveExamSession methods"
git push origin main
```

Render.com will auto-deploy (if connected to GitHub).

### Option 2: Manual Docker Build
```bash
cd ExamForge.SignalRServer
docker build -t examforge-signalr .
docker run -p 5000:8080 \
  -e FIRESTORE_PROJECT_ID=examforge-201e8 \
  -e GOOGLE_APPLICATION_CREDENTIALS_JSON='{ ... }' \
  examforge-signalr
```

---

## ? Expected Console Output After Fix

**Before fix:**
```
? HubException: Method does not exist.
```

**After fix:**
```
? Connected to SignalR hub
[SessionHub] Student joined: John Doe (12345) for exam abc-123
[Auto-Save] Progress saved
? Incident logged: TAB_SWITCH for John Doe
[SessionHub] Student left: John Doe (12345) from exam abc-123
```

---

## ?? Files Modified

1. **ExamForge.SignalRServer\Hubs\SessionHub.cs**
   - Added `JoinExamSession()` method
   - Added `LeaveExamSession()` method
   - Enhanced `ReportEvent()` with new event types
   - Added `LogIncidentToFirestoreAsync()` method
   - Updated severity mapping for all anti-cheat events

---

## ? Build Status

**Build: SUCCESSFUL**  
**Compilation Errors: 0**  
**Ready for Deployment: YES**

Deploy the SignalR server and test again - all "Method does not exist" errors will be resolved! ??
