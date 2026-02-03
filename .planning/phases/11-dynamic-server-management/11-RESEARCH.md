# Phase 11: Dynamic Server Management - Research

**Researched:** 2026-02-03
**Domain:** Kubernetes dynamic resource management, configuration export/import, React UI patterns
**Confidence:** HIGH

## Summary

This phase enables runtime creation/deletion of FTP, SFTP, and NAS servers through a React dashboard, with full configuration export/import for environment replication. The implementation requires extending existing KubernetesClient integration (Phase 6) to support write operations, implementing ownerReferences for automatic cleanup, updating ConfigMaps for service discovery, and building comprehensive UI workflows for server lifecycle management.

The standard approach is using the official KubernetesClient library for C# to create/delete Kubernetes resources programmatically, with ownerReferences ensuring garbage collection prevents orphaned resources. NodePort allocation uses Kubernetes' automatic assignment within the 30000-32767 range, with manual override capability. Configuration export/import follows JSON file download/upload patterns common in React applications.

**Primary recommendation:** Leverage existing KubernetesDiscoveryService and Helm templates as blueprints for dynamic deployments. Extend RBAC permissions to include create/update/delete verbs, implement server creation as a stateless API operation (no controller loop), and use React's useState for multi-select operations with optimistic UI updates.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| KubernetesClient | 18.0.13 | Kubernetes API integration | Official C# client, already in use (Phase 6), supports full CRUD operations |
| k8s.Models.* | 18.0.13 | Kubernetes object models | Part of KubernetesClient, provides V1Deployment, V1Service, V1ConfigMap, etc. |
| System.Text.Json | 9.0 | JSON serialization | Built into .NET 9, handles config export/import |
| FluentValidation | 11.x | Request validation | Industry standard for complex validation rules in .NET |
| React useState | 19 | Multi-select state | Built-in hook, already in use (Phase 7) |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FluentValidation.DependencyInjectionExtensions | 11.x | DI registration | For validator registration in ASP.NET Core |
| react-hook-form | 7.x (optional) | Form state management | If server creation form becomes complex (10+ fields) |
| File-saver | 2.x (optional) | Browser file downloads | Alternative to native Blob API for cross-browser compatibility |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| KubernetesClient direct API | Helm SDK for .NET | Helm SDK more appropriate for templating, but adds dependency; direct API gives full control for runtime operations |
| Manual JSON export | System.Configuration serialization | Configuration classes provide type safety but less flexible for mixed static/dynamic state |
| Built-in validation | Custom validation logic | FluentValidation more maintainable at scale but adds package; inline validation simpler for basic rules |

**Installation:**
```bash
# Backend (already installed)
cd src/FileSimulator.ControlApi
dotnet add package KubernetesClient --version 18.0.13  # Already present
dotnet add package FluentValidation.DependencyInjectionExtensions --version 11.9.0

# Frontend (no new packages required)
cd dashboard
# Multi-select uses existing React useState
# File download uses native Blob API (already in browser)
```

## Architecture Patterns

### Recommended Project Structure
```
src/FileSimulator.ControlApi/
├── Services/
│   ├── IKubernetesDiscoveryService.cs      # Existing (Phase 6)
│   ├── KubernetesDiscoveryService.cs       # Existing (Phase 6)
│   ├── IKubernetesManagementService.cs     # NEW - CRUD operations
│   ├── KubernetesManagementService.cs      # NEW - Create/delete deployments
│   └── ConfigurationExportService.cs       # NEW - Export/import logic
├── Models/
│   ├── DiscoveredServer.cs                 # Existing (Phase 6)
│   ├── CreateServerRequest.cs              # NEW - Creation payload
│   ├── ServerConfiguration.cs              # NEW - Export format
│   └── ServerConfigurationExport.cs        # NEW - Full export wrapper
├── Validators/
│   ├── CreateFtpServerValidator.cs         # NEW - FTP validation rules
│   ├── CreateSftpServerValidator.cs        # NEW - SFTP validation rules
│   └── CreateNasServerValidator.cs         # NEW - NAS validation rules
└── Controllers/
    ├── ServersController.cs                # NEW - CRUD endpoints
    └── ConfigurationController.cs          # NEW - Export/import endpoints

dashboard/src/
├── components/
│   ├── ServerGrid.tsx                      # Existing (Phase 7) - ADD multi-select
│   ├── ServerCard.tsx                      # Existing (Phase 7) - ADD dynamic badge
│   ├── CreateServerModal.tsx               # NEW - Server creation wizard
│   ├── DeleteConfirmDialog.tsx             # NEW - Deletion confirmation
│   ├── ImportConfigDialog.tsx              # NEW - Import conflict resolution
│   └── BatchOperationsBar.tsx              # NEW - Multi-select actions
├── hooks/
│   ├── useServerManagement.ts              # NEW - CRUD API calls
│   ├── useConfigExport.ts                  # NEW - Export/import logic
│   └── useMultiSelect.ts                   # NEW - Multi-select state
└── types/
    └── serverManagement.ts                 # NEW - Request/response types
```

