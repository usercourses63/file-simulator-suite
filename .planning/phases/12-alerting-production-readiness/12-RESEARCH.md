# Phase 12: Alerting and Production Readiness - Research

**Researched:** 2026-02-04
**Domain:** Alerting systems, Redis SignalR backplane, production containerization, Kubernetes deployment
**Confidence:** HIGH

## Summary

This phase combines alerting infrastructure with full production deployment. The research covers ten interconnected domains: backend alert services, Redis backplane for SignalR scale-out, configuration persistence, multi-stage Dockerfile patterns, local container registries, Kafka external access, dynamic NodePort allocation, NFS export fixes, React error boundaries, and toast notification patterns.

The standard approach uses ASP.NET Core health checks with custom `IHealthCheck` implementations for disk space and Kafka broker monitoring, broadcasting alerts via SignalR to the dashboard. Redis backplane (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) enables multiple Control API pods to share WebSocket connections. For containerization, a multi-stage Dockerfile pattern (Node.js build -> nginx serve) reduces dashboard image size while providing proper SPA routing and health endpoints.

**Primary recommendations:**
1. Use `DriveInfo.AvailableFreeSpace` for disk monitoring with 1GB default threshold
2. Use Sonner for toast notifications (2-3KB, TypeScript-first, modern API)
3. Use `react-error-boundary` package for comprehensive error handling
4. Use minikube registry addon with port forwarding for local images
5. Incorporate NFS fix directly into Helm chart using emptyDir pattern
6. Keep alert retention at 7 days (SQLite storage with auto-cleanup)

## Standard Stack

### Core

| Library/Package | Version | Purpose | Why Standard |
|----------------|---------|---------|--------------|
| Microsoft.AspNetCore.SignalR.StackExchangeRedis | 9.0.x | Redis backplane for SignalR | Official Microsoft package, seamless integration |
| StackExchange.Redis | 2.8.x | Redis client (bundled) | Transitive dependency, industry standard |
| Sonner | 1.7.x | Toast notifications | 2-3KB, TypeScript-first, 11.5K+ GitHub stars, 7M+ weekly NPM downloads |
| react-error-boundary | 5.x | Error boundary wrapper | Modern hooks-based API, 1M+ weekly downloads |
| nginx:alpine | latest | Static file serving | Smallest nginx image (~8MB), production-grade |
| registry:2 | latest | Local container registry | Official Docker registry, simple setup |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| AspNetCore.HealthChecks.Network | 8.0.1 | Network health checks | Already in project, TCP port checks |
| System.IO.DriveInfo | built-in | Disk space monitoring | Windows directory space checks |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Sonner | react-toastify | react-toastify is 16KB vs 2-3KB, more features but heavier |
| Redis backplane | Azure SignalR Service | Azure service is managed but not applicable for local Minikube |
| nginx:alpine | nginx-unprivileged | unprivileged more secure but needs port >1024 |
| registry:2 | minikube docker-env | docker-env simpler but doesn't persist images between profile recreates |

**Installation:**

```bash
# Backend
cd src/FileSimulator.ControlApi
dotnet add package Microsoft.AspNetCore.SignalR.StackExchangeRedis --version 9.0.0

# Frontend
cd src/dashboard
npm install sonner react-error-boundary
```

## Architecture Patterns

### Recommended Project Structure Additions

