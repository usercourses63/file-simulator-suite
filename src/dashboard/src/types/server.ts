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
 * Health state derived from isHealthy + latencyMs for UI display.
 * - healthy: Pod running, health check passed, latency < 3000ms
 * - degraded: Pod running, health check passed, latency >= 3000ms
 * - down: Pod not running or health check failed
 * - unknown: Initial state before first health check
 */
export type HealthState = 'healthy' | 'degraded' | 'down' | 'unknown';

/**
 * Real-time status of a protocol server.
 * Matches backend ServerStatus record (camelCase JSON serialization).
 */
export interface ServerStatus {
  name: string;
  protocol: Protocol;
  podStatus: PodStatus;
  isHealthy: boolean;
  healthMessage?: string;
  latencyMs?: number;
  checkedAt: string; // ISO 8601 timestamp
}

/**
 * Collection of all server statuses, broadcast via SignalR.
 * Matches backend ServerStatusUpdate record (camelCase JSON serialization).
 */
export interface ServerStatusUpdate {
  servers: ServerStatus[];
  timestamp: string; // ISO 8601 timestamp
  totalServers: number;
  healthyServers: number;
}
