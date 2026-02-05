# CLAUDE.md - File Simulator Suite Implementation Guide

## Project Overview

**Version:** 2.0 (Released: 2026-02-05)

This project implements a File Access Simulator Suite for Kubernetes/OpenShift environments. It provides:

**v1.0 Features (Protocol Simulators):**
- Multiple file transfer protocols (FTP, SFTP, HTTP/WebDAV, S3/MinIO, SMB, NFS)
- 7-server Multi-NAS topology matching production OCP network architecture
- Bidirectional Windows sync via init containers and sidecars
- Production-identical PV/PVC patterns with static provisioning

**v2.0 Features (Control Platform):**
- **Control API**: REST API for server management, file operations, Kafka, alerts, and metrics
- **Real-time Dashboard**: React-based UI with SignalR for live updates
- **Dynamic Servers**: Create FTP, SFTP, NAS servers on-demand via API
- **Kafka Integration**: Topic management, message produce/consume, consumer groups
- **Alerting System**: Configurable thresholds for disk, health, and Kafka
- **Historical Metrics**: SQLite-backed time-series with 7-day retention and hourly rollups
- **E2E Testing**: Playwright-based browser automation for dashboard validation

## CRITICAL: Multi-Profile kubectl Safety

**⚠️ IMPORTANT: This project uses separate Minikube profiles to avoid conflicts.**

### Profile Architecture
- **file-simulator** (Hyper-V driver): Contains file-simulator namespace with 8 protocol servers
- **minikube** (Docker driver): Contains ez-platform namespace with user's applications

### Mandatory kubectl Practice

**✅ ALWAYS USE --context FLAG:**
```bash
kubectl --context=file-simulator get pods -n file-simulator
kubectl --context=file-simulator apply -f deployment.yaml
kubectl --context=file-simulator delete pod <name> -n file-simulator

kubectl --context=minikube get pods -n ez-platform
kubectl --context=minikube logs <pod> -n ez-platform
```

**❌ NEVER DO THIS:**
```bash
kubectl config use-context file-simulator  # ❌ Causes accidental deletions
kubectl delete namespace file-simulator    # ❌ Which cluster? Don't know!
```

**Helm commands must also use --kube-context:**
```bash
helm upgrade --install file-sim ./helm-chart/file-simulator \
    --kube-context=file-simulator \
    --namespace file-simulator
```

### Why This Matters

Without using `--context` flag, the user experienced:
- Accidental deletion of file-simulator namespace from wrong cluster
- Confusion about which cluster is active
- Lost deployments requiring full re-deployment
- Cross-contamination between isolated environments

**ROOT CAUSE:** Using `kubectl config use-context` to switch between clusters creates hidden state that leads to mistakes.

## DNS/Hostname Setup (RECOMMENDED)

Use stable hostnames instead of changing Minikube IPs. This ensures consistent access from browsers and applications.

### Initial Setup (Run Once as Administrator)

```powershell
# Update Windows hosts file with Minikube IP
.\scripts\Setup-Hosts.ps1
```

This adds entries to `C:\Windows\System32\drivers\etc\hosts`:
```
172.25.170.231  file-simulator.local
172.25.170.231  api.file-simulator.local
172.25.170.231  dashboard.file-simulator.local
```

### After Minikube Restart

If Minikube IP changes (e.g., after `minikube delete`), run the setup script again:
```powershell
.\scripts\Setup-Hosts.ps1
```

### Service URLs (Stable Hostnames)

| Service         | URL                                    |
|-----------------|----------------------------------------|
| Dashboard       | http://file-simulator.local:30080      |
| Control API     | http://file-simulator.local:30500      |
| FTP             | ftp://file-simulator.local:30021       |
| SFTP            | sftp://file-simulator.local:30022      |
| HTTP/WebDAV     | http://file-simulator.local:30088      |
| S3/MinIO        | http://file-simulator.local:30900      |
| SMB             | \\\\file-simulator.local\shared        |
| NFS             | file-simulator.local:32049:/data       |

