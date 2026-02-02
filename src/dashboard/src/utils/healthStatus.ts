import { ServerStatus, HealthState } from '../types/server';

/**
 * Latency threshold in ms above which server is considered degraded.
 */
const DEGRADED_LATENCY_THRESHOLD = 3000;

/**
 * Determine the health state of a server for UI display.
 *
 * @param server - Server status from backend
 * @returns HealthState for CSS class and display
 */
export function getHealthState(server: ServerStatus): HealthState {
  // Pod not running = down
  if (server.PodStatus !== 'Running') {
    return 'down';
  }

  // Health check failed = down
  if (!server.IsHealthy) {
    return 'down';
  }

  // High latency = degraded
  if (server.LatencyMs !== undefined && server.LatencyMs >= DEGRADED_LATENCY_THRESHOLD) {
    return 'degraded';
  }

  // All checks passed = healthy
  return 'healthy';
}

/**
 * Get display text for health state.
 */
export function getHealthStateText(state: HealthState): string {
  switch (state) {
    case 'healthy':
      return 'Healthy';
    case 'degraded':
      return 'Degraded';
    case 'down':
      return 'Down';
    case 'unknown':
      return 'Checking...';
  }
}

/**
 * Count servers by health state.
 */
export function countByHealthState(servers: ServerStatus[]): Record<HealthState, number> {
  const counts: Record<HealthState, number> = {
    healthy: 0,
    degraded: 0,
    down: 0,
    unknown: 0
  };

  for (const server of servers) {
    const state = getHealthState(server);
    counts[state]++;
  }

  return counts;
}
