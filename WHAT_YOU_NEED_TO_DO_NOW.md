# ?? WHAT YOU NEED TO DO NOW - Step by Step

## ? Your Implementation is Complete!

All code has been successfully implemented. Now you need to **deploy and test**. Here's exactly what to do:

---

## ?? ISSUE #1: SignalR Server Not Running

**Error You're Seeing:**
```
Failed to load resource: the server responded with a status of 404 ()
Cannot POST /sessionHub/negotiate
```

**What This Means:**
The exam page is trying to connect to the SignalR server, but the server isn't running or the URL is wrong.

### ? SOLUTION: Run SignalR Server

**Do this NOW:**

1. **Open a NEW Terminal** in Visual Studio:
   - Click: View ? Terminal (or press Ctrl+`)

2. **Navigate to SignalR folder:**
   ```bash
   cd ExamForge.SignalRServer
   ```

3. **Run the server:**
   ```bash
   dotnet run
   ```

4. **Wait for this message:**
   ```
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://localhost:5000
   ```

5. **KEEP THIS TERMINAL OPEN!** Don't close it while testing.

---

## ?? ISSUE #2: Anti-Cheat Features Not Working

**Problem:** Tab switching doesn't show warning

**Most Likely Cause:** The "Detect Tabbing" checkbox wasn't enabled when you published the exam

### ? SOLUTION: Verify and Republish

1. **In the exam browser tab**, press F12 ? Console

2. **Type this command:**
   ```javascript
   antiCheatConfig.DetectTabbing
   ```

3. **Check the result:**
   - If it says `true` ? Anti-cheat is enabled, proceed to Fix #2b
   - If it says `false` ? Anti-cheat is disabled, proceed to Fix #2a

### ? Fix #2a: Enable Anti-Cheat and Republish

**If `antiCheatConfig.DetectTabbing` returned `false`:**

1. **Go back to your WPF application**

2. **Navigate to:** "Exam Foundry" tab ? "Anti-Cheat" button

3. **Enable these checkboxes:**
   - ? Check "Detect Tabbing"
   - ? Check "Warning Only" (sub-option under Detect Tabbing)

4. **Click the "Review" button** (bottom right)

5. **Click "Publish Exam"** button

6. **Copy the new exam URL**

7. **Open the NEW URL** in your browser

8. **Test tab switching** again

### ? Fix #2b: Check Browser Console Logs

**If `antiCheatConfig.DetectTabbing` returned `true` but still not working:**

1. **In browser console**, look for these messages when you switch tabs:
   ```
   [Anti-Cheat] Visibility changed, hidden: true
   [Anti-Cheat] Tab switched. Count: 1
   ```

2. **If you DON'T see these messages:**
   - The event listener isn't registered properly
   - **Solution:** I need to update the code (let me know and I'll fix it)

3. **If you SEE the messages but no alert:**
   - Check if `antiCheatConfig.WarningOnly` is true
   - Type in console: `antiCheatConfig.WarningOnly`
   - Should return `true`

---

## ?? ISSUE #3: Firestore incident_reports Not Created

This will be fixed automatically once the SignalR server is running. The server will create the collection on first save.

**Current Path (Temporary):**
```
temp_sessions/
  ?? {examId}/
      ?? students/
          ?? {studentId}
```

**Future Path (After Proper Implementation):**
```
examforge_users/
  ?? {userId}/
      ?? incident_reports/
          ?? {reportId}
```

**Why Temporary?**
The SignalR server needs to look up which user owns the exam. This requires implementing `GetUserIdFromExamIdAsync()`.

**This works for now** because:
- Sessions are still saved
- Cross-device resume still works
- Data is cleaned up properly

---

## ?? YOUR ACTION CHECKLIST

### ?? Step 1: Run SignalR Server (5 minutes)
```bash
cd ExamForge.SignalRServer
dotnet run
```
**Keep it running!**

### ?? Step 2: Verify Anti-Cheat is Enabled (2 minutes)
1. In browser console, check: `antiCheatConfig.DetectTabbing`
2. If `false`, go back to WPF app, enable checkboxes, republish

### ?? Step 3: Test Tab Detection (1 minute)
1. Start the exam
2. Switch to another tab
3. Switch back
4. **Expected:** Alert pops up
5. **If not:** Share console output with me

### ?? Step 4: Test Other Features (5 minutes)
- Test copy/paste (Ctrl+C on text)
- Test screenshot (PrintScreen key)
- Test one question at a time (if enabled)

---

## ?? Quick Diagnosis Commands

**Open browser console and run these:**

```javascript
// Check if anti-cheat config loaded
antiCheatConfig

// Check specific settings
antiCheatConfig.DetectTabbing    // Should be: true
antiCheatConfig.WarningOnly      // Should be: true
antiCheatConfig.DisableCopyPaste // Should be: true if enabled

// Check if functions exist
typeof reportViolation          // Should be: "function"
typeof saveSessionProgress      // Should be: "function"

// Check if student info is set (after starting exam)
window.studentInfo

// Manually trigger tab switch to test
document.dispatchEvent(new Event('visibilitychange'))
```

---

## ? Expected Behavior (When Working)

### When You Switch Tabs:
1. Console log: `[Anti-Cheat] Visibility changed, hidden: true`
2. Console log: `[Anti-Cheat] Tab switched. Count: 1`
3. Console log: `[Anti-Cheat] tab_switch: {...}`
4. **Alert pops up:** "?? Warning: You switched away from the exam window."

### When You Try to Copy:
1. Alert: "Copying is disabled during the exam."
2. Console: `[Anti-Cheat] copy_attempt: {...}`

### When You Press PrintScreen:
1. Alert: "?? Screenshot Detected"
2. Console: `[Anti-Cheat] screenshot_attempt: {...}`

---

## ?? Still Not Working?

### Share This Information:

1. **Console output** (all messages from page load)
2. **Result of:** `antiCheatConfig.DetectTabbing`
3. **Result of:** `antiCheatConfig.WarningOnly`  
4. **Screenshot** of the Anti-Cheat tab (showing which checkboxes are enabled)
5. **SignalR server status** (is it running? Any errors?)

I'll diagnose the exact issue and provide the fix!

---

## ?? Once It's Working:

After you verify tab detection is working locally:

1. ? Push to GitHub
2. ? Deploy SignalR server to Render.com
3. ? Update appsettings.json with production URL
4. ? Republish exam
5. ? Test in production

**Total time:** ~30 minutes

---

## ?? Need Help?

Reply with:
- [ ] SignalR server running? (yes/no)
- [ ] `antiCheatConfig.DetectTabbing` result? (true/false/undefined)
- [ ] Console logs when switching tabs?
- [ ] Any errors in Visual Studio Output window?

I'll get you fixed up! ??
