# Phase 9: Historical Metrics and Storage - Research

**Researched:** 2026-02-03
**Domain:** Time-series data persistence (SQLite + EF Core), charting (Recharts), sparklines (react-sparklines)
**Confidence:** HIGH

## Summary

This research investigates how to implement historical metrics storage with 7-day retention and trend visualization for the File Simulator Suite. The standard approach is **Entity Framework Core 9 with SQLite** for persistence, **Recharts 3.x** for full charting with zoom/pan, and **react-sparklines** for lightweight inline sparklines on server cards.

Key findings:

1. **SQLite with EF Core 9**: Use `IDbContextFactory<T>` pattern for background services (not direct injection). SQLite has limitations around DateTimeOffset (use DateTime UTC instead) and percentile calculations (requires manual SQL or SQLite 3.51+ extension).

2. **Recharts 3.x Zoom Implementation**: Recharts does not have native zoom/pan - it requires manual implementation using `ReferenceArea`, `onMouseDown`/`onMouseMove`/`onMouseUp` events, and state management for selection coordinates and axis domains.

3. **Sparklines**: Use `react-sparklines` library (already lightweight, SVG-based, responsive) for inline server card sparklines. It integrates cleanly with React 19 and requires minimal footprint.

4. **Background Services**: Use `IDbContextFactory<T>` for retention cleanup and rollup generation services. DbContext is NOT thread-safe and has Scoped lifetime, so cannot be directly injected into BackgroundService.

5. **P95 Calculation**: SQLite does not have built-in percentile functions in most builds. Calculate P95 in C# code during rollup generation, or use manual SQL row-numbering approach.

**Primary recommendation:** Use EF Core 9 with SQLite Code First migrations, store timestamps as DateTime (UTC), create composite indexes on (server_id, timestamp), implement zoom using Recharts ReferenceArea with manual state management, and use react-sparklines for server card mini-charts.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.x | SQLite database provider | Official Microsoft provider, mature, well-documented |
| Microsoft.EntityFrameworkCore.Design | 9.0.x | EF Core migrations tooling | Required for Code First migrations |
| Recharts | 3.6.x | Time-series charting | React-native, composable, official examples for zoom, 28k GitHub stars |
| react-sparklines | 1.7.x | Inline mini-charts | Lightweight (~10KB), SVG-based, responsive, no dependencies |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Microsoft.EntityFrameworkCore.Tools | 9.0.x | CLI migrations | `dotnet ef migrations add` commands |
| date-fns | 3.x | Date manipulation | Time range calculations, chart tick formatting |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| SQLite | TimescaleDB/PostgreSQL | Overkill for 13 servers; SQLite is embedded, no extra container |
| Recharts | ApexCharts | ApexCharts has built-in zoom but heavier; Recharts already established in ecosystem |
| react-sparklines | Recharts mini-LineChart | react-sparklines is purpose-built, smaller footprint |
| Manual rollups | Materialized views | SQLite doesn't support materialized views; manual approach gives full control |

**Installation:**

Backend:
```bash
cd src/FileSimulator.ControlApi
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0
```

Frontend:
```bash
cd src/dashboard
npm install recharts date-fns react-sparklines
npm install --save-dev @types/react-sparklines
```

## Architecture Patterns

### Recommended Project Structure

Backend additions:
```
src/FileSimulator.ControlApi/
├── Data/
│   ├── MetricsDbContext.cs          # EF Core DbContext
│   ├── Entities/
│   │   ├── HealthSample.cs          # Raw 5-second samples
│   │   └── HealthHourly.cs          # Hourly rollups
│   └── Migrations/                  # EF Core migrations
├── Services/
│   ├── MetricsService.cs            # Query/persist metrics
│   ├── MetricsRecordingService.cs   # Capture samples on health check
│   ├── RollupGenerationService.cs   # Background: hourly aggregation
│   └── RetentionCleanupService.cs   # Background: 7-day purge
└── Controllers/
    └── MetricsController.cs         # REST API for metrics queries
```