### Pattern 1: Dynamic Deployment Creation with OwnerReferences

**What:** Creating Kubernetes Deployment and Service at runtime with ownerReferences to control plane pod
**When to use:** Server creation API endpoint
**Example:**
```csharp
// Source: KubernetesClient library patterns + Kubernetes official docs
public async Task<DiscoveredServer> CreateFtpServerAsync(
    CreateFtpServerRequest request,
    CancellationToken ct)
{
    // 1. Get control plane pod for ownerReference
    var controlPlanePod = await GetControlPlanePodAsync(ct);

    // 2. Build deployment from template
    var deployment = new V1Deployment
    {
        Metadata = new V1ObjectMeta
        {
            Name = $"{_options.ReleasePrefix}-ftp-{request.Name}",
            NamespaceProperty = _options.Namespace,
            Labels = new Dictionary<string, string>
            {
                ["app.kubernetes.io/name"] = "file-simulator",
                ["app.kubernetes.io/component"] = "ftp",
                ["app.kubernetes.io/managed-by"] = "control-api",
                ["app.kubernetes.io/instance"] = request.Name
            },
            OwnerReferences = new List<V1OwnerReference>
            {
                new V1OwnerReference
                {
                    ApiVersion = "v1",
                    Kind = "Pod",
                    Name = controlPlanePod.Metadata.Name,
                    Uid = controlPlanePod.Metadata.Uid,
                    Controller = true,  // Enables cascading deletion
                    BlockOwnerDeletion = true
                }
            }
        },
        Spec = new V1DeploymentSpec
        {
            Replicas = 1,
            Selector = new V1LabelSelector
            {
                MatchLabels = new Dictionary<string, string>
                {
                    ["app.kubernetes.io/name"] = "file-simulator",
                    ["app.kubernetes.io/instance"] = request.Name
                }
            },
            Template = new V1PodTemplateSpec
            {
                Metadata = new V1ObjectMeta
                {
                    Labels = new Dictionary<string, string>
                    {
                        ["app.kubernetes.io/name"] = "file-simulator",
                        ["app.kubernetes.io/component"] = "ftp",
                        ["app.kubernetes.io/instance"] = request.Name
                    }
                },
                Spec = new V1PodSpec
                {
                    Containers = new List<V1Container>
                    {
                        new V1Container
                        {
                            Name = "vsftpd",
                            Image = "fauria/vsftpd:latest",
                            Ports = new List<V1ContainerPort>
                            {
                                new V1ContainerPort { ContainerPort = 21, Protocol = "TCP" }
                            },
                            Env = new List<V1EnvVar>
                            {
                                new V1EnvVar { Name = "FTP_USER", Value = request.Username },
                                new V1EnvVar { Name = "FTP_PASS", Value = request.Password }
                            },
                            VolumeMounts = new List<V1VolumeMount>
                            {
                                new V1VolumeMount
                                {
                                    Name = "data",
                                    MountPath = $"/home/vsftpd/{request.Username}"
                                }
                            }
                        }
                    },
                    Volumes = new List<V1Volume>
                    {
                        new V1Volume
                        {
                            Name = "data",
                            PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
                            {
                                ClaimName = $"{_options.ReleasePrefix}-pvc"
                            }
                        }
                    }
                }
            }
        }
    };

    // 3. Create deployment
    var createdDeployment = await _client.AppsV1.CreateNamespacedDeploymentAsync(
        deployment,
        _options.Namespace,
        cancellationToken: ct);

    // 4. Create service with auto-assigned or specified NodePort
    var service = new V1Service
    {
        Metadata = new V1ObjectMeta
        {
            Name = $"{_options.ReleasePrefix}-ftp-{request.Name}",
            NamespaceProperty = _options.Namespace,
            Labels = deployment.Metadata.Labels,
            OwnerReferences = deployment.Metadata.OwnerReferences  // Same owner
        },
        Spec = new V1ServiceSpec
        {
            Type = "NodePort",
            Selector = deployment.Spec.Selector.MatchLabels,
            Ports = new List<V1ServicePort>
            {
                new V1ServicePort
                {
                    Port = 21,
                    TargetPort = 21,
                    Protocol = "TCP",
                    NodePort = request.NodePort  // null = auto-assign, int = specific
                }
            }
        }
    };

    var createdService = await _client.CoreV1.CreateNamespacedServiceAsync(
        service,
        _options.Namespace,
        cancellationToken: ct);

    // 5. Update ConfigMap for service discovery
    await UpdateServiceDiscoveryConfigMapAsync(ct);

    // 6. Return created server info
    return new DiscoveredServer
    {
        Name = request.Name,
        Protocol = "FTP",
        PodName = createdDeployment.Metadata.Name,
        ServiceName = createdService.Metadata.Name,
        NodePort = createdService.Spec.Ports[0].NodePort,
        ClusterIp = createdService.Spec.ClusterIP,
        IsDynamic = true
    };
}
```

