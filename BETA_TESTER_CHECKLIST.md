# 🧪 ExamForge — Beta Tester Checklist

> **Purpose:** Systematically verify every feature of the ExamForge platform before release.
> **Date Generated:** July 18, 2026
> **Test Environments:**
> - Instructor Dashboard: https://examforge-frontend-vin7.onrender.com
> - Exam Portal: https://examforge-exam-portal.onrender.com
> - Student Portal: https://examforge-student-portal.onrender.com
> - Backend API: https://examforge-backend-taz3.onrender.com

---

## 👤 AUTHENTICATION & ACCOUNT MANAGEMENT

### Email/Password Registration
- [X] **STUDENT:** Navigate to `/signup` and create an account with email/password
- [X] **STUDENT:** Verify username validation (min 3 chars, alphanumeric + underscores only)
- [X] **STUDENT:** Verify password validation (min 8 characters on frontend)
- [X] **STUDENT:** Confirm password mismatch error is shown
- [X] **STUDENT:** After successful signup, auto-redirect to `/dashboard`
- [X] **INSTRUCTOR:** Create an instructor account (role assignment)
- [X] **ALL:** Verify error messages appear for duplicate email registration

### Login / Logout
- [X] **ALL:** Log in with valid email/password
- [X] **ALL:** Verify error message for wrong credentials ("Invalid email or password")
- [X] **ALL:** Verify error message for empty fields
- [X] **ALL:** After login, verify redirect to original requested page (if redirected from a protected route)
- [X] **ALL:** Log out and verify redirect to login page
- [X] **ALL:** Verify that protected routes are inaccessible when logged out (redirect to login)
- [X] **ALL:** Verify "Remember me" session persists across page refreshes

### Google OAuth
- [X] **ALL:** Click "Sign in with Google" button
- [X] **ALL:** Complete Google OAuth flow
- [X] **ALL:** Verify successful login and redirect to dashboard
- [X] **ALL:** Google signup (new account creation via Google)

### Token Management
- [X] **ALL:** Verify token refresh works (session stays alive)
- [X] **ALL:** After token expiry, verify user is redirected to login (not a crash/white screen)
- [X] **ALL:** Clear localStorage tokens manually → verify app handles gracefully (redirect to login)

---

## 📊 DASHBOARD (Instructor)

### Header / Greeting
- [X] Verify time-of-day greeting works (Good morning/afternoon/evening)
- [X] Verify current date display is correct
- [X] Verify user's name is displayed correctly

### Stats Cards
- [X] **Active count:** Matches number of published/active exams
- [X] **To grade count:** Matches number of completed exams requiring grading
- [X] **Draft count:** Matches number of exams in draft status
- [X] **Total count:** Matches total exams across all statuses
- [X] Verify all stat cards update when exams are created/modified

### Recent Exams List
- [ ] Shows up to 5 most recent exams
- [ ] Each row displays: title, subject, question count, status badge
- [ ] Clicking an exam row navigates to that exam's questions page
- [ ] "View all →" link navigates to `/exams`

### Quick Actions
- [ ] "View all exams" button works
- [ ] "Create new exam" button navigates to `/exams/create`

---

## 📝 EXAM MANAGEMENT (Instructor)

### Exams List Page (`/exams`)
- [ ] **Tab navigation:** "Active" tab shows draft/published/active exams
- [ ] **Tab navigation:** "Past" tab shows completed/archived exams
- [ ] Verify exams load and display correctly (title, status badge, spine color)
- [ ] Verify loading spinner shows while fetching
- [ ] Verify error state is handled gracefully
- [ ] "Create new exam" button navigates to create form
- [ ] **Clone exam:** Click clone → verify a new draft is created → auto-redirects to edit
- [ ] **Republish exam:** Click republish on a completed exam → verify status changes
- [ ] **Complete exam:** Click complete → confirmation prompt → verify status becomes "completed"
- [ ] **Delete exam:** Click delete → confirmation prompt → verify exam is removed from list
- [ ] Verify past exams can be republished
- [ ] Verify completed exams show proper badge/styling

