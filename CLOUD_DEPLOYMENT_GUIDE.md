# ?? CLOUD-ONLY DEPLOYMENT GUIDE - No Localhost!

## ? Configuration Updated for Full Cloud Deployment

All code has been updated to work entirely in the cloud. Here's your deployment plan:

---

## ?? Your Cloud Architecture

You have **2 separate Render services**:

### 1. **Publishing Server** (Already Exists)
   - URL: `https://examforge-publisher.onrender.com`
   - Purpose: Hosts exam HTML files
   - Endpoints: `/api/publish`, `/health`

### 2. **SignalR Server** (Needs to be Deployed)
   - URL: `https://examforge-signalr.onrender.com` (will create)
   - Purpose: Real-time monitoring & session management
   - Endpoints: `/sessionHub`, `/health`

---

## ?? DEPLOYMENT STEPS

### Step 1: Push Code to GitHub

```bash
cd C:\Users\braea\source\repos\ExamForge
git add .
git commit -m "Add cloud-only SignalR support and auto-resume feature"
git push origin Checkpoint
```

---

### Step 2: Deploy SignalR Server to Render.com

#### 2a. Create New Web Service

1. Go to https://render.com/dashboard
2. Click "New +" ? "Web Service"
3. **Select your repository:** `akobryan1/ExamForge`

#### 2b. Configure the Service

| Setting | Value |
|---------|-------|
| **Name** | `examforge-signalr` |
| **Region** | Choose closest to you |
| **Branch** | `Checkpoint` |
| **Root Directory** | `ExamForge.SignalRServer` |
| **Runtime** | .NET |
| **Build Command** | `dotnet publish -c Release -o out` |
| **Start Command** | `dotnet out/ExamForge.SignalRServer.dll` |
| **Instance Type** | Free (or paid for better performance) |

#### 2c. Add Environment Variables

Click "Advanced" ? "Add Environment Variable":

| Key | Value |
|-----|-------|
| `FIRESTORE_PROJECT_ID` | `examforge-201e8` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:5000` |

#### 2d. Add Firebase Credentials (IMPORTANT!)

You need to add your Firebase service account JSON as an environment variable:

1. Open your `firebase-adminsdk.json` file locally
2. Copy the entire JSON content
3. In Render, add environment variable:
   - **Key:** `GOOGLE_APPLICATION_CREDENTIALS_JSON`
   - **Value:** (paste the entire JSON)

4. Update `SessionHub.cs` to use this (I'll do this for you below)

#### 2e. Deploy

1. Click "Create Web Service"
2. Wait 5-10 minutes for first deployment
3. Once deployed, copy the URL (e.g., `https://examforge-signalr.onrender.com`)

---

### Step 3: Update appsettings.json with Your Render URLs

**In `ExamForge\appsettings.json`:**

```json
{
  "Firebase": {
    "CredentialsPath": "firebase-adminsdk.json",
    "ProjectId": "examforge-201e8",
    "HostingUrl": "https://examforge-201e8.web.app",
    "ApiEndpoint": "https://examforge-api.braeanmay.workers.dev",
    "PublishingServerUrl": "https://examforge-publisher.onrender.com",
    "SignalRHubUrl": "https://examforge-signalr.onrender.com"
  }
}
```

**Replace** `https://examforge-signalr.onrender.com` with your actual deployed URL!

---

### Step 4: Republish Your Exam

Since your previous exam was published with old settings:

1. **In WPF App:**
   - Go to Anti-Cheat tab
   - ? Enable "Detect Tabbing" ? "Warning Only"
   - ? Enable "Auto-Resume Session"
   - Go back to exam structure
   - Click "Review"
   - Click "Publish Exam"

2. **Copy the NEW exam URL**

3. **Open the NEW URL** in browser

4. **Test:**
   - Start exam
   - Switch tabs
   - **Expected:** Warning alert appears

---

## ?? Fix for Firebase Credentials on Render

Update `SessionHub.cs` to load credentials from environment variable:

```csharp
private static FirestoreDb GetFirestoreDb()
{
    if (_firestoreDb != null) return _firestoreDb;
    lock (_fsLock)
    {
        if (_firestoreDb != null) return _firestoreDb;

        var projectId = Environment.GetEnvironmentVariable("FIRESTORE_PROJECT_ID") ?? "examforge-201e8";
        
        try
        {
            // Check if credentials are in environment variable (Render deployment)
            var credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS_JSON");
            
            if (!string.IsNullOrEmpty(credentialsJson))
            {
                // Use credentials from environment variable
                var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credentialsJson);
                var builder = new FirestoreDbBuilder
                {
                    ProjectId = projectId,
                    Credential = credential
                };
                _firestoreDb = builder.Build();
                Console.WriteLine("? Firestore initialized from environment credentials");
            }
            else
            {
                // Fallback to file-based credentials (local development)
                _firestoreDb = FirestoreDb.Create(projectId);
                Console.WriteLine("? Firestore initialized from file");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Failed to initialize Firestore: {ex.Message}");
            _firestoreDb = null;
        }

        return _firestoreDb;
    }
}
```