### Connection Info API

Get all connection details for your applications:

```bash
# JSON format (default)
curl http://file-simulator.local:30500/api/connection-info

# Environment variables format
curl http://file-simulator.local:30500/api/connection-info?format=env

# YAML format (for Kubernetes ConfigMaps)
curl http://file-simulator.local:30500/api/connection-info?format=yaml

# .NET appsettings.json format
curl http://file-simulator.local:30500/api/connection-info?format=dotnet

# Save to file
.\scripts\Get-ConnectionInfo.ps1 -Format env -Save .env
```

### Configuring Your Applications

**Option 1: Environment Variables**
```powershell
# Generate .env file for your app
.\scripts\Get-ConnectionInfo.ps1 -Format env -Save path/to/your/app/.env
```

**Option 2: appsettings.json**
```powershell
# Get .NET configuration
curl http://file-simulator.local:30500/api/connection-info?format=dotnet
```

**Option 3: Kubernetes ConfigMap**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: file-simulator-config
data:
  FILE_FTP_HOST: "file-simulator.local"
  FILE_FTP_PORT: "30021"
  FILE_SFTP_HOST: "file-simulator.local"
  FILE_SFTP_PORT: "30022"
  # ... see full output from connection-info API
```

## Technology Stack

### Backend
- **Runtime**: .NET 9.0
- **Framework**: ASP.NET Core Minimal API
- **Real-time**: SignalR for WebSocket connections
- **Database**: SQLite with Entity Framework Core
- **Kafka Client**: Confluent.Kafka 2.12.0
- **Kubernetes Client**: KubernetesClient 18.0.13
- **Validation**: FluentValidation

### Frontend
- **Framework**: React 19 with TypeScript
- **Build Tool**: Vite 6
- **Charts**: Recharts for time-series visualization
- **UI Components**: Custom components with CSS Grid/Flexbox
- **Real-time**: @microsoft/signalr for WebSocket

### Infrastructure
- **Container Orchestration**: Kubernetes (Minikube/OCP)
- **Deployment**: Helm 3.x
- **File Protocols**: FTP (FluentFTP), SFTP (SSH.NET), S3 (AWSSDK.S3), HTTP (HttpClient), SMB (SMBLibrary), NFS (mounted filesystem)
- **Event Streaming**: Apache Kafka with ZooKeeper sidecar

### Testing
- **E2E Testing**: Playwright.Xunit 1.58.0
- **CLI Testing**: TestConsole with Spectre.Console

## Architecture

### v2.0 Architecture (Control Platform)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         file-simulator namespace                              │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│  ┌────────────────────┐  ┌────────────────────┐  ┌────────────────────┐     │
│  │  Dashboard (React) │  │  Control API (.NET)│  │  Kafka + ZooKeeper │     │
│  │      :30080        │  │      :30500        │  │      :30093        │     │
│  │                    │◄─┤ SignalR WebSocket  │  │                    │     │
│  │  - Server grid     │  │ REST endpoints     │  │  - Topic mgmt      │     │
│  │  - File browser    │  │ K8s discovery      │  │  - Produce/consume │     │
│  │  - Kafka UI        │  │ SQLite metrics     │  │  - Consumer groups │     │
│  │  - Alerts          │  │ Alert service      │  │                    │     │
│  │  - History charts  │  │ FileWatcher        │  │                    │     │
│  └────────────────────┘  └────────────────────┘  └────────────────────┘     │
│                                    │                                          │
│  ┌─────┐ ┌──────┐ ┌──────┐ ┌─────┐ ┌─────┐ ┌──────────────────────────┐    │
│  │ FTP │ │ SFTP │ │ HTTP │ │ S3  │ │ SMB │ │   7x Multi-NAS Servers   │    │
│  │30021│ │30022 │ │30088 │ │30900│ │ 445 │ │   :32150-32156           │    │
│  └──┬──┘ └──┬───┘ └──┬───┘ └──┬──┘ └──┬──┘ └───────────┬──────────────┘    │
│     └───────┴────────┴────────┴───────┴────────────────┘                     │
│                         Shared PVC (hostPath)                                 │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
              Windows Host: C:\simulator-data (mounted)
```

