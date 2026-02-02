# Architecture Research

**Domain:** Real-time monitoring and control platform for Kubernetes infrastructure
**Researched:** 2026-02-02
**Confidence:** HIGH

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Frontend Layer (React SPA)                            │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐ │
│  │  Dashboard   │  │  File Ops    │  │  Server Mgmt │  │  Config     │ │
│  │  (Metrics)   │  │  (Upload)    │  │  (Add/Del)   │  │  (Import)   │ │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬──────┘ │
│         │                 │                 │                 │         │
│         └─────────────────┴─────────────────┴─────────────────┘         │
│                              │                                           │
│                    REST API + SignalR WebSocket                          │
├─────────────────────────────────────────────────────────────────────────┤
│                    Backend Layer (ASP.NET Core)                          │
├─────────────────────────────────────────────────────────────────────────┤
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐               │
│  │   API         │  │   SignalR Hub │  │   Background  │               │
│  │   Controllers │  │   (Real-time) │  │   Workers     │               │
│  └───────┬───────┘  └───────┬───────┘  └───────┬───────┘               │
│          │                  │                  │                         │
│          └──────────────────┴──────────────────┘                         │
│                              │                                           │
│         ┌────────────────────┼────────────────────┐                     │
│         │                    │                    │                     │
│  ┌──────▼──────┐  ┌──────────▼────────┐  ┌──────▼──────┐              │
│  │ K8s API     │  │  File Watcher     │  │  Health     │              │
│  │ Client      │  │  (Windows Dir)    │  │  Checker    │              │
│  │ (Dynamic)   │  │                   │  │  Service    │              │
│  └──────┬──────┘  └──────────┬────────┘  └──────┬──────┘              │
├─────────┼─────────────────────┼─────────────────┼──────────────────────┤
│         │                     │                 │                       │
│  ┌──────▼──────┐       ┌──────▼──────┐   ┌─────▼──────┐               │
│  │ Time-Series │       │   Kafka     │   │   Redis    │               │
│  │ DB (SQLite/ │       │   Broker    │   │  Backplane │               │
│  │ Prometheus) │       │             │   │  (SignalR) │               │
│  └─────────────┘       └──────┬──────┘   └────────────┘               │
├────────────────────────────────┼────────────────────────────────────────┤
│                    Integration Layer                                     │
├────────────────────────────────┼────────────────────────────────────────┤
│  Kubernetes API Server         │                                         │
│         │                      │                                         │
│  ┌──────▼────────────────────────────────────────────────────┐          │
│  │     Existing Protocol Servers (Helm Chart)                 │          │
│  ├────────────────────────────────────────────────────────────┤          │
│  │  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │          │
│  │  │ FTP  │ │ SFTP │ │ HTTP │ │ S3   │ │ SMB  │ │ NAS  │  │          │
│  │  │ x1-N │ │ x1-N │ │ WebDV│ │ MinIO│ │ Samba│ │ x1-7 │  │          │
│  │  └──┬───┘ └──┬───┘ └──┬───┘ └──┬───┘ └──┬───┘ └──┬───┘  │          │
│  │     └────────┴────────┴────────┴────────┴────────┘       │          │
│  │                Shared PVC (hostPath)                       │          │
│  └────────────────────────────────────────────────────────────┘          │
└─────────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                Windows Host: C:\simulator-data
```

## Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **React Dashboard** | User interface for monitoring and control | React 18+ with Vite, Material-UI/Ant Design |
| **SignalR Hub** | Real-time bi-directional communication | ASP.NET Core SignalR with Redis backplane |
| **API Controllers** | REST endpoints for CRUD operations | ASP.NET Core Minimal API or Controllers |
| **Kubernetes Client** | Dynamic resource management (CRUD pods) | KubernetesClient NuGet (official C# client) |
| **Health Checker** | Protocol connectivity and status monitoring | Background service with Quartz.NET scheduling |
| **File Watcher** | Detect Windows directory changes | FileSystemWatcher with debouncing |
| **Time-Series DB** | Historical metrics storage | SQLite (dev) or Prometheus (production) |
| **Kafka Broker** | Event streaming and pub/sub | Strimzi (single broker, KRaft mode) |
| **Redis Backplane** | SignalR scale-out across pods | Redis (in-memory, minimal persistence) |
| **Protocol Servers** | Existing FTP/SFTP/NAS/HTTP/S3/SMB services | Docker images via Helm chart (already deployed) |

## Recommended Project Structure

```
src/
├── FileSimulator.Dashboard/          # React SPA
│   ├── src/
│   │   ├── components/               # UI components
│   │   │   ├── dashboard/            # Metrics & charts
│   │   │   ├── file-browser/         # File operations
│   │   │   ├── server-management/    # Add/remove servers
│   │   │   └── shared/               # Reusable components
│   │   ├── hooks/                    # Custom React hooks
│   │   │   ├── useSignalR.ts         # WebSocket connection
│   │   │   ├── useMetrics.ts         # Time-series data
│   │   │   └── useFileWatcher.ts     # Real-time file events
│   │   ├── services/                 # API clients
│   │   │   ├── apiClient.ts          # REST API wrapper
│   │   │   └── signalRService.ts     # SignalR connection manager
│   │   ├── store/                    # State management
│   │   │   ├── useServerStore.ts     # Zustand store for servers
│   │   │   ├── useMetricsStore.ts    # Zustand store for metrics
│   │   │   └── useEventStore.ts      # Zustand store for events
│   │   └── App.tsx                   # Root component
│   ├── package.json
│   ├── vite.config.ts
│   └── Dockerfile                    # Multi-stage build
│
├── FileSimulator.ControlPlane/       # ASP.NET Core Backend
│   ├── Controllers/
│   │   ├── ServersController.cs      # CRUD for protocol servers
│   │   ├── FilesController.cs        # File operations
│   │   ├── MetricsController.cs      # Historical metrics
│   │   └── ConfigController.cs       # Import/export config
│   ├── Hubs/
│   │   └── MonitoringHub.cs          # SignalR hub
│   ├── Services/
│   │   ├── IKubernetesService.cs     # Kubernetes API abstraction
│   │   ├── KubernetesService.cs      # Create/delete pods dynamically
│   │   ├── IHealthCheckService.cs    # Protocol health checks
│   │   ├── HealthCheckService.cs     # Poll all servers
│   │   ├── IFileWatcherService.cs    # Windows directory monitoring
│   │   ├── FileWatcherService.cs     # FileSystemWatcher wrapper
│   │   ├── IMetricsService.cs        # Time-series operations
│   │   └── MetricsService.cs         # Write/query metrics
│   ├── BackgroundServices/
│   │   ├── HealthMonitorWorker.cs    # Background health checks
│   │   └── MetricsCollectorWorker.cs # Periodic metrics collection
│   ├── Models/
│   │   ├── ServerDefinition.cs       # Protocol server config
│   │   ├── HealthStatus.cs           # Health check result
│   │   ├── FileEvent.cs              # File change event
│   │   └── Metric.cs                 # Time-series data point
│   ├── Data/
│   │   ├── MetricsDbContext.cs       # EF Core context
│   │   └── Migrations/               # Database migrations
│   ├── Program.cs                    # Application entry point
│   ├── appsettings.json              # Configuration
│   └── Dockerfile                    # Container image
│
├── FileSimulator.Client/             # Existing .NET client library
│   └── [Existing file protocol services]
│
└── helm-chart/
    └── file-simulator/               # Existing Helm chart
        ├── charts/                   # Subchart for new components
        │   ├── control-plane/        # Backend + Dashboard
        │   │   ├── templates/
        │   │   │   ├── backend-deployment.yaml
        │   │   │   ├── backend-service.yaml
        │   │   │   ├── dashboard-deployment.yaml
        │   │   │   ├── dashboard-service.yaml
        │   │   │   ├── redis-deployment.yaml
        │   │   │   ├── kafka-deployment.yaml
        │   │   │   └── ingress.yaml
        │   │   ├── Chart.yaml
        │   │   └── values.yaml
        │   └── [Future subcharts]
        ├── templates/                # Existing protocol servers
        │   ├── ftp.yaml
        │   ├── sftp.yaml
        │   ├── nas.yaml
        │   └── [Other existing templates]
        ├── Chart.yaml                # Umbrella chart metadata
        └── values.yaml               # Global configuration
