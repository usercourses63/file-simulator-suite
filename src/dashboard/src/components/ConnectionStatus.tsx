import { useMemo } from 'react';

interface ConnectionStatusProps {
  isConnected: boolean;
  isReconnecting: boolean;
  reconnectAttempt: number;
  lastUpdate: Date | null;
}

/**
 * Displays SignalR WebSocket connection status with reconnection feedback.
 *
 * Shows:
 * - "Connected" with green dot when connected
 * - "Reconnecting (attempt X/5)..." with yellow dot during reconnection
 * - "Disconnected" with red dot when not connected
 * - "Last update: Xs ago" timestamp
 */
export function ConnectionStatus({
  isConnected,
  isReconnecting,
  reconnectAttempt,
  lastUpdate
}: ConnectionStatusProps) {
  // Calculate time since last update
  const timeAgo = useMemo(() => {
    if (!lastUpdate) return null;
    const seconds = Math.floor((Date.now() - lastUpdate.getTime()) / 1000);
    if (seconds < 60) return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    return `${minutes}m ago`;
  }, [lastUpdate]);

  // Determine status text and state
  const getStatusInfo = () => {
    if (isReconnecting) {
      return {
        text: `Reconnecting (attempt ${reconnectAttempt}/5)...`,
        state: 'reconnecting' as const
      };
    }
    if (isConnected) {
      return {
        text: 'Connected',
        state: 'connected' as const
      };
    }
    return {
      text: 'Disconnected',
      state: 'disconnected' as const
    };
  };

  const { text, state } = getStatusInfo();

  return (
    <div className="connection-status">
      <div className={`connection-indicator connection-indicator--${state}`}>
        <span className="connection-dot"></span>
        <span className="connection-text">{text}</span>
      </div>
      {lastUpdate && (
        <span className="connection-time">Last update: {timeAgo}</span>
      )}
    </div>
  );
}

export default ConnectionStatus;
