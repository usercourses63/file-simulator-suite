using System.Text.Json.Serialization;

namespace FileSimulator.IntegrationTests.Models;

/// <summary>
/// Response from /api/connection-info endpoint containing all server configurations.
/// </summary>
public class ConnectionInfoResponse
{
    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; set; }

    [JsonPropertyName("servers")]
    public List<ServerInfo> Servers { get; set; } = new();

    [JsonPropertyName("defaultCredentials")]
    public Dictionary<string, CredentialInfo> DefaultCredentials { get; set; } = new();

    [JsonPropertyName("endpoints")]
    public EndpointsInfo Endpoints { get; set; } = new();

    /// <summary>
    /// Gets the first server matching the specified protocol.
    /// </summary>
    public ServerInfo? GetServer(string protocol)
    {
        return Servers.FirstOrDefault(s =>
            s.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all servers matching the specified protocol.
    /// </summary>
    public IEnumerable<ServerInfo> GetServers(string protocol)
    {
        return Servers.Where(s =>
            s.Protocol.Equals(protocol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a server by its name.
    /// </summary>
    public ServerInfo? GetServerByName(string name)
    {
        return Servers.FirstOrDefault(s =>
            s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Server information from the connection-info response.
/// </summary>
public class ServerInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("clusterIp")]
    public string ClusterIp { get; set; } = string.Empty;

    [JsonPropertyName("clusterPort")]
    public int ClusterPort { get; set; }

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("isDynamic")]
    public bool IsDynamic { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("connectionString")]
    public string ConnectionString { get; set; } = string.Empty;

    [JsonPropertyName("credentials")]
    public CredentialInfo Credentials { get; set; } = new();
}

/// <summary>
/// Credential information for a server.
/// </summary>
public class CredentialInfo
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Endpoint URLs from the connection-info response.
/// </summary>
public class EndpointsInfo
{
    [JsonPropertyName("dashboard")]
    public string Dashboard { get; set; } = string.Empty;

    [JsonPropertyName("controlApi")]
    public string ControlApi { get; set; } = string.Empty;

    [JsonPropertyName("management")]
    public string Management { get; set; } = string.Empty;

    [JsonPropertyName("ftp")]
    public string Ftp { get; set; } = string.Empty;

    [JsonPropertyName("sftp")]
    public string Sftp { get; set; } = string.Empty;

    [JsonPropertyName("http")]
    public string Http { get; set; } = string.Empty;

    [JsonPropertyName("s3")]
    public string S3 { get; set; } = string.Empty;

    [JsonPropertyName("smb")]
    public string Smb { get; set; } = string.Empty;

    [JsonPropertyName("nfs")]
    public string Nfs { get; set; } = string.Empty;

    [JsonPropertyName("kafka")]
    public string? Kafka { get; set; }
}

/// <summary>
/// Kafka-specific server information from /api/kafka/info endpoint.
/// </summary>
public class KafkaInfo
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("bootstrapServers")]
    public string BootstrapServers { get; set; } = string.Empty;

    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; set; }
}
