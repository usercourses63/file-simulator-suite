---
phase: 24-secondary-tab-improvements
plan: 01
subsystem: ui
tags: [react, recharts, signalr, history-tab, latency-chart]

# Dependency graph
requires:
  - phase: 22-nas-compact-table-view
    provides: "Server grid with sparkline-to-history navigation"
provides:
  - "History tab dropdown filtered to currently deployed servers"
  - "Chart legend filtered to servers with data in selected range"
  - "deployedServerNames prop pipeline from App.tsx to HistoryTab"
affects: [25-e2e-tests]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "deployedServerNames prop for filtering stale/deleted server references"
    - "activeServerIds memo for data-presence filtering in charts"
    - "Color consistency via original serverIds index lookup"

key-files:
  created: []
  modified:
    - "src/dashboard/src/App.tsx"
    - "src/dashboard/src/components/HistoryTab.tsx"
    - "src/dashboard/src/components/LatencyChart.tsx"

key-decisions:
  - "Dropdown uses deployedServerNames from SignalR; falls back to metrics-derived serverIds when unavailable"
  - "Chart colors use original serverIds index (not activeServerIds index) for cross-range color stability"

patterns-established:
  - "Data-presence filtering: useMemo to filter IDs by checking data.some(point => point[id] !== undefined)"

# Metrics
duration: 2min
completed: 2026-02-12
---

# Phase 24 Plan 01: History Tab Server Filtering Summary

**History dropdown filtered to deployed servers via SignalR data, chart legend filtered to servers with data via activeServerIds memo**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-12T16:06:52Z
- **Completed:** 2026-02-12T16:09:05Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- History tab server dropdown now only shows currently deployed servers (excludes deleted dynamic servers)
- Chart legend hides servers that have no data points in the selected time range
- Color assignments remain stable per server across different time range selections

## Task Commits

Each task was committed atomically:

1. **Task 1: Pass deployed server names to HistoryTab and filter dropdown** - `41b8116` (feat)
2. **Task 2: Filter LatencyChart legend to only servers with data** - `47ec241` (feat)

## Files Created/Modified
- `src/dashboard/src/App.tsx` - Added deployedServerNames useMemo and prop pass-through to SafeHistoryTab
- `src/dashboard/src/components/HistoryTab.tsx` - Added deployedServerNames prop, dropdownServers computed value for select element
- `src/dashboard/src/components/LatencyChart.tsx` - Added activeServerIds memo, updated Line rendering to use filtered list with stable colors

## Decisions Made
- Dropdown uses deployedServerNames from SignalR real-time data; falls back to metrics-derived serverIds when unavailable (graceful degradation)
- Chart colors are indexed against the original serverIds array (not activeServerIds) so a server always gets the same color regardless of which other servers have data

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- History tab filtering complete, ready for 24-02 (remaining secondary tab improvements)
- E2E tests in phase 25 should verify dropdown filtering behavior

## Self-Check: PASSED

- FOUND: src/dashboard/src/App.tsx
- FOUND: src/dashboard/src/components/HistoryTab.tsx
- FOUND: src/dashboard/src/components/LatencyChart.tsx
- FOUND: commit 41b8116
- FOUND: commit 47ec241
- VERIFIED: deployedServerNames pattern in App.tsx and HistoryTab.tsx
- VERIFIED: activeServerIds pattern in LatencyChart.tsx

---
*Phase: 24-secondary-tab-improvements*
*Completed: 2026-02-12*
