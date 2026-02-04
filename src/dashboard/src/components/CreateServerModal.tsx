import { useState, useEffect, useCallback } from 'react';
import { useServerManagement } from '../hooks/useServerManagement';
import {
  CreateFtpServerRequest,
  CreateSftpServerRequest,
  CreateNasServerRequest,
  DeploymentProgress
} from '../types/serverManagement';
import './CreateServerModal.css';

type Protocol = 'ftp' | 'sftp' | 'nas';

interface CreateServerModalProps {
  isOpen: boolean;
  onClose: () => void;
  onCreated: () => void;
  apiBaseUrl: string;
}

// Directory presets for all protocols
const DIRECTORY_PRESETS = [
  { value: '', label: 'Root (C:\\simulator-data)' },
  { value: 'input', label: 'Input (C:\\simulator-data\\input)' },
  { value: 'output', label: 'Output (C:\\simulator-data\\output)' },
  { value: 'backup', label: 'Backup (C:\\simulator-data\\backup)' },
  { value: 'custom', label: 'Custom directory' }
];

// NAS-specific presets (required directory)
const NAS_PRESETS = [
  { value: 'input', label: 'Input (nas-input-dynamic)' },
  { value: 'output', label: 'Output (nas-output-dynamic)' },
  { value: 'backup', label: 'Backup (nas-backup-dynamic)' },
  { value: 'custom', label: 'Custom directory' }
];