### v1.0 Architecture (Protocol Servers Only)

```
┌─────────────────────────────────────────────────────────────────┐
│                    file-simulator namespace                      │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────┐   │
│  │           Management UI (FileBrowser :30080)             │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────┐ ┌──────┐ ┌──────┐ ┌─────┐ ┌─────┐ ┌─────┐          │
│  │ FTP │ │ SFTP │ │ HTTP │ │ S3  │ │ SMB │ │ NFS │          │
│  │30021│ │30022 │ │30088 │ │30900│ │30445│ │32049│          │
│  └──┬──┘ └──┬───┘ └──┬───┘ └──┬──┘ └──┬──┘ └──┬──┘          │
│     └───────┴────────┴────────┴───────┴───────┘              │
│                    Shared PVC (hostPath)                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
         Windows Host: C:\simulator-data (mounted)
```

---

## Control API Quick Reference

The Control API provides programmatic access to all simulator features.

**Base URL:** `http://file-simulator.local:30500`

### Key Endpoints

| Category | Endpoint | Method | Description |
|----------|----------|--------|-------------|
| Health | `/api/health` | GET | Health check |
| Connection | `/api/connection-info` | GET | Get all service connection details |
| Servers | `/api/servers` | GET | List all servers |
| Servers | `/api/servers/ftp` | POST | Create dynamic FTP server |
| Servers | `/api/servers/sftp` | POST | Create dynamic SFTP server |
| Servers | `/api/servers/nas` | POST | Create dynamic NAS server |
| Servers | `/api/servers/{name}` | DELETE | Delete dynamic server |
| Servers | `/api/servers/{name}/start` | POST | Start stopped server |
| Servers | `/api/servers/{name}/stop` | POST | Stop running server |
| Files | `/api/files/tree` | GET | List directory tree |
| Files | `/api/files/upload` | POST | Upload file (100MB limit) |
| Files | `/api/files/download` | GET | Download file |
| Files | `/api/files` | DELETE | Delete file/directory |
| Kafka | `/api/kafka/topics` | GET/POST | List or create topics |
| Kafka | `/api/kafka/topics/{name}/messages` | GET/POST | Get or produce messages |
| Kafka | `/api/kafka/consumer-groups` | GET | List consumer groups |
| Alerts | `/api/alerts/active` | GET | Get active alerts |
| Alerts | `/api/alerts/history` | GET | Get alert history |
| Metrics | `/api/metrics/samples` | GET | Get raw health samples |
| Metrics | `/api/metrics/hourly` | GET | Get hourly aggregations |
| Config | `/api/configuration/export` | GET | Export server configuration |
| Config | `/api/configuration/import` | POST | Import server configuration |

### SignalR Hubs

| Hub | URL | Events |
|-----|-----|--------|
| Server Status | `/hubs/server-status` | `ServerStatusUpdate` (every 5s) |
| File Events | `/hubs/file-events` | `FileEvent` (create/modify/delete) |
| Kafka | `/hubs/kafka` | `KafkaMessage` (per-topic subscription) |
| Metrics | `/hubs/metrics` | `MetricsSample` (latest health data) |
| Alerts | `/hubs/alerts` | `AlertTriggered`, `AlertResolved` |

For complete API documentation, see [docs/API-REFERENCE.md](docs/API-REFERENCE.md).

---

## IMPLEMENTATION TASKS

### Phase 1: Infrastructure Setup

#### Task 1.1: Create Helm Chart Structure
```bash
# Create directory structure
mkdir -p helm-chart/file-simulator/templates
```

