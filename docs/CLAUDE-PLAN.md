# File Simulator Testing Plan for Client Projects

## Quick Setup for Testing

This plan helps you integrate File Simulator Suite into your .NET client application for testing file protocol operations.

---

## Phase 1: Environment Setup

### 1.1 Verify Simulator Running

```powershell
# Check simulator status
kubectl get pods -n file-simulator

# Get simulator IP
$SIMULATOR_IP = minikube ip -p file-simulator
Write-Host "Simulator: http://$SIMULATOR_IP:30180"
```

**Expected Output:** All pods Running (1/1)

### 1.2 Add Required NuGet Packages

```xml
<ItemGroup>
  <PackageReference Include="AWSSDK.S3" Version="3.7.305" />
  <PackageReference Include="FluentFTP" Version="50.0.1" />
  <PackageReference Include="SSH.NET" Version="2024.1.0" />
  <PackageReference Include="SMBLibrary" Version="1.5.2" />
</ItemGroup>
```

### 1.3 Configure Connection Settings

Create `appsettings.Development.json`:

```json
{
  "FileSimulator": {
    "Ftp": {
      "Host": "172.23.17.71",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123"
    },
    "Sftp": {
      "Host": "172.23.17.71",
      "Port": 30022,
      "Username": "sftpuser",
      "Password": "sftppass123"
    },
    "S3": {
      "Endpoint": "http://172.23.17.71:30900",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin123"
    },
    "Smb": {
      "Host": "172.23.17.71",
      "Port": 445,
      "ShareName": "simulator",
      "Username": "smbuser",
      "Password": "smbpass123"
    }
  }
}
```

---

## Phase 2: Service Registration

### Option A: Use FileSimulator.Client Library (Recommended)

**Add reference:**
```powershell
dotnet add reference ../FileSimulator.Client/FileSimulator.Client.csproj
```

**Register in Program.cs:**
```csharp
using FileSimulator.Client.Extensions;

// Register all protocols
builder.Services.AddFileSimulator(builder.Configuration);

// Or register individually
builder.Services.AddFtpFileService(builder.Configuration);
builder.Services.AddSftpFileService(builder.Configuration);
builder.Services.AddS3FileService(builder.Configuration);
```

**Use in services:**
```csharp
public class MyService
{
    private readonly FtpFileService _ftp;
    private readonly S3FileService _s3;

    public MyService(FtpFileService ftp, S3FileService s3)
    {
        _ftp = ftp;
        _s3 = s3;
    }

    public async Task ProcessFile(string filename)
    {
        var data = await _ftp.ReadFileAsync($"/input/{filename}");
        var processed = Transform(data);
        await _s3.WriteFileAsync($"output/{filename}", processed);
    }
}
```

### Option B: Direct Protocol Implementation

**FTP:**
```csharp
var client = new AsyncFtpClient("172.23.17.71", "ftpuser", "ftppass123", 30021);
await client.AutoConnect();
await client.UploadFile("local.txt", "/output/remote.txt");
```

**S3:**
```csharp
var config = new AmazonS3Config
{
    ServiceURL = "http://172.23.17.71:30900",
    ForcePathStyle = true,
    UseHttp = true
};
var s3 = new AmazonS3Client("minioadmin", "minioadmin123", config);
await s3.PutObjectAsync(new PutObjectRequest
{
    BucketName = "output",
    Key = "file.txt",
    FilePath = "local.txt"
});
```

---

## Phase 3: Write Tests

### 3.1 Create Test Project

```powershell
dotnet new mstest -n YourProject.IntegrationTests
cd YourProject.IntegrationTests
dotnet add reference ../YourProject/YourProject.csproj
```

### 3.2 Basic Integration Test

```csharp
[TestClass]
public class FileProcessingTests
{
    private static FtpFileService _ftpService;
    private static S3FileService _s3Service;

    [ClassInitialize]
    public static void Setup(TestContext context)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        var services = new ServiceCollection();
        services.AddFileSimulator(config);
        var provider = services.BuildServiceProvider();

        _ftpService = provider.GetRequiredService<FtpFileService>();
        _s3Service = provider.GetRequiredService<S3FileService>();
    }

    [TestMethod]
    public async Task TestFileUpload()
    {
        // Arrange
        var testData = Encoding.UTF8.GetBytes("test content");

        // Act
        await _ftpService.WriteFileAsync("/output/test.txt", testData);

        // Assert
        var readData = await _ftpService.ReadFileAsync("/output/test.txt");
        CollectionAssert.AreEqual(testData, readData);
    }

    [TestMethod]
    public async Task TestCrossProtocolAccess()
    {
        // Upload via FTP
        var data = Encoding.UTF8.GetBytes("cross-protocol test");
        await _ftpService.WriteFileAsync("/output/shared.txt", data);

        // Verify via S3 (different protocol, same storage)
        var s3Data = await _s3Service.ReadFileAsync("output/shared.txt");
        CollectionAssert.AreEqual(data, s3Data);
    }

    [TestMethod]
    public async Task TestFileProcessingPipeline()
    {
        // 1. Upload input via FTP
        var input = Encoding.UTF8.GetBytes("raw data");
        await _ftpService.WriteFileAsync("/input/data.txt", input);

        // 2. Process (your business logic)
        var processor = new FileProcessor(_ftpService, _s3Service);
        await processor.ProcessAsync("data.txt");

        // 3. Verify output in S3
        var output = await _s3Service.ReadFileAsync("processed/data.txt");
        Assert.IsNotNull(output);
        Assert.IsTrue(output.Length > 0);
    }
}
```

