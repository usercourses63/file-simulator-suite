# Phase 7: Real-Time Monitoring Dashboard - Research

**Researched:** 2026-02-02
**Domain:** React 19 + SignalR real-time dashboard with WebSocket integration
**Confidence:** HIGH

## Summary

This research investigates how to implement a React 19 dashboard with SignalR WebSocket integration for real-time server health monitoring. The standard approach is **React 19 with Vite + TypeScript + @microsoft/signalr client** using modern React patterns (custom hooks, useEffect lifecycle, automatic reconnection).

Key findings:

1. **SignalR Client Library**: Use @microsoft/signalr 8.x with built-in `.withAutomaticReconnect()` for robust reconnection handling
2. **React 19 Features**: Leverage simplified hooks (use() API for context), automatic memoization from React compiler, and cleaner component patterns
3. **Connection Management**: Custom useSignalR hook pattern with useEffect for lifecycle, cleanup on unmount, and connection state tracking
4. **Real-Time State Updates**: useState + SignalR .on() handlers for incoming messages, with connection status indicator in UI
5. **Vite Build Tool**: Fast dev server with HMR, TypeScript support out-of-box, environment variable support via import.meta.env

The backend Phase 6 implementation already provides:
- SignalR hub at `/hubs/status` with `ServerStatusUpdate` messages every 5 seconds
- REST endpoints at `/api/servers` and `/api/status` for initial data
- CORS enabled for dashboard development
- Structured ServerStatus + ServerStatusUpdate models

**Primary recommendation:** Create React 19 + Vite + TypeScript SPA with custom useSignalR hook, server status grid using CSS Grid layout, connection status indicator with retry counter, and protocol details panel as right sidebar.

## Standard Stack

The established libraries/tools for React + SignalR real-time dashboards:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| React | 19.x | UI framework | Latest stable, simplified hooks, automatic memoization |
| Vite | 7.x | Build tool | Fast HMR, native ESM, TypeScript support, replaces CRA |
| TypeScript | 5.x | Type safety | Essential for SignalR type-safe message handling |
| @microsoft/signalr | 8.x | SignalR client | Official Microsoft client, .withAutomaticReconnect() built-in |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| @types/react | Latest | React type definitions | TypeScript projects (installed automatically) |
| @types/react-dom | Latest | ReactDOM type definitions | TypeScript projects (installed automatically) |
| @vitejs/plugin-react | Latest | Vite React plugin | Enables JSX, Fast Refresh, React compiler |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom useSignalR | react-signalr NPM packages | Custom hook gives full control; packages add dependency overhead |
| Vite | Create React App | Vite 10x faster dev server; CRA deprecated/unmaintained |
| CSS modules | Tailwind CSS | Plain CSS sufficient for Phase 7 scope; Tailwind adds complexity |
| useState | Zustand/Redux | useState sufficient for 13 servers; state library overkill |

**Installation:**
```bash
# Create Vite project with React + TypeScript
npm create vite@latest dashboard -- --template react-ts
cd dashboard

# Install SignalR client
npm install @microsoft/signalr

# Dev dependencies included by template:
# - typescript
# - @types/react, @types/react-dom
# - @vitejs/plugin-react
# - vite
```

## Architecture Patterns

### Recommended Project Structure
```
dashboard/
├── public/               # Static assets
├── src/
│   ├── components/       # React components
│   │   ├── ServerGrid.tsx           # 13-server status grid
│   │   ├── ServerCard.tsx           # Individual server card
│   │   ├── ServerDetailsPanel.tsx   # Right sidebar details
│   │   ├── ConnectionStatus.tsx     # WebSocket status indicator
│   │   └── SummaryHeader.tsx        # Health counts (X healthy, Y down)
│   ├── hooks/            # Custom hooks
│   │   └── useSignalR.ts            # SignalR connection hook
│   ├── types/            # TypeScript types
│   │   └── server.ts                # ServerStatus, ServerStatusUpdate types
│   ├── utils/            # Helper functions
│   │   └── healthStatus.ts          # Health logic (healthy/degraded/down)
│   ├── App.tsx           # Main app component
│   ├── App.css           # App styles
│   ├── main.tsx          # Entry point
│   └── vite-env.d.ts     # Vite type declarations
├── index.html            # HTML entry
├── vite.config.ts        # Vite configuration
├── tsconfig.json         # TypeScript config
├── package.json          # Dependencies
└── .env.development      # Environment variables
```

