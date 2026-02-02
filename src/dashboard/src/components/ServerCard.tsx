import { useState, useEffect, useRef } from 'react';
import { ServerStatus } from '../types/server';
import { getHealthState, getHealthStateText } from '../utils/healthStatus';

interface ServerCardProps {
  server: ServerStatus;
  onClick: () => void;
}

/**
 * Individual server status card showing name, protocol, health, and latency.
 *
 * Features:
 * - Colored left border indicating health state (green/yellow/red)
 * - Status dot with health state text
 * - Latency display in milliseconds
 * - Brief pulse animation when status changes
 * - Click handler to open details panel
 */
export function ServerCard({ server, onClick }: ServerCardProps) {
  const healthState = getHealthState(server);
  const healthText = getHealthStateText(healthState);

  // Track previous health state to trigger animation on change
  const prevHealthState = useRef(healthState);
  const [isAnimating, setIsAnimating] = useState(false);

  useEffect(() => {
    if (prevHealthState.current !== healthState) {
      setIsAnimating(true);
      prevHealthState.current = healthState;

      // Remove animation class after animation completes
      const timer = setTimeout(() => setIsAnimating(false), 600);
      return () => clearTimeout(timer);
    }
  }, [healthState]);

  // Format latency for display
  const formatLatency = (latencyMs?: number) => {
    if (latencyMs === undefined) return '-';
    if (latencyMs < 1000) return `${latencyMs}ms`;
    return `${(latencyMs / 1000).toFixed(1)}s`;
  };

  return (
    <div
      className={`server-card server-card--${healthState} ${isAnimating ? 'server-card--pulse' : ''}`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onClick()}
    >
      <div className="server-card-header">
        <span className="server-name">{server.name}</span>
        <span className="server-protocol">{server.protocol}</span>
      </div>

      <div className="server-card-body">
        <div className="server-status">
          <span className={`status-dot status-dot--${healthState}`}></span>
          <span className="status-text">{healthText}</span>
        </div>

        <div className="server-metrics">
          <span className="metric-label">Latency:</span>
          <span className="metric-value">{formatLatency(server.latencyMs)}</span>
        </div>
      </div>

      {server.healthMessage && (
        <div className="server-card-footer">
          <span className="health-message">{server.healthMessage}</span>
        </div>
      )}
    </div>
  );
}

export default ServerCard;
