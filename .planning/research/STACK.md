# Technology Stack Research - v2.0 Control Platform

**Project:** File Simulator Suite - v2.0 Simulator Control Platform
**Researched:** 2026-02-02
**Confidence:** HIGH

## Executive Summary

This research covers ONLY the stack additions needed for v2.0 milestone features: React dashboard, real-time monitoring, Kafka simulator, and Kubernetes orchestration. The existing .NET 9.0/ASP.NET Core infrastructure is preserved and extended with SignalR, SQLite, and minimal container additions.

**Key Decision:** Use ASP.NET Core + SignalR for real-time backend, React + Vite for dashboard frontend, SQLite for historical data, single-broker Kafka via Strimzi, and official .NET KubernetesClient for dynamic pod management.

---

## Core Technologies (NEW for v2.0)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **ASP.NET Core SignalR** | 9.0+ | Real-time WebSocket backend | Built into ASP.NET Core, integrates with existing .NET 9 stack, automatic connection management, hub pattern for broadcasting events |
| **React** | 19.x | Dashboard frontend UI | De facto standard for interactive UIs, extensive ecosystem, excellent TypeScript support |
| **Vite** | 6.x | Frontend build tool | Fastest build tool for React in 2026, instant HMR, TypeScript native, replaces CRA |
| **EF Core + SQLite** | 10.0.2 | Historical data persistence | Embedded database (no separate container), zero configuration, perfect for development/testing simulators, cross-platform |
| **Strimzi Kafka Operator** | 0.50.0 | Minimal Kafka cluster | Helm-based, single-broker mode for testing, KRaft (no ZooKeeper), standard Kubernetes patterns |
| **KubernetesClient** | 18.0.13 | Dynamic pod orchestration | Official .NET Kubernetes client, create/delete deployments, watch pod events, .NET 9 compatible |

---

## Supporting Libraries

### Backend (.NET 9.0)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **Microsoft.AspNetCore.SignalR.Client** | 9.0.0 | SignalR client library | If backend needs to connect to other SignalR hubs |
| **Microsoft.EntityFrameworkCore.Sqlite** | 10.0.2 | SQLite database provider | Historical metrics, server state, event logs |
| **KubernetesClient** | 18.0.13 | Kubernetes API client | Create/delete FTP/SFTP deployments dynamically |
| **System.IO.FileSystemWatcher** | Built-in | Windows directory monitoring | Watch C:\simulator-data for file events |

### Frontend (React + TypeScript)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **react-use-websocket** | 4.13.0 | React WebSocket hooks | Simplified SignalR connection in React components |
| **@microsoft/signalr** | 9.0.0 | SignalR JavaScript client | Direct SignalR hub connections from React |
| **recharts** | 3.6.0 | Real-time charting | Line charts for throughput, pie charts for protocol distribution |
| **@tanstack/react-query** | 5.x | Server state management | REST API calls for historical data queries |
| **zustand** | 5.x | Client state management | Dashboard UI state, filters, selected servers |

---

## Installation Commands

### Backend (.NET)

```bash
# Add to existing FileSimulator.Server.csproj
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.2
dotnet add package KubernetesClient --version 18.0.13
# SignalR already included in ASP.NET Core 9.0
```

### Frontend (React)

```bash
# Create React app with Vite
npm create vite@latest dashboard -- --template react-ts
cd dashboard

# Core dependencies
npm install @microsoft/signalr@9.0.0
npm install react-use-websocket@4.13.0
npm install recharts@3.6.0
npm install @tanstack/react-query@5
npm install zustand@5

# Dev dependencies
npm install -D @types/node
npm install -D typescript@5
```

### Infrastructure (Helm)