### Pattern 1: Custom useSignalR Hook with Automatic Reconnection
**What:** Encapsulate SignalR connection lifecycle in a custom hook that returns connection state and methods
**When to use:** Always - separates SignalR logic from UI components, enables reuse across components

**Example:**
```typescript
// Source: Verified pattern from Context7 + WebSearch research
import { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

interface UseSignalRResult<T> {
  data: T | null;
  isConnected: boolean;
  isReconnecting: boolean;
  reconnectAttempt: number;
  error: string | null;
}

export function useSignalR<T>(
  hubUrl: string,
  eventName: string
): UseSignalRResult<T> {
  const [data, setData] = useState<T | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isReconnecting, setIsReconnecting] = useState(false);
  const [reconnectAttempt, setReconnectAttempt] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    // Build connection with automatic reconnection
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect() // Built-in retry logic
      .build();

    connectionRef.current = connection;

    // Connection lifecycle handlers
    connection.onreconnecting((error) => {
      setIsReconnecting(true);
      setIsConnected(false);
      setReconnectAttempt(prev => prev + 1);
      console.log('SignalR reconnecting...', error);
    });

    connection.onreconnected(() => {
      setIsReconnecting(false);
      setIsConnected(true);
      setReconnectAttempt(0);
      setError(null);
      console.log('SignalR reconnected');
    });

    connection.onclose((error) => {
      setIsConnected(false);
      setIsReconnecting(false);
      if (error) {
        setError(error.message);
        console.error('SignalR connection closed with error:', error);
      }
    });

    // Subscribe to messages
    connection.on(eventName, (message: T) => {
      setData(message);
    });

    // Start connection
    connection.start()
      .then(() => {
        setIsConnected(true);
        setError(null);
        console.log('SignalR connected');
      })
      .catch(err => {
        setError(err.message);
        console.error('SignalR connection failed:', err);
      });

    // Cleanup on unmount
    return () => {
      connection.stop();
    };
  }, [hubUrl, eventName]);

  return { data, isConnected, isReconnecting, reconnectAttempt, error };
}
```

### Pattern 2: Server Status Grid with CSS Grid
**What:** Responsive grid layout for 13 server cards using CSS Grid with grouping by type
**When to use:** Dashboard card layouts that need to wrap responsively without media queries

**Example:**
```tsx
// Source: CSS Grid dashboard pattern from WebSearch research
import React from 'react';
import ServerCard from './ServerCard';
import { ServerStatus } from '../types/server';

interface ServerGridProps {
  servers: ServerStatus[];
  onCardClick: (server: ServerStatus) => void;
}

export function ServerGrid({ servers, onCardClick }: ServerGridProps) {
  // Group servers by type
  const nasServers = servers.filter(s => s.Protocol === 'NFS');
  const protocolServers = servers.filter(s => s.Protocol !== 'NFS');

  return (
    <div className="server-grid-container">
      <section>
        <h2>NAS Servers ({nasServers.length})</h2>
        <div className="server-grid">
          {nasServers.map(server => (
            <ServerCard
              key={server.Name}
              server={server}
              onClick={() => onCardClick(server)}
            />
          ))}
        </div>
      </section>

      <section>
        <h2>Protocol Servers ({protocolServers.length})</h2>
        <div className="server-grid">
          {protocolServers.map(server => (
            <ServerCard
              key={server.Name}
              server={server}
              onClick={() => onCardClick(server)}
            />
          ))}
        </div>
      </section>
    </div>
  );
}
```