**Files to create:**
- `helm-chart/file-simulator/Chart.yaml` - Helm chart metadata
- `helm-chart/file-simulator/values.yaml` - Default configuration
- `helm-chart/file-simulator/values-multi-instance.yaml` - Multi-server configuration
- `helm-chart/file-simulator/templates/_helpers.tpl` - Template helpers
- `helm-chart/file-simulator/templates/namespace.yaml` - Namespace definition
- `helm-chart/file-simulator/templates/storage.yaml` - PV and PVC
- `helm-chart/file-simulator/templates/serviceaccount.yaml` - Service account

#### Task 1.2: Create Protocol Server Deployments
**Files to create:**
- `helm-chart/file-simulator/templates/management.yaml` - FileBrowser UI
- `helm-chart/file-simulator/templates/ftp.yaml` - vsftpd server
- `helm-chart/file-simulator/templates/sftp.yaml` - OpenSSH SFTP
- `helm-chart/file-simulator/templates/http.yaml` - nginx + WebDAV
- `helm-chart/file-simulator/templates/s3.yaml` - MinIO
- `helm-chart/file-simulator/templates/smb.yaml` - Samba
- `helm-chart/file-simulator/templates/nas.yaml` - NFS server

#### Task 1.3: Create Multi-Instance Templates
**Files to create:**
- `helm-chart/file-simulator/templates/ftp-multi.yaml` - Multiple FTP servers
- `helm-chart/file-simulator/templates/sftp-multi.yaml` - Multiple SFTP servers

---

### Phase 2: .NET Client Library

#### Task 2.1: Create Project Structure
```bash
mkdir -p src/FileSimulator.Client/{Services,Extensions,Examples}
```

#### Task 2.2: Create File Protocol Services
**File: `src/FileSimulator.Client/Services/FileProtocolServices.cs`**

Implement `IFileProtocolService` interface with methods:
- `DiscoverFilesAsync(path, pattern)` - List/poll files
- `ReadFileAsync(remotePath)` - Download to memory
- `DownloadFileAsync(remotePath, localPath)` - Download to disk
- `WriteFileAsync(remotePath, content)` - Upload from memory
- `UploadFileAsync(localPath, remotePath)` - Upload from disk
- `DeleteFileAsync(remotePath)` - Delete file
- `HealthCheckAsync()` - Check connectivity

**Implementations required:**
1. `FtpFileService` - Using FluentFTP
2. `SftpFileService` - Using SSH.NET
3. `S3FileService` - Using AWSSDK.S3
4. `HttpFileService` - Using HttpClient
5. `SmbFileService` - Using SMBLibrary
6. `NfsFileService` - Using mounted filesystem

**Options classes:**
- `FtpServerOptions`
- `SftpServerOptions`
- `S3ServerOptions`
- `HttpServerOptions`
- `SmbServerOptions`
- `NfsServerOptions`

#### Task 2.3: Create File Polling Service
**File: `src/FileSimulator.Client/Services/FilePollingService.cs`**

Implement:
- `FilePollingEndpoint` - Configuration for polling endpoint
- `FilePollingOptions` - Configuration container
- `FileDiscoveredEvent` - Event raised when file found
- `IFileDiscoveryHandler` - Interface for handling discoveries
- `FilePollingService` - Main polling orchestrator
- `FilePollingJob` - Quartz job for scheduled polling
- `FilePollingExtensions` - DI registration helpers

#### Task 2.4: Create Service Extensions
**File: `src/FileSimulator.Client/Extensions/ServiceCollectionExtensions.cs`**

Implement:
- `AddFileSimulator(configuration)` - Register services from config
- `AddFileSimulatorForMinikube(ip)` - Configure for local development
- `AddFileSimulatorForCluster(namespace, release)` - Configure for K8s
- `AddFileSimulatorHealthChecks(options)` - Health check registration
- `AddFileSimulatorHttpClient(options)` - HTTP client with Polly

---

### Phase 3: Example Microservice

#### Task 3.1: Create Complete Example
**File: `src/FileSimulator.Client/Examples/CompleteExampleMicroservice.cs`**

