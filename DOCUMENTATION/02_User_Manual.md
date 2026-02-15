# ExamForge User Manual
**Version 1.0 - Complete Guide for Instructors and Students**

---

## Table of Contents
1. [Getting Started](#getting-started)
2. [Dashboard Overview](#dashboard-overview)
3. [Creating Exams](#creating-exams)
4. [Publishing Exams](#publishing-exams)
5. [Live Monitoring](#live-monitoring)
6. [Grading](#grading)
7. [Analytics](#analytics)
8. [Student Guide](#student-guide)
9. [Troubleshooting](#troubleshooting)

---

## Getting Started

### **First Launch**

1. **Open ExamForge** from desktop or start menu
2. **Login** with your institutional credentials
3. **Dashboard** will display with quick stats

### **Interface Overview**

- **Sidebar (Left):** Navigation between modules
- **Main Area:** Current module content
- **Top Bar:** User info, settings, notifications

---

## Dashboard Overview

### **Quick Statistics**

The dashboard shows at-a-glance metrics:
- **Total Exams Created:** All your exams
- **Pending Review:** Essays awaiting grading
- **Active Sessions:** Currently running exams
- **Recent Activity:** Latest exam submissions

### **Recent Exams**

View your 5 most recent exams with:
- Exam title
- Status (Draft, Published, Completed)
- Student count
- Average score

### **Quick Actions**

- **Create New Exam:** Start building an exam
- **View Analytics:** Jump to analytics dashboard
- **Grading Queue:** Access essays to grade

---

## Creating Exams

### **Step 1: Structure Your Exam**

1. **Click** "Create New Exam" or sidebar "Structure"
2. **Enter Exam Details:**
   - Title: e.g., "Midterm Exam - Biology 101"
   - Description: Brief overview
   - Subject: Select or create new
   - Passing Score: Default 60%

3. **Define Sections:**
   - Click "+ Add Section"
   - Name: e.g., "Multiple Choice Questions"
   - Description: Optional section details
   - Allocate points per section

**Example Structure:**
```
Exam: Final Exam - Mathematics
??? Section 1: Multiple Choice (40 points, 20 questions)
??? Section 2: True/False (20 points, 10 questions)
??? Section 3: Essay Questions (40 points, 4 questions)
```

### **Step 2: Add Questions**

1. **Navigate to "Content" tab**
2. **Click "+ Add Question"**
3. **Choose Question Type:**
   - Multiple Choice (MCQ)
   - True/False
   - Essay/Short Answer
   - Modified True/False

#### **Multiple Choice Questions:**

1. **Enter Question:** "What is the capital of France?"
2. **Add Options:**
   - Click "+ Add Option"
   - Enter each choice (A, B, C, D)
3. **Select Correct Answer:** Choose from dropdown
4. **Assign Points:** Default 2 points
5. **Add Explanation:** (Optional) Shown after grading

**Tips:**
- ? Make distractors plausible
- ? Avoid "All of the above" unless necessary
- ? Randomize option order

#### **True/False Questions:**

1. **Enter Statement:** "The Earth revolves around the Sun."
2. **Select Correct Answer:** True or False
3. **Assign Points:** Default 1 point
4. **Add Explanation:** Clarify why

#### **Essay Questions:**

1. **Enter Prompt:** "Explain the process of photosynthesis..."
2. **Assign Points:** Usually 5-10 points
3. **Add Rubric:** (Recommended)
   - Content accuracy: 4 points
   - Organization: 3 points
   - Grammar: 3 points
4. **Set Word Limit:** (Optional) Min/max words

#### **Modified True/False:**

1. **Enter Statement:** "Napoleon was born in 1769 in Corsica."
2. **Mark Correct/Incorrect Parts**
3. **Student corrects if false**

### **Step 3: Question Bank**

Save frequently used questions:

1. **After creating question:** Click "Save to Bank"
2. **Add tags:** e.g., "biology, cells, easy"
3. **To reuse:** Click "Import from Bank" ? Search ? Insert

### **Step 4: Media and Formatting**

- **Add Images:** Click ?? icon ? Upload
- **Format Text:** Use toolbar for bold, italic, lists
- **Math Equations:** Use LaTeX syntax: `$E = mc^2$`

---

## Publishing Exams

### **Review Before Publishing**

1. **Click "Review" tab**
2. **Check:**
   - All questions complete ?
   - Point values correct ?
   - Sections balanced ?
   - No typos ?

### **Schedule Configuration**

1. **Live Exam Window:**
   - Start Date/Time: When students can begin
   - End Date/Time: Deadline for submissions
   - Example: Mar 15, 2024 9:00 AM - Mar 15, 2024 11:00 AM

2. **Exam Duration:**
   - Time allowed per student: e.g., 90 minutes
   - Students can start anytime within window
   - Timer counts down from duration

### **Anti-Cheat Settings**

Configure security measures:

- ?? **Disable Copy/Paste:** Prevent text copying
- ?? **Fullscreen Lock:** Exam must be fullscreen
- ?? **Tab Switch Detection:** Alert on focus loss
- ?? **Disable Right-Click:** Prevent context menu
- ?? **Randomize Questions:** Shuffle order
- ?? **Randomize Options:** Shuffle MCQ choices
- ?? **Prevent Back Navigation:** Can't return to previous questions
- ?? **IP Address Logging:** Track where taken

**Recommended for High-Stakes:**
- Enable all options
- Use proctoring software separately
- Require webcam monitoring

### **Publish!**

1. **Click "Publish Exam"**
2. **Confirmation dialog** shows:
   - Exam will be accessible at: [URL]
   - Schedule: [Dates]
   - Security: [Settings enabled]
3. **Click "Confirm"**
4. **Copy Exam URL** to share with students

### **Sharing Exam URL**

**Option 1: Copy Link**
- Click "Copy URL" button
- Paste in LMS, email, or announcement

**Option 2: QR Code**
- Click "Generate QR Code"
- Display in classroom
- Students scan to access

**Option 3: LMS Integration**
- Click "Sync to LMS"
- Configure Canvas/Blackboard/Moodle
- Auto-publish to course

---

## Live Monitoring

### **View Active Sessions**

1. **Navigate to "Published Exams" ? "Live" tab**
2. **See real-time roster:**
   - Student name
   - Connection status (Online/Disconnected)
   - Progress percentage
   - Time remaining
   - Flag count (integrity alerts)

### **Live Metrics**

Top of screen shows:
- **Online Students:** Currently connected
- **Disconnected:** Lost connection
- **Average Progress:** Overall completion %
- **Total Flags:** Integrity incidents

### **Emergency Controls**

#### **Broadcast Message:**
1. Click "?? Broadcast Message"
2. Type message: e.g., "5 minutes remaining!"
3. Click "Send"
4. All students see popup immediately

#### **Pause All Exams:**
1. Click "?? Pause All"
2. Confirm action
3. All student timers freeze
4. Use for technical issues or announcements

#### **Extend Time:**
1. Right-click student
2. "Extend Time" ? Add minutes
3. Individual student's timer extended

### **Monitor Integrity**

Watch for red flags:
- ?? **Tab Switch:** Student left exam window
- ?? **Focus Loss:** Exam lost focus
- ?? **Multiple Logins:** Suspicious login patterns
- ?? **Copy Attempt:** Tried to copy text

**Actions:**
- Review after exam
- Contact student
- Flag for review

---

## Grading

### **Auto-Graded Questions**

MCQ and True/False are **automatically graded** when submitted:
- Instant results for students (if enabled)
- Scores in gradebook immediately
- No instructor action needed

### **Manual Grading (Essays)**

1. **Navigate to "Published Exams" ? "Closed/Grading" tab**
2. **Pending Review** section shows essays awaiting grading
3. **Click "Review/Grade"** on a submission

#### **Grading Dialog:**

Shows:
- Student name
- Question text
- Student's essay response
- Rubric (if defined)
- Point allocation

**To Grade:**
1. Read essay carefully
2. Assign points based on rubric
3. Add feedback comments (optional)
4. Click "Submit Grade"

**Tips:**
- ? Use rubric consistently
- ? Provide constructive feedback
- ? Grade in batches for efficiency
- ? Take breaks to maintain fairness

### **Bulk Operations**

After grading multiple essays:
1. Click "Bulk Finalize"
2. All graded items finalized
3. Student scores updated

### **Release Results**

When ready to show students their grades:
1. Select exam
2. Click "Release Results"
3. Students can now view scores and feedback

### **Reopen for Makeup**

Allow student to retake:
1. Select exam
2. Click "Reopen for Makeup"
3. Extends deadline by 7 days
4. Previous submission preserved

---

## Analytics

### **Class Overview**

View aggregated metrics:
- **Class Average:** Mean score
- **Pass Rate:** % scoring ? 60%
- **Completion Rate:** % who submitted
- **Score Distribution:** Histogram of grades

**Use for:**
- Identifying if exam was too hard/easy
- Spotting trends in class performance
- Reporting to administration

### **Item Analysis**

See question-level statistics:
- **Difficulty:** % who got it right
- **Discrimination:** Separates high/low performers
- **Point-Biserial:** Correlation with total score
- **Quality Indicator:** Excellent/Good/Fair/Poor

**Red Flags:**
- ?? Difficulty < 20%: Too hard, review question
- ?? Discrimination < 0.2: Doesn't distinguish ability
- ?? Negative correlation: High scorers getting it wrong!

**Actions:**
- Click "Details" for full distractor analysis
- Consider removing bad questions from future exams
- Adjust grading if question was flawed

### **Student Drilldown**

Analyze individual performance:

1. **Select student** from list
2. **View metrics:**
   - Total score and rank
   - Time taken
   - Flags (integrity incidents)
   - Mastery by topic

3. **View Full Submission:**
   - Click "?? View Full Submission"
   - See all questions and answers
   - Compare correct vs. student answers
   - Export student report (PDF/CSV)

**Use for:**
- Parent conferences
- Academic advising
- Extra help targeting

### **Integrity & Incidents**

Review flagged behavior:
- Tab switches during exam
- Focus loss events
- Multiple login attempts
- IP address changes

**Timeline** shows:
- Timestamp
- Student
- Event type
- Severity
- Details

**Export Report:**
- Click "Export Report"
- PDF/CSV of all incidents
- For academic integrity board

### **Trends Over Time**

Compare performance across exams:
- Line chart of class averages
- Identify improving/declining trends
- Spot learning gaps

---

## Student Guide

### **For Students Taking Exams**

#### **Accessing the Exam:**

1. **Receive exam URL** from instructor
2. **Open URL** in modern browser (Chrome, Firefox, Edge)
3. **Enter credentials:**
   - Student ID
   - Name
   - Access code (if required)

#### **Taking the Exam:**

1. **Read instructions** carefully
2. **Answer questions** one at a time
3. **Timer** shows time remaining
4. **Flag questions** to review later
5. **Navigate** using Previous/Next buttons

**Tips for Students:**
- ? Read all questions before starting
- ? Answer easy questions first
- ? Use flagging for questions to review
- ? Check answers before submitting
- ? Don't refresh page during exam

#### **Technical Issues:**

If connection lost:
1. **Don't panic!** Answers are auto-saved
2. **Refresh page** and log back in
3. **Continue** where you left off

If timer wrong:
- Contact instructor immediately
- They can extend time if needed

#### **After Submission:**

1. **Confirmation screen** appears
2. **Note submission ID** for records
3. **Results** released by instructor
4. **View feedback** when available

---

## Troubleshooting

### **Common Issues and Solutions**

#### **"Can't connect to Firebase"**
- Check internet connection
- Verify firewall not blocking
- Contact IT support

#### **"Exam URL not working"**
- Check exam is published
- Verify within exam time window
- Try different browser

#### **"Students not showing in Live tab"**
- Ensure SignalR server is running
- Check students are online
- Refresh ExamForge

#### **"Auto-grading not working"**
- Verify correct answers are set
- Check question type supports auto-grading
- Re-save exam and test

#### **"Can't export grades"**
- Ensure export folder has write permissions
- Check disk space
- Try different format (PDF vs CSV)

#### **"PDF export failing"**
- Verify iText7 package installed
- Check file path permissions
- Try CSV export as alternative

---

## Keyboard Shortcuts

**Global:**
- `Ctrl + N` - New Exam
- `Ctrl + S` - Save
- `Ctrl + P` - Publish
- `F5` - Refresh

**During Grading:**
- `Tab` - Next field
- `Enter` - Submit grade
- `Esc` - Cancel

---

## Best Practices

### **Exam Creation:**
- ? Test exam before publishing
- ? Have colleague review questions
- ? Use mix of question types
- ? Include explanations for learning
- ? Set realistic time limits

### **Live Monitoring:**
- ? Monitor actively during high-stakes exams
- ? Have TA help with large classes
- ? Broadcast time warnings (30 min, 10 min, 5 min)
- ? Document any issues immediately

### **Grading:**
- ? Grade all of question 1, then all of question 2 (consistency)
- ? Hide student names to reduce bias
- ? Use rubrics religiously
- ? Take breaks to maintain focus
- ? Review edge cases with department head

### **Analytics:**
- ? Review item analysis after every exam
- ? Adjust future exams based on data
- ? Share insights with department
- ? Use trends to improve teaching

---

## Support

**Need Help?**
- Email: support@yourinstitution.edu
- Phone: (555) 123-4567
- Hours: Mon-Fri 8am-5pm

**Resources:**
- Video tutorials: [Link to videos]
- FAQ: [Link to FAQ]
- Community forum: [Link to forum]

---

## Appendix

### **Question Type Reference**

| Type | Auto-Grade | Partial Credit | Best For |
|------|------------|----------------|----------|
| MCQ | Yes | No | Knowledge recall, application |
| True/False | Yes | No | Verification, quick assessment |
| Essay | No | Yes | Deep understanding, synthesis |
| Short Answer | No | Yes | Brief explanations |
| Modified T/F | Partial | Yes | Correction of misconceptions |

### **Scoring Rubric Template**

```
Criteria             | Excellent (10) | Good (7) | Fair (4) | Poor (0)
--------------------|----------------|----------|----------|----------
Content Accuracy    | Fully correct  | Mostly   | Partial  | Incorrect
Organization        | Clear flow     | Adequate | Unclear  | Confusing
Evidence/Examples   | Strong support | Some     | Minimal  | None
Writing Quality     | Polished       | Good     | Errors   | Major issues
```

---

**Thank you for using ExamForge!**
*Making assessment better, one exam at a time.* ??