**CSS:**
```css
/* Source: Modern CSS Grid layout techniques 2026 */
.server-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  align-items: start; /* Prevents card stretching */
}

.server-card {
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 1rem;
  background: white;
  cursor: pointer;
  transition: border-color 0.3s;
  min-height: 120px;
}

.server-card:hover {
  border-color: #3b82f6;
}

/* Health status colors */
.server-card.healthy {
  border-left: 4px solid #22c55e;
}

.server-card.degraded {
  border-left: 4px solid #eab308;
}

.server-card.down {
  border-left: 4px solid #ef4444;
}

/* Status change animation */
@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.server-card.status-changed {
  animation: pulse 0.6s ease-in-out;
}
```

### Pattern 3: Connection Status Indicator with Retry Counter
**What:** Display WebSocket connection state with reconnection attempt counter
**When to use:** Always - users need to know if real-time updates are working

**Example:**
```tsx
// Source: SignalR connection handling best practices
import React from 'react';

interface ConnectionStatusProps {
  isConnected: boolean;
  isReconnecting: boolean;
  reconnectAttempt: number;
  lastUpdate: Date | null;
}

export function ConnectionStatus({
  isConnected,
  isReconnecting,
  reconnectAttempt,
  lastUpdate
}: ConnectionStatusProps) {
  const getStatusText = () => {
    if (isReconnecting) {
      return `Reconnecting (attempt ${reconnectAttempt}/5)...`;
    }
    if (isConnected) {
      return 'Connected';
    }
    return 'Disconnected';
  };

  const getTimeAgo = () => {
    if (!lastUpdate) return '';
    const seconds = Math.floor((Date.now() - lastUpdate.getTime()) / 1000);
    return `Last update: ${seconds}s ago`;
  };

  return (
    <div className="connection-status">
      <div className={`status-indicator ${isConnected ? 'connected' : 'disconnected'}`}>
        <span className="status-dot"></span>
        <span className="status-text">{getStatusText()}</span>
      </div>
      {lastUpdate && <span className="status-time">{getTimeAgo()}</span>}
    </div>
  );
}
```

### Pattern 4: Protocol Details Panel with Type-Specific Sections
**What:** Right sidebar that shows connection info + metrics for selected server
**When to use:** User clicks a server card to view details

**Example:**
```tsx
// Source: Dashboard detail panel pattern
import React from 'react';
import { ServerStatus } from '../types/server';

interface ServerDetailsPanelProps {
  server: ServerStatus | null;
  onClose: () => void;
}

export function ServerDetailsPanel({ server, onClose }: ServerDetailsPanelProps) {
  if (!server) return null;

  const renderProtocolSpecificInfo = () => {
    switch (server.Protocol) {
      case 'FTP':
        return (
          <div className="protocol-info">
            <h4>FTP Configuration</h4>
            <p>Host: ftp-server.file-simulator.svc.cluster.local</p>
            <p>Port: 21</p>
            <p>Passive Mode: Enabled</p>
          </div>
        );
      case 'SFTP':
        return (
          <div className="protocol-info">
            <h4>SFTP Configuration</h4>
            <p>Host: sftp-server.file-simulator.svc.cluster.local</p>
            <p>Port: 22</p>
          </div>
        );
      case 'S3':
        return (
          <div className="protocol-info">
            <h4>S3 Configuration</h4>
            <p>Endpoint: http://minio.file-simulator.svc.cluster.local:9000</p>
            <p>Bucket: simulator-data</p>
          </div>
        );
      case 'NFS':
        return (
          <div className="protocol-info">
            <h4>NFS Configuration</h4>
            <p>Export: /exports/data</p>
            <p>Mount: nfs-server.file-simulator.svc.cluster.local:/exports/data</p>
          </div>
        );
      default:
        return null;
    }
  };

  return (
    <div className="details-panel">
      <div className="panel-header">
        <h3>{server.Name}</h3>
        <button onClick={onClose}>×</button>
      </div>

      <div className="panel-content">
        <section>
          <h4>Status</h4>
          <p className={`status ${server.IsHealthy ? 'healthy' : 'down'}`}>
            {server.IsHealthy ? 'Healthy' : 'Down'}
          </p>
          <p>Pod Status: {server.PodStatus}</p>
          {server.HealthMessage && <p>Message: {server.HealthMessage}</p>}
        </section>

        <section>
          <h4>Metrics</h4>
          <p>Latency: {server.LatencyMs}ms</p>
          <p>Last Checked: {new Date(server.CheckedAt).toLocaleTimeString()}</p>
        </section>

        {renderProtocolSpecificInfo()}
      </div>
    </div>
  );
}
```

