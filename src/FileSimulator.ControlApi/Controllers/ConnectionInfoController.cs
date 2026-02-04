namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using FileSimulator.ControlApi.Services;

/// <summary>
/// Provides connection information for all file simulator servers.
/// Use this endpoint to configure your applications to connect to the simulators.
/// </summary>
[ApiController]
[Route("api/connection-info")]
public class ConnectionInfoController : ControllerBase
{
    private readonly IKubernetesDiscoveryService _discoveryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectionInfoController> _logger;

    public ConnectionInfoController(
        IKubernetesDiscoveryService discoveryService,
        IConfiguration configuration,
        ILogger<ConnectionInfoController> logger)
    {
        _discoveryService = discoveryService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get connection information for all servers.
    /// Returns hostnames, ports, and connection strings for each protocol.
    /// </summary>
    /// <param name="hostname">Override hostname (default: from request or file-simulator.local)</param>
    /// <param name="format">Output format: json (default), env, yaml, or shell</param>
    [HttpGet]
    [Produces("application/json", "text/plain", "text/yaml")]
    public async Task<IActionResult> GetConnectionInfo(
        [FromQuery] string? hostname = null,
        [FromQuery] string format = "json",
        CancellationToken ct = default)
    {
        // Determine hostname to use
        var host = hostname
            ?? _configuration["ConnectionInfo:Hostname"]
            ?? Request.Host.Host;

        // If connecting via localhost, suggest using the proper hostname
        if (host == "localhost" || host == "127.0.0.1")
        {
            host = _configuration["ConnectionInfo:Hostname"] ?? "file-simulator.local";
        }

        var servers = await _discoveryService.DiscoverServersAsync(ct);

        var connectionInfo = new ConnectionInfoResponse
        {
            Hostname = host,
            GeneratedAt = DateTime.UtcNow,
            Servers = servers.Select(s => new ServerConnectionInfo
            {
                Name = s.Name,
                Protocol = s.Protocol,
                Host = host,
                Port = s.NodePort ?? s.Port,
                ClusterIp = s.ClusterIp,
                ClusterPort = s.Port,
                ServiceName = s.ServiceName,
                IsDynamic = s.IsDynamic,
                Status = s.PodReady ? "healthy" : "down",
                Directory = s.Directory,
                ConnectionString = BuildConnectionString(s, host)
            }).ToList(),
            DefaultCredentials = new DefaultCredentials
            {
                Ftp = new CredentialInfo { Username = "simuser", Password = "simpass123", Note = "Default FTP credentials" },
                Sftp = new CredentialInfo { Username = "simuser", Password = "simpass123", Note = "Default SFTP credentials" },
                S3 = new CredentialInfo { Username = "minioadmin", Password = "minioadmin", Note = "MinIO root credentials" },
                Http = new CredentialInfo { Username = "admin", Password = "admin", Note = "WebDAV credentials" },
                Smb = new CredentialInfo { Username = "simuser", Password = "simpass", Note = "SMB share credentials" }
            },
            Endpoints = new EndpointSummary
            {
                Dashboard = $"http://{host}:30080",
                ControlApi = $"http://{host}:30500",
                Ftp = $"ftp://{host}:30021",
                Sftp = $"sftp://{host}:30022",
                Http = $"http://{host}:30088",
                S3 = $"http://{host}:30900",
                Smb = $@"\\{host}\shared",
                Nfs = $"{host}:32049:/data"
            }
        };

        return format.ToLower() switch
        {
            "env" => Content(FormatAsEnv(connectionInfo), "text/plain"),
            "shell" => Content(FormatAsShell(connectionInfo), "text/plain"),
            "yaml" => Content(FormatAsYaml(connectionInfo), "text/yaml"),
            "dotnet" => Content(FormatAsDotNet(connectionInfo), "text/plain"),
            _ => Ok(connectionInfo)
        };
    }

    /// <summary>
    /// Get connection info for a specific protocol.
    /// </summary>
    [HttpGet("{protocol}")]
    public async Task<ActionResult<ServerConnectionInfo>> GetProtocolInfo(
        string protocol,
        [FromQuery] string? hostname = null,
        CancellationToken ct = default)
    {
        var host = hostname
            ?? _configuration["ConnectionInfo:Hostname"]
            ?? Request.Host.Host;

        if (host == "localhost" || host == "127.0.0.1")
            host = _configuration["ConnectionInfo:Hostname"] ?? "file-simulator.local";

        var servers = await _discoveryService.DiscoverServersAsync(ct);
        var server = servers.FirstOrDefault(s =>
            s.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase) ||
            s.Name.Equals(protocol, StringComparison.OrdinalIgnoreCase));

        if (server == null)
            return NotFound(new { error = $"No server found for protocol '{protocol}'" });

        return Ok(new ServerConnectionInfo
        {
            Name = server.Name,
            Protocol = server.Protocol,
            Host = host,
            Port = server.NodePort ?? server.Port,
            ClusterIp = server.ClusterIp,
            ClusterPort = server.Port,
            ServiceName = server.ServiceName,
            IsDynamic = server.IsDynamic,
            Status = server.PodReady ? "healthy" : "down",
            Directory = server.Directory,
            ConnectionString = BuildConnectionString(server, host)
        });
    }

