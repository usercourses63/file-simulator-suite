# Phase 7: Real-Time Monitoring Dashboard - Context

**Gathered:** 2026-02-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver a React dashboard with real-time health monitoring and protocol connectivity visualization. The dashboard displays health status for all 13 servers (7 NAS + 6 protocols) updated every 5 seconds via SignalR WebSocket. Users can see status changes without page refresh, view connection quality metrics, and access protocol-specific details. Dashboard reconnects automatically when WebSocket disconnects.

</domain>

<decisions>
## Implementation Decisions

### Server Grid Layout
- Servers grouped by type: NAS servers together (7), protocol servers together (6)
- Medium-sized cards (~120px height) showing name + status + 2-3 visible metrics
- Primary metric displayed on each card: response latency (e.g., "45ms")
- Clicking a card opens a details panel (right sidebar)
- Summary header above grid showing status counts: "7 Healthy - 0 Degraded - 0 Down"

### Status Visualization
- Three health states: Healthy (green), Degraded (yellow), Down (red)
- Unknown/Loading state: Gray with "Checking..." for initial load or connection loss
- Visual style: Colored dot + subtle border tint on cards
- State change attention: Brief pulse animation when status changes

### Real-Time Updates UX
- Connection status indicator in dashboard header (small dot showing "Connected" or "Reconnecting...")
- Reconnection display: "Reconnecting (attempt 2/5)..." with retry count
- Data freshness: "Last update: 3s ago" always visible in header
- Activity feed: Claude decides based on implementation value for Phase 7

### Protocol Details Panel
- Position: Right sidebar that slides in when card is clicked
- Content: Connection info + metrics (host:port, connection string examples, latency, success rate, last 5 checks)
- Copy-to-clipboard buttons on connection strings
- Credentials shown in plain text (dev environment convenience)
- Protocol-specific sections: Tailored details per protocol type (FTP shows passive mode, S3 shows bucket, etc.)

### Claude's Discretion
- Whether degraded/down servers sort to top of their group for visibility
- Whether to include a small activity feed for recent events (recommend skip for Phase 7, add in Phase 9)
- Exact spacing, typography, and animation timing
- Error boundary and loading skeleton implementations

</decisions>

<specifics>
## Specific Ideas

- Summary header provides at-a-glance health overview without scanning all 13 cards
- Pulse animation on status change draws attention without being disruptive
- Retry count during reconnection helps users understand progress ("attempt 2/5" vs just "reconnecting")
- Plain text credentials appropriate for development environment where convenience matters more than security
- Protocol-specific detail sections (FTP passive mode, S3 bucket name, NFS mount path) make the panel genuinely useful

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope

</deferred>

---

*Phase: 07-real-time-monitoring-dashboard*
*Context gathered: 2026-02-02*
