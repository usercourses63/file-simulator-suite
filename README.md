# File Simulator Suite

A comprehensive file access simulator for Kubernetes development and testing. This suite provides multiple file transfer protocols (FTP, SFTP, HTTP/WebDAV, S3/MinIO, SMB, NFS) in a unified deployment, enabling seamless testing between Windows development environments and Kubernetes/OpenShift clusters.

## Table of Contents

- [Purpose](#-purpose)
- [Architecture](#-architecture)
- [Prerequisites](#-prerequisites)
- [Installation](#-installation)
  - [Automated Installation](#automated-installation)
  - [Manual Installation](#manual-installation)
- [Multi-Profile Setup (IMPORTANT)](#-multi-profile-setup-important)
- [Service Endpoints](#-service-endpoints)
- [Configuration](#-configuration)
- [.NET Client Library](#-net-client-library)
- [Cross-Cluster Configuration](#-cross-cluster-configuration)
- [Testing](#-testing)
- [Troubleshooting](#-troubleshooting)
  - [NFS Server Fix (Critical)](#nfs-server-fix-critical)
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
# VERIFIED CONFIGURATION: 8GB RAM, 4 CPUs for stable operation of all 8 protocols
minikube start `
    --profile file-simulator `
    --driver=hyperv `
    --memory=8192 `
    --cpus=4 `
    --disk-size=20g `
    --mount `
    --mount-string="C:\simulator-data:/mnt/simulator-data"

# DO NOT set as default profile if you have other clusters
# Use --context flag instead (see Multi-Profile Setup section)
```

#### Step 3: Deploy Helm Chart

```powershell
# Navigate to project directory
cd C:\Users\UserC\source\repos\file-simulator-suite

# Deploy the simulator
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --namespace file-simulator `
    --create-namespace

# Wait for pods to be ready (7 out of 8 will start immediately)
kubectl --context=file-simulator get pods -n file-simulator

# NFS will be in CrashLoopBackOff - this is expected and will be fixed in next step
```

#### Step 3.1: Apply NFS Server Fix (REQUIRED)

The NFS server cannot export Windows-mounted hostPath volumes. Apply this fix:

```powershell
# Create the NFS fix patch file
@"
spec:
  template:
    spec:
      containers:
      - name: nfs-server
        volumeMounts:
        - name: nfs-data
          mountPath: /data
        - name: shared-data
          mountPath: /shared
      volumes:
      - name: nfs-data
        emptyDir: {}
      - name: shared-data
        persistentVolumeClaim:
          claimName: file-sim-file-simulator-pvc
"@ | Out-File -FilePath nfs-fix-patch.yaml -Encoding UTF8

# Apply the patch
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator `
    --patch-file nfs-fix-patch.yaml

# Verify NFS is now running
Start-Sleep -Seconds 20
kubectl --context=file-simulator get pods -n file-simulator | Select-String nas
```

#### Step 4: Start Minikube Tunnel (Required for SMB)

```powershell
# Run in a separate Administrator terminal (keep it running)
minikube tunnel -p file-simulator
```

#### Step 5: Configure Stable Hostname (RECOMMENDED)

```powershell
# Run as Administrator to add hosts file entry
.\configure-stable-hostname.ps1

# This creates a DNS entry: file-simulator.local → current cluster IP
# Allows using hostname instead of IP in configurations
```

#### Step 6: Verify All 8 Protocols

```powershell
# Check all pods are running
kubectl --context=file-simulator get pods -n file-simulator

# All 8 pods should show 1/1 READY status:
# - file-sim-file-simulator-ftp
# - file-sim-file-simulator-sftp
# - file-sim-file-simulator-http
# - file-sim-file-simulator-webdav
# - file-sim-file-simulator-s3
# - file-sim-file-simulator-smb
# - file-sim-file-simulator-nas (NFS)
# - file-sim-file-simulator-management

# Get cluster IP or use hostname
$MINIKUBE_IP = minikube ip -p file-simulator
# Or use stable hostname: $HOSTNAME = "file-simulator.local"

# Display endpoints
Write-Host "Service Endpoints (via hostname):"
Write-Host "  Management UI: http://file-simulator.local:30180"
Write-Host "  FTP:           ftp://file-simulator.local:30021"
Write-Host "  SFTP:          sftp://file-simulator.local:30022"
Write-Host "  HTTP:          http://file-simulator.local:30088"
Write-Host "  WebDAV:        http://file-simulator.local:30089"
Write-Host "  S3 API:        http://file-simulator.local:30900"
Write-Host "  S3 Console:    http://file-simulator.local:30901"
Write-Host "  NFS:           file-simulator.local:32149"
Write-Host "  SMB:           \\<LoadBalancer-IP>\simulator"
Write-Host "`nOr via IP: $MINIKUBE_IP (use hostname for stability)"
```

---

## Multi-Profile Setup (IMPORTANT)

### Why Use Separate Minikube Profiles?

If you're running multiple Kubernetes environments (e.g., ez-platform on Docker driver + file-simulator on Hyper-V), using separate profiles prevents accidental operations on the wrong cluster.

**CRITICAL SAFETY RULE:** Always use `--context` flag with kubectl commands to explicitly target the correct cluster.

### Profile Architecture (Recommended Setup)

```
Host Machine
├── Minikube Profile: "minikube" (Docker driver)
│   └── Namespace: ez-platform
│       └── Your production/test services
│
└── Minikube Profile: "file-simulator" (Hyper-V driver)
    └── Namespace: file-simulator
        └── 8 file transfer protocol services
```

**Key Benefits:**
- ✅ Complete isolation between environments
- ✅ Different drivers (Docker vs Hyper-V)
- ✅ No resource competition
- ✅ Independent scaling and configuration

### Safe kubectl Operations

**❌ DANGEROUS - DO NOT DO THIS:**
```powershell
# Switching contexts is error-prone
kubectl config use-context file-simulator
kubectl delete pod <name> -n file-simulator

kubectl config use-context minikube
kubectl delete pod <name> -n ez-platform

# Problem: Easy to forget which context is active!
```

**✅ SAFE - ALWAYS USE --context FLAG:**
```powershell
# Explicitly specify context in every command
kubectl --context=file-simulator get pods -n file-simulator
kubectl --context=file-simulator logs <pod-name> -n file-simulator
kubectl --context=file-simulator delete pod <name> -n file-simulator

kubectl --context=minikube get pods -n ez-platform
kubectl --context=minikube logs <pod-name> -n ez-platform
kubectl --context=minikube delete pod <name> -n ez-platform
```

### PowerShell Helper Functions (Optional)

Add to your PowerShell profile for convenience:

```powershell
# Edit profile: notepad $PROFILE

# File Simulator shortcuts
function k-fs { kubectl --context=file-simulator --namespace=file-simulator $args }
function helm-fs { helm --kube-context=file-simulator $args }

# ez-platform shortcuts
function k-ez { kubectl --context=minikube --namespace=ez-platform $args }
function helm-ez { helm --kube-context=minikube $args }

# Usage examples:
# k-fs get pods
# k-fs logs <pod-name>
# k-ez get pods
# helm-fs upgrade file-sim ./helm-chart/file-simulator
```

### Verify Current Context

```powershell
# Check which contexts exist
kubectl config get-contexts

# Output shows:
# CURRENT   NAME             CLUSTER
# *         file-simulator   file-simulator
#           minikube         minikube

# The * indicates active context, but IGNORE THIS - always use --context flag!
```

### Profile Management Commands

```powershell
# List all Minikube profiles
minikube profile list

# Get IP of specific profile
minikube ip -p file-simulator   # Hyper-V cluster
minikube ip -p minikube          # Docker cluster

# Stop specific profile
minikube stop -p file-simulator
minikube stop -p minikube

# Start specific profile
minikube start -p file-simulator
minikube start -p minikube

# Delete specific profile (CAREFUL!)
minikube delete -p file-simulator  # Only deletes file-simulator
```

### Helm Operations with Multiple Profiles

```powershell
# Deploy to file-simulator cluster
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator `
    --create-namespace

# List releases in specific cluster
helm list --kube-context=file-simulator -A
helm list --kube-context=minikube -A

# Uninstall from specific cluster
helm uninstall file-sim --kube-context=file-simulator -n file-simulator
```

### Best Practices Summary

1. **Never use `kubectl config use-context`** - it causes accidental deletions
2. **Always include `--context=<profile-name>` flag** in kubectl commands
3. **Always include `--kube-context=<profile-name>` flag** in helm commands
4. **Use PowerShell functions** to enforce context safety
5. **Keep one namespace per profile** for clarity
6. **Label terminal windows** with their target profile

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

When your microservices run in a different Minikube cluster (e.g., `minikube` profile with Docker driver) than the simulator (`file-simulator` profile with Hyper-V driver), configure them to use the NodePort endpoints:

#### Step 1: Get Simulator IP and Verify Services

```powershell
# Get the Hyper-V file-simulator cluster IP
$SIMULATOR_IP = minikube ip -p file-simulator
Write-Host "Simulator IP: $SIMULATOR_IP"
# Example output: 172.25.201.3

# Verify all services are running
kubectl --context=file-simulator get svc -n file-simulator

# Verify all 8 pods are healthy
kubectl --context=file-simulator get pods -n file-simulator
# All should show STATUS=Running, READY=1/1
```

#### Step 2: Configure Microservice appsettings

Update your microservice's `appsettings.json` with the actual Simulator IP (get it from `minikube ip -p file-simulator`):

```json
{
  "FileSimulator": {
    "Ftp": {
      "Host": "172.25.201.3",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123"
    },
    "Sftp": {
      "Host": "172.25.201.3",
      "Port": 30022,
      "Username": "sftpuser",
      "Password": "sftppass123"
    },
    "Http": {
      "BaseUrl": "http://172.25.201.3:30088"
    },
    "WebDav": {
      "BaseUrl": "http://172.25.201.3:30089",
      "Username": "httpuser",
      "Password": "httppass123"
    },
    "S3": {
      "ServiceUrl": "http://172.25.201.3:30900",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin123"
    },
    "Smb": {
      "Host": "172.25.201.3",
      "Port": 445,
      "ShareName": "simulator",
      "Username": "smbuser",
      "Password": "smbpass123"
    },
    "Nfs": {
      "Host": "172.25.201.3",
      "Port": 32149,
      "MountPath": "/mnt/nfs"
    }
  }
}
```

#### Step 3: Deploy ConfigMap in Microservice Cluster

**Important**: Deploy this to your OTHER cluster (e.g., minikube profile with Docker driver):

```powershell
# First, get the file-simulator IP
$SIMULATOR_IP = minikube ip -p file-simulator
Write-Host "Using Simulator IP: $SIMULATOR_IP"

# Create ConfigMap (replace IP in the YAML below with your $SIMULATOR_IP)
kubectl --context=minikube apply -f - <<EOF
apiVersion: v1
kind: ConfigMap
metadata:
  name: file-simulator-config
  namespace: ez-platform
data:
  FILE_FTP_HOST: "$SIMULATOR_IP"
  FILE_FTP_PORT: "30021"
  FILE_SFTP_HOST: "$SIMULATOR_IP"
  FILE_SFTP_PORT: "30022"
  FILE_HTTP_URL: "http://$SIMULATOR_IP:30088"
  FILE_WEBDAV_URL: "http://$SIMULATOR_IP:30089"
  FILE_S3_ENDPOINT: "http://$SIMULATOR_IP:30900"
  FILE_S3_ACCESS_KEY: "minioadmin"
  FILE_SMB_HOST: "$SIMULATOR_IP"
  FILE_SMB_PORT: "445"
  FILE_NFS_HOST: "$SIMULATOR_IP"
  FILE_NFS_PORT: "32149"
---
apiVersion: v1
kind: Secret
metadata:
  name: file-simulator-secrets
  namespace: ez-platform
type: Opaque
stringData:
  FILE_FTP_USERNAME: "ftpuser"
  FILE_FTP_PASSWORD: "ftppass123"
  FILE_SFTP_USERNAME: "sftpuser"
  FILE_SFTP_PASSWORD: "sftppass123"
  FILE_HTTP_USERNAME: "httpuser"
  FILE_HTTP_PASSWORD: "httppass123"
  FILE_S3_SECRET_KEY: "minioadmin123"
  FILE_SMB_USERNAME: "smbuser"
  FILE_SMB_PASSWORD: "smbpass123"
EOF
```

#### Step 4: Use in Your Deployment

```yaml
# Deploy to ez-platform namespace in minikube cluster
apiVersion: apps/v1
kind: Deployment
metadata:
  name: your-microservice
  namespace: ez-platform
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

# Apply with:
# kubectl --context=minikube apply -f your-deployment.yaml
```

### NFS PersistentVolume for Cross-Cluster Access

To mount the file-simulator's NFS share in your application cluster (e.g., ez-platform in minikube profile):

```powershell
# Get file-simulator IP first
$SIMULATOR_IP = minikube ip -p file-simulator

# Apply to your application cluster (use correct context!)
kubectl --context=minikube apply -f - <<EOF
apiVersion: v1
kind: PersistentVolume
metadata:
  name: file-simulator-nfs-pv
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteMany
  storageClassName: file-simulator-nfs
  nfs:
    server: $SIMULATOR_IP    # File-simulator cluster IP (e.g., 172.25.201.3)
    path: /data              # NFS export path
  mountOptions:
    - nfsvers=4.2            # Use NFSv4.2 (single port)
    - port=32149             # NFS NodePort from file-simulator cluster
---
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: file-simulator-nfs-pvc
  namespace: ez-platform
spec:
  accessModes:
    - ReadWriteMany
  storageClassName: file-simulator-nfs
  resources:
    requests:
      storage: 10Gi
  volumeName: file-simulator-nfs-pv
EOF
```

**Important Notes:**
- The NFS path is `/data` (the actual export path)
- Use `--context=minikube` to deploy to your application cluster
- NFSv4.2 with port parameter requires kernel NFS client support
- Test connectivity before deploying PV/PVC

### Complete Examples

See [`examples/client-cluster/`](examples/client-cluster/) for complete, tested Kubernetes manifests including:
- ConfigMap with all protocol endpoints
- NFS PV/PVC configuration
- Example deployment templates
- Test console deployment

### Network Considerations

For cross-cluster communication between different Minikube profiles:

1. **Both clusters must be network-accessible** (Hyper-V and Docker drivers are typically on different networks)
2. **Firewall rules** may need to allow traffic on NodePorts (30000-32767)
3. **SMB (port 445)** requires `minikube tunnel` running on the file-simulator cluster
4. **Use NodePort endpoints** for external access, not internal DNS names

**Test connectivity from your application cluster:**

```powershell
# Get simulator IP
$SIMULATOR_IP = minikube ip -p file-simulator

# Test from microservice cluster (use correct context!)
kubectl --context=minikube run test-pod --rm -it --image=alpine -n ez-platform -- sh

# Inside pod, test connectivity:
apk add curl
curl http://172.25.201.3:30088/           # HTTP server
curl http://172.25.201.3:30901/           # S3 console
nc -zv 172.25.201.3 30021                 # FTP port
nc -zv 172.25.201.3 30022                 # SFTP port
nc -zv 172.25.201.3 32149                 # NFS port
```

### Tested Configuration

**Verified working configuration:**
- **Profile:** file-simulator
- **Driver:** Hyper-V
- **Memory:** 8GB (8192 MB)
- **CPUs:** 4
- **Disk:** 20GB
- **Mount:** C:\simulator-data → /mnt/simulator-data
- **All 8 protocols:** Tested and operational ✅

**Resource utilization at full load:**
- CPU requests: 575m (~14% of 4 CPUs)
- CPU limits: 1.9 CPUs (~48% of 4 CPUs)
- Memory requests: 706Mi (~9% of 8GB)
- Memory limits: 2.8Gi (~35% of 8GB)

**Recommended minimum:** 4GB RAM, 2 CPUs (tight)
**Recommended comfortable:** 8GB RAM, 4 CPUs (tested ✅)
**Production recommended:** 12GB RAM, 6 CPUs (headroom)

---

## Testing

### Quick Health Check

```powershell
# Verify all 8 pods are running
kubectl --context=file-simulator get pods -n file-simulator

# Expected output: All 8 pods with STATUS=Running, READY=1/1
```

### Run Test Scripts

```powershell
# PowerShell test script
.\scripts\test-simulator.ps1

# With specific IP
.\scripts\test-simulator.ps1 -MinikubeIp 172.25.201.3
```

### Manual Protocol Tests

**Important**: Always use the correct Minikube IP for your file-simulator cluster:

```powershell
$SIMULATOR_IP = minikube ip -p file-simulator
Write-Host "File Simulator IP: $SIMULATOR_IP"
```

#### FTP

```powershell
# Using Windows FTP client
ftp
open $SIMULATOR_IP 30021
# Login: ftpuser / ftppass123

# Commands to test:
# dir              - list files
# put test.txt     - upload file
# get test.txt     - download file
# bye              - disconnect
```

#### SFTP

```powershell
$SIMULATOR_IP = minikube ip -p file-simulator

# Using OpenSSH (if installed)
sftp -P 30022 sftpuser@$SIMULATOR_IP
# Password: sftppass123

# Commands to test:
# ls               - list files
# put test.txt     - upload file
# get test.txt     - download file
# exit             - disconnect

# Or using WinSCP command line
winscp.com /command "open sftp://sftpuser:sftppass123@${SIMULATOR_IP}:30022/" "ls" "exit"
```

#### S3 (MinIO)

```powershell
$SIMULATOR_IP = minikube ip -p file-simulator

# Using AWS CLI
aws configure set aws_access_key_id minioadmin
aws configure set aws_secret_access_key minioadmin123
aws --endpoint-url http://${SIMULATOR_IP}:30900 s3 ls
aws --endpoint-url http://${SIMULATOR_IP}:30900 s3 mb s3://test-bucket
aws --endpoint-url http://${SIMULATOR_IP}:30900 s3 cp test.txt s3://test-bucket/

# Or access MinIO Console in browser
Start-Process "http://${SIMULATOR_IP}:30901"
```

#### SMB

```powershell
$SIMULATOR_IP = minikube ip -p file-simulator

# IMPORTANT: Ensure minikube tunnel is running first!
# In separate Administrator terminal: minikube tunnel -p file-simulator

# Get LoadBalancer IP
$SMB_IP = kubectl --context=file-simulator get svc file-sim-file-simulator-smb -n file-simulator -o jsonpath='{.status.loadBalancer.ingress[0].ip}'

# Map network drive
net use Z: \\${SMB_IP}\simulator /user:smbuser smbpass123

# Test access
dir Z:\
echo "test" > Z:\output\test.txt
type Z:\output\test.txt

# Disconnect when done
net use Z: /delete
```

#### HTTP/WebDAV

```powershell
$SIMULATOR_IP = minikube ip -p file-simulator

# Test HTTP file server
Invoke-RestMethod -Uri "http://${SIMULATOR_IP}:30088/"

# Upload via WebDAV
$password = ConvertTo-SecureString "httppass123" -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential ("httpuser", $password)
Invoke-WebRequest -Uri "http://${SIMULATOR_IP}:30089/output/test.txt" `
    -Method PUT `
    -InFile "test.txt" `
    -Credential $cred

# Download via HTTP
Invoke-WebRequest -Uri "http://${SIMULATOR_IP}:30088/output/test.txt" -OutFile "downloaded.txt"
```

#### NFS

```bash
# From Linux or WSL
SIMULATOR_IP=$(minikube ip -p file-simulator)

# Install NFS client tools
sudo apt install nfs-common

# Create mount point
sudo mkdir -p /mnt/nfs

# Mount NFS share
sudo mount -t nfs ${SIMULATOR_IP}:/data /mnt/nfs

# Test operations
echo "test" | sudo tee /mnt/nfs/test.txt
cat /mnt/nfs/test.txt
ls -lh /mnt/nfs/

# Unmount when done
sudo umount /mnt/nfs
```

**NFS Test from Kubernetes Pod:**

```powershell
# Create test pod that mounts NFS
kubectl --context=file-simulator apply -f - <<EOF
apiVersion: v1
kind: Pod
metadata:
  name: nfs-test
  namespace: file-simulator
spec:
  containers:
  - name: test
    image: alpine
    command: ['sh', '-c', 'apk add nfs-utils && mkdir -p /mnt/nfs && mount -t nfs file-sim-file-simulator-nas.file-simulator.svc.cluster.local:/data /mnt/nfs && echo "test" > /mnt/nfs/test.txt && cat /mnt/nfs/test.txt && sleep 3600']
  restartPolicy: Never
EOF

# Check logs
kubectl --context=file-simulator logs nfs-test -n file-simulator

# Cleanup
kubectl --context=file-simulator delete pod nfs-test -n file-simulator
```

---

## Troubleshooting

### NFS Server Fix (Critical)

**Problem**: NFS pod crashes with error:
```
exportfs: /data does not support NFS export
----> ERROR: /usr/sbin/exportfs failed
```

**Root Cause**: The NFS server cannot re-export a Windows-mounted hostPath volume (`/mnt/simulator-data`). This is a known limitation - NFS requires a native filesystem to create export tables.

**Solution**: Patch the NFS deployment to use emptyDir for NFS exports:

```powershell
# Create patch file
@"
spec:
  template:
    spec:
      containers:
      - name: nfs-server
        volumeMounts:
        - name: nfs-data
          mountPath: /data
        - name: shared-data
          mountPath: /shared
      volumes:
      - name: nfs-data
        emptyDir: {}
      - name: shared-data
        persistentVolumeClaim:
          claimName: file-sim-file-simulator-pvc
"@ | Out-File -FilePath nfs-fix-patch.yaml -Encoding UTF8

# Apply the patch
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator `
    --patch-file nfs-fix-patch.yaml

# Verify NFS is running
Start-Sleep -Seconds 20
kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=nas --tail=10

# Should show: "READY AND WAITING FOR NFS CLIENT CONNECTIONS"
```

**How the Fix Works:**
- `/data` → emptyDir (ephemeral storage for NFS daemon state/exports)
- `/shared` → PVC (shared storage accessible to all protocols)
- Clients mount NFS and access data through NFSv4 protocol
- Files can still be shared across all protocols via the PVC at `/shared`

**Verification:**
```powershell
# Test NFS connectivity
kubectl --context=file-simulator exec -n file-simulator <nas-pod-name> -- ls -lh /data/

# Test file operations
kubectl --context=file-simulator exec -n file-simulator <nas-pod-name> -- sh -c "echo 'test' > /data/test.txt && cat /data/test.txt"
```

### SMB Connection Issues

**Problem**: Cannot connect to `\\IP\simulator`

**Solutions**:
1. Ensure `minikube tunnel` is running in Administrator terminal
2. Check if LoadBalancer has external IP:
   ```powershell
   kubectl --context=file-simulator get svc -n file-simulator | findstr smb
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
1. Check pod logs (always use --context flag):
   ```powershell
   kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=ftp
   kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=smb
   kubectl --context=file-simulator logs -n file-simulator -l app.kubernetes.io/component=nas
   ```
2. Check events:
   ```powershell
   kubectl --context=file-simulator get events -n file-simulator --sort-by='.lastTimestamp'
   ```
3. Check resource availability:
   ```powershell
   kubectl --context=file-simulator describe nodes
   ```
4. **If insufficient memory/CPU**: Increase cluster resources:
   ```powershell
   minikube delete -p file-simulator
   minikube start -p file-simulator --driver=hyperv --memory=8192 --cpus=4 --disk-size=20g `
       --mount --mount-string="C:\simulator-data:/mnt/simulator-data"
   ```

### Connection Refused from Microservice Cluster

**Problem**: Cannot reach simulator from different cluster

**Solutions**:
1. Verify network connectivity:
   ```powershell
   # Get IPs of both clusters
   minikube ip -p file-simulator      # e.g., 172.25.201.3 (Hyper-V)
   minikube ip -p minikube            # e.g., 192.168.49.2 (Docker)

   # Test ping from other cluster
   kubectl --context=minikube run test --rm -it --image=alpine -n default -- ping -c 3 172.25.201.3
   ```
2. Check firewall rules allow cross-network traffic
3. Ensure both Minikube instances use compatible network modes
4. **Use NodePorts (30XXX) for cross-cluster access**, not internal cluster DNS names

### Accidental Operations on Wrong Cluster

**Problem**: Deleted resources from the wrong cluster by forgetting to switch context

**Root Cause**: Using `kubectl config use-context` to switch between clusters

**Prevention**:
- ✅ **ALWAYS use `--context=<profile-name>` flag** in every kubectl command
- ✅ **NEVER use `kubectl config use-context`** - this causes accidents
- ✅ Use PowerShell helper functions that enforce context safety
- ✅ Keep one namespace per profile (file-simulator in one, ez-platform in another)

**Recovery**: If you accidentally delete resources:
```powershell
# Redeploy file-simulator
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --kube-context=file-simulator `
    --namespace file-simulator

# Apply NFS fix
kubectl --context=file-simulator patch deployment file-sim-file-simulator-nas `
    -n file-simulator --patch-file nfs-fix-patch.yaml
```

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