### Pattern 2: Cascading Deletion with Explicit Cleanup

**What:** Delete deployment and explicitly clean up service, PVC (services/PVCs don't auto-cascade)
**When to use:** Server deletion API endpoint
**Example:**
```csharp
// Source: Kubernetes garbage collection docs + client library patterns
public async Task DeleteServerAsync(string serverName, bool deleteData, CancellationToken ct)
{
    var labelSelector = $"app.kubernetes.io/instance={serverName}";

    // 1. Delete deployment (pods cascade automatically via ownerReferences)
    var deployments = await _client.AppsV1.ListNamespacedDeploymentAsync(
        _options.Namespace,
        labelSelector: labelSelector,
        cancellationToken: ct);

    foreach (var deployment in deployments.Items)
    {
        await _client.AppsV1.DeleteNamespacedDeploymentAsync(
            deployment.Metadata.Name,
            _options.Namespace,
            propagationPolicy: "Foreground",  // Wait for pods to terminate
            cancellationToken: ct);
    }

    // 2. Delete service explicitly (does NOT cascade from deployment)
    var services = await _client.CoreV1.ListNamespacedServiceAsync(
        _options.Namespace,
        labelSelector: labelSelector,
        cancellationToken: ct);

    foreach (var service in services.Items)
    {
        await _client.CoreV1.DeleteNamespacedServiceAsync(
            service.Metadata.Name,
            _options.Namespace,
            cancellationToken: ct);
    }

    // 3. Delete PVC if requested (for NAS servers with dedicated storage)
    if (deleteData)
    {
        var pvcs = await _client.CoreV1.ListNamespacedPersistentVolumeClaimAsync(
            _options.Namespace,
            labelSelector: labelSelector,
            cancellationToken: ct);

        foreach (var pvc in pvcs.Items)
        {
            await _client.CoreV1.DeleteNamespacedPersistentVolumeClaimAsync(
                pvc.Metadata.Name,
                _options.Namespace,
                cancellationToken: ct);
        }
    }

    // 4. Update ConfigMap for service discovery
    await UpdateServiceDiscoveryConfigMapAsync(ct);

    _logger.LogInformation(
        "Deleted server {ServerName} (deleteData: {DeleteData})",
        serverName, deleteData);
}
```

### Pattern 3: ConfigMap Update for Service Discovery

**What:** Update ConfigMap with current server list after add/delete operations
**When to use:** After any server lifecycle operation
**Example:**
```csharp
// Source: KubernetesClient patterns
private async Task UpdateServiceDiscoveryConfigMapAsync(CancellationToken ct)
{
    var configMapName = $"{_options.ReleasePrefix}-endpoints";

    // 1. Discover all current servers
    var servers = await _discoveryService.DiscoverServersAsync(ct);

    // 2. Build ConfigMap data
    var configData = new Dictionary<string, string>();

    foreach (var server in servers)
    {
        var key = $"{server.Protocol}_{server.Name}".Replace("-", "_").ToUpper();
        var value = $"{server.ServiceName}.{_options.Namespace}.svc.cluster.local:{server.Port}";
        configData[key] = value;
    }

    // 3. Update or create ConfigMap
    try
    {
        var existingConfigMap = await _client.CoreV1.ReadNamespacedConfigMapAsync(
            configMapName,
            _options.Namespace,
            cancellationToken: ct);

        existingConfigMap.Data = configData;

        await _client.CoreV1.ReplaceNamespacedConfigMapAsync(
            existingConfigMap,
            configMapName,
            _options.Namespace,
            cancellationToken: ct);
    }
    catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        var configMap = new V1ConfigMap
        {
            Metadata = new V1ObjectMeta
            {
                Name = configMapName,
                NamespaceProperty = _options.Namespace
            },
            Data = configData
        };

        await _client.CoreV1.CreateNamespacedConfigMapAsync(
            configMap,
            _options.Namespace,
            cancellationToken: ct);
    }

    _logger.LogInformation("Updated service discovery ConfigMap with {Count} servers", servers.Count);
}
```

### Pattern 4: Configuration Export/Import

**What:** Export full simulator state (static + dynamic servers) to JSON, import with conflict resolution
**When to use:** Configuration backup/restore endpoints
**Example:**
```csharp
// Source: System.Text.Json + ASP.NET Core file download patterns
public async Task<ServerConfigurationExport> ExportConfigurationAsync(CancellationToken ct)
{
    var servers = await _discoveryService.DiscoverServersAsync(ct);

    var export = new ServerConfigurationExport
    {
        Version = "2.0",
        ExportedAt = DateTime.UtcNow,
        Namespace = _options.Namespace,
        Servers = servers.Select(s => new ServerConfiguration
        {
            Name = s.Name,
            Protocol = s.Protocol,
            NodePort = s.NodePort,
            // ... all configuration from server specs
            IsDynamic = s.IsDynamic
        }).ToList()
    };

    return export;
}

// ASP.NET Core endpoint
app.MapGet("/api/configuration/export", async (
    ConfigurationExportService exportService,
    CancellationToken ct) =>
{
    var config = await exportService.ExportConfigurationAsync(ct);
    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    return Results.File(
        Encoding.UTF8.GetBytes(json),
        "application/json",
        $"file-simulator-config-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
});

// Import with validation
public async Task<ImportResult> ImportConfigurationAsync(
    ServerConfigurationExport config,
    ConflictResolutionStrategy strategy,
    CancellationToken ct)
{
    var result = new ImportResult();
    var existingServers = await _discoveryService.DiscoverServersAsync(ct);

    foreach (var serverConfig in config.Servers)
    {
        var existing = existingServers.FirstOrDefault(s =>
            s.Name == serverConfig.Name ||
            s.NodePort == serverConfig.NodePort);

        if (existing != null)
        {
            // Conflict handling based on strategy
            switch (strategy)
            {
                case ConflictResolutionStrategy.Skip:
                    result.Skipped.Add(serverConfig.Name);
                    continue;

                case ConflictResolutionStrategy.Replace:
                    await _managementService.DeleteServerAsync(existing.Name, false, ct);
                    break;

                case ConflictResolutionStrategy.Rename:
                    serverConfig.Name = $"{serverConfig.Name}-imported";
                    serverConfig.NodePort = null;  // Auto-assign new port
                    break;
            }
        }

        try
        {
            await _managementService.CreateServerAsync(serverConfig, ct);
            result.Created.Add(serverConfig.Name);
        }
        catch (Exception ex)
        {
            result.Failed.Add(serverConfig.Name, ex.Message);
        }
    }

    return result;
}
```

### Pattern 5: React Multi-Select with Optimistic UI

**What:** Multi-select with checkboxes and batch operations with optimistic updates
**When to use:** Server grid with multi-delete functionality
**Example:**
```typescript
// Source: React best practices + community patterns
export const useMultiSelect = (servers: DiscoveredServer[]) => {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const toggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const selectAll = () => {
    const dynamicServerIds = servers
      .filter(s => s.isDynamic)
      .map(s => s.name);
    setSelectedIds(new Set(dynamicServerIds));
  };

  const clearSelection = () => {
    setSelectedIds(new Set());
  };

  const isSelected = (id: string) => selectedIds.has(id);

  return {
    selectedIds,
    toggleSelect,
    selectAll,
    clearSelection,
    isSelected,
    selectedCount: selectedIds.size
  };
};

// Component usage
export const ServerGrid: React.FC = () => {
  const { servers, deleteServer, isDeleting } = useServerManagement();
  const { selectedIds, toggleSelect, clearSelection, isSelected, selectedCount } =
    useMultiSelect(servers);

  const handleBatchDelete = async () => {
    if (!confirm(`Delete ${selectedCount} servers?`)) return;

    // Optimistic update: remove from UI immediately
    const toDelete = Array.from(selectedIds);

    try {
      // Call API for each
      await Promise.all(toDelete.map(id => deleteServer(id, false)));
      clearSelection();
    } catch (error) {
      // On error, UI will revert via SWR/React Query refetch
      console.error('Batch delete failed:', error);
    }
  };

  return (
    <>
      {selectedCount > 0 && (
        <BatchOperationsBar
          count={selectedCount}
          onDelete={handleBatchDelete}
          onCancel={clearSelection}
        />
      )}

      <div className="server-grid">
        {servers.map(server => (
          <ServerCard
            key={server.name}
            server={server}
            showCheckbox={server.isDynamic}
            isSelected={isSelected(server.name)}
            onToggleSelect={() => toggleSelect(server.name)}
          />
        ))}
      </div>
    </>
  );
};
```

### Pattern 6: Inline Editing with Validation

**What:** Inline field editing with immediate validation feedback
**When to use:** Server details panel for post-creation configuration updates
**Example:**
```typescript
// Source: React inline editing patterns + Material UI examples
export const InlineEditField: React.FC<{
  value: string;
  onSave: (newValue: string) => Promise<void>;
  validate?: (value: string) => string | null;
  label: string;
}> = ({ value, onSave, validate, label }) => {
  const [isEditing, setIsEditing] = useState(false);
  const [editValue, setEditValue] = useState(value);
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const handleSave = async () => {
    // Validate
    const validationError = validate?.(editValue);
    if (validationError) {
      setError(validationError);
      return;
    }

    setIsSaving(true);
    try {
      await onSave(editValue);
      setIsEditing(false);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Save failed');
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    setEditValue(value);
    setError(null);
    setIsEditing(false);
  };

  if (!isEditing) {
    return (
      <div className="inline-field">
        <label>{label}</label>
        <span className="inline-field__value" onClick={() => setIsEditing(true)}>
          {value}
        </span>
      </div>
    );
  }

  return (
    <div className="inline-field inline-field--editing">
      <label>{label}</label>
      <input
        value={editValue}
        onChange={e => setEditValue(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter') handleSave();
          if (e.key === 'Escape') handleCancel();
        }}
        disabled={isSaving}
        autoFocus
      />
      {error && <span className="inline-field__error">{error}</span>}
      <div className="inline-field__actions">
        <button onClick={handleSave} disabled={isSaving}>
          {isSaving ? 'Saving...' : 'Save'}
        </button>
        <button onClick={handleCancel} disabled={isSaving}>
          Cancel
        </button>
      </div>
    </div>
  );
};
```

### Anti-Patterns to Avoid

- **Controller-based approach:** Don't implement a continuous reconciliation loop like Kubernetes operators. This phase is stateless API operations, not a controller. Complexity outweighs benefits for this use case.
- **Storing state in memory:** Don't cache server configurations in service memory. Always query Kubernetes API as source of truth to avoid drift when pods restart.
- **Synchronous validation:** Don't validate NodePort availability by querying existing services synchronously before creation. Let Kubernetes reject conflicts; handle the error response.
- **Deleting Helm-managed resources:** Don't allow deletion of static servers via UI. Check for `app.kubernetes.io/managed-by: Helm` label and block operation.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| YAML template generation | String concatenation or manual builders | Existing Helm templates as C# models | Edge cases in escaping, indentation, multi-line strings; K8s client library handles serialization |
| NodePort conflict detection | Pre-check service list for port conflicts | Kubernetes automatic allocation | Race conditions between check and create; K8s handles atomically |
| ConfigMap watching | Poll ConfigMap every N seconds | Application-level service discovery cache | ConfigMap updates don't trigger pod restarts; apps must implement own watch/refresh |
| File upload size limits | JavaScript-side validation only | Kestrel MaxRequestBodySize + client validation | Server must enforce limit; client validation is UX enhancement only |
| JSON schema validation | Custom validators per field | FluentValidation with rules | Complex cross-field validation (e.g., port ranges), rule reuse, clear error messages |
| React form state | Individual useState per field | Single state object with reducer | Complex forms with 10+ fields become unwieldy; reduces re-renders |

**Key insight:** Kubernetes API provides atomic operations with built-in conflict detection. Trust the API to reject invalid states rather than building elaborate pre-validation. For dynamic systems, defensive pre-checks add latency and introduce race conditions without eliminating the need for error handling.

## Common Pitfalls

### Pitfall 1: OwnerReferences Pointing to Wrong Resource

**What goes wrong:** Setting ownerReference to Deployment instead of control plane Pod causes resources to persist when control plane restarts
**Why it happens:** Misunderstanding of ownership chain - dynamically created deployments should be owned by control plane pod, not by each other
**How to avoid:**
- Always query current control plane pod (`kubectl.CoreV1.ListNamespacedPodAsync` with label selector)
- Set ownerReference.Controller = true for cascading deletion
- Verify with: `kubectl --context=file-simulator get deployment <name> -o jsonpath='{.metadata.ownerReferences}'`
**Warning signs:** Deployments remain after control plane pod deleted/recreated; orphaned resources accumulate

### Pitfall 2: Services and PVCs Don't Cascade Automatically

**What goes wrong:** Deleting deployment leaves orphaned services and PVCs consuming ports and storage
**Why it happens:** Kubernetes garbage collection only cascades pods/replicasets from deployments; services/PVCs are independent resources
**How to avoid:**
- Explicitly delete services by label selector after deployment deletion
- For PVCs, ask user if data should be kept (NAS servers) vs deleted (protocol servers)
- Use try-finally or structured cleanup to ensure all resources deleted even on partial failure
**Warning signs:** NodePorts remain allocated after server deletion; storage quota consumed by unused PVCs

### Pitfall 3: RBAC Permissions Insufficient for Write Operations

**What goes wrong:** Create/delete operations return 403 Forbidden despite successful read operations
**Why it happens:** Phase 6 RBAC only granted `get, list, watch` verbs; Phase 11 needs `create, update, delete`
**How to avoid:**
- Update values.yaml controlApi.rbac.rules to include write verbs
- Test with: `kubectl --context=file-simulator auth can-i create deployments --as=system:serviceaccount:file-simulator:file-sim-file-simulator-control-api -n file-simulator`
- Apply updated RBAC before deploying Phase 11 code
**Warning signs:** All create/delete endpoints return 403; logs show "Forbidden" from Kubernetes API

### Pitfall 4: NodePort Range Exhaustion

**What goes wrong:** Server creation fails with "port allocation failed" when NodePort range (30000-32767) is exhausted
**Why it happens:** Minikube default range has ~2767 ports; each FTP server uses 1 port, but multi-FTP deployments from v1.0 may consume many
**How to avoid:**
- Implement resource quota: hard limit on max dynamic servers (e.g., 10 FTP, 10 SFTP, 5 NAS)
- Track NodePort usage in dashboard: show "X of Y ports available"
- Provide clear error message: "NodePort range exhausted. Delete unused servers or increase cluster range."
**Warning signs:** Intermittent creation failures; error message about port allocation

### Pitfall 5: ConfigMap Update Race Condition

**What goes wrong:** Rapid create/delete operations cause ConfigMap to show incorrect server list
**Why it happens:** Concurrent updates to same ConfigMap resource; last write wins, earlier updates lost
**How to avoid:**
- Use optimistic concurrency: read ConfigMap resourceVersion, use in update, handle conflicts
- Alternative: Queue ConfigMap updates through background service (debounce 1-2 seconds)
- For MVP: Accept eventual consistency - applications poll ConfigMap anyway
**Warning signs:** ConfigMap sometimes missing newly created servers; requires manual refresh to show accurate state

### Pitfall 6: Inline Edit Applies Changes Immediately

**What goes wrong:** User edits NodePort in details panel, presses Enter, expects preview but changes apply to cluster
**Why it happens:** Confusion between "inline edit" (apply immediately) vs "edit mode" (apply on Save button)
**How to avoid:**
- Show visual feedback during save: spinner, disable input, success toast
- For destructive changes (NodePort), show confirmation: "Change port 30021 → 30055? This will restart the server."
- Implement undo: store previous value, offer "Undo" toast for 5 seconds after save
**Warning signs:** User complaints about accidental changes; requests for "edit without saving" mode

### Pitfall 7: Deleting Static Servers Breaks Helm

**What goes wrong:** User deletes Helm-managed server via UI, subsequent `helm upgrade` fails with resource conflicts
**Why it happens:** Helm tracks resources; manual deletion causes drift between Helm state and cluster state
**How to avoid:**
- Check for `app.kubernetes.io/managed-by: Helm` label before deletion
- Show "Managed by Helm" badge on static servers; disable delete button
- Alternative: Allow deletion but warn "This will be recreated by Helm on next deployment"
**Warning signs:** User reports "server keeps coming back" after deletion; Helm commands fail with "resource already exists"

### Pitfall 8: Import Overwrites Production Servers

**What goes wrong:** User exports config from test environment, imports to production, overwrites production servers
**Why it happens:** Import doesn't distinguish between environments; blindly replaces matching servers
**How to avoid:**
- Show import preview: list of servers to create/replace/skip BEFORE applying
- Default to "Skip" strategy for conflicts; require explicit user choice for "Replace"
- Add environment label to exports: warn if importing dev config to prod namespace
**Warning signs:** Production servers suddenly have test credentials; data loss reports

## Code Examples

Verified patterns from official sources:

### Querying Control Plane Pod for OwnerReference
```csharp
// Source: KubernetesClient documentation
private async Task<V1Pod> GetControlPlanePodAsync(CancellationToken ct)
{
    var pods = await _client.CoreV1.ListNamespacedPodAsync(
        _options.Namespace,
        labelSelector: "app.kubernetes.io/component=control-api",
        cancellationToken: ct);

    var controlPod = pods.Items.FirstOrDefault(p =>
        p.Status.Phase == "Running" &&
        p.Metadata.Name.Contains("control-api"));

    if (controlPod == null)
        throw new InvalidOperationException("Control plane pod not found or not running");

    return controlPod;
}
```

### FluentValidation for Server Creation
```csharp
// Source: FluentValidation documentation + ASP.NET Core integration
public class CreateFtpServerValidator : AbstractValidator<CreateFtpServerRequest>
{
    public CreateFtpServerValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .Matches("^[a-z0-9-]+$").WithMessage("Name must be lowercase alphanumeric with hyphens")
            .Length(3, 32);

        RuleFor(x => x.NodePort)
            .InclusiveBetween(30000, 32767).When(x => x.NodePort.HasValue)
            .WithMessage("NodePort must be in range 30000-32767");

        RuleFor(x => x.Username)
            .NotEmpty()
            .Length(3, 32);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters");
    }
}

// Registration in Program.cs
builder.Services.AddValidatorsFromAssemblyContaining<CreateFtpServerValidator>();

// Usage in endpoint with endpoint filter
app.MapPost("/api/servers/ftp", async (
    CreateFtpServerRequest request,
    IValidator<CreateFtpServerRequest> validator,
    IKubernetesManagementService management,
    CancellationToken ct) =>
{
    var validation = await validator.ValidateAsync(request, ct);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(validation.ToDictionary());
    }

    var server = await management.CreateFtpServerAsync(request, ct);
    return Results.Created($"/api/servers/{server.Name}", server);
});
```

### React File Download/Upload for Config Export/Import
```typescript
// Source: Native browser APIs + React patterns
// Export - file download
export const useConfigExport = () => {
  const exportConfig = async () => {
    const response = await fetch('/api/configuration/export');
    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `file-simulator-config-${new Date().toISOString()}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
  };

  return { exportConfig };
};

