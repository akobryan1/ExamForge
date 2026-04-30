import express, { Router, Request, Response } from 'express';
import multer, { FileFilterCallback } from 'multer';
import path from 'path';
import fs from 'fs';
import { FileUploadService } from '../services/FileUploadService';
import { authenticate } from '../middleware/auth';

const router: Router = express.Router();

// Ensure uploads directory exists
const uploadsDir = path.join(__dirname, '../../uploads');
if (!fs.existsSync(uploadsDir)) {
  fs.mkdirSync(uploadsDir, { recursive: true });
}

// Configure multer storage
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    cb(null, uploadsDir);
  },
  filename: (req, file, cb) => {
    const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1E9);
    const ext = path.extname(file.originalname);
    cb(null, `${file.fieldname}-${uniqueSuffix}${ext}`);
  }
});

// File filter to restrict file types
const fileFilter = (req: Request, file: Express.Multer.File, cb: FileFilterCallback) => {
  const allowedTypes = [
    'application/pdf',
    'application/msword',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/vnd.ms-powerpoint',
    'application/vnd.openxmlformats-officedocument.presentationml.presentation',
    'text/plain',
    'image/jpeg',
    'image/png',
    'image/gif',
  ];

  if (allowedTypes.includes(file.mimetype)) {
    cb(null, true);
  } else {
    cb(new Error('Invalid file type. Only PDF, Word, PowerPoint, text, and image files are allowed.'));
  }
};

// Multer configurations for different use cases
const uploadMaterial = multer({
  storage,
  fileFilter,
  limits: { fileSize: 100 * 1024 * 1024 }, // 100MB for materials
});

const uploadAttachment = multer({
  storage,
  fileFilter,
  limits: { fileSize: 25 * 1024 * 1024 }, // 25MB for general attachments
});

// All routes require authentication
router.use(authenticate);

/**
 * POST /api/files/upload - Upload a file
 */
router.post('/upload', (req, res, next) => {
  const fileType = req.query.type as string || 'other';
  const upload = fileType === 'material' ? uploadMaterial : uploadAttachment;

  upload.single('file')(req, res, async (err) => {
    if (err instanceof multer.MulterError) {
      if (err.code === 'LIMIT_FILE_SIZE') {
        return res.status(400).json({ error: 'File too large. Maximum size is 100MB for materials and 25MB for attachments.' });
      }
      return res.status(400).json({ error: err.message });
    } else if (err) {
      return res.status(400).json({ error: err.message });
    }

    if (!req.file) {
      return res.status(400).json({ error: 'No file uploaded' });
    }

    try {
      const examId = req.body.examId || undefined;
      const metadata = await FileUploadService.saveFileMetadata(
        req.user!.userId,
        req.file,
        examId,
        fileType as 'material' | 'attachment' | 'other'
      );

      return res.status(201).json(metadata);
    } catch (error: any) {
      // Clean up uploaded file if metadata save fails
      if (req.file && fs.existsSync(req.file.path)) {
        fs.unlinkSync(req.file.path);
      }
      return res.status(500).json({ error: error.message });
    }
  });
});

/**
 * GET /api/files - Get user's uploaded files
 */
router.get('/', async (req: Request, res: Response) => {
  try {
    const examId = req.query.examId as string | undefined;
    const files = await FileUploadService.getUserFiles(req.user!.userId, examId);
    return res.json(files);
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * GET /api/files/:id - Download a file
 */
router.get('/:id', async (req: Request, res: Response) => {
  try {
    const file = await FileUploadService.getFileById(req.params.id, req.user!.userId);
    
    if (!file) {
      return res.status(404).json({ error: 'File not found' });
    }

    const filePath = FileUploadService.getFilePath(file.filename, uploadsDir);
    
    if (!fs.existsSync(filePath)) {
      return res.status(404).json({ error: 'File not found on server' });
    }

    return res.download(filePath, file.originalName);
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

/**
 * DELETE /api/files/:id - Delete a file
 */
router.delete('/:id', async (req: Request, res: Response) => {
  try {
    await FileUploadService.deleteFile(req.params.id, req.user!.userId, uploadsDir);
    return res.json({ message: 'File deleted successfully' });
  } catch (error: any) {
    return res.status(500).json({ error: error.message });
  }
});

export default router;
