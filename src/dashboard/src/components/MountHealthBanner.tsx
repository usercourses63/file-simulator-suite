import { useState } from 'react';
import { MountHealthStatus } from '../types/mountHealth';
import './MountHealthBanner.css';

export interface MountHealthBannerProps {
  status: MountHealthStatus | null;
}

function formatTimeSince(isoDate: string): string {
  const diff = Date.now() - new Date(isoDate).getTime();
  const seconds = Math.floor(diff / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m ago`;
}

export function MountHealthBanner({ status }: MountHealthBannerProps) {
  const [dismissed, setDismissed] = useState(false);

  // Don't render if healthy, no status yet, or dismissed
  if (!status || status.state === 'Healthy' || dismissed) {
    return null;
  }

  // Reset dismissed state if state changes to a worse state
  // (handled by parent re-rendering with new status)

  const stateClass = status.state.toLowerCase();
  const timeSinceHealthy = status.lastHealthyAt
    ? formatTimeSince(status.lastHealthyAt)
    : 'unknown';

  return (
    <div className={`mount-health-banner mount-health-banner--${stateClass}`}>
      <div className="mount-health-banner__content">
        <span className="mount-health-banner__icon">
          {status.state === 'Stale' ? '!' : status.state === 'Recovering' ? '~' : '?'}
        </span>
        <span className="mount-health-banner__state">
          Mount {status.state}
        </span>
        {status.message && (
          <span className="mount-health-banner__message">
            {status.message}
          </span>
        )}
        <span className="mount-health-banner__time">
          Last healthy: {timeSinceHealthy}
        </span>
      </div>
      <button
        className="mount-health-banner__dismiss"
        onClick={() => setDismissed(true)}
        title="Dismiss"
        type="button"
      >
        x
      </button>
    </div>
  );
}

export default MountHealthBanner;
