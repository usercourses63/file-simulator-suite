using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using SMBLibrary;
using SMBLibrary.Client;

namespace FileSimulator.Client.Services;

#region Interfaces

/// <summary>
/// Common interface for all file protocol operations
/// </summary>
public interface IFileProtocolService
{
    string ProtocolName { get; }
    
    /// <summary>
    /// Discover/list files in a directory (for polling)
    /// </summary>
    Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default);
    
    /// <summary>
    /// Read/download a file
    /// </summary>
    Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default);
    
    /// <summary>
    /// Read file to a local path
    /// </summary>
    Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default);
    
    /// <summary>
    /// Write/upload a file
    /// </summary>
    Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default);
    
    /// <summary>
    /// Upload from local path
    /// </summary>
    Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default);
    
    /// <summary>
    /// Delete a file after processing
    /// </summary>
    Task DeleteFileAsync(string remotePath, CancellationToken ct = default);
    
    /// <summary>
    /// Check if service is healthy/connected
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}

/// <summary>
/// Information about a remote file
/// </summary>
public record RemoteFileInfo
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
    public long Size { get; init; }
    public DateTime ModifiedAt { get; init; }
    public bool IsDirectory { get; init; }
}

#endregion

#region FTP Service

/// <summary>
/// FTP file operations using FluentFTP
/// </summary>
public class FtpFileService : IFileProtocolService, IDisposable
{
    private readonly FtpServerOptions _options;
    private readonly ILogger<FtpFileService> _logger;
    private AsyncFtpClient? _client;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public string ProtocolName => "FTP";

    public FtpFileService(IOptions<FtpServerOptions> options, ILogger<FtpFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private async Task<AsyncFtpClient> GetClientAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_client == null || !_client.IsConnected)
            {
                _client?.Dispose();
                _client = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);
                _client.Config.ConnectTimeout = 30000;
                _client.Config.ReadTimeout = 60000;
                _client.Config.DataConnectionConnectTimeout = 30000;
                
                await _client.AutoConnect(ct);
                _logger.LogInformation("Connected to FTP server {Host}:{Port}", _options.Host, _options.Port);
            }
            return _client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var items = await client.GetListing(path, FtpListOption.Modify, ct);
        
        var files = items
            .Where(i => i.Type == FtpObjectType.File)
            .Where(i => pattern == null || MatchesPattern(i.Name, pattern))
            .Select(i => new RemoteFileInfo
            {
                FullPath = i.FullName,
                Name = i.Name,
                Size = i.Size,
                ModifiedAt = i.Modified,
                IsDirectory = false
            });

        _logger.LogDebug("FTP discovered {Count} files in {Path}", files.Count(), path);
        return files;
    }

    public async Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        using var stream = new MemoryStream();
        
        var success = await client.DownloadStream(stream, remotePath, token: ct);
        if (!success)
            throw new IOException($"Failed to download file: {remotePath}");
            
        _logger.LogInformation("FTP read {Size} bytes from {Path}", stream.Length, remotePath);
        return stream.ToArray();
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        
        var status = await client.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite, FtpVerify.None, null, ct);
        if (status != FtpStatus.Success)
            throw new IOException($"Failed to download file: {remotePath}, Status: {status}");
            
        _logger.LogInformation("FTP downloaded {RemotePath} to {LocalPath}", remotePath, localPath);
    }

    public async Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        using var stream = new MemoryStream(content);
        
        var status = await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite, true, null, ct);
        if (status != FtpStatus.Success)
            throw new IOException($"Failed to upload file: {remotePath}, Status: {status}");
            
        _logger.LogInformation("FTP wrote {Size} bytes to {Path}", content.Length, remotePath);
    }

    public async Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        
        var status = await client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, true, FtpVerify.None, null, ct);
        if (status != FtpStatus.Success)
            throw new IOException($"Failed to upload file: {localPath}, Status: {status}");
            
        _logger.LogInformation("FTP uploaded {LocalPath} to {RemotePath}", localPath, remotePath);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        await client.DeleteFile(remotePath, ct);
        _logger.LogInformation("FTP deleted {Path}", remotePath);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var client = await GetClientAsync(ct);
            return client.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FTP health check failed");
            return false;
        }
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _connectionLock.Dispose();
    }
}