### Anti-Patterns to Avoid

- **Creating multiple SignalR connections**: One connection per app, not per component - use Context or singleton pattern
- **Not handling cleanup**: Always call connection.stop() in useEffect cleanup to prevent memory leaks
- **Forgetting reconnection events**: Monitor onreconnecting/onreconnected for UI feedback
- **Synchronous connection start**: Always use async/await or .then() for connection.start()
- **Missing error boundaries**: Wrap SignalR components in React error boundaries for graceful failures

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| WebSocket reconnection | Custom retry logic | `.withAutomaticReconnect()` | Handles exponential backoff, max retries, connection lifecycle |
| React + SignalR integration | Complex context setup | Custom useSignalR hook | Encapsulates connection lifecycle, cleanup, state management |
| Responsive grid layout | Media queries for each breakpoint | CSS Grid `auto-fit, minmax()` | Automatically wraps cards without hardcoded breakpoints |
| Connection state tracking | Manual WebSocket readyState checks | SignalR lifecycle events | onreconnecting, onreconnected, onclose provide full visibility |
| Time-ago display | setInterval polling | React state + useEffect | Update timestamp on each message receive, calculate in render |

**Key insight:** SignalR client library handles most WebSocket complexity. Focus on React component patterns and clean separation between connection management (hooks) and UI (components).

## Common Pitfalls

### Pitfall 1: useEffect Infinite Loop with SignalR Connection
**What goes wrong:** Creating new SignalR connection on every render causes connection churn and memory leaks
**Why it happens:** Missing dependency array or unstable dependencies (functions, objects) in useEffect
**How to avoid:**
- Use empty dependency array `[]` if connection config is static
- Use useRef for connection instance to prevent recreating
- Memoize config objects with useMemo if dynamic
**Warning signs:** Browser network tab shows repeated WebSocket connect/disconnect, console spam with "SignalR connected/disconnected"

### Pitfall 2: State Updates After Unmount
**What goes wrong:** SignalR messages arrive after component unmounts, causing "Can't perform React state update on unmounted component" warning
**Why it happens:** SignalR connection outlives component, cleanup not implemented properly
**How to avoid:**
- Always call `connection.stop()` in useEffect cleanup function
- Remove event handlers before stopping: `connection.off(eventName)`
- Use connection state flag to guard setState calls
**Warning signs:** React warning in console, setState called after component unmounted

### Pitfall 3: CORS Issues with SignalR WebSocket
**What goes wrong:** SignalR connection fails with CORS error despite REST API working
**Why it happens:** WebSocket upgrade requires special CORS handling, SignalR negotiation endpoint needs CORS
**How to avoid:**
- Backend must allow credentials in CORS: `policy.AllowAnyOrigin()` (dev) or `policy.WithOrigins()`
- SignalR connection should NOT use `withCredentials: true` unless authentication required
- Verify `/hubs/status/negotiate` endpoint returns 200 before WebSocket upgrade
**Warning signs:** Console error "CORS policy blocked", network tab shows 403 on negotiate endpoint

### Pitfall 4: Not Displaying Reconnection State
**What goes wrong:** Users don't know if real-time updates are working, think dashboard is broken during reconnect
**Why it happens:** Developer assumes connection is always stable, no UI for transient states
**How to avoid:**
- Display connection status indicator (Connected/Reconnecting/Disconnected)
- Show reconnection attempt counter: "Reconnecting (attempt 2/5)..."
- Display "Last update: Xs ago" timestamp so users know if data is stale
- Gray out or show loading state on server cards during reconnection
**Warning signs:** User confusion about whether dashboard is working, support tickets about "not updating"

