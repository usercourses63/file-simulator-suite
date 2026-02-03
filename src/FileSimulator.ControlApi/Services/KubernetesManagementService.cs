namespace FileSimulator.ControlApi.Services;

using k8s;
using k8s.Models;
using FileSimulator.ControlApi.Models;

/// <summary>
/// Service for creating, updating, and deleting dynamic server instances.
/// Uses Kubernetes API directly for runtime management with ownerReferences
/// pointing to the control plane pod for automatic garbage collection.
/// </summary>
public class KubernetesManagementService : IKubernetesManagementService
{
    private readonly IKubernetes _client;
    private readonly IConfigMapUpdateService _configMapService;
    private readonly ILogger<KubernetesManagementService> _logger;
    private readonly string _namespace;
    private readonly string _releasePrefix;
    private readonly string _pvcName;

    // Labels used for dynamic resources
    private const string LabelAppName = "app.kubernetes.io/name";
    private const string LabelComponent = "app.kubernetes.io/component";
    private const string LabelManagedBy = "app.kubernetes.io/managed-by";
    private const string LabelInstance = "app.kubernetes.io/instance";
    private const string LabelPartOf = "app.kubernetes.io/part-of";

    public KubernetesManagementService(
        IKubernetes client,
        IConfigMapUpdateService configMapService,
        ILogger<KubernetesManagementService> logger,
        IConfiguration configuration)
    {
        _client = client;
        _configMapService = configMapService;
        _logger = logger;
        _namespace = configuration["Kubernetes:Namespace"] ?? "file-simulator";
        _releasePrefix = configuration["Kubernetes:ReleasePrefix"] ?? "file-sim-file-simulator";
        _pvcName = configuration["Kubernetes:PvcName"] ?? $"{_releasePrefix}-pvc";
    }

    /// <inheritdoc />
    public async Task<V1Pod> GetControlPlanePodAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Looking for control plane pod in namespace {Namespace}", _namespace);

        var pods = await _client.CoreV1.ListNamespacedPodAsync(
            _namespace,
            labelSelector: $"{LabelComponent}=control-api",
            cancellationToken: ct);

        var controlPod = pods.Items.FirstOrDefault(p =>
            p.Status?.Phase == "Running" &&
            p.Metadata?.Name?.Contains("control-api") == true);

        if (controlPod == null)
        {
            _logger.LogError("Control plane pod not found or not running. Found {Count} pods with control-api label",
                pods.Items.Count);
            throw new InvalidOperationException("Control plane pod not found or not running");
        }

        _logger.LogDebug("Found control plane pod: {PodName} (UID: {Uid})",
            controlPod.Metadata.Name, controlPod.Metadata.Uid);