Frontend additions:
```
src/dashboard/src/
├── components/
│   ├── HistoryTab.tsx               # New Tab 3 - full charting
│   ├── LatencyChart.tsx             # Recharts LineChart with zoom
│   ├── TimeRangeSelector.tsx        # Preset buttons + dropdown
│   ├── ServerSparkline.tsx          # react-sparklines wrapper
│   └── ServerCard.tsx               # Enhanced with sparkline
├── hooks/
│   ├── useMetrics.ts                # Fetch historical metrics
│   └── useMetricsStream.ts          # SignalR streaming for live updates
└── types/
    └── metrics.ts                   # HealthSample, HealthHourly types
```

### Pattern 1: EF Core DbContext Factory in Background Services

**What:** Use `IDbContextFactory<T>` to create DbContext instances in BackgroundService, avoiding scoped lifetime conflicts

**When to use:** Always for background services that need database access

**Example:**
```csharp
// Source: Microsoft Learn - DbContext Lifetime, Configuration
public class RetentionCleanupService : BackgroundService
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;
    private readonly ILogger<RetentionCleanupService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public RetentionCleanupService(
        IDbContextFactory<MetricsDbContext> contextFactory,
        ILogger<RetentionCleanupService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(stoppingToken);

                var cutoff = DateTime.UtcNow.AddDays(-7);

                // Delete old samples
                var deletedSamples = await context.HealthSamples
                    .Where(s => s.Timestamp < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                // Delete old rollups
                var deletedRollups = await context.HealthHourly
                    .Where(r => r.HourStart < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                _logger.LogInformation(
                    "Retention cleanup: deleted {Samples} samples, {Rollups} rollups older than {Cutoff}",
                    deletedSamples, deletedRollups, cutoff);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention cleanup failed");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
```

### Pattern 2: Recharts Zoom with ReferenceArea and State

**What:** Implement click-and-drag zoom using ReferenceArea component and mouse events

**When to use:** Any Recharts chart requiring zoom functionality

**Example:**
```typescript
// Source: Recharts GitHub examples + community patterns
import { useState, useCallback } from 'react';
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid,
  Tooltip, Legend, ReferenceArea, ResponsiveContainer
} from 'recharts';

interface ZoomableChartProps {
  data: Array<{ timestamp: number; latencyMs: number; serverId: string }>;
}

interface ZoomState {
  left: number | 'dataMin';
  right: number | 'dataMax';
  refAreaLeft: number | null;
  refAreaRight: number | null;
  animation: boolean;
}

export function LatencyChart({ data }: ZoomableChartProps) {
  const [zoomState, setZoomState] = useState<ZoomState>({
    left: 'dataMin',
    right: 'dataMax',
    refAreaLeft: null,
    refAreaRight: null,
    animation: true
  });

  const handleMouseDown = useCallback((e: any) => {
    if (e?.activeLabel) {
      setZoomState(prev => ({ ...prev, refAreaLeft: e.activeLabel }));
    }
  }, []);

  const handleMouseMove = useCallback((e: any) => {
    if (zoomState.refAreaLeft && e?.activeLabel) {
      setZoomState(prev => ({ ...prev, refAreaRight: e.activeLabel }));
    }
  }, [zoomState.refAreaLeft]);

  const handleMouseUp = useCallback(() => {
    const { refAreaLeft, refAreaRight } = zoomState;

    if (refAreaLeft === refAreaRight || refAreaRight === null) {
      setZoomState(prev => ({
        ...prev,
        refAreaLeft: null,
        refAreaRight: null
      }));
      return;
    }

    // Ensure left < right
    const [left, right] = refAreaLeft! < refAreaRight
      ? [refAreaLeft, refAreaRight]
      : [refAreaRight, refAreaLeft];

    setZoomState({
      left: left!,
      right: right!,
      refAreaLeft: null,
      refAreaRight: null,
      animation: true
    });
  }, [zoomState.refAreaLeft, zoomState.refAreaRight]);

  const handleZoomOut = useCallback(() => {
    setZoomState({
      left: 'dataMin',
      right: 'dataMax',
      refAreaLeft: null,
      refAreaRight: null,
      animation: true
    });
  }, []);

  return (
    <div className="latency-chart">
      <button onClick={handleZoomOut} disabled={zoomState.left === 'dataMin'}>
        Reset Zoom
      </button>
      <ResponsiveContainer width="100%" height={400}>
        <LineChart
          data={data}
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
        >
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis
            dataKey="timestamp"
            domain={[zoomState.left, zoomState.right]}
            type="number"
            tickFormatter={(ts) => new Date(ts).toLocaleTimeString()}
          />
          <YAxis domain={['auto', 'auto']} unit="ms" />
          <Tooltip
            labelFormatter={(ts) => new Date(ts).toLocaleString()}
            formatter={(value: number) => [`${value}ms`, 'Latency']}
          />
          <Legend />
          <Line
            type="monotone"
            dataKey="latencyMs"
            stroke="#8884d8"
            dot={false}
            animationDuration={zoomState.animation ? 500 : 0}
          />
          {zoomState.refAreaLeft && zoomState.refAreaRight && (
            <ReferenceArea
              x1={zoomState.refAreaLeft}
              x2={zoomState.refAreaRight}
              strokeOpacity={0.3}
              fill="#8884d8"
              fillOpacity={0.3}
            />
          )}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
```