```bash
# Add Strimzi operator to existing Helm deployment
helm repo add strimzi https://strimzi.io/charts/
helm install kafka-operator strimzi/strimzi-kafka-operator \
    --kube-context=file-simulator \
    --namespace file-simulator \
    --set watchNamespaces={file-simulator}
```

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not Alternative |
|----------|-------------|-------------|---------------------|
| Real-time backend | SignalR | Socket.IO | SignalR is built-in, ASP.NET Core native, no extra dependencies |
| Real-time backend | SignalR | gRPC streaming | SignalR easier for broadcast patterns, better browser support |
| Frontend framework | React | Vue 3 | React has larger ecosystem, more enterprise examples for dashboards |
| Frontend framework | React | Blazor WebAssembly | React better for real-time updates, lighter weight, more charting libraries |
| Build tool | Vite | Webpack | Vite is 10-20x faster, HMR instant, default for React in 2026 |
| Database | SQLite | PostgreSQL | SQLite is embedded (no container), perfect for simulator/testing, simpler |
| Database | SQLite | In-memory only | SQLite provides persistence across restarts, queryable history |
| Kafka deployment | Strimzi | Bitnami Kafka Helm | Strimzi is Kubernetes-native operator, better CRD integration |
| Kafka deployment | Strimzi | Confluent Platform | Strimzi is OSS, minimal, sufficient for testing simulator |
| Kubernetes client | KubernetesClient | KubeOps SDK | KubernetesClient is official, simpler for direct API calls |
| State management | Zustand | Redux | Zustand is lighter, less boilerplate, sufficient for dashboard UI |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **ZooKeeper with Kafka** | Deprecated in Kafka 3.x+, KRaft is standard in 2026 | Strimzi with KRaft mode (default) |
| **Create React App (CRA)** | Deprecated in 2023, no longer maintained | Vite (official React recommendation) |
| **Redux Toolkit** | Overkill for this dashboard, too much boilerplate | Zustand for client state, React Query for server state |
| **Separate PostgreSQL container** | Adds complexity, requires migrations, overkill for simulator | SQLite embedded in API pod |
| **gRPC for real-time** | Harder to broadcast, less browser support, needs codegen | SignalR with automatic fallback to SSE/long polling |
| **Custom WebSocket implementation** | SignalR handles reconnection, groups, scaling automatically | SignalR hubs |
| **Full-scale Kafka cluster (3+ brokers)** | This is a simulator, not production Kafka | Single broker Strimzi deployment |

---

## Architecture Integration

### How NEW Stack Integrates with Existing Infrastructure

```
┌─────────────────────────────────────────────────────────────────┐
│                    file-simulator namespace                      │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  NEW: React Dashboard (Nginx :30081)                      │   │
│  │  - Vite-built static files                                │   │
│  │  - SignalR client -> Control API                          │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  NEW: Control API (ASP.NET Core :5000)                    │   │
│  │  - SignalR Hub (real-time events)                         │   │
│  │  - REST API (historical queries)                          │   │
│  │  - EF Core + SQLite (embedded)                            │   │
│  │  - KubernetesClient (dynamic FTP/SFTP)                    │   │
│  │  - FileSystemWatcher (C:\simulator-data)                  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  NEW: Kafka (Single Broker :30092)                        │   │
│  │  - Strimzi Operator managed                               │   │
│  │  - KRaft mode (no ZooKeeper)                              │   │
│  │  - StatefulSet with PVC                                   │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  EXISTING: FileBrowser UI (:30080)                        │   │
│  │  EXISTING: 7 NAS servers (NFS exports)                    │   │
│  │  EXISTING: 6 protocol servers (FTP, SFTP, HTTP, S3, SMB)  │   │
│  │  EXISTING: Shared PVC (hostPath -> C:\simulator-data)     │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

**Data Flow:**
1. **File events:** Windows FileSystemWatcher → Control API → SignalR → React Dashboard
2. **Protocol metrics:** Protocol servers log to NAS → Control API polls → SQLite → Dashboard
3. **Kafka events:** Kafka consumer in Control API → SignalR broadcast → Dashboard
4. **Dynamic control:** Dashboard → REST API → KubernetesClient → Create/Delete FTP pods

---

## Stack Patterns by Use Case

### Pattern 1: Real-Time File Event Monitoring

**Tech:** FileSystemWatcher → SignalR Hub → react-use-websocket

```csharp
// Backend: Control API
public class FileEventsHub : Hub
{
    public async Task BroadcastFileEvent(FileEvent evt)
    {
        await Clients.All.SendAsync("FileDiscovered", evt);
    }
}

