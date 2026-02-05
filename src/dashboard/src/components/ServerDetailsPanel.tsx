import { useState, useEffect, useCallback } from 'react';
import { useServerManagement } from '../hooks/useServerManagement';
import { ServerStatus } from '../types/server';
import { getHealthState, getHealthStateText } from '../utils/healthStatus';
import { getProtocolInfo } from '../utils/protocolInfo';
import {
  UpdateFtpServerRequest,
  UpdateSftpServerRequest,
  UpdateNasServerRequest,
  FtpConfiguration,
  SftpConfiguration,
  NasConfiguration
} from '../types/serverManagement';
import './ServerDetailsPanel.css';

interface ServerConfig {
  isDynamic: boolean;
  managedBy: string;
  ftp?: FtpConfiguration | null;
  sftp?: SftpConfiguration | null;
  nas?: NasConfiguration | null;
}

interface ServerDetailsPanelProps {
  server: ServerStatus | null;
  serverConfig?: ServerConfig;
  onClose: () => void;
  onUpdated?: () => void;
  apiBaseUrl?: string;
}

interface EditableField {
  key: string;
  label: string;
  value: string | number;
  type: 'text' | 'password' | 'number';
}

/**
 * Right sidebar panel showing detailed server information.
 *
 * Features:
 * - Slide-in animation when opening
 * - Server name, protocol, and health status
 * - Connection strings with copy-to-clipboard
 * - Protocol-specific configuration details
 * - Inline editing for dynamic servers
 * - Read-only mode for Helm-managed servers
 * - Lifecycle actions (start/stop/restart) for dynamic servers
 */