### Pattern 3: Sparkline Integration in Server Card

**What:** Embed mini sparkline showing last hour of latency data in server card

**When to use:** Server cards on the Servers tab

**Example:**
```typescript
// Source: react-sparklines documentation
import { Sparklines, SparklinesLine, SparklinesSpots, SparklinesReferenceLine } from 'react-sparklines';

interface ServerSparklineProps {
  data: number[];  // Last 60 minutes of latency values (or null for unhealthy)
  isHealthy: boolean;
  onClick: () => void;
}

export function ServerSparkline({ data, isHealthy, onClick }: ServerSparklineProps) {
  // Convert nulls to 0 for display, track unhealthy periods
  const displayData = data.map(v => v ?? 0);
  const color = isHealthy ? '#22c55e' : '#ef4444';

  return (
    <div className="server-sparkline" onClick={onClick} title="Click to view history">
      <Sparklines data={displayData} width={100} height={20} margin={2}>
        <SparklinesLine color={color} style={{ strokeWidth: 1.5, fill: 'none' }} />
        <SparklinesSpots size={2} style={{ fill: color }} />
        <SparklinesReferenceLine type="mean" style={{ stroke: '#999', strokeDasharray: '2,2' }} />
      </Sparklines>
    </div>
  );
}
```

### Pattern 4: EF Core Entity Configuration with Indexes

**What:** Configure entities with proper indexes for time-series queries

**When to use:** Always for time-series data tables

**Example:**
```csharp
// Source: EF Core best practices + SQLite recommendations
public class MetricsDbContext : DbContext
{
    public MetricsDbContext(DbContextOptions<MetricsDbContext> options)
        : base(options) { }

    public DbSet<HealthSample> HealthSamples => Set<HealthSample>();
    public DbSet<HealthHourly> HealthHourly => Set<HealthHourly>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // HealthSample entity
        modelBuilder.Entity<HealthSample>(entity =>
        {
            entity.ToTable("health_samples");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Timestamp)
                .IsRequired()
                .HasColumnName("timestamp");

            entity.Property(e => e.ServerId)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("server_id");

            entity.Property(e => e.ServerType)
                .IsRequired()
                .HasMaxLength(10)
                .HasColumnName("server_type");

            entity.Property(e => e.IsHealthy)
                .IsRequired()
                .HasColumnName("is_healthy");

            entity.Property(e => e.LatencyMs)
                .HasColumnName("latency_ms");

            // Composite index for time-range queries per server
            entity.HasIndex(e => new { e.ServerId, e.Timestamp })
                .HasDatabaseName("ix_health_samples_server_timestamp");

            // Index for retention cleanup queries
            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("ix_health_samples_timestamp");
        });

        // HealthHourly entity
        modelBuilder.Entity<HealthHourly>(entity =>
        {
            entity.ToTable("health_hourly");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.HourStart)
                .IsRequired()
                .HasColumnName("hour_start");

            entity.Property(e => e.ServerId)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("server_id");

            // Composite index for time-range queries
            entity.HasIndex(e => new { e.ServerId, e.HourStart })
                .HasDatabaseName("ix_health_hourly_server_hour");
        });
    }
}
```

### Pattern 5: P95 Calculation in C# (Rollup Service)

**What:** Calculate percentiles in C# during rollup generation since SQLite lacks native percentile functions

