namespace FileSimulator.ControlApi.Services;

using k8s;
using k8s.Models;
using FileSimulator.ControlApi.Models;
using Microsoft.Extensions.Options;

public class KubernetesOptions
{
    public bool InCluster { get; set; } = true;
    public string Namespace { get; set; } = "file-simulator";
    public string? ExternalHost { get; set; }
}

public class KubernetesDiscoveryService : IKubernetesDiscoveryService
{
    private readonly IKubernetes _client;
    private readonly KubernetesOptions _options;
    private readonly ILogger<KubernetesDiscoveryService> _logger;

    // Label selectors for file-simulator components
    private const string AppLabel = "app.kubernetes.io/name";
    private const string AppValue = "file-simulator";
    private const string ManagedByLabel = "app.kubernetes.io/managed-by";

    // Protocol detection from deployment names
    // Order matters: check more specific patterns first (sftp before ftp, webdav before http)
    private static readonly (string Key, string Protocol)[] ProtocolMappings =
    [
        ("management", "Management"),  // FileBrowser UI - check before http
        ("sftp", "SFTP"),              // Check before ftp (sftp contains ftp)
        ("ftp", "FTP"),
        ("nas", "NFS"),
        ("webdav", "WebDAV"),          // Check before http (for write operations)
        ("http", "HTTP"),              // Read-only HTTP server
        ("s3", "S3"),
        ("smb", "SMB")
    ];

