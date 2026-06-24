import { Routes, Route, Navigate } from 'react-router-dom'
import { ExamineeRegistrationPage } from './pages/ExamineeRegistrationPage'
import { ExamPreviewPage } from './pages/ExamPreviewPage'
import { TakeExamPage } from './pages/TakeExamPage'
import { ExamResultsPage } from './pages/ExamResultsPage'
import { StudentLoginPage } from './pages/StudentLoginPage'

console.log('[App] Module loaded');

function App() {
  console.log('[App] Rendering App component');
  return (
    <Routes>
      {/* Public routes */}
      <Route path="/register/exam/:examId" element={<ExamineeRegistrationPage />} />
      <Route path="/exams/:examId" element={<ExamPreviewPage />} />
      <Route path="/exams/:examId/take" element={<TakeExamPage />} />
      <Route path="/attempts/:attemptId/results" element={<ExamResultsPage />} />
      
      {/* Student login */}
      <Route path="/login" element={<StudentLoginPage />} />
      
      {/* Default redirect - registration page */}
      <Route path="/" element={<Navigate to="/register/exam" replace />} />
      <Route path="*" element={<Navigate to="/register/exam" replace />} />
    </Routes>
  )
}

export default App
