# ExamForge Deployment Guide

## Overview
This guide covers deploying ExamForge to production environments for enterprise use.

---

## **Prerequisites**

### **System Requirements:**
- **OS:** Windows 10/11 or Windows Server 2019+
- **Framework:** .NET 8.0 Runtime or .NET 10.0 Runtime
- **RAM:** Minimum 4GB (8GB recommended)
- **Storage:** 500MB for application + storage for exam data
- **Network:** Internet connection for Firestore and SignalR

### **Required Services:**
1. **Firebase/Firestore Account** (Free or paid)
2. **SignalR Server** (Deployed on Render.com or self-hosted)
3. **IIS or Standalone hosting** for the WPF application

---

## **Deployment Steps**

### **Step 1: Configure Firebase/Firestore**

1. **Create Firebase Project:**
   - Go to https://console.firebase.google.com
   - Create new project: "ExamForge-Production"
   - Enable Firestore Database
   - Create collections:
     - `published_exams`
     - `examinee_data`
     - `grading_queue`
     - `sessions`
     - `integrity_incidents`

2. **Generate Service Account Key:**
   - Go to Project Settings ? Service Accounts
   - Click "Generate New Private Key"
   - Save as `examforge-firebase-key.json`
   - **IMPORTANT:** Keep this file secure!

3. **Configure Firestore Rules:**
```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    // Exam data - read-only for students, read/write for instructors
    match /published_exams/{examId} {
      allow read: if request.auth != null;
      allow write: if request.auth.token.role == 'instructor';
    }
    
    // Student submissions
    match /examinee_data/{submissionId} {
      allow create: if request.auth != null;
      allow read, update: if request.auth.uid == resource.data.studentId 
                           || request.auth.token.role == 'instructor';
    }
    
    // Grading queue - instructors only
    match /grading_queue/{itemId} {
      allow read, write: if request.auth.token.role == 'instructor';
    }
    
    // Sessions - restricted
    match /sessions/{sessionId} {
      allow read: if request.auth != null;
      allow write: if request.auth.token.role == 'instructor';
    }
    
    // Integrity incidents - instructors only
    match /integrity_incidents/{incidentId} {
      allow read, write: if request.auth.token.role == 'instructor';
    }
  }
}
```

---

### **Step 2: Deploy SignalR Server**

#### **Option A: Deploy to Render.com (Recommended)**

1. **Create Render Account:**
   - Sign up at https://render.com

2. **Deploy SignalR Server:**
   - In Render dashboard: "New +" ? "Web Service"
   - Connect to your GitHub repository
   - Settings:
     - **Name:** examforge-signalr
     - **Environment:** Docker
     - **Region:** Choose closest to your users
     - **Branch:** main
     - **Dockerfile Path:** ExamForge.SignalRServer/Dockerfile
     - **Instance Type:** Free (for testing) or Starter ($7/month)

3. **Note the URL:**
   - After deployment, copy the URL (e.g., `https://examforge-signalr.onrender.com`)
   - You'll use this in the app configuration

#### **Option B: Self-Host on Windows Server**

1. **Install .NET 8.0 Runtime** on server
2. **Publish SignalR Server:**
```bash
cd ExamForge.SignalRServer
dotnet publish -c Release -o ./publish
```
3. **Run as Windows Service:**
```bash
sc create ExamForgeSignalR binPath="C:\ExamForge\SignalRServer\publish\ExamForge.SignalRServer.exe"
sc start ExamForgeSignalR
```
4. **Configure Firewall:** Open port 80/443

---

### **Step 3: Configure ExamForge Application**

1. **Update appsettings.json:**
```json
{
  "Firebase": {
    "ProjectId": "examforge-production",
    "CredentialsPath": "C:\\ExamForge\\Config\\examforge-firebase-key.json"
  },
  "SignalR": {
    "HubUrl": "https://examforge-signalr.onrender.com/sessionHub"
  },
  "Application": {
    "PublicExamUrl": "https://yourdomain.com/exams/",
    "ExportPath": "C:\\ExamForge\\Exports"
  }
}
```

2. **Place Configuration Files:**
   - Copy `appsettings.json` to application directory
   - Copy `examforge-firebase-key.json` to secure location
   - Update paths in appsettings.json

---

### **Step 4: Publish WPF Application**

#### **Publish for Windows Desktop:**

1. **In Visual Studio:**
   - Right-click ExamForge project ? Publish
   - Choose "Folder" target
   - Set output path: `C:\ExamForge\Publish`
   - Click "Publish"

2. **Using Command Line:**
```bash
dotnet publish ExamForge\ExamForge.csproj -c Release -r win-x64 --self-contained true -o C:\ExamForge\Publish
```

3. **Files to Include:**
   - All .dll files
   - ExamForge.exe
   - appsettings.json
   - Resources folder
   - Views folder
   - public/exams folder

---

### **Step 5: Deploy to Client Machines**

#### **Option A: MSI Installer (Recommended for Enterprise)**

1. **Install WiX Toolset:**
   - Download from https://wixtoolset.org/
   - Install WiX v3.11+

2. **Create Installer Project:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" Name="ExamForge" Language="1033" Version="1.0.0.0" 
           Manufacturer="Your Organization" UpgradeCode="YOUR-GUID-HERE">
    <Package InstallerVersion="200" Compressed="yes" InstallScope="perMachine" />
    
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="ExamForge" />
      </Directory>
    </Directory>
    
    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <!-- Add all application files here -->
    </ComponentGroup>
    
    <Feature Id="ProductFeature" Title="ExamForge" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
    </Feature>
  </Product>