    public KubernetesDiscoveryService(
        IOptions<KubernetesOptions> options,
        ILogger<KubernetesDiscoveryService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Create K8s client based on environment
        if (_options.InCluster)
        {
            var config = KubernetesClientConfiguration.InClusterConfig();
            _client = new Kubernetes(config);
            _logger.LogInformation("Kubernetes client configured for in-cluster access");
        }
        else
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
            _client = new Kubernetes(config);
            _logger.LogInformation("Kubernetes client configured from kubeconfig");
        }
    }

    public async Task<IReadOnlyList<DiscoveredServer>> DiscoverServersAsync(CancellationToken ct = default)
    {
        var servers = new List<DiscoveredServer>();

        try
        {
            // Get all pods in namespace with file-simulator labels
            var pods = await _client.CoreV1.ListNamespacedPodAsync(
                _options.Namespace,
                labelSelector: $"{AppLabel}={AppValue}",
                cancellationToken: ct);

            // Get all services in namespace
            var services = await _client.CoreV1.ListNamespacedServiceAsync(
                _options.Namespace,
                labelSelector: $"{AppLabel}={AppValue}",
                cancellationToken: ct);

            // Get all deployments to extract credentials from env vars and args
            var deployments = await _client.AppsV1.ListNamespacedDeploymentAsync(
                _options.Namespace,
                labelSelector: $"{AppLabel}={AppValue}",
                cancellationToken: ct);

            _logger.LogDebug("Found {PodCount} pods, {ServiceCount} services, {DeploymentCount} deployments",
                pods.Items.Count, services.Items.Count, deployments.Items.Count);

            // Match pods to services
            foreach (var pod in pods.Items)
            {
                // Skip control-api itself
                if (pod.Metadata.Name.Contains("control-api"))
                    continue;

                var protocol = DetectProtocol(pod.Metadata.Name);
                if (protocol == null)
                {
                    _logger.LogDebug("Skipping pod {Pod} - unknown protocol", pod.Metadata.Name);
                    continue;
                }

                // Find matching service
                var service = FindMatchingService(pod, services.Items);
                if (service == null)
                {
                    _logger.LogWarning("No service found for pod {Pod}", pod.Metadata.Name);
                    continue;
                }

                var port = service.Spec.Ports.FirstOrDefault();
                var nodePort = port?.NodePort;

                // Determine if server is dynamic (managed by control-api vs Helm)
                var podLabels = pod.Metadata.Labels ?? new Dictionary<string, string>();
                var managedBy = podLabels.TryGetValue(ManagedByLabel, out var manager) ? manager : "Helm";
                var isDynamic = managedBy == "control-api";

                // For dynamic servers, use instance label; for Helm, use parsed name
                var serverName = isDynamic && podLabels.TryGetValue("app.kubernetes.io/instance", out var instance)
                    ? instance
                    : GetServerName(pod.Metadata.Name);

                // Extract directory info based on protocol and server name
                var directory = GetServerDirectory(serverName, protocol, pod);

                // Extract credentials from matching deployment
                var deployment = FindMatchingDeployment(pod, deployments.Items);
                var credentials = ExtractCredentials(protocol, deployment);

                servers.Add(new DiscoveredServer
                {
                    Name = serverName,
                    PodName = pod.Metadata.Name,
                    Protocol = protocol,
                    ServiceName = service.Metadata.Name,
                    ClusterIp = service.Spec.ClusterIP,
                    Port = port?.Port ?? 0,
                    NodePort = nodePort,
                    PodStatus = pod.Status.Phase,
                    PodReady = IsPodReady(pod),
                    IsDynamic = isDynamic,
                    ManagedBy = managedBy,
                    Directory = directory,
                    Credentials = credentials
                });
            }

            _logger.LogInformation("Discovered {Count} protocol servers", servers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover servers from Kubernetes API");
        }

        return servers;
    }

    public async Task<DiscoveredServer?> GetServerAsync(string name, CancellationToken ct = default)
    {
        var servers = await DiscoverServersAsync(ct);
        return servers.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? DetectProtocol(string podName)
    {
        // Array order ensures specific matches come first (sftp before ftp, management before http)
        foreach (var (key, protocol) in ProtocolMappings)
        {
            if (podName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return protocol;
        }
        return null;
    }

    private static string GetServerName(string podName)
    {
        // Extract meaningful name: "file-sim-file-simulator-nas-input-1-xxx" -> "nas-input-1"
        var parts = podName.Split('-');

        // Look for NAS patterns
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "nas" && i + 2 < parts.Length)
            {
                // nas-input-1, nas-output-2, nas-backup
                var nasType = parts[i + 1];
                if (nasType == "backup")
                    return "nas-backup";
                if (i + 2 < parts.Length && int.TryParse(parts[i + 2], out _))
                    return $"nas-{nasType}-{parts[i + 2]}";
            }
        }

        // For other protocols, check for exact segment match (not substring)
        // This prevents "sftp" from matching "ftp" incorrectly
        foreach (var (key, _) in ProtocolMappings)
        {
            // Check if any segment exactly matches the protocol key
            if (parts.Any(p => p.Equals(key, StringComparison.OrdinalIgnoreCase)))
                return key;
        }

        return podName;
    }

    private static V1Service? FindMatchingService(V1Pod pod, IList<V1Service> services)
    {
        // Match by selector labels
        var podLabels = pod.Metadata.Labels ?? new Dictionary<string, string>();

        foreach (var service in services)
        {
            var selector = service.Spec.Selector;
            if (selector == null) continue;

            bool matches = selector.All(kv =>
                podLabels.TryGetValue(kv.Key, out var value) && value == kv.Value);

            if (matches)
                return service;
        }

        return null;
    }

    private static bool IsPodReady(V1Pod pod)
    {
        var readyCondition = pod.Status.Conditions?
            .FirstOrDefault(c => c.Type == "Ready");
        return readyCondition?.Status == "True";
    }

    // Windows base path for simulator data (mounted in minikube)
    private const string WindowsBasePath = @"C:\simulator-data";

    /// <summary>
    /// Extract the Windows folder path this server serves.
    /// Shows the actual Windows path where files are stored.
    /// </summary>
    private static string? GetServerDirectory(string serverName, string protocol, V1Pod pod)
    {
        // For NAS servers, extract from server name pattern or volume mounts
        if (protocol == "NFS")
        {
            // Check init containers for subPath (dynamic servers use init container)
            var subPath = pod.Spec.InitContainers?
                .SelectMany(c => c.VolumeMounts ?? Enumerable.Empty<V1VolumeMount>())
                .Where(vm => !string.IsNullOrEmpty(vm.SubPath) && vm.Name == "windows-data")
                .Select(vm => vm.SubPath)
                .FirstOrDefault();

            // Also check main container volume mounts (Helm servers)
            if (string.IsNullOrEmpty(subPath))
            {
                subPath = pod.Spec.Containers
                    .SelectMany(c => c.VolumeMounts ?? Enumerable.Empty<V1VolumeMount>())
                    .Where(vm => !string.IsNullOrEmpty(vm.SubPath))
                    .Select(vm => vm.SubPath)
                    .FirstOrDefault();
            }

            if (!string.IsNullOrEmpty(subPath))
                return $@"{WindowsBasePath}\{subPath.Replace("/", "\\")}";

            // Infer from server name (Helm-managed servers)
            if (serverName.Contains("input"))
                return $@"{WindowsBasePath}\input";
            if (serverName.Contains("output"))
                return $@"{WindowsBasePath}\output";
            if (serverName.Contains("backup"))
                return $@"{WindowsBasePath}\backup";

            return WindowsBasePath;
        }

        // For FTP/SFTP, check for subPath in volume mounts (dynamic servers may have subdirectory)
        if (protocol == "FTP" || protocol == "SFTP")
        {
            var subPath = pod.Spec.Containers
                .SelectMany(c => c.VolumeMounts ?? Enumerable.Empty<V1VolumeMount>())
                .Where(vm => vm.Name == "data" && !string.IsNullOrEmpty(vm.SubPath))
                .Select(vm => vm.SubPath)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(subPath))
                return $@"{WindowsBasePath}\{subPath.Replace("/", "\\")}";

            return WindowsBasePath;
        }

        // For HTTP/WebDAV, files are stored in root of shared volume
        if (protocol == "HTTP")
        {
            return WindowsBasePath;
        }

        // For S3/MinIO, uses internal storage (not shared PVC)
        if (protocol == "S3")
        {
            return "(internal S3 storage)";
        }

        // For SMB, files are stored in root of shared volume
        if (protocol == "SMB")
        {
            return WindowsBasePath;
        }

        // For Management UI (FileBrowser), files are stored in root of shared volume
        if (protocol == "Management")
        {
            return WindowsBasePath;
        }

        return null;
    }

    /// <summary>
    /// Find the deployment that owns a pod by matching labels.
    /// </summary>
    private static V1Deployment? FindMatchingDeployment(V1Pod pod, IList<V1Deployment> deployments)
    {
        var podLabels = pod.Metadata.Labels ?? new Dictionary<string, string>();

        foreach (var deployment in deployments)
        {
            var selector = deployment.Spec.Selector?.MatchLabels;
            if (selector == null) continue;

            bool matches = selector.All(kv =>
                podLabels.TryGetValue(kv.Key, out var value) && value == kv.Value);

            if (matches)
                return deployment;
        }

        return null;
    }

    /// <summary>
    /// Extract credentials from deployment based on protocol.
    /// Reads environment variables and container args to get actual credentials.
    /// </summary>
    private static ServerCredentials? ExtractCredentials(string protocol, V1Deployment? deployment)
    {
        if (deployment == null)
            return null;

        var containers = deployment.Spec.Template.Spec.Containers;
        if (containers == null || containers.Count == 0)
            return null;

        var container = containers[0];
        var envVars = container.Env ?? new List<V1EnvVar>();
        var args = container.Args ?? new List<string>();

        return protocol.ToUpperInvariant() switch
        {
            "FTP" => ExtractFtpCredentials(envVars),
            "SFTP" => ExtractSftpCredentials(args),
            "HTTP" => new ServerCredentials { Note = "HTTP server (read-only, no authentication)" },
            "WEBDAV" => ExtractWebDavCredentials(envVars),
            "S3" => ExtractS3Credentials(envVars),
            "SMB" => ExtractSmbCredentials(args),
            "MANAGEMENT" => new ServerCredentials
            {
                Username = "admin",
                Password = "admin123",
                Note = "FileBrowser UI credentials (configured via database)"
            },
            "NFS" => new ServerCredentials
            {
                Note = "NFS uses anonymous access (no authentication required)"
            },
            _ => null
        };
    }

    /// <summary>
    /// Extract FTP credentials from FTP_USER and FTP_PASS environment variables.
    /// </summary>
    private static ServerCredentials? ExtractFtpCredentials(IList<V1EnvVar> envVars)
    {
        var username = envVars.FirstOrDefault(e => e.Name == "FTP_USER")?.Value;
        var password = envVars.FirstOrDefault(e => e.Name == "FTP_PASS")?.Value;

        if (string.IsNullOrEmpty(username))
            return null;

        return new ServerCredentials
        {
            Username = username,
            Password = password ?? "",
            Note = "FTP credentials from deployment environment"
        };
    }

    /// <summary>
    /// Extract SFTP credentials from container args (format: username:password:uid:gid).
    /// </summary>
    private static ServerCredentials? ExtractSftpCredentials(IList<string> args)
    {
        // atmoz/sftp uses args like "sftpuser:sftppass123:1000:1000"
        foreach (var arg in args)
        {
            var parts = arg.Split(':');
            if (parts.Length >= 2)
            {
                return new ServerCredentials
                {
                    Username = parts[0],
                    Password = parts[1],
                    Note = "SFTP credentials from container args"
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Extract WebDAV credentials from USERNAME and PASSWORD environment variables.
    /// </summary>
    private static ServerCredentials? ExtractWebDavCredentials(IList<V1EnvVar> envVars)
    {
        var username = envVars.FirstOrDefault(e => e.Name == "USERNAME")?.Value;
        var password = envVars.FirstOrDefault(e => e.Name == "PASSWORD")?.Value;

        if (!string.IsNullOrEmpty(username))
        {
            return new ServerCredentials
            {
                Username = username,
                Password = password ?? "",
                Note = "WebDAV credentials from deployment environment"
            };
        }

        return new ServerCredentials { Note = "WebDAV (no credentials found)" };
    }

    /// <summary>
    /// Extract S3/MinIO credentials from MINIO_ROOT_USER and MINIO_ROOT_PASSWORD.
    /// </summary>
    private static ServerCredentials? ExtractS3Credentials(IList<V1EnvVar> envVars)
    {
        var username = envVars.FirstOrDefault(e => e.Name == "MINIO_ROOT_USER")?.Value;
        var password = envVars.FirstOrDefault(e => e.Name == "MINIO_ROOT_PASSWORD")?.Value;

        if (string.IsNullOrEmpty(username))
            return null;

        return new ServerCredentials
        {
            Username = username,
            Password = password ?? "",
            Note = "MinIO root credentials from deployment environment"
        };
    }

    /// <summary>
    /// Extract SMB credentials from container args (format: -u username;password).
    /// </summary>
    private static ServerCredentials? ExtractSmbCredentials(IList<string> args)
    {
        // dperson/samba uses -u "username;password" args
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == "-u")
            {
                var userArg = args[i + 1];
                var parts = userArg.Split(';');
                if (parts.Length >= 2)
                {
                    return new ServerCredentials
                    {
                        Username = parts[0],
                        Password = parts[1],
                        Note = "SMB credentials from container args"
                    };
                }
            }
        }

        return null;
    }
}