### 3.3 Run Tests

```powershell
dotnet test --logger "console;verbosity=detailed"
```

---

## Phase 4: Common Testing Patterns

### Pattern 1: File Upload Validation

```csharp
[TestMethod]
public async Task ValidateFileUpload_CorrectFormat()
{
    var uploader = new FileUploader(_ftpService);

    await uploader.UploadAsync("invoice.pdf");

    var exists = await _ftpService.DiscoverFilesAsync("/output", "invoice.pdf");
    Assert.IsTrue(exists.Any());
}
```

### Pattern 2: ETL Pipeline Testing

```csharp
[TestMethod]
public async Task TestETLPipeline()
{
    // Extract from FTP
    var rawData = await _ftpService.ReadFileAsync("/input/sales.csv");

    // Transform
    var transformer = new DataTransformer();
    var transformed = transformer.Transform(rawData);

    // Load to S3
    await _s3Service.WriteFileAsync("analytics/sales.parquet", transformed);

    // Verify
    var result = await _s3Service.ReadFileAsync("analytics/sales.parquet");
    Assert.IsNotNull(result);
}
```

### Pattern 3: Error Handling

```csharp
[TestMethod]
public async Task TestFileNotFound()
{
    await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
    {
        await _ftpService.ReadFileAsync("/nonexistent/file.txt");
    });
}

[TestMethod]
public async Task TestConnectionFailure()
{
    var badConfig = new FtpServerOptions
    {
        Host = "invalid-host",
        Port = 99999
    };

    var badService = new FtpFileService(
        Options.Create(badConfig),
        NullLogger<FtpFileService>.Instance
    );

    var isHealthy = await badService.HealthCheckAsync();
    Assert.IsFalse(isHealthy);
}
```

---

## Phase 5: Verification Checklist

### Pre-Test Checklist

- [ ] File Simulator pods all Running (1/1)
- [ ] Can access Management UI at http://172.23.17.71:30180
- [ ] NuGet packages installed
- [ ] appsettings configured with correct IP
- [ ] Services registered in DI container

### Test Execution Checklist

- [ ] Basic connectivity test passes
- [ ] File upload test passes
- [ ] File download test passes
- [ ] Cross-protocol access works
- [ ] Error handling works correctly
- [ ] All integration tests pass

### Post-Test Verification

```powershell
# View test files in Management UI
start http://172.23.17.71:30180

# Check via kubectl
kubectl -n file-simulator exec deploy/file-sim-file-simulator-ftp -- \
    ls -la /home/vsftpd/ftpuser/output

# Verify cross-protocol sharing
kubectl -n file-simulator exec deploy/file-sim-file-simulator-ftp -- \
    ls /home/vsftpd/ftpuser/output | \
kubectl -n file-simulator exec deploy/file-sim-file-simulator-nas -- \
    ls /data/output
# Should show same files
```

---

## Quick Reference

### Service Endpoints

```
Management UI:  http://172.23.17.71:30180  (admin/admin123)
FTP:            172.23.17.71:30021         (ftpuser/ftppass123)
SFTP:           172.23.17.71:30022         (sftpuser/sftppass123)
HTTP:           http://172.23.17.71:30088  (httpuser/httppass123)
S3 API:         http://172.23.17.71:30900  (minioadmin/minioadmin123)
S3 Console:     http://172.23.17.71:30901  (minioadmin/minioadmin123)
SMB:            \\172.23.17.71\simulator   (smbuser/smbpass123)
```

### Troubleshooting Commands

```powershell
# Verify simulator
kubectl get pods -n file-simulator
minikube ip -p file-simulator

# Test connectivity
Test-NetConnection -ComputerName 172.23.17.71 -Port 30021  # FTP
Test-NetConnection -ComputerName 172.23.17.71 -Port 30022  # SFTP

# View logs
kubectl logs -n file-simulator -l app.kubernetes.io/component=ftp

# Restart service
kubectl rollout restart -n file-simulator deploy/file-sim-file-simulator-ftp
```

### Common Issues

**Connection Refused:**
```powershell
$IP = minikube ip -p file-simulator
Write-Host "Update config with IP: $IP"
```

**SMB Fails:**
```powershell
# Start minikube tunnel
minikube tunnel -p file-simulator
```

**File Not Found:**
```powershell
# Check all pods use same PVC
kubectl get pvc -n file-simulator
kubectl get pv
```

---

## Expected Results

After completing this plan:

✅ **Services Registered:** All file protocol services available via DI
✅ **Tests Passing:** Integration tests verify file operations
✅ **Cross-Protocol:** Files uploaded via one protocol visible in others
✅ **Error Handling:** Application handles connection failures gracefully
✅ **CI/CD Ready:** Tests can run in automated pipelines

---

## Next Steps

1. **Extend test coverage** for your specific business logic
2. **Add performance tests** to measure throughput
3. **Create test data fixtures** for consistent testing
4. **Document protocol-specific requirements** in your domain
5. **Set up CI/CD pipeline** with File Simulator deployment

---

## Support Resources

- **Full Integration Guide:** `docs/CLIENT-INTEGRATION-GUIDE.md`
- **Example Project:** `src/FileSimulator.TestConsole/`
- **Helm Chart:** `helm-chart/file-simulator/`
- **Cross-cluster Setup:** `examples/client-cluster/README.md`
