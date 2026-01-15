# File Simulator Suite

A comprehensive file access simulator for Kubernetes development and testing. This suite provides multiple file transfer protocols (FTP, SFTP, HTTP/WebDAV, S3/MinIO, SMB, NFS) in a unified deployment, enabling seamless testing between Windows development environments and Kubernetes/OpenShift clusters.

## Table of Contents

- [Purpose](#-purpose)
- [Architecture](#-architecture)
- [Prerequisites](#-prerequisites)
- [Installation](#-installation)
  - [Automated Installation](#automated-installation)
  - [Manual Installation](#manual-installation)
- [Service Endpoints](#-service-endpoints)
- [Configuration](#-configuration)
- [.NET Client Library](#-net-client-library)
- [Cross-Cluster Configuration](#-cross-cluster-configuration)
- [Testing](#-testing)
- [Troubleshooting](#-troubleshooting)
- [Project Structure](#-project-structure)

---

## Purpose

- **Bridge Development & Production**: Allow code running in Minikube to work identically to OCP
- **Easy Testing**: Let testers on Windows supply input files and retrieve outputs
- **Protocol Flexibility**: Support multiple file access protocols from a single deployment
- **Central Management**: Web-based UI for file management across all protocols
- **SMB Support**: Full Windows file sharing with LoadBalancer for standard port 445

---

## Architecture

```
+-----------------------------------------------------------------------------+
|                         file-simulator namespace                             |
|                                                                              |
|  +------------------------------------------------------------------------+  |
|  |                     Management UI (FileBrowser)                        |  |
|  |                     http://minikube-ip:30180                           |  |
|  +------------------------------------------------------------------------+  |
|                                     |                                        |
|  +----------+----------+-----------+----+----------+-----------+---------+  |
|  |   FTP    |   SFTP   |      HTTP      |    S3    |    SMB    |   NFS   |  |
|  |  :30021  |  :30022  |     :30088     |  :30900  | :445 (LB) | :32149  |  |
|  +----+-----+----+-----+-------+--------+----+-----+-----+-----+----+----+  |
|       |          |             |             |           |          |       |
|  +----+----------+-------------+-------------+-----------+----------+----+  |
|  |                    Shared PVC (hostPath mount)                         |  |
|  |                      /mnt/simulator-data                               |  |
|  +-----------------------------------------------------------------------+  |
+-----------------------------------------------------------------------------+
                                      |
                                      v
+-----------------------------------------------------------------------------+
|                    Windows Host: C:\simulator-data                          |
|  +-- input/     <- Testers place test files here                            |
|  +-- output/    <- Services write output files here                         |
|  +-- temp/      <- Temporary processing files                               |
|  +-- config/    <- Configuration and helper scripts                         |
+-----------------------------------------------------------------------------+
```

### SMB Architecture (LoadBalancer with Minikube Tunnel)

SMB requires the standard port 445 for Windows clients. This is achieved using:

```
Windows Client                  Minikube Tunnel                    SMB Pod
     |                               |                                |
     | net use \\172.23.17.71\sim    |                                |
     |------------------------------>|                                |
     |        Port 445               |                                |
     |                               |------------------------------->|
     |                               |   LoadBalancer External IP     |
     |                               |         Port 445               |
```

---

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| Windows 10/11 | - | Host OS with Hyper-V enabled |
| Hyper-V | - | Required for SMB LoadBalancer support |
| Minikube | 1.32+ | Kubernetes cluster |
| kubectl | 1.28+ | Kubernetes CLI |
| Helm | 3.x | Package manager |
| .NET SDK | 9.0 | For C# client library |

### Enable Hyper-V (Required for SMB)

SMB requires LoadBalancer with `minikube tunnel` which works best with Hyper-V driver:

```powershell
# Run as Administrator
Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V -All

# Restart required after enabling
Restart-Computer
```

### Verify Hyper-V is enabled

```powershell
Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V
# State should be "Enabled"
```

---

## Installation

### Automated Installation

The easiest way to install is using the automated installation script:

```powershell
# Run as Administrator
.\scripts\Install-Simulator.ps1

# With custom options
.\scripts\Install-Simulator.ps1 -MinikubeMemory 8192 -MinikubeCPUs 4 -SkipMinikubeCreate
```

The script will:
1. Create the `C:\simulator-data` directory structure
2. Create a Minikube cluster with Hyper-V driver
3. Mount the Windows directory to Minikube
4. Deploy the Helm chart
5. Start `minikube tunnel` for SMB LoadBalancer
6. Display all service endpoints and credentials

### Manual Installation

#### Step 1: Create Windows Directory Structure

```powershell
# Create directories
$basePath = "C:\simulator-data"
New-Item -ItemType Directory -Force -Path "$basePath\input"
New-Item -ItemType Directory -Force -Path "$basePath\output"
New-Item -ItemType Directory -Force -Path "$basePath\temp"

# Grant permissions
icacls $basePath /grant Everyone:F /T
```

#### Step 2: Create Minikube Cluster with Hyper-V

```powershell
# Delete existing cluster if present
minikube delete -p file-simulator

# Create new cluster with Hyper-V driver
minikube start `
    --profile file-simulator `
    --driver=hyperv `
    --memory=4096 `
    --cpus=2 `
    --disk-size=20g `
    --mount `
    --mount-string="C:\simulator-data:/mnt/simulator-data"

# Set as default profile
minikube profile file-simulator
```

#### Step 3: Deploy Helm Chart

```powershell
# Navigate to project directory
cd C:\Users\UserC\source\repos\file-simulator-suite

# Deploy the simulator
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --namespace file-simulator `
    --create-namespace

# Wait for pods to be ready
kubectl wait --for=condition=ready pod -l app.kubernetes.io/part-of=file-simulator-suite `
    -n file-simulator --timeout=300s
```

#### Step 4: Start Minikube Tunnel (Required for SMB)

```powershell
# Run in a separate Administrator terminal (keep it running)
minikube tunnel -p file-simulator
```

#### Step 5: Get Service Endpoints

```powershell
# Get Minikube IP
$MINIKUBE_IP = minikube ip -p file-simulator

# Display endpoints
Write-Host "Service Endpoints:"
Write-Host "  Management UI: http://$MINIKUBE_IP`:30180"
Write-Host "  FTP:           ftp://$MINIKUBE_IP`:30021"
Write-Host "  SFTP:          sftp://$MINIKUBE_IP`:30022"
Write-Host "  HTTP:          http://$MINIKUBE_IP`:30088"
Write-Host "  S3 API:        http://$MINIKUBE_IP`:30900"
Write-Host "  S3 Console:    http://$MINIKUBE_IP`:30901"
Write-Host "  SMB:           \\$MINIKUBE_IP\simulator"
```

---

## Service Endpoints

### NodePort Services

| Service | Port | URL Format | Description |
|---------|------|------------|-------------|
| Management UI | 30180 | `http://<IP>:30180` | FileBrowser web interface |
| FTP | 30021 | `ftp://<IP>:30021` | Classic FTP server |
| SFTP | 30022 | `sftp://<IP>:30022` | Secure file transfer |
| HTTP | 30088 | `http://<IP>:30088` | HTTP file server |
| WebDAV | 30089 | `http://<IP>:30089` | WebDAV server |
| S3 API | 30900 | `http://<IP>:30900` | MinIO S3 API |
| S3 Console | 30901 | `http://<IP>:30901` | MinIO web console |
| NFS | 32149 | `<IP>:32149` | NFS export |

### LoadBalancer Services (Requires minikube tunnel)

| Service | Port | URL Format | Description |
|---------|------|------------|-------------|
| SMB | 445 | `\\<IP>\simulator` | Windows file sharing |

### Internal Kubernetes DNS Names

Use these URLs from pods running inside the cluster:

```
FTP:  ftp://file-sim-file-simulator-ftp.file-simulator.svc.cluster.local:21
SFTP: sftp://file-sim-file-simulator-sftp.file-simulator.svc.cluster.local:22
HTTP: http://file-sim-file-simulator-http.file-simulator.svc.cluster.local:80
S3:   http://file-sim-file-simulator-s3.file-simulator.svc.cluster.local:9000
SMB:  smb://file-sim-file-simulator-smb.file-simulator.svc.cluster.local:445
NFS:  file-sim-file-simulator-nas.file-simulator.svc.cluster.local:2049
```

---

## Configuration

### Default Credentials

| Service | Username | Password |
|---------|----------|----------|
| Management UI | admin | admin123 |
| FTP | ftpuser | ftppass123 |
| SFTP | sftpuser | sftppass123 |
| HTTP/WebDAV | httpuser | httppass123 |
| S3 (MinIO) | minioadmin | minioadmin123 |
| SMB | smbuser | smbpass123 |

### Helm Values Customization

Create a custom values file `custom-values.yaml`:

```yaml
# Disable unused services
nas:
  enabled: false

# Change credentials
s3:
  auth:
    rootUser: myAccessKey
    rootPassword: mySuperSecretKey

ftp:
  auth:
    username: myftpuser
    password: mysecurepassword

# Adjust resources
management:
  resources:
    limits:
      memory: "512Mi"
      cpu: "500m"
```

Deploy with custom values:

```powershell
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --namespace file-simulator `
    --create-namespace `
    -f custom-values.yaml
```

---

## .NET Client Library

### Project Setup

Add required NuGet packages to your project:

```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.305" />
<PackageReference Include="FluentFTP" Version="50.0.1" />
<PackageReference Include="SSH.NET" Version="2024.1.0" />
<PackageReference Include="SMBLibrary" Version="1.5.2" />
```

### Configuration (appsettings.json)

Configure the client to connect to the simulator:

```json
{
  "FileSimulator": {
    "Ftp": {
      "Host": "172.23.17.71",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123",
      "BasePath": "/output"
    },
    "Sftp": {
      "Host": "172.23.17.71",
      "Port": 30022,
      "Username": "sftpuser",
      "Password": "sftppass123",
      "BasePath": "/data/output"
    },
    "Http": {
      "BaseUrl": "http://172.23.17.71:30088",
      "BasePath": "/output"
    },
    "WebDav": {
      "BaseUrl": "http://172.23.17.71:30089",
      "Username": "httpuser",
      "Password": "httppass123",
      "BasePath": "/output"
    },
    "S3": {
      "ServiceUrl": "http://172.23.17.71:30900",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin123",
      "BucketName": "simulator",
      "BasePath": "output"
    },
    "Smb": {
      "Host": "172.23.17.71",
      "Port": 445,
      "ShareName": "simulator",
      "Username": "smbuser",
      "Password": "smbpass123",
      "BasePath": "output"
    },
    "Nfs": {
      "Host": "172.23.17.71",
      "Port": 32149,
      "MountPath": "/mnt/nfs",
      "BasePath": "output"
    }
  },
  "TestSettings": {
    "TestFileSizeBytes": 1024,
    "TimeoutSeconds": 30
  }
}
```

### Service Registration

Register the file services in your `Program.cs`:

```csharp
using FileSimulator.Client.Services;

var builder = WebApplication.CreateBuilder(args);

// Register configuration
builder.Services.Configure<FtpOptions>(builder.Configuration.GetSection("FileSimulator:Ftp"));
builder.Services.Configure<SftpOptions>(builder.Configuration.GetSection("FileSimulator:Sftp"));
builder.Services.Configure<S3Options>(builder.Configuration.GetSection("FileSimulator:S3"));
builder.Services.Configure<SmbOptions>(builder.Configuration.GetSection("FileSimulator:Smb"));

// Register services
builder.Services.AddSingleton<IFtpFileService, FtpFileService>();
builder.Services.AddSingleton<ISftpFileService, SftpFileService>();
builder.Services.AddSingleton<IS3FileService, S3FileService>();
builder.Services.AddSingleton<ISmbFileService, SmbFileService>();

var app = builder.Build();
app.Run();
```

### FTP Client Example

```csharp
using FluentFTP;
using Microsoft.Extensions.Options;

public class FtpFileService : IFtpFileService, IDisposable
{
    private readonly FtpOptions _options;
    private readonly ILogger<FtpFileService> _logger;
    private AsyncFtpClient? _client;

    public FtpFileService(IOptions<FtpOptions> options, ILogger<FtpFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AsyncFtpClient> GetClientAsync(CancellationToken ct = default)
    {
        if (_client == null || !_client.IsConnected)
        {
            _client?.Dispose();
            _client = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);
            await _client.AutoConnect(ct);
            _logger.LogInformation("Connected to FTP server {Host}:{Port}", _options.Host, _options.Port);
        }
        return _client;
    }

    public async Task<bool> UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var fullPath = $"{_options.BasePath.TrimEnd('/')}/{remotePath.TrimStart('/')}";

        var status = await client.UploadFile(localPath, fullPath, FtpRemoteExists.Overwrite, true, token: ct);
        _logger.LogInformation("Uploaded {LocalPath} to FTP:{RemotePath} - Status: {Status}", localPath, fullPath, status);

        return status == FtpStatus.Success;
    }

    public async Task<byte[]> DownloadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var fullPath = $"{_options.BasePath.TrimEnd('/')}/{remotePath.TrimStart('/')}";

        using var ms = new MemoryStream();
        await client.DownloadStream(ms, fullPath, token: ct);
        return ms.ToArray();
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string path = "", string pattern = "*", CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var fullPath = $"{_options.BasePath.TrimEnd('/')}/{path.TrimStart('/')}";

        var items = await client.GetListing(fullPath, token: ct);
        return items
            .Where(i => i.Type == FtpObjectType.File)
            .Where(i => MatchesPattern(i.Name, pattern))
            .Select(i => i.FullName);
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose() => _client?.Dispose();
}
```

### S3 (MinIO) Client Example

```csharp
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

public class S3FileService : IS3FileService
{
    private readonly S3Options _options;
    private readonly ILogger<S3FileService> _logger;
    private readonly AmazonS3Client _client;

    public S3FileService(IOptions<S3Options> options, ILogger<S3FileService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = true,  // Required for MinIO
            UseHttp = _options.ServiceUrl.StartsWith("http://")
        };

        _client = new AmazonS3Client(_options.AccessKey, _options.SecretKey, config);
    }

    public async Task<bool> UploadFileAsync(string localPath, string key, CancellationToken ct = default)
    {
        var fullKey = $"{_options.BasePath.TrimEnd('/')}/{key.TrimStart('/')}";

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = fullKey,
            FilePath = localPath
        };

        var response = await _client.PutObjectAsync(request, ct);
        _logger.LogInformation("Uploaded {LocalPath} to S3:{Bucket}/{Key}", localPath, _options.BucketName, fullKey);

        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }

    public async Task<byte[]> DownloadFileAsync(string key, CancellationToken ct = default)
    {
        var fullKey = $"{_options.BasePath.TrimEnd('/')}/{key.TrimStart('/')}";

        var request = new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = fullKey
        };

        using var response = await _client.GetObjectAsync(request, ct);
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string prefix = "", CancellationToken ct = default)
    {
        var fullPrefix = $"{_options.BasePath.TrimEnd('/')}/{prefix.TrimStart('/')}";

        var request = new ListObjectsV2Request
        {
            BucketName = _options.BucketName,
            Prefix = fullPrefix
        };

        var response = await _client.ListObjectsV2Async(request, ct);
        return response.S3Objects.Select(o => o.Key);
    }

    public async Task<bool> DeleteFileAsync(string key, CancellationToken ct = default)
    {
        var fullKey = $"{_options.BasePath.TrimEnd('/')}/{key.TrimStart('/')}";

        var request = new DeleteObjectRequest
        {
            BucketName = _options.BucketName,
            Key = fullKey
        };

        var response = await _client.DeleteObjectAsync(request, ct);
        return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
    }
}
```

### SMB Client Example

```csharp
using SMBLibrary;
using SMBLibrary.Client;
using Microsoft.Extensions.Options;

public class SmbFileService : ISmbFileService, IDisposable
{
    private readonly SmbOptions _options;
    private readonly ILogger<SmbFileService> _logger;
    private SMB2Client? _client;
    private ISMBFileStore? _fileStore;

    public SmbFileService(IOptions<SmbOptions> options, ILogger<SmbFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ISMBFileStore> GetFileStoreAsync(CancellationToken ct = default)
    {
        if (_client == null || !_client.IsConnected)
        {
            _client?.Disconnect();
            _client = new SMB2Client();

            var connected = _client.Connect(
                System.Net.IPAddress.Parse(_options.Host),
                SMBTransportType.DirectTCPTransport);

            if (!connected)
                throw new Exception($"Failed to connect to SMB server {_options.Host}");

            var status = _client.Login(string.Empty, _options.Username, _options.Password);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception($"Failed to login to SMB server: {status}");

            _fileStore = _client.TreeConnect(_options.ShareName, out status);
            if (status != NTStatus.STATUS_SUCCESS)
                throw new Exception($"Failed to connect to share {_options.ShareName}: {status}");

            _logger.LogInformation("Connected to SMB share \\\\{Host}\\{Share}", _options.Host, _options.ShareName);
        }
        return _fileStore!;
    }

    public async Task<bool> UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var fileStore = await GetFileStoreAsync(ct);
        var fullPath = $"{_options.BasePath.TrimEnd('\\')}\\{remotePath.TrimStart('\\')}";

        var status = fileStore.CreateFile(
            out object fileHandle,
            out FileStatus fileStatus,
            fullPath,
            AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
            FileAttributes.Normal,
            ShareAccess.None,
            CreateDisposition.FILE_OVERWRITE_IF,
            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
            null);

        if (status != NTStatus.STATUS_SUCCESS)
        {
            _logger.LogError("Failed to create SMB file {Path}: {Status}", fullPath, status);
            return false;
        }

        try
        {
            var content = await File.ReadAllBytesAsync(localPath, ct);
            status = fileStore.WriteFile(out int bytesWritten, fileHandle, 0, content);
            _logger.LogInformation("Uploaded {LocalPath} to SMB:{RemotePath} ({Bytes} bytes)", localPath, fullPath, bytesWritten);
            return status == NTStatus.STATUS_SUCCESS;
        }
        finally
        {
            fileStore.CloseFile(fileHandle);
        }
    }

    public async Task<byte[]> DownloadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var fileStore = await GetFileStoreAsync(ct);
        var fullPath = $"{_options.BasePath.TrimEnd('\\')}\\{remotePath.TrimStart('\\')}";

        var status = fileStore.CreateFile(
            out object fileHandle,
            out FileStatus fileStatus,
            fullPath,
            AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE,
            FileAttributes.Normal,
            ShareAccess.Read,
            CreateDisposition.FILE_OPEN,
            CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT,
            null);

        if (status != NTStatus.STATUS_SUCCESS)
            throw new FileNotFoundException($"SMB file not found: {fullPath}", fullPath);

        try
        {
            using var ms = new MemoryStream();
            long offset = 0;
            const int maxReadSize = 65536;

            while (true)
            {
                status = fileStore.ReadFile(out byte[] data, fileHandle, offset, maxReadSize);
                if (status != NTStatus.STATUS_SUCCESS && status != NTStatus.STATUS_END_OF_FILE)
                    throw new Exception($"Failed to read SMB file: {status}");

                if (data == null || data.Length == 0)
                    break;

                ms.Write(data, 0, data.Length);
                offset += data.Length;

                if (data.Length < maxReadSize)
                    break;
            }

            return ms.ToArray();
        }
        finally
        {
            fileStore.CloseFile(fileHandle);
        }
    }

    public void Dispose()
    {
        _fileStore?.Disconnect();
        _client?.Disconnect();
    }
}
```

### SFTP Client Example

```csharp
using Renci.SshNet;
using Microsoft.Extensions.Options;

public class SftpFileService : ISftpFileService, IDisposable
{
    private readonly SftpOptions _options;
    private readonly ILogger<SftpFileService> _logger;
    private SftpClient? _client;

    public SftpFileService(IOptions<SftpOptions> options, ILogger<SftpFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public SftpClient GetClient()
    {
        if (_client == null || !_client.IsConnected)
        {
            _client?.Dispose();
            _client = new SftpClient(_options.Host, _options.Port, _options.Username, _options.Password);
            _client.Connect();
            _logger.LogInformation("Connected to SFTP server {Host}:{Port}", _options.Host, _options.Port);
        }
        return _client;
    }

    public async Task<bool> UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var client = GetClient();
        var fullPath = $"{_options.BasePath.TrimEnd('/')}/{remotePath.TrimStart('/')}";

        await using var fileStream = File.OpenRead(localPath);
        await Task.Run(() => client.UploadFile(fileStream, fullPath, true), ct);

        _logger.LogInformation("Uploaded {LocalPath} to SFTP:{RemotePath}", localPath, fullPath);
        return true;
    }

    public async Task<byte[]> DownloadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var client = GetClient();
        var fullPath = $"{_options.BasePath.TrimEnd('/')}/{remotePath.TrimStart('/')}";

        using var ms = new MemoryStream();
        await Task.Run(() => client.DownloadFile(fullPath, ms), ct);
        return ms.ToArray();
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string path = "", string pattern = "*", CancellationToken ct = default)
    {
        var client = GetClient();
        var fullPath = $"{_options.BasePath.TrimEnd('/')}/{path.TrimStart('/')}";

        var files = await Task.Run(() => client.ListDirectory(fullPath), ct);
        return files
            .Where(f => !f.IsDirectory)
            .Where(f => MatchesPattern(f.Name, pattern))
            .Select(f => f.FullName);
    }

    private static bool MatchesPattern(string name, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    public void Dispose() => _client?.Dispose();
}
```

### NFS Client Example

NFS requires the share to be mounted locally. The client works with the mounted filesystem.

```csharp
using Microsoft.Extensions.Options;

public class NfsFileService : INfsFileService
{
    private readonly NfsOptions _options;
    private readonly ILogger<NfsFileService> _logger;

    public NfsFileService(IOptions<NfsOptions> options, ILogger<NfsFileService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> UploadFileAsync(string localPath, string remotePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, _options.BasePath.TrimStart('/'), remotePath.TrimStart('/'));
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await Task.Run(() => File.Copy(localPath, fullPath, overwrite: true), ct);
        _logger.LogInformation("Uploaded {LocalPath} to NFS:{RemotePath}", localPath, fullPath);
        return true;
    }

    public async Task<byte[]> DownloadFileAsync(string remotePath, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, _options.BasePath.TrimStart('/'), remotePath.TrimStart('/'));
        return await File.ReadAllBytesAsync(fullPath, ct);
    }

    public Task<IEnumerable<string>> ListFilesAsync(string path = "", string pattern = "*", CancellationToken ct = default)
    {
        var fullPath = Path.Combine(_options.MountPath, _options.BasePath.TrimStart('/'), path.TrimStart('/'));

        if (!Directory.Exists(fullPath))
        {
            return Task.FromResult(Enumerable.Empty<string>());
        }

        var files = Directory.GetFiles(fullPath, pattern).Select(f => Path.GetFileName(f));
        return Task.FromResult(files);
    }

    public bool IsMountAvailable()
    {
        return Directory.Exists(_options.MountPath);
    }
}

public class NfsOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 2049;
    public string MountPath { get; set; } = "/mnt/nfs";
    public string BasePath { get; set; } = "output";
}
```

**Mounting NFS on Windows (via WSL):**

```powershell
# In WSL2
sudo apt install nfs-common
sudo mkdir -p /mnt/nfs
sudo mount -t nfs 172.23.17.71:/data /mnt/nfs
```

**Mounting NFS on Linux:**

```bash
sudo apt install nfs-common
sudo mkdir -p /mnt/nfs
sudo mount -t nfs 172.23.17.71:/data /mnt/nfs
```

### Console Test Application

The project includes a test console application at `src/FileSimulator.TestConsole/`:

```powershell
# Run the test console
cd src/FileSimulator.TestConsole
dotnet run

# Or with specific configuration
dotnet run -- --protocol ftp --action upload --file test.txt
```

---

## Cross-Cluster Configuration

### Scenario: Microservices in Different Minikube Cluster

When your microservices run in a different Minikube cluster (e.g., Docker driver) than the simulator (Hyper-V driver), configure them to use the external IP:

#### Step 1: Get Simulator IP

```powershell
# Get the Hyper-V Minikube IP
$SIMULATOR_IP = minikube ip -p file-simulator
Write-Host "Simulator IP: $SIMULATOR_IP"
# Example output: 172.23.17.71
```

#### Step 2: Configure Microservice appsettings

Update your microservice's `appsettings.json` or environment variables:

```json
{
  "FileSimulator": {
    "Ftp": {
      "Host": "172.23.17.71",
      "Port": 30021
    },
    "Sftp": {
      "Host": "172.23.17.71",
      "Port": 30022
    },
    "S3": {
      "ServiceUrl": "http://172.23.17.71:30900"
    },
    "Smb": {
      "Host": "172.23.17.71",
      "Port": 445
    },
    "Nfs": {
      "Host": "172.23.17.71",
      "Port": 32149,
      "MountPath": "/mnt/nfs"
    }
  }
}
```

#### Step 3: Deploy ConfigMap in Microservice Cluster

```yaml
# file-simulator-config.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: file-simulator-config
  namespace: your-namespace
data:
  FILE_FTP_HOST: "172.23.17.71"
  FILE_FTP_PORT: "30021"
  FILE_SFTP_HOST: "172.23.17.71"
  FILE_SFTP_PORT: "30022"
  FILE_HTTP_URL: "http://172.23.17.71:30088"
  FILE_S3_ENDPOINT: "http://172.23.17.71:30900"
  FILE_S3_ACCESS_KEY: "minioadmin"
  FILE_SMB_HOST: "172.23.17.71"
  FILE_SMB_PORT: "445"
  FILE_NFS_HOST: "172.23.17.71"
  FILE_NFS_PORT: "32149"
  FILE_NFS_MOUNT_PATH: "/mnt/nfs"
---
apiVersion: v1
kind: Secret
metadata:
  name: file-simulator-secrets
  namespace: your-namespace
type: Opaque
stringData:
  FILE_FTP_PASSWORD: "ftppass123"
  FILE_SFTP_PASSWORD: "sftppass123"
  FILE_S3_SECRET_KEY: "minioadmin123"
  FILE_SMB_PASSWORD: "smbpass123"
```

#### Step 4: Use in Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: your-microservice
spec:
  template:
    spec:
      containers:
        - name: app
          image: your-image
          envFrom:
            - configMapRef:
                name: file-simulator-config
            - secretRef:
                name: file-simulator-secrets
```

### Network Considerations

For cross-cluster communication:

1. **Both clusters must be on the same network** (default with Hyper-V and Docker Desktop)
2. **Firewall rules** may need to allow traffic on NodePorts (30000-32767)
3. **SMB (port 445)** requires `minikube tunnel` running on the simulator cluster

Test connectivity from microservice cluster:

```powershell
# From a pod in the microservice cluster
kubectl run test-pod --rm -it --image=alpine -- sh
# Inside pod:
apk add curl
curl http://172.23.17.71:30088/health
curl http://172.23.17.71:30900/minio/health/live
```

---

## Testing

### Run Test Scripts

```powershell
# PowerShell test script
.\scripts\test-simulator.ps1

# With specific IP
.\scripts\test-simulator.ps1 -MinikubeIp 172.23.17.71
```

### Manual Protocol Tests

#### FTP

```powershell
$ip = minikube ip -p file-simulator
# Using Windows FTP client
ftp
open $ip 30021
# Login: ftpuser / ftppass123
```

#### SFTP

```powershell
# Using OpenSSH (if installed)
sftp -P 30022 sftpuser@172.23.17.71
# Password: sftppass123

# Or using WinSCP command line
winscp.com /command "open sftp://sftpuser:sftppass123@172.23.17.71:30022/" "ls" "exit"
```

#### S3 (MinIO)

```powershell
# Using AWS CLI
aws configure set aws_access_key_id minioadmin
aws configure set aws_secret_access_key minioadmin123
aws --endpoint-url http://172.23.17.71:30900 s3 ls
aws --endpoint-url http://172.23.17.71:30900 s3 mb s3://test-bucket
aws --endpoint-url http://172.23.17.71:30900 s3 cp test.txt s3://test-bucket/
```

#### SMB

```powershell
# Map network drive (requires minikube tunnel running)
net use Z: \\172.23.17.71\simulator /user:smbuser smbpass123

# Test access
dir Z:\
echo "test" > Z:\output\test.txt

# Disconnect
net use Z: /delete
```

#### HTTP/WebDAV

```powershell
# List files
Invoke-RestMethod -Uri "http://172.23.17.71:30088/"

# Upload via WebDAV
$cred = Get-Credential  # httpuser / httppass123
Invoke-WebRequest -Uri "http://172.23.17.71:30089/output/test.txt" `
    -Method PUT `
    -InFile "test.txt" `
    -Credential $cred
```

---

## Troubleshooting

### SMB Connection Issues

**Problem**: Cannot connect to `\\IP\simulator`

**Solutions**:
1. Ensure `minikube tunnel` is running in Administrator terminal
2. Check if LoadBalancer has external IP:
   ```powershell
   kubectl get svc -n file-simulator | findstr smb
   # Should show EXTERNAL-IP, not <pending>
   ```
3. Verify port 445 is not blocked by firewall
4. Restart the tunnel if IP changed:
   ```powershell
   # Stop tunnel (Ctrl+C) and restart
   minikube tunnel -p file-simulator
   ```

### Minikube Mount Not Working

**Problem**: Files not visible in pods

**Solutions**:
1. Restart Minikube with mount:
   ```powershell
   minikube stop -p file-simulator
   minikube start -p file-simulator --mount --mount-string="C:\simulator-data:/mnt/simulator-data"
   ```
2. Check mount status:
   ```powershell
   minikube ssh -p file-simulator -- ls -la /mnt/simulator-data
   ```

### Pods Not Starting

**Problem**: Pods stuck in `Pending` or `CrashLoopBackOff`

**Solutions**:
1. Check pod logs:
   ```powershell
   kubectl logs -n file-simulator -l app.kubernetes.io/component=ftp
   kubectl logs -n file-simulator -l app.kubernetes.io/component=smb
   ```
2. Check events:
   ```powershell
   kubectl get events -n file-simulator --sort-by='.lastTimestamp'
   ```
3. Check resource availability:
   ```powershell
   kubectl describe nodes
   ```

### Connection Refused from Microservice Cluster

**Problem**: Cannot reach simulator from different cluster

**Solutions**:
1. Verify network connectivity:
   ```powershell
   # Get IPs of both clusters
   minikube ip -p file-simulator      # e.g., 172.23.17.71
   minikube ip -p your-other-cluster  # e.g., 192.168.49.2

   # Test ping from other cluster
   kubectl run test --rm -it --image=alpine -n default -- ping -c 3 172.23.17.71
   ```
2. Check firewall rules allow cross-network traffic
3. Ensure both Minikube instances use compatible network modes

---

## Project Structure

```
file-simulator-suite/
|-- helm-chart/
|   +-- file-simulator/
|       |-- Chart.yaml                 # Helm chart metadata
|       |-- values.yaml                # Default configuration
|       |-- values-multi-instance.yaml # Multi-server configuration
|       +-- templates/
|           |-- _helpers.tpl           # Template helpers
|           |-- namespace.yaml         # Namespace definition
|           |-- storage.yaml           # PV and PVC
|           |-- serviceaccount.yaml    # Service account
|           |-- management.yaml        # FileBrowser deployment
|           |-- ftp.yaml               # FTP server
|           |-- sftp.yaml              # SFTP server
|           |-- http.yaml              # HTTP/nginx server
|           |-- webdav.yaml            # WebDAV server
|           |-- s3.yaml                # MinIO S3
|           |-- smb.yaml               # Samba SMB
|           +-- nas.yaml               # NFS server
|
|-- src/
|   |-- FileSimulator.Client/          # .NET client library
|   |   |-- FileSimulator.Client.csproj
|   |   |-- Services/
|   |   |   |-- FtpFileService.cs
|   |   |   |-- SftpFileService.cs
|   |   |   |-- S3FileService.cs
|   |   |   |-- SmbFileService.cs
|   |   |   +-- HttpFileService.cs
|   |   +-- Options/
|   |       |-- FtpOptions.cs
|   |       |-- SftpOptions.cs
|   |       |-- S3Options.cs
|   |       +-- SmbOptions.cs
|   |
|   +-- FileSimulator.TestConsole/     # Test console application
|       |-- FileSimulator.TestConsole.csproj
|       |-- Program.cs
|       +-- appsettings.json
|
|-- scripts/
|   |-- Install-Simulator.ps1          # Automated installation
|   |-- setup-windows.ps1              # Windows directory setup
|   |-- test-simulator.ps1             # PowerShell test script
|   +-- test-simulator.sh              # Bash test script
|
|-- CLAUDE.md                          # Implementation guide
+-- README.md                          # This file
```

---

## License

MIT License - See LICENSE file for details.
