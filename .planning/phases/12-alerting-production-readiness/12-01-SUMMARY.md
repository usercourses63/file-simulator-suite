---
phase: 12
plan: 01
subsystem: alerting
tags: [backend, signalr, health-checks, alerts, sqlite, ef-core]

requires:
  - 09-01  # MetricsDbContext and EF Core infrastructure
  - 06-01  # Control API foundation and SignalR setup
  - 10-01  # Kafka infrastructure for health monitoring

provides:
  - Alert domain model and persistence
  - Custom health checks (disk space, Kafka)
  - AlertService background monitoring
  - AlertHub for real-time notifications
  - /health endpoint with custom checks

affects:
  - 12-02  # Frontend alert UI will consume AlertHub
  - future  # Production monitoring and alerting dashboard

tech-stack:
  added:
    - Microsoft.Extensions.Diagnostics.HealthChecks (built-in)
  patterns:
    - IHostedService background monitoring
    - SignalR real-time broadcasting
    - Custom IHealthCheck implementations
    - EF Core migrations for schema changes

key-files:
  created:
    - src/FileSimulator.ControlApi/Models/Alert.cs
    - src/FileSimulator.ControlApi/Models/AlertSeverity.cs
    - src/FileSimulator.ControlApi/Data/Entities/AlertEntity.cs
    - src/FileSimulator.ControlApi/Services/DiskSpaceHealthCheck.cs
    - src/FileSimulator.ControlApi/Services/KafkaHealthCheck.cs
    - src/FileSimulator.ControlApi/Services/AlertService.cs
    - src/FileSimulator.ControlApi/Hubs/AlertHub.cs
    - src/FileSimulator.ControlApi/Migrations/20260205074911_AddAlertEntity.cs
  modified:
    - src/FileSimulator.ControlApi/Data/MetricsDbContext.cs
    - src/FileSimulator.ControlApi/Program.cs

decisions:
  - key: alert-severity-levels
    choice: Three levels (Info, Warning, Critical)
    rationale: Simple hierarchy covers most operational scenarios
    alternatives: [Five levels like log levels, Two levels (warning/critical)]
    impact: Frontend UI can use color coding and prioritization

  - key: alert-deduplication
    choice: Update existing alert if same Type+Source unresolved
    rationale: Prevents flooding with duplicate alerts during persistent issues
    alternatives: [Always create new alert, Use sliding window]
    impact: Clean alert feed, but consecutive occurrences not tracked

  - key: server-health-threshold
    choice: 3 consecutive failures (15 seconds)
    rationale: Balances quick detection with avoiding false positives
    alternatives: [Single failure, 5 failures, Time-based 30s]
    impact: Alert fires within 15s of persistent issue

  - key: disk-space-threshold
    choice: 1GB warning threshold
    rationale: Adequate for development/testing, adjustable via env var
    alternatives: [Percentage-based 10%, 5GB threshold]
    impact: Works for typical dev machines, may need tuning for production

  - key: kafka-health-check
    choice: TCP connection test with 5s timeout
    rationale: Fast, simple, doesn't require AdminClient overhead
    alternatives: [Full AdminClient metadata query, HTTP health endpoint]
    impact: Detects broker down quickly, doesn't verify topics/ACLs

  - key: alert-retention
    choice: 7-day automatic cleanup
    rationale: Keeps database size manageable while preserving recent history
    alternatives: [30 days, Unlimited with manual cleanup, 24 hours]
    impact: AlertHub.GetAlertHistory shows max 7 days of data

  - key: database-migration-strategy
    choice: Switch from EnsureCreated to Migrate
    rationale: Proper support for schema changes as project evolves
    alternatives: [Keep EnsureCreated, Manual migrations]
    impact: Database schema versioned, safer for production upgrades

metrics:
  duration: 5.3min
  completed: 2026-02-05
  tasks: 6
  commits: 6
  files_created: 8
  files_modified: 2
---

# Phase 12 Plan 01: Backend Alert Infrastructure Summary

Implement backend alert management system with custom health checks, alert persistence, and real-time SignalR notifications.

## One-liner

Backend alert infrastructure with DiskSpace/Kafka health checks, AlertService monitoring, SignalR broadcasting, and 7-day alert retention.

## What Was Built

### Alert Domain Models
- **AlertSeverity enum**: Info, Warning, Critical severity levels
- **Alert domain model**: Complete alert data structure with Type, Severity, Title, Message, Source, timestamps
- **AlertEntity**: EF Core entity with snake_case persistence and ToModel/FromModel conversions

### Database Schema
- **alerts table**: Stores all alert records with composite index on (Type, Source, IsResolved)
- **Triggered index**: Supports efficient retention cleanup queries
- **Migration 20260205074911_AddAlertEntity**: Schema version control via EF Core migrations
- **Switched to Migrate()**: Replaced EnsureCreated with proper migration support

### Custom Health Checks
- **DiskSpaceHealthCheck**: Monitors available disk space with 1GB threshold
  - Returns Degraded when below threshold
  - Includes available/total/percent data in result
  - Works on Windows (C:\simulator-data) and Linux (/mnt/simulator-data)
  - Helper method formats bytes to human-readable sizes

- **KafkaHealthCheck**: Monitors Kafka broker connectivity
  - TCP connection test with 5-second timeout
  - Returns Unhealthy when broker unreachable
  - Reads broker from Kafka:BootstrapServers configuration
  - Fast detection without AdminClient overhead

