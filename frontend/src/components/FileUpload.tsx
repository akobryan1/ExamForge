import { useState, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { FileService, UploadedFile } from '../services/FileService';
import { Button } from './Button';
import '../styles/components/file-upload.css';

interface FileUploadProps {
  examId?: string;
  type?: 'material' | 'attachment' | 'other';
  maxFiles?: number;
  onFilesChange?: (files: UploadedFile[]) => void;
  initialFiles?: UploadedFile[];
}

export function FileUpload({
  examId,
  type = 'other',
  maxFiles = 10,
  onFilesChange,
  initialFiles = [],
}: FileUploadProps) {
  const [files, setFiles] = useState<UploadedFile[]>(initialFiles);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState<number>(0);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const selectedFiles = e.target.files;
    if (!selectedFiles || selectedFiles.length === 0) return;

    if (files.length + selectedFiles.length > maxFiles) {
      setError(`Maximum ${maxFiles} files allowed`);
      return;
    }

    setUploading(true);
    setError(null);
    
    try {
      const uploadedFiles: UploadedFile[] = [];
      const totalFiles = selectedFiles.length;

      for (let i = 0; i < totalFiles; i++) {
        const file = selectedFiles[i];
        setUploadProgress(Math.round(((i + 1) / totalFiles) * 100));
        
        const uploaded = await FileService.uploadFile(file, type, examId);
        uploadedFiles.push(uploaded);
      }

      const newFiles = [...files, ...uploadedFiles];
      setFiles(newFiles);
      onFilesChange?.(newFiles);
      
      // Reset input
      if (fileInputRef.current) {
        fileInputRef.current.value = '';
      }
    } catch (err: any) {
      setError(err.message || 'Failed to upload files');
    } finally {
      setUploading(false);
      setUploadProgress(0);
    }
  };

  const handleDelete = async (fileId: string) => {
    if (!confirm('Are you sure you want to delete this file?')) return;

    try {
      await FileService.deleteFile(fileId);
      const newFiles = files.filter(f => f.id !== fileId);
      setFiles(newFiles);
      onFilesChange?.(newFiles);
    } catch (err: any) {
      setError(err.message || 'Failed to delete file');
    }
  };

  const handleDownload = async (fileId: string, originalName: string) => {
    try {
      const blob = await FileService.downloadFile(fileId);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = originalName;
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
    } catch (err: any) {
      setError(err.message || 'Failed to download file');
    }
  };

  return (
    <div className="file-upload-container">
      <div className="file-upload-header">
        <Button
          variant="outline"
          onClick={() => fileInputRef.current?.click()}
          disabled={uploading || files.length >= maxFiles}
        >
          📎 Upload Files
        </Button>
        {files.length > 0 && (
          <span className="file-count">
            {files.length} / {maxFiles} files
          </span>
        )}
      </div>

      <input
        ref={fileInputRef}
        type="file"
        multiple
        onChange={handleFileSelect}
        style={{ display: 'none' }}
        accept=".pdf,.doc,.docx,.ppt,.pptx,.txt,.jpg,.jpeg,.png,.gif"
      />

      {uploading && (
        <div className="upload-progress">
          <div className="progress-bar">
            <div className="progress-fill" style={{ width: `${uploadProgress}%` }} />
          </div>
          <span className="progress-text">Uploading... {uploadProgress}%</span>
        </div>
      )}

      {error && (
        <div className="error-message">
          {error}
          <button className="error-close" onClick={() => setError(null)}>×</button>
        </div>
      )}

      <AnimatePresence>
        {files.length > 0 && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="file-list"
          >
            {files.map((file) => (
              <motion.div
                key={file.id}
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 20 }}
                className="file-item"
              >
                <div className="file-icon">📄</div>
                <div className="file-info">
                  <div className="file-name">{file.originalName}</div>
                  <div className="file-meta">
                    {FileService.formatFileSize(file.size)} • {new Date(file.uploadedAt).toLocaleDateString()}
                  </div>
                </div>
                <div className="file-actions">
                  <button
                    className="action-button download"
                    onClick={() => handleDownload(file.id, file.originalName)}
                    title="Download"
                  >
                    ⬇️
                  </button>
                  <button
                    className="action-button delete"
                    onClick={() => handleDelete(file.id)}
                    title="Delete"
                  >
                    🗑️
                  </button>
                </div>
              </motion.div>
            ))}
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