**When to use:** Generating hourly rollups from raw samples

**Example:**
```csharp
// Source: Manual implementation - SQLite percentile extension not available in most builds
private static double? CalculatePercentile(List<double> sortedValues, double percentile)
{
    if (sortedValues.Count == 0) return null;
    if (sortedValues.Count == 1) return sortedValues[0];

    double index = (percentile / 100.0) * (sortedValues.Count - 1);
    int lower = (int)Math.Floor(index);
    int upper = (int)Math.Ceiling(index);

    if (lower == upper)
        return sortedValues[lower];

    // Linear interpolation
    double fraction = index - lower;
    return sortedValues[lower] + (sortedValues[upper] - sortedValues[lower]) * fraction;
}

public async Task GenerateHourlyRollupAsync(DateTime hourStart, CancellationToken ct)
{
    await using var context = await _contextFactory.CreateDbContextAsync(ct);

    var hourEnd = hourStart.AddHours(1);

    // Get samples for this hour, grouped by server
    var samples = await context.HealthSamples
        .Where(s => s.Timestamp >= hourStart && s.Timestamp < hourEnd)
        .GroupBy(s => new { s.ServerId, s.ServerType })
        .ToListAsync(ct);

    foreach (var group in samples)
    {
        var latencies = group
            .Where(s => s.LatencyMs.HasValue)
            .Select(s => s.LatencyMs!.Value)
            .OrderBy(l => l)
            .ToList();

        var rollup = new HealthHourly
        {
            HourStart = hourStart,
            ServerId = group.Key.ServerId,
            ServerType = group.Key.ServerType,
            SampleCount = group.Count(),
            HealthyCount = group.Count(s => s.IsHealthy),
            AvgLatencyMs = latencies.Count > 0 ? latencies.Average() : null,
            MinLatencyMs = latencies.Count > 0 ? latencies.Min() : null,
            MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : null,
            P95LatencyMs = CalculatePercentile(latencies, 95)
        };

        context.HealthHourly.Add(rollup);
    }

    await context.SaveChangesAsync(ct);
}
```

### Anti-Patterns to Avoid

- **Direct DbContext injection in BackgroundService:** DbContext has Scoped lifetime; use IDbContextFactory or IServiceScopeFactory instead
- **Using DateTimeOffset with SQLite:** SQLite cannot order/compare DateTimeOffset; use DateTime (UTC) instead
- **Not creating indexes on timestamp columns:** Time-range queries will table-scan without proper indexes
- **Storing timestamps as strings:** Use INTEGER (Unix timestamp) or TEXT in ISO format, not localized strings
- **Expecting native Recharts zoom:** Recharts requires manual implementation with ReferenceArea and state
- **Large batch deletes without batching:** Use ExecuteDeleteAsync for efficient bulk deletes in EF Core 9

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| DbContext in background services | Manual scope management | IDbContextFactory<T> | Thread-safe, proper lifetime, pooling support |
| Chart zoom functionality | Custom SVG manipulation | Recharts ReferenceArea + events | Proven pattern, handles edge cases |
| Mini sparkline charts | Custom SVG drawing | react-sparklines | 10KB, responsive, reference lines built-in |
| Bulk data deletion | Loop with single deletes | ExecuteDeleteAsync() | Single SQL DELETE, no entity tracking |
| Time range calculations | Manual Date arithmetic | date-fns library | Handles edge cases, timezone-aware |
| Database migrations | Manual SQL scripts | EF Core Migrations | Version controlled, reversible, typed |

**Key insight:** SQLite + EF Core is a well-tested combination for embedded databases. The main pitfalls are around datetime handling (use UTC) and background service lifetime management (use factory pattern).

## Common Pitfalls

### Pitfall 1: Scoped DbContext in Singleton BackgroundService

**What goes wrong:** InvalidOperationException: "Cannot consume scoped service 'DbContext' from singleton 'BackgroundService'"

**Why it happens:** BackgroundService is registered as Singleton; DbContext is Scoped by default

**How to avoid:**
```csharp
// WRONG
public class MyService : BackgroundService
{
    private readonly MetricsDbContext _context; // Never inject directly
}

// CORRECT
public class MyService : BackgroundService
{
    private readonly IDbContextFactory<MetricsDbContext> _contextFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(stoppingToken);
        // Use context
    }
}
```