```
src/FileSimulator.ControlApi/
|-- Services/
|   |-- AlertService.cs               # NEW - Alert management and broadcasting
|   |-- DiskSpaceHealthCheck.cs       # NEW - Custom disk monitoring
|   |-- KafkaHealthCheck.cs           # NEW - Kafka broker health
|-- Hubs/
|   |-- AlertHub.cs                   # NEW - SignalR hub for alerts
|-- Models/
|   |-- Alert.cs                      # NEW - Alert entity
|   |-- AlertSeverity.cs              # NEW - Info/Warning/Critical enum
|-- Data/
|   |-- AlertEntity.cs                # NEW - EF Core entity for persistence

src/dashboard/
|-- src/
|   |-- components/
|   |   |-- AlertToast.tsx            # NEW - Toast notification component
|   |   |-- AlertBanner.tsx           # NEW - Persistent unresolved alert banner
|   |   |-- AlertsTab.tsx             # NEW - Dedicated alerts tab
|   |   |-- ErrorBoundary.tsx         # NEW - Error boundary wrapper
|   |-- hooks/
|   |   |-- useAlerts.ts              # NEW - Alert state management
|   |   |-- useAlertStream.ts         # NEW - SignalR alert subscription
|   |-- types/
|   |   |-- alert.ts                  # NEW - Alert type definitions

helm-chart/file-simulator/
|-- templates/
|   |-- redis.yaml                    # NEW - Redis deployment for backplane
|   |-- dashboard.yaml                # NEW - Dashboard deployment
|-- Dockerfile.dashboard              # NEW - Multi-stage dashboard build
```

### Pattern 1: Backend Alert Service with SignalR Broadcasting

**What:** Centralized alert management that monitors conditions, stores alerts in SQLite, and broadcasts via SignalR
**When to use:** Server health degradation, disk space warnings, Kafka broker unavailability
**Example:**

```csharp
// Source: ASP.NET Core health checks + SignalR patterns
public class AlertService : IHostedService, IDisposable
{
    private readonly IDbContextFactory<MetricsDbContext> _dbFactory;
    private readonly IHubContext<AlertHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertService> _logger;
    private Timer? _checkTimer;

    // Alert thresholds (Claude's discretion)
    private const long DiskSpaceThresholdBytes = 1L * 1024 * 1024 * 1024; // 1GB
    private static readonly TimeSpan AlertRetentionPeriod = TimeSpan.FromDays(7);

    public AlertService(
        IDbContextFactory<MetricsDbContext> dbFactory,
        IHubContext<AlertHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<AlertService> logger)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Alert service starting");
        // Check every 30 seconds
        _checkTimer = new Timer(CheckConditions, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    private async void CheckConditions(object? state)
    {
        try
        {
            await CheckDiskSpaceAsync();
            await CheckKafkaHealthAsync();
            await CheckServerHealthAsync();
            await CleanupOldAlertsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alert conditions");
        }
    }

    private async Task CheckDiskSpaceAsync()
    {
        var path = Environment.GetEnvironmentVariable("CONTROL_DATA_PATH")
            ?? (OperatingSystem.IsWindows() ? @"C:\simulator-data" : "/mnt/simulator-data");

        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(path)!);
            if (drive.AvailableFreeSpace < DiskSpaceThresholdBytes)
            {
                await RaiseAlertAsync(new Alert
                {
                    Id = Guid.NewGuid(),
                    Type = "DiskSpace",
                    Severity = AlertSeverity.Warning,
                    Title = "Low Disk Space",
                    Message = $"Available: {drive.AvailableFreeSpace / (1024 * 1024)}MB (threshold: {DiskSpaceThresholdBytes / (1024 * 1024 * 1024)}GB)",
                    Source = path,
                    TriggeredAt = DateTime.UtcNow,
                    IsResolved = false
                });
            }
            else
            {
                await ResolveAlertsAsync("DiskSpace", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check disk space for {Path}", path);
        }
    }

    private async Task RaiseAlertAsync(Alert alert)
    {
        using var context = await _dbFactory.CreateDbContextAsync();

        // Check if similar unresolved alert exists
        var existing = await context.Alerts
            .FirstOrDefaultAsync(a => a.Type == alert.Type
                && a.Source == alert.Source
                && !a.IsResolved);

        if (existing != null)
        {
            // Update existing alert
            existing.Message = alert.Message;
            existing.TriggeredAt = alert.TriggeredAt;
        }
        else
        {
            // New alert
            context.Alerts.Add(alert);
            await context.SaveChangesAsync();

            // Broadcast new alert via SignalR
            await _hubContext.Clients.All.SendAsync("AlertTriggered", alert);
            _logger.LogWarning("Alert raised: {Title} - {Message}", alert.Title, alert.Message);
        }
    }

    private async Task ResolveAlertsAsync(string type, string source)
    {
        using var context = await _dbFactory.CreateDbContextAsync();

        var unresolvedAlerts = await context.Alerts
            .Where(a => a.Type == type && a.Source == source && !a.IsResolved)
            .ToListAsync();

        foreach (var alert in unresolvedAlerts)
        {
            alert.IsResolved = true;
            alert.ResolvedAt = DateTime.UtcNow;

            // Broadcast resolution via SignalR
            await _hubContext.Clients.All.SendAsync("AlertResolved", alert);
            _logger.LogInformation("Alert resolved: {Title}", alert.Title);
        }

        await context.SaveChangesAsync();
    }

    // ... cleanup and disposal
}
```

