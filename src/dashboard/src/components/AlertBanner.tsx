import { Alert } from '../types/alert';

/**
 * Props for AlertBanner component.
 */
export interface AlertBannerProps {
  /** List of active (unresolved) alerts */
  alerts: Alert[];
}

/**
 * Persistent banner displaying unresolved alert counts.
 * Appears below the header when there are active alerts.
 * Color-coded by highest severity level.
 */
export function AlertBanner({ alerts }: AlertBannerProps) {
  // If no alerts, don't render anything
  if (alerts.length === 0) {
    return null;
  }

  // Group alerts by severity
  const criticalCount = alerts.filter(a => a.severity === 'Critical').length;
  const warningCount = alerts.filter(a => a.severity === 'Warning').length;
  const infoCount = alerts.filter(a => a.severity === 'Info').length;

  // Determine highest severity for banner styling
  const highestSeverity = criticalCount > 0
    ? 'critical'
    : warningCount > 0
    ? 'warning'
    : 'info';

  // Create banner message
  const parts: string[] = [];
  if (criticalCount > 0) {
    parts.push(`${criticalCount} Critical`);
  }
  if (warningCount > 0) {
    parts.push(`${warningCount} Warning`);
  }
  if (infoCount > 0) {
    parts.push(`${infoCount} Info`);
  }

  const message = parts.join(', ') + ` alert${alerts.length > 1 ? 's' : ''} active`;

  return (
    <div className={`alert-banner alert-banner--${highestSeverity}`}>
      <div className="alert-banner__content">
        {criticalCount > 0 && (
          <span className="alert-banner__count alert-banner__count--critical">
            {criticalCount} Critical
          </span>
        )}
        {warningCount > 0 && (
          <span className="alert-banner__count alert-banner__count--warning">
            {warningCount} Warning
          </span>
        )}
        {infoCount > 0 && (
          <span className="alert-banner__count alert-banner__count--info">
            {infoCount} Info
          </span>
        )}
        <span className="alert-banner__message">{message}</span>
      </div>
    </div>
  );
}

export default AlertBanner;
