import { ServerStatus } from '../types/server';
import { countByHealthState } from '../utils/healthStatus';

interface SummaryHeaderProps {
  servers: ServerStatus[];
}

/**
 * Displays summary health counts: "X Healthy - Y Degraded - Z Down"
 *
 * Provides at-a-glance overview of all 13 servers without scanning individual cards.
 */
export function SummaryHeader({ servers }: SummaryHeaderProps) {
  const counts = countByHealthState(servers);

  return (
    <div className="summary-header">
      <div className="summary-counts">
        <span className="summary-count summary-count--healthy">
          <span className="summary-dot summary-dot--healthy"></span>
          {counts.healthy} Healthy
        </span>
        <span className="summary-separator">-</span>
        <span className="summary-count summary-count--degraded">
          <span className="summary-dot summary-dot--degraded"></span>
          {counts.degraded} Degraded
        </span>
        <span className="summary-separator">-</span>
        <span className="summary-count summary-count--down">
          <span className="summary-dot summary-dot--down"></span>
          {counts.down} Down
        </span>
      </div>
      <div className="summary-total">
        {servers.length} servers monitored
      </div>
    </div>
  );
}

export default SummaryHeader;
