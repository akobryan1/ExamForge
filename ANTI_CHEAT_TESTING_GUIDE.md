# Anti-Cheat System Testing Guide

## What Was Fixed

### 1. Anti-Cheat System
- **Problem**: The anti-cheat JavaScript code was being rendered as visible text on the exam page instead of executing as code.
- **Root Cause**: The generated JavaScript wasn't properly embedded in the HTML `<script>` tags, and there were potential issues with unescaped `</script>` sequences.
- **Solution**: 
  - Added proper escaping of `</script>` sequences in the generated JavaScript
  - Added initialization logging to verify the script runs
  - Properly inserted all anti-cheat event listeners (tab detection, copy/paste prevention, screenshot prevention, one-question-at-a-time navigation)
  - Added console logging to show the anti-cheat configuration is loaded

### 2. Timing Calendar
- **Problem**: Users could accidentally select past dates/times when scheduling exams.
- **Solution**: 
  - Set the `Minimum` property of both DateTimePickers to `DateTime.Today`
  - Added default values (current time for start, current time + 1 hour for end)
  - Past dates are now greyed out and unselectable in the calendar

## How to Test Anti-Cheat Features

### Prerequisites
1. Build and run the application
2. Create a new exam with at least 2 questions
3. Go to the "Anti-Cheat" tab in the Structure Builder
4. Enable the anti-cheat features you want to test

### Test 1: Tab Switching Detection

**Setup:**
1. In Anti-Cheat tab, check "Detect Tabbing"
2. Check "Warning Only"
3. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Open DevTools Console (F12)
3. Verify you see: `Anti-cheat configuration loaded: {...}` with `DetectTabbing: true`
4. Start the exam
5. Switch to another tab or minimize the browser
6. You should see:
   - An alert: "Warning: You switched away from the exam window. This has been recorded."
   - Console message: "Tab switched. Count: 1"

**Expected Result:** ? Alert appears, tab switch is logged

### Test 2: Point Deduction

**Setup:**
1. In Anti-Cheat tab, check "Detect Tabbing"
2. Check "Deduct Points" and enter "5" points
3. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Open DevTools Console (F12)
3. Start the exam
4. Switch tabs
5. Check console for: "Deducted points: 5. Total deducted: 5"
6. Switch tabs again
7. Check console for: "Deducted points: 5. Total deducted: 10"

**Expected Result:** ? Points are deducted each time you switch tabs

### Test 3: Auto-Submit

**Setup:**
1. In Anti-Cheat tab, check "Detect Tabbing"
2. Check "Auto-Submit"
3. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. Switch to another tab
4. You should see:
   - Alert: "You switched away from the exam. The exam will be submitted automatically."
   - The exam should submit automatically

**Expected Result:** ? Exam submits immediately when you switch tabs

### Test 4: Disable Copy/Paste

**Setup:**
1. In Anti-Cheat tab, check "Disable Copy + Pasting"
2. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. Try to:
   - Right-click (should be blocked)
   - Press Ctrl+C to copy (should show alert)
   - Press Ctrl+V to paste (should show alert)
   - Press Ctrl+X to cut (should show alert)

**Expected Result:** ? All copy/paste operations are blocked with alerts

### Test 5: Disable Screenshot

**Setup:**
1. In Anti-Cheat tab, check "Disable Screenshot or PrintScreen"
2. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. Press the "PrintScreen" key
4. You should see an alert: "Screenshot/PrintScreen is disabled during the exam."

**Expected Result:** ? Alert appears when PrintScreen is pressed
**Note:** This is best-effort detection. OS-level screenshot tools (Windows+Shift+S, Snipping Tool) cannot be fully blocked by browser JavaScript.

### Test 6: One Question at a Time

**Setup:**
1. In Anti-Cheat tab, check "One Question at a Time"
2. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. You should see:
   - Only one question visible at a time
   - Navigation bar at the top with "Previous" and "Next" buttons
   - Question counter (e.g., "1/5")
