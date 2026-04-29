import { apiClient } from './apiClient';

export interface UploadedFile {
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

class FileServiceClass {
  /**
   * Upload a file
   */
  async uploadFile(
    file: File,
    type: 'material' | 'attachment' | 'other' = 'other',
    examId?: string
  ): Promise<UploadedFile> {
    const formData = new FormData();
    formData.append('file', file);
    if (examId) {
      formData.append('examId', examId);
    }

    const response = await apiClient.post(`/api/files/upload?type=${type}`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return {
      ...response.data,
      uploadedAt: new Date(response.data.uploadedAt),
    };
  }

  /**
   * Get user's uploaded files
   */
  async getUserFiles(examId?: string): Promise<UploadedFile[]> {
    const url = examId ? `/api/files?examId=${examId}` : '/api/files';
    const response = await apiClient.get(url);
    
    return response.data.map((file: any) => ({
      ...file,
      uploadedAt: new Date(file.uploadedAt),
    }));
  }

  /**
   * Download a file
   */
  async downloadFile(fileId: string): Promise<Blob> {
    const response = await apiClient.get(`/api/files/${fileId}`, {
      responseType: 'blob',
    });
    return response.data;
  }

  /**
   * Delete a file
   */
  async deleteFile(fileId: string): Promise<void> {
    await apiClient.delete(`/api/files/${fileId}`);
  }

  /**
   * Get download URL for a file
   */
  getDownloadUrl(fileId: string): string {
    return `${apiClient.defaults.baseURL}/api/files/${fileId}`;
  }

  /**
   * Format file size for display
   */
  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
  }
}

export const FileService = new FileServiceClass();