```

### Structure Rationale

- **Umbrella Chart Pattern**: Single Helm chart contains all components; enables atomic deployments and rollbacks
- **Subchart Isolation**: Control plane components (dashboard, backend, Kafka, Redis) in dedicated subchart; can be enabled/disabled via `controlPlane.enabled` flag
- **Co-located Frontend/Backend**: Both in same Kubernetes namespace for simplified networking and shared ConfigMaps
- **Zustand for State**: Lightweight, performant, no provider wrapping needed; ideal for real-time dashboard updates
- **Separation of Concerns**: React hooks isolate SignalR logic from UI components; services abstract API communication

## Architectural Patterns

### Pattern 1: SignalR Hub with Redis Backplane

**What:** Real-time bi-directional communication between backend and multiple browser clients, scaled across multiple backend pods using Redis as message broker.

**When to use:** Real-time dashboards with multiple concurrent users and horizontally scaled backend (2+ pods).

**Trade-offs:**
- **Pros**: Supports multiple backend pods, no sticky sessions needed with WebSocket-only mode, low latency (<100ms)
- **Cons**: Requires Redis infrastructure, adds complexity for single-pod deployments, Redis memory consumption scales with message throughput

**Example:**
```csharp
// Startup.cs - Configure SignalR with Redis backplane
services.AddSignalR()
    .AddStackExchangeRedis(options =>
    {
        options.Configuration.EndPoints.Add("redis-svc:6379");
        options.Configuration.ChannelPrefix = RedisChannel.Literal("simulator:");
    });

// MonitoringHub.cs - Broadcast to all connected clients
public class MonitoringHub : Hub
{
    public async Task BroadcastHealthStatus(HealthStatus status)
    {
        // Redis ensures all pods receive this message
        await Clients.All.SendAsync("HealthStatusChanged", status);
    }
}

// Health check service triggers broadcast
public class HealthCheckService : BackgroundService
{
    private readonly IHubContext<MonitoringHub> _hubContext;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var statuses = await CheckAllServersAsync(ct);
            foreach (var status in statuses)
            {
                // Broadcast to all connected clients across all pods
                await _hubContext.Clients.All.SendAsync(
                    "HealthStatusChanged", status, ct);
            }
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        }
    }
}
```

**Minikube Development Workaround:**
For single-pod development, Redis backplane is optional. SignalR works without Redis when only one backend pod exists. Enable Redis in production-like testing scenarios.

### Pattern 2: Kubernetes Dynamic Resource Management

**What:** Programmatically create/delete Kubernetes resources (Deployments, Services, PVCs) at runtime using the .NET Kubernetes client library.

**When to use:** Control plane features like "Add FTP Server" button that provisions new infrastructure on demand.

**Trade-offs:**
- **Pros**: Self-service infrastructure, no manual kubectl commands, validates before applying
- **Cons**: Requires elevated RBAC permissions, complex error handling, must track created resources for cleanup

**Example:**
```csharp
// KubernetesService.cs
public class KubernetesService : IKubernetesService
{
    private readonly IKubernetes _k8sClient;
    private readonly string _namespace = "file-simulator";

