import { useState } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { ServerStatus, ServerStatusUpdate } from './types/server';
import ConnectionStatus from './components/ConnectionStatus';
import SummaryHeader from './components/SummaryHeader';
import ServerGrid from './components/ServerGrid';
import './App.css';

function App() {
  // Get SignalR hub URL from environment
  const hubUrl = import.meta.env.VITE_SIGNALR_HUB_URL || 'http://192.168.49.2:30500/hubs/status';

  // Connect to SignalR hub and receive status updates
  const { data, isConnected, isReconnecting, reconnectAttempt, error, lastUpdate } =
    useSignalR<ServerStatusUpdate>(hubUrl, 'ServerStatusUpdate');

  // Track selected server for details panel
  const [selectedServer, setSelectedServer] = useState<ServerStatus | null>(null);

  return (
    <div className="app">
      <header className="app-header">
        <div className="header-title">
          <h1>File Simulator Suite</h1>
          <span className="header-subtitle">Monitoring Dashboard</span>
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

        {data ? (
          <>
            <SummaryHeader servers={data.Servers} />
            <ServerGrid
              servers={data.Servers}
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
      </main>

      {/* Details panel placeholder - implemented in Plan 03 */}
      {selectedServer && (
        <div className="details-panel-placeholder">
          Selected: {selectedServer.Name}
          <button onClick={() => setSelectedServer(null)}>Close</button>
        </div>
      )}
    </div>
  );
}

export default App;
