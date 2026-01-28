# File Simulator Integration Guide for Client Projects

## Overview

This guide shows how to integrate your .NET client application with the File Simulator Suite to test file protocol operations in development and CI/CD environments.

## Prerequisites

- File Simulator Suite running in Minikube or Kubernetes
- .NET 9.0 SDK
- Your client project

## Quick Start

### Step 1: Add NuGet Packages

```xml
<PackageReference Include="AWSSDK.S3" Version="3.7.305" />
<PackageReference Include="FluentFTP" Version="50.0.1" />
<PackageReference Include="SSH.NET" Version="2024.1.0" />
<PackageReference Include="SMBLibrary" Version="1.5.2" />
```

### Step 2: Get Simulator Endpoints

```powershell
# Get Minikube IP
$SIMULATOR_IP = minikube ip -p file-simulator

# Display endpoints
Write-Host "Simulator IP: $SIMULATOR_IP"
Write-Host "Management UI: http://$SIMULATOR_IP:30180"
Write-Host "FTP:  $SIMULATOR_IP:30021"
Write-Host "SFTP: $SIMULATOR_IP:30022"
Write-Host "HTTP: http://$SIMULATOR_IP:30088"
Write-Host "S3:   http://$SIMULATOR_IP:30900"
Write-Host "SMB:  \\$SIMULATOR_IP\simulator"
Write-Host "NFS:  $SIMULATOR_IP:32149"
```

### Step 3: Configure Your Application

**Option A: appsettings.json**

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
    "S3": {
      "Endpoint": "http://172.23.17.71:30900",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin123",
      "DefaultBucket": "output"
    },
    "Http": {
      "BaseUrl": "http://172.23.17.71:30088",
      "Username": "httpuser",
      "Password": "httppass123",
      "BasePath": "/output"
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
  }
}
```

**Option B: Environment Variables**

```powershell
# FTP
$env:FILE_FTP_HOST="172.23.17.71"
$env:FILE_FTP_PORT="30021"
$env:FILE_FTP_USERNAME="ftpuser"
$env:FILE_FTP_PASSWORD="ftppass123"

# SFTP
$env:FILE_SFTP_HOST="172.23.17.71"
$env:FILE_SFTP_PORT="30022"
$env:FILE_SFTP_USERNAME="sftpuser"
$env:FILE_SFTP_PASSWORD="sftppass123"

# S3
$env:FILE_S3_ENDPOINT="http://172.23.17.71:30900"
$env:FILE_S3_ACCESS_KEY="minioadmin"
$env:FILE_S3_SECRET_KEY="minioadmin123"

# HTTP
$env:FILE_HTTP_URL="http://172.23.17.71:30088"

# SMB
$env:FILE_SMB_HOST="172.23.17.71"
$env:FILE_SMB_PORT="445"
$env:FILE_SMB_SHARE="simulator"
$env:FILE_SMB_USERNAME="smbuser"
$env:FILE_SMB_PASSWORD="smbpass123"
```

---

## Integration Patterns

### Pattern 1: Using FileSimulator.Client Library

**Install the library:**

```powershell
dotnet add reference path/to/FileSimulator.Client/FileSimulator.Client.csproj
```

**Register services:**

```csharp
using FileSimulator.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Option A: Register all protocols
builder.Services.AddFileSimulator(builder.Configuration);

// Option B: Register individual protocols
builder.Services.AddFtpFileService(builder.Configuration);
builder.Services.AddSftpFileService(builder.Configuration);
builder.Services.AddS3FileService(builder.Configuration);
builder.Services.AddSmbFileService(builder.Configuration);
builder.Services.AddNfsFileService(builder.Configuration);

// Option C: Register all at once
builder.Services.AddAllFileProtocolServices(builder.Configuration);

var app = builder.Build();
```

**Use in your services:**

```csharp
public class MyFileProcessor
{
    private readonly FtpFileService _ftpService;
    private readonly S3FileService _s3Service;

    public MyFileProcessor(FtpFileService ftpService, S3FileService s3Service)
    {
        _ftpService = ftpService;
        _s3Service = s3Service;
    }

    public async Task ProcessFile(string filename)
    {
        // Read from FTP
        var data = await _ftpService.ReadFileAsync($"/input/{filename}");

        // Process data
        var processedData = Transform(data);

        // Upload to S3
        await _s3Service.WriteFileAsync($"processed/{filename}", processedData);
    }
}
```

### Pattern 2: Direct Protocol Implementation

**FTP Example:**

```csharp
using FluentFTP;

var client = new AsyncFtpClient("172.23.17.71", "ftpuser", "ftppass123", 30021);
await client.AutoConnect();

// Upload
await client.UploadFile("local-file.txt", "/output/remote-file.txt");

// Download
await client.DownloadFile("local-download.txt", "/output/remote-file.txt");