// Import - file upload
export const useConfigImport = () => {
  const [isImporting, setIsImporting] = useState(false);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);

  const importConfig = async (file: File) => {
    setIsImporting(true);
    try {
      const text = await file.text();
      const config = JSON.parse(text);

      const formData = new FormData();
      formData.append('file', file);

      const response = await fetch('/api/configuration/import', {
        method: 'POST',
        body: formData
      });

      const result = await response.json();
      setImportResult(result);
    } catch (error) {
      console.error('Import failed:', error);
      throw error;
    } finally {
      setIsImporting(false);
    }
  };

  return { importConfig, isImporting, importResult };
};

// Component usage
export const ImportConfigDialog: React.FC = () => {
  const { importConfig, isImporting } = useConfigImport();

  const handleFileSelect = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    try {
      await importConfig(file);
      alert('Configuration imported successfully');
    } catch (error) {
      alert('Import failed: ' + error);
    }
  };

  return (
    <div>
      <input
        type="file"
        accept=".json"
        onChange={handleFileSelect}
        disabled={isImporting}
      />
      {isImporting && <p>Importing...</p>}
    </div>
  );
};
```

### React Multi-Select State Management
```typescript
// Source: React documentation + community patterns
export const useMultiSelect = <T extends { id: string }>(
  items: T[],
  canSelect: (item: T) => boolean = () => true
) => {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const toggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const selectAll = () => {
    const selectableIds = items
      .filter(canSelect)
      .map(item => item.id);
    setSelectedIds(new Set(selectableIds));
  };

  const clearSelection = () => {
    setSelectedIds(new Set());
  };

  const isSelected = (id: string) => selectedIds.has(id);

  const selectedItems = items.filter(item => selectedIds.has(item.id));

  return {
    selectedIds,
    selectedItems,
    selectedCount: selectedIds.size,
    toggleSelect,
    selectAll,
    clearSelection,
    isSelected
  };
};
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Kubernetes Operators (custom controllers) | Stateless API operations | 2022-2024 trend | Operators appropriate for complex reconciliation; overkill for simple CRUD. Phase 11 is stateless API. |
| Manual YAML string building | Kubernetes client model objects | KubernetesClient v2+ | Type safety, IDE autocomplete, less error-prone |
| Helm SDK for dynamic resources | Direct Kubernetes API | 2023+ patterns | Helm for templating static resources; API for runtime operations. Mixing approaches adds complexity. |
| FormData for JSON API | Native JSON.stringify/parse | 2024+ Fetch API | Simpler for JSON payloads; FormData when multipart needed |
| react-hook-form for all forms | Native useState for simple forms | React 19 patterns | Hook forms powerful but overkill for 3-5 fields; native useState sufficient |
| Class-based validators | FluentValidation functional rules | FluentValidation v11+ | More composable, testable, declarative |

