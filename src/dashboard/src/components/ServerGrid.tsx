import { ServerStatus } from '../types/server';
import ServerCard from './ServerCard';

interface DynamicServerInfo {
  isDynamic: boolean;
  managedBy: string;
}

interface ServerGridProps {
  servers: ServerStatus[];
  onCardClick: (server: ServerStatus) => void;
  sparklineData?: Map<string, number[]>;  // serverId -> latency values
  onSparklineClick?: (serverId: string) => void;  // Navigate to History tab
  // Multi-select props
  showMultiSelect?: boolean;
  selectedIds?: Set<string>;
  onToggleSelect?: (id: string) => void;
  onDelete?: (server: ServerStatus) => void;
  // Dynamic server info
  dynamicInfo?: Record<string, DynamicServerInfo>;
}

/**
 * Displays server cards in a responsive grid, grouped by type.
 *
 * Groups:
 * - NAS Servers (7): Protocol === 'NFS'
 * - Protocol Servers (6): FTP, SFTP, HTTP, S3, SMB
 *
 * Uses CSS Grid with auto-fit for responsive wrapping.
 * Supports multi-select with checkbox and delete for dynamic servers.
 */
export function ServerGrid({
  servers,
  onCardClick,
  sparklineData,
  onSparklineClick,
  showMultiSelect,
  selectedIds,
  onToggleSelect,
  onDelete,
  dynamicInfo
}: ServerGridProps) {
  // Group servers by type
  const nasServers = servers.filter(s => s.protocol === 'NFS');
  const protocolServers = servers.filter(s => s.protocol !== 'NFS');

  const renderServerCard = (server: ServerStatus) => {
    const info = dynamicInfo?.[server.name];
    return (
      <ServerCard
        key={server.name}
        server={server}
        onClick={() => onCardClick(server)}
        sparklineData={sparklineData?.get(server.name)}
        onSparklineClick={() => onSparklineClick?.(server.name)}
        showCheckbox={showMultiSelect}
        isSelected={selectedIds?.has(server.name)}
        onToggleSelect={onToggleSelect ? () => onToggleSelect(server.name) : undefined}
        onDelete={onDelete ? () => onDelete(server) : undefined}
        isDynamic={info?.isDynamic ?? false}
        managedBy={info?.managedBy ?? 'Helm'}
      />
    );
  };

  return (
    <div className="server-grid-container">
      <section className="server-section">
        <h2 className="section-title">
          NAS Servers
          <span className="section-count">({nasServers.length})</span>
        </h2>
        <div className="server-grid">
          {nasServers.map(renderServerCard)}
          {nasServers.length === 0 && (
            <div className="server-grid-empty">No NAS servers found</div>
          )}
        </div>
      </section>

      <section className="server-section">
        <h2 className="section-title">
          Protocol Servers
          <span className="section-count">({protocolServers.length})</span>
        </h2>
        <div className="server-grid">
          {protocolServers.map(renderServerCard)}
          {protocolServers.length === 0 && (
            <div className="server-grid-empty">No protocol servers found</div>
          )}
        </div>
      </section>
    </div>
  );
}

export default ServerGrid;