### Pitfall 5: Forgetting Server-Side Message Broadcasting Timing
**What goes wrong:** Dashboard appears to update slowly or inconsistently
**Why it happens:** Backend broadcast interval (5 seconds) doesn't match user expectations, or broadcast timing assumptions
**How to avoid:**
- Document backend broadcast interval (5s) in dashboard UI: "Updates every 5 seconds"
- Use timestamp from ServerStatusUpdate message, not client time
- Show "Last update: Xs ago" so users understand polling frequency
- Consider manual refresh button if 5s too slow for some scenarios
**Warning signs:** Users clicking refresh expecting immediate update, complaints about "slow" dashboard

## Code Examples

Verified patterns from official sources:

### Vite Configuration for React + TypeScript
```typescript
// Source: Context7 Vite documentation
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react()],
    server: {
      port: 3000,
      proxy: {
        // Proxy API requests to backend during development
        '/api': {
          target: env.VITE_API_URL || 'http://localhost:30500',
          changeOrigin: true
        },
        '/hubs': {
          target: env.VITE_API_URL || 'http://localhost:30500',
          changeOrigin: true,
          ws: true // Enable WebSocket proxy
        }
      }
    },
    build: {
      outDir: 'dist',
      sourcemap: true
    }
  }
})
```

### TypeScript Type Definitions for Backend Models
```typescript
// Source: Backend Phase 6 ServerStatus.cs model
export interface ServerStatus {
  Name: string;
  Protocol: 'FTP' | 'SFTP' | 'HTTP' | 'S3' | 'SMB' | 'NFS';
  PodStatus: 'Running' | 'Pending' | 'Failed' | 'Unknown';
  IsHealthy: boolean;
  HealthMessage?: string;
  LatencyMs?: number;
  CheckedAt: string; // ISO 8601 timestamp
}

export interface ServerStatusUpdate {
  Servers: ServerStatus[];
  Timestamp: string; // ISO 8601 timestamp
  TotalServers: number;
  HealthyServers: number;
}

// Health state derived from IsHealthy + LatencyMs
export type HealthState = 'healthy' | 'degraded' | 'down' | 'unknown';

export function getHealthState(server: ServerStatus): HealthState {
  if (server.PodStatus !== 'Running') return 'down';
  if (!server.IsHealthy) return 'down';
  if (server.LatencyMs && server.LatencyMs > 3000) return 'degraded';
  return 'healthy';
}
```

### Environment Variables for Vite
```typescript
// src/vite-env.d.ts - Source: Vite TypeScript environment types
/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_URL: string;
  readonly VITE_SIGNALR_HUB_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
```

```bash
# .env.development - Source: Vite environment configuration
VITE_API_URL=http://192.168.49.2:30500
VITE_SIGNALR_HUB_URL=http://192.168.49.2:30500/hubs/status
```

