/**
 * TypeScript types for metrics API and SignalR events.
 * Matches backend DTOs from FileSimulator.ControlApi.
 */

// Query parameters for metrics API
export interface MetricsQueryParams {
  serverId?: string;
  serverType?: string;
  startTime: string;  // ISO 8601
  endTime: string;    // ISO 8601
}

// Raw sample from /api/metrics/samples
export interface HealthSampleDto {
  id: number;
  timestamp: string;  // ISO 8601
  serverId: string;
  serverType: string;
  isHealthy: boolean;
  latencyMs: number | null;
}

// Hourly aggregation from /api/metrics/hourly
export interface HealthHourlyDto {
  id: number;
  hourStart: string;  // ISO 8601
  serverId: string;
  serverType: string;
  sampleCount: number;
  healthyCount: number;
  avgLatencyMs: number | null;
  minLatencyMs: number | null;
  maxLatencyMs: number | null;
  p95LatencyMs: number | null;
  uptimePercent: number;
}

// Response from /api/metrics/samples
export interface MetricsSamplesResponse {
  samples: HealthSampleDto[];
  totalCount: number;
  queryStart: string;
  queryEnd: string;
}

// Response from /api/metrics/hourly
export interface MetricsHourlyResponse {
  hourly: HealthHourlyDto[];
  totalCount: number;
  queryStart: string;
  queryEnd: string;
}

// Server with metrics info from /api/metrics/servers
export interface ServerWithMetrics {
  serverId: string;
  serverType: string;
  firstSample: string;
  lastSample: string;
  totalSamples: number;
}

// Real-time sample from SignalR MetricsHub
export interface MetricsSampleEvent {
  timestamp: string;
  samples: Array<{
    serverId: string;
    serverType: string;
    isHealthy: boolean;
    latencyMs: number | null;
  }>;
}

// Time range preset
export interface TimeRangePreset {
  label: string;
  value: string;  // e.g., "1h", "6h", "24h", "7d"
}

// Chart data point (flattened for Recharts)
export interface ChartDataPoint {
  timestamp: number;  // Unix timestamp for XAxis
  [serverId: string]: number | null | undefined;  // Latency per server
}
