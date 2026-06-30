import express, { Express, Request, Response, NextFunction } from 'express';
import cors from 'cors';
import helmet from 'helmet';
import morgan from 'morgan';
import cookieParser from 'cookie-parser';
import dotenv from 'dotenv';

// Load environment variables
dotenv.config();

const app: Express = express();
const PORT = process.env.PORT || 5000;

// CORS Configuration
const allowedOrigins = [
  'http://localhost:3000',
  'http://localhost:5173',
  'https://examforge-frontend-vin7.onrender.com',
  'https://examfprge-student-portal.onrender.com',
  'https://examforge-exam-portal.onrender.com',
];

console.log('[CORS] Allowed origins:', allowedOrigins);

// Add CORS_ORIGIN from environment if set
if (process.env.CORS_ORIGIN) {
  const envOrigin = process.env.CORS_ORIGIN;
  console.log('[CORS] Adding CORS_ORIGIN from env:', envOrigin);
  allowedOrigins.push(envOrigin);
}

// Middleware
app.use(helmet()); // Security headers
app.use(cors({
  origin: (origin, callback) => {
    // Allow requests with no origin (like mobile apps or curl requests)
    if (!origin) {
      console.log('[CORS] No origin — allowing');
      return callback(null, true);
    }
    
    const allowed = allowedOrigins.includes(origin);
    console.log(`[CORS] origin=${origin} | allowed=${allowed} | match=${allowedOrigins.findIndex(o => o === origin)}`);
    
    if (allowed) {
      callback(null, true);
    } else {
      console.warn(`[CORS] BLOCKED origin: ${origin}`);
      console.warn(`[CORS] Allowed list:`, JSON.stringify(allowedOrigins));
      callback(new Error('Not allowed by CORS'));
    }
  },
  credentials: true,
  methods: ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'OPTIONS'],
  allowedHeaders: ['Content-Type', 'Authorization'],
}));

// Debug: log each incoming request's origin
app.use((req, _res, next) => {
  console.log(`[REQUEST] ${req.method} ${req.path} | origin=${req.headers.origin || 'none'} | referer=${req.headers.referer || 'none'}`);
  next();
});

app.use(morgan('dev')); // Request logging
app.use(express.json()); // Parse JSON bodies
app.use(express.urlencoded({ extended: true })); // Parse URL-encoded bodies
app.use(cookieParser()); // Parse cookies

// Cache-Control headers for GET responses
app.use((req, res, next) => {
  if (req.method === 'GET') {
    if (req.path.startsWith('/api/exams/analytics/')) {
      res.set('Cache-Control', 'private, max-age=300');
    } else if (
      req.path.startsWith('/api/exams/grading') ||
      req.path.startsWith('/api/exams/incidents')
    ) {
      res.set('Cache-Control', 'private, max-age=60');
    } else if (req.path.startsWith('/api/exams') && req.path !== '/api/exams/:id') {
      res.set('Cache-Control', 'private, max-age=60');
    }
  }
  next();
});

// Health check endpoint
app.get('/', (req: Request, res: Response) => {
  res.json({
    status: 'running',
    service: 'ExamForge Backend API',
    version: '1.0.0',
    timestamp: new Date().toISOString(),
  });
});

app.get('/health', (req: Request, res: Response) => {
  res.json({ status: 'healthy', uptime: process.uptime() });
});

// API Routes
import authRoutes from './routes/auth.js';
import examRoutes from './routes/exams.js';
import fileRoutes from './routes/files.js';
import aiQuestionRoutes from './routes/ai-questions.js';
import notificationRoutes from './routes/notifications.js';
import studentRoutes from './routes/students.js';

app.use('/api/auth', authRoutes);
app.use('/api/exams', examRoutes);
app.use('/api/files', fileRoutes);
app.use('/api/ai', aiQuestionRoutes);
app.use('/api/notifications', notificationRoutes);
app.use('/api/students', studentRoutes);
// app.use('/api/submissions', submissionRoutes);
// app.use('/api/analytics', analyticsRoutes);
// app.use('/api/grading', gradingRoutes);

// 404 handler
app.use((req: Request, res: Response) => {
  res.status(404).json({
    error: 'Not Found',
    message: `Route ${req.method} ${req.path} not found`,
  });
});

// Error handling middleware
app.use((err: Error, req: Request, res: Response, next: NextFunction) => {
  console.error('Error:', err);
  
  res.status(500).json({
    error: 'Internal Server Error',
    message: process.env.NODE_ENV === 'development' ? err.message : 'Something went wrong',
  });
});

// Start server
app.listen(PORT, () => {
  console.log(`🚀 ExamForge Backend API running on port ${PORT}`);
  console.log(`📍 Environment: ${process.env.NODE_ENV || 'development'}`);
  console.log(`🌐 CORS enabled for: ${process.env.CORS_ORIGIN || 'http://localhost:3000'}`);
  console.log(`📋 Allowed origins list:`, JSON.stringify(allowedOrigins, null, 2));
});

export default app;