// FileSystemWatcher service
_watcher.Created += async (s, e) => {
    await _hubContext.Clients.All.SendAsync("FileDiscovered", new {
        Path = e.FullPath,
        Server = "NAS1",
        Timestamp = DateTime.UtcNow
    });
};
```

```typescript
// Frontend: React Dashboard
const { lastJsonMessage } = useWebSocket('ws://localhost:5000/hubs/fileevents', {
  onMessage: (msg) => {
    const fileEvent = JSON.parse(msg.data);
    // Update dashboard
  }
});
```

### Pattern 2: Historical Metrics with REST + React Query

**Tech:** EF Core SQLite → REST API → React Query

```csharp
// Backend: Control API
app.MapGet("/api/metrics/throughput", async (MetricsDbContext db) => {
    return await db.Metrics
        .Where(m => m.Timestamp > DateTime.UtcNow.AddHours(-1))
        .GroupBy(m => m.Protocol)
        .Select(g => new { Protocol = g.Key, Throughput = g.Sum(m => m.BytesTransferred) })
        .ToListAsync();
});
```

```typescript
// Frontend: React Dashboard
const { data } = useQuery({
  queryKey: ['throughput'],
  queryFn: () => fetch('/api/metrics/throughput').then(r => r.json()),
  refetchInterval: 5000
});
```

### Pattern 3: Dynamic Server Creation

**Tech:** KubernetesClient → Kubernetes API

```csharp
// Backend: Control API
app.MapPost("/api/servers/ftp", async (IKubernetes k8s, CreateServerRequest req) => {
    var deployment = new V1Deployment
    {
        Metadata = new V1ObjectMeta { Name = $"ftp-{req.Name}" },
        Spec = new V1DeploymentSpec
        {
            Replicas = 1,
            Template = new V1PodTemplateSpec
            {
                Spec = new V1PodSpec
                {
                    Containers = new List<V1Container>
                    {
                        new V1Container
                        {
                            Name = "ftp",
                            Image = "delfer/alpine-ftp-server",
                            // ... configuration
                        }
                    }
                }
            }
        }
    };

    await k8s.CreateNamespacedDeploymentAsync(deployment, "file-simulator");
    return Results.Created($"/api/servers/ftp/{req.Name}", deployment.Metadata.Name);
});
```

### Pattern 4: Kafka Event Simulation

**Tech:** Confluent.Kafka → Strimzi Broker

```csharp
// Backend: Kafka simulator service
var config = new ProducerConfig { BootstrapServers = "kafka-broker:9092" };
using var producer = new ProducerBuilder<string, string>(config).Build();

await producer.ProduceAsync("file-events", new Message<string, string>
{
    Key = file.Name,
    Value = JsonSerializer.Serialize(new { Path = file.FullPath, Size = file.Length })
});
```

---

## Version Compatibility Matrix

| Package | Version | Compatible With | Notes |
|---------|---------|-----------------|-------|
| ASP.NET Core | 9.0+ | .NET 9.0 | SignalR included |
| EF Core SQLite | 10.0.2 | .NET 9.0, .NET 10.0 | Latest stable (Jan 2026) |
| KubernetesClient | 18.0.13 | .NET 9.0, .NET 10.0 | Released Dec 2025 |
| React | 19.x | TypeScript 5.x | Latest major version |
| Vite | 6.x | Node 18+ | ESM native |
| @microsoft/signalr | 9.0.0 | React 18+, React 19+ | JavaScript client |
| react-use-websocket | 4.13.0 | React 18+ | Uses React 18 hooks |
| Recharts | 3.6.0 | React 18+, React 19+ | D3-based charting |
| Strimzi | 0.50.0 | Kubernetes 1.25+ | Kafka 3.9+ (KRaft) |

**Critical:** All packages are 2025-2026 releases, confirmed compatible with .NET 9/10 and React 19.

---

## Configuration Best Practices

### SignalR Configuration

```csharp
// Program.cs
builder.Services.AddSignalR()
    .AddJsonProtocol(options => {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// For production scaling (NOT needed for Minikube)
builder.Services.AddSignalR()
    .AddStackExchangeRedis("redis:6379");
```

### SQLite Configuration

```csharp
// Program.cs
builder.Services.AddDbContext<MetricsDbContext>(options =>
    options.UseSqlite("Data Source=metrics.db"));

// Automatically create database on startup
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<MetricsDbContext>();
await db.Database.EnsureCreatedAsync();
```

### FileSystemWatcher Best Practices

```csharp
var watcher = new FileSystemWatcher(@"C:\simulator-data")
{
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
    Filter = "*.*",
    IncludeSubdirectories = true,
    InternalBufferSize = 65536 // Max: 64 KB (prevent buffer overflow)
};

// Handle buffer overflow errors
watcher.Error += (s, e) => {
    _logger.LogError("FileSystemWatcher buffer overflow: {Exception}", e.GetException());
    // Trigger full directory rescan
};
```

### Strimzi Minimal Kafka Configuration

```yaml
# kafka-single-broker.yaml (Custom Resource)
apiVersion: kafka.strimzi.io/v1beta2
kind: Kafka
metadata:
  name: kafka-cluster
  namespace: file-simulator
spec:
  kafka:
    version: 3.9.0
    replicas: 1  # Single broker for testing
    listeners:
      - name: plain
        port: 9092
        type: internal
        tls: false
      - name: external
        port: 30092
        type: nodeport
        tls: false
    storage:
      type: persistent-claim
      size: 10Gi
  # KRaft mode (no ZooKeeper)
  entityOperator:
    topicOperator: {}
    userOperator: {}
```

**Deploy:** `kubectl --context=file-simulator apply -f kafka-single-broker.yaml -n file-simulator`

---

## Resource Requirements (Additions to Existing 8GB/4CPU)

| Component | CPU Request | CPU Limit | Memory Request | Memory Limit |
|-----------|-------------|-----------|----------------|--------------|
| **Control API** | 100m | 500m | 256Mi | 512Mi |
| **Dashboard (Nginx)** | 50m | 100m | 64Mi | 128Mi |
| **Kafka Broker** | 200m | 1000m | 512Mi | 1Gi |
| **Strimzi Operator** | 50m | 200m | 128Mi | 256Mi |
| **TOTAL NEW** | **400m** | **1.8 CPUs** | **960Mi** | **~2Gi** |

**Combined with Existing:**
- CPU Requests: 575m (existing) + 400m (new) = **975m (~24% of 4 CPUs)**
- Memory Requests: 706Mi (existing) + 960Mi (new) = **~1.6Gi (~20% of 8GB)**

**Verdict:** Current 8GB/4CPU Minikube cluster can comfortably run v2.0 additions.

---

## Security Considerations

### SignalR Hub Authorization

```csharp
[Authorize] // Require authentication for hub connections
public class FileEventsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Validate connection token
        var token = Context.GetHttpContext()?.Request.Query["access_token"];
        // Add to groups based on user role
        await Groups.AddToGroupAsync(Context.ConnectionId, "Administrators");
    }
}
```

### SQLite Database Location

```csharp
// PRODUCTION: Use persistent volume for database
builder.Services.AddDbContext<MetricsDbContext>(options =>
    options.UseSqlite("Data Source=/data/metrics.db"));  // Mount PVC here