### Create/Edit Exam (`/exams/create` or `/exams/:id/edit`)

#### Tab 1: Basic Info
- [ ] Enter title (required)
- [ ] Enter description (required)
- [ ] Enter subject
- [ ] Enter grade level
- [ ] Verify "passing score" field accepts 0–100
- [ ] Verify time limit accepts positive numbers (minutes)
- [ ] **Editing:** Verify existing exam data populates correctly

#### Tab 2: Access Control
- [ ] Toggle sections/classes on/off → verify dynamic fields appear
- [ ] Add/remove section entries
- [ ] Set start date and end date
- [ ] Set access code (password-protected exam)
- [ ] **Access method:** Toggle between "Guest access" and "Student login"
- [ ] "Allowed students" field (if implemented)

#### Tab 3: Timing & Deadline
- [ ] Set time limit (in minutes)
- [ ] Verify auto-submit toggle works

#### Tab 4: Questions & Display
- [ ] Toggle shuffle questions on/off
- [ ] Toggle shuffle answers on/off
- [ ] Toggle show results after submission
- [ ] Toggle allow review after submission

#### Tab 5: Proctoring & Anti-Cheat
- [ ] Enable proctoring
- [ ] Toggle enforce fullscreen
- [ ] Toggle detect tab switch
- [ ] Toggle detect copy/paste
- [ ] Toggle disable right-click
- [ ] Set custom point deductions per violation type (tab switch, copy/paste, right-click, exit fullscreen, etc.)
- [ ] Verify values persist after save

#### Tab 6: Retakes & Late Submission
- [ ] Enable retakes → set max retakes (unlimited / number / none)
- [ ] Toggle "require approval for retake"
- [ ] Set scoring method (best / latest / average)
- [ ] Set late submission policy (allowed / disabled / request permission)
- [ ] Set grace period minutes
- [ ] Set penalty points and penalty interval (per minute/hour/day)

#### Tab 7: Custom Instructions
- [ ] Enter custom instructions text
- [ ] Toggle "show rules before exam"
- [ ] Verify instructions display in PreExamRules component

#### Save & Navigation
- [ ] **Create:** Submit form → verify exam appears in list
- [ ] **Edit:** Save changes → verify updates persist after reload
- [ ] Verify tab navigation animations work
- [ ] Verify form validation errors show inline
- [ ] Verify success toast appears on save

---

## ❓ QUESTION MANAGEMENT (Instructor)

### Questions Page (`/exams/:examId/questions`)
- [ ] Verify exam details load (title, status, stats)
- [ ] Verify existing questions list loads

### Create Question
- [ ] **Multiple Choice:** Add question text, set choices, mark correct answer, set points/difficulty
- [ ] **True/False:** Add question text, select correct answer (True/False), set points
- [ ] **Modified True/False:** Add question text, answer + correction field
- [ ] **Identification:** Add question text, set correct answer, set points
- [ ] **Enumeration:** Add question text, add enumerated items, set points
- [ ] **Essay:** Add question text, set max points
- [ ] Verify all question types appear in type dropdown
- [ ] Verify conditional fields show/hide based on question type
- [ ] Verify points validation (min 1)
- [ ] Verify difficulty selection (easy/medium/hard)

### Edit/Delete Question
- [ ] Click edit → verify form populates with existing data
- [ ] Modify fields → save → verify changes persist
- [ ] Delete question → confirmation → verify removed from list
- [ ] Verify question count and total points update correctly on exam

### AI Question Generator (`/exams/:examId/ai-generate`)
- [ ] **Step 1:** Paste material text, optionally set topic
- [ ] **Step 2:** Select question type, count (1–20), difficulty
- [ ] Click "Generate Questions" → verify loading state
- [ ] **Step 3:** Review generated questions in list
- [ ] Toggle individual question selection checkboxes
- [ ] Click "Import Selected" → verify questions are added to exam
- [ ] Navigate to questions page → verify imported questions appear
- [ ] Verify error handling if API key is not configured
- [ ] Verify "using placeholder" notice appears when no real API key

