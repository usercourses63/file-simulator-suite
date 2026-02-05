using System.Text.Json.Serialization;

namespace FileSimulator.IntegrationTests.Models;

/// <summary>
/// Response from /api/connection-info endpoint containing all protocol configurations.
/// </summary>
public class ConnectionInfoResponse
{
    [JsonPropertyName("ftp")]
    public FtpConfig Ftp { get; set; } = new();

    [JsonPropertyName("sftp")]
    public SftpConfig Sftp { get; set; } = new();

    [JsonPropertyName("http")]
    public HttpConfig Http { get; set; } = new();

    [JsonPropertyName("webdav")]
    public WebDavConfig WebDav { get; set; } = new();

    [JsonPropertyName("s3")]
    public S3Config S3 { get; set; } = new();

    [JsonPropertyName("smb")]
    public SmbConfig Smb { get; set; } = new();

    [JsonPropertyName("nfs")]
    public NfsConfig Nfs { get; set; } = new();

    [JsonPropertyName("kafka")]
    public KafkaConfig Kafka { get; set; } = new();
}

public class FtpConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("basePath")]
    public string BasePath { get; set; } = string.Empty;
}

public class SftpConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("basePath")]
    public string BasePath { get; set; } = string.Empty;
}

public class HttpConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("serviceUrl")]
    public string ServiceUrl { get; set; } = string.Empty;

    [JsonPropertyName("basePath")]
    public string BasePath { get; set; } = string.Empty;
}

public class WebDavConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("serviceUrl")]
    public string ServiceUrl { get; set; } = string.Empty;

    [JsonPropertyName("basePath")]
    public string BasePath { get; set; } = string.Empty;
}

public class S3Config
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("accessKey")]
    public string AccessKey { get; set; } = string.Empty;

    [JsonPropertyName("secretKey")]
    public string SecretKey { get; set; } = string.Empty;

    [JsonPropertyName("serviceUrl")]
    public string ServiceUrl { get; set; } = string.Empty;

    [JsonPropertyName("bucketName")]
    public string BucketName { get; set; } = string.Empty;
}

public class SmbConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("shareName")]
    public string ShareName { get; set; } = string.Empty;
}

public class NfsConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("mountPath")]
    public string MountPath { get; set; } = string.Empty;

    [JsonPropertyName("exportPath")]
    public string ExportPath { get; set; } = string.Empty;
}

public class KafkaConfig
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }

    [JsonPropertyName("bootstrapServers")]
    public string BootstrapServers { get; set; } = string.Empty;
}
