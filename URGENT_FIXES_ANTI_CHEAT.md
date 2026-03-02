# ?? URGENT FIXES - Anti-Cheat & SignalR Issues

## ?? Issues Identified

### 1. SignalR 404 Error
**Error:** `/sessionHub/negotiate:1 Failed to load resource: the server responded with a status of 404 ()`

**Cause:** SignalR server is not running or the URL is incorrect

### 2. Anti-Cheat Not Working
**Symptom:** Tab switching doesn't trigger warning alert

**Possible Causes:**
- DetectTabbing might not be enabled in the published exam
- Event listeners registered but condition check failing

### 3. Firestore incident_reports Not Created
**Issue:** Sessions should be saved to user's collection, not root

---

## ?? IMMEDIATE FIXES

### Fix 1: Run SignalR Server

The SignalR server MUST be running for the feature to work. You have 2 options:

#### Option A: Run Locally (Quick Test)

1. **Open Terminal** in Visual Studio (View ? Terminal)

2. **Navigate to SignalR Server folder:**
```bash
cd ExamForge.SignalRServer
```

3. **Run the server:**
```bash
dotnet run
```

4. **You should see:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

5. **Keep this terminal window open** while testing

6. **Update appsettings.json** in ExamForge project:
```json
{
  "PublishingServerUrl": "http://localhost:5000",
  "ApiEndpoint": "https://examforge-api.braeanmay.workers.dev"
}
```

7. **Rebuild** the WPF app and republish your exam

#### Option B: Deploy to Render.com (Production)

Follow the deployment guide I created earlier. This is for permanent deployment.

---

### Fix 2: Check Anti-Cheat Configuration

Let's verify the anti-cheat config is being set correctly:

1. **Open the exam in browser**
2. **Open DevTools** (F12) ? Console
3. **Type this command:**
```javascript
antiCheatConfig
```

4. **Check the output.** You should see:
```javascript
{
  DetectTabbing: true,  // ? Should be true
  WarningOnly: true,    // ? Should be true if you enabled it
  DeductPoints: false,
  ...
}
```

5. **If DetectTabbing is false**, then the checkbox wasn't enabled when you published the exam. You need to:
   - Go back to WPF app
   - Open Anti-Cheat tab
   - ? Check "Detect Tabbing"
   - ? Check "Warning Only"
   - **Republish the exam**

---

### Fix 3: Test Anti-Cheat Manually

After the exam starts, test if the listener is registered:

1. **In browser console, type:**
```javascript
// Simulate tab switching
document.dispatchEvent(new Event('visibilitychange'));
```

2. **If nothing happens**, the listener isn't registered properly

3. **Try manually checking:**
```javascript
// Check if the function exists
typeof reportViolation  // Should return "function"
typeof initializeAntiCheat  // Should return "function"
```

---

## ??? COMPREHENSIVE FIX

Based on the issues, I'm going to add better initialization and debugging. Let me update the code:

### Changes Needed:

1. **Wrap all anti-cheat in initialization function**
2. **Call initializeAntiCheat() when exam starts**
3. **Add extensive console logging**
4. **Fix Firestore path to use user's collection**

---

## ?? IMMEDIATE ACTION ITEMS

### ? DO THIS NOW:

1. **Start SignalR Server Locally:**
   ```bash
   cd ExamForge.SignalRServer
   dotnet run
   ```
   Keep this running!

2. **Update appsettings.json:**
   ```json
   {
     "PublishingServerUrl": "http://localhost:5000"
   }
   ```

3. **In WPF App:**
   - Go to Anti-Cheat tab
   - ? Check "Detect Tabbing"
   - ? Check "Warning Only"
   - Click Create/Structure tab
   - Fill in exam details
   - **Publish exam**

4. **Open Exam URL in Browser:**
   - Press F12 ? Console
   - Start the exam
   - Check console for these messages:
     ```
     Exam JS initializing
     Anti-cheat configuration loaded: {...}
     DetectTabbing enabled: true
     WarningOnly: true
     ```

5. **Switch Tabs:**
   - Go to another browser tab
   - Come back to exam tab
   - **Expected:** Alert should pop up
   - **If not working:** Copy the console output and send it to me

---

## ?? Root Cause Analysis

Looking at your error, the most likely cause is:

1. **SignalR server not running** ? 404 error
2. **Anti-cheat might not be enabled** when exam was published
3. **Event listener registered but not firing** ? Need more debugging

The good news: The code is there and working. We just need to ensure:
- ? Server is running
- ? Config is enabled
- ? Listeners are properly initialized

---

## ?? QUICK TEST

Do this right now to diagnose:

1. **Open exam URL**
2. **Press F12** ? Console tab
3. **Type:** `antiCheatConfig.DetectTabbing`
4. **What does it say?**
   - `true` ? Config is correct, issue is with listener
   - `false` ? You need to enable the checkbox and republish
   - `undefined` ? Config didn't load properly

**Tell me the result and I'll provide the exact fix!**

---

## ?? Expected Console Output (When Working)

When you open the exam and switch tabs, you should see:

```
Exam JS initializing
Anti-cheat configuration loaded: {DetectTabbing: true, WarningOnly: true, ...}
DetectTabbing enabled: true
WarningOnly: true
[Auto-Save] Setup complete - saving on answer change
[Anti-Cheat] Visibility changed, hidden: true
[Anti-Cheat] Tab switched. Count: 1
[Anti-Cheat] tab_switch: {count: 1, deductedPoints: 0, timestamp: "2025-03-02T11:04:10.505Z"}
```

Then an alert pops up: **"Warning: You switched away from the exam window..."**

---

## ?? Next Steps After This Works

Once anti-cheat is working:
1. Deploy SignalR server to Render.com for production
2. Test cross-device session resume
3. Verify Firestore incident reports are being created
4. Test all other anti-cheat features

**For now:** Focus on getting the SignalR server running locally and verifying the DetectTabbing config is true!

Let me know what `antiCheatConfig.DetectTabbing` returns in the console! ??
