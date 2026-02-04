import { useState, useEffect, useRef } from 'react';
import { ServerStatus } from '../types/server';
import { getHealthState, getHealthStateText } from '../utils/healthStatus';
import ServerSparkline from './ServerSparkline';

interface ServerCardProps {
  server: ServerStatus;
  onClick: () => void;
  sparklineData?: number[];  // Latency values for sparkline
  onSparklineClick?: () => void;  // Navigate to History tab
  // New props for multi-select
  showCheckbox?: boolean;
  isSelected?: boolean;
  onToggleSelect?: () => void;
  onDelete?: () => void;
  isDynamic?: boolean;
  managedBy?: string;
}

/**
 * Individual server status card showing name, protocol, health, and latency.
 *
 * Features:
 * - Colored left border indicating health state (green/yellow/red)
 * - Status dot with health state text
 * - Latency display in milliseconds
 * - Brief pulse animation when status changes
 * - Mini sparkline showing latency trend
 * - Click handler to open details panel
 * - Checkbox for multi-select (dynamic servers only)
 * - Delete button on hover (dynamic servers only)
 * - Badge showing Dynamic vs Helm-managed
 */
export function ServerCard({
  server,
  onClick,
  sparklineData,
  onSparklineClick,
  showCheckbox,
  isSelected,
  onToggleSelect,
  onDelete,
  isDynamic: isDynamicProp
  // managedBy is received but not currently used; reserved for future tooltip
}: ServerCardProps) {
  // Use prop if provided, otherwise fall back to server's isDynamic property
  const isDynamic = isDynamicProp ?? server.isDynamic;
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

  // Handle sparkline click without triggering card click
  const handleSparklineClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onSparklineClick?.();
  };

  // Handle checkbox click without triggering card click
  const handleCheckboxClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onToggleSelect?.();
  };

  // Handle delete click without triggering card click
  const handleDeleteClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    onDelete?.();
  };

  return (
    <div
      className={`server-card server-card--${healthState} ${isAnimating ? 'server-card--pulse' : ''} ${isSelected ? 'server-card--selected' : ''}`}
      onClick={onClick}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onClick()}
    >
      {/* Selection checkbox for dynamic servers */}
      {showCheckbox && isDynamic && (
        <div className="server-card-checkbox" onClick={handleCheckboxClick}>
          <input
            type="checkbox"
            checked={isSelected}
            onChange={() => onToggleSelect?.()}
            onClick={e => e.stopPropagation()}
          />
        </div>
      )}

      <div className="server-card-header">
        <div className="server-name-row">
          <span className="server-name">{server.name}</span>
          {server.directory && (
            <span className="server-directory" title={`Serves ${server.directory}`}>
              {server.directory}
            </span>
          )}
        </div>
        <div className="server-badges">
          <span className="server-protocol">{server.protocol}</span>
          {isDynamic ? (
            <span className="badge badge--dynamic">Dynamic</span>
          ) : (
            <span className="badge badge--helm" title="Managed by Helm - cannot be deleted">
              Helm
            </span>
          )}
        </div>
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

      {sparklineData && sparklineData.length > 0 && (
        <div className="server-card-sparkline" onClick={handleSparklineClick}>
          <ServerSparkline
            data={sparklineData}
            isHealthy={server.isHealthy}
            onClick={onSparklineClick}
          />
        </div>
      )}

      {server.healthMessage && (
        <div className="server-card-footer">
          <span className="health-message">{server.healthMessage}</span>
        </div>
      )}

      {/* Delete button for dynamic servers */}
      {isDynamic && onDelete && (
        <button
          className="server-card-delete"
          onClick={handleDeleteClick}
          title="Delete server"
          type="button"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M3 6h18M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2m3 0v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6h14z" />
          </svg>
        </button>
      )}
    </div>
  );
}

export default ServerCard;