Implement:
- Service registration for all protocols
- Quartz configuration for polling
- MassTransit configuration for messaging
- API endpoints for each protocol (list, read, write)
- Health check endpoint
- Example `IFileDiscoveryHandler` implementation
- Example MassTransit consumer

#### Task 3.2: Create Configuration Files
**File: `src/FileSimulator.Client/Examples/appsettings.example.json`**

Include configuration for:
- All protocol connection settings
- Polling endpoints configuration
- RabbitMQ connection

---

### Phase 4: Kubernetes Deployment Samples

#### Task 4.1: Create ConfigMap/Secret Samples
**File: `helm-chart/samples/file-simulator-configmap.yaml`**

Include:
- ConfigMap with all service endpoints
- Secret with credentials
- Example deployment using envFrom

#### Task 4.2: Create Microservice Deployment
**File: `helm-chart/samples/microservice-deployment.yaml`**

Include:
- Deployment spec with env injection
- Health check probes
- Resource limits
- Volume mounts for temp storage

---

### Phase 5: Testing & Verification

#### Task 5.1: Create Test Scripts
**Files:**
- `scripts/test-simulator.ps1` - PowerShell test script
- `scripts/test-simulator.sh` - Bash test script

Test each protocol:
1. Kubernetes connectivity
2. Management UI health
3. HTTP/WebDAV operations
4. S3/MinIO operations
5. FTP connectivity
6. SFTP connectivity
7. SMB connectivity
8. NFS connectivity
9. Cross-protocol file sharing

#### Task 5.2: Create Setup Scripts
**File: `scripts/setup-windows.ps1`**

Implement:
- Directory structure creation
- Minikube mount configuration
- Environment file generation
- Helper scripts for each protocol

---

## CODE PATTERNS

### Pattern 1: Protocol Service Implementation
```csharp
public class XxxFileService : IFileProtocolService, IDisposable
{
    private readonly XxxServerOptions _options;
    private readonly ILogger<XxxFileService> _logger;
    private XxxClient? _client;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public string ProtocolName => "XXX";

    public XxxFileService(IOptions<XxxServerOptions> options, ILogger<XxxFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private async Task<XxxClient> GetClientAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_client == null || !_client.IsConnected)
            {
                _client?.Dispose();
                _client = new XxxClient(_options.Host, _options.Port);
                await _client.ConnectAsync(ct);
                _logger.LogInformation("Connected to XXX server {Host}:{Port}", _options.Host, _options.Port);
            }
            return _client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    // Implement IFileProtocolService methods...

    public void Dispose()
    {
        _client?.Dispose();
        _connectionLock.Dispose();
    }
}
```

### Pattern 2: Discovery with Pattern Matching
```csharp
public async Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern, CancellationToken ct)
{
    var client = await GetClientAsync(ct);
    var items = await client.ListDirectoryAsync(path, ct);
    
    var files = items
        .Where(i => !i.IsDirectory)
        .Where(i => pattern == null || MatchesPattern(i.Name, pattern))
        .Select(i => new RemoteFileInfo
        {
            FullPath = i.FullName,
            Name = i.Name,
            Size = i.Size,
            ModifiedAt = i.ModifiedTime,
            IsDirectory = false
        });

    _logger.LogDebug("Discovered {Count} files in {Path}", files.Count(), path);
    return files;
}

private static bool MatchesPattern(string name, string pattern)
{
    var regex = "^" + Regex.Escape(pattern)
        .Replace("\\*", ".*")
        .Replace("\\?", ".") + "$";
    return Regex.IsMatch(name, regex, RegexOptions.IgnoreCase);
}
```

### Pattern 3: File Polling with Quartz
```csharp
[DisallowConcurrentExecution]
public class FilePollingJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var endpointName = context.MergedJobDataMap.GetString("EndpointName");
        var events = await _pollingService.PollEndpointAsync(endpointName, context.CancellationToken);

        foreach (var evt in events)
        {
            foreach (var handler in _handlers)
            {
                await handler.HandleFileDiscoveredAsync(evt, context.CancellationToken);
            }
        }
    }
}
```