### Pattern 2: Redis Backplane for SignalR Scale-out

**What:** Configure SignalR to use Redis pub/sub for multi-pod deployment
**When to use:** When running multiple Control API replicas in Kubernetes
**Example:**

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure SignalR with Redis backplane
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? "redis:6379"; // Default for Kubernetes service

builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("FileSimulator");
    });

// ... rest of configuration
```

```yaml
# helm-chart/file-simulator/templates/redis.yaml
# Source: Standard Redis deployment pattern
{{- if .Values.redis.enabled }}
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "file-simulator.fullname" . }}-redis
  namespace: {{ include "file-simulator.namespace" . }}
  labels:
    {{- include "file-simulator.labels" . | nindent 4 }}
    app.kubernetes.io/component: redis
spec:
  replicas: 1
  selector:
    matchLabels:
      {{- include "file-simulator.selectorLabels" . | nindent 6 }}
      app.kubernetes.io/component: redis
  template:
    metadata:
      labels:
        {{- include "file-simulator.selectorLabels" . | nindent 8 }}
        app.kubernetes.io/component: redis
    spec:
      containers:
        - name: redis
          image: redis:7-alpine
          ports:
            - containerPort: 6379
              name: redis
          resources:
            requests:
              memory: "64Mi"
              cpu: "50m"
            limits:
              memory: "256Mi"
              cpu: "200m"
---
apiVersion: v1
kind: Service
metadata:
  name: {{ include "file-simulator.fullname" . }}-redis
  namespace: {{ include "file-simulator.namespace" . }}
spec:
  type: ClusterIP
  ports:
    - port: 6379
      targetPort: redis
      name: redis
  selector:
    {{- include "file-simulator.selectorLabels" . | nindent 4 }}
    app.kubernetes.io/component: redis
{{- end }}
```

### Pattern 3: Multi-Stage Dockerfile for React/Vite Dashboard

**What:** Two-stage build: Node.js compiles React app, nginx serves static files
**When to use:** Production deployment of dashboard
**Example:**

```dockerfile
# src/dashboard/Dockerfile
# Source: https://dev.to/it-wibrc/guide-to-containerizing-a-modern-javascript-spa-vuevitereact-with-a-multi-stage-nginx-build-1lma
# https://www.buildwithmatija.com/blog/production-react-vite-docker-deployment

# Stage 1: Build
FROM node:20-alpine AS builder

WORKDIR /app

# Copy package files first for layer caching
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .

# Build argument for API URL (Claude's discretion: build-time injection)
ARG VITE_API_BASE_URL=http://file-simulator.local:30500
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN npm run build

# Stage 2: Production
FROM nginx:alpine AS production

# Copy built assets
COPY --from=builder /app/dist /usr/share/nginx/html

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Expose port
EXPOSE 80

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost/health || exit 1

CMD ["nginx", "-g", "daemon off;"]
```

```nginx
# src/dashboard/nginx.conf
# Source: SPA routing pattern for React Router
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;

    # Health check endpoint (for Kubernetes probes)
    location /health {
        access_log off;
        return 200 'healthy\n';
        add_header Content-Type text/plain;
    }

    # Caching headers (Claude's discretion)
    # Static assets - cache aggressively (Vite adds content hashes)
    location /assets/ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # HTML and other files - no cache (allows updates)
    location / {
        try_files $uri $uri/ /index.html;
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }

    # Gzip compression
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml;
    gzip_min_length 256;
}
```

### Pattern 4: Local Registry with Minikube

**What:** Run a local Docker registry accessible from Minikube for custom images
**When to use:** Deploying locally-built Control API and Dashboard images
**Example:**

```powershell
# Source: https://minikube.sigs.k8s.io/docs/handbook/registry/
# scripts/Deploy-Production.ps1 (partial)

