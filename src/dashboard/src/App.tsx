import { useState } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { useFileEvents } from './hooks/useFileEvents';
import { useMetricsStream } from './hooks/useMetricsStream';
import { ServerStatus, ServerStatusUpdate } from './types/server';
import ConnectionStatus from './components/ConnectionStatus';
import SummaryHeader from './components/SummaryHeader';
import ServerGrid from './components/ServerGrid';
import ServerDetailsPanel from './components/ServerDetailsPanel';
import FileBrowser from './components/FileBrowser';
import FileEventFeed from './components/FileEventFeed';
import HistoryTab from './components/HistoryTab';
import KafkaTab from './components/KafkaTab';
import './App.css';

function App() {
  // Get base URL from environment
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://172.25.170.231:30500';
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

  // Track selected server for details panel
  const [selectedServer, setSelectedServer] = useState<ServerStatus | null>(null);

  // Track active tab
  const [activeTab, setActiveTab] = useState<'servers' | 'files' | 'history' | 'kafka'>('servers');

  // Track selected server for History tab filter
  const [historyServerId, setHistoryServerId] = useState<string | undefined>();

  // Handle sparkline click - navigate to History tab with server filter
  const handleSparklineClick = (serverId: string) => {
    setHistoryServerId(serverId);
    setActiveTab('history');
  };

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
          </nav>
        </div>
        <ConnectionStatus
          isConnected={isConnected}
          isReconnecting={isReconnecting}
          reconnectAttempt={reconnectAttempt}
          lastUpdate={lastUpdate}
        />
      </header>

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
                <SummaryHeader servers={data.servers} />
                <ServerGrid
                  servers={data.servers}
                  onCardClick={setSelectedServer}
                  sparklineData={latestSamples}
                  onSparklineClick={handleSparklineClick}
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
              <FileBrowser apiBaseUrl={apiBaseUrl} />
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
          <HistoryTab
            apiBaseUrl={apiBaseUrl}
            initialServerId={historyServerId}
          />
        )}

        {activeTab === 'kafka' && (
          <KafkaTab apiBaseUrl={apiBaseUrl} />
        )}
      </main>

      <ServerDetailsPanel
        server={selectedServer}
        onClose={() => setSelectedServer(null)}
      />
    </div>
  );
}

export default App;
