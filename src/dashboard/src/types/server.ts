/**
 * Protocol types supported by the file simulator suite.
 * Matches backend Protocol enum.
 */
export type Protocol = 'FTP' | 'SFTP' | 'HTTP' | 'S3' | 'SMB' | 'NFS';

/**
 * Kubernetes pod status.
 * Matches backend PodStatus values.
 */
export type PodStatus = 'Running' | 'Pending' | 'Failed' | 'Unknown';

/**
 * Health state derived from IsHealthy + LatencyMs for UI display.
 * - healthy: Pod running, health check passed, latency < 3000ms
 * - degraded: Pod running, health check passed, latency >= 3000ms
 * - down: Pod not running or health check failed
 * - unknown: Initial state before first health check
 */
export type HealthState = 'healthy' | 'degraded' | 'down' | 'unknown';

/**
 * Real-time status of a protocol server.
 * Matches backend ServerStatus record.
 */
export interface ServerStatus {
  Name: string;
  Protocol: Protocol;
  PodStatus: PodStatus;
  IsHealthy: boolean;
  HealthMessage?: string;
  LatencyMs?: number;
  CheckedAt: string; // ISO 8601 timestamp
}

/**
 * Collection of all server statuses, broadcast via SignalR.
 * Matches backend ServerStatusUpdate record.
 */
export interface ServerStatusUpdate {
  Servers: ServerStatus[];
  Timestamp: string; // ISO 8601 timestamp
  TotalServers: number;
  HealthyServers: number;
}