# Enable registry addon (creates registry:5000 service in kube-system)
minikube addons enable registry -p file-simulator

# Forward registry port to host (run in background)
kubectl --context=file-simulator port-forward --namespace kube-system service/registry 5000:80 &

# Build and push Control API
docker build -t localhost:5000/file-simulator-control-api:latest -f src/FileSimulator.ControlApi/Dockerfile src/
docker push localhost:5000/file-simulator-control-api:latest

# Build and push Dashboard
docker build -t localhost:5000/file-simulator-dashboard:latest `
    --build-arg VITE_API_BASE_URL=http://file-simulator.local:30500 `
    -f src/dashboard/Dockerfile src/dashboard/
docker push localhost:5000/file-simulator-dashboard:latest
```

### Pattern 5: Kafka External Listener Configuration

**What:** Configure Kafka to advertise external address for outside-cluster access
**When to use:** Windows clients connecting to Kafka broker
**Note:** Already implemented in kafka.yaml, needs DNS entry for kafka.file-simulator.local

```yaml
# Already in helm-chart/file-simulator/templates/kafka.yaml
# External listener advertised as:
# EXTERNAL://file-simulator.local:30094
```

```powershell
# Setup-Hosts.ps1 update (add kafka entry)
$hostnames = @(
    $Hostname,
    "api.$Hostname",
    "dashboard.$Hostname",
    "kafka.$Hostname"  # NEW for Kafka external access
)
```

### Pattern 6: NFS Fix Incorporated into Helm Chart

**What:** Modify nas.yaml to use emptyDir for NFS export directory
**When to use:** Replace manual nfs-fix-patch.yaml application
**Example:**

```yaml
# helm-chart/file-simulator/templates/nas.yaml (modified)
# Source: DEPLOYMENT-NOTES.md NFS fix pattern
spec:
  template:
    spec:
      containers:
        - name: nfs-server
          # ... existing config ...
          volumeMounts:
            - name: nfs-data
              mountPath: /data          # NFS export (emptyDir)
            - name: shared-data
              mountPath: /shared        # Shared storage (PVC)
      volumes:
        - name: nfs-data
          emptyDir: {}                  # Ephemeral storage for NFS daemon
        - name: shared-data
          persistentVolumeClaim:
            claimName: {{ include "file-simulator.fullname" . }}-pvc
```

### Pattern 7: React Toast Notifications with Sonner

**What:** Lightweight toast notifications with severity-based duration
**When to use:** Alert notifications in dashboard
**Example:**

```typescript
// Source: https://github.com/emilkowalski/sonner
// src/dashboard/src/components/AlertToast.tsx
import { Toaster, toast } from 'sonner';
import { Alert, AlertSeverity } from '../types/alert';

// Duration based on severity (from CONTEXT.md)
const getDuration = (severity: AlertSeverity): number => {
  switch (severity) {
    case 'Info': return 5000;      // 5 seconds
    case 'Warning': return 10000;  // 10 seconds
    case 'Critical': return Infinity; // Until dismissed
  }
};

export const showAlert = (alert: Alert) => {
  const duration = getDuration(alert.severity);

  switch (alert.severity) {
    case 'Info':
      toast.info(alert.title, {
        description: alert.message,
        duration,
      });
      break;
    case 'Warning':
      toast.warning(alert.title, {
        description: alert.message,
        duration,
      });
      break;
    case 'Critical':
      toast.error(alert.title, {
        description: alert.message,
        duration,
        action: {
          label: 'Dismiss',
          onClick: () => {},
        },
      });
      break;
  }
};

