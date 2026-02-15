# ExamForge Authentication Setup Guide

## ?? **Google Sign-In Implementation Complete!**

### **What's New:**
- ? Google OAuth authentication
- ? User-scoped data isolation
- ? LoginWindow shown on startup
- ? Secure Firestore rules
- ? Logout functionality

---

## **?? Setup Steps**

### **1. Get Google OAuth Credentials**

1. **Go to Google Cloud Console:**
   - https://console.cloud.google.com
   - Select project: `examforge-201e8`

2. **Enable APIs:**
   - APIs & Services ? Library
   - Search and enable:
     - Google+ API
     - Google Identity Toolkit API

3. **Create OAuth Client:**
   - APIs & Services ? Credentials
   - Click "Create Credentials" ? "OAuth 2.0 Client ID"
   - Application type: **Desktop application**
   - Name: "ExamForge Instructor Desktop"
   - Click "Create"

4. **Copy Credentials:**
   ```
   Client ID: 123456789-xxxxxxxxxxxxxxxx.apps.googleusercontent.com
   Client Secret: GOCSPX-xxxxxxxxxxxxxxxxxxxxxx
   ```

5. **Update Code:**
   - Open: `ExamForge/Services/FirebaseAuthService.cs`
   - Line 28-29: Replace with your credentials:
```csharp
ClientId = "YOUR_CLIENT_ID.apps.googleusercontent.com",
ClientSecret = "YOUR_CLIENT_SECRET"
```

---

### **2. Update Firestore Security Rules**

In Firebase Console ? Firestore ? Rules, paste:

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    
    // User-scoped collections
    match /users/{userId} {
      allow read, write: if request.auth.uid == userId;
      
      match /exams/{examId} {
        allow read, write: if request.auth.uid == userId;
      }
      
      match /submissions/{submissionId} {
        allow read, write: if request.auth.uid == userId;
      }
      
      match /grading_queue/{itemId} {
        allow read, write: if request.auth.uid == userId;
      }
      
      match /sessions/{sessionId} {
        allow read, write: if request.auth.uid == userId;
      }
      
      match /integrity_incidents/{incidentId} {
        allow read, write: if request.auth.uid == userId;
      }
    }
  }
}
```

Click **Publish**

---

### **3. Update All Firestore Collection Paths**

**IMPORTANT:** All Firestore queries must now use user-scoped paths.

In `FirestoreService.cs`, replace ALL instances of:
```csharp
_firestoreDb.Collection("published_exams")
? _firestoreDb.Collection(GetUserPath("exams"))

_firestoreDb.Collection("examinee_data")
? _firestoreDb.Collection(GetUserPath("submissions"))

_firestoreDb.Collection("grading_queue")
? _firestoreDb.Collection(GetUserPath("grading_queue"))

_firestoreDb.Collection("sessions")
? _firestoreDb.Collection(GetUserPath("sessions"))

_firestoreDb.Collection("integrity_incidents")
? _firestoreDb.Collection(GetUserPath("integrity_incidents"))
```

**Quick Find/Replace:**
- Find: `Collection("published_exams")`
- Replace: `Collection(GetUserPath("exams"))`

Repeat for all collections.

---

### **4. Test Authentication Flow**

1. **Build and Run:**
```bash
dotnet build
dotnet run
```

2. **Expected Behavior:**
   - ? LoginWindow appears (NOT MainWindow)
   - ? Click "Sign in with Google"
   - ? Browser opens for Google authentication
   - ? Sign in with your Google account
   - ? Grant permissions
   - ? LoginWindow closes
   - ? MainWindow opens with full UI
   - ? Sidebar shows your name/email
   - ? All data is scoped to your user ID

3. **Test Logout:**
   - Click logout in sidebar
   - ? Returns to LoginWindow
   - ? Data is cleared

---

## **??? Architecture**

### **Before (Shared Collections):**
```
/published_exams/{examId}
/examinee_data/{submissionId}
/grading_queue/{itemId}
/sessions/{sessionId}
```
? All users see each other's data

### **After (User-Scoped):**
```
/users/{userId}/exams/{examId}
/users/{userId}/submissions/{submissionId}
/users/{userId}/grading_queue/{itemId}
/users/{userId}/sessions/{sessionId}
```
? Complete data isolation per instructor

---

## **?? Security Features**

1. **Authentication Required:**
   - Cannot access app without signing in
   - Google OAuth handles credentials

2. **Data Isolation:**
   - Each instructor has own collection path
   - Firestore rules enforce server-side

3. **Automatic Logout:**
   - Session managed by Google
   - Clean logout returns to login screen

4. **No Credential Storage:**
   - No passwords stored in app
   - Google handles authentication token

---

## **?? Required NuGet Packages**

Already installed:
```
Google.Cloud.Firestore
FirebaseAdmin
Google.Apis.Auth
Google.Apis.Auth.OAuth2
```

---

## **?? Deployment Checklist**

Before distributing:
- [ ] Update OAuth credentials in code
- [ ] Test login flow works
- [ ] Verify data isolation (create 2 test accounts)
- [ ] Publish Firestore security rules
- [ ] Test logout and re-login
- [ ] Build release version with correct credentials
- [ ] Test on clean machine (VM)
- [ ] Document user credentials process

---

## **?? Multi-User Testing**

1. Sign in as User A ? Create exam ? Logout
2. Sign in as User B ? Check exams list
3. ? User B should NOT see User A's exam
4. User B creates own exam
5. Sign back in as User A
6. ? User A should only see their own exam

---

## **?? Troubleshooting**

### **"Sign-in failed"**
- Check OAuth Client ID/Secret are correct
- Verify Google+ API is enabled
- Check internet connection

### **"Cannot access data"**
- Firestore rules may not be published
- Check userId is being set correctly
- Verify authentication completed

### **"Permission denied"**
- Firestore security rules are blocking
- Check user is authenticated
- Verify collection paths use GetUserPath()

---

## **?? Next Steps**

1. ? Authentication implemented
2. ? Update all Firestore collection references
3. ? Test with multiple users
4. ? Deploy to production
5. ? Document for end users

---

**Status:** ? Login System Complete - Ready for Collection Path Updates

**Last Updated:** 2024-01-15