### Pattern 4: Configuration from Environment
```csharp
public static FileSimulatorOptions FromEnvironment()
{
    return new FileSimulatorOptions
    {
        FtpHost = Environment.GetEnvironmentVariable("FILE_FTP_HOST") ?? "localhost",
        FtpPort = int.Parse(Environment.GetEnvironmentVariable("FILE_FTP_PORT") ?? "21"),
        // ... etc
    };
}

public static FileSimulatorOptions ForCluster(string @namespace = "file-simulator", string releaseName = "file-sim")
{
    var prefix = $"{releaseName}-file-simulator";
    var suffix = $".{@namespace}.svc.cluster.local";
    
    return new FileSimulatorOptions
    {
        FtpHost = $"{prefix}-ftp{suffix}",
        FtpPort = 21,
        // ... etc
    };
}
```

---

## VALIDATION CHECKLIST

### Helm Chart Validation
- [ ] `helm lint helm-chart/file-simulator`
- [ ] `helm template file-sim helm-chart/file-simulator --debug`
- [ ] All services deploy correctly
- [ ] PVC mounts properly
- [ ] NodePorts accessible from host

### .NET Library Validation
- [ ] Project builds without errors
- [ ] All protocols connect successfully
- [ ] Discovery returns correct file lists
- [ ] Read operations return file content
- [ ] Write operations create files
- [ ] Delete operations remove files
- [ ] Health checks report correctly

### Control API Validation (v2.0)
- [ ] API health check returns 200: `curl http://file-simulator.local:30500/api/health`
- [ ] Connection info returns all services: `curl http://file-simulator.local:30500/api/connection-info`
- [ ] Server list includes all Helm-deployed servers
- [ ] Dynamic FTP server creation works
- [ ] Dynamic SFTP server creation works
- [ ] Dynamic NAS server creation works
- [ ] Server start/stop/restart operations work
- [ ] File tree returns correct structure
- [ ] File upload succeeds (test with small file)
- [ ] File download returns correct content
- [ ] Kafka topic create/list/delete works
- [ ] Kafka message produce/consume works
- [ ] Alerts appear for unhealthy servers
- [ ] Metrics samples and hourly rollups available

### Dashboard Validation (v2.0)
- [ ] Dashboard loads at http://file-simulator.local:30080
- [ ] Servers tab shows server grid with health status
- [ ] Real-time server status updates via SignalR
- [ ] Create Server modal opens and validates input
- [ ] Files tab shows directory tree
- [ ] File upload via drag-and-drop works
- [ ] Kafka tab shows topics and allows message production
- [ ] Alerts tab shows active and historical alerts
- [ ] History tab shows latency charts

### E2E Test Validation (v2.0)
- [ ] Playwright browsers installed: `pwsh .\scripts\Install-PlaywrightBrowsers.ps1`
- [ ] Smoke tests pass: `dotnet test --filter "FullyQualifiedName~SmokeTests"`
- [ ] Dashboard tests pass: `dotnet test --filter "FullyQualifiedName~DashboardTests"`
- [ ] Server management tests pass
- [ ] File operations tests pass
- [ ] Kafka tests pass

### Integration Validation
- [ ] File uploaded via HTTP visible in S3
- [ ] File uploaded via FTP visible in Management UI
- [ ] Polling detects new files
- [ ] Event handlers process files
- [ ] Processed files moved/deleted correctly

---

## NUGET PACKAGES REQUIRED

```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.305" />
<PackageReference Include="FluentFTP" Version="50.0.1" />
<PackageReference Include="SSH.NET" Version="2024.1.0" />
<PackageReference Include="SMBLibrary" Version="1.5.2" />
<PackageReference Include="Quartz" Version="3.8.1" />
<PackageReference Include="Quartz.Extensions.Hosting" Version="3.8.1" />
<PackageReference Include="MassTransit" Version="8.2.5" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.2.5" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Network" Version="8.0.1" />
<PackageReference Include="AspNetCore.HealthChecks.Uris" Version="8.0.1" />
```

