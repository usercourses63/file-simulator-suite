import { useState } from 'react';
import { ServerStatus } from '../types/server';
import { getHealthState, getHealthStateText } from '../utils/healthStatus';
import ServerSparkline from './ServerSparkline';
import './NasTable.css';

interface NasTableProps {
  servers: ServerStatus[];
  onRowClick: (server: ServerStatus) => void;
  sparklineData?: Map<string, number[]>;
  onSparklineClick?: (serverId: string) => void;
  showMultiSelect?: boolean;
  selectedIds?: Set<string>;
  onToggleSelect?: (id: string) => void;
  onDelete?: (server: ServerStatus) => void;
  dynamicInfo?: Record<string, { isDynamic: boolean; managedBy: string }>;
}

type GroupKey = 'input' | 'output' | 'backup' | 'other';

const GROUP_ORDER: GroupKey[] = ['input', 'output', 'backup', 'other'];

const GROUP_LABELS: Record<GroupKey, string> = {
  input: 'Input Servers',
  output: 'Output Servers',
  backup: 'Backup Servers',
  other: 'Other Servers',
};

/** Common prefix stripped from Helm-managed NAS server names for compact display. */
const HELM_PREFIX = 'file-sim-file-simulator-';

function getGroupKey(server: ServerStatus): GroupKey {
  const dir = (server.directory ?? '').toLowerCase();
  if (dir.includes('input')) return 'input';
  if (dir.includes('output')) return 'output';
  if (dir.includes('backup')) return 'backup';

  const name = server.name.toLowerCase();
  if (name.includes('input')) return 'input';
  if (name.includes('output')) return 'output';
  if (name.includes('backup')) return 'backup';

  return 'other';
}

function groupServers(servers: ServerStatus[]): Map<GroupKey, ServerStatus[]> {
  const groups = new Map<GroupKey, ServerStatus[]>();
  for (const server of servers) {
    const key = getGroupKey(server);
    const list = groups.get(key) ?? [];
    list.push(server);
    groups.set(key, list);
  }
  return groups;
}

function formatLatency(latencyMs?: number): string {
  if (latencyMs === undefined) return '--';
  if (latencyMs < 1000) return `${latencyMs}ms`;
  return `${(latencyMs / 1000).toFixed(1)}s`;
}

function shortName(name: string, isDynamic: boolean): string {
  if (isDynamic) return name;
  if (name.startsWith(HELM_PREFIX)) return name.slice(HELM_PREFIX.length);
  return name;
}

interface GroupHealthSummary {
  label: string;
  modifier: 'healthy' | 'mixed' | 'down';
}

function getGroupHealth(servers: ServerStatus[]): GroupHealthSummary {
  let healthyCount = 0;
  for (const s of servers) {
    const state = getHealthState(s);
    if (state === 'healthy') healthyCount++;
  }

  if (healthyCount === servers.length) {
    return { label: 'All Healthy', modifier: 'healthy' };
  }
  if (healthyCount === 0) {
    return { label: 'All Down', modifier: 'down' };
  }
  return { label: `${healthyCount}/${servers.length} Healthy`, modifier: 'mixed' };
}

/**
 * Renders NAS servers as compact grouped table rows.
 *
 * Groups servers by directory type (Input/Output/Backup) with sub-headers
 * showing server count and aggregate health. Each row is ~40px tall for
 * maximum density -- all 7 NAS servers visible without scrolling at 1920x1080.
 */