// In App.tsx
export const AlertToaster: React.FC = () => (
  <Toaster
    position="bottom-right"
    richColors
    closeButton
    visibleToasts={5}
    // No sound (visual only per CONTEXT.md)
  />
);
```

### Pattern 8: React Error Boundaries

**What:** Wrap components to catch rendering errors and show fallback UI
**When to use:** Prevent single component failure from crashing entire dashboard
**Example:**

```typescript
// Source: https://blog.logrocket.com/react-error-handling-react-error-boundary/
// src/dashboard/src/components/ErrorBoundary.tsx
import { ErrorBoundary as ReactErrorBoundary, FallbackProps } from 'react-error-boundary';

const ErrorFallback: React.FC<FallbackProps> = ({ error, resetErrorBoundary }) => (
  <div className="error-fallback">
    <h2>Something went wrong</h2>
    <p>{error.message}</p>
    <button onClick={resetErrorBoundary}>Try again</button>
  </div>
);

// Log errors for debugging
const logError = (error: Error, info: { componentStack: string }) => {
  console.error('Error boundary caught:', error);
  console.error('Component stack:', info.componentStack);
};

// Wrap each major section
export const withErrorBoundary = <P extends object>(
  Component: React.ComponentType<P>,
  fallback?: React.ReactNode
) => {
  return function WrappedComponent(props: P) {
    return (
      <ReactErrorBoundary
        FallbackComponent={ErrorFallback}
        onError={logError}
        onReset={() => window.location.reload()}
      >
        <Component {...props} />
      </ReactErrorBoundary>
    );
  };
};

