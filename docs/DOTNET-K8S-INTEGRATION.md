# .NET Kubernetes API Integration Guide

**Target:** .NET 10
**Package:** KubernetesClient 14.x
**Purpose:** Dynamically manage PV/PVC/ConfigMaps/Mounts for File Simulator NAS servers from your system settings UI

## NuGet Packages

```xml
<PackageReference Include="KubernetesClient" Version="14.0.7" />
<PackageReference Include="KubernetesClient.Models" Version="14.0.7" />
```

---

## Table of Contents

1. [KubernetesClient Setup](#1-kubernetesclient-setup)
2. [Configuration Model](#2-configuration-model)
3. [Creating PersistentVolumes](#3-creating-persistentvolumes)
4. [Creating PersistentVolumeClaims](#4-creating-persistentvolumeclaims)
5. [Creating ConfigMaps](#5-creating-configmaps)
6. [Adding Volume Mounts to Deployments](#6-adding-volume-mounts-to-deployments)
7. [Removing Resources](#7-removing-resources)
8. [Complete Service Example](#8-complete-service-example)
9. [UI Integration Example](#9-ui-integration-example)

---

## 1. KubernetesClient Setup

### Basic Client Initialization

```csharp
using k8s;
using k8s.Models;

public class K8sClientFactory
{
    public static Kubernetes CreateClient(string kubeConfigPath = null)
    {
        KubernetesClientConfiguration config;

        if (!string.IsNullOrEmpty(kubeConfigPath))
        {
            // Load from specific kubeconfig file
            config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigPath);
        }
        else
        {
            // Auto-detect: in-cluster config or default kubeconfig
            config = KubernetesClientConfiguration.InClusterConfig();

            // Fallback to default kubeconfig if not in cluster
            if (config == null)
            {
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }
        }

        return new Kubernetes(config);
    }

    public static Kubernetes CreateClientWithContext(string contextName, string kubeConfigPath = null)
    {
        // Load kubeconfig
        var kubeconfig = KubernetesClientConfiguration.LoadKubeConfig(
            kubeConfigPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".kube",
                "config"
            )
        );

        // Find the specified context
        var context = kubeconfig.Contexts.FirstOrDefault(c => c.Name == contextName);
        if (context == null)
        {
            throw new ArgumentException($"Context '{contextName}' not found in kubeconfig");
        }

        // Build config from context
        var config = KubernetesClientConfiguration.BuildConfigFromConfigObject(kubeconfig, contextName);

        return new Kubernetes(config);
    }
}
```

### Dependency Injection Setup

```csharp
// In Program.cs or Startup.cs
public static IServiceCollection AddKubernetesClient(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddSingleton<Kubernetes>(sp =>
    {
        var contextName = configuration["Kubernetes:Context"] ?? "file-simulator";
        var kubeConfigPath = configuration["Kubernetes:KubeConfigPath"];

        return K8sClientFactory.CreateClientWithContext(contextName, kubeConfigPath);
    });

    return services;
}

// Usage in Program.cs
builder.Services.AddKubernetesClient(builder.Configuration);
```

### appsettings.json Configuration

```json
{
  "Kubernetes": {
    "Context": "file-simulator",
    "Namespace": "file-simulator",
    "KubeConfigPath": null,
    "FileSimulatorNamespace": "file-simulator",
    "ApplicationNamespace": "default"
  }
}
```

---

## 2. Configuration Model

### NAS Server Configuration Model

```csharp
public record NasServerConfig
{
    public required string Name { get; init; }  // e.g., "nas-input-1"
    public required string Type { get; init; }  // "Input", "Output", "Backup"
    public required string Host { get; init; }  // DNS or IP
    public int Port { get; init; } = 2049;
    public string ExportPath { get; init; } = "/data";
    public string? WindowsPath { get; init; }  // For external reference
    public bool ReadOnly { get; init; }
    public string? PvName { get; init; }   // Auto-generated if null
    public string? PvcName { get; init; }  // Auto-generated if null
    public string? MountPath { get; init; } // e.g., "/mnt/input-1"
    public bool BidirectionalSync { get; init; }
    public int? SyncInterval { get; init; }

    // Kubernetes-specific
    public string StorageCapacity { get; init; } = "10Gi";
    public string AccessMode { get; init; } = "ReadWriteMany";
    public string ReclaimPolicy { get; init; } = "Retain";
    public List<string>? MountOptions { get; init; } = new() { "nfsvers=3", "tcp", "hard", "intr" };
    public Dictionary<string, string>? Labels { get; init; }
}

public static class NasServerDefaults
{
    public static List<NasServerConfig> GetFileSimulatorServers(string minikubeIp)
    {
        var baseDns = "file-simulator.svc.cluster.local";

        return new List<NasServerConfig>
        {
            // Input servers
            new() {
                Name = "nas-input-1",
                Type = "Input",
                Host = $"file-sim-nas-input-1.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-input-1",
                ReadOnly = false,
                MountPath = "/mnt/input-1",
                Labels = new() { ["nas-role"] = "input", ["nas-server"] = "nas-input-1" }
            },
            new() {
                Name = "nas-input-2",
                Type = "Input",
                Host = $"file-sim-nas-input-2.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-input-2",
                ReadOnly = false,
                MountPath = "/mnt/input-2",
                Labels = new() { ["nas-role"] = "input", ["nas-server"] = "nas-input-2" }
            },
            new() {
                Name = "nas-input-3",
                Type = "Input",
                Host = $"file-sim-nas-input-3.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-input-3",
                ReadOnly = false,
                MountPath = "/mnt/input-3",
                Labels = new() { ["nas-role"] = "input", ["nas-server"] = "nas-input-3" }
            },

            // Backup server
            new() {
                Name = "nas-backup",
                Type = "Backup",
                Host = $"file-sim-nas-backup.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-backup",
                ReadOnly = true,
                MountPath = "/mnt/backup",
                Labels = new() { ["nas-role"] = "backup", ["nas-server"] = "nas-backup" }
            },

            // Output servers (with bidirectional sync)
            new() {
                Name = "nas-output-1",
                Type = "Output",
                Host = $"file-sim-nas-output-1.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-output-1",
                ReadOnly = false,
                MountPath = "/mnt/output-1",
                BidirectionalSync = true,
                SyncInterval = 30,
                Labels = new() { ["nas-role"] = "output", ["nas-server"] = "nas-output-1" }
            },
            new() {
                Name = "nas-output-2",
                Type = "Output",
                Host = $"file-sim-nas-output-2.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-output-2",
                ReadOnly = false,
                MountPath = "/mnt/output-2",
                BidirectionalSync = true,
                SyncInterval = 30,
                Labels = new() { ["nas-role"] = "output", ["nas-server"] = "nas-output-2" }
            },
            new() {
                Name = "nas-output-3",
                Type = "Output",
                Host = $"file-sim-nas-output-3.{baseDns}",
                Port = 2049,
                WindowsPath = @"C:\simulator-data\nas-output-3",
                ReadOnly = false,
                MountPath = "/mnt/output-3",
                BidirectionalSync = true,
                SyncInterval = 30,
                Labels = new() { ["nas-role"] = "output", ["nas-server"] = "nas-output-3" }
            }
        };
    }
}
```

---

## 3. Creating PersistentVolumes

### Service Method

```csharp
using k8s;
using k8s.Models;

public class NasResourceService
{
    private readonly Kubernetes _k8sClient;
    private readonly ILogger<NasResourceService> _logger;

    public NasResourceService(Kubernetes k8sClient, ILogger<NasResourceService> logger)
    {
        _k8sClient = k8sClient;
        _logger = logger;
    }

    public async Task<V1PersistentVolume> CreatePersistentVolumeAsync(
        NasServerConfig nasConfig,
        CancellationToken cancellationToken = default)
    {
        var pvName = nasConfig.PvName ?? $"{nasConfig.Name}-pv";

        var pv = new V1PersistentVolume
        {
            ApiVersion = "v1",
            Kind = "PersistentVolume",
            Metadata = new V1ObjectMeta
            {
                Name = pvName,
                Labels = new Dictionary<string, string>
                {
                    ["type"] = "nfs",
                    ["nas-role"] = nasConfig.Type.ToLower(),
                    ["nas-server"] = nasConfig.Name,
                    ["environment"] = "development"
                }
            },
            Spec = new V1PersistentVolumeSpec
            {
                Capacity = new Dictionary<string, ResourceQuantity>
                {
                    ["storage"] = new ResourceQuantity(nasConfig.StorageCapacity)
                },
                AccessModes = new List<string> { nasConfig.AccessMode },
                PersistentVolumeReclaimPolicy = nasConfig.ReclaimPolicy,
                Nfs = new V1NFSVolumeSource
                {
                    Server = nasConfig.Host,
                    Path = nasConfig.ExportPath,
                    ReadOnlyProperty = nasConfig.ReadOnly
                },
                MountOptions = nasConfig.MountOptions
            }
        };

        // Merge custom labels if provided
        if (nasConfig.Labels != null)
        {
            foreach (var label in nasConfig.Labels)
            {
                pv.Metadata.Labels[label.Key] = label.Value;
            }
        }

        try
        {
            var created = await _k8sClient.CoreV1.CreatePersistentVolumeAsync(pv, cancellationToken: cancellationToken);
            _logger.LogInformation("Created PersistentVolume: {PvName} for NAS server {NasName}", pvName, nasConfig.Name);
            return created;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("PersistentVolume {PvName} already exists", pvName);

            // Get existing PV
            var existing = await _k8sClient.CoreV1.ReadPersistentVolumeAsync(pvName, cancellationToken: cancellationToken);
            return existing;
        }
    }

    public async Task<bool> DeletePersistentVolumeAsync(
        string pvName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _k8sClient.CoreV1.DeletePersistentVolumeAsync(pvName, cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted PersistentVolume: {PvName}", pvName);
            return true;
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("PersistentVolume {PvName} not found (already deleted?)", pvName);
            return false;
        }
    }

    public async Task<V1PersistentVolume?> GetPersistentVolumeAsync(
        string pvName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _k8sClient.CoreV1.ReadPersistentVolumeAsync(pvName, cancellationToken: cancellationToken);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
```

---

## 4. Creating PersistentVolumeClaims

### Service Method

```csharp
public async Task<V1PersistentVolumeClaim> CreatePersistentVolumeClaimAsync(
    NasServerConfig nasConfig,
    string namespace_,
    CancellationToken cancellationToken = default)
{
    var pvcName = nasConfig.PvcName ?? $"{nasConfig.Name}-pvc";
    var pvName = nasConfig.PvName ?? $"{nasConfig.Name}-pv";

    var pvc = new V1PersistentVolumeClaim
    {
        ApiVersion = "v1",
        Kind = "PersistentVolumeClaim",
        Metadata = new V1ObjectMeta
        {
            Name = pvcName,
            NamespaceProperty = namespace_,
            Labels = new Dictionary<string, string>
            {
                ["nas-server"] = nasConfig.Name,
                ["nas-role"] = nasConfig.Type.ToLower()
            }
        },
        Spec = new V1PersistentVolumeClaimSpec
        {
            AccessModes = new List<string> { nasConfig.AccessMode },
            Resources = new V1VolumeResourceRequirements
            {
                Requests = new Dictionary<string, ResourceQuantity>
                {
                    ["storage"] = new ResourceQuantity(nasConfig.StorageCapacity)
                }
            },
            Selector = new V1LabelSelector
            {
                MatchLabels = new Dictionary<string, string>
                {
                    ["nas-server"] = nasConfig.Name  // Binds to PV with this label
                }
            }
            // NOTE: StorageClassName is intentionally NULL for static binding
        }
    };

    try
    {
        var created = await _k8sClient.CoreV1.CreateNamespacedPersistentVolumeClaimAsync(
            pvc,
            namespace_,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created PersistentVolumeClaim: {PvcName} in namespace {Namespace}", pvcName, namespace_);
        return created;
    }
    catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
    {
        _logger.LogWarning("PersistentVolumeClaim {PvcName} already exists in {Namespace}", pvcName, namespace_);

        var existing = await _k8sClient.CoreV1.ReadNamespacedPersistentVolumeClaimAsync(
            pvcName,
            namespace_,
            cancellationToken: cancellationToken);
        return existing;
    }
}

public async Task<bool> DeletePersistentVolumeClaimAsync(
    string pvcName,
    string namespace_,
    CancellationToken cancellationToken = default)
{
    try
    {
        await _k8sClient.CoreV1.DeleteNamespacedPersistentVolumeClaimAsync(
            pvcName,
            namespace_,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted PersistentVolumeClaim: {PvcName} from {Namespace}", pvcName, namespace_);
        return true;
    }
    catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        _logger.LogWarning("PersistentVolumeClaim {PvcName} not found in {Namespace}", pvcName, namespace_);
        return false;
    }
}

public async Task<string> GetPvcBindingStatusAsync(
    string pvcName,
    string namespace_,
    CancellationToken cancellationToken = default)
{
    var pvc = await _k8sClient.CoreV1.ReadNamespacedPersistentVolumeClaimAsync(
        pvcName,
        namespace_,
        cancellationToken: cancellationToken);

    return pvc.Status?.Phase ?? "Unknown";  // Pending, Bound, Lost
}
```

---

## 5. Creating ConfigMaps

### Service Method

```csharp
public async Task<V1ConfigMap> CreateNasEndpointsConfigMapAsync(
    List<NasServerConfig> nasServers,
    string namespace_,
    string minikubeIp,
    CancellationToken cancellationToken = default)
{
    var configMapName = "file-simulator-nas-endpoints";

    var data = new Dictionary<string, string>
    {
        ["NAS_COUNT"] = nasServers.Count.ToString(),
        ["NAS_NAMESPACE"] = "file-simulator",
        ["MINIKUBE_IP"] = minikubeIp
    };

    // Add entries for each NAS server
    foreach (var nas in nasServers)
    {
        var prefix = nas.Name.ToUpper().Replace("-", "_");

        data[$"{prefix}_HOST"] = nas.Host;
        data[$"{prefix}_PORT"] = nas.Port.ToString();
        data[$"{prefix}_PATH"] = nas.ExportPath;
        data[$"{prefix}_PVC"] = nas.PvcName ?? $"{nas.Name}-pvc";
        data[$"{prefix}_TYPE"] = nas.Type;
        data[$"{prefix}_READONLY"] = nas.ReadOnly.ToString().ToLower();

        // Add NodePort if external access (not cluster DNS)
        if (nas.Host.Contains(minikubeIp))
        {
            data[$"{prefix}_NODEPORT"] = nas.Port.ToString();
        }
        else
        {
            // Extract NodePort from service metadata (32150-32156)
            var nodePort = nas.Name switch
            {
                "nas-input-1" => "32150",
                "nas-input-2" => "32151",
                "nas-input-3" => "32152",
                "nas-backup" => "32153",
                "nas-output-1" => "32154",
                "nas-output-2" => "32155",
                "nas-output-3" => "32156",
                _ => "2049"
            };
            data[$"{prefix}_NODEPORT"] = nodePort;
        }
    }

    var configMap = new V1ConfigMap
    {
        ApiVersion = "v1",
        Kind = "ConfigMap",
        Metadata = new V1ObjectMeta
        {
            Name = configMapName,
            NamespaceProperty = namespace_,
            Labels = new Dictionary<string, string>
            {
                ["app"] = "file-simulator",
                ["component"] = "nas-endpoints"
            }
        },
        Data = data
    };

    try
    {
        var created = await _k8sClient.CoreV1.CreateNamespacedConfigMapAsync(
            configMap,
            namespace_,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created ConfigMap: {Name} with {Count} NAS servers in {Namespace}",
            configMapName, nasServers.Count, namespace_);
        return created;
    }
    catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
    {
        _logger.LogWarning("ConfigMap {Name} already exists, updating instead", configMapName);

        // Update existing ConfigMap
        var existing = await _k8sClient.CoreV1.ReadNamespacedConfigMapAsync(
            configMapName,
            namespace_,
            cancellationToken: cancellationToken);

        existing.Data = data;  // Replace all data

        var updated = await _k8sClient.CoreV1.ReplaceNamespacedConfigMapAsync(
            existing,
            configMapName,
            namespace_,
            cancellationToken: cancellationToken);

        return updated;
    }
}

public async Task<bool> DeleteConfigMapAsync(
    string configMapName,
    string namespace_,
    CancellationToken cancellationToken = default)
{
    try
    {
        await _k8sClient.CoreV1.DeleteNamespacedConfigMapAsync(
            configMapName,
            namespace_,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Deleted ConfigMap: {Name} from {Namespace}", configMapName, namespace_);
        return true;
    }
    catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        _logger.LogWarning("ConfigMap {Name} not found in {Namespace}", configMapName, namespace_);
        return false;
    }
}
```

---

## 6. Adding Volume Mounts to Deployments

### Service Method - Add NAS Mount to Existing Deployment

```csharp
public async Task<V1Deployment> AddNasMountToDeploymentAsync(
    string deploymentName,
    string namespace_,
    NasServerConfig nasConfig,
    CancellationToken cancellationToken = default)
{
    // Read existing deployment
    var deployment = await _k8sClient.AppsV1.ReadNamespacedDeploymentAsync(
        deploymentName,
        namespace_,
        cancellationToken: cancellationToken);

    var pvcName = nasConfig.PvcName ?? $"{nasConfig.Name}-pvc";
    var mountPath = nasConfig.MountPath ?? $"/mnt/{nasConfig.Name}";
    var volumeName = $"nas-{nasConfig.Name}";

    // Get the first container (or specify container name)
    var container = deployment.Spec.Template.Spec.Containers.First();

    // Check if volume already exists
    deployment.Spec.Template.Spec.Volumes ??= new List<V1Volume>();
    if (deployment.Spec.Template.Spec.Volumes.Any(v => v.Name == volumeName))
    {
        _logger.LogWarning("Volume {VolumeName} already exists in deployment {DeploymentName}", volumeName, deploymentName);
        return deployment;
    }

    // Add volume reference
    deployment.Spec.Template.Spec.Volumes.Add(new V1Volume
    {
        Name = volumeName,
        PersistentVolumeClaim = new V1PersistentVolumeClaimVolumeSource
        {
            ClaimName = pvcName,
            ReadOnlyProperty = nasConfig.ReadOnly
        }
    });

    // Add volume mount to container
    container.VolumeMounts ??= new List<V1VolumeMount>();
    if (container.VolumeMounts.Any(vm => vm.Name == volumeName))
    {
        _logger.LogWarning("VolumeMount {VolumeName} already exists in deployment {DeploymentName}", volumeName, deploymentName);
        return deployment;
    }

    container.VolumeMounts.Add(new V1VolumeMount
    {
        Name = volumeName,
        MountPath = mountPath,
        ReadOnlyProperty = nasConfig.ReadOnly
    });

    // Update deployment
    var updated = await _k8sClient.AppsV1.ReplaceNamespacedDeploymentAsync(
        deployment,
        deploymentName,
        namespace_,
        cancellationToken: cancellationToken);

    _logger.LogInformation("Added NAS mount {NasName} to deployment {DeploymentName} at {MountPath}",
        nasConfig.Name, deploymentName, mountPath);

    return updated;
}

public async Task<V1Deployment> RemoveNasMountFromDeploymentAsync(
    string deploymentName,
    string namespace_,
    string nasName,
    CancellationToken cancellationToken = default)
{
    // Read existing deployment
    var deployment = await _k8sClient.AppsV1.ReadNamespacedDeploymentAsync(
        deploymentName,
        namespace_,
        cancellationToken: cancellationToken);

    var volumeName = $"nas-{nasName}";

    // Get the first container
    var container = deployment.Spec.Template.Spec.Containers.First();

    // Remove volume mount from container
    if (container.VolumeMounts != null)
    {
        var mountToRemove = container.VolumeMounts.FirstOrDefault(vm => vm.Name == volumeName);
        if (mountToRemove != null)
        {
            container.VolumeMounts.Remove(mountToRemove);
            _logger.LogInformation("Removed volume mount {VolumeName} from container", volumeName);
        }
    }

    // Remove volume reference
    if (deployment.Spec.Template.Spec.Volumes != null)
    {
        var volumeToRemove = deployment.Spec.Template.Spec.Volumes.FirstOrDefault(v => v.Name == volumeName);
        if (volumeToRemove != null)
        {
            deployment.Spec.Template.Spec.Volumes.Remove(volumeToRemove);
            _logger.LogInformation("Removed volume {VolumeName} from deployment", volumeName);
        }
    }

    // Update deployment
    var updated = await _k8sClient.AppsV1.ReplaceNamespacedDeploymentAsync(
        deployment,
        deploymentName,
        namespace_,
        cancellationToken: cancellationToken);

    _logger.LogInformation("Removed NAS mount {NasName} from deployment {DeploymentName}", nasName, deploymentName);

    return updated;
}
```

---

## 7. Removing Resources

### Service Method - Complete Cleanup

```csharp
public async Task<bool> RemoveNasServerAsync(
    string nasName,
    string namespace_,
    string? deploymentName = null,
    CancellationToken cancellationToken = default)
{
    var pvName = $"{nasName}-pv";
    var pvcName = $"{nasName}-pvc";
    var success = true;

    try
    {
        // 1. Remove from deployment if specified
        if (!string.IsNullOrEmpty(deploymentName))
        {
            try
            {
                await RemoveNasMountFromDeploymentAsync(deploymentName, namespace_, nasName, cancellationToken);
                _logger.LogInformation("Removed {NasName} mount from deployment {DeploymentName}", nasName, deploymentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove mount from deployment");
                success = false;
            }
        }

        // 2. Delete PVC (must be done before PV if you want immediate cleanup)
        try
        {
            await DeletePersistentVolumeClaimAsync(pvcName, namespace_, cancellationToken);

            // Wait for PVC to be fully deleted
            await WaitForPvcDeletionAsync(pvcName, namespace_, TimeSpan.FromSeconds(30), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete PVC {PvcName}", pvcName);
            success = false;
        }

        // 3. Delete PV (after PVC is released)
        try
        {
            await DeletePersistentVolumeAsync(pvName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete PV {PvName}", pvName);
            success = false;
        }

        return success;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to remove NAS server {NasName}", nasName);
        return false;
    }
}

private async Task WaitForPvcDeletionAsync(
    string pvcName,
    string namespace_,
    TimeSpan timeout,
    CancellationToken cancellationToken)
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    while (stopwatch.Elapsed < timeout)
    {
        try
        {
            await _k8sClient.CoreV1.ReadNamespacedPersistentVolumeClaimAsync(
                pvcName,
                namespace_,
                cancellationToken: cancellationToken);

            // Still exists, wait
            await Task.Delay(1000, cancellationToken);
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Successfully deleted
            return;
        }
    }

    _logger.LogWarning("PVC {PvcName} deletion timeout after {Timeout}s", pvcName, timeout.TotalSeconds);
}
```

---

## 8. Complete Service Example

### Full NasResourceService with All Operations

```csharp
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace FileSimulator.K8s.Services;

public interface INasResourceService
{
    // PV operations
    Task<V1PersistentVolume> CreatePersistentVolumeAsync(NasServerConfig nasConfig, CancellationToken ct = default);
    Task<bool> DeletePersistentVolumeAsync(string pvName, CancellationToken ct = default);
    Task<V1PersistentVolume?> GetPersistentVolumeAsync(string pvName, CancellationToken ct = default);
    Task<List<V1PersistentVolume>> ListAllPersistentVolumesAsync(CancellationToken ct = default);

    // PVC operations
    Task<V1PersistentVolumeClaim> CreatePersistentVolumeClaimAsync(NasServerConfig nasConfig, string namespace_, CancellationToken ct = default);
    Task<bool> DeletePersistentVolumeClaimAsync(string pvcName, string namespace_, CancellationToken ct = default);
    Task<string> GetPvcBindingStatusAsync(string pvcName, string namespace_, CancellationToken ct = default);

    // ConfigMap operations
    Task<V1ConfigMap> CreateNasEndpointsConfigMapAsync(List<NasServerConfig> nasServers, string namespace_, string minikubeIp, CancellationToken ct = default);
    Task<bool> DeleteConfigMapAsync(string configMapName, string namespace_, CancellationToken ct = default);

    // Deployment operations
    Task<V1Deployment> AddNasMountToDeploymentAsync(string deploymentName, string namespace_, NasServerConfig nasConfig, CancellationToken ct = default);
    Task<V1Deployment> RemoveNasMountFromDeploymentAsync(string deploymentName, string namespace_, string nasName, CancellationToken ct = default);
    Task<List<string>> GetDeploymentNasMountsAsync(string deploymentName, string namespace_, CancellationToken ct = default);

    // Composite operations
    Task<bool> ProvisionNasServerAsync(NasServerConfig nasConfig, string namespace_, CancellationToken ct = default);
    Task<bool> RemoveNasServerAsync(string nasName, string namespace_, string? deploymentName = null, CancellationToken ct = default);
}

public class NasResourceService : INasResourceService
{
    private readonly Kubernetes _k8sClient;
    private readonly ILogger<NasResourceService> _logger;

    public NasResourceService(Kubernetes k8sClient, ILogger<NasResourceService> logger)
    {
        _k8sClient = k8sClient;
        _logger = logger;
    }

    // [Include all methods from sections 3-7 above]

    public async Task<List<V1PersistentVolume>> ListAllPersistentVolumesAsync(
        CancellationToken cancellationToken = default)
    {
        var pvList = await _k8sClient.CoreV1.ListPersistentVolumeAsync(
            labelSelector: "type=nfs",
            cancellationToken: cancellationToken);

        return pvList.Items.ToList();
    }

    public async Task<List<string>> GetDeploymentNasMountsAsync(
        string deploymentName,
        string namespace_,
        CancellationToken cancellationToken = default)
    {
        var deployment = await _k8sClient.AppsV1.ReadNamespacedDeploymentAsync(
            deploymentName,
            namespace_,
            cancellationToken: cancellationToken);

        var container = deployment.Spec.Template.Spec.Containers.First();

        // Find all NAS mounts (volumes starting with "nas-")
        var nasMounts = container.VolumeMounts?
            .Where(vm => vm.Name.StartsWith("nas-"))
            .Select(vm => vm.Name.Replace("nas-", ""))
            .ToList() ?? new List<string>();

        return nasMounts;
    }

    public async Task<bool> ProvisionNasServerAsync(
        NasServerConfig nasConfig,
        string namespace_,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Create PV (cluster-scoped)
            await CreatePersistentVolumeAsync(nasConfig, cancellationToken);

            // 2. Create PVC (namespace-scoped)
            await CreatePersistentVolumeClaimAsync(nasConfig, namespace_, cancellationToken);

            // 3. Wait for PVC to bind
            var maxWait = TimeSpan.FromSeconds(30);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            while (stopwatch.Elapsed < maxWait)
            {
                var status = await GetPvcBindingStatusAsync(
                    nasConfig.PvcName ?? $"{nasConfig.Name}-pvc",
                    namespace_,
                    cancellationToken);

                if (status == "Bound")
                {
                    _logger.LogInformation("NAS server {NasName} provisioned successfully", nasConfig.Name);
                    return true;
                }

                await Task.Delay(2000, cancellationToken);
            }

            _logger.LogWarning("PVC did not bind within {Timeout}s", maxWait.TotalSeconds);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision NAS server {NasName}", nasConfig.Name);
            return false;
        }
    }
}
```

---

## 9. UI Integration Example

### Settings UI Controller (ASP.NET Core)

```csharp
using Microsoft.AspNetCore.Mvc;
using FileSimulator.K8s.Services;

namespace FileSimulator.Api.Controllers;

[ApiController]
[Route("api/settings/nas")]
public class NasSettingsController : ControllerBase
{
    private readonly INasResourceService _nasService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NasSettingsController> _logger;

    public NasSettingsController(
        INasResourceService nasService,
        IConfiguration configuration,
        ILogger<NasSettingsController> logger)
    {
        _nasService = nasService;
        _configuration = configuration;
        _logger = logger;
    }

    // GET /api/settings/nas - List available NAS servers
    [HttpGet]
    public async Task<ActionResult<List<NasServerInfo>>> GetAvailableNasServers(CancellationToken ct)
    {
        try
        {
            // Get all PVs with label selector
            var pvs = await _nasService.ListAllPersistentVolumesAsync(ct);

            var servers = pvs.Select(pv => new NasServerInfo
            {
                Name = pv.Metadata.Labels["nas-server"],
                Type = pv.Metadata.Labels["nas-role"],
                Host = pv.Spec.Nfs.Server,
                Port = 2049,
                ExportPath = pv.Spec.Nfs.Path,
                PvName = pv.Metadata.Name,
                Status = pv.Status?.Phase ?? "Unknown",
                Capacity = pv.Spec.Capacity["storage"].ToString()
            }).ToList();

            return Ok(servers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list NAS servers");
            return StatusCode(500, "Failed to list NAS servers");
        }
    }

    // GET /api/settings/nas/active - List active mounts in deployment
    [HttpGet("active")]
    public async Task<ActionResult<List<string>>> GetActiveMounts(
        [FromQuery] string? deployment = null,
        CancellationToken ct = default)
    {
        try
        {
            var deploymentName = deployment ?? _configuration["Kubernetes:DeploymentName"] ?? "your-app";
            var namespace_ = _configuration["Kubernetes:ApplicationNamespace"] ?? "default";

            var mounts = await _nasService.GetDeploymentNasMountsAsync(deploymentName, namespace_, ct);
            return Ok(mounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active mounts");
            return StatusCode(500, "Failed to get active mounts");
        }
    }

    // POST /api/settings/nas/add - Add NAS mount to deployment
    [HttpPost("add")]
    public async Task<ActionResult> AddNasMount(
        [FromBody] AddNasMountRequest request,
        CancellationToken ct)
    {
        try
        {
            var namespace_ = _configuration["Kubernetes:ApplicationNamespace"] ?? "default";

            // 1. Ensure PV exists
            var pv = await _nasService.GetPersistentVolumeAsync(request.PvName, ct);
            if (pv == null)
            {
                return NotFound($"PersistentVolume {request.PvName} not found. Create it first or use file-simulator examples.");
            }

            // 2. Create PVC in application namespace
            var nasConfig = new NasServerConfig
            {
                Name = request.NasName,
                Type = request.Type,
                Host = pv.Spec.Nfs.Server,
                ExportPath = pv.Spec.Nfs.Path,
                PvcName = request.PvcName,
                PvName = request.PvName,
                MountPath = request.MountPath,
                ReadOnly = request.ReadOnly,
                StorageCapacity = pv.Spec.Capacity["storage"].ToString(),
                AccessMode = pv.Spec.AccessModes.First()
            };

            await _nasService.CreatePersistentVolumeClaimAsync(nasConfig, namespace_, ct);

            // 3. Wait for binding
            await Task.Delay(2000, ct);

            var status = await _nasService.GetPvcBindingStatusAsync(request.PvcName, namespace_, ct);
            if (status != "Bound")
            {
                return StatusCode(500, $"PVC did not bind. Status: {status}");
            }

            // 4. Add mount to deployment
            await _nasService.AddNasMountToDeploymentAsync(
                request.DeploymentName,
                namespace_,
                nasConfig,
                ct);

            _logger.LogInformation("Successfully added NAS mount {NasName} to {DeploymentName}", request.NasName, request.DeploymentName);

            return Ok(new { Message = $"NAS mount {request.NasName} added successfully", Status = "Bound" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add NAS mount");
            return StatusCode(500, ex.Message);
        }
    }

    // DELETE /api/settings/nas/remove - Remove NAS mount from deployment
    [HttpDelete("remove")]
    public async Task<ActionResult> RemoveNasMount(
        [FromQuery] string nasName,
        [FromQuery] string? deployment = null,
        [FromQuery] bool deletePvc = true,
        CancellationToken ct = default)
    {
        try
        {
            var deploymentName = deployment ?? _configuration["Kubernetes:DeploymentName"] ?? "your-app";
            var namespace_ = _configuration["Kubernetes:ApplicationNamespace"] ?? "default";

            var success = await _nasService.RemoveNasServerAsync(
                nasName,
                namespace_,
                deploymentName,
                ct);

            if (success)
            {
                return Ok(new { Message = $"NAS mount {nasName} removed successfully" });
            }
            else
            {
                return StatusCode(500, "Failed to remove NAS mount (check logs)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove NAS mount");
            return StatusCode(500, ex.Message);
        }
    }

    // POST /api/settings/nas/provision - Provision new NAS server (PV + PVC)
    [HttpPost("provision")]
    public async Task<ActionResult> ProvisionNasServer(
        [FromBody] ProvisionNasRequest request,
        CancellationToken ct)
    {
        try
        {
            var namespace_ = _configuration["Kubernetes:ApplicationNamespace"] ?? "default";

            var nasConfig = new NasServerConfig
            {
                Name = request.NasName,
                Type = request.Type,
                Host = request.Host,
                Port = request.Port,
                ExportPath = request.ExportPath ?? "/data",
                ReadOnly = request.ReadOnly,
                MountPath = request.MountPath,
                StorageCapacity = request.StorageCapacity ?? "10Gi",
                MountOptions = request.MountOptions ?? new List<string> { "nfsvers=3", "tcp", "hard", "intr" }
            };

            var success = await _nasService.ProvisionNasServerAsync(nasConfig, namespace_, ct);

            if (success)
            {
                return Ok(new { Message = $"NAS server {request.NasName} provisioned successfully" });
            }
            else
            {
                return StatusCode(500, "Provisioning failed or PVC did not bind");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provision NAS server");
            return StatusCode(500, ex.Message);
        }
    }

    // PUT /api/settings/nas/configmap - Update ConfigMap with all active NAS servers
    [HttpPut("configmap")]
    public async Task<ActionResult> UpdateConfigMap(CancellationToken ct)
    {
        try
        {
            var namespace_ = _configuration["Kubernetes:ApplicationNamespace"] ?? "default";
            var minikubeIp = _configuration["Kubernetes:MinikubeIp"] ?? "172.25.201.3";

            // Get all active NAS servers (from PVs)
            var pvs = await _nasService.ListAllPersistentVolumesAsync(ct);

            var nasServers = pvs.Select(pv => new NasServerConfig
            {
                Name = pv.Metadata.Labels["nas-server"],
                Type = pv.Metadata.Labels["nas-role"],
                Host = pv.Spec.Nfs.Server,
                Port = 2049,
                ExportPath = pv.Spec.Nfs.Path,
                ReadOnly = pv.Spec.Nfs.ReadOnlyProperty ?? false,
                PvcName = $"{pv.Metadata.Labels["nas-server"]}-pvc"
            }).ToList();

            await _nasService.CreateNasEndpointsConfigMapAsync(nasServers, namespace_, minikubeIp, ct);

            return Ok(new { Message = $"ConfigMap updated with {nasServers.Count} NAS servers" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ConfigMap");
            return StatusCode(500, ex.Message);
        }
    }
}

// Request DTOs
public record AddNasMountRequest
{
    public required string NasName { get; init; }
    public required string Type { get; init; }
    public required string PvName { get; init; }
    public required string PvcName { get; init; }
    public required string DeploymentName { get; init; }
    public required string MountPath { get; init; }
    public bool ReadOnly { get; init; }
}

public record ProvisionNasRequest
{
    public required string NasName { get; init; }
    public required string Type { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; } = 2049;
    public string? ExportPath { get; init; }
    public string? MountPath { get; init; }
    public bool ReadOnly { get; init; }
    public string? StorageCapacity { get; init; }
    public List<string>? MountOptions { get; init; }
}

public record NasServerInfo
{
    public string Name { get; init; }
    public string Type { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public string ExportPath { get; init; }
    public string PvName { get; init; }
    public string Status { get; init; }
    public string Capacity { get; init; }
}
```

---

## 10. Blazor UI Example (Settings Page)

### Razor Component

```razor
@page "/settings/nas"
@inject INasResourceService NasService
@inject HttpClient Http
@inject ILogger<NasSettings> Logger

<PageTitle>NAS Server Settings</PageTitle>

<h3>NAS Server Configuration</h3>

<div class="row">
    <div class="col-md-6">
        <h4>Available NAS Servers</h4>

        @if (availableServers == null)
        {
            <p><em>Loading...</em></p>
        }
        else if (availableServers.Any())
        {
            <table class="table">
                <thead>
                    <tr>
                        <th>Name</th>
                        <th>Type</th>
                        <th>Status</th>
                        <th>Actions</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var server in availableServers)
                    {
                        <tr>
                            <td>@server.Name</td>
                            <td>@server.Type</td>
                            <td>
                                <span class="badge @GetStatusBadge(server.Status)">
                                    @server.Status
                                </span>
                            </td>
                            <td>
                                @if (activeMounts.Contains(server.Name))
                                {
                                    <button class="btn btn-sm btn-danger" @onclick="() => RemoveMount(server.Name)">
                                        Remove
                                    </button>
                                }
                                else
                                {
                                    <button class="btn btn-sm btn-success" @onclick="() => AddMount(server)">
                                        Add Mount
                                    </button>
                                }
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        }
        else
        {
            <p>No NAS servers found. Deploy file-simulator first.</p>
        }
    </div>

    <div class="col-md-6">
        <h4>Active Mounts</h4>

        @if (activeMounts.Any())
        {
            <ul class="list-group">
                @foreach (var mount in activeMounts)
                {
                    <li class="list-group-item d-flex justify-content-between align-items-center">
                        <span>
                            <strong>@mount</strong>
                            <br/>
                            <small class="text-muted">Mounted at /mnt/@mount</small>
                        </span>
                        <button class="btn btn-sm btn-outline-danger" @onclick="() => RemoveMount(mount)">
                            Unmount
                        </button>
                    </li>
                }
            </ul>
        }
        else
        {
            <p>No active mounts. Add NAS servers from the available list.</p>
        }
    </div>
</div>

@code {
    private List<NasServerInfo>? availableServers;
    private List<string> activeMounts = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadServers();
        await LoadActiveMounts();
    }

    private async Task LoadServers()
    {
        try
        {
            var response = await Http.GetFromJsonAsync<List<NasServerInfo>>("/api/settings/nas");
            availableServers = response ?? new();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load NAS servers");
            availableServers = new();
        }
    }

    private async Task LoadActiveMounts()
    {
        try
        {
            var response = await Http.GetFromJsonAsync<List<string>>("/api/settings/nas/active");
            activeMounts = response ?? new();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load active mounts");
            activeMounts = new();
        }
    }

    private async Task AddMount(NasServerInfo server)
    {
        try
        {
            var request = new AddNasMountRequest
            {
                NasName = server.Name,
                Type = server.Type,
                PvName = server.PvName,
                PvcName = $"{server.Name}-pvc",
                DeploymentName = "your-app",  // From config
                MountPath = $"/mnt/{server.Name}",
                ReadOnly = server.Type == "Backup"
            };

            var response = await Http.PostAsJsonAsync("/api/settings/nas/add", request);
            response.EnsureSuccessStatusCode();

            await LoadActiveMounts();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to add mount {ServerName}", server.Name);
        }
    }

    private async Task RemoveMount(string nasName)
    {
        try
        {
            var response = await Http.DeleteAsync($"/api/settings/nas/remove?nasName={nasName}");
            response.EnsureSuccessStatusCode();

            await LoadActiveMounts();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove mount {NasName}", nasName);
        }
    }

    private string GetStatusBadge(string status) => status switch
    {
        "Bound" => "bg-success",
        "Available" => "bg-primary",
        "Pending" => "bg-warning",
        _ => "bg-secondary"
    };
}
```

---

## 11. React/TypeScript UI Example (Alternative)

### NAS Settings Component

```typescript
import React, { useState, useEffect } from 'react';
import axios from 'axios';

interface NasServerInfo {
    name: string;
    type: string;
    host: string;
    port: number;
    exportPath: string;
    pvName: string;
    status: string;
    capacity: string;
}

interface AddNasMountRequest {
    nasName: string;
    type: string;
    pvName: string;
    pvcName: string;
    deploymentName: string;
    mountPath: string;
    readOnly: boolean;
}

export const NasSettingsPage: React.FC = () => {
    const [availableServers, setAvailableServers] = useState<NasServerInfo[]>([]);
    const [activeMounts, setActiveMounts] = useState<string[]>([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        loadData();
    }, []);

    const loadData = async () => {
        setLoading(true);
        try {
            const [serversResp, mountsResp] = await Promise.all([
                axios.get<NasServerInfo[]>('/api/settings/nas'),
                axios.get<string[]>('/api/settings/nas/active')
            ]);

            setAvailableServers(serversResp.data);
            setActiveMounts(mountsResp.data);
        } catch (error) {
            console.error('Failed to load NAS data:', error);
        } finally {
            setLoading(false);
        }
    };

    const addMount = async (server: NasServerInfo) => {
        try {
            const request: AddNasMountRequest = {
                nasName: server.name,
                type: server.type,
                pvName: server.pvName,
                pvcName: `${server.name}-pvc`,
                deploymentName: 'your-app',  // From config or props
                mountPath: `/mnt/${server.name}`,
                readOnly: server.type === 'Backup'
            };

            await axios.post('/api/settings/nas/add', request);
            await loadData();  // Reload to show updated state
        } catch (error) {
            console.error(`Failed to add mount ${server.name}:`, error);
            alert(`Failed to add mount: ${error.response?.data || error.message}`);
        }
    };

    const removeMount = async (nasName: string) => {
        try {
            await axios.delete(`/api/settings/nas/remove?nasName=${nasName}`);
            await loadData();  // Reload to show updated state
        } catch (error) {
            console.error(`Failed to remove mount ${nasName}:`, error);
            alert(`Failed to remove mount: ${error.response?.data || error.message}`);
        }
    };

    if (loading) {
        return <div>Loading NAS servers...</div>;
    }

    return (
        <div className="container-fluid">
            <h2>NAS Server Configuration</h2>

            <div className="row">
                <div className="col-md-6">
                    <h4>Available NAS Servers</h4>
                    <table className="table table-striped">
                        <thead>
                            <tr>
                                <th>Name</th>
                                <th>Type</th>
                                <th>Host</th>
                                <th>Status</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {availableServers.map(server => (
                                <tr key={server.name}>
                                    <td>{server.name}</td>
                                    <td>
                                        <span className={`badge bg-${getTypeBadge(server.type)}`}>
                                            {server.type}
                                        </span>
                                    </td>
                                    <td>
                                        <small>{server.host}</small>
                                    </td>
                                    <td>
                                        <span className={`badge bg-${getStatusBadge(server.status)}`}>
                                            {server.status}
                                        </span>
                                    </td>
                                    <td>
                                        {activeMounts.includes(server.name) ? (
                                            <button
                                                className="btn btn-sm btn-danger"
                                                onClick={() => removeMount(server.name)}
                                            >
                                                Remove
                                            </button>
                                        ) : (
                                            <button
                                                className="btn btn-sm btn-success"
                                                onClick={() => addMount(server)}
                                                disabled={server.status !== 'Available' && server.status !== 'Bound'}
                                            >
                                                Add Mount
                                            </button>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                <div className="col-md-6">
                    <h4>Active Mounts</h4>
                    {activeMounts.length > 0 ? (
                        <ul className="list-group">
                            {activeMounts.map(mount => (
                                <li key={mount} className="list-group-item d-flex justify-content-between align-items-center">
                                    <div>
                                        <strong>{mount}</strong>
                                        <br/>
                                        <small className="text-muted">Mounted at /mnt/{mount}</small>
                                    </div>
                                    <button
                                        className="btn btn-sm btn-outline-danger"
                                        onClick={() => removeMount(mount)}
                                    >
                                        Unmount
                                    </button>
                                </li>
                            ))}
                        </ul>
                    ) : (
                        <p className="text-muted">No active mounts</p>
                    )}
                </div>
            </div>
        </div>
    );
};

function getTypeBadge(type: string): string {
    switch (type.toLowerCase()) {
        case 'input': return 'primary';
        case 'output': return 'success';
        case 'backup': return 'warning';
        default: return 'secondary';
    }
}

function getStatusBadge(status: string): string {
    switch (status) {
        case 'Bound': return 'success';
        case 'Available': return 'primary';
        case 'Pending': return 'warning';
        default: return 'secondary';
    }
}
```

---

## 12. Complete Example: Settings Service

### All-in-One Service for UI Backend

```csharp
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FileSimulator.K8s.Services;

public class FileAccessSettingsService
{
    private readonly Kubernetes _k8sClient;
    private readonly INasResourceService _nasService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileAccessSettingsService> _logger;

    private string AppNamespace => _configuration["Kubernetes:ApplicationNamespace"] ?? "default";
    private string DeploymentName => _configuration["Kubernetes:DeploymentName"] ?? "your-app";

    public FileAccessSettingsService(
        Kubernetes k8sClient,
        INasResourceService nasService,
        IConfiguration configuration,
        ILogger<FileAccessSettingsService> logger)
    {
        _k8sClient = k8sClient;
        _nasService = nasService;
        _configuration = configuration;
        _logger = logger;
    }

    // Get all file access services configured in the system
    public async Task<FileAccessConfiguration> GetConfigurationAsync(CancellationToken ct = default)
    {
        var config = new FileAccessConfiguration
        {
            NasServers = new List<NasServerStatus>()
        };

        // Get all NAS PVs
        var pvs = await _nasService.ListAllPersistentVolumesAsync(ct);

        foreach (var pv in pvs)
        {
            var nasName = pv.Metadata.Labels["nas-server"];
            var pvcName = $"{nasName}-pvc";

            // Check if PVC exists in application namespace
            var pvcStatus = "Not Claimed";
            try
            {
                var bindingStatus = await _nasService.GetPvcBindingStatusAsync(pvcName, AppNamespace, ct);
                pvcStatus = bindingStatus;
            }
            catch
            {
                // PVC doesn't exist in application namespace
            }

            // Check if mounted in deployment
            var activeMounts = await _nasService.GetDeploymentNasMountsAsync(DeploymentName, AppNamespace, ct);
            var isMounted = activeMounts.Contains(nasName);

            config.NasServers.Add(new NasServerStatus
            {
                Name = nasName,
                Type = pv.Metadata.Labels["nas-role"],
                Host = pv.Spec.Nfs.Server,
                Port = 2049,
                ExportPath = pv.Spec.Nfs.Path,
                PvName = pv.Metadata.Name,
                PvcName = pvcName,
                PvStatus = pv.Status?.Phase ?? "Unknown",
                PvcStatus = pvcStatus,
                IsMounted = isMounted,
                MountPath = isMounted ? $"/mnt/{nasName}" : null,
                ReadOnly = pv.Spec.Nfs.ReadOnlyProperty ?? false
            });
        }

        return config;
    }

    // Enable a NAS server (create PVC + add to deployment)
    public async Task<Result> EnableNasServerAsync(
        string nasName,
        string? customMountPath = null,
        CancellationToken ct = default)
    {
        try
        {
            // 1. Get PV
            var pvName = $"{nasName}-pv";
            var pv = await _nasService.GetPersistentVolumeAsync(pvName, ct);
            if (pv == null)
            {
                return Result.Failure($"PV {pvName} not found");
            }

            // 2. Create NAS config from PV
            var nasConfig = new NasServerConfig
            {
                Name = nasName,
                Type = pv.Metadata.Labels["nas-role"],
                Host = pv.Spec.Nfs.Server,
                ExportPath = pv.Spec.Nfs.Path,
                MountPath = customMountPath ?? $"/mnt/{nasName}",
                ReadOnly = pv.Spec.Nfs.ReadOnlyProperty ?? false,
                StorageCapacity = pv.Spec.Capacity["storage"].ToString(),
                AccessMode = pv.Spec.AccessModes.First(),
                Labels = pv.Metadata.Labels
            };

            // 3. Create PVC
            await _nasService.CreatePersistentVolumeClaimAsync(nasConfig, AppNamespace, ct);

            // Wait for binding
            await Task.Delay(2000, ct);

            // 4. Add to deployment
            await _nasService.AddNasMountToDeploymentAsync(DeploymentName, AppNamespace, nasConfig, ct);

            _logger.LogInformation("Enabled NAS server {NasName} in deployment {DeploymentName}", nasName, DeploymentName);

            return Result.Success($"NAS server {nasName} enabled successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable NAS server {NasName}", nasName);
            return Result.Failure(ex.Message);
        }
    }

    // Disable a NAS server (remove from deployment + delete PVC)
    public async Task<Result> DisableNasServerAsync(
        string nasName,
        CancellationToken ct = default)
    {
        try
        {
            var success = await _nasService.RemoveNasServerAsync(
                nasName,
                AppNamespace,
                DeploymentName,
                ct);

            if (success)
            {
                _logger.LogInformation("Disabled NAS server {NasName} from deployment {DeploymentName}", nasName, DeploymentName);
                return Result.Success($"NAS server {nasName} disabled successfully");
            }
            else
            {
                return Result.Failure($"Failed to disable NAS server {nasName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable NAS server {NasName}", nasName);
            return Result.Failure(ex.Message);
        }
    }

    // Update ConfigMap with currently enabled servers
    public async Task<Result> UpdateServiceDiscoveryAsync(CancellationToken ct = default)
    {
        try
        {
            var config = await GetConfigurationAsync(ct);
            var enabledServers = config.NasServers.Where(ns => ns.IsMounted).ToList();

            var nasConfigs = enabledServers.Select(ns => new NasServerConfig
            {
                Name = ns.Name,
                Type = ns.Type,
                Host = ns.Host,
                Port = ns.Port,
                ExportPath = ns.ExportPath,
                PvcName = ns.PvcName
            }).ToList();

            var minikubeIp = _configuration["Kubernetes:MinikubeIp"] ?? "172.25.201.3";
            await _nasService.CreateNasEndpointsConfigMapAsync(nasConfigs, AppNamespace, minikubeIp, ct);

            return Result.Success($"ConfigMap updated with {enabledServers.Count} NAS servers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ConfigMap");
            return Result.Failure(ex.Message);
        }
    }
}

// DTOs
public record FileAccessConfiguration
{
    public List<NasServerStatus> NasServers { get; init; } = new();
}

public record NasServerStatus
{
    public string Name { get; init; }
    public string Type { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public string ExportPath { get; init; }
    public string PvName { get; init; }
    public string PvcName { get; init; }
    public string PvStatus { get; init; }
    public string PvcStatus { get; init; }
    public bool IsMounted { get; init; }
    public string? MountPath { get; init; }
    public bool ReadOnly { get; init; }
}

public record Result
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; }

    public static Result Success(string message) => new() { IsSuccess = true, Message = message };
    public static Result Failure(string message) => new() { IsSuccess = false, Message = message };
}
```

---

## 13. Usage Examples

### Scenario 1: User Adds nas-input-1 from Settings UI

```csharp
// In your controller or service
var settingsService = new FileAccessSettingsService(k8sClient, nasService, configuration, logger);

// User clicks "Add nas-input-1" in UI
var result = await settingsService.EnableNasServerAsync("nas-input-1");

if (result.IsSuccess)
{
    // Success! The deployment now has:
    // - PVC nas-input-1-pvc created in application namespace
    // - Volume mount at /mnt/nas-input-1 in deployment
    // - Pod will restart automatically to apply new mount

    // Update ConfigMap so environment variables are available
    await settingsService.UpdateServiceDiscoveryAsync();
}
```

**What happens behind the scenes:**
1. PV `nas-input-1-pv` already exists (from file-simulator examples)
2. PVC `nas-input-1-pvc` created in your namespace
3. PVC binds to PV via label selector (`nas-server: nas-input-1`)
4. Deployment updated with new volume and volumeMount
5. Kubernetes triggers rolling update (pod restarts with new mount)
6. Application can now read/write files at `/mnt/nas-input-1/`

### Scenario 2: User Removes nas-output-2 from Settings UI

```csharp
// User clicks "Remove nas-output-2" in UI
var result = await settingsService.DisableNasServerAsync("nas-output-2");

if (result.IsSuccess)
{
    // Success! The deployment no longer has:
    // - Volume mount at /mnt/nas-output-2
    // - PVC nas-output-2-pvc deleted
    // - PV nas-output-2-pv remains available for future use

    // Update ConfigMap to reflect change
    await settingsService.UpdateServiceDiscoveryAsync();
}
```

### Scenario 3: Initialize All 7 File Simulator NAS Servers

```csharp
// Run on application startup or via admin endpoint
public async Task<Result> InitializeFileSimulatorAsync(CancellationToken ct = default)
{
    var minikubeIp = "172.25.201.3";  // Get from config or Minikube API
    var servers = NasServerDefaults.GetFileSimulatorServers(minikubeIp);

    // Provision PVs for all 7 servers (cluster-scoped, only once)
    foreach (var server in servers)
    {
        try
        {
            await _nasService.CreatePersistentVolumeAsync(server, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PV for {ServerName}", server.Name);
        }
    }

    // Create ConfigMap with all endpoints
    await _nasService.CreateNasEndpointsConfigMapAsync(servers, AppNamespace, minikubeIp, ct);

    return Result.Success($"Initialized {servers.Count} NAS servers");
}

// Add specific servers to your deployment
public async Task<Result> EnableInputServersAsync(CancellationToken ct = default)
{
    var inputServers = new[] { "nas-input-1", "nas-input-2", "nas-input-3" };

    foreach (var nasName in inputServers)
    {
        await settingsService.EnableNasServerAsync(nasName, ct);
    }

    return Result.Success($"Enabled {inputServers.Length} input servers");
}
```

---

## 14. Environment Variables from ConfigMap

### Accessing NAS Configuration at Runtime

```csharp
public class NasConnectionResolver
{
    private readonly IConfiguration _configuration;

    public NasConnectionResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetNasHost(string nasName)
    {
        var envVar = $"NAS_{nasName.ToUpper().Replace("-", "_")}_HOST";
        return _configuration[envVar] ?? throw new InvalidOperationException($"{envVar} not found in configuration");
    }

    public int GetNasPort(string nasName)
    {
        var envVar = $"NAS_{nasName.ToUpper().Replace("-", "_")}_PORT";
        return int.Parse(_configuration[envVar] ?? "2049");
    }

    public string GetNasPath(string nasName)
    {
        var envVar = $"NAS_{nasName.ToUpper().Replace("-", "_")}_PATH";
        return _configuration[envVar] ?? "/data";
    }

    public string GetPvcName(string nasName)
    {
        var envVar = $"NAS_{nasName.ToUpper().Replace("-", "_")}_PVC";
        return _configuration[envVar] ?? $"{nasName}-pvc";
    }

    // Usage example
    public string GetFullNfsPath(string nasName, string subPath = "")
    {
        var host = GetNasHost(nasName);
        var path = GetNasPath(nasName);
        var fullPath = $"{path.TrimEnd('/')}/{subPath.TrimStart('/')}";

        return $"{host}:{fullPath}";
    }
}

// In deployment YAML
envFrom:
  - configMapRef:
      name: file-simulator-nas-endpoints

// Or inject specific values
env:
  - name: INPUT_NAS_HOST
    valueFrom:
      configMapKeyRef:
        name: file-simulator-nas-endpoints
        key: NAS_INPUT_1_HOST
  - name: INPUT_NAS_PORT
    valueFrom:
      configMapKeyRef:
        name: file-simulator-nas-endpoints
        key: NAS_INPUT_1_PORT
```

---

## 15. Testing

### Unit Test Example

```csharp
using Xunit;
using Moq;
using k8s;
using k8s.Models;

public class NasResourceServiceTests
{
    [Fact]
    public async Task CreatePersistentVolume_CreatesWithCorrectNfsConfig()
    {
        // Arrange
        var mockClient = new Mock<Kubernetes>();
        var logger = Mock.Of<ILogger<NasResourceService>>();
        var service = new NasResourceService(mockClient.Object, logger);

        var nasConfig = new NasServerConfig
        {
            Name = "nas-input-1",
            Type = "Input",
            Host = "file-sim-nas-input-1.file-simulator.svc.cluster.local",
            Port = 2049,
            ExportPath = "/data"
        };

        V1PersistentVolume? capturedPv = null;
        mockClient.Setup(c => c.CoreV1.CreatePersistentVolumeAsync(
                It.IsAny<V1PersistentVolume>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .Callback<V1PersistentVolume, string, string, string, CancellationToken>((pv, _, _, _, _) => capturedPv = pv)
            .ReturnsAsync(new V1PersistentVolume());

        // Act
        await service.CreatePersistentVolumeAsync(nasConfig);

        // Assert
        Assert.NotNull(capturedPv);
        Assert.Equal("nas-input-1-pv", capturedPv.Metadata.Name);
        Assert.Equal("file-sim-nas-input-1.file-simulator.svc.cluster.local", capturedPv.Spec.Nfs.Server);
        Assert.Equal("/data", capturedPv.Spec.Nfs.Path);
        Assert.Contains("ReadWriteMany", capturedPv.Spec.AccessModes);
        Assert.Equal("Retain", capturedPv.Spec.PersistentVolumeReclaimPolicy);
    }
}
```

---

## 16. Best Practices

### Error Handling

Always handle common Kubernetes exceptions:

```csharp
try
{
    await _k8sClient.CoreV1.CreateNamespacedPersistentVolumeClaimAsync(pvc, namespace_);
}
catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Conflict)
{
    // Resource already exists - decide whether to update or skip
}
catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
{
    // Namespace or related resource doesn't exist
}
catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.Forbidden)
{
    // Insufficient permissions (RBAC issue)
}
catch (k8s.Autorest.HttpOperationException ex)
{
    // Other HTTP errors
    _logger.LogError("Kubernetes API error: {StatusCode} - {Content}", ex.Response.StatusCode, ex.Response.Content);
}
```

### Resource Lifecycle Management

**Create Order:**
1. PersistentVolume (cluster-scoped)
2. PersistentVolumeClaim (namespace-scoped)
3. Wait for PVC to bind
4. ConfigMap (optional, for service discovery)
5. Update Deployment with volume mount

**Delete Order:**
1. Remove from Deployment (triggers pod restart)
2. Delete PVC
3. Wait for PVC deletion to complete
4. Delete PV (if you want to remove it entirely)
5. Update ConfigMap to remove entries

### RBAC Requirements

Your application's ServiceAccount needs permissions:

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: nas-manager
rules:
  # PV operations (cluster-scoped)
  - apiGroups: [""]
    resources: ["persistentvolumes"]
    verbs: ["get", "list", "create", "update", "patch", "delete"]

  # PVC operations (namespace-scoped)
  - apiGroups: [""]
    resources: ["persistentvolumeclaims"]
    verbs: ["get", "list", "create", "update", "patch", "delete"]

  # ConfigMap operations
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list", "create", "update", "patch", "delete"]

  # Deployment operations
  - apiGroups: ["apps"]
    resources: ["deployments"]
    verbs: ["get", "list", "update", "patch"]

---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: nas-manager-binding
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: nas-manager
subjects:
  - kind: ServiceAccount
    name: your-app-sa
    namespace: default
```

---

## 17. Quick Reference

### Common Operations

```csharp
// Initialize all file-simulator NAS servers
var servers = NasServerDefaults.GetFileSimulatorServers("172.25.201.3");
foreach (var server in servers)
{
    await nasService.CreatePersistentVolumeAsync(server);
}

// Add nas-input-1 to your deployment
await settingsService.EnableNasServerAsync("nas-input-1");

// Remove nas-output-3 from your deployment
await settingsService.DisableNasServerAsync("nas-output-3");

// Get current configuration
var config = await settingsService.GetConfigurationAsync();

// List all available NAS servers
var pvs = await nasService.ListAllPersistentVolumesAsync();

// Check if PVC is bound
var status = await nasService.GetPvcBindingStatusAsync("nas-input-1-pvc", "default");

// Update ConfigMap with active mounts
await settingsService.UpdateServiceDiscoveryAsync();
```

---

## 18. Related Documentation

- **NAS Integration Guide:** [`NAS-INTEGRATION-GUIDE.md`](../helm-chart/file-simulator/docs/NAS-INTEGRATION-GUIDE.md)
- **Server Connection Properties:** [`SERVER-CONNECTION-PROPERTIES.md`](SERVER-CONNECTION-PROPERTIES.md)
- **Example PV/PVC Manifests:** [`../helm-chart/file-simulator/examples/`](../helm-chart/file-simulator/examples/)
- **Multi-Mount Example:** [`../helm-chart/file-simulator/examples/deployments/multi-mount-example.yaml`](../helm-chart/file-simulator/examples/deployments/multi-mount-example.yaml)

---

**Version:** v1.0 (2026-02-01) | For milestone details, see [.planning/MILESTONES.md](../.planning/MILESTONES.md)
