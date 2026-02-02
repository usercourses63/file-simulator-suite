/**
 * Types for file operations and event streaming.
 * Matches backend DTOs from FileSimulator.ControlApi.
 */

/**
 * File event types from FileSystemWatcher.
 */
export type FileEventType = 'Created' | 'Modified' | 'Deleted' | 'Renamed';

/**
 * Protocol types that can access files.
 */
export type FileProtocol = 'FTP' | 'SFTP' | 'HTTP' | 'S3' | 'SMB' | 'NFS';

/**
 * Real-time file event from Windows directory watcher.
 * Received via SignalR /hubs/fileevents.
 */
export interface FileEvent {
  /** Full path to the file */
  path: string;
  /** Path relative to base directory */
  relativePath: string;
  /** Just the filename */
  fileName: string;
  /** Type of file system event */
  eventType: FileEventType;
  /** For rename events, the previous path */
  oldPath?: string;
  /** When the event occurred (ISO 8601) */
  timestamp: string;
  /** Protocols that can see this file */
  protocols: FileProtocol[];
  /** Whether this is a directory event */
  isDirectory: boolean;
}

/**
 * File or directory node in the file tree.
 * Received from GET /api/files/tree.
 */
export interface FileNode {
  /** Path relative to base directory (unique ID) */
  id: string;
  /** File or directory name */
  name: string;
  /** True if this is a directory */
  isDirectory: boolean;
  /** File size in bytes (undefined for directories) */
  size?: number;
  /** Last modified timestamp (ISO 8601) */
  modified: string;
  /** Protocols that can access this path */
  protocols: FileProtocol[];
  /** Child nodes (for directories) */
  children?: FileNode[];
}

/**
 * Upload progress tracking.
 */
export interface UploadProgress {
  fileName: string;
  progress: number; // 0-100
  status: 'pending' | 'uploading' | 'complete' | 'error';
  error?: string;
}

/**
 * File operation result.
 */
export interface FileOperationResult {
  success: boolean;
  message?: string;
  file?: FileNode;
}
