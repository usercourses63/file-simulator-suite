---
phase: 22-nas-compact-table-view
plan: 02
subsystem: ui
tags: [react, sparkline, accordion, expand-collapse, nas, signalr]

# Dependency graph
requires:
  - phase: 22-01
    provides: "NasTable component with grouped compact rows"
  - phase: 9-05
    provides: "ServerSparkline component and sparkline data flow"
provides:
  - "NasTable expandable row detail with sparkline and server metadata"
  - "Sparkline data wired from ServerGrid to NasTable for NAS servers"
affects: [23-server-details-panel, 25-e2e-tests]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Accordion expand/collapse with chevron rotation", "Inline detail section with sparkline and metadata grid"]

key-files:
  created: []
  modified:
    - src/dashboard/src/components/NasTable.tsx
    - src/dashboard/src/components/NasTable.css
    - src/dashboard/src/components/ServerGrid.tsx

key-decisions:
  - "Chevron click uses stopPropagation to separate expand/collapse from row click (details panel)"
  - "ServerSparkline reused in expanded detail with 200x30 dimensions for inline display"
  - "Accordion pattern: only one row expanded at a time via single expandedServer state"

patterns-established:
  - "Accordion row expand: chevron toggles detail section, stopPropagation separates from row click"
  - "Inline detail grid: flex layout with sparkline sidebar and auto-fill metadata grid"

# Metrics
duration: 2min
completed: 2026-02-12
---

# Phase 22 Plan 02: NAS Expandable Row Detail Summary

**Accordion expand/collapse on NAS table rows revealing ServerSparkline latency chart and server metadata (health, service, cluster IP, last checked)**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-12T15:24:45Z
- **Completed:** 2026-02-12T15:27:00Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Clicking chevron on any NAS row expands inline detail showing latency sparkline and server metadata
- Accordion behavior ensures only one row expanded at a time; chevron rotates 90 degrees when expanded
- Sparkline data flows from App -> ServerGrid -> NasTable -> ServerSparkline; clicking sparkline navigates to History tab
- Expanded detail shows health state (color-coded), health message, service name, cluster IP, last checked timestamp

## Task Commits

Each task was committed atomically:

1. **Task 1: Add expandable row detail with sparkline and metrics to NasTable** - `6a68bb2` (feat)
2. **Task 2: Wire sparkline data from ServerGrid to NasTable** - `4328429` (feat)

## Files Created/Modified
- `src/dashboard/src/components/NasTable.tsx` - Added sparklineData/onSparklineClick props, expandedServer state, chevron click handler, expanded detail section with ServerSparkline and metadata grid
- `src/dashboard/src/components/NasTable.css` - Added chevron rotation transition, expanded row highlight, detail section flex layout, detail grid, animation keyframes, health state colors
- `src/dashboard/src/components/ServerGrid.tsx` - Passes sparklineData and onSparklineClick props to NasTable

## Decisions Made
- Chevron click uses stopPropagation so expanding a row does not also open the details panel (row body click still opens panel)
- Reused existing ServerSparkline component at 200x30 for consistency with card sparklines
- Single expandedServer state string provides natural accordion behavior without extra logic

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- NasTable component fully complete with grouped rows and expandable detail
- Phase 22 (NAS Compact Table View) done; ready for Phase 23 (Server Details Panel Adaptation)
- TypeScript compilation and production build pass cleanly

## Self-Check: PASSED

- [x] NasTable.tsx modified with expandable detail
- [x] NasTable.css modified with detail styles
- [x] ServerGrid.tsx modified with sparkline props
- [x] Commit 6a68bb2 exists
- [x] Commit 4328429 exists
- [x] TypeScript compilation passes
- [x] Production build succeeds

---
*Phase: 22-nas-compact-table-view*
*Completed: 2026-02-12*