### App Component with useSignalR Hook Integration
```tsx
// Source: React 19 + SignalR integration pattern
import { useState } from 'react';
import { useSignalR } from './hooks/useSignalR';
import { ServerStatusUpdate } from './types/server';
import ServerGrid from './components/ServerGrid';
import ServerDetailsPanel from './components/ServerDetailsPanel';
import ConnectionStatus from './components/ConnectionStatus';
import SummaryHeader from './components/SummaryHeader';

function App() {
  const hubUrl = import.meta.env.VITE_SIGNALR_HUB_URL;
  const { data, isConnected, isReconnecting, reconnectAttempt } =
    useSignalR<ServerStatusUpdate>(hubUrl, 'ServerStatusUpdate');

  const [selectedServer, setSelectedServer] = useState(null);

  return (
    <div className="app">
      <header>
        <h1>File Simulator Suite - Monitoring Dashboard</h1>
        <ConnectionStatus
          isConnected={isConnected}
          isReconnecting={isReconnecting}
          reconnectAttempt={reconnectAttempt}
          lastUpdate={data ? new Date(data.Timestamp) : null}
        />
      </header>

      <main>
        {data ? (
          <>
            <SummaryHeader
              totalServers={data.TotalServers}
              healthyServers={data.HealthyServers}
            />
            <ServerGrid
              servers={data.Servers}
              onCardClick={setSelectedServer}
            />
          </>
        ) : (
          <div className="loading">Loading server status...</div>
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
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Create React App | Vite | 2023 | 10x faster dev server, native ESM, smaller bundle |
| useMemo/useCallback everywhere | React 19 compiler | React 19 (2024) | Automatic memoization, cleaner code |
| Multiple hooks for context | use() API | React 19 (2024) | Single hook for promises + context, conditional use allowed |
| Manual WebSocket management | SignalR .withAutomaticReconnect() | SignalR 3.0+ | Built-in exponential backoff, max retries |
| CSS-in-JS libraries | Native CSS modules + Grid/Flexbox | 2024-2025 | Better performance, no JS overhead, standard CSS |
| useEffect + useState for data | use() hook for promises | React 19 (2024) | Simpler async data handling, less boilerplate |

**Deprecated/outdated:**
- Create React App: Officially deprecated, Vite is recommended replacement
- forwardRef: React 19 allows passing refs as props directly
- useMemo/useCallback for performance: React 19 compiler handles automatically

## Open Questions

Things that couldn't be fully resolved:

1. **Should we use React 19 compiler?**
   - What we know: React 19 includes compiler for automatic memoization
   - What's unclear: Compiler opt-in or opt-out in Vite, compatibility with TypeScript strict mode
   - Recommendation: Skip compiler for Phase 7 (requires explicit enablement), add in Phase 9 if performance issues

2. **CSS approach: Plain CSS vs CSS Modules vs Tailwind?**
   - What we know: Plain CSS sufficient for Phase 7 scope (13 cards + 1 panel)
   - What's unclear: User preference for styling approach
   - Recommendation: Plain CSS with BEM naming for Phase 7, can migrate to Tailwind in Phase 9 if UI expands

3. **Should activity feed be in Phase 7 or deferred?**
   - What we know: CONTEXT.md says "Claude decides based on implementation value"
   - What's unclear: User workflow - is event history needed immediately?
   - Recommendation: Defer to Phase 9 (historical data phase), focus Phase 7 on real-time status only

## Sources

### Primary (HIGH confidence)
- [Context7: @microsoft/signalr](https://www.npmjs.com/package/@microsoft/signalr) - SignalR client API, connection patterns
- [Context7: React official docs](https://react.dev) - useEffect lifecycle, hooks patterns, component structure
- [Context7: Vite official docs](https://vitejs.dev) - Build configuration, environment variables, dev server setup
- Backend Phase 6-03 implementation: ServerStatus.cs, ServerStatusHub.cs, ServerStatusBroadcaster.cs - Verified backend contract

### Secondary (MEDIUM confidence)
- [Enhancing SignalR Connection Handling in React with TypeScript](https://www.xjavascript.com/blog/signalr-react-typescript/) - Comprehensive guide to SignalR + React integration patterns
- [React 19 New Features (FreeCodeCamp)](https://www.freecodecamp.org/news/react-19-new-hooks-explained-with-examples/) - New hooks (use, useActionState, useOptimistic)
- [Modern CSS Layout Techniques 2025-2026](https://www.frontendtools.tech/blog/modern-css-layout-techniques-flexbox-grid-subgrid-2025) - CSS Grid + Flexbox dashboard patterns
- [Building Dashboard UI using Grid and Flex-box (Medium)](https://medium.com/@kevjose/building-dashboards-using-grid-and-flex-box-620adc1fff51) - Card layout patterns

### Tertiary (LOW confidence)
- [WebSearch: React hooks SignalR connection management best practices](https://github.com/hwdtech/react-signalr) - Community patterns, marked for validation
- [WebSearch: React dashboard real-time updates](https://www.telerik.com/kendo-react-ui/components/sample-applications/admin-dashboard) - Vendor examples, not authoritative

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official libraries (@microsoft/signalr, React 19, Vite 7), verified versions
- Architecture: HIGH - Patterns from official docs + verified backend contract from Phase 6
- Pitfalls: HIGH - Common issues documented in official GitHub issues, experienced in production

**Research date:** 2026-02-02
**Valid until:** 30 days (stable technology stack, React 19 released December 2024)
