import { useState, useCallback } from 'react';
import type { FileNode, FileOperationResult } from '../types/fileTypes';

/**
 * Result returned by useFileOperations hook.
 */
export interface UseFileOperationsResult {
  /** Loading state for any operation */
  isLoading: boolean;
  /** Last error message */
  error: string | null;
  /** Fetch directory contents */
  fetchTree: (path?: string) => Promise<FileNode[]>;
  /** Upload a file */
  uploadFile: (file: File, targetPath?: string) => Promise<FileOperationResult>;
  /** Download a file (triggers browser download) */
  downloadFile: (path: string) => Promise<void>;
  /** Delete a file or directory */
  deleteFile: (path: string, recursive?: boolean) => Promise<FileOperationResult>;
}

/**
 * Hook for file operations via REST API.
 *
 * @param apiBaseUrl - Base URL for the API (e.g., "http://192.168.49.2:30500")
 * @returns File operation functions and state
 */
export function useFileOperations(apiBaseUrl: string): UseFileOperationsResult {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchTree = useCallback(async (path?: string): Promise<FileNode[]> => {
    setIsLoading(true);
    setError(null);

    try {
      const url = path
        ? `${apiBaseUrl}/api/files/tree?path=${encodeURIComponent(path)}`
        : `${apiBaseUrl}/api/files/tree`;

      const response = await fetch(url);

      if (!response.ok) {
        throw new Error(`Failed to fetch: ${response.statusText}`);
      }

      return await response.json();
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Unknown error';
      setError(message);
      return [];
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const uploadFile = useCallback(async (
    file: File,
    targetPath?: string
  ): Promise<FileOperationResult> => {
    setIsLoading(true);
    setError(null);

    try {
      const formData = new FormData();
      formData.append('file', file);

      const url = targetPath
        ? `${apiBaseUrl}/api/files/upload?path=${encodeURIComponent(targetPath)}`
        : `${apiBaseUrl}/api/files/upload`;

      const response = await fetch(url, {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || response.statusText);
      }

      const result = await response.json();
      return { success: true, file: result };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Upload failed';
      setError(message);
      return { success: false, message };
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const downloadFile = useCallback(async (path: string): Promise<void> => {
    setIsLoading(true);
    setError(null);

    try {
      const url = `${apiBaseUrl}/api/files/download?path=${encodeURIComponent(path)}`;
      const response = await fetch(url);

      if (!response.ok) {
        throw new Error(`Download failed: ${response.statusText}`);
      }

      // Trigger browser download
      const blob = await response.blob();
      const downloadUrl = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = downloadUrl;
      a.download = path.split('/').pop() || path.split('\\').pop() || 'download';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(downloadUrl);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Download failed';
      setError(message);
      throw err;
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  const deleteFile = useCallback(async (
    path: string,
    recursive = false
  ): Promise<FileOperationResult> => {
    setIsLoading(true);
    setError(null);

    try {
      const params = new URLSearchParams({ path });
      if (recursive) params.append('recursive', 'true');

      const response = await fetch(`${apiBaseUrl}/api/files?${params}`, {
        method: 'DELETE',
      });

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || response.statusText);
      }

      return { success: true, message: 'Deleted successfully' };
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Delete failed';
      setError(message);
      return { success: false, message };
    } finally {
      setIsLoading(false);
    }
  }, [apiBaseUrl]);

  return {
    isLoading,
    error,
    fetchTree,
    uploadFile,
    downloadFile,
    deleteFile,
  };
}

export default useFileOperations;