---

## 🧑‍🎓 STUDENT / EXAMINEE FLOWS

### Student Self-Registration (`/register/student`)
- [ ] Fill all required fields (email, studentId, studentName, password, confirm)
- [ ] Verify validation for empty fields
- [ ] Verify password mismatch error
- [ ] Verify password min length (6 chars)
- [ ] Successful registration → redirect to login
- [ ] Attempt duplicate registration → verify error

### Examinee Registration (via Instructor Link)
- [ ] Navigate to student portal with `?instructor=...` parameter
- [ ] Verify registration fields load (sections, years, courses if configured)
- [ ] Fill in all required fields (name, studentId, section, year, course, email, password)
- [ ] Verify field-level validation errors
- [ ] Submit → verify success screen with "Go to login" button
- [ ] Verify instructor-invite-only access (no instructor param = error message)

### Exam Preview (Student-Side)
- [ ] Navigate to exam preview via public link
- [ ] Verify exam details display: title, description, subject, question count, points, time limit, passing score, status
- [ ] Verify "Start exam" button is enabled only for published/active exams
- [ ] Verify "Exam not available" message for draft/completed/archived exams
- [ ] Verify exam not found page for invalid exam IDs
- [ ] **Exam Portal:** Verify auto-redirect to login if exam requires student login

### Taking an Exam

#### Pre-Exam (if configured)
- [ ] **Access Code:** Enter correct code → proceed
- [ ] **Access Code:** Enter wrong code → error
- [ ] **Pre-Exam Rules:** If enabled, verify rules modal appears with:
  - [ ] Custom instructions text
  - [ ] Exam details (question count, points, time limit)
  - [ ] Proctoring measures listed with point deductions
  - [ ] Retake policy displayed
  - [ ] Late submission policy displayed
  - [ ] Accept / Cancel buttons
- [ ] **Guest info prompt** (if guest access): Enter name, studentId, course, year

#### During the Exam
- [ ] **Multiple Choice:** Select an option → verify selection highlighted
- [ ] **True/False:** Select True or False
- [ ] **Modified True/False:** Select True/False → if False, verify correction textarea appears
- [ ] **Identification:** Type answer in text input
- [ ] **Enumeration:** Type items (one per line) in textarea
- [ ] **Essay:** Type answer in textarea
- [ ] **Timer:** Verify countdown timer is visible and counting down
- [ ] **Timer:** Verify auto-submit when time runs out
- [ ] **Auto-save:** Verify answers are saved automatically every 30 seconds
- [ ] **Question navigation:** Navigate between questions (next/previous)
- [ ] **Question navigation:** Click question numbers to jump
- [ ] Verify answered questions are visually distinguished from unanswered
- [ ] **Submit:** Click submit → confirmation prompt → verify exam is submitted

#### Proctoring Features
- [ ] **Fullscreen enforcement:** Verify browser enters fullscreen on exam start
- [ ] **Fullscreen exit:** Exit fullscreen → verify violation is recorded (exit_fullscreen)
- [ ] **Tab switch detection:** Switch to another tab → verify violation recorded (tab_switch)
- [ ] **Copy/Paste detection:** Try Ctrl+C / Ctrl+V → verify violation recorded
- [ ] **Right-click:** Right-click anywhere → verify violation recorded + context menu suppressed

### Exam Results
- [ ] After submission, verify redirect to results page
- [ ] Verify score, percentage, and pass/fail display
- [ ] Verify question-by-question review (if enabled)
- [ ] Verify correct/incorrect answers shown (if enabled)
- [ ] Verify point deductions for violations shown (if any)
- [ ] Verify late submission penalty displayed (if applicable)
- [ ] Verify "Review exam" button works (if allowReview enabled)

