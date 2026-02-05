import { useState, useMemo } from 'react';
import { Alert, AlertStats, AlertSeverity } from '../types/alert';
import { formatDistanceToNow } from 'date-fns';
import './AlertsTab.css';

interface AlertsTabProps {
  alerts: Alert[];
  stats: AlertStats;
  loading: boolean;
}

const ALERTS_PER_PAGE = 50;

/**
 * Dedicated tab for viewing alert history with filtering and pagination.
 */
export function AlertsTab({ alerts, stats, loading }: AlertsTabProps) {
  const [severityFilter, setSeverityFilter] = useState<AlertSeverity | 'all'>('all');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [currentPage, setCurrentPage] = useState(1);

  // Get unique alert types for filter dropdown
  const alertTypes = useMemo(() => {
    const types = new Set(alerts.map(a => a.type));
    return Array.from(types).sort();
  }, [alerts]);

  // Filter alerts based on selected filters
  const filteredAlerts = useMemo(() => {
    return alerts.filter(alert => {
      // Severity filter
      if (severityFilter !== 'all' && alert.severity !== severityFilter) {
        return false;
      }

      // Type filter
      if (typeFilter !== 'all' && alert.type !== typeFilter) {
        return false;
      }

      // Search query (title, message, or source)
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        return (
          alert.title.toLowerCase().includes(query) ||
          alert.message.toLowerCase().includes(query) ||
          alert.source.toLowerCase().includes(query)
        );
      }

      return true;
    });
  }, [alerts, severityFilter, typeFilter, searchQuery]);

  // Sort by triggeredAt descending (newest first)
  const sortedAlerts = useMemo(() => {
    return [...filteredAlerts].sort((a, b) => {
      const timeA = new Date(a.triggeredAt).getTime();
      const timeB = new Date(b.triggeredAt).getTime();
      return timeB - timeA;
    });
  }, [filteredAlerts]);

  // Paginate alerts
  const totalPages = Math.ceil(sortedAlerts.length / ALERTS_PER_PAGE);
  const paginatedAlerts = useMemo(() => {
    const start = (currentPage - 1) * ALERTS_PER_PAGE;
    const end = start + ALERTS_PER_PAGE;
    return sortedAlerts.slice(start, end);
  }, [sortedAlerts, currentPage]);

  // Reset to page 1 when filters change
  const handleFilterChange = (callback: () => void) => {
    callback();
    setCurrentPage(1);
  };

  return (
    <div className="alerts-tab">
      <div className="alerts-tab__stats">
        <div className="stat-card">
          <div className="stat-card__value">{stats.totalCount}</div>
          <div className="stat-card__label">Total Active</div>
        </div>
        <div className="stat-card stat-card--info">
          <div className="stat-card__value">{stats.infoCount}</div>
          <div className="stat-card__label">Info</div>
        </div>
        <div className="stat-card stat-card--warning">
          <div className="stat-card__value">{stats.warningCount}</div>
          <div className="stat-card__label">Warning</div>
        </div>
        <div className="stat-card stat-card--critical">
          <div className="stat-card__value">{stats.criticalCount}</div>
          <div className="stat-card__label">Critical</div>
        </div>
      </div>

      <div className="alerts-tab__filters">
        <div className="filter-group">
          <label htmlFor="severity-filter">Severity:</label>
          <select
            id="severity-filter"
            value={severityFilter}
            onChange={(e) => handleFilterChange(() => setSeverityFilter(e.target.value as AlertSeverity | 'all'))}
          >
            <option value="all">All</option>
            <option value="Info">Info</option>
            <option value="Warning">Warning</option>
            <option value="Critical">Critical</option>
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="type-filter">Type:</label>
          <select
            id="type-filter"
            value={typeFilter}
            onChange={(e) => handleFilterChange(() => setTypeFilter(e.target.value))}
          >
            <option value="all">All</option>
            {alertTypes.map(type => (
              <option key={type} value={type}>{type}</option>
            ))}
          </select>
        </div>

        <div className="filter-group filter-group--search">
          <label htmlFor="search-filter">Search:</label>
          <input
            id="search-filter"
            type="text"
            placeholder="Search alerts..."
            value={searchQuery}
            onChange={(e) => handleFilterChange(() => setSearchQuery(e.target.value))}
          />
        </div>
      </div>

      {loading ? (
        <div className="alerts-tab__loading">
          <div className="loading-spinner"></div>
          <p>Loading alerts...</p>
        </div>
      ) : filteredAlerts.length === 0 ? (
        <div className="alerts-tab__empty">
          {alerts.length === 0 ? (
            <>
              <div className="empty-icon">✓</div>
              <p>No alerts found</p>
              <p className="empty-subtitle">Your system is healthy</p>
            </>
          ) : (
            <>
              <div className="empty-icon">🔍</div>
              <p>No alerts match your filters</p>
              <p className="empty-subtitle">Try adjusting your search criteria</p>
            </>
          )}
        </div>
      ) : (
        <>
          <div className="alerts-tab__table-container">
            <table className="alerts-tab__table">
              <thead>
                <tr>
                  <th>Severity</th>
                  <th>Type</th>
                  <th>Title</th>
                  <th>Message</th>
                  <th>Source</th>
                  <th>Triggered</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {paginatedAlerts.map(alert => (
                  <tr key={alert.id}>
                    <td>
                      <span className={`alert-badge alert-badge--${alert.severity.toLowerCase()}`}>
                        {alert.severity}
                      </span>
                    </td>
                    <td>{alert.type}</td>
                    <td className="alerts-tab__title">{alert.title}</td>
                    <td className="alerts-tab__message">{alert.message}</td>
                    <td>{alert.source}</td>
                    <td>
                      <span title={new Date(alert.triggeredAt).toLocaleString()}>
                        {formatDistanceToNow(new Date(alert.triggeredAt), { addSuffix: true })}
                      </span>
                    </td>
                    <td>
                      {alert.isResolved ? (
                        <span className="alert-status alert-status--resolved">Resolved</span>
                      ) : (
                        <span className="alert-status alert-status--active">Active</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="alerts-tab__pagination">
              <button
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                disabled={currentPage === 1}
                type="button"
              >
                Previous
              </button>
              <span>
                Page {currentPage} of {totalPages} ({sortedAlerts.length} alerts)
              </span>
              <button
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages}
                type="button"
              >
                Next
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}

export default AlertsTab;
