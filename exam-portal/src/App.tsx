import { useEffect } from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { ExamPreviewPage } from './pages/ExamPreviewPage'
import { TakeExamPage } from './pages/TakeExamPage'
import { ExamResultsPage } from './pages/ExamResultsPage'
import { ExamLoginPage } from './pages/ExamLoginPage'

function App() {
  // Auto-logout when tab/window is closed
  useEffect(() => {
    const handleBeforeUnload = () => {
      localStorage.removeItem('accessToken');
    };
    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, []);

  return (
    <Routes>
      <Route path="/exams/:examId" element={<ExamPreviewPage />} />
      <Route path="/exams/:examId/take" element={<TakeExamPage />} />
      <Route path="/attempts/:attemptId/results" element={<ExamResultsPage />} />
      <Route path="/login" element={<ExamLoginPage />} />
      <Route path="/" element={<Navigate to="/exams" replace />} />
      <Route path="*" element={<Navigate to="/exams" replace />} />
    </Routes>
  )
}

export default App
