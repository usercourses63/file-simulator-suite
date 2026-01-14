using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentFTP;
using Renci.SshNet;

namespace FileSimulator.Client;

/// <summary>
/// Unified client for accessing files through various protocols in the File Simulator Suite.
/// Designed to work identically in Minikube development and OCP production environments.
/// </summary>
public class FileSimulatorClient : IDisposable
{
    private readonly FileSimulatorOptions _options;
    private AsyncFtpClient? _ftpClient;
    private SftpClient? _sftpClient;
    private AmazonS3Client? _s3Client;
    private HttpClient? _httpClient;

    public FileSimulatorClient(FileSimulatorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    #region FTP Operations

    public async Task<AsyncFtpClient> GetFtpClientAsync()
    {
        if (_ftpClient == null || !_ftpClient.IsConnected)
        {
            _ftpClient = new AsyncFtpClient(
                _options.FtpHost,
                _options.FtpUsername,
                _options.FtpPassword,
                _options.FtpPort);
            await _ftpClient.AutoConnect();
        }
        return _ftpClient;
    }

    public async Task UploadViaFtpAsync(string localPath, string remotePath)
    {
        var client = await GetFtpClientAsync();
        await client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, true);
    }

    public async Task DownloadViaFtpAsync(string remotePath, string localPath)
    {
        var client = await GetFtpClientAsync();
        await client.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite);
    }

    public async Task<string[]> ListFtpDirectoryAsync(string path = "/")
    {
        var client = await GetFtpClientAsync();
        var items = await client.GetListing(path);
        return items.Select(i => i.FullName).ToArray();
    }

    #endregion

    #region SFTP Operations

    public SftpClient GetSftpClient()
    {
        if (_sftpClient == null || !_sftpClient.IsConnected)
        {
            _sftpClient = new SftpClient(
                _options.SftpHost,
                _options.SftpPort,
                _options.SftpUsername,
                _options.SftpPassword);
            _sftpClient.Connect();
        }
        return _sftpClient;
    }

    public void UploadViaSftp(string localPath, string remotePath)
    {
        var client = GetSftpClient();
        using var stream = File.OpenRead(localPath);
        client.UploadFile(stream, remotePath, true);
    }

    public void DownloadViaSftp(string remotePath, string localPath)
    {
        var client = GetSftpClient();
        using var stream = File.Create(localPath);
        client.DownloadFile(remotePath, stream);
    }

    public IEnumerable<string> ListSftpDirectory(string path = "/")
    {
        var client = GetSftpClient();
        return client.ListDirectory(path).Select(f => f.FullName);
    }

    #endregion

    #region S3 Operations

    public AmazonS3Client GetS3Client()
    {
        if (_s3Client == null)
        {
            var config = new AmazonS3Config
            {
                ServiceURL = _options.S3Endpoint,
                ForcePathStyle = true,
                UseHttp = !_options.S3Endpoint.StartsWith("https")
            };
            _s3Client = new AmazonS3Client(
                _options.S3AccessKey,
                _options.S3SecretKey,
                config);
        }
        return _s3Client;
    }

    public async Task UploadViaS3Async(string localPath, string bucket, string key)
    {
        var client = GetS3Client();
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            FilePath = localPath
        });
    }

    public async Task DownloadViaS3Async(string bucket, string key, string localPath)
    {
        var client = GetS3Client();
        var response = await client.GetObjectAsync(bucket, key);
        await response.WriteResponseStreamToFileAsync(localPath, false, CancellationToken.None);
    }

    public async Task<IEnumerable<string>> ListS3BucketAsync(string bucket, string? prefix = null)
    {
        var client = GetS3Client();
        var response = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = prefix
        });
        return response.S3Objects.Select(o => o.Key);
    }

    #endregion

    #region HTTP Operations

    public HttpClient GetHttpClient()
    {
        if (_httpClient == null)
        {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(_options.HttpUsername))
            {
                handler.Credentials = new System.Net.NetworkCredential(
                    _options.HttpUsername,
                    _options.HttpPassword);
            }
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_options.HttpBaseUrl)
            };
        }
        return _httpClient;
    }

    public async Task UploadViaHttpAsync(string localPath, string remotePath)
    {
        var client = GetHttpClient();
        using var content = new StreamContent(File.OpenRead(localPath));
        var response = await client.PutAsync($"/webdav/{remotePath}", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task DownloadViaHttpAsync(string remotePath, string localPath)
    {
        var client = GetHttpClient();
        var response = await client.GetAsync($"/download/{remotePath}");
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(localPath);
        await stream.CopyToAsync(fileStream);
    }

    #endregion

    #region Unified Operations

    /// <summary>
    /// Upload a file using the specified protocol
    /// </summary>
    public async Task UploadAsync(string localPath, string remotePath, FileProtocol protocol = FileProtocol.S3)
    {
        switch (protocol)
        {
            case FileProtocol.FTP:
                await UploadViaFtpAsync(localPath, remotePath);
                break;
            case FileProtocol.SFTP:
                UploadViaSftp(localPath, remotePath);
                break;
            case FileProtocol.S3:
                var (bucket, key) = ParseS3Path(remotePath);
                await UploadViaS3Async(localPath, bucket, key);
                break;
            case FileProtocol.HTTP:
                await UploadViaHttpAsync(localPath, remotePath);
                break;
            default:
                throw new ArgumentException($"Unsupported protocol: {protocol}");
        }
    }

    /// <summary>
    /// Download a file using the specified protocol
    /// </summary>
    public async Task DownloadAsync(string remotePath, string localPath, FileProtocol protocol = FileProtocol.S3)
    {
        switch (protocol)
        {
            case FileProtocol.FTP:
                await DownloadViaFtpAsync(remotePath, localPath);
                break;
            case FileProtocol.SFTP:
                DownloadViaSftp(remotePath, localPath);
                break;
            case FileProtocol.S3:
                var (bucket, key) = ParseS3Path(remotePath);
                await DownloadViaS3Async(bucket, key, localPath);
                break;
            case FileProtocol.HTTP:
                await DownloadViaHttpAsync(remotePath, localPath);
                break;
            default:
                throw new ArgumentException($"Unsupported protocol: {protocol}");
        }
    }

    private static (string bucket, string key) ParseS3Path(string path)
    {
        var parts = path.TrimStart('/').Split('/', 2);
        return parts.Length == 2 
            ? (parts[0], parts[1]) 
            : (parts[0], string.Empty);
    }

    #endregion

    public void Dispose()
    {
        _ftpClient?.Dispose();
        _sftpClient?.Dispose();
        _s3Client?.Dispose();
        _httpClient?.Dispose();
    }
}

public enum FileProtocol
{
    FTP,
    SFTP,
    S3,
    HTTP,
    SMB,
    NFS
}

public class FileSimulatorOptions
{
    // FTP Settings
    public string FtpHost { get; set; } = "localhost";
    public int FtpPort { get; set; } = 21;
    public string FtpUsername { get; set; } = "ftpuser";
    public string FtpPassword { get; set; } = "ftppass123";

    // SFTP Settings
    public string SftpHost { get; set; } = "localhost";
    public int SftpPort { get; set; } = 22;
    public string SftpUsername { get; set; } = "sftpuser";
    public string SftpPassword { get; set; } = "sftppass123";

    // S3/MinIO Settings
    public string S3Endpoint { get; set; } = "http://localhost:9000";
    public string S3AccessKey { get; set; } = "minioadmin";
    public string S3SecretKey { get; set; } = "minioadmin123";

    // HTTP Settings
    public string HttpBaseUrl { get; set; } = "http://localhost:80";
    public string? HttpUsername { get; set; } = "httpuser";
    public string? HttpPassword { get; set; } = "httppass123";

    // SMB Settings
    public string SmbHost { get; set; } = "localhost";
    public string SmbShare { get; set; } = "simulator";
    public string SmbUsername { get; set; } = "smbuser";
    public string SmbPassword { get; set; } = "smbpass123";

    /// <summary>
    /// Creates options from environment variables
    /// </summary>
    public static FileSimulatorOptions FromEnvironment()
    {
        return new FileSimulatorOptions
        {
            FtpHost = Environment.GetEnvironmentVariable("FILE_FTP_HOST") ?? "localhost",
            FtpPort = int.Parse(Environment.GetEnvironmentVariable("FILE_FTP_PORT") ?? "21"),
            FtpUsername = Environment.GetEnvironmentVariable("FILE_FTP_USERNAME") ?? "ftpuser",
            FtpPassword = Environment.GetEnvironmentVariable("FILE_FTP_PASSWORD") ?? "ftppass123",
            
            SftpHost = Environment.GetEnvironmentVariable("FILE_SFTP_HOST") ?? "localhost",
            SftpPort = int.Parse(Environment.GetEnvironmentVariable("FILE_SFTP_PORT") ?? "22"),
            SftpUsername = Environment.GetEnvironmentVariable("FILE_SFTP_USERNAME") ?? "sftpuser",
            SftpPassword = Environment.GetEnvironmentVariable("FILE_SFTP_PASSWORD") ?? "sftppass123",
            
            S3Endpoint = Environment.GetEnvironmentVariable("FILE_S3_ENDPOINT") ?? "http://localhost:9000",
            S3AccessKey = Environment.GetEnvironmentVariable("FILE_S3_ACCESS_KEY") ?? "minioadmin",
            S3SecretKey = Environment.GetEnvironmentVariable("FILE_S3_SECRET_KEY") ?? "minioadmin123",
            
            HttpBaseUrl = Environment.GetEnvironmentVariable("FILE_HTTP_URL") ?? "http://localhost:80",
            HttpUsername = Environment.GetEnvironmentVariable("FILE_HTTP_USERNAME"),
            HttpPassword = Environment.GetEnvironmentVariable("FILE_HTTP_PASSWORD"),
            
            SmbHost = Environment.GetEnvironmentVariable("FILE_SMB_HOST") ?? "localhost",
            SmbShare = Environment.GetEnvironmentVariable("FILE_SMB_SHARE") ?? "simulator",
            SmbUsername = Environment.GetEnvironmentVariable("FILE_SMB_USERNAME") ?? "smbuser",
            SmbPassword = Environment.GetEnvironmentVariable("FILE_SMB_PASSWORD") ?? "smbpass123"
        };
    }

    /// <summary>
    /// Creates options for Minikube development with NodePorts
    /// </summary>
    public static FileSimulatorOptions ForMinikube(string minikubeIp)
    {
        return new FileSimulatorOptions
        {
            FtpHost = minikubeIp,
            FtpPort = 30021,
            SftpHost = minikubeIp,
            SftpPort = 30022,
            S3Endpoint = $"http://{minikubeIp}:30900",
            HttpBaseUrl = $"http://{minikubeIp}:30088",
            SmbHost = minikubeIp
        };
    }

    /// <summary>
    /// Creates options for in-cluster usage (pods accessing the simulator)
    /// </summary>
    public static FileSimulatorOptions ForCluster(string @namespace = "file-simulator", string releaseName = "file-sim")
    {
        var prefix = $"{releaseName}-file-simulator";
        var suffix = $".{@namespace}.svc.cluster.local";
        
        return new FileSimulatorOptions
        {
            FtpHost = $"{prefix}-ftp{suffix}",
            FtpPort = 21,
            SftpHost = $"{prefix}-sftp{suffix}",
            SftpPort = 22,
            S3Endpoint = $"http://{prefix}-s3{suffix}:9000",
            HttpBaseUrl = $"http://{prefix}-http{suffix}",
            SmbHost = $"{prefix}-smb{suffix}"
        };
    }
}