**Warning signs:** Exception at startup, or intermittent database errors

### Pitfall 2: DateTimeOffset with SQLite Ordering

**What goes wrong:** "SQLite cannot order by expressions of type 'DateTimeOffset'" exception

**Why it happens:** SQLite stores DateTimeOffset as TEXT; comparison requires type conversion

**How to avoid:**
```csharp
// Use DateTime instead, always UTC
public class HealthSample
{
    public DateTime Timestamp { get; set; } // UTC, not DateTimeOffset
}

// When saving
sample.Timestamp = DateTime.UtcNow;

// When querying - no issues
var recent = await context.HealthSamples
    .Where(s => s.Timestamp > cutoff)
    .OrderByDescending(s => s.Timestamp)
    .ToListAsync();
```

**Warning signs:** Runtime exception on ORDER BY or comparison queries

### Pitfall 3: Recharts ReferenceArea Not Rendering

**What goes wrong:** Zoom selection area doesn't appear during drag

**Why it happens:** ReferenceArea requires both x1 and x2 to be set, and data domain must match

**How to avoid:**
```typescript
// Conditional rendering only when both coordinates set
{refAreaLeft !== null && refAreaRight !== null && (
  <ReferenceArea
    x1={refAreaLeft}
    x2={refAreaRight}
    strokeOpacity={0.3}
  />
)}

// Ensure XAxis domain type matches data
<XAxis
  dataKey="timestamp"
  type="number"  // Must match timestamp number type
  domain={[left, right]}
/>
```

**Warning signs:** Drag gesture doesn't show selection highlight

### Pitfall 4: Missing Index on Timestamp Column

**What goes wrong:** Queries become slow as data grows (table scan)

**Why it happens:** SQLite performs full table scan without appropriate indexes

**How to avoid:**
```csharp
// In OnModelCreating
entity.HasIndex(e => e.Timestamp)
    .HasDatabaseName("ix_health_samples_timestamp");

// For server-specific queries, composite index
entity.HasIndex(e => new { e.ServerId, e.Timestamp })
    .HasDatabaseName("ix_health_samples_server_timestamp");
```

**Warning signs:** Query time grows linearly with data size; slow retention cleanup

### Pitfall 5: ResponsiveContainer Parent Height Issue

**What goes wrong:** Recharts chart renders with zero height

**Why it happens:** ResponsiveContainer needs parent with defined height; percentage height doesn't work without explicit container

**How to avoid:**
```tsx
// WRONG - chart won't render
<div>
  <ResponsiveContainer width="100%" height="100%">
    <LineChart data={data}>...</LineChart>
  </ResponsiveContainer>
</div>

// CORRECT - explicit height
<div style={{ height: '400px' }}>
  <ResponsiveContainer width="100%" height="100%">
    <LineChart data={data}>...</LineChart>
  </ResponsiveContainer>
</div>

// OR - use fixed height on ResponsiveContainer
<ResponsiveContainer width="100%" height={400}>
  <LineChart data={data}>...</LineChart>
</ResponsiveContainer>
```

**Warning signs:** Chart area is blank, no errors in console

## Code Examples

Verified patterns from official sources:

### Entity Definitions

```csharp
// Source: EF Core documentation patterns
public class HealthSample
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }  // UTC
    public string ServerId { get; set; } = default!;
    public string ServerType { get; set; } = default!;
    public bool IsHealthy { get; set; }
    public double? LatencyMs { get; set; }
}

public class HealthHourly
{
    public int Id { get; set; }
    public DateTime HourStart { get; set; }  // UTC, truncated to hour
    public string ServerId { get; set; } = default!;
    public string ServerType { get; set; } = default!;
    public int SampleCount { get; set; }
    public int HealthyCount { get; set; }
    public double? AvgLatencyMs { get; set; }
    public double? MinLatencyMs { get; set; }
    public double? MaxLatencyMs { get; set; }
    public double? P95LatencyMs { get; set; }
}
```

### Service Registration

