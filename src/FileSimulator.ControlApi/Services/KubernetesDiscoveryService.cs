namespace FileSimulator.ControlApi.Services;

using k8s;
using k8s.Models;
using FileSimulator.ControlApi.Models;
using Microsoft.Extensions.Options;

public class KubernetesOptions
{
    public bool InCluster { get; set; } = true;
    public string Namespace { get; set; } = "file-simulator";
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
    private static readonly Dictionary<string, string> ProtocolMappings = new()
    {
        { "ftp", "FTP" },
        { "sftp", "SFTP" },
        { "nas", "NFS" },
        { "http", "HTTP" },
        { "s3", "S3" },
        { "smb", "SMB" },
        { "management", "HTTP" }  // FileBrowser
    };

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

            _logger.LogDebug("Found {PodCount} pods and {ServiceCount} services",
                pods.Items.Count, services.Items.Count);

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
                    ManagedBy = managedBy
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
        foreach (var (key, value) in ProtocolMappings)
        {
            if (podName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return value;
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
}