    public KubernetesService()
    {
        var config = KubernetesClientConfiguration.InClusterConfig();
        _k8sClient = new Kubernetes(config);
    }

    public async Task<string> CreateFtpServerAsync(
        FtpServerConfig config, CancellationToken ct)
    {
        var name = $"ftp-{Guid.NewGuid().ToString()[..8]}";

        // Create Deployment
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = name,
                Labels = new Dictionary<string, string>
                {
                    ["app"] = "file-simulator",
                    ["protocol"] = "ftp",
                    ["managed-by"] = "control-plane"
                }
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        ["app"] = name
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta
                    {
                        Labels = new Dictionary<string, string>
                        {
                            ["app"] = name
                        }
                    },
                    Spec = new V1PodSpec
                    {
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = "ftp-server",
                                Image = "fauria/vsftpd:latest",
                                Env = new List<V1EnvVar>
                                {
                                    new V1EnvVar { Name = "FTP_USER", Value = config.Username },
                                    new V1EnvVar { Name = "FTP_PASS", Value = config.Password }
                                },
                                Ports = new List<V1ContainerPort>
                                {
                                    new V1ContainerPort { ContainerPort = 21 }
                                },
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new V1VolumeMount
                                    {
                                        Name = "shared-data",
                                        MountPath = "/home/vsftpd"
                                    }
                                }
                            }
                        },
                        Volumes = new List<V1Volume>
                        {
                            new V1Volume
                            {
                                Name = "shared-data",
                                PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                                {
                                    ClaimName = "file-sim-file-simulator-pvc"
                                }
                            }
                        }
                    }
                }
            }
        };

        await _k8sClient.CreateNamespacedDeploymentAsync(
            deployment, _namespace, cancellationToken: ct);

        // Create Service (NodePort)
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta { Name = name },
            Spec = new V1ServiceSpec
            {
                Type = "NodePort",
                Selector = new Dictionary<string, string> { ["app"] = name },
                Ports = new List<V1ServicePort>
                {
                    new V1ServicePort
                    {
                        Port = 21,
                        TargetPort = 21,
                        NodePort = config.NodePort
                    }
                }
            }
        };

        await _k8sClient.CreateNamespacedServiceAsync(
            service, _namespace, cancellationToken: ct);

        return name; // Return server ID for tracking
    }

    public async Task DeleteServerAsync(string name, CancellationToken ct)
    {
        // Delete Service first
        await _k8sClient.DeleteNamespacedServiceAsync(
            name, _namespace, cancellationToken: ct);

        // Then Deployment
        await _k8sClient.DeleteNamespacedDeploymentAsync(
            name, _namespace, cancellationToken: ct);
    }
}
```

**RBAC Requirements:**
The control plane ServiceAccount needs permissions to create/delete Deployments, Services, and ConfigMaps. Include in Helm chart:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: control-plane-role
  namespace: file-simulator
rules:
- apiGroups: ["apps"]
  resources: ["deployments"]
  verbs: ["get", "list", "create", "update", "delete"]
- apiGroups: [""]
  resources: ["services", "configmaps"]
  verbs: ["get", "list", "create", "update", "delete"]
```

### Pattern 3: React Hook for SignalR Connection Management

**What:** Custom React hook that manages SignalR connection lifecycle (connect, reconnect, disconnect) and provides typed message handlers.

**When to use:** Every React component that needs real-time updates from backend.

**Trade-offs:**
- **Pros**: Encapsulates connection logic, automatic reconnection, cleanup on unmount
- **Cons**: Must handle connection state in UI, potential for memory leaks if not properly cleaned up

**Example:**
```typescript
// hooks/useSignalR.ts
import { useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

interface UseSignalROptions {
  url: string;
  onHealthStatusChanged?: (status: HealthStatus) => void;
  onFileEventReceived?: (event: FileEvent) => void;
  onMetricsUpdated?: (metrics: Metric[]) => void;
}

export function useSignalR(options: UseSignalROptions) {
  const { url, onHealthStatusChanged, onFileEventReceived, onMetricsUpdated } = options;
  const [connectionState, setConnectionState] = useState<'disconnected' | 'connecting' | 'connected'>('disconnected');
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    // Build connection
    const connection = new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect() // Exponential backoff: 0s, 2s, 10s, 30s
      .configureLogging(LogLevel.Information)
      .build();

    // Register event handlers
    if (onHealthStatusChanged) {
      connection.on('HealthStatusChanged', onHealthStatusChanged);
    }
    if (onFileEventReceived) {
      connection.on('FileEventReceived', onFileEventReceived);
    }
    if (onMetricsUpdated) {
      connection.on('MetricsUpdated', onMetricsUpdated);
    }

    // State change handlers
    connection.onreconnecting(() => setConnectionState('connecting'));
    connection.onreconnected(() => setConnectionState('connected'));
    connection.onclose(() => setConnectionState('disconnected'));

    // Start connection
    setConnectionState('connecting');
    connection.start()
      .then(() => setConnectionState('connected'))
      .catch(err => {
        console.error('SignalR connection failed:', err);
        setConnectionState('disconnected');
      });

    connectionRef.current = connection;

    // Cleanup on unmount
    return () => {
      connection.stop();
      connectionRef.current = null;
    };
  }, [url]); // Only reconnect if URL changes

  return { connectionState, connection: connectionRef.current };
}

// Usage in component
function Dashboard() {
  const { connectionState, connection } = useSignalR({
    url: '/api/monitoring',
    onHealthStatusChanged: (status) => {
      console.log('Health status changed:', status);
      updateServerStore(status); // Update Zustand store
    },
    onFileEventReceived: (event) => {
      console.log('File event:', event);
      addToEventLog(event);
    }
  });

  return (
    <div>
      <ConnectionBadge state={connectionState} />
      {/* Dashboard content */}
    </div>
  );
}
```