public class FtpServerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 21;
    public string Username { get; set; } = "ftpuser";
    public string Password { get; set; } = "ftppass123";
}

#endregion

#region SFTP Service

/// <summary>
/// SFTP file operations using SSH.NET
/// </summary>
public class SftpFileService : IFileProtocolService, IDisposable
{
    private readonly SftpServerOptions _options;
    private readonly ILogger<SftpFileService> _logger;
    private SftpClient? _client;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public string ProtocolName => "SFTP";

    public SftpFileService(IOptions<SftpServerOptions> options, ILogger<SftpFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private SftpClient GetClient()
    {
        _connectionLock.Wait();
        try
        {
            if (_client == null || !_client.IsConnected)
            {
                _client?.Dispose();
                _client = new SftpClient(_options.Host, _options.Port, _options.Username, _options.Password);
                _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                _client.OperationTimeout = TimeSpan.FromMinutes(5);
                
                _client.Connect();
                _logger.LogInformation("Connected to SFTP server {Host}:{Port}", _options.Host, _options.Port);
            }
            return _client;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var client = GetClient();
            var items = client.ListDirectory(path);
            
            var files = items
                .Where(i => !i.IsDirectory && i.Name != "." && i.Name != "..")
                .Where(i => pattern == null || MatchesPattern(i.Name, pattern))
                .Select(i => new RemoteFileInfo
                {
                    FullPath = i.FullName,
                    Name = i.Name,
                    Size = i.Length,
                    ModifiedAt = i.LastWriteTime,
                    IsDirectory = false
                });

            _logger.LogDebug("SFTP discovered {Count} files in {Path}", files.Count(), path);
            return files;
        }, ct);
    }

    public Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var client = GetClient();
            using var stream = new MemoryStream();
            
            client.DownloadFile(remotePath, stream);
            
            _logger.LogInformation("SFTP read {Size} bytes from {Path}", stream.Length, remotePath);
            return stream.ToArray();
        }, ct);
    }

    public Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var client = GetClient();
            using var stream = File.Create(localPath);
            
            client.DownloadFile(remotePath, stream);
            
            _logger.LogInformation("SFTP downloaded {RemotePath} to {LocalPath}", remotePath, localPath);
        }, ct);
    }

    public Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var client = GetClient();
            using var stream = new MemoryStream(content);
            
            client.UploadFile(stream, remotePath, true);
            
            _logger.LogInformation("SFTP wrote {Size} bytes to {Path}", content.Length, remotePath);
        }, ct);
    }

    public Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var client = GetClient();
            using var stream = File.OpenRead(localPath);
            
            client.UploadFile(stream, remotePath, true);
            
            _logger.LogInformation("SFTP uploaded {LocalPath} to {RemotePath}", localPath, remotePath);
        }, ct);
    }

    public Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var client = GetClient();
            client.DeleteFile(remotePath);
            _logger.LogInformation("SFTP deleted {Path}", remotePath);
        }, ct);
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var client = GetClient();
                return client.IsConnected;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SFTP health check failed");
                return false;
            }
        }, ct);
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _connectionLock.Dispose();
    }
}

public class SftpServerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "sftpuser";
    public string Password { get; set; } = "sftppass123";
}

#endregion

#region S3 Service

/// <summary>
/// S3/MinIO file operations using AWS SDK
/// </summary>
public class S3FileService : IFileProtocolService, IDisposable
{
    private readonly S3ServerOptions _options;
    private readonly ILogger<S3FileService> _logger;
    private readonly AmazonS3Client _client;

    public string ProtocolName => "S3";

    public S3FileService(IOptions<S3ServerOptions> options, ILogger<S3FileService> logger)
    {
        _options = options.Value;
        _logger = logger;
        
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,
            UseHttp = !_options.Endpoint.StartsWith("https")
        };
        
