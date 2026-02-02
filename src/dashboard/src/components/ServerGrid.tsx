import { ServerStatus } from '../types/server';
import ServerCard from './ServerCard';

interface ServerGridProps {
  servers: ServerStatus[];
  onCardClick: (server: ServerStatus) => void;
}

/**
 * Displays server cards in a responsive grid, grouped by type.
 *
 * Groups:
 * - NAS Servers (7): Protocol === 'NFS'
 * - Protocol Servers (6): FTP, SFTP, HTTP, S3, SMB
 *
 * Uses CSS Grid with auto-fit for responsive wrapping.
 */
export function ServerGrid({ servers, onCardClick }: ServerGridProps) {
  // Group servers by type
  const nasServers = servers.filter(s => s.protocol === 'NFS');
  const protocolServers = servers.filter(s => s.protocol !== 'NFS');

  return (
    <div className="server-grid-container">
      <section className="server-section">
        <h2 className="section-title">
          NAS Servers
          <span className="section-count">({nasServers.length})</span>
        </h2>
        <div className="server-grid">
          {nasServers.map(server => (
            <ServerCard
              key={server.name}
              server={server}
              onClick={() => onCardClick(server)}
            />
          ))}
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
          {protocolServers.map(server => (
            <ServerCard
              key={server.name}
              server={server}
              onClick={() => onCardClick(server)}
            />
          ))}
          {protocolServers.length === 0 && (
            <div className="server-grid-empty">No protocol servers found</div>
          )}
        </div>
      </section>
    </div>
  );
}

export default ServerGrid;
