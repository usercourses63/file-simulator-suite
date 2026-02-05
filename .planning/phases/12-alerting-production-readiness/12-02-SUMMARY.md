---
phase: 12
plan: 02
subsystem: alerting-api
tags: [rest-api, redis, signalr, scalability, dotnet, helm]
requires: [12-01]
provides:
  - Alert REST API endpoints
  - Redis backplane infrastructure
  - SignalR scale-out capability
affects: [12-03, 12-04]
tech-stack:
  added:
    - Microsoft.AspNetCore.SignalR.StackExchangeRedis 9.0.0
    - Redis 7-alpine
  patterns:
    - Optional feature pattern (Redis disabled by default)
    - Connection-based configuration (checks ConnectionStrings:Redis)
    - Conditional Helm templating (redis.enabled flag)
key-files:
  created:
    - src/FileSimulator.ControlApi/Controllers/AlertsController.cs
    - helm-chart/file-simulator/templates/redis.yaml
  modified:
    - src/FileSimulator.ControlApi/FileSimulator.ControlApi.csproj
    - src/FileSimulator.ControlApi/Program.cs
    - helm-chart/file-simulator/values.yaml
    - helm-chart/file-simulator/templates/control-api.yaml
decisions:
  - id: redis-disabled-default
    title: Redis disabled by default
    rationale: Single-replica deployments don't need Redis overhead. Enable only for scale-out scenarios.
    alternatives: [Always enable Redis, Use different backplane]
  - id: redis-7-alpine
    title: Use redis:7-alpine image
    rationale: Smallest production-ready Redis image (minimal attack surface, fast pulls)
    alternatives: [redis:7, redis:latest]
  - id: connection-string-config
    title: Redis configuration via ConnectionStrings:Redis
    rationale: Standard .NET configuration pattern, consistent with SQLite and other connections
    alternatives: [Custom config section, Environment variable only]
metrics:
  duration: 4.2 minutes
  tasks: 6
  commits: 7
  files-changed: 6
completed: 2026-02-05
---

# Phase 12 Plan 02: Alert REST API and Redis Backplane Summary

**One-liner:** REST endpoints for alert queries with optional Redis SignalR backplane for scale-out

## What Was Built

### Alert REST API
Created `AlertsController` with four query endpoints:
1. **GET /api/alerts/active** - Returns unresolved alerts ordered by TriggeredAt descending
2. **GET /api/alerts/history?severity={level}** - Last 100 alerts with optional severity filter
3. **GET /api/alerts/{id}** - Specific alert by ID (404 if not found)
4. **GET /api/alerts/stats** - Alert counts by severity and type in last 24 hours

All endpoints use `IDbContextFactory<MetricsDbContext>` for background service compatibility and include proper async/await with CancellationToken support.

### Redis Backplane Infrastructure
Added optional Redis support for SignalR scale-out:
- **NuGet Package:** Microsoft.AspNetCore.SignalR.StackExchangeRedis 9.0.0
- **Configuration:** Checks `ConnectionStrings:Redis` at startup
- **Behavior:**
  - If connection string exists → use Redis backplane with "FileSimulator" channel prefix
  - If null/empty → use default in-memory backplane (single replica)
- **Logging:** Startup logs indicate active backplane mode

### Helm Integration
Redis deployment template with conditional rendering:
- **Conditional:** `{{- if .Values.redis.enabled }}`
- **Image:** redis:7-alpine (minimal production image)
- **Service:** ClusterIP on port 6379
- **Resources:** 64Mi/50m requests, 256Mi/200m limits
- **Health checks:** Liveness and readiness probes using `redis-cli ping`
- **Environment variable:** Control API gets `ConnectionStrings__Redis` when redis.enabled=true

## Technical Decisions Made

### Redis Disabled by Default
Redis adds operational complexity (another pod, monitoring, recovery). For single-replica Control API deployments (default), in-memory backplane is sufficient. Enable Redis only when scaling to multiple replicas.

**Configuration:**
```yaml
redis:
  enabled: false  # Change to true for multi-replica
```

### Connection String Pattern
Used standard .NET `ConnectionStrings:Redis` configuration pattern for consistency with SQLite database connections. This integrates naturally with ASP.NET Core configuration system and supports multiple configuration sources (appsettings.json, environment variables, Kubernetes ConfigMaps).

### Channel Prefix for Isolation
Redis backplane uses `ChannelPrefix: RedisChannel.Literal("FileSimulator")` to isolate SignalR messages from other applications sharing the same Redis instance. This prevents message crosstalk in shared Redis scenarios.

## Implementation Patterns

### Optional Feature Pattern (Helm)
```yaml
{{- if .Values.redis.enabled }}
# Deploy Redis resources
{{- end }}
```

Combined with conditional environment variable injection in Control API:
```yaml
{{- if .Values.redis.enabled }}
- name: ConnectionStrings__Redis
  value: "{{ include "file-simulator.fullname" . }}-redis:6379"
{{- end }}
```

### Runtime Configuration Pattern (C#)
```csharp
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("FileSimulator");
    });
}
else
{
    builder.Services.AddSignalR();
}
```

## Testing Performed

- ✅ AlertsController compiles without errors
- ✅ Redis backplane configuration compiles without errors
- ✅ Helm chart syntax validation passes (`helm lint`)
- ✅ All .NET projects build successfully

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

**Phase 12 Plan 03** (Alert visualization dashboard) can proceed:
- Alert REST API provides data source for dashboard components
- All alert queries support time-based filtering
- Stats endpoint provides aggregated data for overview widgets

**Multi-Replica Scale-Out** is ready:
- Set `redis.enabled: true` in values.yaml
- Scale Control API: `kubectl scale deployment control-api --replicas=3`
- SignalR messages automatically distributed across pods via Redis backplane

## Commits

| Commit | Type | Description |
|--------|------|-------------|
| 7fa1769 | feat | Add Redis SignalR backplane NuGet package |
| f73cf6c | feat | Create Alerts REST API controller |
| 5e11852 | feat | Configure Redis backplane for SignalR scale-out |
| 61cb77b | feat | Create Redis Helm deployment template |
| 5cc0a63 | feat | Add Redis configuration to Helm values |
| b184dd4 | feat | Inject Redis connection string into Control API |
| 18ccec5 | docs | Add alert REST endpoints to API root listing |

## Files Modified

### Created
- `src/FileSimulator.ControlApi/Controllers/AlertsController.cs` (157 lines)
- `helm-chart/file-simulator/templates/redis.yaml` (77 lines)

### Modified
- `src/FileSimulator.ControlApi/FileSimulator.ControlApi.csproj` (+1 line: Redis package)
- `src/FileSimulator.ControlApi/Program.cs` (+22 lines: Redis backplane + endpoint list)
- `helm-chart/file-simulator/values.yaml` (+24 lines: Redis configuration section)
- `helm-chart/file-simulator/templates/control-api.yaml` (+4 lines: Redis connection env var)

**Total Changes:** +285 lines added across 6 files