        _client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public async Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        // Path format: "bucket/prefix" or just "bucket"
        var (bucket, prefix) = ParsePath(path);
        
        var request = new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = prefix
        };

        var files = new List<RemoteFileInfo>();
        ListObjectsV2Response response;
        
        do
        {
            response = await _client.ListObjectsV2Async(request, ct);
            
            foreach (var obj in response.S3Objects)
            {
                var name = Path.GetFileName(obj.Key);
                if (pattern == null || MatchesPattern(name, pattern))
                {
                    files.Add(new RemoteFileInfo
                    {
                        FullPath = $"{bucket}/{obj.Key}",
                        Name = name,
                        Size = obj.Size,
                        ModifiedAt = obj.LastModified,
                        IsDirectory = obj.Key.EndsWith("/")
                    });
                }
            }
            
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated);

        _logger.LogDebug("S3 discovered {Count} files in {Bucket}/{Prefix}", files.Count, bucket, prefix);
        return files;
    }

    public async Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var (bucket, key) = ParsePath(remotePath);
        
        var response = await _client.GetObjectAsync(bucket, key, ct);
        using var stream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(stream, ct);
        
        _logger.LogInformation("S3 read {Size} bytes from {Bucket}/{Key}", stream.Length, bucket, key);
        return stream.ToArray();
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        var (bucket, key) = ParsePath(remotePath);
        
        var response = await _client.GetObjectAsync(bucket, key, ct);
        await response.WriteResponseStreamToFileAsync(localPath, false, ct);
        
        _logger.LogInformation("S3 downloaded {Bucket}/{Key} to {LocalPath}", bucket, key, localPath);
    }

    public async Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default)
    {
        var (bucket, key) = ParsePath(remotePath);
        
        using var stream = new MemoryStream(content);
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream
        };
        
        await _client.PutObjectAsync(request, ct);
        
        _logger.LogInformation("S3 wrote {Size} bytes to {Bucket}/{Key}", content.Length, bucket, key);
    }

    public async Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var (bucket, key) = ParsePath(remotePath);
        
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            FilePath = localPath
        };
        
        await _client.PutObjectAsync(request, ct);
        
        _logger.LogInformation("S3 uploaded {LocalPath} to {Bucket}/{Key}", localPath, bucket, key);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        var (bucket, key) = ParsePath(remotePath);
        
        await _client.DeleteObjectAsync(bucket, key, ct);
        
        _logger.LogInformation("S3 deleted {Bucket}/{Key}", bucket, key);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.ListBucketsAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "S3 health check failed");
            return false;
        }
    }

    private static (string bucket, string key) ParsePath(string path)
    {
        var cleanPath = path.TrimStart('/');
        var parts = cleanPath.Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], string.Empty);
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

public class S3ServerOptions
{
    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin123";
    public string DefaultBucket { get; set; } = "input";
}

#endregion

#region HTTP/WebDAV Service

/// <summary>
/// HTTP/WebDAV file operations
/// </summary>
public class HttpFileService : IFileProtocolService, IDisposable
{
    private readonly HttpServerOptions _options;
    private readonly ILogger<HttpFileService> _logger;
    private readonly HttpClient _client;

    public string ProtocolName => "HTTP";

    public HttpFileService(IOptions<HttpServerOptions> options, ILogger<HttpFileService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var handler = new HttpClientHandler();
        if (!string.IsNullOrEmpty(_options.Username))
        {
            handler.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        _client = new HttpClient(handler)
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromMinutes(10)
        };
    }

    public async Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        // Use JSON endpoint for listing
        var response = await _client.GetAsync($"/api/files{path}", ct);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync(ct);
        var items = System.Text.Json.JsonSerializer.Deserialize<List<NginxFileInfo>>(json) ?? new();
        