---

## DEPLOYMENT COMMANDS

### Verified Working Configuration (v2.0)

```powershell
# 1. Create cluster with Hyper-V driver (REQUIRED for SMB)
# NOTE: v2.0 requires 12GB for Control API, Dashboard, and Kafka
minikube start `
    --profile file-simulator `
    --driver=hyperv `
    --memory=12288 `
    --cpus=4 `
    --disk-size=20g `
    --mount `
    --mount-string="C:\simulator-data:/mnt/simulator-data"

# 2. Deploy simulator (ALWAYS use --kube-context)
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --create-namespace

# 3. NFS fix is now integrated into nas.yaml - no manual patch needed!
# (emptyDir fix applied automatically in Helm template)

# 4. Start tunnel for SMB (separate Administrator terminal)
minikube tunnel -p file-simulator

# 5. Verify deployment (ALWAYS use --context)
kubectl --context=file-simulator get pods -n file-simulator
kubectl --context=file-simulator get svc -n file-simulator

# v2.0 pods include: control-api, dashboard, kafka + all protocol servers

# 6. Test connectivity
# Quick API health check:
curl http://file-simulator.local:30500/api/health

# Full protocol tests:
cd src/FileSimulator.TestConsole
dotnet run

# E2E dashboard tests:
cd tests/FileSimulator.E2ETests
$env:USE_EXISTING_SIMULATOR = "true"
dotnet test
```

### v1.0 Configuration (Protocol Servers Only)

```powershell
# For v1.0 features only (no Control API, Dashboard, Kafka):
minikube start `
    --profile file-simulator `
    --driver=hyperv `
    --memory=8192 `
    --cpus=4 `
    --disk-size=20g `
    --mount `
    --mount-string="C:\simulator-data:/mnt/simulator-data"
```

### NFS Fix Patch File (REQUIRED)

Create `nfs-fix-patch.yaml`:
```yaml
spec:
  template:
    spec:
      containers:
      - name: nfs-server
        volumeMounts:
        - name: nfs-data
          mountPath: /data
        - name: shared-data
          mountPath: /shared
      volumes:
      - name: nfs-data
        emptyDir: {}
      - name: shared-data
        persistentVolumeClaim:
          claimName: file-sim-file-simulator-pvc
```

**Why needed:** NFS cannot export Windows-mounted hostPath volumes. This patch uses emptyDir for NFS exports while maintaining shared storage access via PVC.

### Multi-Instance Deployment

```bash
# Deploy with multiple FTP/SFTP servers
helm upgrade --install file-sim ./helm-chart/file-simulator \
    --kube-context=file-simulator \
    -f ./helm-chart/file-simulator/values-multi-instance.yaml \
    --namespace file-simulator --create-namespace
```

---

## VERIFIED CONFIGURATION (TESTED)

### v2.0 Configuration (Full Platform)

**Cluster Specifications:**
- **Minikube Profile:** file-simulator
- **Driver:** hyperv (required for SMB LoadBalancer)
- **Memory:** 12GB (12288 MB) - required for Kafka + Control Platform
- **CPUs:** 4 - comfortable headroom
- **Disk:** 20GB
- **Mount:** C:\simulator-data:/mnt/simulator-data

**v2.0 Components Verified:**
1. Control API - REST endpoints working
2. Dashboard - React UI loading with SignalR
3. Kafka - Topic management and messaging
4. Dynamic Servers - FTP/SFTP/NAS creation
5. Alerts - Trigger and resolve cycle
6. Metrics - SQLite storage and hourly rollups
7. E2E Tests - Playwright tests passing

### v1.0 Configuration (Protocol Servers Only)

**Cluster Specifications:**
- **Memory:** 8GB (8192 MB) - minimum for all 8 protocols
- **CPUs:** 4

