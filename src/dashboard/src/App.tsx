import { useState, useMemo, useEffect, useRef } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { useFileEvents } from './hooks/useFileEvents';
import { useMetricsStream } from './hooks/useMetricsStream';
import { useMultiSelect } from './hooks/useMultiSelect';
import { useServerManagement } from './hooks/useServerManagement';
import { useAlerts } from './hooks/useAlerts';
import { ServerStatus, ServerStatusUpdate } from './types/server';
import { MountHealthStatus } from './types/mountHealth';
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
import MountHealthBanner from './components/MountHealthBanner';
import ActivityLog from './components/ActivityLog';
import { useActivityLog } from './hooks/useActivityLog';
import type { ActivityEvent, ActivitySeverity } from './types/activity';
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

  // Subscribe to mount health updates from same hub (broadcasts only on state change)
  const { data: mountHealth } = useSignalR<MountHealthStatus>(statusHubUrl, 'MountHealthUpdate');

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

  // Activity log for servers tab sidebar
  const { events: activityEvents, addEvent, addEvents, clearEvents: clearActivityEvents } = useActivityLog();

  // Refs for diffing SignalR data to produce activity events
  const prevServersRef = useRef<Map<string, { isHealthy: boolean; protocol: string }> | null>(null);
  const lastFileEventCountRef = useRef(0);
  const prevAlertIdsRef = useRef<Set<string> | null>(null);
  const prevMountStateRef = useRef<string | null>(null);

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

  // Diff servers to emit activity events
  useEffect(() => {
    if (!data?.servers) return;
    const current = new Map(
      data.servers.map(s => [s.name, { isHealthy: s.isHealthy, protocol: s.protocol }])
    );

    if (prevServersRef.current === null) {
      // First load — populate silently
      prevServersRef.current = current;
      return;
    }

    const prev = prevServersRef.current;
    const now = new Date().toISOString();
    const batch: ActivityEvent[] = [];

    for (const [name, info] of current) {
      const old = prev.get(name);
      if (!old) {
        batch.push({ id: `sc-${now}-${name}`, type: 'server-created', timestamp: now, title: `${info.protocol} server created`, detail: name, severity: 'info' });
      } else {
        if (!old.isHealthy && info.isHealthy) {
          batch.push({ id: `sh-${now}-${name}`, type: 'server-healthy', timestamp: now, title: `${name} is healthy`, severity: 'success' });
        } else if (old.isHealthy && !info.isHealthy) {
          batch.push({ id: `sd-${now}-${name}`, type: 'server-down', timestamp: now, title: `${name} is down`, severity: 'critical' });
        }
      }
    }

    for (const [name, info] of prev) {
      if (!current.has(name)) {
        batch.push({ id: `sx-${now}-${name}`, type: 'server-deleted', timestamp: now, title: `${info.protocol} server deleted`, detail: name, severity: 'warning' });
      }
    }

    if (batch.length > 0) addEvents(batch);
    prevServersRef.current = current;
  }, [data?.servers, addEvents]);

  // Diff file events to emit activity events (skip Modified to reduce noise)
  useEffect(() => {
    if (fileEvents.length <= lastFileEventCountRef.current) {
      lastFileEventCountRef.current = fileEvents.length;
      return;
    }

    const newCount = fileEvents.length - lastFileEventCountRef.current;
    const newEntries = fileEvents.slice(0, newCount);
    const now = new Date().toISOString();
    const batch: ActivityEvent[] = [];

    for (const fe of newEntries) {
      if (fe.eventType === 'Modified') continue;
      const type = fe.eventType === 'Created' ? 'file-created' : fe.eventType === 'Deleted' ? 'file-deleted' : null;
      if (!type) continue;
      batch.push({
        id: `fe-${now}-${fe.fileName}`,
        type,
        timestamp: fe.timestamp || now,
        title: `File ${fe.eventType.toLowerCase()}: ${fe.fileName}`,
        detail: fe.relativePath,
        severity: type === 'file-deleted' ? 'warning' : 'info',
      });
    }

    if (batch.length > 0) addEvents(batch);
    lastFileEventCountRef.current = fileEvents.length;
  }, [fileEvents, addEvents]);

  // Diff active alerts to emit activity events
  useEffect(() => {
    const currentIds = new Set(activeAlerts.map(a => a.id));

    if (prevAlertIdsRef.current === null) {
      prevAlertIdsRef.current = currentIds;
      return;
    }

    const prev = prevAlertIdsRef.current;
    const now = new Date().toISOString();
    const batch: ActivityEvent[] = [];
    const alertMap = new Map(activeAlerts.map(a => [a.id, a]));

    for (const id of currentIds) {
      if (!prev.has(id)) {
        const alert = alertMap.get(id);
        const severity: ActivitySeverity = alert?.severity === 'Critical' ? 'critical' : alert?.severity === 'Warning' ? 'warning' : 'info';
        batch.push({
          id: `at-${now}-${id}`,
          type: 'alert-triggered',
          timestamp: now,
          title: `Alert: ${alert?.title ?? id}`,
          detail: alert?.message,
          severity,
        });
      }
    }

    for (const id of prev) {
      if (!currentIds.has(id)) {
        batch.push({
          id: `ar-${now}-${id}`,
          type: 'alert-resolved',
          timestamp: now,
          title: `Alert resolved`,
          detail: id,
          severity: 'success',
        });
      }
    }

    if (batch.length > 0) addEvents(batch);
    prevAlertIdsRef.current = currentIds;
  }, [activeAlerts, addEvents]);

  // Emit mount health state changes
  useEffect(() => {
    if (!mountHealth?.state) return;
    if (prevMountStateRef.current === null) {
      prevMountStateRef.current = mountHealth.state;
      return;
    }
    if (mountHealth.state !== prevMountStateRef.current) {
      const now = new Date().toISOString();
      const severity: ActivitySeverity = mountHealth.state === 'Healthy' ? 'success' : mountHealth.state === 'Stale' ? 'critical' : 'warning';
      addEvent({
        id: `mh-${now}`,
        type: 'mount-state-change',
        timestamp: now,
        title: `Mount: ${mountHealth.state}`,
        detail: mountHealth.message ?? undefined,
        severity,
      });
      prevMountStateRef.current = mountHealth.state;
    }
  }, [mountHealth, addEvent]);

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
          <h1>File Simulator Suite <span className="header-version">v{__APP_VERSION__}</span></h1>
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

      <MountHealthBanner status={mountHealth} />
      <AlertBanner alerts={activeAlerts} />

      <main className="app-main">
        {error && !isReconnecting && (
          <div className="error-banner">
            <span className="error-icon">!</span>
            <span className="error-message">Connection error: {error}</span>
          </div>
        )}

        {activeTab === 'servers' && (
          <div className="servers-container">
            <aside className="servers-sidebar">
              <ActivityLog events={activityEvents} onClear={clearActivityEvents} />
            </aside>
            <div className="servers-main">
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
            </div>
          </div>
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