export function CreateServerModal({ isOpen, onClose, onCreated, apiBaseUrl }: CreateServerModalProps) {
  const {
    createFtpServer,
    createSftpServer,
    createNasServer,
    checkNameAvailability,
    isLoading,
    error,
    progress,
    clearError,
    clearProgress
  } = useServerManagement({ apiBaseUrl });

  // Form state
  const [protocol, setProtocol] = useState<Protocol>('ftp');
  const [name, setName] = useState('');
  const [nameAvailable, setNameAvailable] = useState<boolean | null>(null);
  const [nameCheckTimeout, setNameCheckTimeout] = useState<number | null>(null);

  // Common options
  const [useAutoNodePort, setUseAutoNodePort] = useState(true);
  const [nodePort, setNodePort] = useState<number | null>(null);

  // FTP/SFTP options
  const [username, setUsername] = useState('simuser');
  const [password, setPassword] = useState('simpass');
  const [passivePortStart, setPassivePortStart] = useState<number | null>(null);
  const [passivePortEnd, setPassivePortEnd] = useState<number | null>(null);
  const [uid, setUid] = useState(1000);
  const [gid, setGid] = useState(1000);

  // Directory options (for FTP/SFTP/NAS)
  const [ftpSftpDirectory, setFtpSftpDirectory] = useState('');  // Empty = root
  const [ftpSftpCustomDir, setFtpSftpCustomDir] = useState('');

  // NAS options
  const [directoryPreset, setDirectoryPreset] = useState('input');
  const [customDirectory, setCustomDirectory] = useState('');
  const [exportOptions, setExportOptions] = useState('rw,sync,no_subtree_check,no_root_squash');

  // Reset form when modal opens/closes
  useEffect(() => {
    if (isOpen) {
      setProtocol('ftp');
      setName('');
      setNameAvailable(null);
      setUseAutoNodePort(true);
      setNodePort(null);
      setUsername('simuser');
      setPassword('simpass');
      setPassivePortStart(null);
      setPassivePortEnd(null);
      setUid(1000);
      setGid(1000);
      setFtpSftpDirectory('');
      setFtpSftpCustomDir('');
      setDirectoryPreset('input');
      setCustomDirectory('');
      setExportOptions('rw,sync,no_subtree_check,no_root_squash');
      clearError();
      clearProgress();
    }
  }, [isOpen, clearError, clearProgress]);

  // Debounced name availability check
  useEffect(() => {
    if (nameCheckTimeout) {
      clearTimeout(nameCheckTimeout);
    }

    if (!name.trim()) {
      setNameAvailable(null);
      return;
    }

    const timeout = window.setTimeout(async () => {
      try {
        const result = await checkNameAvailability(name.trim());
        setNameAvailable(result.available);
      } catch {
        setNameAvailable(null);
      }
    }, 300);

    setNameCheckTimeout(timeout);
    return () => clearTimeout(timeout);
  }, [name, checkNameAvailability]);

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();

    if (!name.trim() || nameAvailable === false) {
      return;
    }

    try {
      const effectiveNodePort = useAutoNodePort ? null : nodePort;

      if (protocol === 'ftp') {
        const effectiveDirectory = ftpSftpDirectory === 'custom' ? ftpSftpCustomDir : ftpSftpDirectory;
        const request: CreateFtpServerRequest = {
          name: name.trim(),
          username,
          password,
          nodePort: effectiveNodePort,
          passivePortStart,
          passivePortEnd,
          directory: effectiveDirectory || null
        };
        await createFtpServer(request);
      } else if (protocol === 'sftp') {
        const effectiveDirectory = ftpSftpDirectory === 'custom' ? ftpSftpCustomDir : ftpSftpDirectory;
        const request: CreateSftpServerRequest = {
          name: name.trim(),
          username,
          password,
          nodePort: effectiveNodePort,
          uid,
          gid,
          directory: effectiveDirectory || null
        };
        await createSftpServer(request);
      } else if (protocol === 'nas') {
        const directory = directoryPreset === 'custom' ? customDirectory : directoryPreset;
        const request: CreateNasServerRequest = {
          name: name.trim(),
          directory,
          nodePort: effectiveNodePort,
          exportOptions
        };
        await createNasServer(request);
      }

      onCreated();
      onClose();
    } catch {
      // Error is displayed in modal via progress state
    }
  }, [
    name, nameAvailable, protocol, useAutoNodePort, nodePort,
    username, password, passivePortStart, passivePortEnd,
    uid, gid, directoryPreset, customDirectory, exportOptions,
    createFtpServer, createSftpServer, createNasServer, onCreated, onClose
  ]);

  const getProgressClass = (phase: DeploymentProgress['phase']): string => {
    if (phase === 'complete') return 'progress--complete';
    if (phase === 'error') return 'progress--error';
    return 'progress--active';
  };

  if (!isOpen) return null;

  return (
    <div className="create-server-overlay" onClick={onClose}>
      <div className="create-server-modal" onClick={e => e.stopPropagation()}>
        <header className="modal-header">
          <h2>Create Server</h2>
          <button type="button" className="modal-close" onClick={onClose}>&times;</button>
        </header>

        {/* Progress indicator */}
        {progress && (
          <div className={`modal-progress ${getProgressClass(progress.phase)}`}>
            <div className="progress-indicator">
              {progress.phase !== 'complete' && progress.phase !== 'error' && (
                <div className="progress-spinner" />
              )}
              <span className="progress-message">{progress.message}</span>
            </div>
          </div>
        )}

        {/* Error display */}
        {error && (
          <div className="modal-error">
            <span className="error-icon">!</span>
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} className="modal-form">
          {/* Protocol Selection */}
          <div className="form-section">
            <label className="form-label">Protocol</label>
            <div className="protocol-selector">
              <button
                type="button"
                className={`protocol-btn ${protocol === 'ftp' ? 'protocol-btn--active' : ''}`}
                onClick={() => setProtocol('ftp')}
                disabled={isLoading}
              >
                FTP
              </button>
              <button
                type="button"
                className={`protocol-btn ${protocol === 'sftp' ? 'protocol-btn--active' : ''}`}
                onClick={() => setProtocol('sftp')}
                disabled={isLoading}
              >
                SFTP
              </button>
              <button
                type="button"
                className={`protocol-btn ${protocol === 'nas' ? 'protocol-btn--active' : ''}`}
                onClick={() => setProtocol('nas')}
                disabled={isLoading}
              >
                NAS
              </button>
            </div>
          </div>

          {/* Server Name */}
          <div className="form-section">
            <label htmlFor="server-name" className="form-label">
              Server Name
              {nameAvailable === true && <span className="name-available"> (available)</span>}
              {nameAvailable === false && <span className="name-unavailable"> (already exists)</span>}
            </label>
            <input
              id="server-name"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="e.g., my-ftp-server"
              className={`form-input ${nameAvailable === false ? 'form-input--error' : ''}`}
              disabled={isLoading}
              required
            />
            <span className="form-hint">
              Lowercase letters, numbers, and hyphens only. Must start with a letter.
            </span>
          </div>

          {/* NodePort Configuration */}
          <div className="form-section">
            <label className="form-label">NodePort</label>
            <div className="checkbox-row">
              <input
                type="checkbox"
                id="auto-nodeport"
                checked={useAutoNodePort}
                onChange={e => setUseAutoNodePort(e.target.checked)}
                disabled={isLoading}
              />
              <label htmlFor="auto-nodeport">Auto-assign NodePort</label>
            </div>
            {!useAutoNodePort && (
              <input
                type="number"
                value={nodePort || ''}
                onChange={e => setNodePort(e.target.value ? parseInt(e.target.value) : null)}
                placeholder="30000-32767"
                min={30000}
                max={32767}
                className="form-input"
                disabled={isLoading}
              />
            )}
          </div>

          {/* FTP-specific options */}
          {protocol === 'ftp' && (
            <>
              <div className="form-section">
                <label htmlFor="ftp-username" className="form-label">Username</label>
                <input
                  id="ftp-username"
                  type="text"
                  value={username}
                  onChange={e => setUsername(e.target.value)}
                  className="form-input"
                  disabled={isLoading}
                  required
                />
              </div>
              <div className="form-section">
                <label htmlFor="ftp-password" className="form-label">Password</label>
                <input
                  id="ftp-password"
                  type="text"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  className="form-input"
                  disabled={isLoading}
                  required
                />
              </div>
              <div className="form-section">
                <label htmlFor="ftp-directory" className="form-label">Directory (Optional)</label>
                <select
                  id="ftp-directory"
                  value={ftpSftpDirectory}
                  onChange={e => setFtpSftpDirectory(e.target.value)}
                  className="form-select"
                  disabled={isLoading}
                >
                  {DIRECTORY_PRESETS.map(preset => (
                    <option key={preset.value} value={preset.value}>
                      {preset.label}
                    </option>
                  ))}
                </select>
                {ftpSftpDirectory === 'custom' && (
                  <input
                    type="text"
                    value={ftpSftpCustomDir}
                    onChange={e => setFtpSftpCustomDir(e.target.value)}
                    placeholder="e.g., mydata"
                    className="form-input"
                    style={{ marginTop: '8px' }}
                    disabled={isLoading}
                    required
                  />
                )}
                <span className="form-hint">Subdirectory under C:\simulator-data to serve</span>
              </div>
              <div className="form-section">
                <label className="form-label">Passive Port Range (Optional)</label>
                <div className="port-range-inputs">
                  <input
                    type="number"
                    value={passivePortStart || ''}
                    onChange={e => setPassivePortStart(e.target.value ? parseInt(e.target.value) : null)}
                    placeholder="Start (e.g., 30100)"
                    min={30000}
                    max={32767}
                    className="form-input"
                    disabled={isLoading}
                  />
                  <span className="port-range-separator">to</span>
                  <input
                    type="number"
                    value={passivePortEnd || ''}
                    onChange={e => setPassivePortEnd(e.target.value ? parseInt(e.target.value) : null)}
                    placeholder="End (e.g., 30110)"
                    min={30000}
                    max={32767}
                    className="form-input"
                    disabled={isLoading}
                  />
                </div>
              </div>
            </>
          )}

          {/* SFTP-specific options */}
          {protocol === 'sftp' && (
            <>
              <div className="form-section">
                <label htmlFor="sftp-username" className="form-label">Username</label>
                <input
                  id="sftp-username"
                  type="text"
                  value={username}
                  onChange={e => setUsername(e.target.value)}
                  className="form-input"
                  disabled={isLoading}
                  required
                />
              </div>
              <div className="form-section">
                <label htmlFor="sftp-password" className="form-label">Password</label>
                <input
                  id="sftp-password"
                  type="text"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  className="form-input"
                  disabled={isLoading}
                  required
                />
              </div>
              <div className="form-section">
                <label htmlFor="sftp-directory" className="form-label">Directory (Optional)</label>
                <select
                  id="sftp-directory"
                  value={ftpSftpDirectory}
                  onChange={e => setFtpSftpDirectory(e.target.value)}
                  className="form-select"
                  disabled={isLoading}
                >
                  {DIRECTORY_PRESETS.map(preset => (
                    <option key={preset.value} value={preset.value}>
                      {preset.label}
                    </option>
                  ))}
                </select>
                {ftpSftpDirectory === 'custom' && (
                  <input
                    type="text"
                    value={ftpSftpCustomDir}
                    onChange={e => setFtpSftpCustomDir(e.target.value)}
                    placeholder="e.g., mydata"
                    className="form-input"
                    style={{ marginTop: '8px' }}
                    disabled={isLoading}
                    required
                  />
                )}
                <span className="form-hint">Subdirectory under C:\simulator-data to serve</span>
              </div>
              <div className="form-section">
                <label className="form-label">UID / GID</label>
                <div className="uid-gid-inputs">
                  <div>
                    <input
                      type="number"
                      value={uid}
                      onChange={e => setUid(parseInt(e.target.value) || 1000)}
                      min={1}
                      className="form-input"
                      disabled={isLoading}
                    />
                    <span className="form-hint">UID</span>
                  </div>
                  <div>
                    <input
                      type="number"
                      value={gid}
                      onChange={e => setGid(parseInt(e.target.value) || 1000)}
                      min={1}
                      className="form-input"
                      disabled={isLoading}
                    />
                    <span className="form-hint">GID</span>
                  </div>
                </div>
              </div>
            </>
          )}

          {/* NAS-specific options */}
          {protocol === 'nas' && (
            <>
              <div className="form-section">
                <label htmlFor="nas-directory" className="form-label">Directory</label>
                <select
                  id="nas-directory"
                  value={directoryPreset}
                  onChange={e => setDirectoryPreset(e.target.value)}
                  className="form-select"
                  disabled={isLoading}
                >
                  {NAS_PRESETS.map(preset => (
                    <option key={preset.value} value={preset.value}>
                      {preset.label}
                    </option>
                  ))}
                </select>
                {directoryPreset === 'custom' && (
                  <input
                    type="text"
                    value={customDirectory}
                    onChange={e => setCustomDirectory(e.target.value)}
                    placeholder="/custom/path"
                    className="form-input"
                    style={{ marginTop: '8px' }}
                    disabled={isLoading}
                    required
                  />
                )}
              </div>
              <div className="form-section">
                <label htmlFor="nas-export-options" className="form-label">Export Options</label>
                <input
                  id="nas-export-options"
                  type="text"
                  value={exportOptions}
                  onChange={e => setExportOptions(e.target.value)}
                  className="form-input"
                  disabled={isLoading}
                />
                <span className="form-hint">NFS export options (advanced)</span>
              </div>
            </>
          )}

          {/* Actions */}
          <div className="modal-actions">
            <button
              type="button"
              className="btn btn--secondary"
              onClick={onClose}
              disabled={isLoading}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="btn btn--primary"
              disabled={isLoading || !name.trim() || nameAvailable === false}
            >
              {isLoading ? 'Creating...' : `Create ${protocol.toUpperCase()} Server`}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default CreateServerModal;
