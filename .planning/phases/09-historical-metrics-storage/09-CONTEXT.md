# Phase 9: Historical Metrics and Storage - Context

## Goal
Add time-series data persistence with 7-day retention and historical trend visualization.

## Success Criteria
1. SQLite database persists health metrics with 5-second granularity for 7 days
2. Historical trends dashboard shows connection counts, latency, and errors over time
3. User can query metrics for specific time ranges (last 1h, last 24h, last 7d)
4. Database survives pod restarts with data intact
5. Auto-cleanup removes data older than 7 days to prevent unbounded growth

## Decisions from Discussion

### 1. Data Visualization
**Choice:** Full charting library with zoom, pan, tooltips, and legend toggling

**Implementation:**
- Use Recharts (React-native, composable, good TypeScript support)
- Line charts for latency trends
- Area charts for cumulative/status metrics
- Interactive features: zoom, pan, tooltips on hover, legend click to toggle series
- Responsive sizing for different screen widths

### 2. Time Range Selection
**Choice:** Preset buttons + relative dropdown, default 24-hour view

**Implementation:**
- Quick preset buttons: 1h, 6h, 24h, 7d
- Dropdown for granular options: 15m, 30m, 1h, 2h, 6h, 12h, 24h, 3d, 7d
- Default view: Last 24 hours when History tab opened
- Auto-refresh toggle (every 30 seconds when enabled)

### 3. Metrics to Track
**Choice:** Health status + response times (latency)

**Data points per sample:**
- `timestamp` - UTC datetime
- `server_id` - Server identifier (e.g., "nas-input-1", "ftp")
- `server_type` - Protocol type (NAS, FTP, SFTP, HTTP, S3, SMB, NFS)
- `is_healthy` - Boolean health status
- `latency_ms` - Response time in milliseconds (null if unhealthy)

**Derived metrics (calculated from stored data):**
- Uptime percentage over period
- Average/min/max/p95 latency
- Error count (transitions to unhealthy)

### 4. Dashboard Layout
**Choice:** History tab + inline sparklines on server cards

**Implementation:**
- New "History" tab (Tab 3) with full charting interface
- Mini sparklines (last 1 hour) embedded in each server card on Servers tab
- Sparklines show latency trend, colored by health (green=healthy, red=unhealthy periods)
- Click sparkline to jump to History tab filtered to that server

### 5. Data Aggregation
**Choice:** Raw 5-second samples + hourly rollups

**Schema design:**
```
Table: health_samples (raw data)
- id (INTEGER PRIMARY KEY)
- timestamp (DATETIME, indexed)
- server_id (TEXT)
- server_type (TEXT)
- is_healthy (BOOLEAN)
- latency_ms (REAL)

Table: health_hourly (rollups)
- id (INTEGER PRIMARY KEY)
- hour_start (DATETIME, indexed)
- server_id (TEXT)
- server_type (TEXT)
- sample_count (INTEGER)
- healthy_count (INTEGER)
- avg_latency_ms (REAL)
- min_latency_ms (REAL)
- max_latency_ms (REAL)
- p95_latency_ms (REAL)
```

**Query strategy:**
- < 2 hours: Use raw samples
- 2h - 24h: Use raw samples (acceptable count)
- > 24h: Use hourly rollups

**Retention:**
- Raw samples: 7 days
- Hourly rollups: 7 days (could extend later if needed)

## Technical Approach

### Backend (Control API)
- Entity Framework Core with SQLite provider
- Background service for hourly rollup generation
- Background service for retention cleanup (runs daily)
- REST endpoints for metrics queries
- SignalR for real-time sample streaming to dashboard

### Frontend (Dashboard)
- Recharts library for charting
- New HistoryTab component with time range controls
- ServerCard enhanced with sparkline component
- Custom hooks for metrics data fetching

### Storage
- SQLite database file at `/mnt/control-data/metrics.db`
- PVC for control-api pod to persist database across restarts
- Estimated size: ~15 MB after 7 days (raw + rollups for 13 servers)

## Dependencies
- Phase 7: Monitoring data collection (HealthCheckService provides data)
- Phase 8: SignalR infrastructure (reuse for metrics streaming)

## Estimated Plans
1. **09-01**: Backend - SQLite schema, EF Core entities, MetricsService, retention cleanup
2. **09-02**: Backend - REST API endpoints for metrics queries, SignalR streaming
3. **09-03**: Frontend - Recharts setup, HistoryTab component, time range controls
4. **09-04**: Frontend - Sparklines on server cards, History tab integration
5. **09-05**: Human verification checkpoint