---

## ✅ GRADING (Instructor)

### Grading Queue (`/grading`)
- [ ] Verify queue lists all pending/graded essay and modified true/false answers
- [ ] **Filter:** Toggle between Pending / Graded / All
- [ ] Select a pending item → verify detail panel opens
- [ ] View question text, student answer, max points

### Manual Grading
- [ ] Enter earned points
- [ ] Enter feedback text
- [ ] Optionally enter justification
- [ ] Submit grade → verify success
- [ ] Verify graded item moves to "Graded" filter

### AI-Assisted Grading
- [ ] Enter AI API key (from OpenRouter or configured in Settings)
- [ ] Select AI model
- [ ] Optionally enter key points and model answer for grading criteria
- [ ] Click "AI Grade" → verify AI processes and returns:
  - [ ] Score (number)
  - [ ] Feedback text
  - [ ] Justification text
- [ ] Verify score is auto-populated into grade field
- [ ] Verify AI feedback is auto-populated into feedback field
- [ ] Verify error handling if API call fails
- [ ] Verify error handling if AI returns invalid JSON

---

## 📈 ANALYTICS (Instructor)

### Analytics Page (`/analytics`)
- [ ] Select an exam from the dropdown
- [ ] Verify overview metrics: total attempts, average score, pass rate, average time
- [ ] Verify score distribution chart (bar chart)
- [ ] Verify time vs score scatter plot
- [ ] Verify question-by-question statistics (correct rate, average points)
- [ ] Verify empty state when no exams exist
- [ ] Verify "Select an Exam" prompt when none selected
- [ ] Verify loading state

---

## 🚨 INCIDENT REPORTS (Instructor)

### Incident Reports Page (`/incidents`)
- [ ] Verify incident list loads (student name, exam title, event type, severity, timestamp)
- [ ] **Filter by status:** All / Active / Archived
- [ ] **Filter by severity:** All / Low / Medium / High
- [ ] **Search:** Search by student name, student ID, exam title, event type
- [ ] Verify color-coded severity badges (red=high, amber=medium, green=low)

### Bulk Actions
- [ ] Select individual incidents via checkboxes
- [ ] Click "Select All" checkbox
- [ ] **Archive:** Select incidents → click Archive → verify archived
- [ ] **Unarchive:** Switch to Archived filter → select incidents → Unarchive
- [ ] **Delete:** Select incidents → Delete → confirmation → verify removed
- [ ] Verify incident count updates after actions

---

## 👥 STUDENT MANAGEMENT (Instructor)

### Student Management Page (`/students`)
- [ ] View registered students list (name, course, year, section)
- [ ] **Search:** Type search query → verify debounce (1.2s delay) → results filter
- [ ] Verify loading state

### Registration Link Generation
- [ ] Click "Generate link" → verify link appears
- [ ] Click "Copy link" → verify copied to clipboard
- [ ] Verify link format: `{STUDENT_PORTAL_URL}/register/exam?instructor={instructorId}`

### Registration Fields Configuration
- [ ] Add sections (dynamic add/remove)
- [ ] Add years (dynamic add/remove)
- [ ] Add courses (dynamic add/remove)
- [ ] Save fields → verify persistence on reload
- [ ] Verify fields reflect in student registration form

### Submitted Papers
- [ ] Switch to "Papers" tab
- [ ] View all submitted exam papers with student info
- [ ] Verify papers auto-refresh every 30 seconds

---

## 📤 EXPORT FUNCTIONALITY

### Exam Papers Export
- [ ] Open Export panel (type: Papers)
- [ ] Select column preset or custom columns
- [ ] Apply filters: section, exam, status
- [ ] Export to CSV → verify file downloads with correct columns
- [ ] Export to PDF → verify file downloads with correct formatting

### Incident Reports Export
- [ ] Open Export panel (type: Incidents)
- [ ] Select column preset or custom columns
- [ ] Apply filters: section, exam, severity, archived status
- [ ] Export to CSV → verify file downloads
- [ ] Export to PDF → verify file downloads

