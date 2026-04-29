import { getFirestore } from '../config/firebase';
import { Timestamp } from '@google-cloud/firestore';
import * as fs from 'fs';
import * as path from 'path';

interface FileMetadata {
  id: string;
  originalName: string;
  filename: string;
  mimetype: string;
  size: number;
  uploadedBy: string;
  uploadedAt: Date;
  examId?: string;
  type: 'material' | 'attachment' | 'other';
}

export class FileUploadService {
  /**
   * Save file metadata to Firestore
   */
  static async saveFileMetadata(
    userId: string,
    file: Express.Multer.File,
    examId?: string,
    type: 'material' | 'attachment' | 'other' = 'other'
  ): Promise<FileMetadata> {
    const db = getFirestore();

    const metadata: Omit<FileMetadata, 'id'> = {
      originalName: file.originalname,
      filename: file.filename,
      mimetype: file.mimetype,
      size: file.size,
      uploadedBy: userId,
      uploadedAt: new Date(),
      examId,
      type,
    };

    const docRef = await db.collection(`examforge_users/${userId}/uploaded_files`).add({
      ...metadata,
      uploadedAt: Timestamp.fromDate(metadata.uploadedAt),
    });

    return {
      id: docRef.id,
      ...metadata,
    };
  }

  /**
   * Get user's uploaded files
   */
  static async getUserFiles(userId: string, examId?: string): Promise<FileMetadata[]> {
    const db = getFirestore();
    let query = db.collection(`examforge_users/${userId}/uploaded_files`).orderBy('uploadedAt', 'desc');

    if (examId) {
      query = query.where('examId', '==', examId) as any;
    }

    const snapshot = await query.get();
    return snapshot.docs.map(doc => ({
      id: doc.id,
      ...doc.data(),
      uploadedAt: doc.data().uploadedAt.toDate(),
    })) as FileMetadata[];
  }

  /**
   * Get file metadata by ID
   */
  static async getFileById(fileId: string, userId: string): Promise<FileMetadata | null> {
    const db = getFirestore();
    const doc = await db.doc(`examforge_users/${userId}/uploaded_files/${fileId}`).get();

    if (!doc.exists) {
      return null;
    }

    return {
      id: doc.id,
      ...doc.data(),
      uploadedAt: doc.data()!.uploadedAt.toDate(),
    } as FileMetadata;
  }

  /**
   * Delete file and its metadata
   */
  static async deleteFile(fileId: string, userId: string, uploadsDir: string): Promise<void> {
    const db = getFirestore();
    const fileDoc = await db.doc(`examforge_users/${userId}/uploaded_files/${fileId}`).get();

    if (!fileDoc.exists) {
      throw new Error('File not found');
    }

    const fileData = fileDoc.data();
    const filePath = path.join(uploadsDir, fileData!.filename);

    // Delete file from filesystem
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath);
    }

    // Delete metadata from Firestore
    await fileDoc.ref.delete();
  }

  /**
   * Get file path for serving
   */
  static getFilePath(filename: string, uploadsDir: string): string {
    return path.join(uploadsDir, filename);
  }
}