**Deprecated/outdated:**
- **string-based Kubernetes client (kubectl shelling):** Use KubernetesClient library with strongly-typed models
- **ConfigMap hot-reload via sidecar:** Applications must implement own watch/refresh; no automatic pod restart
- **Pre-validation of NodePort availability:** Kubernetes handles atomically; pre-check introduces race condition
- **Optimistic locking without retry:** Always handle conflicts by retrying with updated resourceVersion

## Open Questions

Things that couldn't be fully resolved:

1. **Control Plane Pod Restart Impact**
   - What we know: OwnerReferences to control plane pod will cause all dynamic servers to be garbage collected if pod restarts
   - What's unclear: Is this desired behavior or should dynamic servers persist across control plane restarts?
   - Recommendation: Phase 11 implements pod-owned pattern (servers deleted on restart). Phase 12 can add persistence via custom resource or SQLite state tracking if users request it. Document behavior clearly in UI.

2. **Resource Quota Enforcement Strategy**
   - What we know: Kubernetes ResourceQuota can limit object count by kind (e.g., max 10 deployments); also can be implemented in application logic
   - What's unclear: Should limits be enforced by Kubernetes ResourceQuota or Control API validation?
   - Recommendation: Start with Control API validation (simpler, clearer error messages). Add K8s ResourceQuota in Phase 12 as defense-in-depth.