4. Click "Next" to go to the next question
5. Click "Previous" to go back

**Expected Result:** ? Only one question is shown at a time with navigation controls

### Test 7: Disable Backtrack

**Setup:**
1. In Anti-Cheat tab, check "One Question at a Time"
2. Check "Disable Backtrack"
3. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. Answer question 1
4. Click "Next" to go to question 2
5. Try clicking "Previous"
6. The "Previous" button should be disabled/greyed out

**Expected Result:** ? Cannot go back to previous questions

### Test 8: Time Limit Per Question

**Setup:**
1. In Anti-Cheat tab, check "One Question at a Time"
2. Check "Time limit per question" and enter "10" seconds
3. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. You should see:
   - A timer showing remaining time for the current question (e.g., "Time: 10s")
   - Timer counts down
4. Wait 10 seconds without answering
5. The exam should automatically advance to the next question

**Expected Result:** ? Timer counts down, auto-advances when time expires, shows warning at 10 seconds remaining (red text)

### Test 9: Pause/Resume (One Question at a Time)

**Setup:**
1. In Anti-Cheat tab, check "One Question at a Time"
2. Check "Time limit per question"
3. Publish the exam

**Test Steps:**
1. Open the published exam URL
2. Start the exam
3. Click the "Pause" button in the navigation bar
4. Timer should stop counting down
5. Click "Resume" button
6. Timer should continue counting down

**Expected Result:** ? Pause button stops the timer, Resume continues it

## Testing the Timing Calendar

### Test: Cannot Select Past Dates

**Test Steps:**
1. Open the application
2. Go to "Exam Foundry" tab
3. Click "Timing" button
4. Look at the "Start" and "End" DateTimePickers
5. Try to:
   - Click on a past date in the calendar
   - Type a past date

**Expected Result:** ? Past dates are greyed out and unselectable

### Default Values

**Test Steps:**
1. Open a new exam (fresh Timing tab)
2. The "Start" picker should default to current date/time
3. The "End" picker should default to current date/time + 1 hour

**Expected Result:** ? Sensible defaults are set automatically

## Troubleshooting

### Anti-Cheat Not Working

1. **Check Console for Errors:**
   - Press F12 to open DevTools
   - Look for any red error messages
   - Verify you see "Exam JS initializing" and "Anti-cheat configuration loaded"

2. **Verify Configuration:**
   - In console, type: `typeof antiCheatConfig`
   - Should return "object"
   - Type: `antiCheatConfig.DetectTabbing`
   - Should return `true` if you enabled it

3. **Check if JavaScript is Running:**
   - In console, type: `typeof submitExam`
   - Should return "function"
   - If it returns "undefined", the script didn't run

4. **View Page Source:**
   - Right-click ? "View Page Source"
   - Search for "Anti-cheat configuration loaded"
   - Make sure the code is inside `<script>` tags, not visible as text

### Calendar Issues

1. **Past Dates Not Greyed Out:**
   - Make sure you're using the latest build
   - The Loaded event should fire when the control loads
   - Check if `start_datetime_picker.Minimum` is set to today

2. **Can Still Type Past Dates:**
   - The DateTimePicker allows typing, but will validate on blur
   - If you type a past date and tab away, it should reset to the minimum date

## SignalR Integration (Optional)

If you want server-side logging of anti-cheat events:

1. The exam page will attempt to load the SignalR client from CDN
2. It will try to connect to the hub at: `{PublishingServerUrl}/sessionHub`
3. When tab switches occur, it will call `ReportEvent` to log to Firestore
4. Check browser console for SignalR connection messages:
   - "? Connected to SignalR hub" = success
   - "SignalR start failed" = connection failed (not critical, local detection still works)

## Summary

? All anti-cheat features are now functional
? Calendar prevents past date selection
? Proper error handling and logging for debugging
? Best-effort screenshot prevention (browser limitations apply)

If you encounter any issues, check the browser console first for error messages and configuration values.
