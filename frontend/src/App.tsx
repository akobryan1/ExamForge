import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider } from './contexts/AuthContext'
import { ProtectedRoute } from './components/ProtectedRoute'
import { Login } from './pages/Login'
import { Signup } from './pages/Signup'
import { Dashboard } from './pages/Dashboard'
import { ExamsPage } from './pages/ExamsPage'
import { CreateExamPageEnhanced } from './pages/CreateExamPageEnhanced'
import { QuestionsPage } from './pages/QuestionsPage'
import { ExamPreviewPage } from './pages/ExamPreviewPage'
import { TakeExamPage } from './pages/TakeExamPage'
import { ExamResultsPage } from './pages/ExamResultsPage'
import { GradingQueuePage } from './pages/GradingQueuePage'
import { AnalyticsPage } from './pages/AnalyticsPage'
import { IncidentReportsPage } from './pages/IncidentReportsPage'
import { StudentRegistrationPage } from './pages/StudentRegistrationPage';
import { AIQuestionGeneratorPage } from './pages/AIQuestionGeneratorPage';
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      retry: 1,
    },
  },
})

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <Routes>
            {/* Public routes */}
            <Route path="/login" element={<Login />} />
            <Route path="/signup" element={<Signup />} />
            <Route path="/register/student" element={<StudentRegistrationPage />} />
            
            {/* Protected routes */}
            <Route
              path="/dashboard"
              element={
                <ProtectedRoute>
                  <Dashboard />
                </ProtectedRoute>
              }
            />
            <Route
              path="/exams"
              element={
                <ProtectedRoute>
                  <ExamsPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/exams/create"
              element={
                <ProtectedRoute>
                  <CreateExamPageEnhanced />
                </ProtectedRoute>
              }
            />
            <Route
              path="/exams/:examId/edit"
              element={
                <ProtectedRoute>
                  <CreateExamPageEnhanced />
                </ProtectedRoute>
              }
            />
            <Route
              path="/exams/:examId/questions"
              element={
                <ProtectedRoute>
                  <QuestionsPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/exams/:examId/ai-generate"
              element={
                <ProtectedRoute>
                  <AIQuestionGeneratorPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/exams/:examId"
              element={
                <ProtectedRoute>
                  <ExamPreviewPage />
                </ProtectedRoute>
              }
            />
            {/* Public route — guests allowed if exam has allowGuestAccess */}
            <Route
              path="/exams/:examId/take"
              element={<TakeExamPage />}
            />
            <Route
              path="/attempts/:attemptId/results"
              element={
                <ProtectedRoute>
                  <ExamResultsPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/grading"
              element={
                <ProtectedRoute>
                  <GradingQueuePage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/analytics"
              element={
                <ProtectedRoute>
                  <AnalyticsPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/incidents"
              element={
                <ProtectedRoute>
                  <IncidentReportsPage />
                </ProtectedRoute>
              }
            />
            
            {/* Default redirect */}
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="*" element={<Navigate to="/dashboard" replace />} />
          </Routes>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  )
}

export default App