### Pattern 4: Time-Series Metrics with Batched Writes

**What:** Collect metrics frequently (every 1-10 seconds) but batch writes to database to reduce I/O overhead and improve performance.

**When to use:** High-frequency metrics collection (CPU, memory, connection counts) where individual writes would overwhelm the database.

**Trade-offs:**
- **Pros**: Reduces database load by 10-100x, improves write throughput, lower latency for queries
- **Cons**: Metrics delayed by batch window (acceptable for monitoring), data loss risk if crash before flush (mitigated by small batch windows)

**Example:**
```csharp
// MetricsService.cs
public class MetricsService : BackgroundService
{
    private readonly ConcurrentQueue<Metric> _buffer = new();
    private readonly MetricsDbContext _dbContext;
    private readonly ILogger<MetricsService> _logger;
    private const int BatchSize = 100;
    private const int FlushIntervalSeconds = 30;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(FlushIntervalSeconds));

        while (await timer.WaitForNextTickAsync(ct))
        {
            await FlushMetricsAsync(ct);
        }
    }

    public void RecordMetric(string serverName, string metricName, double value)
    {
        _buffer.Enqueue(new Metric
        {
            Timestamp = DateTime.UtcNow,
            ServerName = serverName,
            MetricName = metricName,
            Value = value
        });

        // Flush immediately if buffer full
        if (_buffer.Count >= BatchSize)
        {
            _ = FlushMetricsAsync(CancellationToken.None);
        }
    }

    private async Task FlushMetricsAsync(CancellationToken ct)
    {
        var batch = new List<Metric>();

        // Drain buffer
        while (_buffer.TryDequeue(out var metric) && batch.Count < BatchSize)
        {
            batch.Add(metric);
        }

        if (batch.Count == 0) return;

        try
        {
            await _dbContext.Metrics.AddRangeAsync(batch, ct);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogDebug("Flushed {Count} metrics to database", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush metrics");

            // Re-queue on failure
            foreach (var metric in batch)
            {
                _buffer.Enqueue(metric);
            }
        }
    }
}
```

**For Prometheus Alternative:**
If using Prometheus instead of SQLite/SQL Server, expose metrics via `/metrics` endpoint and let Prometheus scrape:

```csharp
// Configure Prometheus metrics exporter
services.AddPrometheusMetrics(options =>
{
    options.Port = 9090;
    options.Path = "/metrics";
});

// Record metrics
public class HealthCheckService
{
    private static readonly Gauge ServerHealthGauge = Metrics.CreateGauge(
        "file_simulator_server_health",
        "Health status of protocol servers (0=down, 1=up)",
        new GaugeConfiguration
        {
            LabelNames = new[] { "server_name", "protocol" }
        });

    private async Task CheckServerHealthAsync(ServerDefinition server)
    {
        var isHealthy = await PingServerAsync(server);
        ServerHealthGauge.WithLabels(server.Name, server.Protocol)
            .Set(isHealthy ? 1 : 0);
    }
}
```

### Pattern 5: File Watcher with Debouncing

**What:** Monitor Windows directories for file changes and emit events, but debounce rapid changes (e.g., large file writes triggering multiple events) to avoid event floods.

**When to use:** Real-time file event streaming to dashboard when files are written to Windows directories.

**Trade-offs:**
- **Pros**: Reduces event spam (1000 events → 1 debounced event), improves UI responsiveness, lower SignalR bandwidth
- **Cons**: Events delayed by debounce window (typically 500ms-2s), multiple rapid changes may be collapsed into single event

**Example:**
```csharp
// FileWatcherService.cs
public class FileWatcherService : BackgroundService
{
    private readonly IHubContext<MonitoringHub> _hubContext;
    private readonly ConcurrentDictionary<string, Timer> _debounceTimers = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromSeconds(1);

    protected override Task ExecuteAsync(CancellationToken ct)
    {
        var watcher = new FileSystemWatcher(@"C:\simulator-data")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Created += (sender, e) => OnFileChanged("created", e.FullPath);
        watcher.Changed += (sender, e) => OnFileChanged("modified", e.FullPath);
        watcher.Deleted += (sender, e) => OnFileChanged("deleted", e.FullPath);

        return Task.CompletedTask;
    }

    private void OnFileChanged(string changeType, string filePath)
    {
        // Cancel existing timer for this file
        if (_debounceTimers.TryGetValue(filePath, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        // Create new debounce timer
        var timer = new Timer(_ =>
        {
            // Emit event after debounce delay
            var fileEvent = new FileEvent
            {
                Timestamp = DateTime.UtcNow,
                ChangeType = changeType,
                FilePath = filePath,
                FileName = Path.GetFileName(filePath)
            };

            _hubContext.Clients.All.SendAsync("FileEventReceived", fileEvent);

            // Cleanup timer
            _debounceTimers.TryRemove(filePath, out _);
        }, null, _debounceDelay, Timeout.InfiniteTimeSpan);

        _debounceTimers[filePath] = timer;
    }
}
```

