# ?? Render Deployment - Exact Settings

## Step-by-Step: Deploy SignalR Server to Render.com

### Prerequisites:
- ? GitHub repository pushed
- ? Render.com account connected to GitHub

---

## ?? Deployment Configuration

### 1. Create New Web Service

1. Go to: https://render.com/dashboard
2. Click: **"New +" button** (top right)
3. Select: **"Web Service"**
4. Connect: **Your GitHub account** (if not already connected)
5. Select repository: **`akobryan1/ExamForge`**

---

### 2. Service Configuration

Fill in these EXACT values:

| Field | Value | Notes |
|-------|-------|-------|
| **Name** | `examforge-signalr` | Or choose your own name |
| **Region** | `Oregon (US West)` | Or choose closest to you |
| **Branch** | `Checkpoint` | Your GitHub branch name |
| **Root Directory** | `ExamForge.SignalRServer` | ?? IMPORTANT! |
| **Runtime** | `.NET` | Auto-detected |
| **Build Command** | `dotnet publish -c Release -o out` | Pre-filled |
| **Start Command** | `dotnet out/ExamForge.SignalRServer.dll` | Pre-filled |

---

### 3. Instance Type

**Free Tier:**
- ? Free
- ?? Sleeps after 15 minutes of inactivity
- ?? First request takes ~30 seconds to wake up
- ? Good for testing

**Paid Tier (Starter - $7/month):**
- ? Always running
- ? Fast response times
- ? Recommended for production

**Choose:** Free for now, upgrade later if needed

---

### 4. Environment Variables

Click **"Advanced"** ? **"Add Environment Variable"**

Add these THREE environment variables:

#### Variable 1: Project ID
| Field | Value |
|-------|-------|
| **Key** | `FIRESTORE_PROJECT_ID` |
| **Value** | `examforge-201e8` |

#### Variable 2: Environment
| Field | Value |
|-------|-------|
| **Key** | `ASPNETCORE_ENVIRONMENT` |
| **Value** | `Production` |

#### Variable 3: Firebase Credentials
| Field | Value |
|-------|-------|
| **Key** | `GOOGLE_APPLICATION_CREDENTIALS_JSON` |
| **Value** | (see below) |

**For Variable 3:**
1. Open your local `firebase-adminsdk.json` file
2. Copy the ENTIRE JSON content
3. Paste it into the Value field
4. Should look like:
```json
{
  "type": "service_account",
  "project_id": "examforge-201e8",
  "private_key_id": "...",
  "private_key": "-----BEGIN PRIVATE KEY-----\n...",
  "client_email": "...",
  ...
}
```

---

### 5. Deploy

1. Click **"Create Web Service"** (bottom of page)
2. Render will start building your service
3. Wait 5-10 minutes for first deployment
4. Watch the logs for progress

---

## ?? Expected Build Output

You should see logs like this:

```
==> Cloning from https://github.com/akobryan1/ExamForge...
==> Checking out commit 1a2b3c4...
==> Using root directory ExamForge.SignalRServer
==> Running 'dotnet publish -c Release -o out'
    Restoring dependencies...
    Building project...
    Publishing...
    ? Build succeeded
==> Launching service
    Hosting environment: Production
    Now listening on: http://0.0.0.0:5000
    ? Application started
```

---

## ? Verify Deployment

### Test 1: Check Service Health

1. Once deployed, Render gives you a URL like:
   ```
   https://examforge-signalr-xyz.onrender.com
   ```

2. Open in browser: `https://YOUR-URL.onrender.com/`

3. You should see:
   ```json
   {
     "status": "running",
     "service": "ExamForge SignalR Server",
     "timestamp": "2025-01-02T12:34:56Z"
     }
   ```

### Test 2: Check SignalR Hub

1. Try accessing: `https://YOUR-URL.onrender.com/sessionHub`

2. Should see error (expected):
   ```
   Available transports: ...
   ```
   This means SignalR is working!

---

## ?? Copy Your URL

Once deployed successfully:

1. **Copy the full URL** from Render dashboard
   Example: `https://examforge-signalr-abc123.onrender.com`

2. **Update your local** `appsettings.json`:
   ```json
   {
     "Firebase": {
       ...
       "SignalRHubUrl": "https://examforge-signalr-abc123.onrender.com"
     }
   }
   ```

3. **Rebuild** your WPF application

4. **Republish** your exam

---

## ?? Troubleshooting

### Build Fails: "Project file not found"
**Cause:** Root Directory is wrong
**Fix:** Make sure it's `ExamForge.SignalRServer` (case-sensitive!)

### Build Fails: "Restore failed"
**Cause:** Missing NuGet packages
**Fix:** Check that `ExamForge.SignalRServer.csproj` has all required packages

### Service Starts but Crashes
**Cause:** Missing environment variables
**Fix:** Check that all 3 environment variables are set correctly

### "Firestore not initialized"
**Cause:** Firebase credentials missing or malformed
**Fix:**
- Make sure `GOOGLE_APPLICATION_CREDENTIALS_JSON` contains valid JSON
- Check for missing quotes or commas
- Paste the ENTIRE firebase-adminsdk.json content

---

## ?? Post-Deployment Checklist

After successful deployment:

- [ ] Service shows "Live" status in Render dashboard
- [ ] Health endpoint returns JSON response
- [ ] URL copied and saved
- [ ] appsettings.json updated with URL
- [ ] WPF application rebuilt
- [ ] Exam republished with new settings

---

## ? First Request Wait Time

**IMPORTANT:** If using free tier:
- Service sleeps after 15 minutes of no activity
- First request takes 30-60 seconds to wake up
- Subsequent requests are instant

**When testing:**
1. Open exam URL
2. Wait 30 seconds if you see SignalR connection error
3. Refresh page
4. Should connect successfully

---

## ?? Cost Estimate

### Free Tier:
- **Cost:** $0/month
- **Limits:** Sleeps after inactivity
- **Good for:** Testing, development

### Starter Tier ($7/month):
- **Cost:** $7/month
- **Limits:** None
- **Good for:** Production, always-on

**Recommendation:** Start with free, upgrade if needed

---

## ?? Redeployment

To redeploy after code changes:

1. Push changes to GitHub:
   ```bash
   git add .
   git commit -m "Update SignalR server"
   git push origin Checkpoint
   ```

2. In Render dashboard:
   - Go to your service
   - Click "Manual Deploy" ? "Deploy latest commit"
   - Wait for rebuild

Render also auto-deploys on every push to your branch!

---

## ?? Need Help?

**Common Issues:**
- Service won't start ? Check logs in Render dashboard
- Build fails ? Check Root Directory is correct
- Firestore errors ? Check environment variables
- SignalR connection fails ? Check service is "Live"

**Still stuck?** Share:
1. Render deployment logs (last 50 lines)
2. Error messages from browser console
3. appsettings.json SignalRHubUrl value

I'll help you debug! ??