### AlertService Background Monitoring
- **IHostedService implementation**: Runs 30-second check timer
- **CheckDiskSpaceAsync**: Uses DiskSpaceHealthCheck, raises Warning alerts when degraded
- **CheckKafkaHealthAsync**: Uses KafkaHealthCheck, raises Critical alerts when unhealthy
- **CheckServerHealthAsync**: Queries HealthSamples for consecutive failures
  - 3 consecutive unhealthy samples = 15 seconds of failure
  - Raises Critical alerts for unhealthy servers
  - Automatically resolves when server recovers

- **RaiseAlertAsync**: Creates or updates alerts
  - Deduplicates by Type+Source (no duplicate unresolved alerts)
  - Broadcasts AlertTriggered event via SignalR
  - Logs warnings for new alerts

- **ResolveAlertsAsync**: Marks alerts resolved
  - Sets ResolvedAt timestamp
  - Broadcasts AlertResolved event via SignalR
  - Logs resolutions

- **CleanupOldAlertsAsync**: Removes alerts older than 7 days

### AlertHub SignalR Endpoint
- **GetActiveAlerts**: Returns unresolved alerts ordered by severity and time
- **GetAlertHistory**: Returns last 100 alerts (resolved + unresolved)
- **Real-time events**: AlertTriggered and AlertResolved broadcast to all clients
- **Connection logging**: Tracks client connections for debugging

### Service Registration
- **Health checks**: Registered DiskSpaceHealthCheck and KafkaHealthCheck as singletons
- **Health check builder**: Added checks with names "disk_space" and "kafka"
- **AlertService**: Registered as hosted service (starts automatically)
- **AlertHub**: Mapped to /hubs/alerts endpoint
- **Startup logs**: Added alert hub availability message

## How It Works

### Monitoring Lifecycle
1. **AlertService starts**: 5-second delay, then 30-second interval timer
2. **Each cycle**:
   - Check disk space → raise/resolve DiskSpace alerts
   - Check Kafka connectivity → raise/resolve KafkaConnection alerts
   - Check server health samples → raise/resolve ServerHealth alerts
   - Clean up alerts older than 7 days

3. **When condition detected**:
   - Check for existing unresolved alert (Type+Source)
   - Create new alert or update existing
   - Broadcast to SignalR clients

4. **When condition clears**:
   - Find unresolved alerts for Type+Source
   - Mark resolved, set ResolvedAt timestamp
   - Broadcast resolution to SignalR clients

### Health Check Integration
- **/health endpoint**: Aggregates all registered checks
- **DiskSpaceHealthCheck**: Independent check callable from AlertService or health endpoint
- **KafkaHealthCheck**: Same dual usage pattern
- **HealthCheckResult.Data**: Includes diagnostic information for troubleshooting

### Alert Deduplication
- **Composite key**: (Type, Source, IsResolved=false) ensures single active alert per source
- **Update pattern**: Existing alert message refreshed, not duplicated
- **Resolution pattern**: All matching unresolved alerts resolved together

## Deviations from Plan

None - plan executed exactly as written.

## Testing Notes

### Manual Verification
1. **Start Control API**: AlertService should start with 30s interval log
2. **Check /health endpoint**: Should return Healthy with disk_space and kafka checks
3. **Monitor logs**: Should see "Alert raised" or "Alert resolved" logs every 30s if conditions change
4. **SignalR client**: Connect to /hubs/alerts and call GetActiveAlerts/GetAlertHistory

### Health Check Testing
```bash
# Test health endpoint
curl http://localhost:5000/health

# Expected when all healthy:
# {"status":"Healthy","results":{"disk_space":{"status":"Healthy"},"kafka":{"status":"Healthy"}}}
```

### Triggering Alerts
- **Disk space**: Fill drive near 1GB threshold to trigger DiskSpace alert
- **Kafka**: Stop Kafka pod to trigger KafkaConnection alert
- **Server health**: Stop server pod to trigger ServerHealth alert after 3 failures

## Next Phase Readiness

**Phase 12 Plan 02 (Frontend Alert UI)** can proceed:
- AlertHub provides GetActiveAlerts and GetAlertHistory methods
- SignalR events (AlertTriggered, AlertResolved) available for real-time UI updates
- Alert severity levels defined for color coding
- Database schema supports efficient active alert queries

**Known dependencies:**
- Frontend needs useSignalR hook (already exists from Phase 7)
- Alert panel UI component needed
- Alert icon/badge for header notification

**No blockers.**

## Key Decisions Made

1. **Three-tier severity**: Info/Warning/Critical covers operational scenarios simply
2. **Alert deduplication**: Update existing prevents duplicate alerts for persistent issues
3. **3-failure threshold**: Balances quick detection (15s) with false positive avoidance
4. **1GB disk threshold**: Adequate for dev/test, easily adjustable via env var
5. **TCP health check**: Fast Kafka detection without AdminClient overhead
6. **7-day retention**: Manageable database size with adequate history
7. **Migrate over EnsureCreated**: Proper schema versioning for production evolution

## Performance Impact

**Minimal:**
- 30-second monitoring interval (low frequency)
- Health checks are fast (TCP timeout 5s, disk check <1ms)
- Alert table indexed for efficient queries
- SignalR broadcasts only on state changes (not every cycle)

**Database growth:**
- ~2-3 alerts per day typical (few condition changes)
- 7-day retention caps at ~20 alerts maximum
- Cleanup runs every 30s (no accumulation)

## What's Next

**Immediate (12-02):**
- Frontend AlertPanel component with active/history tabs
- SignalR subscription to AlertTriggered/AlertResolved events
- Header badge showing active alert count
- Color coding by severity (red=Critical, yellow=Warning, blue=Info)

**Future enhancements:**
- Email/webhook notifications for Critical alerts
- Alert acknowledgment (mark as seen without resolving)
- Configurable thresholds via API/UI
- Alert analytics (frequency, MTTR metrics)
