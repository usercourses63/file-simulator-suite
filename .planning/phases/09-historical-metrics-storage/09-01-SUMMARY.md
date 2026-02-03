---
phase: 09-historical-metrics-storage
plan: 01
subsystem: database
tags: [ef-core, sqlite, time-series, metrics]

# Dependency graph
requires:
  - phase: 06-backend-api-foundation
    provides: ASP.NET Core Control API project structure
provides:
  - EF Core 9 SQLite data layer for metrics persistence
  - HealthSample entity for raw 5-second samples
  - HealthHourly entity for hourly rollup aggregations
  - MetricsService for recording and querying metrics
affects: [09-02, 09-03, 09-04, 10-event-streaming]

# Tech tracking
tech-stack:
  added: [Microsoft.EntityFrameworkCore.Sqlite 9.0.0, Microsoft.EntityFrameworkCore.Design 9.0.0]
  patterns: [IDbContextFactory pattern for background service compatibility, DateTime UTC for SQLite timestamps]

key-files:
  created:
    - src/FileSimulator.ControlApi/Data/Entities/HealthSample.cs
    - src/FileSimulator.ControlApi/Data/Entities/HealthHourly.cs
    - src/FileSimulator.ControlApi/Data/MetricsDbContext.cs
    - src/FileSimulator.ControlApi/Services/IMetricsService.cs
    - src/FileSimulator.ControlApi/Services/MetricsService.cs
  modified:
    - src/FileSimulator.ControlApi/FileSimulator.ControlApi.csproj
    - src/FileSimulator.ControlApi/Program.cs

key-decisions:
  - "Use DateTime (UTC) not DateTimeOffset - SQLite cannot order/compare DateTimeOffset"
  - "Use IDbContextFactory pattern - enables MetricsService in background services"
  - "Snake_case table/column names - follows standard database conventions"
  - "Composite indexes on (ServerId, Timestamp) - optimizes time-range queries per server"

patterns-established:
  - "IDbContextFactory injection: Use factory for database access in services that may be called from background services"
  - "DateTime.UtcNow for timestamps: Always store UTC, never local time"
  - "EnsureCreated on startup: Auto-create SQLite database if missing"

# Metrics
duration: 4min
completed: 2026-02-03
---

# Phase 9 Plan 01: SQLite Data Layer Summary

**EF Core 9 SQLite metrics persistence with HealthSample/HealthHourly entities, composite indexes, and MetricsService**

## Performance

- **Duration:** 4 min
- **Started:** 2026-02-03T10:00:00Z
- **Completed:** 2026-02-03T10:04:00Z
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- EF Core 9 SQLite packages installed for embedded database storage
- HealthSample entity for raw 5-second health check samples with timestamp, serverId, latency
- HealthHourly entity for hourly rollups with P95 latency and aggregation stats
- MetricsDbContext with proper Fluent API configuration and composite indexes
- MetricsService with RecordSampleAsync, QuerySamplesAsync, QueryHourlyAsync methods
- Database auto-creates on startup via EnsureCreated

## Task Commits

Each task was committed atomically:

1. **Task 1: Add EF Core SQLite packages and create entities** - `1a97780` (feat)
2. **Task 2: Create MetricsDbContext with entity configuration** - `aee7602` (feat)
3. **Task 3: Create MetricsService and register in DI** - `cd97c7f` (feat)

## Files Created/Modified
- `src/FileSimulator.ControlApi/FileSimulator.ControlApi.csproj` - Added EF Core SQLite packages
- `src/FileSimulator.ControlApi/Data/Entities/HealthSample.cs` - Raw sample entity
- `src/FileSimulator.ControlApi/Data/Entities/HealthHourly.cs` - Hourly rollup entity
- `src/FileSimulator.ControlApi/Data/MetricsDbContext.cs` - DbContext with indexes
- `src/FileSimulator.ControlApi/Services/IMetricsService.cs` - Service interface
- `src/FileSimulator.ControlApi/Services/MetricsService.cs` - Service implementation
- `src/FileSimulator.ControlApi/Program.cs` - DI registration and EnsureCreated

## Decisions Made
- Used DateTime (not DateTimeOffset) for all timestamp properties - SQLite limitation prevents ordering/comparison of DateTimeOffset
- Used IDbContextFactory pattern instead of direct DbContext injection - enables background service compatibility
- Added snake_case column/table names via Fluent API - follows database conventions
- Created composite index on (ServerId, Timestamp) - most common query pattern for time-range queries per server
- Added separate index on Timestamp - enables efficient retention cleanup queries

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed without issues.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- MetricsDbContext ready for MetricsRecordingService (Plan 02) to capture samples from health checks
- MetricsService ready for REST API endpoints (Plan 02) for metrics queries
- IDbContextFactory pattern enables background services (RetentionCleanupService, RollupGenerationService)

---
*Phase: 09-historical-metrics-storage*
*Completed: 2026-02-03*