        var files = items
            .Where(i => i.type == "file")
            .Where(i => pattern == null || MatchesPattern(i.name, pattern))
            .Select(i => new RemoteFileInfo
            {
                FullPath = $"{path.TrimEnd('/')}/{i.name}",
                Name = i.name,
                Size = i.size,
                ModifiedAt = DateTime.Parse(i.mtime),
                IsDirectory = false
            });

        _logger.LogDebug("HTTP discovered {Count} files in {Path}", files.Count(), path);
        return files;
    }

    public async Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"/download{remotePath}", ct);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsByteArrayAsync(ct);
        
        _logger.LogInformation("HTTP read {Size} bytes from {Path}", content.Length, remotePath);
        return content;
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        var response = await _client.GetAsync($"/download{remotePath}", ct);
        response.EnsureSuccessStatusCode();
        
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = File.Create(localPath);
        await stream.CopyToAsync(fileStream, ct);
        
        _logger.LogInformation("HTTP downloaded {RemotePath} to {LocalPath}", remotePath, localPath);
    }

    public async Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default)
    {
        using var httpContent = new ByteArrayContent(content);
        var response = await _client.PutAsync($"/webdav{remotePath}", httpContent, ct);
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("HTTP wrote {Size} bytes to {Path}", content.Length, remotePath);
    }

    public async Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(localPath);
        using var content = new StreamContent(stream);
        
        var response = await _client.PutAsync($"/webdav{remotePath}", content, ct);
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("HTTP uploaded {LocalPath} to {RemotePath}", localPath, remotePath);
    }

    public async Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        var response = await _client.DeleteAsync($"/webdav{remotePath}", ct);
        response.EnsureSuccessStatusCode();
        
        _logger.LogInformation("HTTP deleted {Path}", remotePath);
    }

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetAsync("/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP health check failed");
            return false;
        }
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private record NginxFileInfo(string name, string type, string mtime, long size);
}

public class HttpServerOptions
{
    public string BaseUrl { get; set; } = "http://localhost:80";
    public string? Username { get; set; } = "httpuser";
    public string? Password { get; set; } = "httppass123";
}

#endregion

#region SMB Service

/// <summary>
/// SMB/CIFS file operations using SMBLibrary
/// </summary>
public class SmbFileService : IFileProtocolService, IDisposable
{
    private readonly SmbServerOptions _options;
    private readonly ILogger<SmbFileService> _logger;
    private SMB2Client? _client;
    private ISMBFileStore? _fileStore;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public string ProtocolName => "SMB";

