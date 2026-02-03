---
phase: 09-historical-metrics-storage
plan: 05
subsystem: frontend-dashboard
tags: [react, sparklines, signalr, css, typescript]

dependency-graph:
  requires: [09-04]
  provides: [sparklines-on-cards, history-tab-navigation, metrics-stream-hook]
  affects: [10-kafka-integration]

tech-stack:
  added: []
  patterns:
    - react-sparklines for inline mini-charts
    - SignalR rolling buffer for real-time updates
    - Tab-based navigation with state management
    - CSS BEM styling for new components

file-tracking:
  key-files:
    created:
      - src/dashboard/src/components/ServerSparkline.tsx
      - src/dashboard/src/hooks/useMetricsStream.ts
    modified:
      - src/dashboard/src/components/ServerCard.tsx
      - src/dashboard/src/components/ServerGrid.tsx
      - src/dashboard/src/App.tsx
      - src/dashboard/src/App.css
      - src/dashboard/src/types/metrics.ts

decisions:
  - key: sparkline-click-navigation
    choice: Sparkline click navigates to History tab with server filter
    reason: Provides quick access to detailed metrics for specific server

metrics:
  duration: 7m
  completed: 2026-02-03
---

# Phase 9 Plan 05: Sparklines, History Tab Integration, and CSS Styling Summary

**One-liner:** Real-time sparklines on server cards with History tab navigation and complete CSS styling for metrics visualization.

## What Was Built

### Task 1: ServerSparkline and useMetricsStream Hook

**ServerSparkline Component** (`src/dashboard/src/components/ServerSparkline.tsx`)
- Wraps react-sparklines library for mini latency charts
- 80x20 pixel sparkline showing latency trend
- Color-coded: green for healthy, red for unhealthy
- Reference line showing mean latency
- Click handler for navigation to History tab
- Accessible with keyboard support (Enter key)

**useMetricsStream Hook** (`src/dashboard/src/hooks/useMetricsStream.ts`)
- SignalR connection to `/hubs/metrics`
- Receives MetricsSample events with per-server latency data
- Maintains rolling buffer of 60 samples per server (5 minutes at 5s interval)
- Auto-reconnect with exponential backoff
- Returns `Map<serverId, number[]>` for sparkline consumption

### Task 2: ServerCard Enhancement and App Integration

**ServerCard Updates** (`src/dashboard/src/components/ServerCard.tsx`)
- Added `sparklineData` prop for latency values
- Added `onSparklineClick` prop for history navigation
- Sparkline renders below metrics when data available
- Click event stops propagation to prevent triggering card click

**ServerGrid Updates** (`src/dashboard/src/components/ServerGrid.tsx`)
- Added `sparklineData` prop (Map<string, number[]>)
- Added `onSparklineClick` callback
- Passes data and handlers to each ServerCard

**App.tsx Updates**
- Added History tab button in navigation
- Extended activeTab type to include 'history'
- Connected useMetricsStream hook
- Added historyServerId state for server filtering
- Sparkline click sets server filter and switches to History tab
- Renders HistoryTab component when active

### Task 3: CSS Styling

**History Tab Styles**
- `.history-tab` container with padding
- `.history-tab-header` flexbox layout for title and controls
- `.history-tab-filters` for server selector and resolution badge
- `.history-tab-error`, `.history-tab-loading`, `.history-tab-empty` states
- `.history-tab-stats` for sample count display

**Time Range Selector Styles**
- `.time-range-selector` flex container
- `.time-range-presets` button group with rounded corners
- `.time-range-preset--active` green active state
- `.time-range-dropdown` select styling
- `.time-range-auto-refresh` checkbox label

**Latency Chart Styles**
- `.latency-chart` card container with border
- `.latency-chart-controls` for zoom button and hint
- `.zoom-reset-btn` styled button with disabled state
- `.zoom-hint` subtle helper text

**Server Sparkline Styles**
- `.server-card-sparkline` separator with border-top
- `.server-sparkline` clickable area with hover effect
- `.server-sparkline--empty` placeholder styling
- `.sparkline-placeholder` for "--" text

**Recharts Overrides**
- Grid lines use CSS variable colors
- Text fills use secondary text color
- Legend text color override

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Missing 09-04 dependencies initially**
- **Found during:** Task 1 preparation
- **Issue:** HistoryTab, LatencyChart, TimeRangeSelector, useMetrics, and metrics.ts were expected but showed as missing
- **Fix:** Found that 09-04 had already been executed (commits exist), files were tracked
- **Files affected:** None (existing files were correctly present)
- **Resolution:** Proceeded with 09-05 tasks building on existing 09-04 artifacts

## Commits

| Hash | Type | Description |
|------|------|-------------|
| f98daea | feat | add ServerSparkline component and useMetricsStream hook |
| f0dbe6a | feat | integrate sparklines and History tab into dashboard |
| cb6ffa5 | style | add CSS for History tab, sparklines, and time range selector |

## Verification Results

1. **npm run build** - Compiles without TypeScript errors
2. **ServerCard sparkline section** - Verified with grep: imports ServerSparkline, renders conditionally
3. **Three tabs in App.tsx** - Verified: Servers, Files, History
4. **useMetricsStream connects to /hubs/metrics** - Verified: metricsHubUrl variable set
5. **CSS styling** - Verified: history-tab, time-range-selector, latency-chart, server-sparkline classes present

## Architecture Notes

### Data Flow
```
MetricsHub (SignalR)
    │
    ▼
useMetricsStream hook
    │ Map<serverId, number[]>
    ▼
App.tsx
    │ sparklineData prop
    ▼
ServerGrid
    │ sparklineData per server
    ▼
ServerCard
    │
    ▼
ServerSparkline (react-sparklines)
```

### User Interaction Flow
```
User clicks sparkline on ServerCard
    │
    ▼
handleSparklineClick(serverId)
    │
    ▼
setHistoryServerId(serverId)
setActiveTab('history')
    │
    ▼
HistoryTab renders with initialServerId
    │
    ▼
Server dropdown pre-selects clicked server
Chart shows metrics filtered to that server
```

## Next Phase Readiness

Phase 9 frontend is complete. The dashboard now has:
- Real-time sparklines on all server cards
- History tab with time range selection and zoomable charts
- Complete CSS styling matching existing theme

Ready for Phase 10 (Kafka Integration) which will add:
- Kafka message broker for event streaming
- Additional real-time data sources