// List
var files = await client.GetListing("/output");
foreach (var file in files)
{
    Console.WriteLine($"{file.Name} - {file.Size} bytes");
}
```

**S3 Example:**

```csharp
using Amazon.S3;
using Amazon.S3.Model;

var config = new AmazonS3Config
{
    ServiceURL = "http://172.23.17.71:30900",
    ForcePathStyle = true,
    UseHttp = true
};

var client = new AmazonS3Client("minioadmin", "minioadmin123", config);

// Upload
var putRequest = new PutObjectRequest
{
    BucketName = "output",
    Key = "test.txt",
    FilePath = "local-file.txt"
};
await client.PutObjectAsync(putRequest);

// Download
var getRequest = new GetObjectRequest
{
    BucketName = "output",
    Key = "test.txt"
};
var response = await client.GetObjectAsync(getRequest);
```

**SFTP Example:**

```csharp
using Renci.SshNet;

var client = new SftpClient("172.23.17.71", 30022, "sftpuser", "sftppass123");
client.Connect();

// Upload
using var fileStream = File.OpenRead("local-file.txt");
client.UploadFile(fileStream, "/data/output/remote-file.txt");

// Download
using var downloadStream = File.Create("downloaded-file.txt");
client.DownloadFile("/data/output/remote-file.txt", downloadStream);

client.Disconnect();
```

---

## Testing Strategies

### Strategy 1: Unit Tests with Simulator

```csharp
[TestClass]
public class FileProcessorTests
{
    private FtpFileService _ftpService;
    private S3FileService _s3Service;

    [TestInitialize]
    public void Setup()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddFileSimulator(config)
            .BuildServiceProvider();

        _ftpService = serviceProvider.GetRequiredService<FtpFileService>();
        _s3Service = serviceProvider.GetRequiredService<S3FileService>();
    }

    [TestMethod]
    public async Task TestFileProcessing()
    {
        // Arrange
        var testData = Encoding.UTF8.GetBytes("test content");
        await _ftpService.WriteFileAsync("/input/test.txt", testData);

        // Act
        var processor = new MyFileProcessor(_ftpService, _s3Service);
        await processor.ProcessFile("test.txt");

        // Assert
        var s3Data = await _s3Service.ReadFileAsync("processed/test.txt");
        Assert.IsNotNull(s3Data);
    }
}
```

### Strategy 2: Integration Tests

```csharp
[TestClass]
public class CrossProtocolTests
{
    [TestMethod]
    public async Task TestFtpToS3Transfer()
    {
        // Upload via FTP
        var ftpClient = new AsyncFtpClient("172.23.17.71", "ftpuser", "ftppass123", 30021);
        await ftpClient.AutoConnect();
        await ftpClient.UploadBytes(Encoding.UTF8.GetBytes("test"), "/output/test.txt");

        // Wait for processing
        await Task.Delay(1000);

        // Verify in S3
        var s3Config = new AmazonS3Config
        {
            ServiceURL = "http://172.23.17.71:30900",
            ForcePathStyle = true,
            UseHttp = true
        };
        var s3Client = new AmazonS3Client("minioadmin", "minioadmin123", s3Config);

        var response = await s3Client.GetObjectAsync("output", "test.txt");
        Assert.AreEqual(200, (int)response.HttpStatusCode);
    }
}
```

### Strategy 3: End-to-End Tests

```csharp
[TestClass]
public class E2EFileWorkflowTests
{
    [TestMethod]
    public async Task CompleteFileWorkflow()
    {
        // 1. Upload input file via FTP
        var ftpClient = new AsyncFtpClient("172.23.17.71", "ftpuser", "ftppass123", 30021);
        await ftpClient.AutoConnect();
        await ftpClient.UploadFile("test-input.csv", "/input/test.csv");

        // 2. Trigger processing (your application logic)
        var processor = new FileProcessor();
        await processor.ProcessInputFiles();

        // 3. Verify output via S3
        var s3Client = new AmazonS3Client("minioadmin", "minioadmin123", new AmazonS3Config
        {
            ServiceURL = "http://172.23.17.71:30900",
            ForcePathStyle = true,
            UseHttp = true
        });

        var outputExists = await s3Client.GetObjectAsync("output", "test-processed.csv");
        Assert.IsNotNull(outputExists);

        // 4. Verify file accessible via HTTP
        var httpClient = new HttpClient();
        var httpResponse = await httpClient.GetAsync("http://172.23.17.71:30088/download/output/test-processed.csv");
        Assert.IsTrue(httpResponse.IsSuccessStatusCode);
    }
}
```

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Start Minikube
        run: |
          choco install minikube
          minikube start --driver=hyperv

      - name: Deploy File Simulator
        run: |
          helm upgrade --install file-sim ./helm-chart/file-simulator `
            --namespace file-simulator --create-namespace --wait

      - name: Get Simulator IP
        id: simulator
        run: |
          $IP = minikube ip
          echo "SIMULATOR_IP=$IP" >> $env:GITHUB_OUTPUT

      - name: Run Integration Tests
        env:
          FILE_FTP_HOST: ${{ steps.simulator.outputs.SIMULATOR_IP }}
          FILE_S3_ENDPOINT: http://${{ steps.simulator.outputs.SIMULATOR_IP }}:30900
        run: |
          dotnet test --filter Category=Integration