3. **NAS Server Directory Isolation**
   - What we know: Static NAS servers use subdirectories (nas-input-1, nas-output-1) on shared PVC
   - What's unclear: Should dynamic NAS servers create dedicated PVCs or use subdirectories on shared PVC?
   - Recommendation: Use subdirectories on shared PVC for consistency with v1.0. User decides on deletion whether to keep directory contents. Simpler than managing per-server PVCs.

4. **Config Import Validation Depth**
   - What we know: Import should validate JSON schema, but unclear how deep validation should go (e.g., validate NodePort not already in use)
   - What's unclear: Should import pre-validate all constraints or rely on Kubernetes API to reject invalid creates?
   - Recommendation: Validate schema only (structure, types, required fields). Let Kubernetes API reject resource conflicts. Show specific error per server in import result.

## Sources

### Primary (HIGH confidence)
- [Kubernetes official documentation: Owners and Dependents](https://kubernetes.io/docs/concepts/overview/working-with-objects/owners-dependents/) - OwnerReferences pattern
- [Kubernetes official documentation: Use Cascading Deletion in a Cluster](https://kubernetes.io/docs/tasks/administer-cluster/use-cascading-deletion/) - Garbage collection behavior
- [Kubernetes official documentation: Using RBAC Authorization](https://kubernetes.io/docs/reference/access-authn-authz/rbac/) - RBAC verbs and rules
- [Kubernetes official documentation: Resource Quotas](https://kubernetes.io/docs/concepts/policy/resource-quotas/) - Object count limits
- [Kubernetes blog: NodePort Dynamic and Static Allocation (v1.27)](https://kubernetes.io/blog/2023/05/11/nodeport-dynamic-and-static-allocation/) - NodePort range behavior
- [GitHub: kubernetes-client/csharp](https://github.com/kubernetes-client/csharp) - Official C# client
- [FluentValidation official documentation: ASP.NET Core](https://fluentvalidation.net/aspnet) - ASP.NET Core integration
- [GitHub: FluentValidation/FluentValidation.AspNetCore](https://github.com/FluentValidation/FluentValidation.AspNetCore) - Official ASP.NET integration package

### Secondary (MEDIUM confidence)
- [Medium: Creating Dynamic Kubernetes Jobs with the C# Client](https://medium.com/@sezer.darendeli/creating-dynamic-kubernetes-jobs-with-the-c-client-2c2087cc73d5) - C# client patterns verified with official docs
- [Willem's Fizzy Logic: Building a custom Kubernetes operator in C#](https://fizzylogic.nl/2023/01/07/building-a-custom-kubernetes-operator-in-c) - OwnerReferences in C#
- [Medium: Kubernetes Ordered cleanup with OwnerReference](https://medium.com/@AbhijeetKasurde/kubernetes-ordered-cleanup-with-ownerreference-81adeaceb0c9) - Cleanup patterns
- [GitHub: stakater/Reloader](https://github.com/stakater/Reloader) - ConfigMap watch patterns
- [Baeldung: Why Kubernetes NodePort Services Range From 30000 – 32767](https://www.baeldung.com/ops/kubernetes-nodeport-range) - NodePort range explanation
- [Code Maze: How to Use FluentValidation in ASP.NET Core](https://code-maze.com/fluentvalidation-in-aspnet/) - FluentValidation examples
- [Medium: Minimal API Validation in .NET 10](https://medium.com/@adrianbailador/minimal-api-validation-in-net-10-8997a48b8a66) - Recent validation patterns
- [FreeCodeCamp: React Tutorial – How to Work with Multiple Checkboxes](https://www.freecodecamp.org/news/how-to-work-with-multiple-checkboxes-in-react/) - Multi-select patterns
- [LogRocket: How to build an inline editable UI in React](https://blog.logrocket.com/build-inline-editable-ui-react/) - Inline editing patterns
- [Simple Table: Editable React Data Grids: In-Cell Editing vs Form-Based Editing (2026)](https://www.simple-table.com/blog/editable-react-data-grids-in-cell-vs-form-editing) - Recent editing pattern guidance

### Tertiary (LOW confidence - for context only)
- Various Stack Overflow discussions on Kubernetes client patterns (not cited due to variability)
- Community blog posts on React form patterns (verified against official React docs)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - KubernetesClient already in use (Phase 6), official library, well-documented
- Architecture: HIGH - Patterns verified with official Kubernetes docs and existing codebase patterns
- Pitfalls: HIGH - Based on official Kubernetes garbage collection behavior and community-reported issues
- React patterns: MEDIUM - Patterns verified with official React docs but specific libraries (useMultiSelect) are custom implementations

**Research date:** 2026-02-03
**Valid until:** ~60 days (stable domain - Kubernetes API and React patterns change slowly; KubernetesClient v18 is current LTS)

**Notes:**
- Existing Phase 6 KubernetesDiscoveryService provides read-only foundation - Phase 11 extends with write operations
- RBAC rules in values.yaml currently limited to `get, list, watch` - must be updated to include `create, update, delete`
- Helm templates (ftp.yaml, nas.yaml) serve as blueprints for dynamic resource creation
- User decisions from CONTEXT.md mandate: full configuration exposure, NodePort auto-assign with override, NAS preset directories, progress modals, multi-select delete