## Data Flow

### Real-Time Health Monitoring Flow

```
[Background Worker]
    ↓ Every 10 seconds
[Health Check Service] → [Poll FTP/SFTP/NAS/HTTP/S3/SMB]
    ↓ Health status result
[SignalR Hub] → [Redis Backplane] → [All Backend Pods]
    ↓ Broadcast to connected clients
[React Hook (useSignalR)] → [Zustand Store Update]
    ↓ State change triggers re-render
[Dashboard Component] → [Display Health Badges]
```

### File Event Streaming Flow

```
[Windows Directory] → File created/modified/deleted
    ↓
[FileSystemWatcher] → [Debounce Timer (1s)]
    ↓
[FileWatcherService] → [SignalR Hub]
    ↓
[Redis Backplane] → [All Backend Pods]
    ↓
[React Hook] → [Event Log Store (Zustand)]
    ↓
[File Events Panel] → Display recent changes
```

### Dynamic Server Creation Flow

```
[React UI] → User clicks "Add FTP Server"
    ↓
[API Controller] → POST /api/servers
    ↓ Validate config
[KubernetesService] → Create Deployment + Service
    ↓ Kubernetes API calls
[K8s API Server] → Deploy Pod + Expose NodePort
    ↓ Wait for Pod Ready
[Health Check Service] → Detect new server
    ↓ Initial health check
[SignalR Hub] → Broadcast "ServerAdded" event
    ↓
[React Hook] → Add to Server List (Zustand)
    ↓
[Dashboard] → Show new server with health status
```

### Metrics Query Flow

```
[React Dashboard] → Request metrics for last 24h
    ↓ GET /api/metrics?server=ftp-1&period=24h
[API Controller] → [MetricsService.QueryAsync()]
    ↓ EF Core query
[SQLite/SQL Server] → Aggregate by 5-minute buckets
    ↓ Return time-series data
[API Controller] → JSON response
    ↓
[React Component] → Render Chart (Recharts/Chart.js)
```

### Key Data Flows

1. **Startup Initialization:** Backend discovers existing protocol servers via Kubernetes API, registers each for health checks, broadcasts initial state to dashboard
2. **Continuous Monitoring:** Background worker polls all servers every 10s, emits health status changes via SignalR, stores metrics in database
3. **Real-Time Events:** FileSystemWatcher detects Windows directory changes, debounces events, streams to all connected dashboards
4. **User Actions:** Dashboard sends REST commands (add/remove server, upload file), backend updates Kubernetes/filesystem, broadcasts state changes
5. **Historical Analysis:** Dashboard queries time-series database, receives aggregated metrics, renders charts for trend analysis

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| Single-node Minikube (current) | Single backend pod, no Redis backplane needed, SQLite for metrics, single Kafka broker (Strimzi KRaft mode), direct SignalR without sticky sessions |
| Multi-node Dev (2-3 pods) | Add Redis for SignalR backplane, configure sticky sessions (nginx ingress affinity), keep SQLite or upgrade to PostgreSQL, single Kafka broker sufficient |
| Production (10+ pods) | Redis Cluster (3+ nodes) for high availability, PostgreSQL or InfluxDB for metrics, Kafka cluster (3+ brokers) with replication factor 3, Prometheus + Grafana for observability, Azure SignalR Service alternative to Redis backplane |

### Scaling Priorities

1. **First bottleneck:** SignalR connections across multiple backend pods
   - **Symptom:** Users see stale data, events not received by all clients
   - **Solution:** Add Redis backplane, ensure all pods connect to same Redis instance, monitor Redis memory usage

2. **Second bottleneck:** Database writes for high-frequency metrics
   - **Symptom:** SQLite locks under load (>100 writes/sec), query latency increases
   - **Solution:** Implement batched writes (Pattern 4), migrate to PostgreSQL or InfluxDB, use indexed timestamp column, partition by date

3. **Third bottleneck:** Health check polling at scale (50+ servers)
   - **Symptom:** Health checks take >10s to complete, delayed status updates
   - **Solution:** Parallelize health checks with SemaphoreSlim(10) concurrency limit, cache results for 5s, offload to separate worker pool

## Anti-Patterns

### Anti-Pattern 1: Polling API for Real-Time Updates

**What people do:** React dashboard polls REST API every 2-5 seconds to fetch latest health status.

**Why it's wrong:**
- Wastes bandwidth (full payload every request even if no changes)
- Increases backend load (N concurrent users = N × queries/sec)
- Delays updates by polling interval
- Scales poorly (100 users × 2 requests/sec = 200 req/sec doing nothing)

**Do this instead:** Use SignalR WebSockets for server-push architecture. Backend emits events only when state changes. Dashboard receives updates in real-time (<100ms latency). Scales to 10,000+ concurrent connections on single pod.

### Anti-Pattern 2: Storing Kubernetes Resources in Database

**What people do:** Create database table for protocol servers, track state in SQL, sync periodically with Kubernetes.