// Usage in App.tsx
const SafeKafkaTab = withErrorBoundary(KafkaTab);
const SafeHistoryTab = withErrorBoundary(HistoryTab);
const SafeFileBrowser = withErrorBoundary(FileBrowser);
```

### Anti-Patterns to Avoid

- **Polling for alerts in frontend:** Use SignalR push, not periodic REST calls
- **Storing alerts only in memory:** Use SQLite for persistence across restarts
- **Single global error boundary:** Use multiple boundaries around major sections
- **Building images inside Minikube VM:** Use local registry for better caching
- **Hardcoding API URLs:** Use build args or runtime environment variables
- **Skipping health endpoints:** nginx must expose /health for Kubernetes probes

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Toast notifications | Custom notification system | Sonner | Animations, accessibility, 2-3KB |
| Error boundaries | Manual try/catch everywhere | react-error-boundary | Hooks-based, fallback components |
| Redis SignalR backplane | Custom pub/sub implementation | AddStackExchangeRedis | Official Microsoft integration |
| Disk space monitoring | WMI queries | DriveInfo.AvailableFreeSpace | Cross-platform, simple API |
| Docker registry | Manual registry setup | minikube registry addon | Integrated with cluster |
| SPA routing in nginx | Complex rewrite rules | try_files $uri /index.html | Standard pattern |

## Common Pitfalls

### Pitfall 1: SignalR Connection Routing in Kubernetes

**What goes wrong:** WebSocket connections fail after negotiation when multiple Control API pods exist
**Why it happens:** Negotiate request goes to pod A, connect request goes to pod B (different connection ID)
**How to avoid:**
- Configure sticky sessions in ingress/load balancer, OR
- Skip negotiation for WebSocket-only transport (not recommended for production)
**Warning signs:** 404 errors on SignalR connect, intermittent connection failures

**Sources:** [SignalR Kubernetes](https://medium.com/swlh/scaling-signalr-core-web-applications-with-kubernetes-fca32d787c7d), [G-Research SignalR](https://www.gresearch.com/news/signalr-on-kubernetes/)

### Pitfall 2: Vite Environment Variables Not Available at Runtime

**What goes wrong:** `import.meta.env.VITE_*` variables are empty in production build
**Why it happens:** Vite replaces env vars at build time, not runtime
**How to avoid:**
- Pass as build args in Dockerfile: `ARG VITE_API_BASE_URL`
- Or use window.__CONFIG__ pattern for runtime configuration
**Warning signs:** API calls go to localhost:5000 instead of production URL

**Sources:** [Vite Docker](https://www.buildwithmatija.com/blog/production-react-vite-docker-deployment)

### Pitfall 3: nginx SPA Routing Returns 404

**What goes wrong:** Direct navigation to /history or /kafka returns 404
**Why it happens:** nginx tries to find literal file, doesn't fall back to index.html
**How to avoid:** Use `try_files $uri $uri/ /index.html;` in nginx config
**Warning signs:** Refresh on any route except / returns 404

**Sources:** [Containerizing SPA](https://dev.to/it-wibrc/guide-to-containerizing-a-modern-javascript-spa-vuevitereact-with-a-multi-stage-nginx-build-1lma)

### Pitfall 4: Minikube Registry Port Forwarding Drops

**What goes wrong:** `docker push localhost:5000/...` fails with connection refused
**Why it happens:** port-forward process terminated
**How to avoid:**
- Run port-forward in background with `&`
- Check process is running before push
- Alternative: Use `minikube docker-env` to build directly in VM
**Warning signs:** Connection refused to localhost:5000

**Sources:** [Minikube Registry](https://minikube.sigs.k8s.io/docs/handbook/registry/)

### Pitfall 5: Alert Storms from Flapping Conditions

**What goes wrong:** Rapid alert/resolve cycles flood dashboard with toasts
**Why it happens:** Threshold boundary oscillation, brief network issues
**How to avoid:**
- Debounce alerts: require condition to persist for N seconds before alerting
- Hysteresis: different thresholds for alert vs resolve (e.g., alert at 1GB, resolve at 1.5GB)
- Rate limit new alerts per type/source
**Warning signs:** Many alerts with same title in quick succession

### Pitfall 6: Dynamic NAS NodePort Collision

**What goes wrong:** Creating dynamic NAS server fails with "port already in use"
**Why it happens:** Reserved range 32150-32199 may overlap with other services
**How to avoid:**
- Track allocated ports in database
- Query Kubernetes for existing NodePorts before allocation
- Auto-increment from last used port
**Warning signs:** Server creation fails intermittently

## Code Examples

### Disk Space Health Check

```csharp
// Source: System.IO.DriveInfo API + ASP.NET Core health checks
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly long _thresholdBytes;
    private readonly string _path;

    public DiskSpaceHealthCheck(long thresholdBytes = 1L * 1024 * 1024 * 1024, string? path = null)
    {
        _thresholdBytes = thresholdBytes;
        _path = path ?? (OperatingSystem.IsWindows() ? @"C:\simulator-data" : "/mnt/simulator-data");
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_path)!);
            var availableBytes = drive.AvailableFreeSpace;
            var totalBytes = drive.TotalSize;
            var percentFree = (double)availableBytes / totalBytes * 100;

            var data = new Dictionary<string, object>
            {
                ["AvailableBytes"] = availableBytes,
                ["TotalBytes"] = totalBytes,
                ["PercentFree"] = percentFree,
                ["Path"] = _path
            };

            if (availableBytes < _thresholdBytes)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space: {availableBytes / (1024 * 1024)}MB available (threshold: {_thresholdBytes / (1024 * 1024 * 1024)}GB)",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space OK: {percentFree:F1}% free",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Could not check disk space: {ex.Message}"));
        }
    }
}
```

### Setup-Hosts.ps1 Update Mechanism (Claude's Discretion)

```powershell
# Dynamic entries support for Setup-Hosts.ps1
# Call connection-info API and add dynamic NAS entries

param(
    [string]$Profile = "file-simulator",
    [string]$Hostname = "file-simulator.local",
    [switch]$IncludeDynamic  # NEW: fetch dynamic servers from API
)

# ... existing code ...

# Define static hostnames
$hostnames = @(
    $Hostname,
    "api.$Hostname",
    "dashboard.$Hostname",
    "kafka.$Hostname"
)

