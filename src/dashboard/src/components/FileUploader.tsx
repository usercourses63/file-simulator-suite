import { useCallback, useState } from 'react';
import { useDropzone } from 'react-dropzone';
import type { UploadProgress } from '../types/fileTypes';

interface FileUploaderProps {
  onUpload: (file: File) => Promise<{ success: boolean; message?: string }>;
  targetPath?: string;
  disabled?: boolean;
}

/**
 * Drag-and-drop file uploader using react-dropzone.
 * Supports multiple files with progress tracking.
 */
export function FileUploader({ onUpload, targetPath, disabled }: FileUploaderProps) {
  const [uploads, setUploads] = useState<UploadProgress[]>([]);

  const processFile = useCallback(async (file: File) => {
    // Add to uploads list
    setUploads(prev => [...prev, {
      fileName: file.name,
      progress: 0,
      status: 'uploading',
    }]);

    try {
      const result = await onUpload(file);

      setUploads(prev => prev.map(u =>
        u.fileName === file.name
          ? {
              ...u,
              progress: 100,
              status: result.success ? 'complete' : 'error',
              error: result.message,
            }
          : u
      ));

      // Remove completed uploads after 3 seconds
      if (result.success) {
        setTimeout(() => {
          setUploads(prev => prev.filter(u => u.fileName !== file.name));
        }, 3000);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Upload failed';
      setUploads(prev => prev.map(u =>
        u.fileName === file.name
          ? { ...u, status: 'error', error: message }
          : u
      ));
    }
  }, [onUpload]);

  const onDrop = useCallback((acceptedFiles: File[]) => {
    acceptedFiles.forEach(processFile);
  }, [processFile]);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    disabled,
    multiple: true,
    maxSize: 104857600, // 100 MB
  });

  const clearCompleted = useCallback(() => {
    setUploads(prev => prev.filter(u => u.status === 'uploading'));
  }, []);

  return (
    <div className="file-uploader">
      <div
        {...getRootProps()}
        className={`file-uploader__dropzone ${isDragActive ? 'file-uploader__dropzone--active' : ''} ${disabled ? 'file-uploader__dropzone--disabled' : ''}`}
      >
        <input {...getInputProps()} />
        {isDragActive ? (
          <p className="file-uploader__text">Drop files here...</p>
        ) : (
          <p className="file-uploader__text">
            Drag &amp; drop files here, or click to browse
            {targetPath && (
              <span className="file-uploader__target">
                Upload to: {targetPath || '/'}
              </span>
            )}
          </p>
        )}
      </div>

      {uploads.length > 0 && (
        <div className="file-uploader__list">
          <div className="file-uploader__list-header">
            <span>Uploads</span>
            <button
              className="file-uploader__clear"
              onClick={clearCompleted}
              type="button"
            >
              Clear completed
            </button>
          </div>
          {uploads.map(upload => (
            <div
              key={upload.fileName}
              className={`file-uploader__item file-uploader__item--${upload.status}`}
            >
              <span className="file-uploader__item-name">{upload.fileName}</span>
              <span className="file-uploader__item-status">
                {upload.status === 'uploading' && 'Uploading...'}
                {upload.status === 'complete' && 'Complete'}
                {upload.status === 'error' && (upload.error || 'Failed')}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default FileUploader;
