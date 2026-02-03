---
phase: 09
plan: 03
subsystem: metrics-api
tags: ["rest-api", "signalr", "dto", "real-time"]
dependency-graph:
  requires: ["09-01", "09-02"]
  provides: ["metrics-rest-endpoints", "metrics-signalr-hub"]
  affects: ["10-*", "11-*"]
tech-stack:
  added: []
  patterns: ["REST API DTOs", "SignalR hub broadcasting"]
key-files:
  created:
    - src/FileSimulator.ControlApi/Models/MetricsQueryParams.cs
    - src/FileSimulator.ControlApi/Models/MetricsResponse.cs
    - src/FileSimulator.ControlApi/Controllers/MetricsController.cs
    - src/FileSimulator.ControlApi/Hubs/MetricsHub.cs
  modified:
    - src/FileSimulator.ControlApi/Services/ServerStatusBroadcaster.cs
    - src/FileSimulator.ControlApi/Program.cs
decisions:
  - id: "09-03-01"
    decision: "7-day limit on raw sample queries"
    reason: "Performance guard - large sample queries should use hourly rollups"
  - id: "09-03-02"
    decision: "ServerType filter applied post-query"
    reason: "Flexibility - can filter by protocol without index changes"
  - id: "09-03-03"
    decision: "MetricsSample event includes all servers in single broadcast"
    reason: "Efficiency - single SignalR message per health check cycle"
metrics:
  duration: "4 min"
  completed: "2026-02-03"
---

# Phase 9 Plan 03: Metrics REST API and SignalR Hub Summary

REST endpoints for querying historical metrics with real-time SignalR streaming to dashboards.

## What Was Built

### Task 1: Metrics DTOs and Query Parameters

Created request/response models for the metrics API:

**MetricsQueryParams.cs:**
- `ServerId`: Optional filter by server identifier
- `ServerType`: Optional filter by protocol (NAS, FTP, SFTP, etc.)
- `StartTime`/`EndTime`: Required time range (ISO 8601 format)

**MetricsResponse.cs:**
- `HealthSampleDto`: Raw sample with timestamp, health status, latency
- `HealthHourlyDto`: Hourly rollup with stats + computed `UptimePercent`
- `MetricsSamplesResponse`/`MetricsHourlyResponse`: Response wrappers

### Task 2: MetricsController with REST Endpoints

**GET /api/metrics/samples:**
- Returns raw health samples for time range
- Validates EndTime > StartTime
- Limits queries to 7 days max (use hourly for longer)
- Applies optional ServerType filter post-query

**GET /api/metrics/hourly:**
- Returns hourly aggregations for time range
- No time range limit (designed for long-range queries)
- Includes UptimePercent calculation

**GET /api/metrics/servers:**
- Lists all servers with available metrics
- Returns first/last sample timestamps and total count
- Uses raw GroupBy on HealthSamples table

### Task 3: MetricsHub and Real-Time Streaming

**MetricsHub (/hubs/metrics):**
- Simple SignalR hub for real-time metrics streaming
- Logs client connect/disconnect events

**ServerStatusBroadcaster Integration:**
- Injects `IHubContext<MetricsHub>`
- Broadcasts `MetricsSample` event after recording samples
- Event payload: `{ timestamp, samples: [{ serverId, serverType, isHealthy, latencyMs }] }`

**Program.cs Updates:**
- Added `app.MapHub<MetricsHub>("/hubs/metrics")`
- Updated endpoint list with all new routes
- Added startup log for metrics hub

## Key Code Patterns

### DTO Mapping Pattern

```csharp
var dtos = samples.Select(s => new HealthSampleDto
{
    Id = s.Id,
    Timestamp = s.Timestamp,
    ServerId = s.ServerId,
    ServerType = s.ServerType,
    IsHealthy = s.IsHealthy,
    LatencyMs = s.LatencyMs
}).ToList();
```

### Real-Time Broadcast Pattern

```csharp
await _metricsHubContext.Clients.All.SendAsync(
    "MetricsSample",
    new
    {
        timestamp = DateTime.UtcNow,
        samples = statuses.Select(s => new
        {
            serverId = s.Name,
            serverType = s.Protocol,
            isHealthy = s.IsHealthy,
            latencyMs = s.IsHealthy ? (int?)s.LatencyMs : null
        })
    },
    ct);
```

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 09-03-01 | 7-day limit on raw sample queries | At 5s intervals, 7 days = ~120,000 samples per server. Longer ranges should use hourly rollups for performance. |
| 09-03-02 | ServerType filter applied post-query | IMetricsService queries by ServerId only. Post-filtering allows ServerType without database changes. |
| 09-03-03 | Single MetricsSample broadcast per cycle | All servers included in one message reduces SignalR overhead vs per-server events. |

## Deviations from Plan

None - plan executed exactly as written.

## Commits

| Commit | Description |
|--------|-------------|
| 165f7fa | feat(09-03): add metrics DTOs and query parameters |
| 49f6a6b | feat(09-03): add MetricsController with REST endpoints |
| ec63ab6 | feat(09-03): add MetricsHub for real-time streaming |

## Files Changed

**Created:**
- `src/FileSimulator.ControlApi/Models/MetricsQueryParams.cs` - Query parameter model
- `src/FileSimulator.ControlApi/Models/MetricsResponse.cs` - Response DTOs
- `src/FileSimulator.ControlApi/Controllers/MetricsController.cs` - REST endpoints
- `src/FileSimulator.ControlApi/Hubs/MetricsHub.cs` - SignalR hub

**Modified:**
- `src/FileSimulator.ControlApi/Services/ServerStatusBroadcaster.cs` - MetricsHub injection + broadcast
- `src/FileSimulator.ControlApi/Program.cs` - Hub mapping + endpoint list

## Integration Points

**REST API Endpoints:**
- `GET /api/metrics/samples?startTime=&endTime=&serverId=&serverType=`
- `GET /api/metrics/hourly?startTime=&endTime=&serverId=&serverType=`
- `GET /api/metrics/servers`

**SignalR Hub:**
- `/hubs/metrics` - Connect to receive real-time `MetricsSample` events

**Event Format:**
```json
{
  "timestamp": "2026-02-03T12:34:56Z",
  "samples": [
    { "serverId": "nas-input-1", "serverType": "NAS", "isHealthy": true, "latencyMs": 12 },
    { "serverId": "ftp", "serverType": "FTP", "isHealthy": true, "latencyMs": 8 }
  ]
}
```

## Next Phase Readiness

Phase 9 plans 04-06 can proceed:
- 09-04: Dashboard integration with MetricsHub
- 09-05: Historical charts using /api/metrics/* endpoints
- 09-06: Uptime calculations using hourly rollups