# Add dynamic NAS entries if requested
if ($IncludeDynamic) {
    try {
        $connectionInfo = Invoke-RestMethod -Uri "http://${Hostname}:30500/api/connection-info" -TimeoutSec 5
        $dynamicNas = $connectionInfo.servers | Where-Object { $_.protocol -eq 'NFS' -and $_.isDynamic }

        foreach ($nas in $dynamicNas) {
            $nasHostname = "nas-$($nas.name).$Hostname"
            $hostnames += $nasHostname
            Write-Host "  Adding dynamic NAS: $nasHostname" -ForegroundColor Cyan
        }
    } catch {
        Write-Host "Warning: Could not fetch dynamic servers from API" -ForegroundColor Yellow
    }
}

# ... rest of existing code ...
```

### Deploy-Production.ps1 Script Structure

```powershell
<#
.SYNOPSIS
    Full production deployment of File Simulator Suite to Minikube
.PARAMETER Clean
    Delete and recreate the Minikube cluster (default: false)
#>
param(
    [switch]$Clean
)

$ErrorActionPreference = "Stop"  # Fail fast

Write-Host "=== File Simulator Production Deployment ===" -ForegroundColor Cyan

# Step 1: Cluster management
if ($Clean) {
    Write-Host "`n[1/8] Deleting existing cluster..." -ForegroundColor Yellow
    minikube delete -p file-simulator 2>$null

    Write-Host "[1/8] Creating fresh cluster..." -ForegroundColor Yellow
    minikube start `
        --profile file-simulator `
        --driver=hyperv `
        --memory=8192 `
        --cpus=4 `
        --disk-size=20g `
        --mount `
        --mount-string="C:\simulator-data:/mnt/simulator-data"
} else {
    Write-Host "`n[1/8] Verifying cluster..." -ForegroundColor Yellow
    $status = minikube status -p file-simulator 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Cluster 'file-simulator' not running. Use -Clean to create it."
    }
}

# Step 2: Enable registry addon
Write-Host "`n[2/8] Enabling registry addon..." -ForegroundColor Yellow
minikube addons enable registry -p file-simulator

# Step 3: Start port forward for registry
Write-Host "`n[3/8] Starting registry port forward..." -ForegroundColor Yellow
$job = Start-Job -ScriptBlock {
    kubectl --context=file-simulator port-forward --namespace kube-system service/registry 5000:80
}
Start-Sleep -Seconds 3

# Step 4: Build and push Control API
Write-Host "`n[4/8] Building Control API image..." -ForegroundColor Yellow
docker build -t localhost:5000/file-simulator-control-api:latest `
    -f src/FileSimulator.ControlApi/Dockerfile src/
if ($LASTEXITCODE -ne 0) { throw "Control API build failed" }

docker push localhost:5000/file-simulator-control-api:latest
if ($LASTEXITCODE -ne 0) { throw "Control API push failed" }

# Step 5: Build and push Dashboard
Write-Host "`n[5/8] Building Dashboard image..." -ForegroundColor Yellow
docker build -t localhost:5000/file-simulator-dashboard:latest `
    --build-arg VITE_API_BASE_URL=http://file-simulator.local:30500 `
    -f src/dashboard/Dockerfile src/dashboard/
if ($LASTEXITCODE -ne 0) { throw "Dashboard build failed" }

docker push localhost:5000/file-simulator-dashboard:latest
if ($LASTEXITCODE -ne 0) { throw "Dashboard push failed" }

# Step 6: Deploy Helm chart
Write-Host "`n[6/8] Deploying Helm chart..." -ForegroundColor Yellow
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --create-namespace `
    --wait `
    --timeout 5m

# Step 7: Update hosts file
Write-Host "`n[7/8] Updating hosts file..." -ForegroundColor Yellow
.\scripts\Setup-Hosts.ps1 -IncludeDynamic

# Step 8: Verify deployment
Write-Host "`n[8/8] Verifying deployment..." -ForegroundColor Yellow
.\scripts\Verify-Production.ps1

# Cleanup
Stop-Job $job
Remove-Job $job

