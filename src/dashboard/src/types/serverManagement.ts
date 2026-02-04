/**
 * Server management types matching backend DTOs.
 */

// Base request properties
export interface CreateServerRequestBase {
  name: string;
  nodePort?: number | null;
}

// FTP server creation
export interface CreateFtpServerRequest extends CreateServerRequestBase {
  username: string;
  password: string;
  passivePortStart?: number | null;
  passivePortEnd?: number | null;
  directory?: string | null;  // Subdirectory of shared PVC (e.g., "input", "output")
}

// SFTP server creation
export interface CreateSftpServerRequest extends CreateServerRequestBase {
  username: string;
  password: string;
  uid?: number;
  gid?: number;
  directory?: string | null;  // Subdirectory of shared PVC (e.g., "input", "output")
}

// NAS server creation
export interface CreateNasServerRequest extends CreateServerRequestBase {
  directory: string;
  exportOptions?: string;
}

// Union type for any server creation
export type CreateServerRequest =
  | { protocol: 'ftp'; data: CreateFtpServerRequest }
  | { protocol: 'sftp'; data: CreateSftpServerRequest }
  | { protocol: 'nas'; data: CreateNasServerRequest };

// Server with dynamic flag (extends base server type)
export interface DynamicServer {
  name: string;
  protocol: string;
  podName: string;
  serviceName: string;
  clusterIp: string;
  port: number;
  nodePort?: number | null;
  podStatus: string;
  podReady: boolean;
  discoveredAt: string;
  isDynamic: boolean;
  managedBy: string;
}

// Lifecycle action types
export type LifecycleAction = 'start' | 'stop' | 'restart';

// Update request types (for inline editing)
export interface UpdateFtpServerRequest {
  username?: string;
  password?: string;
  nodePort?: number | null;
  passivePortStart?: number | null;
  passivePortEnd?: number | null;
}

export interface UpdateSftpServerRequest {
  username?: string;
  password?: string;
  nodePort?: number | null;
  uid?: number;
  gid?: number;
}

export interface UpdateNasServerRequest {
  directory?: string;
  nodePort?: number | null;
  exportOptions?: string;
}

// Import/export types
export interface FtpConfiguration {
  username: string;
  password: string;
  passivePortStart?: number | null;
  passivePortEnd?: number | null;
}

export interface SftpConfiguration {
  username: string;
  password: string;
  uid: number;
  gid: number;
}

export interface NasConfiguration {
  directory: string;
  exportOptions: string;
}

export interface ServerConfiguration {
  name: string;
  protocol: string;
  nodePort?: number | null;
  isDynamic: boolean;
  ftp?: FtpConfiguration | null;
  sftp?: SftpConfiguration | null;
  nas?: NasConfiguration | null;
}

export interface ExportMetadata {
  description?: string | null;
  exportedBy?: string | null;
  environment?: string | null;
}

export interface ServerConfigurationExport {
  version: string;
  exportedAt: string;
  namespace: string;
  releasePrefix: string;
  servers: ServerConfiguration[];
  metadata?: ExportMetadata | null;
}

export interface ImportResult {
  created: string[];
  skipped: string[];
  failed: Record<string, string>;
  totalProcessed: number;
}

export type ConflictResolutionStrategy = 'Skip' | 'Replace' | 'Rename';

export interface ImportConfigurationRequest {
  configuration: ServerConfigurationExport;
  strategy: ConflictResolutionStrategy;
}

// Per-conflict resolution (for interactive import)
export interface ConflictResolution {
  serverName: string;
  action: 'skip' | 'replace' | 'rename';
  newName?: string;  // Only if action === 'rename'
}

export interface InteractiveImportRequest {
  configuration: ServerConfigurationExport;
  resolutions: ConflictResolution[];
}

// API response types
export interface ApiError {
  error: string;
  details?: string;
  errors?: Record<string, string[]>;
}

// Deployment progress
export interface DeploymentProgress {
  phase: 'validating' | 'creating-deployment' | 'creating-service' | 'updating-configmap' | 'complete' | 'error';
  message: string;
  serverName?: string;
}

// Name availability check
export interface NameCheckResult {
  name: string;
  available: boolean;
}