    private static string BuildConnectionString(Models.DiscoveredServer server, string host)
    {
        var port = server.NodePort ?? server.Port;
        return server.Protocol.ToUpper() switch
        {
            "FTP" => $"ftp://simuser:simpass123@{host}:{port}",
            "SFTP" => $"sftp://simuser:simpass123@{host}:{port}",
            "HTTP" => $"http://{host}:{port}",
            "S3" => $"http://minioadmin:minioadmin@{host}:{port}",
            "SMB" => $@"\\{host}\shared",
            "NFS" => $"{host}:{port}:/data",
            _ => $"{host}:{port}"
        };
    }

    private static string FormatAsEnv(ConnectionInfoResponse info)
    {
        var lines = new List<string>
        {
            "# File Simulator Connection Settings",
            $"# Generated: {info.GeneratedAt:O}",
            "",
            $"FILE_SIMULATOR_HOST={info.Hostname}",
            "",
            "# Endpoints",
            $"FILE_SIMULATOR_DASHBOARD_URL={info.Endpoints.Dashboard}",
            $"FILE_SIMULATOR_API_URL={info.Endpoints.ControlApi}",
            "",
            "# FTP",
            $"FILE_FTP_HOST={info.Hostname}",
            "FILE_FTP_PORT=30021",
            $"FILE_FTP_USERNAME={info.DefaultCredentials.Ftp.Username}",
            $"FILE_FTP_PASSWORD={info.DefaultCredentials.Ftp.Password}",
            "",
            "# SFTP",
            $"FILE_SFTP_HOST={info.Hostname}",
            "FILE_SFTP_PORT=30022",
            $"FILE_SFTP_USERNAME={info.DefaultCredentials.Sftp.Username}",
            $"FILE_SFTP_PASSWORD={info.DefaultCredentials.Sftp.Password}",
            "",
            "# HTTP/WebDAV",
            $"FILE_HTTP_URL=http://{info.Hostname}:30088",
            $"FILE_HTTP_USERNAME={info.DefaultCredentials.Http.Username}",
            $"FILE_HTTP_PASSWORD={info.DefaultCredentials.Http.Password}",
            "",
            "# S3/MinIO",
            $"FILE_S3_ENDPOINT=http://{info.Hostname}:30900",
            $"FILE_S3_ACCESS_KEY={info.DefaultCredentials.S3.Username}",
            $"FILE_S3_SECRET_KEY={info.DefaultCredentials.S3.Password}",
            "FILE_S3_BUCKET=simulator",
            "",
            "# SMB",
            $@"FILE_SMB_PATH=\\{info.Hostname}\shared",
            $"FILE_SMB_USERNAME={info.DefaultCredentials.Smb.Username}",
            $"FILE_SMB_PASSWORD={info.DefaultCredentials.Smb.Password}",
            "",
            "# NFS",
            $"FILE_NFS_SERVER={info.Hostname}",
            "FILE_NFS_PORT=32049",
            "FILE_NFS_PATH=/data"
        };
        return string.Join("\n", lines);
    }

    private static string FormatAsShell(ConnectionInfoResponse info)
    {
        var lines = new List<string>
        {
            "#!/bin/bash",
            "# File Simulator Connection Settings",
            $"# Generated: {info.GeneratedAt:O}",
            "",
            $"export FILE_SIMULATOR_HOST=\"{info.Hostname}\"",
            "",
            "# Endpoints",
            $"export FILE_SIMULATOR_DASHBOARD_URL=\"{info.Endpoints.Dashboard}\"",
            $"export FILE_SIMULATOR_API_URL=\"{info.Endpoints.ControlApi}\"",
            "",
            "# FTP",
            $"export FILE_FTP_HOST=\"{info.Hostname}\"",
            "export FILE_FTP_PORT=\"30021\"",
            $"export FILE_FTP_USERNAME=\"{info.DefaultCredentials.Ftp.Username}\"",
            $"export FILE_FTP_PASSWORD=\"{info.DefaultCredentials.Ftp.Password}\"",
            "",
            "# SFTP",
            $"export FILE_SFTP_HOST=\"{info.Hostname}\"",
            "export FILE_SFTP_PORT=\"30022\"",
            $"export FILE_SFTP_USERNAME=\"{info.DefaultCredentials.Sftp.Username}\"",
            $"export FILE_SFTP_PASSWORD=\"{info.DefaultCredentials.Sftp.Password}\"",
            "",
            "# S3/MinIO",
            $"export FILE_S3_ENDPOINT=\"http://{info.Hostname}:30900\"",
            $"export FILE_S3_ACCESS_KEY=\"{info.DefaultCredentials.S3.Username}\"",
            $"export FILE_S3_SECRET_KEY=\"{info.DefaultCredentials.S3.Password}\"",
            "export FILE_S3_BUCKET=\"simulator\""
        };
        return string.Join("\n", lines);
    }

