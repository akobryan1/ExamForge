# ExamForge Testing Guide
**Comprehensive Quality Assurance & Industry Standards**

---

## Overview

This guide covers testing ExamForge according to industry standards including:
- **IEEE 829** (Software Test Documentation)
- **ISO/IEC 25010** (Software Quality)
- **WCAG 2.1** (Accessibility)
- **FERPA** (Educational Data Privacy)
- **ISO 27001** (Information Security)

---

## Table of Contents

1. [Test Strategy](#test-strategy)
2. [Functional Testing](#functional-testing)
3. [Performance Testing](#performance-testing)
4. [Security Testing](#security-testing)
5. [Usability Testing](#usability-testing)
6. [Accessibility Testing](#accessibility-testing)
7. [Integration Testing](#integration-testing)
8. [Regression Testing](#regression-testing)
9. [User Acceptance Testing](#user-acceptance-testing)
10. [Load Testing](#load-testing)
11. [Test Metrics](#test-metrics)

---

## Test Strategy

### **Testing Levels**

Following **IEEE 829** standard:

| Level | Scope | Responsibility | Timeline |
|-------|-------|----------------|----------|
| Unit Testing | Individual functions | Developer | During development |
| Integration Testing | Component interactions | Developer/QA | After unit tests |
| System Testing | Complete system | QA Team | Before UAT |
| Acceptance Testing | User requirements | End users | Before production |

### **Test Environment**

**Required Setups:**
1. **Development:** Local machine, test data
2. **Staging:** Mirror of production, realistic data
3. **Production:** Live environment, real data

### **Test Data Management**

- **Synthetic Data:** Generated test students/exams
- **Anonymized Real Data:** Sanitized production data
- **Edge Cases:** Boundary conditions, invalid inputs

---

## Functional Testing

### **Test Coverage Matrix**

Per **ISO/IEC 25010** quality characteristics:

#### **Module 1: Exam Creation**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-EC-001 | Create exam with valid data | Exam saved successfully | Critical | |
| TC-EC-002 | Create exam with missing title | Error message displayed | High | |
| TC-EC-003 | Add MCQ with 4 options | Question saved with options | Critical | |
| TC-EC-004 | Add essay question with rubric | Rubric attached to question | Medium | |
| TC-EC-005 | Delete question from exam | Question removed, points recalculated | Medium | |
| TC-EC-006 | Save to question bank | Question available for reuse | Low | |

**Test Procedure TC-EC-001:**
```
1. Launch ExamForge
2. Navigate to Structure Builder
3. Enter exam details:
   - Title: "Test Exam 001"
   - Subject: "Math"
   - Passing: 60%
4. Click "Save"
5. Verify: Success message appears
6. Verify: Exam appears in exam list
```

#### **Module 2: Exam Publishing**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-EP-001 | Publish complete exam | Exam URL generated | Critical | |
| TC-EP-002 | Publish incomplete exam | Error: Must complete all questions | Critical | |
| TC-EP-003 | Set schedule in past | Error: Schedule must be future | High | |
| TC-EP-004 | Enable anti-cheat settings | Settings saved and applied | High | |
| TC-EP-005 | Generate QR code | QR code displays, scans to exam | Low | |

#### **Module 3: Exam Taking (Student)**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-ET-001 | Access exam via URL | Login page displays | Critical | |
| TC-ET-002 | Login with valid credentials | Exam loads successfully | Critical | |
| TC-ET-003 | Answer MCQ question | Selection saved | Critical | |
| TC-ET-004 | Navigate between questions | Progress saved | Critical | |
| TC-ET-005 | Submit exam | Confirmation screen, submission recorded | Critical | |
| TC-ET-006 | Attempt to access after deadline | Access denied message | High | |
| TC-ET-007 | Lose internet connection | Auto-reconnect, progress restored | High | |
| TC-ET-008 | Tab switch (security on) | Flag logged in integrity log | High | |
| TC-ET-009 | Time runs out | Auto-submit triggered | High | |

#### **Module 4: Live Monitoring**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-LM-001 | View live roster | All active students shown | Critical | |
| TC-LM-002 | Student connects | Appears as "Online" | Critical | |
| TC-LM-003 | Student disconnects | Status changes to "Disconnected" | High | |
| TC-LM-004 | Broadcast message | All students receive popup | High | |
| TC-LM-005 | Pause all exams | All timers freeze | High | |
| TC-LM-006 | Extend time for one student | Only that student's time extended | Medium | |
| TC-LM-007 | View integrity flags | Flagged incidents highlighted | High | |

#### **Module 5: Grading**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-GR-001 | Auto-grade MCQ | Correct answers scored automatically | Critical | |
| TC-GR-002 | Auto-grade True/False | Scored immediately | Critical | |
| TC-GR-003 | Manually grade essay | Points assigned, feedback saved | Critical | |
| TC-GR-004 | Bulk finalize grades | All graded items finalized | High | |
| TC-GR-005 | Release results to students | Status updated, students can view | High | |
| TC-GR-006 | Reopen for makeup | Deadline extended, student can retake | Medium | |

#### **Module 6: Analytics**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-AN-001 | View class overview | Metrics calculated correctly | High | |
| TC-AN-002 | Calculate item analysis | Difficulty, discrimination shown | High | |
| TC-AN-003 | View student drilldown | Individual performance detailed | Medium | |
| TC-AN-004 | Export gradebook (Excel) | File downloads, opens correctly | High | |
| TC-AN-005 | Export gradebook (PDF) | Professional PDF generated | Medium | |
| TC-AN-006 | View integrity timeline | Incidents listed chronologically | High | |

#### **Module 7: Export/Reports**

| Test Case ID | Scenario | Expected Result | Priority | Status |
|--------------|----------|-----------------|----------|--------|
| TC-EX-001 | Export gradebook to Excel | XLSX file with all grades | High | |
| TC-EX-002 | Export student report (PDF) | PDF with questions & answers | Medium | |
| TC-EX-003 | Export integrity report (CSV) | CSV with all incidents | Medium | |
| TC-EX-004 | Export live session report | CSV with current session data | Low | |

---

## Performance Testing

### **Per ISO/IEC 25010 - Performance Efficiency**

#### **Response Time Requirements**

| Operation | Target | Acceptable | Unacceptable |
|-----------|--------|------------|--------------|
| Dashboard load | < 1s | < 2s | > 3s |
| Exam creation save | < 0.5s | < 1s | > 2s |
| Question search | < 0.5s | < 1s | > 2s |
| Gradebook export | < 3s | < 5s | > 10s |
| Live roster refresh | < 1s | < 2s | > 3s |
| Student submission | < 1s | < 2s | > 3s |

#### **Performance Test Cases**

**PT-001: Dashboard Load Time**
```
Setup:
- 100 published exams in database
- 1000 submissions in database

Test Steps:
1. Clear cache
2. Launch ExamForge
3. Measure time to dashboard fully loaded

Pass Criteria: < 2 seconds
```

**PT-002: Large Exam Handling**
```
Setup:
- Create exam with 200 questions
- Add 50 students submissions

Test Steps:
1. Open exam in Content Builder
2. Navigate through questions
3. Load analytics

Pass Criteria:
- Page load < 3 seconds
- No UI freezing
- Smooth scrolling
```

**PT-003: Concurrent Grading**
```
Setup:
- 100 essays in grading queue

Test Steps:
1. Open grading dialog
2. Grade 10 essays in sequence
3. Measure time per essay

Pass Criteria:
- Each grade submission < 1 second
- No lag between essays
```

#### **Resource Utilization**

Monitor during testing:
- **CPU Usage:** Should not exceed 50% sustained
- **Memory Usage:** Should not exceed 1 GB for app
- **Network Bandwidth:** Minimal (mostly Firebase queries)
- **Disk I/O:** Minimal (only exports)

---

## Security Testing

### **Per ISO 27001 & FERPA**

#### **Authentication & Authorization**

| Test Case | Scenario | Pass Criteria |
|-----------|----------|---------------|
| SEC-001 | Login with valid credentials | Access granted |
| SEC-002 | Login with invalid credentials | Access denied, error message |
| SEC-003 | Access instructor features as student | Access denied (403) |
| SEC-004 | SQL injection in login | No database access |
| SEC-005 | XSS in exam question | Sanitized, no script execution |
| SEC-006 | Session timeout (30 min) | Auto-logout after inactivity |
| SEC-007 | Password brute force | Account lockout after 5 attempts |

#### **Data Protection (FERPA Compliance)**

**Test Scenario: Student Privacy**
```
Test Steps:
1. Create 2 student accounts (Student A, Student B)
2. Student A takes exam
3. Student B logs in
4. Attempt to access Student A's submission

Pass Criteria:
- Student B cannot see Student A's data
- Error logged in security log
```

**Test Scenario: Data Encryption**
```
Test Steps:
1. Submit exam with sensitive data
2. Capture network traffic (Wireshark)
3. Inspect Firestore communication

Pass Criteria:
- All data transmitted over HTTPS
- Firebase credentials never in plaintext
- No sensitive data in URL parameters
```

#### **Integrity Monitoring Security**

**Test Scenario: Tab Switch Detection**
```
Test Steps:
1. Student starts exam
2. Switch to another tab/window
3. Return to exam

Pass Criteria:
- Event logged in integrity_incidents
- Student sees warning
- Instructor notified in live monitoring
```

#### **Firestore Security Rules Validation**

Test each rule:
```javascript
// Test: Instructor can read all exams
Allow: Instructor role
Deny: Student role

// Test: Student can only read their own submission
Allow: Student reading own ID
Deny: Student reading other student's ID

// Test: Unauthenticated cannot access anything
Deny: No auth token
```

**Use Firebase Rules Test Suite:**
```bash
firebase emulators:start --only firestore
firebase emulators:test --only firestore
```

---

## Usability Testing

### **Per Nielsen's 10 Heuristics**

#### **Heuristic Evaluation**

| Heuristic | Test | Pass/Fail | Notes |
|-----------|------|-----------|-------|
| Visibility of system status | Does user know what's happening? | | |
| Match system & real world | Familiar terms used? | | |
| User control & freedom | Easy to undo mistakes? | | |
| Consistency | Same actions = same results? | | |
| Error prevention | Design prevents errors? | | |
| Recognition over recall | Minimize memory load? | | |
| Flexibility & efficiency | Shortcuts for experts? | | |
| Aesthetic & minimalist | No unnecessary info? | | |
| Help users recognize errors | Clear error messages? | | |
| Help & documentation | Easy to find help? | | |

#### **Task-Based Usability Test**

**Scenario 1: First-Time Instructor**
```
Task: Create and publish a simple 10-question exam

Observation Points:
- Time to complete: _____ minutes
- Number of errors: _____
- Help requests: _____
- Satisfaction rating (1-5): _____
```

**Success Criteria:**
- Complete in < 20 minutes
- 0-2 errors
- No help needed
- Rating ? 4

#### **Think-Aloud Protocol**

1. Recruit 5-10 instructors
2. Give task: "Create an exam for your course"
3. Ask to vocalize thoughts
4. Observe and record:
   - Confusion points
   - Positive reactions
   - Suggestions

---

## Accessibility Testing

### **Per WCAG 2.1 Level AA**

#### **Automated Testing Tools**

Use:
- **axe DevTools** (browser extension)
- **WAVE** (web accessibility evaluation)
- **Pa11y** (command-line tool)

```bash
# Run Pa11y on public exam page
pa11y http://localhost/exams/exam123.html

# Should return 0 errors for AA compliance
```

#### **Manual Accessibility Checks**

| Criterion | Test | Pass/Fail |
|-----------|------|-----------|
| **1.1.1 Non-text Content** | All images have alt text | |
| **1.4.3 Contrast** | Text contrast ratio ? 4.5:1 | |
| **2.1.1 Keyboard** | All features accessible via keyboard | |
| **2.4.3 Focus Order** | Logical tab order | |
| **3.1.1 Language** | Page language specified | |
| **4.1.2 Name, Role, Value** | All UI components have accessible names | |

**Keyboard Navigation Test:**
```
1. Unplug mouse
2. Navigate entire exam creation workflow using only:
   - Tab (next element)
   - Shift+Tab (previous element)
   - Enter (activate button)
   - Arrow keys (select options)
3. Verify every function accessible

Pass Criteria: Complete workflow keyboard-only
```

**Screen Reader Test:**
```
Tools: NVDA (Windows), JAWS, VoiceOver (Mac)

1. Enable screen reader
2. Navigate exam interface
3. Verify all content announced

Pass Criteria:
- All buttons described
- All form fields labeled
- Navigation landmarks clear
```

---

## Integration Testing

### **Service Integration Tests**

#### **Firebase/Firestore Integration**

**Test: Save & Retrieve Exam**
```csharp
[Test]
public async Task SaveExam_Then_Retrieve_ShouldMatch()
{
    // Arrange
    var firestore = new FirestoreService();
    var exam = new PublishedExam
    {
        Title = "Test Exam",
        Subject = "Math"
    };

    // Act
    var examId = await firestore.SavePublishedExamAsync(exam);
    var retrieved = await firestore.GetPublishedExamAsync(examId);

    // Assert
    Assert.AreEqual("Test Exam", retrieved.Title);
    Assert.AreEqual("Math", retrieved.Subject);
}
```

#### **SignalR Integration**

**Test: Broadcast Message**
```csharp
[Test]
public async Task BroadcastMessage_ShouldReceive()
{
    // Arrange
    var signalR = new SignalRService("https://test.com/hub");
    await signalR.ConnectAsync();
    string received = null;
    
    signalR.OnMessageReceived += (msg) => received = msg;

    // Act
    await signalR.BroadcastMessageAsync("session123", "Test");

    // Assert (wait for async)
    await Task.Delay(1000);
    Assert.AreEqual("Test", received);
}
```

#### **LMS Integration (Canvas)**

**Test: Sync Grade to Canvas**
```csharp
[Test]
public async Task SyncToCanvas_WithValidToken_ShouldSucceed()
{
    // Arrange
    var lmsService = new LmsIntegrationService();
    var submission = new ExamSubmission
    {
        StudentId = "student123",
        TotalScore = 85,
        TotalPossiblePoints = 100
    };

    // Act
    var success = await lmsService.SyncToCanvasAsync(
        "https://canvas.test.edu",
        "test-token",
        "course123",
        submission
    );

    // Assert
    Assert.IsTrue(success);
}
```

---

## Regression Testing

### **Regression Test Suite**

After each change, run regression suite:

**Critical Path Tests (Must pass):**
1. Create exam
2. Publish exam
3. Take exam (student)
4. Grade exam
5. View analytics
6. Export grades

**Automated Regression:**
```bash
# Run full test suite
dotnet test ExamForge.Tests

# Run only critical tests
dotnet test --filter Category=Critical
```

### **Version Comparison Testing**

Before releasing new version:
```
1. Export data from current version
2. Upgrade to new version
3. Import data
4. Verify all features work
5. Compare output files (gradebooks, reports)

Pass Criteria: No data loss, feature parity
```

---

## User Acceptance Testing (UAT)

### **UAT Plan**

**Participants:**
- 5-10 instructors
- 20-30 students
- 2-3 administrators

**Duration:** 2 weeks

**Test Scenarios:**

#### **Instructor UAT Scenarios**

**Scenario 1: Create Midterm Exam**
```
Task: Create a real midterm exam for your course
Timeline: 1 hour

Evaluation:
- Did you complete successfully? Y/N
- Difficulty level (1-5): ___
- Missing features: ___________
- Satisfaction (1-5): ___
```

**Scenario 2: Monitor Live Exam**
```
Task: Monitor a live exam session
Timeline: During actual exam

Evaluation:
- Could you see all students? Y/N
- Were alerts helpful? Y/N
- Any missed incidents?: ___
- Suggestions: ___________
```

#### **Student UAT Scenarios**

**Scenario 1: Take Practice Quiz**
```
Task: Complete 10-question practice quiz
Timeline: 20 minutes

Evaluation:
- Easy to understand? Y/N
- Any technical issues? Y/N
- Exam interface clear? Y/N
- Overall experience (1-5): ___
```

### **UAT Acceptance Criteria**

System passes UAT if:
- ? 90%+ participants complete tasks successfully
- ? Average satisfaction ? 4/5
- ? No critical bugs found
- ? All feedback documented
- ? Users would recommend to colleagues

---

## Load Testing

### **Per ISO/IEC 25010 - Capacity**

#### **Load Test Scenarios**

**Scenario 1: Concurrent Exam Takers**
```
Setup:
- Single exam
- Simulate 500 concurrent students

Test Steps:
1. Spin up 500 virtual users
2. Each starts exam within 5-minute window
3. Each submits after 60 minutes

Measurements:
- Response times (should stay < 3s)
- Error rate (should be 0%)
- Server CPU/Memory (should not spike)
```

**Tools:**
- **JMeter** or **Gatling** for HTTP load testing
- **SignalR Load Test** for WebSocket load

**JMeter Test Plan:**
```xml
<ThreadGroup>
  <numThreads>500</numThreads>
  <rampUp>300</rampUp> <!-- 5 minutes -->
  <loops>1</loops>
</ThreadGroup>

<HTTPSampler>
  <domain>exam.yourschool.edu</domain>
  <path>/exams/exam123.html</path>
  <method>GET</method>
</HTTPSampler>
```

**Scenario 2: Grading Spike**
```
Setup:
- 1000 submissions to grade
- 10 instructors grading simultaneously

Test:
1. Each instructor grades 10 essays/minute
2. Run for 15 minutes

Measurements:
- Firestore write throttling
- UI lag
- Grade save success rate
```

#### **Stress Testing**

**Find Breaking Point:**
```
Gradually increase load:
- 100 users: OK
- 500 users: OK
- 1000 users: ?
- 2000 users: ?

Continue until system fails
Document failure point
```

---

## Test Metrics

### **Coverage Metrics**

**Code Coverage:**
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov

Target: 80% coverage minimum
Critical paths: 100% coverage
```

**Functional Coverage:**
```
Total Test Cases: ____
Executed: ____
Passed: ____
Failed: ____
Blocked: ____

Coverage = (Passed / Total) * 100
Target: > 95%
```

### **Defect Metrics**

**Defect Density:**
```
Defects per 1000 lines of code = (Total Defects / Total LOC) * 1000

Industry Standard: < 1 defect/1000 LOC
```

**Defect Severity Distribution:**
```
Critical: __% (target < 5%)
High: __% (target < 15%)
Medium: __% (target < 30%)
Low: __% (target < 50%)
```

### **Test Execution Metrics**

**Test Execution Rate:**
```
Tests per day = Total Tests / Testing Days
Target: Complete suite in 3-5 days
```

**Pass Rate:**
```
First Run Pass Rate = (Passed / Executed) * 100
Target: > 90%
```

---

## Compliance Testing

### **FERPA Compliance Checklist**

- [ ] Student data encrypted at rest
- [ ] Student data encrypted in transit
- [ ] Access controls implemented
- [ ] Audit logs maintained
- [ ] Consent obtained (where required)
- [ ] Data retention policy defined
- [ ] Data deletion capability exists
- [ ] Privacy policy published

### **GDPR Compliance (if applicable)**

- [ ] Right to access data
- [ ] Right to rectification
- [ ] Right to erasure
- [ ] Right to data portability
- [ ] Consent management
- [ ] Data processing agreements

---

## Test Reporting

### **Test Summary Report Template**

```
TEST SUMMARY REPORT
Date: __________
Version: ExamForge 1.0
Tester: __________

EXECUTIVE SUMMARY:
- Overall Status: Pass / Fail / Conditional
- Test Coverage: ___%
- Pass Rate: ___%
- Critical Issues: __
- Recommendation: Deploy / Hold / Revise

TESTING OVERVIEW:
- Functional Tests: __ passed, __ failed
- Performance Tests: __ passed, __ failed
- Security Tests: __ passed, __ failed
- Usability Tests: __ passed, __ failed
- UAT: __ users, __ satisfaction rating

DEFECTS SUMMARY:
Critical: __
High: __
Medium: __
Low: __

RISKS:
1. [Risk description and mitigation]
2. ...

RECOMMENDATIONS:
1. [Action item]
2. ...

CONCLUSION:
[Overall assessment and go/no-go decision]
```

---

## Continuous Testing

### **CI/CD Integration**

**GitHub Actions Workflow:**
```yaml
name: Test Suite

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
      - name: Code Coverage
        run: dotnet test /p:CollectCoverage=true
```

**Automated Testing Schedule:**
- **Unit Tests:** Every commit
- **Integration Tests:** Every merge to main
- **Full Regression:** Nightly
- **Performance Tests:** Weekly
- **Security Scan:** Weekly

---

## Best Practices

### **Testing Principles**

1. **Test Early, Test Often**
2. **Automate Repetitive Tests**
3. **Test with Real Data (anonymized)**
4. **Document All Tests**
5. **Retest After Fixes**
6. **Maintain Test Data**
7. **Review Test Results**
8. **Continuous Improvement**

### **Test Data Management**

```
Test Data Requirements:
- 1000 synthetic student accounts
- 100 sample exams (various types)
- 10,000 submissions
- 500 integrity incidents
- Edge cases: empty, max length, special characters
```

---

## Industry Certifications

To certify ExamForge as production-ready:

**ISO/IEC 25010 Compliance:**
- [ ] Functional Suitability
- [ ] Performance Efficiency
- [ ] Compatibility
- [ ] Usability
- [ ] Reliability
- [ ] Security
- [ ] Maintainability
- [ ] Portability

**IEEE 829 Documentation:**
- [ ] Test Plan
- [ ] Test Design Specification
- [ ] Test Case Specification
- [ ] Test Procedure Specification
- [ ] Test Log
- [ ] Test Incident Report
- [ ] Test Summary Report

---

## Final Checklist Before Production

- [ ] All critical tests pass
- [ ] Security audit complete
- [ ] Performance benchmarks met
- [ ] Accessibility validated (WCAG 2.1 AA)
- [ ] UAT approved
- [ ] Documentation complete
- [ ] Training completed
- [ ] Support plan established
- [ ] Backup/recovery tested
- [ ] Monitoring configured

---

**Testing Guide Version:** 1.0  
**Compliance Standards:** IEEE 829, ISO/IEC 25010, WCAG 2.1, FERPA, ISO 27001  
**Last Updated:** 2024-01-15  
**Next Review:** Quarterly

**Contact:** qa@yourdomain.edu