```csharp
// Source: Microsoft Learn - EF Core configuration
// In Program.cs
builder.Services.AddDbContextFactory<MetricsDbContext>(options =>
    options.UseSqlite("Data Source=/mnt/control-data/metrics.db"));

// Background services use factory
builder.Services.AddHostedService<RetentionCleanupService>();
builder.Services.AddHostedService<RollupGenerationService>();

// Scoped services can use direct context
builder.Services.AddScoped<IMetricsService, MetricsService>();
```

### TypeScript Types for Metrics

```typescript
// Source: Matching backend models
export interface HealthSample {
  id: number;
  timestamp: string;  // ISO 8601
  serverId: string;
  serverType: string;
  isHealthy: boolean;
  latencyMs: number | null;
}

export interface HealthHourly {
  id: number;
  hourStart: string;  // ISO 8601
  serverId: string;
  serverType: string;
  sampleCount: number;
  healthyCount: number;
  avgLatencyMs: number | null;
  minLatencyMs: number | null;
  maxLatencyMs: number | null;
  p95LatencyMs: number | null;
}

export interface MetricsQueryParams {
  serverId?: string;
  serverType?: string;
  startTime: string;  // ISO 8601
  endTime: string;    // ISO 8601
  resolution: 'raw' | 'hourly';
}
```

### Time Range Selector Component