---

## 🔔 NOTIFICATIONS

- [ ] Verify notification bell icon displays in header
- [ ] Verify unread count badge shows correct number
- [ ] Click bell → verify dropdown opens with recent notifications
- [ ] Click a notification → verify it marks as read
- [ ] "Mark all as read" button works
- [ ] "View all" navigates to notifications page (if applicable)
- [ ] Verify polling works (new notifications appear every 30s)
- [ ] Verify notification link navigation works (click → navigate to relevant page)
- [ ] Verify empty state when no notifications

---

## ⚙️ SETTINGS

### Settings Page (`/settings`)
- [ ] Verify settings load from backend
- [ ] Change AI model from dropdown
- [ ] Enter new API key (masked display for existing key)
- [ ] Save settings → verify success message
- [ ] Reload page → verify settings persist
- [ ] Verify API key is never sent back unmasked from backend
- [ ] Verify "saving" loading state

---

## 📁 FILE UPLOADS

- [ ] Upload a file (PDF, Word, PowerPoint, text, image)
- [ ] Verify file type restrictions (only PDF, Word, PPT, text, images allowed)
- [ ] Verify file size limits (100MB for materials, 25MB for attachments)
- [ ] View uploaded files list
- [ ] Download a file → verify correct content
- [ ] Delete a file → verify removed from list
- [ ] Upload as material type (associated with exam)
- [ ] Upload as general attachment

---

## 🧭 NAVIGATION & UI

### Sidebar
- [ ] Desktop: sidebar is always visible
- [ ] Mobile: hamburger menu toggles sidebar
- [ ] Mobile: backdrop overlay appears when sidebar is open
- [ ] Escape key closes sidebar on mobile
- [ ] Navigation highlights active route
- [ ] All nav items are accessible:
  - Dashboard, Exams, Create Exam, Students, Grading Queue, Analytics, Incident Reports, Settings
- [ ] Instructor-only items are hidden for students
- [ ] Student-only items (My Exams, Results) shown for student role

### Responsive Design
- [ ] Test at 1920×1080 (desktop)
- [ ] Test at 1024×768 (tablet landscape)
- [ ] Test at 768×1024 (tablet portrait)
- [ ] Test at 375×667 (mobile)
- [ ] Verify no horizontal scroll on any viewport
- [ ] Verify all forms are usable on mobile
- [ ] Verify data tables scroll horizontally on small screens

### Theme & Styling
- [ ] Verify ledger/grade-book theme is consistent across all pages
- [ ] Verify all stamp badges display correctly (draft, active, completed, archived)
- [ ] Verify animations work (Framer Motion transitions)
- [ ] Verify no broken images or icons
- [ ] Verify text is readable on all backgrounds
- [ ] Verify hover states on all interactive elements

---

## ⚠️ ERROR HANDLING & EDGE CASES

### Loading States
- [ ] Verify loading spinners/skeletons on all data-fetching pages
- [ ] Verify loading states don't cause layout shifts

### Empty States
- [ ] Dashboard with no exams: display "No exams yet"
- [ ] Exams page with no exams: display appropriate message
- [ ] Grading queue with no items: display "No items to grade"
- [ ] Analytics with no data: display "No Data Available"
- [ ] Incidents with no reports: display appropriate message
- [ ] Students list empty: display appropriate message

### Error States
- [ ] Network offline → verify graceful error messages (no white screen)
- [ ] Invalid exam ID → "Exam not found" page
- [ ] 401 Unauthorized → redirect to login
- [ ] 403 Forbidden → appropriate error message
- [ ] Server 500 error → generic error message (not stack trace in production)
- [ ] 404 route → redirect to dashboard

### Form Validation
- [ ] All required fields show validation errors
- [ ] Email format validation
- [ ] Password strength/length validation
- [ ] Number fields accept only valid numbers
- [ ] URL/links open correctly
- [ ] Form submission disabled while loading

