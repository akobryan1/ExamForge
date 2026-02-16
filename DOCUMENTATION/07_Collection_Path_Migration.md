# Firestore Collection Path Migration Guide

## **?? Updating Collection Paths for User-Scoped Data**

All Firestore collection references must be updated to use user-scoped paths.

---

## **Pattern to Replace**

### **Before (Shared):**
```csharp
_firestoreDb.Collection("published_exams")
```

### **After (User-Scoped):**
```csharp
_firestoreDb.Collection(GetUserPath("exams"))
```

---

## **Collection Mappings**

| Old Collection | New Method Call |
|----------------|-----------------|
| `"published_exams"` | `GetUserPath("exams")` |
| `"examinee_data"` | `GetUserPath("submissions")` |
| `"grading_queue"` | `GetUserPath("grading_queue")` |
| `"sessions"` | `GetUserPath("sessions")` |
| `"integrity_incidents"` | `GetUserPath("integrity_incidents")` |

---

## **Batch Find/Replace in Visual Studio**

1. **Open Find/Replace:** `Ctrl + H`
2. **Set Scope:** Current Project
3. **Match Case:** Yes

### **Replace Sequence:**

**1. Published Exams:**
```
Find: Collection("published_exams")
Replace: Collection(GetUserPath("exams"))
```

**2. Submissions:**
```
Find: Collection("examinee_data")
Replace: Collection(GetUserPath("submissions"))
```

**3. Grading Queue:**
```
Find: Collection("grading_queue")
Replace: Collection(GetUserPath("grading_queue"))
```

**4. Sessions:**
```
Find: Collection("sessions")
Replace: Collection(GetUserPath("sessions"))
```

**5. Integrity Incidents:**
```
Find: Collection("integrity_incidents")
Replace: Collection(GetUserPath("integrity_incidents"))
```

**Click "Replace All" for each**

---

## **Manual Verification Needed**

After batch replace, manually check:

### **FirestoreService.cs**
- All `GetPublishedExamAsync()` methods
- All `SavePublishedExamAsync()` methods
- All `GetExamSubmissionsAsync()` methods
- All session management methods
- All grading queue methods

### **Other Files**
Some files may reference collections directly:
- `published_exams_usercontrol.xaml.cs`
- `dashboard_usercontrol.xaml.cs`
- `student_analytics_usercontrol.xaml.cs`

These should use `FirestoreService` methods instead of direct collection access.

---

## **Testing After Migration**

1. **Build:** `Ctrl + Shift + B`
2. **Fix any errors** (collection references)
3. **Run:** `F5`
4. **Test:**
   - Sign in with Google
   - Create a test exam
   - Publish exam
   - Check Firestore console: data should be under `users/{yourUserId}/exams/`

---

## **Firestore Console Verification**

Before migration:
```
/published_exams
  /exam123
    - title: "Test Exam"
```

After migration:
```
/users
  /abc123def (your Google user ID)
    /exams
      /exam123
        - title: "Test Exam"
```

---

## **Rollback Plan**

If something breaks:
1. Revert Find/Replace using Git:
```bash
git checkout -- ExamForge/Services/FirestoreService.cs
```

2. Or manually revert:
```
Find: Collection(GetUserPath("exams"))
Replace: Collection("published_exams")
```

---

## **Final Checklist**

After migration:
- [ ] All collection references updated
- [ ] Build succeeds with 0 errors
- [ ] Login flow works
- [ ] Can create exam (saves to user path)
- [ ] Can retrieve exam (reads from user path)
- [ ] Data appears in correct Firestore structure
- [ ] Logout and re-login works
- [ ] Multi-user data isolation verified

---

**Estimated Time:** 15-20 minutes for complete migration
**Complexity:** Medium (mostly find/replace)
**Risk:** Low (easily reversible with Git)