    public SmbFileService(IOptions<SmbServerOptions> options, ILogger<SmbFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private (SMB2Client client, ISMBFileStore store) GetConnection()
    {
        _connectionLock.Wait();
        try
        {
            if (_client == null || !_client.IsConnected)
            {
                _client?.Disconnect();
                _client = new SMB2Client();
                
                // Use custom port if specified (for NodePort access)
                // Note: NTLM auth fails through TCP proxies, use direct NodePort or in-cluster DNS
                bool connected;
                if (_options.Port != 445)
                {
                    connected = _client.Connect(IPAddress.Parse(_options.Host), SMBTransportType.DirectTCPTransport, _options.Port);
                }
                else
                {
                    connected = _client.Connect(IPAddress.Parse(_options.Host), SMBTransportType.DirectTCPTransport);
                }
                if (!connected)
                    throw new IOException($"Failed to connect to SMB server: {_options.Host}:{_options.Port}");

                var status = _client.Login(_options.Domain, _options.Username, _options.Password);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new IOException($"SMB login failed: {status}");

                _fileStore = _client.TreeConnect(_options.ShareName, out status);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new IOException($"Failed to connect to share {_options.ShareName}: {status}");

                _logger.LogInformation("Connected to SMB share \\\\{Host}:{Port}\\{Share}", _options.Host, _options.Port, _options.ShareName);
            }
            return (_client!, _fileStore!);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var (_, store) = GetConnection();
            var searchPath = path.TrimStart('/').Replace('/', '\\');
            if (!string.IsNullOrEmpty(searchPath) && !searchPath.EndsWith("\\"))
                searchPath += "\\";
            searchPath += "*";

            var status = store.CreateFile(
                out var handle,
                out _,
                Path.GetDirectoryName(searchPath) ?? "",
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new IOException($"Failed to open directory: {status}");

            var files = new List<RemoteFileInfo>();
            
            store.QueryDirectory(out var entries, handle, "*", FileInformationClass.FileDirectoryInformation);
            store.CloseFile(handle);

            foreach (var entry in entries.Cast<FileDirectoryInformation>())
            {
                if (entry.FileName == "." || entry.FileName == "..")
                    continue;
                    
                if ((entry.FileAttributes & SMBLibrary.FileAttributes.Directory) != 0)
                    continue;

                if (pattern == null || MatchesPattern(entry.FileName, pattern))
                {
                    files.Add(new RemoteFileInfo
                    {
                        FullPath = $"{path.TrimEnd('/')}/{entry.FileName}",
                        Name = entry.FileName,
                        Size = entry.EndOfFile,
                        ModifiedAt = entry.LastWriteTime,
                        IsDirectory = false
                    });
                }
            }

            _logger.LogDebug("SMB discovered {Count} files in {Path}", files.Count, path);
            return files.AsEnumerable();
        }, ct);
    }

    public Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var (_, store) = GetConnection();
            var smbPath = remotePath.TrimStart('/').Replace('/', '\\');

            var status = store.CreateFile(
                out var handle,
                out _,
                smbPath,
                AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new IOException($"Failed to open file: {status}");

            using var ms = new MemoryStream();
            long offset = 0;
            
            while (true)
            {
                status = store.ReadFile(out var data, handle, offset, 65536);
                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                    throw new IOException($"Failed to read file: {status}");

                if (data == null || data.Length == 0)
                    break;

                ms.Write(data, 0, data.Length);
                offset += data.Length;
            }

            store.CloseFile(handle);
            
            _logger.LogInformation("SMB read {Size} bytes from {Path}", ms.Length, remotePath);
            return ms.ToArray();
        }, ct);
    }

    public async Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        var content = await ReadFileAsync(remotePath, ct);
        await File.WriteAllBytesAsync(localPath, content, ct);
        _logger.LogInformation("SMB downloaded {RemotePath} to {LocalPath}", remotePath, localPath);
    }

    public Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var (_, store) = GetConnection();
            var smbPath = remotePath.TrimStart('/').Replace('/', '\\');

            var status = store.CreateFile(
                out var handle,
                out _,
                smbPath,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.None,
                CreateDisposition.FILE_OVERWRITE_IF,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new IOException($"Failed to create file: {status}");

            int offset = 0;
            while (offset < content.Length)
            {
                var chunk = Math.Min(65536, content.Length - offset);
                var data = new byte[chunk];
                Array.Copy(content, offset, data, 0, chunk);
                
                status = store.WriteFile(out _, handle, offset, data);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new IOException($"Failed to write file: {status}");
                    
                offset += chunk;
            }

            store.CloseFile(handle);
            
            _logger.LogInformation("SMB wrote {Size} bytes to {Path}", content.Length, remotePath);
        }, ct);
    }

    public async Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var content = await File.ReadAllBytesAsync(localPath, ct);
        await WriteFileAsync(remotePath, content, ct);
        _logger.LogInformation("SMB uploaded {LocalPath} to {RemotePath}", localPath, remotePath);
    }

    public Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var (_, store) = GetConnection();
            var smbPath = remotePath.TrimStart('/').Replace('/', '\\');

            var status = store.CreateFile(
                out var handle,
                out _,
                smbPath,
                AccessMask.DELETE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Delete,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_DELETE_ON_CLOSE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
                throw new IOException($"Failed to delete file: {status}");

            store.CloseFile(handle);
            
            _logger.LogInformation("SMB deleted {Path}", remotePath);
        }, ct);
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            try
            {
                var (client, _) = GetConnection();
                return client.IsConnected;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMB health check failed");
                return false;
            }
        }, ct);
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose()
    {
        _fileStore = null;
        _client?.Disconnect();
        _connectionLock.Dispose();
    }
}

