# ?? QUICK START - Copy & Paste These Commands

## ? All Changes Reapplied Successfully!

Your code is ready. Just follow these steps:

---

## Step 1: Push to GitHub (1 minute)

**Open Terminal in Visual Studio** (View ? Terminal or Ctrl+`) and run:

```bash
cd C:\Users\braea\source\repos\ExamForge
git add .
git commit -m "Add cloud-only SignalR, anti-cheat fixes, and auto-resume session"
git push origin Checkpoint
```

**Expected Output:**
```
[Checkpoint abc1234] Add cloud-only SignalR, anti-cheat fixes, and auto-resume session
 X files changed, Y insertions(+), Z deletions(-)
Enumerating objects...
Writing objects: 100%...
```

---

## Step 2: Deploy SignalR Server to Render (10 minutes)

### 2a. Go to Render

1. Open: https://render.com/dashboard
2. Click: "New +" (top right)
3. Select: "Web Service"

### 2b. Connect Repository

1. Select: `akobryan1/ExamForge`
2. Click: "Connect"

### 2c. Configure Service (Copy These EXACT Values)

**Service Name:**
```
examforge-signalr
```

**Root Directory:**
```
ExamForge.SignalRServer
```

**Build Command:**
```
dotnet publish -c Release -o out
```

**Start Command:**
```
dotnet out/ExamForge.SignalRServer.dll
```

### 2d. Add Environment Variables

Click "Advanced" ? "Add Environment Variable"

**Variable 1:**
- Key: `FIRESTORE_PROJECT_ID`
- Value: `examforge-201e8`

**Variable 2:**
- Key: `ASPNETCORE_ENVIRONMENT`
- Value: `Production`

**Variable 3:**
- Key: `GOOGLE_APPLICATION_CREDENTIALS_JSON`
- Value: (Open `firebase-adminsdk.json`, copy ALL the JSON content, paste here)

### 2e. Deploy

1. Click "Create Web Service"
2. Wait 5-10 minutes
3. Watch logs for: `? Application started`
4. **Copy your service URL** (e.g., `https://examforge-signalr-abc.onrender.com`)

---

## Step 3: Update appsettings.json (1 minute)

1. Open: `ExamForge\appsettings.json`

2. Replace this line:
```json
"SignalRHubUrl": "https://examforge-signalr.onrender.com"
```

With your ACTUAL Render URL:
```json
"SignalRHubUrl": "https://YOUR-ACTUAL-URL-FROM-RENDER.onrender.com"
```

3. Save the file

4. Build ? Rebuild Solution

---

## Step 4: Republish Exam (2 minutes)

1. **Run your WPF app**

2. **Go to "Exam Foundry" ? "Anti-Cheat" tab**

3. **Enable these checkboxes:**
   - ? Detect Tabbing
   - ? Warning Only
   - ? Auto-Resume Session

4. **Click "Review" ? "Publish Exam"**

5. **Copy the new exam URL**

6. **Test in browser!**

---

## Step 5: Test Anti-Cheat (5 minutes)

### Open Exam URL

1. **Open the exam URL** in a new browser window

2. **Press F12** ? Console tab

3. **Look for these messages:**
```
Exam JS initializing
[Anti-Cheat] DetectTabbing: true
[Anti-Cheat] WarningOnly: true
[SignalR] Hub URL: https://your-signalr.onrender.com/sessionHub
? Connected to SignalR hub
```

### Test Tab Switching

1. **Start the exam** (fill in student info, click Start)

2. **Switch to another tab** (Ctrl+Tab or click another tab)

3. **Switch back to exam tab**

4. **Expected Result:**
   - ?? Alert pops up: "Warning: You switched away..."
   - Console shows: `[Anti-Cheat] Tab switched. Count: 1`

### Test Auto-Resume

1. **Answer 2-3 questions**

2. **Close the browser tab** completely

3. **Reopen the exam URL**

4. **Expected Result:**
   - ?? Prompt appears: "Resume your previous exam session?"
   - Click "OK"
   - All answers restored
   - Timer continues from where you left off

---

## ? Success Indicators

### SignalR Connected:
- ? Console shows: `Connected to SignalR hub`
- ? If 404: Server not deployed or URL wrong

### Anti-Cheat Working:
- ? Alert pops up when switching tabs
- ? If nothing: Check `antiCheatConfig.DetectTabbing` in console

### Auto-Resume Working:
- ? Console shows `[Auto-Save] Progress saved` when answering
- ? Prompt appears when reopening exam
- ? If not: Check `antiCheatConfig.AutoResumeSession` is true

---

## ?? Troubleshooting

### "SignalR connection failed"
**Fix:** 
1. Make sure you deployed the SignalR server to Render
2. Check the URL in appsettings.json matches your Render URL
3. Wait 30 seconds if free tier (server wakes up)

### "Anti-cheat not working"
**Fix:**
1. In browser console, type: `antiCheatConfig.DetectTabbing`
2. If false: Enable checkbox in WPF app and republish
3. If true: Check browser console for error messages

### "Auto-resume not prompting"
**Fix:**
1. Check: `antiCheatConfig.AutoResumeSession`
2. If false: Enable checkbox and republish
3. Make sure you answered questions before closing

---

## ?? Summary of What's Ready

| Component | Status | Location |
|-----------|--------|----------|
| WPF App | ? Ready | Local machine |
| Publishing Server | ? Deployed | examforge-publisher.onrender.com |
| SignalR Server | ? Deploy Now | Follow Step 2 above |
| API Endpoint | ? Deployed | Cloudflare Workers |
| Firestore | ? Ready | Google Cloud |

**You only need to deploy the SignalR Server!** Everything else is ready.

---

## ?? Deployment Checklist

- [ ] Step 1: Push to GitHub ? DO THIS NOW
- [ ] Step 2: Deploy SignalR to Render ? DO AFTER PUSH
- [ ] Step 3: Update appsettings.json ? DO AFTER DEPLOY
- [ ] Step 4: Republish exam ? DO AFTER CONFIG
- [ ] Step 5: Test features ? DO AFTER REPUBLISH

**Estimated Total Time:** 20 minutes (mostly waiting for Render)

---

## ?? Need Help?

If you encounter issues at any step:

1. **Git push fails:** Check Git credentials, or try GitHub Desktop
2. **Render deploy fails:** Check build logs in Render dashboard
3. **Features not working:** Share browser console output
4. **Build errors:** Share error messages from Visual Studio

**I'm here to help!** Just let me know which step you're stuck on. ??

---

## ?? You're Almost Done!

All the hard work (coding) is complete. Just:
1. Push (1 command)
2. Deploy (click a few buttons)
3. Test (open URL)

**Let's get this deployed!** Start with Step 1 above. ??