**Why it's wrong:**
- Dual source of truth (database vs Kubernetes)
- Sync bugs inevitable (database says server exists, Kubernetes disagrees)
- Manual reconciliation required after failures
- Out-of-band changes (kubectl commands) not reflected in database

**Do this instead:** Kubernetes API is the source of truth. Query Kubernetes directly for server list. Use labels for filtering (`protocol=ftp`, `managed-by=control-plane`). Store only transient data (metrics, events) in database. On startup, reconcile in-memory state from Kubernetes.

### Anti-Pattern 3: Nested Helm Charts with Tight Coupling

**What people do:** Create separate Helm releases for protocol servers, control plane, Kafka, Redis. Each has own namespace and ServiceAccount.

**Why it's wrong:**
- Cross-namespace networking complexity (NetworkPolicies, DNS discovery)
- RBAC challenges (control plane can't manage servers in different namespace)
- Atomic deployments impossible (protocol server upgrade independent of control plane)
- Configuration drift (each chart has separate values.yaml)

**Do this instead:** Use umbrella chart pattern with subcharts. Single Helm release deploys entire stack. Shared namespace for simplified networking. Control plane subchart can be disabled for existing deployments. Single `values.yaml` with nested structure. Atomic rollbacks via `helm rollback`.

### Anti-Pattern 4: Global SignalR Broadcasting

**What people do:** Every event broadcasts to all connected clients: `Clients.All.SendAsync("HealthStatusChanged", status)`.

**Why it's wrong:**
- User monitoring 5 servers receives events for all 50 servers in cluster
- Bandwidth waste (90% of events irrelevant to user)
- UI performance degrades with event volume
- Privacy concerns (users see servers they don't own)

**Do this instead:** Use SignalR groups for targeted broadcasting:

```csharp
// When client connects, join groups for monitored servers
public async Task MonitorServers(string[] serverNames)
{
    foreach (var name in serverNames)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"server:{name}");
    }
}

// Broadcast only to interested clients
await Clients.Group($"server:{serverName}")
    .SendAsync("HealthStatusChanged", status);
```

### Anti-Pattern 5: No Connection State Handling in React

**What people do:** Assume SignalR connection is always connected. Render dashboard immediately without checking connection state.

**Why it's wrong:**
- Network interruptions cause silent failures (events stop arriving)
- No user feedback during reconnection attempts
- Race conditions (component mounts before connection ready)
- User doesn't know why dashboard is stale

**Do this instead:** Expose connection state in UI, disable actions while disconnected, show reconnection attempts:

```typescript
function Dashboard() {
  const { connectionState } = useSignalR({ url: '/api/monitoring' });

  if (connectionState === 'connecting') {
    return <Spinner>Connecting to real-time updates...</Spinner>;
  }

  if (connectionState === 'disconnected') {
    return <Alert severity="error">
      Connection lost. Attempting to reconnect...
    </Alert>;
  }

  // Render dashboard only when connected
  return <DashboardContent />;
}
```

## Integration Points

### Integration with Existing Helm Chart

| Integration Point | Pattern | Notes |
|-------------------|---------|-------|
| **Shared PVC** | Control plane mounts existing `file-sim-file-simulator-pvc` at `/mnt/simulator-data` | Backend needs read/write access for file operations |
| **Protocol Discovery** | Control plane queries Kubernetes API for pods with label `app.kubernetes.io/part-of=file-simulator-suite` | Existing protocol servers must have this label |
| **ConfigMap Updates** | When adding server, update existing `file-sim-file-simulator-config` ConfigMap with new service endpoint | Client applications auto-discover new servers |
| **Service Labels** | Control plane adds label `managed-by=control-plane` to dynamically created servers | Distinguishes user-created from Helm-deployed servers |
| **NodePort Allocation** | Backend tracks used NodePorts (30000-32767), assigns next available when creating server | Prevents port conflicts |

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| **Kubernetes API** | In-cluster config via ServiceAccount, RBAC role grants deployment/service CRUD | Requires `control-plane-role` ClusterRole binding |
| **Redis** | Standard connection string, deployed in same namespace via subchart | Optional for single-pod development |
| **Kafka** | Strimzi operator, KRaft mode (no ZooKeeper), single broker deployment | Enable via `controlPlane.kafka.enabled` |
| **SQLite** | Embedded in backend pod, mounted on PVC for persistence | Upgrade to PostgreSQL for production |
| **Prometheus** | Optional, metrics endpoint at `/metrics` for scraping | Alternative to SQLite for time-series data |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| **Dashboard ↔ Backend API** | REST over HTTP, standard JSON | CORS configured for Minikube NodePort access from Windows |
| **Dashboard ↔ SignalR Hub** | WebSocket (negotiation via /hub/monitoring endpoint) | Automatic reconnect with exponential backoff |
| **Backend ↔ Protocol Servers** | Health checks via TCP socket, protocol-specific handshakes (FTP 220, SFTP SSH-2.0) | Each protocol has dedicated health checker |
| **Backend ↔ Kubernetes API** | gRPC (Kubernetes client library abstracts transport) | Uses HTTP/2 with TLS (in-cluster) |
| **Backend ↔ Windows Filesystem** | FileSystemWatcher via hostPath PVC mount | Requires Windows host path mounted in Minikube |
| **Kafka ↔ Backend** | Kafka protocol (producer/consumer via Confluent.Kafka library) | Backend produces file events, consumes for alerting |

## Deployment Strategy

### Single Helm Chart (Umbrella Pattern)

**Recommended approach:** Extend existing `helm-chart/file-simulator` to include control plane as optional subchart.

**Advantages:**
- Atomic deployments: `helm upgrade` deploys entire stack
- Unified configuration: Single `values.yaml` with nested structure
- Backward compatible: Existing deployments disable control plane via `controlPlane.enabled: false`
- Simplified rollbacks: `helm rollback file-sim` reverts entire system
- Shared resources: Both layers use same PVC, namespace, ServiceAccount

**Structure:**
```
helm-chart/file-simulator/
├── Chart.yaml                    # Umbrella chart (v2.0.0)
├── values.yaml                   # Global + protocol server config
├── templates/                    # Existing protocol servers
│   ├── ftp.yaml
│   ├── nas.yaml
│   └── [Other existing templates]
└── charts/
    └── control-plane/            # New subchart
        ├── Chart.yaml            # v1.0.0
        ├── values.yaml           # Dashboard, backend, Kafka, Redis config
        └── templates/
            ├── backend-deployment.yaml
            ├── backend-service.yaml
            ├── dashboard-deployment.yaml
            ├── dashboard-service.yaml
            ├── redis-deployment.yaml
            ├── kafka-statefulset.yaml
            └── rbac.yaml         # ServiceAccount + Role for K8s API access
```

**Deployment command:**
```powershell
# Enable control plane in existing deployment
helm upgrade file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --set controlPlane.enabled=true `
    --set controlPlane.dashboard.nodePort=30080 `
    --set controlPlane.backend.nodePort=30081
```

**Values.yaml structure:**
```yaml
# Existing global config
global:
  storage:
    hostPath: /mnt/simulator-data
    size: 10Gi

# Existing protocol servers
ftp:
  enabled: true
  # ... existing config

nas:
  enabled: true
  # ... existing config

# NEW: Control plane configuration
controlPlane:
  enabled: false  # Disabled by default for backward compatibility

  dashboard:
    image:
      repository: your-registry/file-simulator-dashboard
      tag: 2.0.0
    service:
      type: NodePort
      port: 80
      nodePort: 30080
    resources:
      requests:
        memory: "128Mi"
        cpu: "100m"
      limits:
        memory: "512Mi"
        cpu: "500m"

  backend:
    image:
      repository: your-registry/file-simulator-control-plane
      tag: 2.0.0
    service:
      type: NodePort
      port: 5000
      nodePort: 30081
    signalr:
      useRedis: false  # Enable for multi-pod
    metrics:
      database: sqlite  # or prometheus
    resources:
      requests:
        memory: "256Mi"
        cpu: "200m"
      limits:
        memory: "1Gi"
        cpu: "1000m"

  redis:
    enabled: false  # Enable when backend.replicas > 1
    image:
      repository: redis
      tag: 7-alpine

  kafka:
    enabled: false  # Enable for event streaming features
    broker:
      replicas: 1
      storage: 5Gi
```

### Alternative: Multiple Releases (Not Recommended)

If umbrella chart proves too complex, deploy as separate releases:

```powershell
# Deploy protocol servers (existing)
helm upgrade --install file-sim-protocols ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator

# Deploy control plane (new)
helm upgrade --install file-sim-control ./helm-chart/control-plane `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --set backend.protocolsNamespace=file-simulator
```

**Disadvantages:**
- Two releases to manage
- Rollbacks must be coordinated manually
- Configuration split across two values.yaml files
- No atomic upgrades

## Build Order Considerations

Based on dependencies, recommended implementation sequence:

### Phase 1: Foundation (Week 1-2)
- Backend API project structure (ASP.NET Core)
- Kubernetes client integration (KubernetesClient NuGet)
- Basic REST endpoints (list servers, health check)
- Docker image + Helm subchart
- RBAC configuration
- Deploy and verify K8s API access

### Phase 2: Monitoring (Week 2-3)
- Health check service (background worker)
- SignalR hub setup (without Redis first)
- React dashboard skeleton (Vite + Material-UI)
- SignalR client integration (useSignalR hook)
- Real-time health status display
- Connection state handling

### Phase 3: Metrics (Week 3-4)
- SQLite database + EF Core models
- Metrics collection service (batched writes)
- Time-series query API
- React chart components (Recharts)
- Historical trends dashboard page

### Phase 4: File Operations (Week 4-5)
- FileSystemWatcher service (debounced)
- File event streaming via SignalR
- File browser UI component
- Upload/download API endpoints
- Delete file operations

### Phase 5: Dynamic Management (Week 5-6)
- Server creation API (dynamic Deployments)
- Server deletion with cleanup
- Configuration templates (FTP, SFTP, NAS)
- Add server UI flow
- Remove server UI flow

### Phase 6: Kafka Integration (Week 6-7)
- Strimzi deployment in subchart
- Kafka producer for file events
- Topic management API
- Kafka dashboard page
- Consumer group visualization

### Phase 7: Production Readiness (Week 7-8)
- Redis backplane (multi-pod support)
- Alerting rules configuration
- Configuration import/export
- Error boundaries in React
- Comprehensive logging
- End-to-end testing

**Rationale for ordering:**
- Foundation first enables iterative testing (kubectl commands work before UI exists)
- Monitoring provides immediate value (see existing servers without any new functionality)
- Metrics build on monitoring (same health data, different storage)
- File operations independent of server management (can be developed in parallel)
- Dynamic management most complex (requires stable K8s client integration)
- Kafka last (adds minimal value until other features complete)

## Sources

### SignalR and Real-Time Communication
- [Scalable & real-time messaging (chat) systems with SignalR, React, .NET and Kubernetes](https://mahdi-karimipour.medium.com/scalable-real-time-messaging-chat-systems-with-signalr-react-net-and-kubernetes-2a0a812f7ffb)
- [Building a Scalable Real-Time Dashboard with React, WebSocket, Docker, Kubernetes, and AWS](https://medium.com/@virajvbahulkar/building-a-scalable-real-time-dashboard-with-react-websocket-docker-kubernetes-and-aws-21c8e2421436)
- [How to Use WebSockets in React for Real-Time Applications (January 2026)](https://oneuptime.com/blog/post/2026-01-15-websockets-react-real-time-applications/view)
- [Real-Time Data Transfer with WebSockets and SignalR in .NET Core and React](https://dotnetfullstackdev.medium.com/real-time-data-transfer-with-websockets-and-signalr-in-net-core-409b0d50719b)
- [ASP.NET Core SignalR production hosting and scaling (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/signalr/scale?view=aspnetcore-9.0)
- [Optimising SignalR on Kubernetes at G-Research](https://www.gresearch.com/news/signalr-on-kubernetes/)
- [Scaling SignalR Core Web Applications With Kubernetes](https://medium.com/swlh/scaling-signalr-core-web-applications-with-kubernetes-fca32d787c7d)

### SignalR Redis Backplane
- [Redis backplane for ASP.NET Core SignalR scale-out (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0)
- [Scaling Horizontally: Kubernetes, Sticky Sessions, and Redis](https://dev.to/deepak_mishra_35863517037/scaling-horizontally-kubernetes-sticky-sessions-and-redis-578o)
- [Practical ASP.NET Core SignalR: Scaling](https://codeopinion.com/practical-asp-net-core-signalr-scaling/)

### Time-Series Databases
- [Prometheus vs. InfluxDB: A Monitoring Comparison](https://logz.io/blog/prometheus-influxdb/)
- [Compare InfluxDB vs Prometheus (InfluxData)](https://www.influxdata.com/comparison/influxdb-vs-prometheus/)
- [Prometheus vs InfluxDB: Side-by-Side Comparison (Last9)](https://last9.io/blog/prometheus-vs-influxdb/)
- [Prometheus vs InfluxDB (SigNoz)](https://signoz.io/blog/prometheus-vs-influxdb/)

### Kafka on Kubernetes
- [Strimzi - Apache Kafka on Kubernetes](https://strimzi.io/)
- [Strimzi Quickstarts](https://strimzi.io/quickstarts/)
- [Running Kafka on a Single Node in K8s Cluster](https://medium.com/@buildbot.tech/running-kafka-on-a-single-node-in-k8s-cluster-b5c68f7fd92d)
- [Deploying Kafka With Kubernetes: A Complete Guide](https://dzone.com/articles/how-to-deploy-apache-kafka-with-kubernetes)

### Kubernetes Client Libraries (.NET)
- [KubernetesClient NuGet Package](https://www.nuget.org/packages/KubernetesClient/)
- [kubernetes-client/csharp (GitHub - Official C# client)](https://github.com/kubernetes-client/csharp)
- [KubeOps.KubernetesClient 10.2.0 (NuGet)](https://www.nuget.org/packages/KubeOps.KubernetesClient/)
- [Creating Dynamic Kubernetes Jobs with the C# Client](https://medium.com/@sezer.darendeli/creating-dynamic-kubernetes-jobs-with-the-c-client-2c2087cc73d5)

### React State Management
- [State Management in 2026: Redux, Context API, and Modern Patterns](https://www.nucamp.co/blog/state-management-in-2026-redux-context-api-and-modern-patterns)
- [React State Management 2025: Redux, Context, Recoil & Zustand](https://www.zignuts.com/blog/react-state-management-2025)
- [Real-time State Management in React Using WebSockets](https://moldstud.com/articles/p-real-time-state-management-in-react-using-websockets-boost-your-apps-performance)
- [5 React State Management Tools Developers Actually Use in 2025](https://www.syncfusion.com/blogs/post/react-state-management-libraries)
- [Zustand + React Query: A New Approach to State Management](https://medium.com/@freeyeon96/zustand-react-query-new-state-management-7aad6090af56)

### Monitoring Architecture
- [System Design Realtime Monitoring System: A Complete Walkthrough](https://systemdesignschool.io/problems/realtime-monitoring-system/solution)
- [How to Build a Real-Time Dashboard: A Step-by-Step Guide for Engineers](https://estuary.dev/blog/how-to-build-a-real-time-dashboard/)
- [Best Cloud Observability Tools 2026](https://cloudchipr.com/blog/best-cloud-observability-tools-2026)

### Helm Chart Patterns
- [How to Simplify Your Kubernetes Helm Deployments](https://codefresh.io/blog/simplify-kubernetes-helm-deployments/)
- [Refactoring with Umbrella Pattern in Helm](https://medium.com/@fdsh/refactoring-with-umbrella-pattern-in-helm-515997a91c89)
- [Helm best practices (Codefresh Docs)](https://codefresh.io/docs/docs/ci-cd-guides/helm-best-practices/)
- [Helm 3 Umbrella Charts & Standalone Chart Image Tags](https://itnext.io/helm-3-umbrella-charts-standalone-chart-image-tags-an-alternative-approach-78a218d74e2d)

---
*Architecture research for: File Simulator Suite v2.0 Simulator Control Platform*
*Researched: 2026-02-02*