Write-Host "`n=== Deployment Complete ===" -ForegroundColor Green
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| react-toastify for all projects | Sonner for modern React 18+ | 2023+ | Smaller bundle, better animations |
| Class-based error boundaries | react-error-boundary hooks | React 18+ | Simpler API, functional components |
| ZooKeeper for Kafka | KRaft mode (already implemented) | Kafka 3.3+ | Simpler deployment |
| Manual nfs-fix-patch.yaml | Helm chart emptyDir pattern | This phase | Eliminates post-deploy step |
| minikube docker-env | Local registry addon | Mature pattern | Better caching, persists across restarts |
| REST polling for alerts | SignalR push | Existing pattern | Real-time, lower overhead |

**Deprecated/outdated:**
- **ALLOW_PLAINTEXT_LISTENER in Bitnami Kafka:** Removed, plaintext allowed by default
- **Pre-React 18 error boundaries:** Use react-error-boundary package instead
- **Manual registry:2 container:** Use minikube registry addon

## Open Questions

1. **Multi-Pod Control API without Redis**
   - What we know: Current deployment uses single replica
   - What's unclear: Whether Redis backplane is needed for Phase 12 or future phases
   - Recommendation: Include Redis in Helm chart but disabled by default. Enable when scaling Control API replicas >1.

2. **Alert Deduplication Window**
   - What we know: Don't want duplicate alerts for same condition
   - What's unclear: How long to suppress re-alerting after resolution
   - Recommendation: 5-minute cooldown after resolution before same alert can re-trigger.

3. **Dynamic NAS DNS Update Frequency**
   - What we know: Setup-Hosts.ps1 -IncludeDynamic fetches from API
   - What's unclear: Should this be automated or manual?
   - Recommendation: Manual (user runs script after creating dynamic servers). Automation adds complexity without significant benefit for dev environment.

## Sources

### Primary (HIGH confidence)
- [Microsoft SignalR Redis Backplane](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane) - Official documentation
- [Kubernetes Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/) - Official documentation
- [Minikube Registry](https://minikube.sigs.k8s.io/docs/handbook/registry/) - Official documentation
- [Sonner GitHub](https://github.com/emilkowalski/sonner) - Official repository
- [react-error-boundary](https://github.com/bvaughn/react-error-boundary) - Official repository
- [DriveInfo.AvailableFreeSpace](https://learn.microsoft.com/en-us/dotnet/api/system.io.driveinfo.availablefreespace) - Official .NET documentation

### Secondary (MEDIUM confidence)
- [SignalR on Kubernetes](https://medium.com/swlh/scaling-signalr-core-web-applications-with-kubernetes-fca32d787c7d) - Community patterns
- [React Vite Docker Deployment](https://www.buildwithmatija.com/blog/production-react-vite-docker-deployment) - Verified patterns
- [Containerizing SPA with Nginx](https://dev.to/it-wibrc/guide-to-containerizing-a-modern-javascript-spa-vuevitereact-with-a-multi-stage-nginx-build-1lma) - Community guide
- [LogRocket React Error Boundary](https://blog.logrocket.com/react-error-handling-react-error-boundary/) - Best practices
- [Knock Notification Libraries](https://knock.app/blog/the-top-notification-libraries-for-react) - 2026 comparison

### Tertiary (LOW confidence)
- [Kafka External Access](https://github.com/helm/charts/issues/12876) - Issue discussion for patterns
- [Strimzi Kafka NodePorts](https://strimzi.io/blog/2019/04/23/accessing-kafka-part-2/) - Older but patterns still valid

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official packages, well-documented APIs
- Architecture patterns: HIGH - Based on existing codebase patterns and official docs
- Pitfalls: MEDIUM - Mix of official docs and community experience
- Containerization: HIGH - Standard multi-stage Dockerfile patterns

**Research date:** 2026-02-04
**Valid until:** ~60 days (stable domain - Kubernetes, SignalR, React patterns change slowly)

**Notes:**
- Redis backplane optional for single-replica deployment (current state)
- NFS fix incorporated into Helm eliminates manual patch step
- Sonner chosen over react-toastify for smaller bundle size
- Dashboard Dockerfile must be created (new file)
- nginx.conf must be created for SPA routing
