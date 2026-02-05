using System.Net.Http.Json;
using System.Text.Json;
using FileSimulator.TestConsole.Models;
using Microsoft.Extensions.Logging;

namespace FileSimulator.TestConsole;

/// <summary>
/// Fetches configuration from the Control API's /api/connection-info endpoint.
/// </summary>
public class ApiConfigurationProvider
{
    private readonly string _apiBaseUrl;
    private readonly ILogger? _logger;
    private readonly HttpClient _httpClient;

    public ApiConfigurationProvider(string apiBaseUrl, ILogger? logger = null)
    {
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Get configuration from the Control API.
    /// Returns null if API is unavailable or returns invalid data.
    /// </summary>
    public async Task<TestConfiguration?> GetConfigurationAsync(CancellationToken ct = default)
    {
        try
        {
            // Check API health first
            var healthUrl = $"{_apiBaseUrl}/api/health";
            var healthResponse = await _httpClient.GetAsync(healthUrl, ct);

            if (!healthResponse.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Control API health check failed: {StatusCode}", healthResponse.StatusCode);
                return null;
            }

            // Fetch connection info
            var connectionInfo = await FetchFromApiAsync(ct);
            if (connectionInfo == null)
            {
                _logger?.LogWarning("Failed to fetch connection info from API");
                return null;
            }

            // Map to TestConfiguration
            var testConfig = MapToTestConfiguration(connectionInfo);
            _logger?.LogInformation("Successfully loaded configuration from Control API");

            return testConfig;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch configuration from Control API at {Url}", _apiBaseUrl);
            return null;
        }
    }

    /// <summary>
    /// Fetch connection info from /api/connection-info endpoint.
    /// </summary>
    private async Task<ConnectionInfoResponse?> FetchFromApiAsync(CancellationToken ct)
    {
        var url = $"{_apiBaseUrl}/api/connection-info";

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = await _httpClient.GetFromJsonAsync<ConnectionInfoResponse>(url, options, ct);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogDebug(ex, "HTTP request failed for {Url}", url);
            return null;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to deserialize response from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Map ConnectionInfoResponse to TestConfiguration.
    /// </summary>
    private TestConfiguration MapToTestConfiguration(ConnectionInfoResponse apiResponse)
    {
        var config = new TestConfiguration
        {
            Source = ConfigurationSource.Api,
            Servers = new Dictionary<string, ServerConfig>()
        };

        // Group servers by protocol (taking first of each type for simple case)
        // For NFS (NAS servers), store all instances with unique keys
        foreach (var server in apiResponse.Servers)
        {
            var protocol = server.Protocol.ToUpperInvariant();

            // For NFS servers, use server name as key (to store multiple NAS servers)
            var key = protocol == "NFS" ? server.Name : protocol;

            // Skip if we already have this protocol (unless it's dynamic or NFS)
            if (config.Servers.ContainsKey(key) && !server.IsDynamic && protocol != "NFS")
                continue;

            var serverConfig = new ServerConfig
            {
                Name = server.Name,
                Host = server.Host,
                Port = server.Port,
                Protocol = server.Protocol,
                Type = server.Protocol.ToLowerInvariant(),
                Status = server.Status,
                Directory = server.Directory
            };

            // Add protocol-specific configuration
            switch (protocol)
            {
                case "FTP":
                    serverConfig.Username = apiResponse.DefaultCredentials.Ftp.Username;
                    serverConfig.Password = apiResponse.DefaultCredentials.Ftp.Password;
                    serverConfig.BasePath = "/output";
                    break;

                case "SFTP":
                    serverConfig.Username = apiResponse.DefaultCredentials.Sftp.Username;
                    serverConfig.Password = apiResponse.DefaultCredentials.Sftp.Password;
                    serverConfig.BasePath = "/data/output";
                    break;

                case "HTTP":
                    serverConfig.Username = apiResponse.DefaultCredentials.Http.Username;
                    serverConfig.Password = apiResponse.DefaultCredentials.Http.Password;
                    serverConfig.BaseUrl = $"http://{server.Host}:{server.Port}";
                    serverConfig.BasePath = "/output";
                    break;

                case "S3":
                    serverConfig.Username = apiResponse.DefaultCredentials.S3.Username;
                    serverConfig.Password = apiResponse.DefaultCredentials.S3.Password;
                    serverConfig.ServiceUrl = $"http://{server.Host}:{server.Port}";
                    serverConfig.BucketName = "output";
                    serverConfig.BasePath = "";
                    break;

                case "SMB":
                    serverConfig.Username = apiResponse.DefaultCredentials.Smb.Username;
                    serverConfig.Password = apiResponse.DefaultCredentials.Smb.Password;
                    serverConfig.ShareName = "simulator";
                    serverConfig.BasePath = "output";
                    break;

                case "NFS":
                    serverConfig.MountPath = "/mnt/nfs";
                    serverConfig.BasePath = "output";
                    break;

                case "MANAGEMENT":
                    serverConfig.Username = apiResponse.DefaultCredentials.Management.Username;
                    serverConfig.Password = apiResponse.DefaultCredentials.Management.Password;
                    serverConfig.BaseUrl = $"http://{server.Host}:{server.Port}";
                    break;
            }

            // Use protocol as key for simple access (or server name for NFS)
            config.Servers[key] = serverConfig;
        }

        return config;
    }

    /// <summary>
    /// Get FTP server configuration.
    /// </summary>
    public ServerConfig? GetFtpConfig(TestConfiguration config)
    {
        return config.Servers.TryGetValue("FTP", out var server) ? server : null;
    }

    /// <summary>
    /// Get SFTP server configuration.
    /// </summary>
    public ServerConfig? GetSftpConfig(TestConfiguration config)
    {
        return config.Servers.TryGetValue("SFTP", out var server) ? server : null;
    }

    /// <summary>
    /// Get all NAS server configurations (NFS, SMB).
    /// </summary>
    public List<ServerConfig> GetNasServers(TestConfiguration config)
    {
        return config.Servers.Values
            .Where(s => s.Protocol.Equals("NFS", StringComparison.OrdinalIgnoreCase) ||
                       s.Protocol.Equals("SMB", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get all dynamic server configurations.
    /// </summary>
    public List<ServerConfig> GetDynamicServers(TestConfiguration config)
    {
        return config.Servers.Values
            .Where(s => s.Name.Contains("dynamic", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get servers by protocol.
    /// </summary>
    public List<ServerConfig> GetServersByProtocol(TestConfiguration config, string protocol)
    {
        return config.Servers.Values
            .Where(s => s.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Get servers by status.
    /// </summary>
    public List<ServerConfig> GetServersByStatus(TestConfiguration config, string status)
    {
        return config.Servers.Values
            .Where(s => s.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
