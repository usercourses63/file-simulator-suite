import { useState, useMemo, useEffect } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { useFileEvents } from './hooks/useFileEvents';
import { useMetricsStream } from './hooks/useMetricsStream';
import { useMultiSelect } from './hooks/useMultiSelect';
import { useServerManagement } from './hooks/useServerManagement';
import { useAlerts } from './hooks/useAlerts';
import { ServerStatus, ServerStatusUpdate } from './types/server';
import ConnectionStatus from './components/ConnectionStatus';
import SummaryHeader from './components/SummaryHeader';
import ServerGrid from './components/ServerGrid';
import ServerDetailsPanel from './components/ServerDetailsPanel';
import FileBrowser from './components/FileBrowser';
import FileEventFeed from './components/FileEventFeed';
import HistoryTab from './components/HistoryTab';
import KafkaTab from './components/KafkaTab';
import AlertsTab from './components/AlertsTab';
import SettingsPanel from './components/SettingsPanel';
import ImportConfigDialog from './components/ImportConfigDialog';
import DeleteConfirmDialog from './components/DeleteConfirmDialog';
import BatchOperationsBar from './components/BatchOperationsBar';
import CreateServerModal from './components/CreateServerModal';
import AlertToaster from './components/AlertToaster';
import AlertBanner from './components/AlertBanner';
import { withErrorBoundary } from './components/ErrorBoundary';
import './App.css';

// Wrap tab components with error boundaries
const SafeFileBrowser = withErrorBoundary(FileBrowser, 'SafeFileBrowser');
const SafeHistoryTab = withErrorBoundary(HistoryTab, 'SafeHistoryTab');
const SafeKafkaTab = withErrorBoundary(KafkaTab, 'SafeKafkaTab');
const SafeAlertsTab = withErrorBoundary(AlertsTab, 'SafeAlertsTab');

// Extended server info for multi-select filtering
interface ServerWithDynamic extends ServerStatus {
  name: string;
  isDynamic: boolean;
}

