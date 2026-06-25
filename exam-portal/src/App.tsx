import { Routes, Route, Navigate } from 'react-router-dom'
import { ExamPreviewPage } from './pages/ExamPreviewPage'
import { TakeExamPage } from './pages/TakeExamPage'
import { ExamResultsPage } from './pages/ExamResultsPage'

function App() {
  return (
    <Routes>
      <Route path="/exams/:examId" element={<ExamPreviewPage />} />
      <Route path="/exams/:examId/take" element={<TakeExamPage />} />
      <Route path="/attempts/:attemptId/results" element={<ExamResultsPage />} />
      <Route path="/" element={<Navigate to="/exams" replace />} />
      <Route path="*" element={<Navigate to="/exams" replace />} />
    </Routes>
  )
}

export default App
