namespace FileSimulator.ControlApi.Services;

using k8s;
using k8s.Models;
using FileSimulator.ControlApi.Models;

/// <summary>
/// Service for exporting and importing configuration for environment replication.
/// Exports all servers (static and dynamic) and imports dynamic servers with conflict resolution.
/// </summary>
public interface IConfigurationExportService
{
    /// <summary>Export all server configurations.</summary>
    Task<ServerConfigurationExport> ExportConfigurationAsync(CancellationToken ct = default);

    /// <summary>Import configurations with conflict resolution.</summary>
    Task<ImportResult> ImportConfigurationAsync(
        ServerConfigurationExport config,
        ConflictResolutionStrategy strategy,
        CancellationToken ct = default);

    /// <summary>Validate import configuration without applying.</summary>
    Task<ImportResult> ValidateImportAsync(
        ServerConfigurationExport config,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of configuration export/import service.
/// </summary>
public class ConfigurationExportService : IConfigurationExportService
{
    private readonly IKubernetes _client;
    private readonly IKubernetesDiscoveryService _discoveryService;
    private readonly IKubernetesManagementService _managementService;
    private readonly ILogger<ConfigurationExportService> _logger;
    private readonly string _namespace;
    private readonly string _releasePrefix;

    public ConfigurationExportService(
        IKubernetes client,
        IKubernetesDiscoveryService discoveryService,
        IKubernetesManagementService managementService,
        ILogger<ConfigurationExportService> logger,
        IConfiguration configuration)
    {
        _client = client;
        _discoveryService = discoveryService;
        _managementService = managementService;
        _logger = logger;
        _namespace = configuration["Kubernetes:Namespace"] ?? "file-simulator";
        _releasePrefix = configuration["Kubernetes:ReleasePrefix"] ?? "file-sim-file-simulator";
    }

    /// <inheritdoc />
    public async Task<ServerConfigurationExport> ExportConfigurationAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Exporting configuration");

        var servers = await _discoveryService.DiscoverServersAsync(ct);
        var configurations = new List<ServerConfiguration>();

        foreach (var server in servers)
        {
            var config = new ServerConfiguration
            {
                Name = server.Name,
                Protocol = server.Protocol,
                NodePort = server.NodePort,
                IsDynamic = server.IsDynamic
            };

            // Try to extract credentials from deployment env vars (for dynamic servers)
            if (server.IsDynamic)
            {
                config = await EnrichWithCredentialsAsync(server, config, ct);
            }
            else
            {
                // Static servers: include placeholder credentials
                config = server.Protocol.ToUpper() switch
                {
                    "FTP" => config with { Ftp = new FtpConfiguration { Username = "simuser", Password = "[helm-managed]" } },
                    "SFTP" => config with { Sftp = new SftpConfiguration { Username = "simuser", Password = "[helm-managed]" } },
                    "NFS" => config with { Nas = new NasConfiguration { Directory = "[helm-managed]" } },
                    _ => config
                };
            }

            configurations.Add(config);
        }

        var export = new ServerConfigurationExport
        {
            Namespace = _namespace,
            ReleasePrefix = _releasePrefix,
            Servers = configurations,
            Metadata = new ExportMetadata
            {
                Environment = "development",
                ExportedBy = "control-api"
            }
        };

        _logger.LogInformation("Exported {Count} server configurations", configurations.Count);
        return export;
    }

    private async Task<ServerConfiguration> EnrichWithCredentialsAsync(
        DiscoveredServer server,
        ServerConfiguration config,
        CancellationToken ct)
    {
        try
        {
            // Map protocol to deployment prefix (NFS protocol uses "nas" prefix in deployments)
            var protocolPrefix = server.Protocol.ToUpper() switch
            {
                "NFS" => "nas",
                _ => server.Protocol.ToLower()
            };
            var deploymentName = $"{_releasePrefix}-{protocolPrefix}-{server.Name}";
            var deployment = await _client.AppsV1.ReadNamespacedDeploymentAsync(
                deploymentName, _namespace, cancellationToken: ct);

            var container = deployment.Spec.Template.Spec.Containers.FirstOrDefault();
            if (container?.Env == null) return config;

            var envVars = container.Env.ToDictionary(e => e.Name, e => e.Value ?? "");

            return server.Protocol.ToUpper() switch
            {
                "FTP" => config with
                {
                    Ftp = new FtpConfiguration
                    {
                        Username = envVars.GetValueOrDefault("FTP_USER", ""),
                        Password = envVars.GetValueOrDefault("FTP_PASS", ""),
                        PassivePortStart = int.TryParse(envVars.GetValueOrDefault("PASV_MIN_PORT", ""), out var ps) ? ps : null,
                        PassivePortEnd = int.TryParse(envVars.GetValueOrDefault("PASV_MAX_PORT", ""), out var pe) ? pe : null
                    }
                },
                "SFTP" => config with
                {
                    Sftp = new SftpConfiguration
                    {
                        Username = container.Args?.FirstOrDefault()?.Split(':').FirstOrDefault() ?? "",
                        Password = container.Args?.FirstOrDefault()?.Split(':').Skip(1).FirstOrDefault() ?? "",
                        Uid = int.TryParse(container.Args?.FirstOrDefault()?.Split(':').Skip(2).FirstOrDefault(), out var uid) ? uid : 1000,
                        Gid = int.TryParse(container.Args?.FirstOrDefault()?.Split(':').Skip(3).FirstOrDefault(), out var gid) ? gid : 1000
                    }
                },
                "NFS" => config with
                {
                    Nas = new NasConfiguration
                    {
                        // Directory is stored in the init container's volume mount SubPath
                        Directory = GetNasDirectory(deployment),
                        ExportOptions = ParseExportOptions(envVars.GetValueOrDefault("NFS_EXPORT_0", ""))
                    }
                },
                _ => config
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not enrich credentials for {Server}", server.Name);
            return config;
        }
    }

    private static string ParseExportOptions(string exportLine)
    {
        // NFS_EXPORT_0="/data *(rw,sync,...)" -> extract options
        var match = System.Text.RegularExpressions.Regex.Match(exportLine, @"\*\(([^)]+)\)");
        return match.Success ? match.Groups[1].Value.Replace(",fsid=0", "") : "rw,sync,no_subtree_check,no_root_squash";
    }

    /// <summary>
    /// Gets the directory for a NAS server from the init container's volume mount.
    /// Dynamic NAS servers store the directory in the "sync-windows-data" init container's SubPath.
    /// </summary>
    private static string GetNasDirectory(V1Deployment deployment)
    {
        // First, check init containers for the sync-windows-data container
        var initContainer = deployment.Spec?.Template?.Spec?.InitContainers?
            .FirstOrDefault(c => c.Name == "sync-windows-data");

        if (initContainer != null)
        {
            // Get SubPath from the windows-data volume mount
            var windowsDataMount = initContainer.VolumeMounts?
                .FirstOrDefault(vm => vm.Name == "windows-data");

            if (!string.IsNullOrEmpty(windowsDataMount?.SubPath))
            {
                return windowsDataMount.SubPath;
            }
        }

        // Fallback: try main container volume mounts
        var mainContainer = deployment.Spec?.Template?.Spec?.Containers?.FirstOrDefault();
        var mountPath = mainContainer?.VolumeMounts?.FirstOrDefault()?.SubPath;

        return mountPath ?? "";
    }

    /// <inheritdoc />
    public async Task<ImportResult> ValidateImportAsync(
        ServerConfigurationExport config,
        CancellationToken ct = default)
    {
        var result = new ImportResult();
        var existingServers = await _discoveryService.DiscoverServersAsync(ct);
        var existingNames = existingServers.Select(s => s.Name).ToHashSet();

        foreach (var serverConfig in config.Servers)
        {
            if (!serverConfig.IsDynamic)
            {
                result.Skipped.Add($"{serverConfig.Name} (static/helm-managed)");
                continue;
            }

            if (existingNames.Contains(serverConfig.Name))
            {
                result.Skipped.Add($"{serverConfig.Name} (conflict)");
            }
            else
            {
                result.Created.Add(serverConfig.Name);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportConfigurationAsync(
        ServerConfigurationExport config,
        ConflictResolutionStrategy strategy,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Importing configuration with strategy {Strategy}", strategy);

        var result = new ImportResult();
        var existingServers = await _discoveryService.DiscoverServersAsync(ct);
        var existingNames = existingServers.Select(s => s.Name).ToHashSet();

        foreach (var serverConfig in config.Servers)
        {
            // Skip static servers - they're managed by Helm
            if (!serverConfig.IsDynamic)
            {
                result.Skipped.Add($"{serverConfig.Name} (static/helm-managed)");
                continue;
            }

            var targetName = serverConfig.Name;
            var hasConflict = existingNames.Contains(targetName);

            if (hasConflict)
            {
                switch (strategy)
                {
                    case ConflictResolutionStrategy.Skip:
                        result.Skipped.Add($"{targetName} (conflict)");
                        continue;

                    case ConflictResolutionStrategy.Replace:
                        try
                        {
                            await _managementService.DeleteServerAsync(targetName, false, ct);
                            _logger.LogInformation("Deleted existing server {Name} for replacement", targetName);
                        }
                        catch (Exception ex)
                        {
                            result.Failed[$"{targetName}"] = $"Failed to delete for replacement: {ex.Message}";
                            continue;
                        }
                        break;

                    case ConflictResolutionStrategy.Rename:
                        var suffix = 1;
                        while (existingNames.Contains($"{serverConfig.Name}-{suffix}"))
                            suffix++;
                        targetName = $"{serverConfig.Name}-{suffix}";
                        _logger.LogInformation("Renamed {Original} to {New} to avoid conflict",
                            serverConfig.Name, targetName);
                        break;
                }
            }

            try
            {
                await CreateServerFromConfigAsync(serverConfig, targetName, ct);
                result.Created.Add(targetName);
                existingNames.Add(targetName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import server {Name}", targetName);
                result.Failed[targetName] = ex.Message;
            }
        }

        _logger.LogInformation("Import complete: {Created} created, {Skipped} skipped, {Failed} failed",
            result.Created.Count, result.Skipped.Count, result.Failed.Count);

        return result;
    }

    private async Task CreateServerFromConfigAsync(
        ServerConfiguration config,
        string targetName,
        CancellationToken ct)
    {
        switch (config.Protocol.ToUpper())
        {
            case "FTP" when config.Ftp != null:
                await _managementService.CreateFtpServerAsync(new CreateFtpServerRequest
                {
                    Name = targetName,
                    NodePort = config.NodePort,
                    Username = config.Ftp.Username,
                    Password = config.Ftp.Password,
                    PassivePortStart = config.Ftp.PassivePortStart,
                    PassivePortEnd = config.Ftp.PassivePortEnd
                }, ct);
                break;

            case "SFTP" when config.Sftp != null:
                await _managementService.CreateSftpServerAsync(new CreateSftpServerRequest
                {
                    Name = targetName,
                    NodePort = config.NodePort,
                    Username = config.Sftp.Username,
                    Password = config.Sftp.Password,
                    Uid = config.Sftp.Uid,
                    Gid = config.Sftp.Gid
                }, ct);
                break;

            case "NFS" when config.Nas != null:
                await _managementService.CreateNasServerAsync(new CreateNasServerRequest
                {
                    Name = targetName,
                    NodePort = config.NodePort,
                    Directory = config.Nas.Directory,
                    ExportOptions = config.Nas.ExportOptions
                }, ct);
                break;

            default:
                throw new InvalidOperationException(
                    $"Cannot import {config.Protocol} server: missing configuration or unsupported protocol");
        }
    }
}