function App() {
  // Get base URL from environment
  // Use environment variable, or file-simulator.local for production, or localhost for development
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL
    || (import.meta.env.DEV ? 'http://localhost:5000' : 'http://file-simulator.local:30500');
  const statusHubUrl = import.meta.env.VITE_SIGNALR_HUB_URL || `${apiBaseUrl}/hubs/status`;
  const fileEventsHubUrl = `${apiBaseUrl}/hubs/fileevents`;
  const metricsHubUrl = `${apiBaseUrl}/hubs/metrics`;

  // Connect to SignalR hub and receive status updates
  const { data, isConnected, isReconnecting, reconnectAttempt, error, lastUpdate } =
    useSignalR<ServerStatusUpdate>(statusHubUrl, 'ServerStatusUpdate');

  // Connect to file events hub
  const { events: fileEvents, isConnected: fileEventsConnected, clearEvents } = useFileEvents(fileEventsHubUrl);

  // Connect to metrics hub for real-time sparkline data
  const { latestSamples } = useMetricsStream(metricsHubUrl);

  // Connect to alerts for toast notifications and banner
  const { activeAlerts, alertHistory, stats, isLoading: alertsLoading, fetchAlertHistory } = useAlerts(apiBaseUrl);

  // Track selected server for details panel
  const [selectedServer, setSelectedServer] = useState<ServerStatus | null>(null);

  // Track active tab
  const [activeTab, setActiveTab] = useState<'servers' | 'files' | 'history' | 'kafka' | 'alerts'>('servers');

  // Track selected server for History tab filter
  const [historyServerId, setHistoryServerId] = useState<string | undefined>();

  // Settings panel and import dialog state
  const [showSettings, setShowSettings] = useState(false);
  const [showImportDialog, setShowImportDialog] = useState(false);
  const [showCreateServer, setShowCreateServer] = useState(false);

  // Server management hook
  const { deleteServer, isLoading: isDeleting } = useServerManagement({ apiBaseUrl });

  // Delete dialog state
  const [deleteTarget, setDeleteTarget] = useState<{ name: string; protocol: string } | null>(null);
  const [batchDeleteTargets, setBatchDeleteTargets] = useState<string[]>([]);

  // Build dynamic info map for servers from SignalR data
  const dynamicInfo = useMemo(() => {
    if (!data?.servers) return {};
    const info: Record<string, { isDynamic: boolean; managedBy: string }> = {};
    for (const server of data.servers) {
      info[server.name] = {
        isDynamic: server.isDynamic,
        managedBy: server.managedBy
      };
    }
    return info;
  }, [data?.servers]);

  // Convert servers to include isDynamic for multi-select filtering
  const serversWithDynamic = useMemo((): ServerWithDynamic[] => {
    if (!data?.servers) return [];
    return data.servers.map(s => ({
      ...s,
      isDynamic: dynamicInfo[s.name]?.isDynamic ?? false
    }));
  }, [data?.servers, dynamicInfo]);

  // Multi-select for servers tab (only dynamic servers can be selected)
  const {
    selectedIds,
    selectedCount,
    toggleSelect,
    selectAll,
    clearSelection
  } = useMultiSelect(serversWithDynamic);

  // Handle sparkline click - navigate to History tab with server filter
  const handleSparklineClick = (serverId: string) => {
    setHistoryServerId(serverId);
    setActiveTab('history');
  };

  // Fetch alert history when alerts tab is activated
  useEffect(() => {
    if (activeTab === 'alerts' && alertHistory.length === 0) {
      fetchAlertHistory();
    }
  }, [activeTab, alertHistory.length, fetchAlertHistory]);

  // Handle single delete
  const handleDelete = (server: ServerStatus) => {
    setDeleteTarget({ name: server.name, protocol: server.protocol });
  };

  // Handle batch delete
  const handleBatchDelete = () => {
    setBatchDeleteTargets(Array.from(selectedIds));
  };

  // Confirm delete
  const handleConfirmDelete = async (deleteData: boolean) => {
    try {
      if (batchDeleteTargets.length > 0) {
        // Batch delete
        await Promise.all(batchDeleteTargets.map(name => deleteServer(name, deleteData)));
        clearSelection();
        setBatchDeleteTargets([]);
      } else if (deleteTarget) {
        // Single delete
        await deleteServer(deleteTarget.name, deleteData);
        setDeleteTarget(null);
      }
    } catch (err) {
      console.error('Delete failed:', err);
    }
  };

  // Cancel delete
  const handleCancelDelete = () => {
    setDeleteTarget(null);
    setBatchDeleteTargets([]);
  };

  // Determine if any delete targets are NAS servers
  const hasNasInDeleteTargets = useMemo(() => {
    if (batchDeleteTargets.length > 0) {
      return batchDeleteTargets.some(name => name.toLowerCase().includes('nas'));
    }
    return deleteTarget?.protocol === 'NFS';
  }, [batchDeleteTargets, deleteTarget]);

  return (
    <div className={`app ${selectedServer && activeTab === 'servers' ? 'app--panel-open' : ''}`}>
      <header className="app-header">
        <div className="header-title">
          <h1>File Simulator Suite</h1>
          <nav className="header-tabs">
            <button
              className={`header-tab ${activeTab === 'servers' ? 'header-tab--active' : ''}`}
              onClick={() => setActiveTab('servers')}
              type="button"
            >
              Servers
            </button>
            <button
              className={`header-tab ${activeTab === 'files' ? 'header-tab--active' : ''}`}
              onClick={() => setActiveTab('files')}
              type="button"
            >
              Files
            </button>
            <button
              className={`header-tab ${activeTab === 'history' ? 'header-tab--active' : ''}`}
              onClick={() => setActiveTab('history')}
              type="button"
            >
              History
            </button>
            <button
              className={`header-tab ${activeTab === 'kafka' ? 'header-tab--active' : ''}`}
              onClick={() => setActiveTab('kafka')}
              type="button"
            >
              Kafka
            </button>
            <button
              className={`header-tab ${activeTab === 'alerts' ? 'header-tab--active' : ''}`}
              onClick={() => setActiveTab('alerts')}
              type="button"
            >
              Alerts
            </button>
          </nav>
        </div>
        <div className="header-actions">
          <button
            className="header-add-server-btn"
            onClick={() => setShowCreateServer(true)}
            title="Add Server"
            type="button"
          >
            + Add Server
          </button>
          <ConnectionStatus
            isConnected={isConnected}
            isReconnecting={isReconnecting}
            reconnectAttempt={reconnectAttempt}
            lastUpdate={lastUpdate}
          />
          <button
            className="header-settings-btn"
            onClick={() => setShowSettings(true)}
            title="Settings"
            type="button"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="3" />
              <path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-2 2 2 2 0 01-2-2v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83 0 2 2 0 010-2.83l.06-.06a1.65 1.65 0 00.33-1.82 1.65 1.65 0 00-1.51-1H3a2 2 0 01-2-2 2 2 0 012-2h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 010-2.83 2 2 0 012.83 0l.06.06a1.65 1.65 0 001.82.33H9a1.65 1.65 0 001-1.51V3a2 2 0 012-2 2 2 0 012 2v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 0 2 2 0 010 2.83l-.06.06a1.65 1.65 0 00-.33 1.82V9a1.65 1.65 0 001.51 1H21a2 2 0 012 2 2 2 0 01-2 2h-.09a1.65 1.65 0 00-1.51 1z" />
            </svg>
          </button>
        </div>
      </header>

      <AlertBanner alerts={activeAlerts} />

      <main className="app-main">
        {error && !isReconnecting && (
          <div className="error-banner">
            <span className="error-icon">!</span>
            <span className="error-message">Connection error: {error}</span>
          </div>
        )}

        {activeTab === 'servers' && (
          <>
            {data ? (
              <>
                <BatchOperationsBar
                  selectedCount={selectedCount}
                  onDelete={handleBatchDelete}
                  onSelectAll={selectAll}
                  onClearSelection={clearSelection}
                  isDeleting={isDeleting}
                />
                <SummaryHeader servers={data.servers} />
                <ServerGrid
                  servers={data.servers}
                  onCardClick={setSelectedServer}
                  sparklineData={latestSamples}
                  onSparklineClick={handleSparklineClick}
                  showMultiSelect={true}
                  selectedIds={selectedIds}
                  onToggleSelect={toggleSelect}
                  onDelete={handleDelete}
                  dynamicInfo={dynamicInfo}
                />
              </>
            ) : (
              <div className="loading-state">
                <div className="loading-spinner"></div>
                <p>Connecting to server...</p>
                {error && <p className="loading-error">{error}</p>}
              </div>
            )}
          </>
        )}

        {activeTab === 'files' && (
          <div className="files-container">
            <div className="files-main">
              <SafeFileBrowser apiBaseUrl={apiBaseUrl} />
            </div>
            <aside className="files-sidebar">
              <FileEventFeed
                events={fileEvents}
                isConnected={fileEventsConnected}
                onClear={clearEvents}
              />
            </aside>
          </div>
        )}

        {activeTab === 'history' && (
          <SafeHistoryTab
            apiBaseUrl={apiBaseUrl}
            initialServerId={historyServerId}
          />
        )}

        {activeTab === 'kafka' && (
          <SafeKafkaTab apiBaseUrl={apiBaseUrl} />
        )}

        {activeTab === 'alerts' && (
          <SafeAlertsTab
            alerts={alertHistory}
            stats={stats}
            loading={alertsLoading}
          />
        )}
      </main>

      <ServerDetailsPanel
        server={selectedServer}
        onClose={() => setSelectedServer(null)}
        apiBaseUrl={apiBaseUrl}
      />

      <SettingsPanel
        isOpen={showSettings}
        onClose={() => setShowSettings(false)}
        onImport={() => {
          setShowSettings(false);
          setShowImportDialog(true);
        }}
        apiBaseUrl={apiBaseUrl}
      />

      <ImportConfigDialog
        isOpen={showImportDialog}
        onClose={() => setShowImportDialog(false)}
        onImported={() => {
          // SignalR will push updates
        }}
        apiBaseUrl={apiBaseUrl}
      />

      <DeleteConfirmDialog
        isOpen={deleteTarget !== null || batchDeleteTargets.length > 0}
        serverName={deleteTarget?.name ?? ''}
        serverNames={batchDeleteTargets.length > 0 ? batchDeleteTargets : undefined}
        isNasServer={hasNasInDeleteTargets}
        isDeleting={isDeleting}
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
      />

      <CreateServerModal
        isOpen={showCreateServer}
        onClose={() => setShowCreateServer(false)}
        onCreated={() => {
          // SignalR will push updates
        }}
        apiBaseUrl={apiBaseUrl}
      />

      <AlertToaster />
    </div>
  );
}

export default App;