</Wix>
```

3. **Build Installer:**
```bash
candle ExamForge.wxs
light ExamForge.wixobj -out ExamForge.msi
```

#### **Option B: Manual Installation**

1. **Copy Published Files:**
   - Copy entire `Publish` folder to `C:\Program Files\ExamForge\`

2. **Create Desktop Shortcut:**
   - Right-click ExamForge.exe ? Send to Desktop

3. **Configure Permissions:**
   - Ensure users have read/write access to export folders

---

### **Step 6: Configure IIS for Exam Hosting (Optional)**

If hosting public exams on your own domain:

1. **Install IIS:**
   - Windows Features ? Internet Information Services

2. **Create Web Application:**
   - IIS Manager ? Add Website
   - Name: ExamForge-Public
   - Physical path: `C:\ExamForge\public`
   - Binding: Port 80 or 443 (SSL)

3. **Configure MIME Types:**
   - Add `.html` ? `text/html`

4. **Set Permissions:**
   - IIS_IUSRS ? Read access to public folder

---

## **Post-Deployment Configuration**

### **1. Initial Setup:**

1. **Launch ExamForge**
2. **First-time Configuration:**
   - Click "Settings" ? "Firebase Configuration"
   - Verify connection (green checkmark)
   - Test SignalR connection

### **2. Create Admin Account:**

1. In Firebase Console:
   - Go to Authentication
   - Add user with role: `instructor`
   - Set email and password

### **3. Test Deployment:**

1. **Create Test Exam:**
   - Create simple exam with 5 questions
   - Publish to web
   - Note the exam URL

2. **Take Test Exam:**
   - Open URL in browser
   - Complete exam as student
   - Submit

3. **Verify Grading:**
   - Check submissions in ExamForge
   - Verify auto-grading works
   - Test manual grading

---

## **Security Best Practices**

### **1. Firestore Security:**
- ? Never commit firebase credentials to Git
- ? Use environment variables for production
- ? Enable Firestore security rules
- ? Regularly rotate service account keys

### **2. Network Security:**
- ? Use HTTPS for all public exams
- ? Configure firewall rules
- ? Limit SignalR access to known IPs (optional)

### **3. Data Protection:**
- ? Encrypt exported files
- ? Regular backups of Firestore data
- ? Implement data retention policies

---

## **Backup and Recovery**

### **Automated Firestore Backup:**

1. **Using Firebase CLI:**
```bash
firebase firestore:backups:schedules:create --schedule-frequency=daily
```

2. **Manual Backup:**
```bash
gcloud firestore export gs://your-backup-bucket
```

### **Application Backup:**
- Backup configuration files
- Backup public/exams folder
- Export database of published exams

---

## **Monitoring and Maintenance**

### **Health Checks:**

1. **Daily Checks:**
   - SignalR server status
   - Firestore connectivity
   - Exam accessibility

2. **Weekly Checks:**
   - Review error logs
   - Check disk space for exports
   - Verify backup success

3. **Monthly Checks:**
   - Update dependencies
   - Review security patches
   - Performance optimization

---

## **Scaling Considerations**

### **For 100-500 Students:**
- Render.com Starter plan ($7/month)
- Firebase Spark plan (free)
- Single application instance

### **For 500-2000 Students:**
- Render.com Standard plan ($25/month)
- Firebase Blaze plan (pay-as-you-go)
- Multiple application instances

### **For 2000+ Students:**
- Dedicated server for SignalR
- Firebase Blaze plan with reserved capacity
- Load balancer for application instances
- CDN for exam hosting

---

## **Troubleshooting**

### **Common Issues:**

1. **"Firebase connection failed"**
   - Check credentials path in appsettings.json
   - Verify service account has Firestore permissions
   - Check internet connectivity

2. **"SignalR connection timeout"**
   - Verify SignalR URL is correct
   - Check if SignalR server is running
   - Firewall may be blocking WebSocket connections

3. **"Exam URL not accessible"**
   - Check public/exams folder permissions
   - Verify IIS configuration
   - Check DNS settings

---

## **Support and Updates**

### **Getting Help:**
- Documentation: See User Manual
- Issues: GitHub repository issues tab
- Email: support@examforge.com (configure your own)

### **Update Process:**
1. Backup current installation
2. Download latest release
3. Stop application
4. Replace files
5. Update database schema if needed
6. Restart application
7. Test functionality

---

## **Deployment Checklist**

Before going live:

- [ ] Firebase project created and configured
- [ ] SignalR server deployed and accessible
- [ ] appsettings.json configured
- [ ] Application published and tested
- [ ] Security rules implemented
- [ ] Backup system configured
- [ ] Admin accounts created
- [ ] Test exam completed successfully
- [ ] Documentation provided to users
- [ ] Support process established

---

## **Production Readiness**

Your ExamForge deployment is production-ready when:
1. ? All services are online and monitored
2. ? Security measures are implemented
3. ? Backup system is automated
4. ? Test exams pass all scenarios
5. ? Staff training is complete
6. ? Support process is established

**Congratulations! Your ExamForge system is ready for production use!** ??
