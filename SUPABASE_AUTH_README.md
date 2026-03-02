# Supabase Authentication Setup

## Overview
ExamForge now uses Supabase for user authentication and user profile management. Each user's data is isolated using their unique user ID.

## Supabase Configuration
- **Project URL**: `https://fachmhcsfxjtgifexbyd.supabase.co`
- **Anon/Public Key**: Already configured in `SupabaseAuthService.cs`

## Database Schema
Table: `examforge_users`
- `userid` (uuid, primary key) - links to auth.users.id
- `username` (text, unique) - user's chosen username
- `email` (text) - user's email address
- `created_at` (timestamptz) - account creation timestamp

## Row Level Security (RLS) Policies
The following policies are active:
1. **Insert**: Users can only insert their own profile
2. **Select**: Users can only view their own profile  
3. **Update**: Users can only update their own profile

## Firestore Data Isolation
User data in Firestore is scoped using the pattern:
```
users/{userId}/exams/{examId}
users/{userId}/submissions/{submissionId}
```

This ensures each user can only access their own published exams and student submissions.

## Features Implemented
1. **Login UserControl** - Username/Email + Password login
2. **Sign-Up UserControl** - New account creation with username uniqueness check
3. **Account Recovery UserControl** - Password reset via email

## User Flow
1. App starts ? `AuthWindow` displays `LoginUserControl`
2. User logs in or creates account
3. `SupabaseAuthService` authenticates with Supabase
4. User ID stored globally in `App.SupabaseAuth`
5. `FirestoreService` initialized with user ID for data isolation
6. `MainWindow` opens with user-scoped data

## Security Notes
- Client secret never stored in desktop app
- All authentication handled by Supabase Auth
- Firestore queries automatically filtered by user ID
- RLS policies enforce data isolation at database level
