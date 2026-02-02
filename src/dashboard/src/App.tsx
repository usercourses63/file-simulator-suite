import { useState } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { useFileEvents } from './hooks/useFileEvents';
import { ServerStatus, ServerStatusUpdate } from './types/server';
import ConnectionStatus from './components/ConnectionStatus';
import SummaryHeader from './components/SummaryHeader';
import ServerGrid from './components/ServerGrid';
import ServerDetailsPanel from './components/ServerDetailsPanel';
import FileBrowser from './components/FileBrowser';
import FileEventFeed from './components/FileEventFeed';
import './App.css';

function App() {
  // Get base URL from environment
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://192.168.49.2:30500';
  const statusHubUrl = import.meta.env.VITE_SIGNALR_HUB_URL || `${apiBaseUrl}/hubs/status`;
  const fileEventsHubUrl = `${apiBaseUrl}/hubs/fileevents`;

  // Connect to SignalR hub and receive status updates
  const { data, isConnected, isReconnecting, reconnectAttempt, error, lastUpdate } =
    useSignalR<ServerStatusUpdate>(statusHubUrl, 'ServerStatusUpdate');

  // Connect to file events hub
  const { events: fileEvents, isConnected: fileEventsConnected, clearEvents } = useFileEvents(fileEventsHubUrl);

  // Track selected server for details panel
  const [selectedServer, setSelectedServer] = useState<ServerStatus | null>(null);

  // Track active tab
  const [activeTab, setActiveTab] = useState<'servers' | 'files'>('servers');

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
      </main>

      <ServerDetailsPanel
        server={selectedServer}
        onClose={() => setSelectedServer(null)}
      />
    </div>
  );
}

export default App;