// DEVELOPMENT: Use in-memory for testing
builder.Services.AddDbContext<MetricsDbContext>(options =>
    options.UseSqlite("Data Source=:memory:"));
```

### Kubernetes API RBAC

```yaml
# serviceaccount-control-api.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: control-api
  namespace: file-simulator
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: control-api-role
  namespace: file-simulator
rules:
  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "list", "create", "delete", "patch"]
  - apiGroups: [""]
    resources: ["pods", "services"]
    verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: control-api-binding
  namespace: file-simulator
subjects:
  - kind: ServiceAccount
    name: control-api
    namespace: file-simulator
roleRef:
  kind: Role
  name: control-api-role
  apiGroup: rbac.authorization.k8s.io
```

---

## Deployment Strategy

### Phase 1: Backend API with SignalR

1. Create `src/FileSimulator.ControlApi/` project
2. Add SignalR hub for file events
3. Add EF Core + SQLite for metrics
4. Add FileSystemWatcher service
5. Deploy to Kubernetes with NodePort

### Phase 2: React Dashboard

1. Create `dashboard/` with Vite + React + TypeScript
2. Add SignalR client connection
3. Add Recharts for real-time visualization
4. Build and deploy with Nginx container

### Phase 3: Kafka Integration

1. Deploy Strimzi operator via Helm
2. Create single-broker Kafka cluster
3. Add Kafka producer to Control API
4. Add Kafka consumer for event streaming

### Phase 4: Dynamic Orchestration

1. Add KubernetesClient to Control API
2. Create ServiceAccount with RBAC
3. Implement REST endpoints for CRUD operations
4. Add UI controls in React dashboard

---

## Migration from Existing Stack

**NO BREAKING CHANGES** - All existing components continue to work:

| Existing Component | Status | Integration |
|--------------------|--------|-------------|
| 7 NAS servers | Unchanged | Control API monitors via NFS mounts |
| FTP/SFTP/HTTP/S3/SMB | Unchanged | Metrics scraped via log files |
| FileBrowser UI | Unchanged | Remains primary file management UI |
| Helm chart | Extended | Add control-api.yaml, dashboard.yaml, kafka.yaml templates |
| Shared PVC | Unchanged | Control API reads from same PVC |

---

## Testing Strategy

### Unit Tests (Backend)

```bash
# Add test project
dotnet new xunit -n FileSimulator.ControlApi.Tests
dotnet add package Microsoft.AspNetCore.SignalR.Client.Core
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### Integration Tests (SignalR)

