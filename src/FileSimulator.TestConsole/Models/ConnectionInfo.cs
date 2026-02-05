namespace FileSimulator.TestConsole.Models;

/// <summary>
/// Response from /api/connection-info endpoint.
/// </summary>
public record ConnectionInfoResponse
{
    public required string Hostname { get; init; }
    public DateTime GeneratedAt { get; init; }
    public required List<ServerConnectionInfo> Servers { get; init; }
    public required DefaultCredentials DefaultCredentials { get; init; }
    public required EndpointSummary Endpoints { get; init; }
}

/// <summary>
/// Connection details for a single server.
/// </summary>
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

/// <summary>
/// Default credentials for all protocols.
/// </summary>
public record DefaultCredentials
{
    public required CredentialInfo Ftp { get; init; }
    public required CredentialInfo Sftp { get; init; }
    public required CredentialInfo S3 { get; init; }
    public required CredentialInfo Http { get; init; }
    public required CredentialInfo Smb { get; init; }
    public required CredentialInfo Management { get; init; }
}

/// <summary>
/// Credential information for a protocol.
/// </summary>
public record CredentialInfo
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string? Note { get; init; }
}

/// <summary>
/// Summary of all endpoint URLs.
/// </summary>
public record EndpointSummary
{
    public required string Dashboard { get; init; }
    public required string ControlApi { get; init; }
    public required string Management { get; init; }
    public required string Ftp { get; init; }
    public required string Sftp { get; init; }
    public required string Http { get; init; }
    public required string S3 { get; init; }
    public required string Smb { get; init; }
    public required string Nfs { get; init; }
}