```typescript
// Source: Custom component following CONTEXT.md decisions
import { useState } from 'react';

interface TimeRangeSelectorProps {
  onRangeChange: (startTime: Date, endTime: Date) => void;
  defaultRange: string;
}

const PRESETS = [
  { label: '1h', value: '1h' },
  { label: '6h', value: '6h' },
  { label: '24h', value: '24h' },
  { label: '7d', value: '7d' }
];

const DROPDOWN_OPTIONS = [
  { label: '15 minutes', value: '15m' },
  { label: '30 minutes', value: '30m' },
  { label: '1 hour', value: '1h' },
  { label: '2 hours', value: '2h' },
  { label: '6 hours', value: '6h' },
  { label: '12 hours', value: '12h' },
  { label: '24 hours', value: '24h' },
  { label: '3 days', value: '3d' },
  { label: '7 days', value: '7d' }
];

function parseRange(value: string): { startTime: Date; endTime: Date } {
  const endTime = new Date();
  const startTime = new Date();

  const match = value.match(/^(\d+)([mhd])$/);
  if (!match) throw new Error(`Invalid range: ${value}`);

  const [, num, unit] = match;
  const amount = parseInt(num, 10);

  switch (unit) {
    case 'm': startTime.setMinutes(startTime.getMinutes() - amount); break;
    case 'h': startTime.setHours(startTime.getHours() - amount); break;
    case 'd': startTime.setDate(startTime.getDate() - amount); break;
  }

  return { startTime, endTime };
}

export function TimeRangeSelector({ onRangeChange, defaultRange }: TimeRangeSelectorProps) {
  const [selectedRange, setSelectedRange] = useState(defaultRange);
  const [autoRefresh, setAutoRefresh] = useState(false);

  const handleRangeSelect = (value: string) => {
    setSelectedRange(value);
    const { startTime, endTime } = parseRange(value);
    onRangeChange(startTime, endTime);
  };

  return (
    <div className="time-range-selector">
      <div className="preset-buttons">
        {PRESETS.map(preset => (
          <button
            key={preset.value}
            className={selectedRange === preset.value ? 'active' : ''}
            onClick={() => handleRangeSelect(preset.value)}
          >
            {preset.label}
          </button>
        ))}
      </div>
      <select
        value={selectedRange}
        onChange={(e) => handleRangeSelect(e.target.value)}
      >
        {DROPDOWN_OPTIONS.map(opt => (
          <option key={opt.value} value={opt.value}>{opt.label}</option>
        ))}
      </select>
      <label className="auto-refresh">
        <input
          type="checkbox"
          checked={autoRefresh}
          onChange={(e) => setAutoRefresh(e.target.checked)}
        />
        Auto-refresh (30s)
      </label>
    </div>
  );
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| IServiceScopeFactory for DbContext | IDbContextFactory<T> | EF Core 5.0 (2020) | Cleaner API, pooling support |
| Manual SQL for bulk deletes | ExecuteDeleteAsync() | EF Core 7.0 (2022) | Single statement, no tracking |
| Recharts 2.x | Recharts 3.x | 2024 | New state management, better hooks |
| No migration locking | __EFMigrationsLock table | EF Core 9.0 (2024) | Prevents concurrent migration issues |
| DateTimeOffset in SQLite | DateTime (UTC) | Always | SQLite limitation, no comparison support |

**Deprecated/outdated:**
- recharts-scale (removed in 3.0): Scale utilities now exported from main recharts package
- react-smooth (removed in 3.0): Animations now maintained within recharts
- Manual scope creation for DbContext: Use IDbContextFactory instead

## Open Questions

Things that couldn't be fully resolved:

1. **SQLite percentile extension availability**
   - What we know: `percentile()` function available in SQLite 3.51.0+ (Nov 2024), but requires compile-time flag
   - What's unclear: Whether Minikube/Alpine-based images include SQLite 3.51+ with extension enabled
   - Recommendation: Calculate P95 in C# code during rollup generation (safe, portable)

2. **Recharts legend click conflict with zoom events**
   - What we know: Known issue - legend click events may conflict with chart onMouseUp
   - What's unclear: Whether Recharts 3.x resolved this regression
   - Recommendation: Test legend toggling with zoom; if conflicts, use custom legend component outside chart

3. **Optimal raw sample retention vs rollup granularity**
   - What we know: CONTEXT.md specifies 7 days raw + 7 days hourly rollups
   - What's unclear: Whether raw samples could be retained shorter (e.g., 48h) with rollups for longer history
   - Recommendation: Start with 7 days raw as specified; can optimize later if storage becomes concern

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - DbContext Lifetime, Configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/) - DbContextFactory patterns
- [Microsoft Learn - SQLite EF Core Limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations) - DateTimeOffset, migrations
- [Microsoft Learn - EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying) - Indexing, bulk operations
- [Recharts GitHub Wiki - 3.0 Migration Guide](https://github.com/recharts/recharts/wiki/3.0-migration-guide) - Breaking changes, new features
- [react-sparklines GitHub](https://github.com/borisyankov/react-sparklines) - API documentation
- [SQLite Percentile Extension](https://sqlite.org/percentile.html) - P95 calculation options

### Secondary (MEDIUM confidence)
- [C# Corner - DbContext in BackgroundService](https://www.c-sharpcorner.com/article/inject-a-dbcontext-instance-into-backgroundservice-in-net-core/) - Factory pattern examples
- [Code Maze - EF Core Best Practices](https://code-maze.com/entity-framework-core-best-practices/) - Indexing, performance
- [Recharts Examples - HighlightAndZoomLineChart](https://recharts.github.io/en-US/examples/HighlightAndZoomLineChart/) - Zoom implementation
- [Medium - Recharts Zoomable Line Chart](https://medium.com/towardsdev/recharts-zoomable-line-chart-with-custom-clickable-legend-ecc7ddf66edb) - Custom zoom patterns
- [Recharts GitHub Issue #710](https://github.com/recharts/recharts/issues/710) - Zoom/pan discussion

### Tertiary (LOW confidence)
- [MoldStud - SQLite Time-Series Best Practices](https://moldstud.com/articles/p-handling-time-series-data-in-sqlite-best-practices) - Partitioning strategies
- [Snyk - Recharts Examples](https://snyk.io/advisor/npm-package/recharts/example) - Community patterns

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official Microsoft packages, well-documented libraries
- Architecture: HIGH - Patterns from official docs, verified with existing project structure
- Pitfalls: HIGH - Common issues documented in official GitHub issues and Microsoft Learn

**Research date:** 2026-02-03
**Valid until:** 30 days (stable technology stack, EF Core 9 released Nov 2024)

**Notes for planner:**
- Existing project has React 19, Vite 6, TypeScript, SignalR client already configured
- Backend is ASP.NET Core 9.0 with SignalR hub infrastructure (StatusHub, FileEventsHub exist)
- Control API already mounts `/mnt/simulator-data` - need separate mount or path for SQLite database
- CONTEXT.md specifies SQLite at `/mnt/control-data/metrics.db` - requires new PVC or hostPath
- SignalR infrastructure from Phase 8 can be reused for streaming new samples to dashboard
- ServerStatusBroadcaster (Phase 7) is the integration point for capturing health samples