```csharp
// Test SignalR hub broadcasts
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/hubs/fileevents")
    .Build();

await connection.StartAsync();
var received = false;
connection.On<FileEvent>("FileDiscovered", evt => received = true);

// Trigger file event
File.WriteAllText(@"C:\simulator-data\test.txt", "test");
await Task.Delay(1000);

Assert.True(received);
```

### E2E Tests (Frontend)

```bash
# Add Playwright for E2E testing
npm install -D @playwright/test
npx playwright install
```

---

## Sources

**ASP.NET Core SignalR:**
- [Overview of ASP.NET Core SignalR | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction?view=aspnetcore-10.0)
- [Real-time ASP.NET with SignalR | .NET](https://dotnet.microsoft.com/en-us/apps/aspnet/signalr)
- [Building a Real-Time Chat Application with SignalR, ASP.NET Core, and React | Medium](https://medium.com/@SuneraKonara/building-a-real-time-chat-application-with-signalr-asp-net-core-and-react-0848be6d6cf9)

**Kafka on Kubernetes:**
- [Strimzi - Apache Kafka on Kubernetes](https://strimzi.io/)
- [Deploying Kafka on a Kind Kubernetes cluster | Medium](https://medium.com/@martin.hodges/deploying-kafka-on-a-kind-kubernetes-cluster-for-development-and-testing-purposes-ed7adefe03cb)
- [Strimzi Kafka Operator Helm Chart](https://artifacthub.io/packages/helm/strimzi/strimzi-kafka-operator)

**.NET KubernetesClient:**
- [KubernetesClient 18.0.13 | NuGet Gallery](https://www.nuget.org/packages/KubernetesClient/)
- [GitHub - kubernetes-client/csharp](https://github.com/kubernetes-client/csharp)
- [Creating Dynamic Kubernetes Jobs with the C# Client | Medium](https://medium.com/@sezer.darendeli/creating-dynamic-kubernetes-jobs-with-the-c-client-2c2087cc73d5)

**FileSystemWatcher:**
- [FileSystemWatcher Class | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0)
- [Efficient Directory Monitoring Techniques for Windows 10 with C# and .NET Core 7](https://www.c-sharpcorner.com/article/efficient-directory-monitoring-techniques-for-windows-10-with-c-sharp-and-net-core-7/)
- [FileSystemWatcher in C# - Code Maze](https://code-maze.com/csharp-filesystemwatcher/)

**React Real-Time Dashboards:**
- [Real-Time Data Visualization in React using WebSockets and Charts | Syncfusion](https://www.syncfusion.com/blogs/post/view-real-time-data-using-websocket)
- [I Built a Real-Time Dashboard in React Using WebSockets and Recoil | Medium](https://medium.com/@connect.hashblock/i-built-a-real-time-dashboard-in-react-using-websockets-and-recoil-076d69b4eeff)
- [How to Use WebSockets in React for Real-Time Applications](https://oneuptime.com/blog/post/2026-01-15-websockets-react-real-time-applications/view)

**React Libraries:**
- [react-use-websocket - npm](https://www.npmjs.com/package/react-use-websocket)
- [recharts - npm](https://www.npmjs.com/package/recharts)
- [Complete Guide to Setting Up React with TypeScript and Vite (2026) | Medium](https://medium.com/@robinviktorsson/complete-guide-to-setting-up-react-with-typescript-and-vite-2025-468f6556aaf2)

**Entity Framework Core SQLite:**
- [Microsoft.EntityFrameworkCore.Sqlite 10.0.2 | NuGet Gallery](https://www.nuget.org/packages/microsoft.entityframeworkcore.sqlite)
- [SQLite Database Provider - EF Core | Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)

**Database Comparison:**
- [PostgreSQL vs SQLite: Which Relational Database Wins In 2026? | SelectHub](https://www.selecthub.com/relational-database-solutions/postgresql-vs-sqlite/)
- [SQLite Vs PostgreSQL - Key Differences | Airbyte](https://airbyte.com/data-engineering-resources/sqlite-vs-postgresql)

---

*Stack research for: File Simulator Suite v2.0 Control Platform*
*Researched: 2026-02-02*
*Confidence: HIGH (all versions verified from official sources, released 2025-2026)*