public class SmbServerOptions
{
    public string Host { get; set; } = "localhost";
    /// <summary>
    /// SMB port. Default 445 for standard SMB, use 30445 for Kubernetes NodePort access.
    /// Note: NTLM authentication fails through TCP proxies (kubectl port-forward, minikube service).
    /// Use direct NodePort access (requires Hyper-V/VirtualBox driver) or in-cluster service DNS.
    /// </summary>
    public int Port { get; set; } = 445;
    public string ShareName { get; set; } = "simulator";
    public string Domain { get; set; } = "";
    public string Username { get; set; } = "smbuser";
    public string Password { get; set; } = "smbpass123";
}

#endregion

#region NFS Service (via mount or NFS client)

/// <summary>
/// NFS file operations - uses mounted filesystem approach
/// For OCP, mount NFS share as a PersistentVolume
/// </summary>
public class NfsFileService : IFileProtocolService
{
    private readonly NfsServerOptions _options;
    private readonly ILogger<NfsFileService> _logger;

    public string ProtocolName => "NFS";

    public NfsFileService(IOptions<NfsServerOptions> options, ILogger<NfsFileService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (!Directory.Exists(_options.MountPath))
        {
            _logger.LogWarning("NFS mount path does not exist: {Path}. Ensure NFS is mounted.", _options.MountPath);
        }
    }

    public Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var fullPath = Path.Combine(_options.MountPath, path.TrimStart('/'));
            
            if (!Directory.Exists(fullPath))
                return Enumerable.Empty<RemoteFileInfo>();

            var searchPattern = pattern ?? "*";
            var files = Directory.GetFiles(fullPath, searchPattern)
                .Select(f => new FileInfo(f))
                .Select(fi => new RemoteFileInfo
                {
                    FullPath = Path.Combine(path, fi.Name),
                    Name = fi.Name,
                    Size = fi.Length,
                    ModifiedAt = fi.LastWriteTimeUtc,
                    IsDirectory = false
                });

            _logger.LogDebug("NFS discovered {Count} files in {Path}", files.Count(), path);
            return files;
        }, ct);
    }

    public async Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, remotePath.TrimStart('/'));
        var content = await File.ReadAllBytesAsync(fullPath, ct);
        
        _logger.LogInformation("NFS read {Size} bytes from {Path}", content.Length, remotePath);
        return content;
    }

    public Task DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, remotePath.TrimStart('/'));
        File.Copy(fullPath, localPath, true);
        
        _logger.LogInformation("NFS copied {RemotePath} to {LocalPath}", remotePath, localPath);
        return Task.CompletedTask;
    }

    public async Task WriteFileAsync(string remotePath, byte[] content, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, remotePath.TrimStart('/'));
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
            
        await File.WriteAllBytesAsync(fullPath, content, ct);
        
        _logger.LogInformation("NFS wrote {Size} bytes to {Path}", content.Length, remotePath);
    }

    public Task UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, remotePath.TrimStart('/'));
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
            
        File.Copy(localPath, fullPath, true);
        
        _logger.LogInformation("NFS copied {LocalPath} to {RemotePath}", localPath, remotePath);
        return Task.CompletedTask;
    }

    public Task DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, remotePath.TrimStart('/'));
        File.Delete(fullPath);
        
        _logger.LogInformation("NFS deleted {Path}", remotePath);
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        var exists = Directory.Exists(_options.MountPath);
        if (!exists)
            _logger.LogWarning("NFS health check failed - mount path does not exist: {Path}", _options.MountPath);
        return Task.FromResult(exists);
    }
}

public class NfsServerOptions
{
    /// <summary>
    /// Local path where NFS is mounted
    /// In OCP, this would be the PersistentVolume mount path
    /// </summary>
    public string MountPath { get; set; } = "/mnt/nfs";
}

#endregion
