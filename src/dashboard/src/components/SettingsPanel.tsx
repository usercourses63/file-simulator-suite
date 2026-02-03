import { useConfigExport } from '../hooks/useConfigExport';
import './SettingsPanel.css';

interface SettingsPanelProps {
  isOpen: boolean;
  onClose: () => void;
  onImport: () => void;
  apiBaseUrl: string;
}

export function SettingsPanel({
  isOpen,
  onClose,
  onImport,
  apiBaseUrl
}: SettingsPanelProps) {
  const { exportConfig, isExporting, error } = useConfigExport({ apiBaseUrl });

  if (!isOpen) return null;

  const handleExport = async () => {
    try {
      await exportConfig();
    } catch {
      // Error already handled by hook
    }
  };

  return (
    <div className="settings-panel-overlay" onClick={onClose}>
      <div className="settings-panel" onClick={e => e.stopPropagation()}>
        <div className="settings-header">
          <h2>Settings</h2>
          <button type="button" className="settings-close" onClick={onClose}>&times;</button>
        </div>

        <div className="settings-content">
          <section className="settings-section">
            <h3>Configuration</h3>
            <p className="settings-description">
              Export your current simulator configuration or import a saved configuration file.
            </p>

            <div className="settings-actions">
              <button
                type="button"
                className="settings-btn settings-btn--secondary"
                onClick={handleExport}
                disabled={isExporting}
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5 5 5-5M12 15V3" />
                </svg>
                {isExporting ? 'Exporting...' : 'Export Configuration'}
              </button>

              <button
                type="button"
                className="settings-btn settings-btn--secondary"
                onClick={onImport}
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M17 8l-5-5-5 5M12 3v12" />
                </svg>
                Import Configuration
              </button>
            </div>

            {error && <div className="settings-error">{error}</div>}

            <p className="settings-note">
              <strong>Note:</strong> Exported files contain server credentials.
              Keep them secure.
            </p>
          </section>

          <section className="settings-section">
            <h3>About</h3>
            <div className="about-info">
              <p><strong>File Simulator Suite</strong></p>
              <p>Version 2.0</p>
              <p className="about-description">
                Multi-protocol file server simulator for Kubernetes development environments.
              </p>
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}

export default SettingsPanel;