        return controlPod;
    }

    /// <inheritdoc />
    public async Task<bool> IsServerNameAvailableAsync(string name, CancellationToken ct = default)
    {
        _logger.LogDebug("Checking if server name '{Name}' is available", name);

        // Check if any deployment exists with this instance name
        var deployments = await _client.AppsV1.ListNamespacedDeploymentAsync(
            _namespace,
            labelSelector: $"{LabelInstance}={name}",
            cancellationToken: ct);

        var available = !deployments.Items.Any();
        _logger.LogDebug("Server name '{Name}' is {Status}", name, available ? "available" : "already in use");

        return available;
    }

    /// <inheritdoc />
    public async Task<DiscoveredServer> CreateFtpServerAsync(CreateFtpServerRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating FTP server '{Name}' with username '{Username}'",
            request.Name, request.Username);

        // Validate name availability
        if (!await IsServerNameAvailableAsync(request.Name, ct))
        {
            throw new InvalidOperationException($"Server name '{request.Name}' is already in use");
        }

        // 1. Get control plane pod for ownerReference
        var controlPod = await GetControlPlanePodAsync(ct);

        var resourceName = $"{_releasePrefix}-ftp-{request.Name}";

        // Standard labels for file-simulator resources
        var labels = new Dictionary<string, string>
        {
            [LabelAppName] = "file-simulator",
            [LabelComponent] = "ftp",
            [LabelManagedBy] = "control-api",
            [LabelInstance] = request.Name,
            [LabelPartOf] = "file-simulator-suite"
        };

        // OwnerReference to control plane POD - ensures cleanup when control plane restarts
        var ownerRef = new V1OwnerReference
        {
            ApiVersion = "v1",
            Kind = "Pod",
            Name = controlPod.Metadata.Name,
            Uid = controlPod.Metadata.Uid,
            Controller = true,
            BlockOwnerDeletion = true
        };

        _logger.LogDebug("Creating FTP deployment {ResourceName} with owner {OwnerPod}",
            resourceName, controlPod.Metadata.Name);

        // 2. Create Deployment
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                NamespaceProperty = _namespace,
                Labels = labels,
                OwnerReferences = new List<V1OwnerReference> { ownerRef }
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        [LabelAppName] = "file-simulator",
                        [LabelInstance] = request.Name
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta { Labels = labels },
                    Spec = new V1PodSpec
                    {
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = "vsftpd",
                                Image = "fauria/vsftpd:latest",
                                ImagePullPolicy = "IfNotPresent",
                                Ports = new List<V1ContainerPort>
                                {
                                    new V1ContainerPort
                                    {
                                        ContainerPort = 21,
                                        Protocol = "TCP",
                                        Name = "ftp"
                                    }
                                },
                                Env = new List<V1EnvVar>
                                {
                                    new V1EnvVar { Name = "FTP_USER", Value = request.Username },
                                    new V1EnvVar { Name = "FTP_PASS", Value = request.Password },
                                    new V1EnvVar { Name = "LOG_STDOUT", Value = "YES" },
                                    new V1EnvVar { Name = "LOCAL_UMASK", Value = "022" }
                                },
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new V1VolumeMount
                                    {
                                        Name = "data",
                                        MountPath = $"/home/vsftpd/{request.Username}"
                                    }
                                },
                                SecurityContext = new V1SecurityContext { Privileged = true },
                                Resources = new V1ResourceRequirements
                                {
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("64Mi"),
                                        ["cpu"] = new ResourceQuantity("50m")
                                    },
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("256Mi"),
                                        ["cpu"] = new ResourceQuantity("200m")
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
                                    ClaimName = _pvcName
                                }
                            }
                        }
                    }
                }
            }
        };

        var createdDeployment = await _client.AppsV1.CreateNamespacedDeploymentAsync(
            deployment, _namespace, cancellationToken: ct);

        _logger.LogDebug("Created FTP deployment {ResourceName}", resourceName);

        // 3. Create Service
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                NamespaceProperty = _namespace,
                Labels = labels,
                OwnerReferences = new List<V1OwnerReference> { ownerRef }
            },
            Spec = new V1ServiceSpec
            {
                Type = "NodePort",
                Selector = new Dictionary<string, string>
                {
                    [LabelAppName] = "file-simulator",
                    [LabelInstance] = request.Name
                },
                Ports = new List<V1ServicePort>
                {
                    new V1ServicePort
                    {
                        Port = 21,
                        TargetPort = 21,
                        Protocol = "TCP",
                        Name = "ftp",
                        NodePort = request.NodePort  // null = auto-assign
                    }
                }
            }
        };

        var createdService = await _client.CoreV1.CreateNamespacedServiceAsync(
            service, _namespace, cancellationToken: ct);

        var assignedNodePort = createdService.Spec.Ports[0].NodePort;

        _logger.LogInformation("Created FTP server '{Name}' with NodePort {NodePort}, owner pod: {OwnerPod}",
            request.Name, assignedNodePort, controlPod.Metadata.Name);

        return new DiscoveredServer
        {
            Name = request.Name,
            Protocol = "FTP",
            PodName = $"{resourceName}-pending",  // Pod name not yet assigned by ReplicaSet
            ServiceName = createdService.Metadata.Name,
            ClusterIp = createdService.Spec.ClusterIP ?? "",
            Port = 21,
            NodePort = assignedNodePort,
            PodStatus = "Pending",
            PodReady = false,
            IsDynamic = true,
            ManagedBy = "control-api"
        };
    }

    /// <inheritdoc />
    public async Task<DiscoveredServer> CreateSftpServerAsync(CreateSftpServerRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating SFTP server '{Name}' with username '{Username}'",
            request.Name, request.Username);

        // Validate name availability
        if (!await IsServerNameAvailableAsync(request.Name, ct))
        {
            throw new InvalidOperationException($"Server name '{request.Name}' is already in use");
        }

        // 1. Get control plane pod for ownerReference
        var controlPod = await GetControlPlanePodAsync(ct);

        var resourceName = $"{_releasePrefix}-sftp-{request.Name}";

        // Standard labels for file-simulator resources
        var labels = new Dictionary<string, string>
        {
            [LabelAppName] = "file-simulator",
            [LabelComponent] = "sftp",
            [LabelManagedBy] = "control-api",
            [LabelInstance] = request.Name,
            [LabelPartOf] = "file-simulator-suite"
        };

        // OwnerReference to control plane POD - ensures cleanup when control plane restarts
        var ownerRef = new V1OwnerReference
        {
            ApiVersion = "v1",
            Kind = "Pod",
            Name = controlPod.Metadata.Name,
            Uid = controlPod.Metadata.Uid,
            Controller = true,
            BlockOwnerDeletion = true
        };

        _logger.LogDebug("Creating SFTP deployment {ResourceName} with owner {OwnerPod}",
            resourceName, controlPod.Metadata.Name);

        // 2. Create Deployment - atmoz/sftp uses Args format: "username:password:uid:gid"
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                NamespaceProperty = _namespace,
                Labels = labels,
                OwnerReferences = new List<V1OwnerReference> { ownerRef }
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        [LabelAppName] = "file-simulator",
                        [LabelInstance] = request.Name
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta { Labels = labels },
                    Spec = new V1PodSpec
                    {
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = "sftp",
                                Image = "atmoz/sftp:latest",
                                ImagePullPolicy = "IfNotPresent",
                                Ports = new List<V1ContainerPort>
                                {
                                    new V1ContainerPort
                                    {
                                        ContainerPort = 22,
                                        Protocol = "TCP",
                                        Name = "sftp"
                                    }
                                },
                                // User format: username:password:uid:gid
                                Args = new List<string>
                                {
                                    $"{request.Username}:{request.Password}:{request.Uid}:{request.Gid}"
                                },
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new V1VolumeMount
                                    {
                                        Name = "data",
                                        MountPath = $"/home/{request.Username}/data"
                                    }
                                },
                                SecurityContext = new V1SecurityContext
                                {
                                    Capabilities = new V1Capabilities
                                    {
                                        Add = new List<string> { "SYS_CHROOT" }
                                    }
                                },
                                Resources = new V1ResourceRequirements
                                {
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("64Mi"),
                                        ["cpu"] = new ResourceQuantity("50m")
                                    },
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("256Mi"),
                                        ["cpu"] = new ResourceQuantity("200m")
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
                                    ClaimName = _pvcName
                                }
                            }
                        }
                    }
                }
            }
        };

        var createdDeployment = await _client.AppsV1.CreateNamespacedDeploymentAsync(
            deployment, _namespace, cancellationToken: ct);

        _logger.LogDebug("Created SFTP deployment {ResourceName}", resourceName);

        // 3. Create Service
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                NamespaceProperty = _namespace,
                Labels = labels,
                OwnerReferences = new List<V1OwnerReference> { ownerRef }
            },
            Spec = new V1ServiceSpec
            {
                Type = "NodePort",
                Selector = new Dictionary<string, string>
                {
                    [LabelAppName] = "file-simulator",
                    [LabelInstance] = request.Name
                },
                Ports = new List<V1ServicePort>
                {
                    new V1ServicePort
                    {
                        Port = 22,
                        TargetPort = 22,
                        Protocol = "TCP",
                        Name = "sftp",
                        NodePort = request.NodePort  // null = auto-assign
                    }
                }
            }
        };

        var createdService = await _client.CoreV1.CreateNamespacedServiceAsync(
            service, _namespace, cancellationToken: ct);

        var assignedNodePort = createdService.Spec.Ports[0].NodePort;

        _logger.LogInformation("Created SFTP server '{Name}' with NodePort {NodePort}, owner pod: {OwnerPod}",
            request.Name, assignedNodePort, controlPod.Metadata.Name);

        return new DiscoveredServer
        {
            Name = request.Name,
            Protocol = "SFTP",
            PodName = $"{resourceName}-pending",  // Pod name not yet assigned by ReplicaSet
            ServiceName = createdService.Metadata.Name,
            ClusterIp = createdService.Spec.ClusterIP ?? "",
            Port = 22,
            NodePort = assignedNodePort,
            PodStatus = "Pending",
            PodReady = false,
            IsDynamic = true,
            ManagedBy = "control-api"
        };
    }

    /// <inheritdoc />
    public async Task<DiscoveredServer> CreateNasServerAsync(CreateNasServerRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating NAS server '{Name}' with directory '{Directory}'",
            request.Name, request.Directory);

        // Validate name availability
        if (!await IsServerNameAvailableAsync(request.Name, ct))
        {
            throw new InvalidOperationException($"Server name '{request.Name}' is already in use");
        }

        // 1. Get control plane pod for ownerReference
        var controlPod = await GetControlPlanePodAsync(ct);

        var resourceName = $"{_releasePrefix}-nas-{request.Name}";

        // Resolve directory preset or use custom
        var directory = request.Directory.ToLower() switch
        {
            "input" => "nas-input-dynamic",
            "output" => "nas-output-dynamic",
            "backup" => "nas-backup-dynamic",
            _ => request.Directory
        };

        // Standard labels for file-simulator resources
        var labels = new Dictionary<string, string>
        {
            [LabelAppName] = "file-simulator",
            [LabelComponent] = "nas",
            [LabelManagedBy] = "control-api",
            [LabelInstance] = request.Name,
            [LabelPartOf] = "file-simulator-suite"
        };

        // OwnerReference to control plane POD - ensures cleanup when control plane restarts
        var ownerRef = new V1OwnerReference
        {
            ApiVersion = "v1",
            Kind = "Pod",
            Name = controlPod.Metadata.Name,
            Uid = controlPod.Metadata.Uid,
            Controller = true,
            BlockOwnerDeletion = true
        };

        _logger.LogDebug("Creating NAS deployment {ResourceName} with owner {OwnerPod}, directory {Directory}",
            resourceName, controlPod.Metadata.Name, directory);

        // 2. Create Deployment - erichough/nfs-server with subdirectory on shared PVC
        var deployment = new V1Deployment
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                NamespaceProperty = _namespace,
                Labels = labels,
                OwnerReferences = new List<V1OwnerReference> { ownerRef }
            },
            Spec = new V1DeploymentSpec
            {
                Replicas = 1,
                Selector = new V1LabelSelector
                {
                    MatchLabels = new Dictionary<string, string>
                    {
                        [LabelAppName] = "file-simulator",
                        [LabelInstance] = request.Name
                    }
                },
                Template = new V1PodTemplateSpec
                {
                    Metadata = new V1ObjectMeta { Labels = labels },
                    Spec = new V1PodSpec
                    {
                        Containers = new List<V1Container>
                        {
                            new V1Container
                            {
                                Name = "nfs-server",
                                Image = "erichough/nfs-server:latest",
                                ImagePullPolicy = "IfNotPresent",
                                Ports = new List<V1ContainerPort>
                                {
                                    new V1ContainerPort { ContainerPort = 2049, Protocol = "TCP", Name = "nfs" },
                                    new V1ContainerPort { ContainerPort = 111, Protocol = "TCP", Name = "rpcbind" }
                                },
                                Env = new List<V1EnvVar>
                                {
                                    new V1EnvVar
                                    {
                                        Name = "NFS_EXPORT_0",
                                        Value = $"/data *({request.ExportOptions},fsid=0)"
                                    },
                                    new V1EnvVar { Name = "NFS_DISABLE_VERSION_3", Value = "false" },
                                    new V1EnvVar { Name = "NFS_LOG_LEVEL", Value = "DEBUG" }
                                },
                                VolumeMounts = new List<V1VolumeMount>
                                {
                                    new V1VolumeMount
                                    {
                                        Name = "data",
                                        MountPath = "/data",
                                        SubPath = directory  // Use subdirectory on shared PVC
                                    }
                                },
                                SecurityContext = new V1SecurityContext
                                {
                                    Privileged = true,
                                    Capabilities = new V1Capabilities
                                    {
                                        Add = new List<string> { "SYS_ADMIN", "DAC_READ_SEARCH" }
                                    }
                                },
                                Resources = new V1ResourceRequirements
                                {
                                    Requests = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("128Mi"),
                                        ["cpu"] = new ResourceQuantity("100m")
                                    },
                                    Limits = new Dictionary<string, ResourceQuantity>
                                    {
                                        ["memory"] = new ResourceQuantity("512Mi"),
                                        ["cpu"] = new ResourceQuantity("500m")
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
                                    ClaimName = _pvcName
                                }
                            }
                        }
                    }
                }
            }
        };

        var createdDeployment = await _client.AppsV1.CreateNamespacedDeploymentAsync(
            deployment, _namespace, cancellationToken: ct);

        _logger.LogDebug("Created NAS deployment {ResourceName}", resourceName);

        // 3. Create Service
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta
            {
                Name = resourceName,
                NamespaceProperty = _namespace,
                Labels = labels,
                OwnerReferences = new List<V1OwnerReference> { ownerRef }
            },
            Spec = new V1ServiceSpec
            {
                Type = "NodePort",
                Selector = new Dictionary<string, string>
                {
                    [LabelAppName] = "file-simulator",
                    [LabelInstance] = request.Name
                },
                Ports = new List<V1ServicePort>
                {
                    new V1ServicePort
                    {
                        Port = 2049,
                        TargetPort = 2049,
                        Protocol = "TCP",
                        Name = "nfs",
                        NodePort = request.NodePort  // null = auto-assign
                    }
                }
            }
        };

        var createdService = await _client.CoreV1.CreateNamespacedServiceAsync(
            service, _namespace, cancellationToken: ct);

        var assignedNodePort = createdService.Spec.Ports[0].NodePort;

        _logger.LogInformation("Created NAS server '{Name}' at directory '{Directory}' with NodePort {NodePort}, owner pod: {OwnerPod}",
            request.Name, directory, assignedNodePort, controlPod.Metadata.Name);

        return new DiscoveredServer
        {
            Name = request.Name,
            Protocol = "NFS",
            PodName = $"{resourceName}-pending",  // Pod name not yet assigned by ReplicaSet
            ServiceName = createdService.Metadata.Name,
            ClusterIp = createdService.Spec.ClusterIP ?? "",
            Port = 2049,
            NodePort = assignedNodePort,
            PodStatus = "Pending",
            PodReady = false,
            IsDynamic = true,
            ManagedBy = "control-api"
        };
    }

    /// <inheritdoc />
    public async Task DeleteServerAsync(string serverName, bool deleteData = false, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting server '{ServerName}', deleteData={DeleteData}", serverName, deleteData);

        var labelSelector = $"{LabelInstance}={serverName},{LabelManagedBy}=control-api";

        // 1. Check if this is a dynamic server (managed by control-api)
        var deployments = await _client.AppsV1.ListNamespacedDeploymentAsync(
            _namespace,
            labelSelector: labelSelector,
            cancellationToken: ct);

        if (!deployments.Items.Any())
        {
            throw new InvalidOperationException(
                $"Server '{serverName}' not found or is managed by Helm (cannot delete static servers via API)");
        }

        // 2. Delete services explicitly first (do NOT cascade from deployment)
        var services = await _client.CoreV1.ListNamespacedServiceAsync(
            _namespace,
            labelSelector: labelSelector,
            cancellationToken: ct);

        foreach (var service in services.Items)
        {
            _logger.LogDebug("Deleting service {Name}", service.Metadata.Name);
            await _client.CoreV1.DeleteNamespacedServiceAsync(
                service.Metadata.Name,
                _namespace,
                cancellationToken: ct);
        }

        // 3. Delete deployments (pods cascade via ownerReferences)
        foreach (var deployment in deployments.Items)
        {
            _logger.LogDebug("Deleting deployment {Name}", deployment.Metadata.Name);
            await _client.AppsV1.DeleteNamespacedDeploymentAsync(
                deployment.Metadata.Name,
                _namespace,
                propagationPolicy: "Foreground",  // Wait for pods to terminate
                cancellationToken: ct);
        }

        // 4. Delete PVC if requested (for NAS servers with dedicated storage)
        // Note: Our NAS uses subdirectory on shared PVC, so we don't delete PVC
        // deleteData flag is for future use or could trigger file cleanup
        if (deleteData)
        {
            _logger.LogInformation("deleteData=true but NAS uses shared PVC subdirectory; " +
                "directory cleanup would require file system access");
            // Future: Could call file cleanup API to delete files in the subdirectory
        }

        _logger.LogInformation("Deleted server '{ServerName}'", serverName);
    }

    /// <inheritdoc />
    public Task StopServerAsync(string serverName, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in plan 11-04");

    /// <inheritdoc />
    public Task StartServerAsync(string serverName, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in plan 11-04");

    /// <inheritdoc />
    public Task RestartServerAsync(string serverName, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in plan 11-04");
}
