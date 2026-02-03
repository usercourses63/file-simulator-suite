namespace FileSimulator.ControlApi.Services;

using k8s;
using k8s.Models;

/// <summary>
/// Updates ConfigMap with current server endpoints for service discovery.
/// Applications read this ConfigMap to discover available protocol servers.
/// </summary>
public interface IConfigMapUpdateService
{
    /// <summary>
    /// Refresh the service discovery ConfigMap with current server list.
    /// Called after create/delete/stop/start/restart operations.
    /// </summary>
    Task UpdateConfigMapAsync(CancellationToken ct = default);
}

public class ConfigMapUpdateService : IConfigMapUpdateService
{
    private readonly IKubernetes _client;
    private readonly IKubernetesDiscoveryService _discoveryService;
    private readonly ILogger<ConfigMapUpdateService> _logger;
    private readonly string _namespace;
    private readonly string _configMapName;

    public ConfigMapUpdateService(
        IKubernetes client,
        IKubernetesDiscoveryService discoveryService,
        ILogger<ConfigMapUpdateService> logger,
        IConfiguration configuration)
    {
        _client = client;
        _discoveryService = discoveryService;
        _logger = logger;
        _namespace = configuration["Kubernetes:Namespace"] ?? "file-simulator";
        var releasePrefix = configuration["Kubernetes:ReleasePrefix"] ?? "file-sim-file-simulator";
        _configMapName = configuration["Kubernetes:EndpointsConfigMap"] ?? $"{releasePrefix}-endpoints";
    }

    public async Task UpdateConfigMapAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Updating service discovery ConfigMap {Name}", _configMapName);

        // 1. Discover all current servers (only running servers with ready pods)
        var servers = await _discoveryService.DiscoverServersAsync(ct);

        // 2. Build ConfigMap data
        var configData = new Dictionary<string, string>();

        foreach (var server in servers)
        {
            // Only include servers that are ready (running and healthy)
            if (!server.PodReady)
            {
                _logger.LogDebug("Skipping server {Name} - not ready (Status: {Status})",
                    server.Name, server.PodStatus);
                continue;
            }

            // Format: PROTOCOL_INSTANCE=service.namespace.svc.cluster.local:port
            var key = $"{server.Protocol}_{server.Name}".Replace("-", "_").ToUpper();
            var clusterAddress = $"{server.ServiceName}.{_namespace}.svc.cluster.local:{server.Port}";
            configData[key] = clusterAddress;

            // Also add NodePort for external access
            if (server.NodePort.HasValue)
            {
                var nodePortKey = $"{key}_NODEPORT";
                configData[nodePortKey] = server.NodePort.Value.ToString();
            }
        }

        // Add metadata
        configData["UPDATED_AT"] = DateTime.UtcNow.ToString("O");
        configData["SERVER_COUNT"] = servers.Count(s => s.PodReady).ToString();

        // 3. Update or create ConfigMap
        try
        {
            var existingConfigMap = await _client.CoreV1.ReadNamespacedConfigMapAsync(
                _configMapName,
                _namespace,
                cancellationToken: ct);

            existingConfigMap.Data = configData;

            await _client.CoreV1.ReplaceNamespacedConfigMapAsync(
                existingConfigMap,
                _configMapName,
                _namespace,
                cancellationToken: ct);

            _logger.LogInformation("Updated ConfigMap {Name} with {Count} ready servers",
                _configMapName, servers.Count(s => s.PodReady));
        }
        catch (k8s.Autorest.HttpOperationException ex) when (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Create new ConfigMap if it doesn't exist
            var configMap = new V1ConfigMap
            {
                Metadata = new V1ObjectMeta
                {
                    Name = _configMapName,
                    NamespaceProperty = _namespace,
                    Labels = new Dictionary<string, string>
                    {
                        ["app.kubernetes.io/name"] = "file-simulator",
                        ["app.kubernetes.io/component"] = "service-discovery",
                        ["app.kubernetes.io/managed-by"] = "control-api"
                    }
                },
                Data = configData
            };

            await _client.CoreV1.CreateNamespacedConfigMapAsync(
                configMap,
                _namespace,
                cancellationToken: ct);

            _logger.LogInformation("Created ConfigMap {Name} with {Count} ready servers",
                _configMapName, servers.Count(s => s.PodReady));
        }
    }
}