export function NasTable({
  servers,
  onRowClick,
  sparklineData,
  onSparklineClick,
  showMultiSelect,
  selectedIds,
  onToggleSelect,
  onDelete,
  dynamicInfo,
}: NasTableProps) {
  const [expandedServer, setExpandedServer] = useState<string | null>(null);

  if (servers.length === 0) return null;

  const handleChevronClick = (e: React.MouseEvent, serverName: string) => {
    e.stopPropagation();
    setExpandedServer(prev => prev === serverName ? null : serverName);
  };

  const groups = groupServers(servers);
  const tableClass = showMultiSelect ? 'nas-table nas-table--with-checkbox' : 'nas-table';

  return (
    <div className={tableClass}>
      {GROUP_ORDER.map((key) => {
        const groupServers = groups.get(key);
        if (!groupServers || groupServers.length === 0) return null;

        const health = getGroupHealth(groupServers);

        return (
          <div className="nas-table__group" key={key}>
            <div className="nas-table__group-header">
              <span className="nas-table__group-label">
                {GROUP_LABELS[key]} ({groupServers.length})
              </span>
              <span className={`nas-table__group-health nas-table__group-health--${health.modifier}`}>
                {health.label}
              </span>
            </div>

            {groupServers.map((server) => {
              const healthState = getHealthState(server);
              const info = dynamicInfo?.[server.name];
              const isDynamic = info?.isDynamic ?? server.isDynamic;
              const isSelected = selectedIds?.has(server.name) ?? false;
              const isExpanded = expandedServer === server.name;

              const rowClasses = [
                'nas-table__row',
                healthState === 'down' ? 'nas-table__row--down' : '',
                isSelected ? 'nas-table__row--selected' : '',
                isExpanded ? 'nas-table__row--expanded' : '',
              ]
                .filter(Boolean)
                .join(' ');

              return (
                <div key={server.name}>
                  <div
                    className={rowClasses}
                    onClick={() => onRowClick(server)}
                    role="button"
                    tabIndex={0}
                    onKeyDown={(e) => e.key === 'Enter' && onRowClick(server)}
                  >
                    {/* Checkbox column (only when showMultiSelect) */}
                    {showMultiSelect && (
                      <div className="nas-table__checkbox">
                        {isDynamic && (
                          <input
                            type="checkbox"
                            checked={isSelected}
                            onChange={() => onToggleSelect?.(server.name)}
                            onClick={(e) => e.stopPropagation()}
                          />
                        )}
                      </div>
                    )}

                    {/* Status dot */}
                    <span className={`nas-table__status-dot nas-table__status-dot--${healthState}`} />

                    {/* Server name */}
                    <span className="nas-table__cell--name" title={server.name}>
                      {shortName(server.name, isDynamic)}
                    </span>

                    {/* Directory */}
                    <span className="nas-table__cell--directory">
                      {server.directory ?? ''}
                    </span>

                    {/* Latency */}
                    <span className="nas-table__cell--latency">
                      {formatLatency(server.latencyMs)}
                    </span>

                    {/* Port */}
                    <span className="nas-table__cell--port">
                      {server.nodePort ?? server.port}
                    </span>

                    {/* Badge */}
                    <span className="nas-table__cell--badge">
                      {isDynamic ? (
                        <span className="badge badge--dynamic" style={{ fontSize: '9px', padding: '1px 4px' }}>
                          <svg viewBox="0 0 24 24" fill="currentColor" stroke="none" width="8" height="8">
                            <path d="M13 2L3 14h9l-1 10 10-12h-9l1-10z" />
                          </svg>
                          Dynamic
                        </span>
                      ) : (
                        <span className="badge badge--helm" style={{ fontSize: '9px', padding: '1px 4px' }}>
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="8" height="8">
                            <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2z" />
                            <path d="M12 8v8M8 12h8" />
                          </svg>
                          Helm
                        </span>
                      )}
                    </span>

                    {/* Chevron (toggles expand/collapse) */}
                    <span
                      className={`nas-table__cell--chevron${isExpanded ? ' nas-table__cell--chevron--expanded' : ''}`}
                      onClick={(e) => handleChevronClick(e, server.name)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') {
                          e.stopPropagation();
                          setExpandedServer(prev => prev === server.name ? null : server.name);
                        }
                      }}
                    >
                      &rsaquo;
                    </span>

                    {/* Delete button (dynamic only, shown on hover) */}
                    {isDynamic && onDelete && (
                      <button
                        className="nas-table__delete-btn"
                        onClick={(e) => {
                          e.stopPropagation();
                          onDelete(server);
                        }}
                        title="Delete server"
                        type="button"
                      >
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M3 6h18M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2m3 0v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6h14z" />
                        </svg>
                      </button>
                    )}
                  </div>

                  {/* Expanded detail section */}
                  {isExpanded && (
                    <div className="nas-table__row-detail">
                      <div className="nas-table__detail-sparkline">
                        <span className="nas-table__detail-label">Latency Trend</span>
                        <ServerSparkline
                          data={sparklineData?.get(server.name) ?? []}
                          isHealthy={server.isHealthy}
                          onClick={() => onSparklineClick?.(server.name)}
                          width={200}
                          height={30}
                        />
                      </div>
                      <div className="nas-table__detail-grid">
                        <div className="nas-table__detail-field">
                          <span className="nas-table__detail-label">Health</span>
                          <span className={`nas-table__detail-value nas-table__detail-value--${healthState}`}>
                            {getHealthStateText(healthState)}
                          </span>
                        </div>
                        {server.healthMessage && (
                          <div className="nas-table__detail-field">
                            <span className="nas-table__detail-label">Message</span>
                            <span className="nas-table__detail-value">{server.healthMessage}</span>
                          </div>
                        )}
                        <div className="nas-table__detail-field">
                          <span className="nas-table__detail-label">Service</span>
                          <span className="nas-table__detail-value nas-table__detail-value--mono">
                            {server.serviceName ?? 'N/A'}
                          </span>
                        </div>
                        <div className="nas-table__detail-field">
                          <span className="nas-table__detail-label">Cluster IP</span>
                          <span className="nas-table__detail-value nas-table__detail-value--mono">
                            {server.clusterIp ?? 'N/A'}
                          </span>
                        </div>
                        <div className="nas-table__detail-field">
                          <span className="nas-table__detail-label">Last Checked</span>
                          <span className="nas-table__detail-value">
                            {new Date(server.checkedAt).toLocaleTimeString()}
                          </span>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        );
      })}
    </div>
  );
}

export default NasTable;