    private static string FormatAsYaml(ConnectionInfoResponse info)
    {
        var lines = new List<string>
        {
            "# File Simulator Connection Settings",
            $"# Generated: {info.GeneratedAt:O}",
            "",
            "fileSimulator:",
            $"  host: {info.Hostname}",
            "",
            "  endpoints:",
            $"    dashboard: {info.Endpoints.Dashboard}",
            $"    api: {info.Endpoints.ControlApi}",
            "",
            "  ftp:",
            $"    host: {info.Hostname}",
            "    port: 30021",
            $"    username: {info.DefaultCredentials.Ftp.Username}",
            $"    password: {info.DefaultCredentials.Ftp.Password}",
            "",
            "  sftp:",
            $"    host: {info.Hostname}",
            "    port: 30022",
            $"    username: {info.DefaultCredentials.Sftp.Username}",
            $"    password: {info.DefaultCredentials.Sftp.Password}",
            "",
            "  http:",
            $"    url: http://{info.Hostname}:30088",
            $"    username: {info.DefaultCredentials.Http.Username}",
            $"    password: {info.DefaultCredentials.Http.Password}",
            "",
            "  s3:",
            $"    endpoint: http://{info.Hostname}:30900",
            $"    accessKey: {info.DefaultCredentials.S3.Username}",
            $"    secretKey: {info.DefaultCredentials.S3.Password}",
            "    bucket: simulator",
            "",
            "  smb:",
            $@"    path: \\{info.Hostname}\shared",
            $"    username: {info.DefaultCredentials.Smb.Username}",
            $"    password: {info.DefaultCredentials.Smb.Password}",
            "",
            "  nfs:",
            $"    server: {info.Hostname}",
            "    port: 32049",
            "    path: /data"
        };
        return string.Join("\n", lines);
    }

    private static string FormatAsDotNet(ConnectionInfoResponse info)
    {
        var lines = new List<string>
        {
            "// File Simulator Connection Settings for .NET",
            $"// Generated: {info.GeneratedAt:O}",
            "",
            "// appsettings.json configuration:",
            "{",
            "  \"FileSimulator\": {",
            $"    \"Host\": \"{info.Hostname}\",",
            "    \"Ftp\": {",
            $"      \"Host\": \"{info.Hostname}\",",
            "      \"Port\": 30021,",
            $"      \"Username\": \"{info.DefaultCredentials.Ftp.Username}\",",
            $"      \"Password\": \"{info.DefaultCredentials.Ftp.Password}\"",
            "    },",
            "    \"Sftp\": {",
            $"      \"Host\": \"{info.Hostname}\",",
            "      \"Port\": 30022,",
            $"      \"Username\": \"{info.DefaultCredentials.Sftp.Username}\",",
            $"      \"Password\": \"{info.DefaultCredentials.Sftp.Password}\"",
            "    },",
            "    \"S3\": {",
            $"      \"Endpoint\": \"http://{info.Hostname}:30900\",",
            $"      \"AccessKey\": \"{info.DefaultCredentials.S3.Username}\",",
            $"      \"SecretKey\": \"{info.DefaultCredentials.S3.Password}\",",
            "      \"Bucket\": \"simulator\"",
            "    }",
            "  }",
            "}"
        };
        return string.Join("\n", lines);
    }
}

// Response models
public record ConnectionInfoResponse
{
    public required string Hostname { get; init; }
    public DateTime GeneratedAt { get; init; }
    public required List<ServerConnectionInfo> Servers { get; init; }
    public required DefaultCredentials DefaultCredentials { get; init; }
    public required EndpointSummary Endpoints { get; init; }
}

public record ServerConnectionInfo
{
    public required string Name { get; init; }
    public required string Protocol { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; }
    public string? ClusterIp { get; init; }
    public int ClusterPort { get; init; }
    public string? ServiceName { get; init; }
    public bool IsDynamic { get; init; }
    public required string Status { get; init; }
    public string? Directory { get; init; }
    public string? ConnectionString { get; init; }
}

public record DefaultCredentials
{
    public required CredentialInfo Ftp { get; init; }
    public required CredentialInfo Sftp { get; init; }
    public required CredentialInfo S3 { get; init; }
    public required CredentialInfo Http { get; init; }
    public required CredentialInfo Smb { get; init; }
}

public record CredentialInfo
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string? Note { get; init; }
}

public record EndpointSummary
{
    public required string Dashboard { get; init; }
    public required string ControlApi { get; init; }
    public required string Ftp { get; init; }
    public required string Sftp { get; init; }
    public required string Http { get; init; }
    public required string S3 { get; init; }
    public required string Smb { get; init; }
    public required string Nfs { get; init; }
}