export function ServerDetailsPanel({
  server,
  serverConfig,
  onClose,
  onUpdated,
  apiBaseUrl = ''
}: ServerDetailsPanelProps) {
  const [copiedField, setCopiedField] = useState<string | null>(null);
  const [editingField, setEditingField] = useState<string | null>(null);
  const [editValue, setEditValue] = useState<string>('');

  const {
    updateFtpServer,
    updateSftpServer,
    updateNasServer,
    startServer,
    stopServer,
    restartServer,
    isLoading,
    error,
    clearError
  } = useServerManagement({ apiBaseUrl });

  // Reset state when panel closes or server changes
  useEffect(() => {
    if (!server) {
      setEditingField(null);
      setEditValue('');
      clearError();
    }
  }, [server, clearError]);

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

  const startEditing = (field: EditableField) => {
    setEditingField(field.key);
    setEditValue(field.type === 'password' ? '' : String(field.value));
  };

  const cancelEditing = () => {
    setEditingField(null);
    setEditValue('');
  };

  const saveField = useCallback(async (fieldKey: string, value: string | number) => {
    if (!server || !apiBaseUrl) return;

    const updates: Record<string, string | number | null> = {
      [fieldKey]: value
    };

    try {
      const protocol = server.protocol.toLowerCase();
      if (protocol === 'ftp') {
        await updateFtpServer(server.name, updates as UpdateFtpServerRequest);
      } else if (protocol === 'sftp') {
        await updateSftpServer(server.name, updates as UpdateSftpServerRequest);
      } else if (protocol === 'nas' || protocol === 'nfs') {
        await updateNasServer(server.name, updates as UpdateNasServerRequest);
      }

      setEditingField(null);
      setEditValue('');
      onUpdated?.();
    } catch {
      // Error displayed in panel
    }
  }, [server, apiBaseUrl, updateFtpServer, updateSftpServer, updateNasServer, onUpdated]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>, fieldKey: string, fieldType: string) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      const parsedValue = fieldType === 'number'
        ? parseInt(editValue) || 0
        : editValue;
      saveField(fieldKey, parsedValue);
    } else if (e.key === 'Escape') {
      cancelEditing();
    }
  };

  const handleLifecycleAction = async (action: 'start' | 'stop' | 'restart') => {
    if (!server) return;
    try {
      if (action === 'start') await startServer(server.name);
      else if (action === 'stop') await stopServer(server.name);
      else await restartServer(server.name);
      onUpdated?.();
    } catch {
      // Error displayed
    }
  };

  // Don't render if no server selected
  if (!server) {
    return null;
  }

  const healthState = getHealthState(server);
  const healthText = getHealthStateText(healthState);
  const protocolInfo = getProtocolInfo(server.name, server.protocol);

  // Use isDynamic directly from server status (from SignalR)
  const isDynamic = server.isDynamic;
  const isHelm = !isDynamic;
  const protocol = server.protocol.toLowerCase();

  // Build editable fields based on protocol
  const getEditableFields = (): EditableField[] => {
    const fields: EditableField[] = [];

    // NodePort is editable for dynamic servers
    if (isDynamic) {
      fields.push({
        key: 'nodePort',
        label: 'NodePort',
        value: protocolInfo.nodePort,
        type: 'number'
      });
    }

    if ((protocol === 'ftp' || protocol === 'sftp') && isDynamic) {
      const username = serverConfig?.ftp?.username ?? serverConfig?.sftp?.username ?? 'simuser';
      fields.push({ key: 'username', label: 'Username', value: username, type: 'text' });
      fields.push({ key: 'password', label: 'Password', value: '********', type: 'password' });
    }

    if (protocol === 'ftp' && isDynamic && serverConfig?.ftp) {
      if (serverConfig.ftp.passivePortStart) {
        fields.push({
          key: 'passivePortStart',
          label: 'Passive Port Start',
          value: serverConfig.ftp.passivePortStart,
          type: 'number'
        });
      }
      if (serverConfig.ftp.passivePortEnd) {
        fields.push({
          key: 'passivePortEnd',
          label: 'Passive Port End',
          value: serverConfig.ftp.passivePortEnd,
          type: 'number'
        });
      }
    }

    if (protocol === 'sftp' && isDynamic && serverConfig?.sftp) {
      fields.push({ key: 'uid', label: 'UID', value: serverConfig.sftp.uid, type: 'number' });
      fields.push({ key: 'gid', label: 'GID', value: serverConfig.sftp.gid, type: 'number' });
    }

    if ((protocol === 'nas' || protocol === 'nfs') && isDynamic && serverConfig?.nas) {
      fields.push({ key: 'directory', label: 'Directory', value: serverConfig.nas.directory, type: 'text' });
      fields.push({ key: 'exportOptions', label: 'Export Options', value: serverConfig.nas.exportOptions, type: 'text' });
    }

    return fields;
  };

  const editableFields = getEditableFields();

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

  // Render an editable field
  const renderEditableField = (field: EditableField) => (
    <div key={field.key} className="detail-field detail-field--editable">
      <span className="field-label">{field.label}</span>
      {editingField === field.key ? (
        <div className="field-edit-wrapper">
          <input
            type={field.type === 'password' ? 'text' : field.type}
            value={editValue}
            onChange={e => setEditValue(e.target.value)}
            onKeyDown={e => handleKeyDown(e, field.key, field.type)}
            autoFocus
            className="field-edit-input"
            placeholder={field.type === 'password' ? 'Enter new password' : ''}
          />
          <div className="field-edit-actions">
            <button
              type="button"
              className="edit-action-btn edit-action-btn--save"
              onClick={() => {
                const val = field.type === 'number' ? parseInt(editValue) || 0 : editValue;
                saveField(field.key, val);
              }}
              disabled={isLoading}
            >
              Save
            </button>
            <button
              type="button"
              className="edit-action-btn edit-action-btn--cancel"
              onClick={cancelEditing}
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <div className="field-value-row">
          <span className="field-value">{String(field.value)}</span>
          <button
            type="button"
            className="edit-btn"
            onClick={() => startEditing(field)}
            title="Edit"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7" />
              <path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z" />
            </svg>
          </button>
        </div>
      )}
    </div>
  );

  return (
    <aside className={`details-panel ${server ? 'details-panel--open' : ''}`}>
      <div className="panel-header">
        <div className="panel-title">
          <h3>{server.name}</h3>
          <div className="panel-badges">
            <span className="panel-protocol">{protocolInfo.displayName}</span>
            {isDynamic ? (
              <span className="badge badge--dynamic">Dynamic</span>
            ) : (
              <span className="badge badge--helm">Helm</span>
            )}
          </div>
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
        {/* Error display */}
        {error && (
          <div className="panel-error">
            {error}
          </div>
        )}

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

          {/* Lifecycle buttons for dynamic servers */}
          {isDynamic && apiBaseUrl && (
            <div className="lifecycle-actions">
              <button
                type="button"
                className="lifecycle-btn"
                onClick={() => handleLifecycleAction('stop')}
                disabled={isLoading}
              >
                Stop
              </button>
              <button
                type="button"
                className="lifecycle-btn"
                onClick={() => handleLifecycleAction('start')}
                disabled={isLoading}
              >
                Start
              </button>
              <button
                type="button"
                className="lifecycle-btn"
                onClick={() => handleLifecycleAction('restart')}
                disabled={isLoading}
              >
                Restart
              </button>
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
          {server.serviceName && server.clusterIp && (
            renderCopyField('Cluster Internal', `${server.clusterIp}:${server.port}`, 'internal')
          )}
          {server.nodePort && (
            renderCopyField('External (Minikube)', `file-simulator.local:${server.nodePort}`, 'external')
          )}
          <div className="detail-field">
            <span className="field-label">Port</span>
            <span className="field-value">{server.port || protocolInfo.defaultPort}</span>
          </div>
          <div className="detail-field">
            <span className="field-label">NodePort</span>
            <span className="field-value">{server.nodePort || protocolInfo.nodePort}</span>
          </div>
        </section>

        {/* Storage Section */}
        {server.directory && (
          <section className="panel-section">
            <h4 className="section-heading">Storage</h4>
            {renderCopyField('Windows Path', server.directory, 'directory')}
            <div className="detail-field">
              <span className="field-label">Description</span>
              <span className="field-value field-value--message">
                {server.directory.includes('internal')
                  ? 'S3/MinIO uses internal object storage (not shared PVC)'
                  : 'Files stored here are accessible via this protocol server'}
              </span>
            </div>
          </section>
        )}

        {/* Credentials Section (read-only for non-dynamic or when no edit support) */}
        {protocolInfo.credentials && !isDynamic && (
          <section className="panel-section">
            <h4 className="section-heading">Credentials</h4>
            {renderCopyField('Username', protocolInfo.credentials.username, 'username')}
            {renderCopyField('Password', protocolInfo.credentials.password, 'password')}
          </section>
        )}

        {/* Editable Configuration Section (for dynamic servers) */}
        {isDynamic && editableFields.length > 0 && apiBaseUrl && (
          <section className="panel-section">
            <h4 className="section-heading">Configuration</h4>
            {editableFields.map(field => renderEditableField(field))}
          </section>
        )}

        {/* Read-only Configuration (for Helm servers) */}
        {isHelm && (
          <section className="panel-section">
            <h4 className="section-heading">Configuration</h4>
            <p className="helm-notice">
              This server is managed by Helm. Settings are read-only.
            </p>
            {Object.entries(protocolInfo.config).map(([key, value]) => (
              <div key={key} className="detail-field">
                <span className="field-label">{key}</span>
                <span className="field-value">{value}</span>
              </div>
            ))}
          </section>
        )}
      </div>
    </aside>
  );
}

export default ServerDetailsPanel;