```

### Azure DevOps Example

```yaml
trigger:
  - main

pool:
  vmImage: 'windows-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'

  - script: |
      choco install minikube -y
      minikube start --driver=hyperv
    displayName: 'Start Minikube'

  - script: |
      helm upgrade --install file-sim ./helm-chart/file-simulator `
        --namespace file-simulator --create-namespace --wait
    displayName: 'Deploy File Simulator'

  - script: |
      $IP = minikube ip
      Write-Host "##vso[task.setvariable variable=SIMULATOR_IP]$IP"
    displayName: 'Get Simulator IP'

  - script: |
      dotnet test --filter Category=Integration
    displayName: 'Run Integration Tests'
    env:
      FILE_FTP_HOST: $(SIMULATOR_IP)
      FILE_S3_ENDPOINT: http://$(SIMULATOR_IP):30900
```

---

## Common Use Cases

### Use Case 1: File Upload Validation

Test that your application correctly uploads files to production-like FTP/SFTP servers.

```csharp
[TestMethod]
public async Task ValidateFileUpload()
{
    var uploader = new FileUploader(ftpConfig);

    // Upload test file
    await uploader.UploadAsync("test-document.pdf");

    // Verify via FTP
    var ftpClient = new AsyncFtpClient(/* ... */);
    var exists = await ftpClient.FileExists("/output/test-document.pdf");
    Assert.IsTrue(exists);

    // Verify file content
    var content = await ftpClient.DownloadBytes("/output/test-document.pdf");
    Assert.IsTrue(content.Length > 0);
}
```

### Use Case 2: File Processing Pipeline

Test a complete ETL pipeline using different protocols.

```csharp
[TestMethod]
public async Task TestETLPipeline()
{
    // 1. Extract: Read from FTP
    var extractor = new FtpExtractor(ftpConfig);
    var rawData = await extractor.ExtractAsync("/input/data.csv");

    // 2. Transform: Process data
    var transformer = new DataTransformer();
    var transformedData = transformer.Transform(rawData);

    // 3. Load: Write to S3
    var loader = new S3Loader(s3Config);
    await loader.LoadAsync("processed/data.parquet", transformedData);

    // 4. Verify: Check via HTTP
    var httpClient = new HttpClient();
    var response = await httpClient.GetAsync(
        "http://172.23.17.71:30088/api/files/output/processed/data.parquet");
    Assert.IsTrue(response.IsSuccessStatusCode);
}
```

### Use Case 3: Cross-Protocol File Sharing

Verify files uploaded via one protocol are accessible via others.

```csharp
[TestMethod]
public async Task TestCrossProtocolAccess()
{
    var testData = Encoding.UTF8.GetBytes("cross-protocol test");

    // Upload via FTP
    await ftpService.WriteFileAsync("/output/shared.txt", testData);

    // Read via SFTP
    var sftpData = await sftpService.ReadFileAsync("/data/output/shared.txt");
    CollectionAssert.AreEqual(testData, sftpData);

    // Read via HTTP
    var httpClient = new HttpClient();
    var httpData = await httpClient.GetByteArrayAsync(
        "http://172.23.17.71:30088/download/output/shared.txt");
    CollectionAssert.AreEqual(testData, httpData);

    // Read via SMB
    var smbData = await smbService.ReadFileAsync("/output/shared.txt");
    CollectionAssert.AreEqual(testData, smbData);
}
```

---

## Troubleshooting

### Connection Refused

**Problem:** Cannot connect to simulator from tests.

**Solution:**
```powershell
# Verify simulator is running
kubectl get pods -n file-simulator

# Get correct IP
$IP = minikube ip -p file-simulator
Write-Host "Use IP: $IP"

# Test connectivity
Test-NetConnection -ComputerName $IP -Port 30021  # FTP
Test-NetConnection -ComputerName $IP -Port 30022  # SFTP
```

### SMB Connection Fails

**Problem:** `STATUS_BAD_NETWORK_NAME` or connection timeout.

**Solution:**
```powershell
# Ensure minikube tunnel is running
minikube tunnel -p file-simulator

# Verify LoadBalancer IP
kubectl get svc -n file-simulator file-sim-file-simulator-smb
```

### Authentication Errors

**Problem:** Login failed / Access denied.

