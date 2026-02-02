import { useState, useCallback } from 'react';
import { ServerStatus } from '../types/server';
import { getHealthState, getHealthStateText } from '../utils/healthStatus';
import { getProtocolInfo, getExternalConnectionString } from '../utils/protocolInfo';

interface ServerDetailsPanelProps {
  server: ServerStatus | null;
  onClose: () => void;
}

/**
 * Right sidebar panel showing detailed server information.
 *
 * Features:
 * - Slide-in animation when opening
 * - Server name, protocol, and health status
 * - Connection strings with copy-to-clipboard
 * - Protocol-specific configuration details
 * - Credentials displayed in plain text (dev convenience)
 * - Last 5 check timestamps (if available)
 */
export function ServerDetailsPanel({ server, onClose }: ServerDetailsPanelProps) {
  const [copiedField, setCopiedField] = useState<string | null>(null);

  // Copy text to clipboard with visual feedback
  const copyToClipboard = useCallback(async (text: string, fieldName: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedField(fieldName);
      setTimeout(() => setCopiedField(null), 2000);
    } catch (err) {
      console.error('Failed to copy:', err);
    }
  }, []);

  // Don't render if no server selected
  if (!server) {
    return null;
  }

  const healthState = getHealthState(server);
  const healthText = getHealthStateText(healthState);
  const protocolInfo = getProtocolInfo(server.name, server.protocol);
  const externalConnection = getExternalConnectionString(server.protocol);

  // Render a copiable field with button
  const renderCopyField = (label: string, value: string, fieldKey: string) => (
    <div className="detail-field detail-field--copyable">
      <span className="field-label">{label}</span>
      <div className="field-value-wrapper">
        <code className="field-value">{value}</code>
        <button
          className={`copy-button ${copiedField === fieldKey ? 'copy-button--copied' : ''}`}
          onClick={() => copyToClipboard(value, fieldKey)}
          title="Copy to clipboard"
        >
          {copiedField === fieldKey ? 'Copied!' : 'Copy'}
        </button>
      </div>
    </div>
  );

  return (
    <aside className={`details-panel ${server ? 'details-panel--open' : ''}`}>
      <div className="panel-header">
        <div className="panel-title">
          <h3>{server.name}</h3>
          <span className="panel-protocol">{protocolInfo.displayName}</span>
        </div>
        <button
          className="panel-close"
          onClick={onClose}
          aria-label="Close details panel"
        >
          &times;
        </button>
      </div>

      <div className="panel-content">
        {/* Status Section */}
        <section className="panel-section">
          <h4 className="section-heading">Status</h4>
          <div className="detail-field">
            <span className="field-label">Health</span>
            <span className={`field-value status-badge status-badge--${healthState}`}>
              {healthText}
            </span>
          </div>
          <div className="detail-field">
            <span className="field-label">Pod Status</span>
            <span className="field-value">{server.podStatus}</span>
          </div>
          {server.healthMessage && (
            <div className="detail-field">
              <span className="field-label">Message</span>
              <span className="field-value field-value--message">{server.healthMessage}</span>
            </div>
          )}
        </section>

        {/* Metrics Section */}
        <section className="panel-section">
          <h4 className="section-heading">Metrics</h4>
          <div className="detail-field">
            <span className="field-label">Latency</span>
            <span className="field-value">
              {server.latencyMs !== undefined ? `${server.latencyMs}ms` : 'N/A'}
            </span>
          </div>
          <div className="detail-field">
            <span className="field-label">Last Checked</span>
            <span className="field-value">
              {new Date(server.checkedAt).toLocaleTimeString()}
            </span>
          </div>
        </section>

        {/* Connection Section */}
        <section className="panel-section">
          <h4 className="section-heading">Connection</h4>
          {renderCopyField('Cluster Internal', protocolInfo.connectionString, 'internal')}
          {renderCopyField('External (Minikube)', externalConnection, 'external')}
          <div className="detail-field">
            <span className="field-label">Port</span>
            <span className="field-value">{protocolInfo.defaultPort}</span>
          </div>
          <div className="detail-field">
            <span className="field-label">NodePort</span>
            <span className="field-value">{protocolInfo.nodePort}</span>
          </div>
        </section>

        {/* Credentials Section (if available) */}
        {protocolInfo.credentials && (
          <section className="panel-section">
            <h4 className="section-heading">Credentials</h4>
            {renderCopyField('Username', protocolInfo.credentials.username, 'username')}
            {renderCopyField('Password', protocolInfo.credentials.password, 'password')}
          </section>
        )}

        {/* Protocol-Specific Configuration */}
        <section className="panel-section">
          <h4 className="section-heading">Configuration</h4>
          {Object.entries(protocolInfo.config).map(([key, value]) => (
            <div key={key} className="detail-field">
              <span className="field-label">{key}</span>
              <span className="field-value">{value}</span>
            </div>
          ))}
        </section>
      </div>
    </aside>
  );
}

export default ServerDetailsPanel;