---

## 🔗 CROSS-PORTAL TESTING

### Exam Portal (https://examforge-exam-portal.onrender.com)
- [ ] Exam preview works with public exam link
- [ ] Exam login page loads and authenticates
- [ ] Take exam flow works end-to-end
- [ ] Submit exam and view results
- [ ] Verify auto-logout on tab close (localStorage cleared)
- [ ] Verify guest access flow

### Student Portal (https://examforge-student-portal.onrender.com)
- [ ] Student registration via instructor link
- [ ] Student login
- [ ] Exam preview and take exam
- [ ] View results after submission
- [ ] Verify registration form uses instructor-configured fields (sections, years, courses)

### CORS & Cross-Origin
- [ ] Verify API calls from all portals succeed
- [ ] Verify cookies (refreshToken) are sent correctly cross-origin
- [ ] Verify WebSocket/SignalR connections work (if applicable)

---

## 🛡️ SECURITY CHECKS

- [ ] Unauthenticated users cannot access protected routes
- [ ] Students cannot access instructor-only routes (Exams API returns 403)
- [ ] Instructors can only view their own students and exams
- [ ] Access code prevents unauthorized exam access
- [ ] API keys are masked in settings (never fully exposed)
- [ ] JWT tokens expire and refresh correctly
- [ ] httpOnly cookies used for refresh tokens
- [ ] CORS blocks unauthorized origins
- [ ] Helmet security headers are applied
- [ ] File uploads are restricted by MIME type
- [ ] No sensitive data in client-side logs

---

## 📱 PERFORMANCE

- [ ] Dashboard loads within 3 seconds
- [ ] Exam list loads within 3 seconds
- [ ] Exam creation/editing is responsive
- [ ] Question CRUD operations are snappy (< 1s)
- [ ] Analytics page renders charts without lag
- [ ] Incident reports page handles 100+ records
- [ ] Export generates file within 5 seconds
- [ ] Exam taking experience is smooth (no lag on question navigation)
- [ ] Auto-save doesn't cause UI jank
- [ ] Notification polling doesn't degrade performance

---

## 🐛 KNOWN ISSUES TO WATCH

- [ ] Verify "modified_true_false" answer parsing works correctly (expects `false__correction` format)
- [ ] Verify auto-complete logic for past end dates works (auto-mark as "completed")
- [ ] Verify cached exam list invalidates after create/update/delete
- [ ] Verify Firestore flat index (`exams` collection) stays in sync with subcollection data
- [ ] Verify guest user flow works without breaking instructor data isolation
- [ ] Verify late submission penalty calculations are correct (not percentage, flat deduction)
- [ ] Verify retake scoring method (best/latest/average) is correctly applied
- [ ] Verify multiple-choice correct answer index mapping (0-based)
- [ ] Verify student ID assignment works correctly during registration

---

## ✅ TEST RESULTS SUMMARY

| Area | Tests Passed | Tests Failed | Notes |
|------|-------------|-------------|-------|
| Authentication | / | / | |
| Dashboard | / | / | |
| Exam Management | / | / | |
| Questions | / | / | |
| AI Generator | / | / | |
| Student Registration | / | / | |
| Taking Exam | / | / | |
| Proctoring | / | / | |
| Grading | / | / | |
| Analytics | / | / | |
| Incident Reports | / | / | |
| Student Management | / | / | |
| Export | / | / | |
| Notifications | / | / | |
| Settings | / | / | |
| File Uploads | / | / | |
| Navigation/UI | / | / | |
| Error Handling | / | / | |
| Security | / | / | |
| Performance | / | / | |

**Overall Status:** ⬜ Not Started / 🟡 In Progress / 🟢 Pass / 🔴 Fail

**Tester Name:** \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_

**Test Date:** \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_

**Browser(s) Used:** \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_

**Device(s) Used:** \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_

**Issues Found:** \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_

**Feedback / Suggestions:** \_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_\_