**Solution:** Verify credentials match defaults:
- FTP: `ftpuser` / `ftppass123`
- SFTP: `sftpuser` / `sftppass123`
- S3: `minioadmin` / `minioadmin123`
- SMB: `smbuser` / `smbpass123`
- HTTP: `httpuser` / `httppass123`

### File Not Found After Upload

**Problem:** File uploaded but not visible in other protocols.

**Solution:**
```powershell
# Check if file exists in PVC
kubectl -n file-simulator exec deploy/file-sim-file-simulator-ftp -- \
    ls -la /home/vsftpd/ftpuser/output

# Verify all pods use same PVC
kubectl -n file-simulator get pvc
```

---

## Best Practices

### 1. Configuration Management

**Development:**
```json
{
  "FileSimulator": {
    "Ftp": { "Host": "172.23.17.71", "Port": 30021 }
  }
}
```

**Production:**
```json
{
  "FileSimulator": {
    "Ftp": { "Host": "prod-ftp.example.com", "Port": 21 }
  }
}
```

### 2. Test Data Management

```csharp
public class TestDataManager
{
    public async Task SetupTestData()
    {
        // Create test files
        await UploadTestFile("sample-invoice.pdf", ftpService);
        await UploadTestFile("customer-data.csv", sftpService);
    }

    public async Task CleanupTestData()
    {
        // Remove test files after tests
        await ftpService.DeleteFileAsync("/output/sample-invoice.pdf");
        await sftpService.DeleteFileAsync("/data/output/customer-data.csv");
    }
}
```

### 3. Health Checks

```csharp
public async Task<bool> IsSimulatorHealthy()
{
    var checks = new[]
    {
        ftpService.HealthCheckAsync(),
        sftpService.HealthCheckAsync(),
        s3Service.HealthCheckAsync(),
        httpService.HealthCheckAsync(),
        smbService.HealthCheckAsync()
    };

    var results = await Task.WhenAll(checks);
    return results.All(r => r);
}
```

### 4. Parallel Testing

```csharp
[TestMethod]
public async Task RunParallelTests()
{
    var tests = new[]
    {
        TestFtpUpload(),
        TestSftpUpload(),
        TestS3Upload(),
        TestHttpUpload()
    };

    await Task.WhenAll(tests);
}
```

---

## Example Test Project Structure

```
MyProject.Tests/
├── Integration/
│   ├── FtpIntegrationTests.cs
│   ├── SftpIntegrationTests.cs
│   ├── S3IntegrationTests.cs
│   └── CrossProtocolTests.cs
├── Helpers/
│   ├── SimulatorFixture.cs
│   └── TestDataFactory.cs
├── appsettings.test.json
└── MyProject.Tests.csproj
```

**SimulatorFixture.cs:**
```csharp
public class SimulatorFixture : IDisposable
{
    public FtpFileService FtpService { get; }
    public SftpFileService SftpService { get; }
    public S3FileService S3Service { get; }

    public SimulatorFixture()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        var services = new ServiceCollection();
        services.AddFileSimulator(config);

        var provider = services.BuildServiceProvider();
        FtpService = provider.GetRequiredService<FtpFileService>();
        SftpService = provider.GetRequiredService<SftpFileService>();
        S3Service = provider.GetRequiredService<S3FileService>();
    }

    public void Dispose()
    {
        // Cleanup
    }
}
```

---

## Quick Reference

### All Service Endpoints

| Protocol | Endpoint | Credentials |
|----------|----------|-------------|
| Management UI | `http://<IP>:30180` | `admin` / `admin123` |
| FTP | `<IP>:30021` | `ftpuser` / `ftppass123` |
| SFTP | `<IP>:30022` | `sftpuser` / `sftppass123` |
| HTTP | `http://<IP>:30088` | `httpuser` / `httppass123` |
| WebDAV | `http://<IP>:30089` | `httpuser` / `httppass123` |
| S3 API | `http://<IP>:30900` | `minioadmin` / `minioadmin123` |
| S3 Console | `http://<IP>:30901` | `minioadmin` / `minioadmin123` |
| SMB | `\\<IP>\simulator` | `smbuser` / `smbpass123` |
| NFS | `<IP>:/` (port 32149) | Anonymous |

### Common Commands

```powershell
# Start simulator
helm upgrade --install file-sim ./helm-chart/file-simulator `
    --namespace file-simulator --create-namespace

# Check status
kubectl get pods -n file-simulator

# View logs
kubectl logs -n file-simulator -l app.kubernetes.io/component=ftp

# Restart service
kubectl rollout restart -n file-simulator deploy/file-sim-file-simulator-ftp

# Access management UI
start http://$(minikube ip -p file-simulator):30180
```

---

## Support

For issues or questions:
- GitHub: https://github.com/usercourses63/file-simulator-suite
- Documentation: See README.md in repository
