---
phase: 09-historical-metrics-storage
plan: 02
subsystem: api, database
tags: [sqlite, ef-core, background-service, metrics, p95, retention]

# Dependency graph
requires:
  - phase: 09-01
    provides: MetricsDbContext, HealthSample/HealthHourly entities, SQLite connection
provides:
  - Metrics recording on 5-second health check cycle
  - Hourly rollup generation with P95 percentile calculation
  - 7-day retention cleanup with bulk delete
  - Persistent storage for metrics database
affects: [09-03, 09-04, 09-05, 09-06]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "IDbContextFactory pattern for background service DB access"
    - "Linear interpolation P95 percentile calculation"
    - "ExecuteDeleteAsync for efficient bulk deletes"
    - "hostPath volume for Minikube database persistence"

key-files:
  created:
    - src/FileSimulator.ControlApi/Services/RollupGenerationService.cs
    - src/FileSimulator.ControlApi/Services/RetentionCleanupService.cs
  modified:
    - src/FileSimulator.ControlApi/Services/ServerStatusBroadcaster.cs
    - src/FileSimulator.ControlApi/Program.cs
    - helm-chart/file-simulator/templates/control-api.yaml
    - helm-chart/file-simulator/values.yaml

key-decisions:
  - "IDbContextFactory pattern instead of direct DbContext injection (background services are singleton)"
  - "5-minute initial delay for rollup generation to let system stabilize"
  - "10-minute initial delay for retention cleanup"
  - "hostPath volume for database persistence (matches simulator-data pattern)"

patterns-established:
  - "Background service metrics recording: inject IDbContextFactory, create context per operation"
  - "P95 calculation: linear interpolation on pre-sorted array"
  - "Bulk delete pattern: ExecuteDeleteAsync with Where clause"

# Metrics
duration: 4min
completed: 2026-02-03
---

# Phase 9 Plan 2: Background Services for Metrics Summary

**Three background services for continuous metrics recording, hourly rollup generation with P95, and 7-day retention cleanup**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-03T06:05:55Z
- **Completed:** 2026-02-03T06:09:45Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments

- ServerStatusBroadcaster records health samples to SQLite after every 5-second health check cycle
- RollupGenerationService generates hourly aggregations with avg/min/max/P95 latency metrics
- RetentionCleanupService purges data older than 7 days using efficient bulk deletes
- Helm chart updated with control-data hostPath volume for database persistence

## Task Commits

Each task was committed atomically:

1. **Task 1: Add metrics recording to ServerStatusBroadcaster** - `7eb39b8` (feat)
2. **Task 2: Create RollupGenerationService and RetentionCleanupService** - `f6d43a5` (feat)
3. **Task 3: Add persistent storage for metrics database** - `33ac5fe` (feat)

## Files Created/Modified

- `src/FileSimulator.ControlApi/Services/ServerStatusBroadcaster.cs` - Added IDbContextFactory injection and RecordMetricsAsync method
- `src/FileSimulator.ControlApi/Services/RollupGenerationService.cs` - NEW: Hourly rollup generation with P95 calculation
- `src/FileSimulator.ControlApi/Services/RetentionCleanupService.cs` - NEW: 7-day retention cleanup with bulk delete
- `src/FileSimulator.ControlApi/Program.cs` - Registered RollupGenerationService and RetentionCleanupService
- `helm-chart/file-simulator/templates/control-api.yaml` - Added control-data volume mount
- `helm-chart/file-simulator/values.yaml` - Added controlApi.persistence configuration

## Decisions Made

1. **IDbContextFactory pattern** - Background services are singletons, DbContext is scoped; using factory pattern allows creating context per operation
2. **Initial delays** - 5min for rollups, 10min for retention to let system stabilize before heavy operations
3. **hostPath volume** - Matches existing simulator-data pattern for Minikube; in production would use PVC with storage class

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Metrics recording is now continuous (every 5 seconds)
- Rollups will be generated hourly with full statistical aggregations
- Retention cleanup ensures database size stays manageable
- Ready for Phase 9-03: REST API endpoints for querying historical metrics

---
*Phase: 09-historical-metrics-storage*
*Completed: 2026-02-03*