**Resource Utilization (v1.0):**
- CPU Requests: 575m (~14% of 4 CPUs)
- CPU Limits: 1.9 CPUs (~48% of 4 CPUs)
- Memory Requests: 706Mi (~9% of 8GB)
- Memory Limits: 2.85Gi (~35% of 8GB)

**All 8 Protocols Tested:**
1. Management UI - HTTP 200
2. HTTP Server - HTTP 200
3. WebDAV - HTTP 401 (auth working)
4. S3/MinIO - Console accessible
5. FTP - TCP connection successful
6. SFTP - TCP connection successful
7. SMB - LoadBalancer active
8. NFS - File operations verified (write/read/list)

## ERROR HANDLING

All protocol services should:
1. Use connection pooling with thread-safe access
2. Implement automatic reconnection on failure
3. Log all operations with appropriate levels
4. Throw meaningful exceptions with context
5. Support cancellation tokens
6. Dispose resources properly

**kubectl Operations:** ALWAYS include `--context=file-simulator` flag to prevent accidental operations on wrong cluster.

---

## NOTES FOR CLAUDE CODE

### Critical Rules
1. **ALWAYS use --context flag** - Never switch contexts, always explicit: `kubectl --context=file-simulator`
2. **Use 12GB memory for v2.0** - Kafka requires ~1.5-2GB additional memory
3. **NFS fix is automatic** - v2.0 Helm chart includes emptyDir fix, no manual patch needed
4. **Keep profiles separate** - file-simulator (Hyper-V) for this, minikube (Docker) for ez-platform

### Development Workflow
5. **Start with the Helm chart** - Deploy infrastructure first
6. **Test each protocol individually** - Ensure connectivity before integration
7. **Use the test scripts** - Verify everything works before moving on
8. **Follow the patterns** - Consistency across all protocol implementations
9. **Log extensively** - Debug issues will be easier with good logs
10. **Handle offline network** - OCP is offline, no external dependencies

### v2.0 Testing Guidance
11. **TestConsole for protocol tests** - `dotnet run` in src/FileSimulator.TestConsole
12. **Playwright for dashboard tests** - Set `USE_EXISTING_SIMULATOR=true` and run `dotnet test`
13. **Dynamic server tests modify state** - Use `--dynamic` flag with caution
14. **SignalR debugging** - Check browser DevTools Network tab for WebSocket frames
15. **SQLite metrics location** - `/data/metrics.db` inside control-api pod

## CRITICAL FIXES AND SAFETY

### NFS Server Fix (Now Automatic)

**Problem:** NFS server crashes with `exportfs: /data does not support NFS export`

**Solution (v2.0):** The NFS fix is now integrated into `nas.yaml` Helm template. No manual patching required.

**Solution (v1.0 or custom deployments):** Apply nfs-fix-patch.yaml after deployment:

```powershell
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator `
    --patch-file nfs-fix-patch.yaml
```

### Multi-Profile Safety

**Problem:** Accidental deletions when forgetting which cluster is active

**Solution:** ALWAYS use explicit context flags

```bash
# CORRECT
kubectl --context=file-simulator get pods -n file-simulator
helm --kube-context=file-simulator list -n file-simulator

# WRONG - NEVER DO THIS
kubectl get pods  # Which cluster? Dangerous!
kubectl config use-context file-simulator  # Hidden state causes mistakes
```

### Dynamic Server Cleanup

**Problem:** Orphaned dynamic servers if tests are interrupted

**Solution:** Dynamic servers use `ownerReferences` pointing to the Control API pod. If the pod is deleted, Kubernetes garbage collection removes the dynamic resources. For manual cleanup:

```bash
# List dynamic servers
kubectl --context=file-simulator get deployments -n file-simulator -l app.kubernetes.io/managed-by=control-api

# Delete specific dynamic server via API
curl -X DELETE http://file-simulator.local:30500/api/servers/{name}
```