---

## ?? Your Updated Render Services

### Service #1: Publishing Server (Already Deployed)
- **Name:** `examforge-publisher`
- **URL:** `https://examforge-publisher.onrender.com`
- **Purpose:** Hosts exam HTML files
- **Status:** ? Already working

### Service #2: SignalR Server (Deploy This)
- **Name:** `examforge-signalr`
- **URL:** `https://examforge-signalr.onrender.com` (you choose the name)
- **Purpose:** Real-time monitoring, session management, anti-cheat reporting
- **Status:** ? Needs deployment

---

## ?? Testing After Deployment

### Test 1: SignalR Connection
1. Open exam URL
2. Press F12 ? Console
3. Look for: `? Connected to SignalR hub`
4. Should NO LONGER see: `SignalR start failed`

### Test 2: Anti-Cheat Tab Detection
1. Start exam
2. Switch tabs
3. **Expected:** Alert appears
4. Check console: `[Anti-Cheat] Tab switched. Count: 1`

### Test 3: Session Auto-Save
1. Answer a question
2. Check console: `[Auto-Save] Progress saved`
3. Close browser tab
4. Reopen exam URL
5. **Expected:** "Resume your previous exam session?" prompt

### Test 4: Cross-Device Resume (If using Google Sign-In)
1. Start exam on Device 1
2. Sign in with Google
3. Answer questions
4. Open exam on Device 2
5. Sign in with same Google account
6. **Expected:** Resume prompt appears

---

## ?? IMPORTANT: No Localhost!

Your configuration is now:
- ? Publishing Server: Cloud (Render)
- ? SignalR Server: Cloud (Render - after you deploy)
- ? API Endpoint: Cloud (Cloudflare Workers)
- ? Firestore: Cloud (Google Cloud)

**Nothing runs on localhost!** Everything is cloud-based.

---

## ?? Your Immediate Action Items

### ? DO THIS IN ORDER:

1. **Push to GitHub:**
   ```bash
   git add .
   git commit -m "Add SignalR hub configuration and auto-resume feature"
   git push origin Checkpoint
   ```

2. **Deploy SignalR Server to Render:**
   - Follow Step 2 above
   - **Root Directory:** `ExamForge.SignalRServer`
   - Add Firebase credentials as environment variable
   - Wait for deployment
   - Copy the URL

3. **Update appsettings.json:**
   - Replace `https://examforge-signalr.onrender.com` with your actual Render URL
   - Save the file

4. **Republish Your Exam:**
   - Enable anti-cheat in WPF app
   - Publish exam
   - Test the new URL

---

## ?? Why Your Previous Exam Redirected to Render

**Problem:** The exam URL probably was pointing to the Render **service** itself, not a specific exam file.

**Example of WRONG URL:**
```
https://examforge-publisher.onrender.com
```

**Example of CORRECT URL:**
```
https://examforge-publisher.onrender.com/exams/{examId}.html
```

**Fix:** When you republish, the new exam will have the correct URL format.

---

## ?? Troubleshooting

### "SignalR connection still fails"
- **Check:** Is the SignalR server deployed and running on Render?
- **Check:** Is the URL in appsettings.json correct?
- **Check:** Does the Render service have Firebase credentials?

### "Exam still redirects to Render homepage"
- **Check:** Is your publishing server properly serving exam files?
- **Check:** Does the `/api/publish` endpoint work?
- **Test:** Try accessing `https://your-publisher.onrender.com/health`

### "Anti-cheat still doesn't work"
- **Check:** Did you republish after updating appsettings.json?
- **Check:** Did you enable the anti-cheat checkboxes?
- **Check:** Browser console for error messages

---

## ?? Next Steps

1. Deploy SignalR server to Render (Step 2 above)
2. Update appsettings.json with your SignalR URL
3. Republish exam
4. Test all features

**Let me know when SignalR is deployed and I'll help you test everything!** ??

---

## ?? Quick Reference

**Your Render Services:**
- Publishing Server: `examforge-publisher.onrender.com` ?
- SignalR Server: `examforge-signalr.onrender.com` ? (deploy this!)

**No localhost needed!** Everything runs in the cloud.
