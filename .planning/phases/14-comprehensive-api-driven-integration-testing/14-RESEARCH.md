# Phase 14: Comprehensive API-Driven Integration Testing - Research

**Researched:** 2026-02-05
**Domain:** .NET Integration Testing / API-Driven Test Automation
**Confidence:** HIGH

## Summary

Phase 14 aims to create comprehensive integration tests that validate 100% simulator reliability through automated API-driven testing. The project already has two test frameworks in place: **TestConsole** (CLI-based functional tests using Spectre.Console) and **E2ETests** (Playwright-based browser automation). The challenge is to consolidate or extend these into a unified test suite that can:

1. Test all static protocol servers (FTP, SFTP, HTTP, WebDAV, S3, SMB, NFS)
2. Test dynamic server lifecycle (create → connect → operate → delete)
3. Validate API credential extraction from Kubernetes
4. Verify cross-protocol file visibility
5. Test Kafka integration
6. Test all 7 NAS servers independently
7. Export results as JUnit XML for CI/CD
8. Exit with non-zero code if any test fails

The **standard approach** for .NET integration testing in 2026 is **xUnit + FluentAssertions** for test framework, with **WebApplicationFactory** for ASP.NET Core API testing. The project already uses this stack for E2E tests. The TestConsole provides excellent functional coverage but lacks structured test reporting and CI/CD integration. The recommendation is to **create a new xUnit-based integration test project** that consolidates protocol tests, dynamic server tests, Kafka tests, and NAS tests while leveraging JUnit XML output for CI/CD reporting.

**Primary recommendation:** Create `FileSimulator.IntegrationTests` project using xUnit + FluentAssertions, consolidate TestConsole logic into xUnit test classes with shared collection fixtures, implement Polly retry policies for flaky infrastructure tests, and configure JUnit XML logger for CI/CD integration.

## Standard Stack

The established libraries/tools for .NET integration testing in 2026:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| xUnit.net | 2.9.0+ | Test framework | Recommended by Microsoft for ASP.NET Core, better test isolation and collection fixtures |
| FluentAssertions | 6.12.2+ | Assertion library | Expressive, readable assertions; standard in .NET community |
| Microsoft.NET.Test.Sdk | 17.12.0+ | Test SDK | Required for running tests in Visual Studio and `dotnet test` |
| Microsoft.AspNetCore.Mvc.Testing | 9.0.0+ | WebApplicationFactory | Standard for in-process API testing with ASP.NET Core |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Polly | 8.x | Resilience and retry policies | Handle flaky infrastructure tests (network timeouts, pod readiness delays) |
| JunitXml.TestLogger | 4.0+ | JUnit XML export | CI/CD integration (Azure DevOps, Jenkins, GitHub Actions) |
| Testcontainers | 3.x | Docker container management | Advanced: spin up Kafka/Minikube programmatically (optional for this phase) |
| WireMock.Net | 1.x | HTTP mocking | Mock external dependencies (not needed - testing real infrastructure) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| xUnit | NUnit | NUnit has [SetUp]/[TearDown] attributes but xUnit's constructor/disposal model provides better test isolation |
| xUnit | MSTest | MSTest is Microsoft's older framework, less community adoption for integration tests |
| FluentAssertions | NUnit constraints | FluentAssertions has better readability and more expressive failure messages |
| JunitXml.TestLogger | TRX format | TRX is .NET-specific; JUnit XML is cross-platform CI/CD standard |

**Installation:**
```bash
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package FluentAssertions
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package JunitXml.TestLogger
dotnet add package Polly
```

**Sources:**
- [ASP.NET Core Integration Testing Best Practises](https://antondevtips.com/blog/asp-net-core-integration-testing-best-practises) (MEDIUM confidence)
- [Integration tests in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) (HIGH confidence)
- [Testing in .NET - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/testing/) (HIGH confidence)

## Architecture Patterns

### Recommended Project Structure
```
tests/
├── FileSimulator.IntegrationTests/
│   ├── Fixtures/                     # Shared test context
│   │   ├── SimulatorFixture.cs       # Connection to file-simulator cluster
│   │   └── ProtocolClientFixture.cs  # Reusable protocol clients
│   ├── Protocols/                    # Static protocol tests
│   │   ├── FtpProtocolTests.cs
│   │   ├── SftpProtocolTests.cs
│   │   ├── HttpProtocolTests.cs
│   │   ├── WebDavProtocolTests.cs
│   │   ├── S3ProtocolTests.cs
│   │   ├── SmbProtocolTests.cs
│   │   └── NfsProtocolTests.cs
│   ├── DynamicServers/               # Lifecycle tests
│   │   ├── FtpServerLifecycleTests.cs
│   │   ├── SftpServerLifecycleTests.cs
│   │   └── NasServerLifecycleTests.cs
│   ├── CrossProtocol/                # File visibility tests
│   │   └── CrossProtocolFileVisibilityTests.cs
│   ├── Kafka/                        # Kafka integration tests
│   │   ├── KafkaTopicManagementTests.cs
│   │   └── KafkaProduceConsumeTests.cs
│   ├── NasServers/                   # Multi-NAS tests
│   │   └── MultiNasServerTests.cs
│   ├── Api/                          # API validation tests
│   │   └── ConnectionInfoApiTests.cs
│   ├── Support/                      # Test utilities
│   │   ├── RetryPolicies.cs          # Polly retry configurations
│   │   ├── TestHelpers.cs            # Common test utilities
│   │   └── ProtocolClients.cs        # Shared client wrappers
│   ├── FileSimulator.IntegrationTests.csproj
│   ├── xunit.runner.json             # Test runner configuration
│   └── appsettings.test.json         # Test configuration
```

### Pattern 1: Collection Fixtures for Shared Context

**What:** xUnit collection fixtures create shared test context across multiple test classes, with setup executed once and cleanup after all tests complete.

**When to use:** When multiple test classes need to share expensive resources (e.g., HTTP clients, protocol connections, Kubernetes API clients).

**Example:**
```csharp
// Source: https://xunit.net/docs/shared-context
// Define collection fixture
public class SimulatorCollectionFixture : IDisposable
{
    public HttpClient ApiClient { get; }
    public string ControlApiUrl { get; } = "http://file-simulator.local:30500";

    public SimulatorCollectionFixture()
    {
        // Setup executed ONCE before any test in collection
        ApiClient = new HttpClient
        {
            BaseAddress = new Uri(ControlApiUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose()
    {
        // Cleanup executed ONCE after all tests complete
        ApiClient?.Dispose();
    }
}

// Define collection
[CollectionDefinition("Simulator")]
public class SimulatorCollection : ICollectionFixture<SimulatorCollectionFixture>
{
    // This class is never instantiated - it's just a marker
}

// Use in test classes
[Collection("Simulator")]
public class FtpProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public FtpProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FTP_CanConnect()
    {
        // Test uses shared fixture
        var response = await _fixture.ApiClient.GetAsync("/api/connection-info");
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
```

**Key benefits:**
- Tests in same collection don't run in parallel (avoid resource contention)
- Expensive setup executes once (faster test suite)
- Automatic cleanup ensures no state leakage

**Sources:**
- [Sharing Context between Tests | xUnit.net](https://xunit.net/docs/shared-context) (HIGH confidence)
- [xUnit Advanced Features: Fixtures | Medium](https://medium.com/@leogjorge/xunit-advanced-features-fixtures-6b0ca4d10469) (MEDIUM confidence)

### Pattern 2: Retry Policies for Flaky Infrastructure

**What:** Use Polly retry policies to handle transient failures in infrastructure tests (network timeouts, pod startup delays, Kubernetes eventual consistency).

**When to use:** When testing infrastructure that has expected transient failures (server readiness delays, DNS propagation, eventual consistency).

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/architecture/cloud-native/application-resiliency-patterns
using Polly;
using Polly.Retry;

public static class RetryPolicies
{
    // Wait-and-retry with exponential backoff
    public static AsyncRetryPolicy<HttpResponseMessage> HttpRetryPolicy => Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"Retry {retryCount} after {timespan.TotalSeconds}s");
            });

    // Dynamic server readiness polling
    public static AsyncRetryPolicy<bool> ServerReadinessPolicy => Policy
        .HandleResult<bool>(ready => !ready)
        .WaitAndRetryAsync(
            retryCount: 30,
            sleepDurationProvider: _ => TimeSpan.FromSeconds(2),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"Waiting for server readiness ({retryCount}/30)...");
            });
}

// Usage in tests
[Fact]
public async Task DynamicFtpServer_BecomesReady_WithinTimeout()
{
    // Create server via API
    var createResponse = await _apiClient.PostAsJsonAsync("/api/servers/ftp", new { name = "test-ftp" });
    createResponse.IsSuccessStatusCode.Should().BeTrue();

    // Poll with retry until ready
    var isReady = await RetryPolicies.ServerReadinessPolicy.ExecuteAsync(async () =>
    {
        var status = await _apiClient.GetFromJsonAsync<ServerStatus>("/api/servers/test-ftp");
        return status?.PodReady == true && status?.Status == "Running";
    });

    isReady.Should().BeTrue("server should become ready within 60 seconds");
}
```

**Key principles:**
- Don't retry test logic failures (assertions) - only infrastructure transients
- Use exponential backoff to avoid overwhelming infrastructure
- Log retry attempts for debugging
- Set reasonable max attempts (don't wait forever)

**Sources:**
- [Application resiliency patterns - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/cloud-native/application-resiliency-patterns) (HIGH confidence)
- [Retry Pattern in Java: Building Fault-Tolerant Systems](https://java-design-patterns.com/patterns/retry/) (MEDIUM confidence - Java but principles apply)
- [Flaky tests, be gone | Evil Martians](https://evilmartians.com/chronicles/flaky-tests-be-gone-long-lasting-relief-chronic-ci-retry-irritation) (HIGH confidence)

### Pattern 3: Test Organization by Feature Area

**What:** Organize tests by feature area (protocol, lifecycle, cross-cutting concerns) rather than by test type.

**When to use:** When test suite covers multiple distinct feature areas with different setup requirements.

**Example:**
```csharp
// Protocols/ - Each protocol gets its own test class
[Collection("Simulator")]
public class FtpProtocolTests
{
    [Fact]
    public Task FTP_Upload_CreatesFile() { }

    [Fact]
    public Task FTP_Download_ReturnsCorrectContent() { }

    [Fact]
    public Task FTP_List_ReturnsAllFiles() { }

    [Fact]
    public Task FTP_Delete_RemovesFile() { }
}

// DynamicServers/ - Lifecycle tests
[Collection("Simulator")]
public class FtpServerLifecycleTests
{
    [Fact]
    public async Task FtpServer_CompleteLifecycle()
    {
        // Create → Wait → Connect → Upload → Delete
        var serverName = $"test-ftp-{Guid.NewGuid():N}";

        try
        {
            // Create
            var createResponse = await CreateServerAsync(serverName);
            createResponse.IsSuccessStatusCode.Should().BeTrue();

            // Wait for ready
            var isReady = await WaitForServerReadyAsync(serverName);
            isReady.Should().BeTrue();

            // Upload file
            var uploadSuccess = await UploadTestFileAsync(serverName);
            uploadSuccess.Should().BeTrue();
        }
        finally
        {
            // Always cleanup
            await DeleteServerAsync(serverName);
        }
    }
}

// CrossProtocol/ - File visibility
[Collection("Simulator")]
public class CrossProtocolFileVisibilityTests
{
    [Fact]
    public async Task File_UploadedViaFTP_VisibleViaSFTP()
    {
        var fileName = $"cross-test-{Guid.NewGuid():N}.txt";

        // Upload via FTP
        await _ftpClient.UploadFileAsync("/output/" + fileName, "test content");

        // List via SFTP
        var sftpFiles = await _sftpClient.ListFilesAsync("/data/output");
        sftpFiles.Should().Contain(f => f.Name == fileName);
    }
}
```

### Pattern 4: JUnit XML Export for CI/CD

**What:** Configure xUnit to output test results in JUnit XML format for CI/CD pipeline integration.

**When to use:** Always - enables integration with Azure DevOps, Jenkins, GitHub Actions, and other CI/CD systems.

**Example:**
```bash
# Run tests with JUnit XML logger
dotnet test --logger:"junit;LogFilePath=test-results/junit-{assembly}.xml"

# In CI/CD pipeline (GitHub Actions example)
- name: Run Integration Tests
  run: dotnet test tests/FileSimulator.IntegrationTests --logger:"junit;LogFilePath=test-results/junit.xml"

- name: Publish Test Results
  uses: EnricoMi/publish-unit-test-result-action@v2
  if: always()
  with:
    files: test-results/*.xml
```

**Exit code handling:**
```csharp
// dotnet test automatically exits with non-zero if any test fails
// No special configuration needed

// For custom test runners:
public static async Task<int> Main(string[] args)
{
    var result = await TestRunner.RunTestsAsync();
    return result.Failed > 0 ? 1 : 0;  // Exit 1 if any failures
}
```

**Sources:**
- [xUnit test reporting with Tesults](https://www.tesults.com/docs/xunit) (MEDIUM confidence)
- [Execute And Publish XUnit Tests Results With .NET Core And VSTS | Xebia](https://xebia.com/blog/execute-and-publish-xunit-tests-results-with-net-core-and-vsts/) (MEDIUM confidence)

### Anti-Patterns to Avoid

- **Anti-pattern: Testing everything in one giant test method** - Makes failures hard to diagnose and debug. Split into focused tests per operation.
- **Anti-pattern: Not cleaning up dynamic servers** - Use try/finally or IDisposable to ensure cleanup even on test failure.
- **Anti-pattern: Sharing mutable state between tests** - Use collection fixtures for read-only shared context only; create fresh resources per test.
- **Anti-pattern: Testing only success paths** - Test error conditions (invalid credentials, network failures, resource conflicts).
- **Anti-pattern: Hard-coded timeouts** - Use configuration or environment variables for timeouts (different CI/CD environments have different performance).

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Test retries for flaky tests | Custom retry loops with Thread.Sleep | Polly retry policies | Handles exponential backoff, jitter, max attempts, and logging automatically |
| JUnit XML generation | Custom XML serialization | JunitXml.TestLogger | Handles xUnit v2/v3 format differences, parallel test output, and CI/CD compatibility |
| Protocol client connection pooling | Manual connection caching | Built-in protocol client connection management (FluentFTP, SSH.NET) | Thread-safe, handles reconnection, disposes properly |
| Test fixture lifecycle | Manual setup/teardown in [Fact] | IClassFixture or ICollectionFixture | Guarantees setup once, cleanup once, handles exceptions |
| HTTP client management in tests | New HttpClient() per test | Shared HttpClient in fixture or IHttpClientFactory | Avoids socket exhaustion, connection pooling |

**Key insight:** Integration testing infrastructure has more edge cases than appear obvious. Use battle-tested libraries that handle timeouts, retries, parallel execution, resource cleanup, and error reporting.

## Common Pitfalls

### Pitfall 1: Flaky Tests from Ignoring Infrastructure Delays

**What goes wrong:** Tests fail intermittently because they assume infrastructure is instantly ready (pods scheduled, services reachable, DNS propagated).

**Why it happens:** Kubernetes is eventually consistent. Pod status="Running" doesn't mean application is ready. Dynamic servers need time to start.

**How to avoid:**
- Use Polly retry policies with exponential backoff
- Test for readiness (health endpoints, connection success) not just pod status
- Set generous timeouts (30-60s for server readiness)
- Retry at infrastructure level, not test logic level

**Warning signs:**
- Tests pass locally but fail in CI/CD
- Tests pass when run individually but fail when run in suite
- "Connection refused" or "Timeout" errors in test output

**Example fix:**
```csharp
// ❌ BAD: Assumes instant readiness
await CreateServerAsync("test-ftp");
await TestFtpConnectivityAsync("test-ftp");  // Fails - server not ready

// ✅ GOOD: Poll until ready
await CreateServerAsync("test-ftp");
await WaitForServerReadyAsync("test-ftp", timeout: TimeSpan.FromSeconds(60));
await TestFtpConnectivityAsync("test-ftp");  // Succeeds
```

**Sources:**
- [Flaky tests, be gone | Evil Martians](https://evilmartians.com/chronicles/flaky-tests-be-gone-long-lasting-relief-chronic-ci-retry-irritation) (HIGH confidence)
- [Cypress vs Selenium in 2026 | TheLinuxCode](https://thelinuxcode.com/cypress-vs-selenium-in-2026-what-actually-changes-your-test-suite-and-what-doesnt/) (MEDIUM confidence - mentions retry best practices)

### Pitfall 2: Not Isolating Dynamic Server Tests

**What goes wrong:** Dynamic server creation/deletion tests leave orphaned resources, causing subsequent test runs to fail with "resource already exists" errors.

**Why it happens:** Test failures during cleanup phase, parallel test execution creating name collisions, or incomplete teardown.

**How to avoid:**
- Use unique names with GUID or timestamp: `test-ftp-{Guid.NewGuid():N}`
- Always cleanup in finally block or IDisposable
- Use collection fixtures to prevent parallel execution of resource creation tests
- Add pre-test cleanup to remove any orphaned resources

**Warning signs:**
- "Server already exists" errors on subsequent test runs
- Need to manually delete resources before tests pass
- Tests pass first time but fail on re-run

**Example fix:**
```csharp
// ❌ BAD: No cleanup on failure
[Fact]
public async Task TestDynamicFtp()
{
    await CreateServerAsync("test-ftp");
    await TestOperationsAsync("test-ftp");
    await DeleteServerAsync("test-ftp");  // Never runs if test fails
}

// ✅ GOOD: Guaranteed cleanup
[Fact]
public async Task TestDynamicFtp()
{
    var serverName = $"test-ftp-{Guid.NewGuid():N}";  // Unique name
    try
    {
        await CreateServerAsync(serverName);
        await WaitForServerReadyAsync(serverName);
        await TestOperationsAsync(serverName);
    }
    finally
    {
        // Always runs even on test failure
        await DeleteServerAsync(serverName);
    }
}
```

### Pitfall 3: Testing Cross-Protocol Visibility Without Synchronization

**What goes wrong:** Cross-protocol tests fail because file system operations aren't immediately visible across protocols due to caching or buffering.

**Why it happens:** Different protocols may cache directory listings, NFS may buffer writes, or shared storage may have propagation delays.

**How to avoid:**
- Add small delays (100-500ms) between upload and verification
- Explicitly refresh directory listings before checking
- Use retry with polling for file existence checks
- Test both directions (FTP→SFTP and SFTP→FTP)

**Warning signs:**
- Test passes when run slowly but fails when run quickly
- File exists via API but not visible via protocol client
- Intermittent failures in cross-protocol tests

**Example fix:**
```csharp
// ❌ BAD: No synchronization
await _ftpClient.UploadFileAsync("/output/test.txt", "content");
var sftpFiles = await _sftpClient.ListFilesAsync("/data/output");
sftpFiles.Should().Contain(f => f.Name == "test.txt");  // May fail

// ✅ GOOD: Retry with polling
await _ftpClient.UploadFileAsync("/output/test.txt", "content");

var fileVisible = await Policy
    .HandleResult<bool>(visible => !visible)
    .WaitAndRetryAsync(5, _ => TimeSpan.FromMilliseconds(200))
    .ExecuteAsync(async () =>
    {
        var files = await _sftpClient.ListFilesAsync("/data/output");
        return files.Any(f => f.Name == "test.txt");
    });

fileVisible.Should().BeTrue();
```

### Pitfall 4: Not Testing Platform-Specific Constraints

**What goes wrong:** Tests pass on developer machine but fail in CI/CD due to platform differences (SMB tunnel requirement, NFS mount not available).

**Why it happens:** Windows vs Linux differences, Minikube tunnel requirement for SMB, NFS requiring mount on Windows.

**How to avoid:**
- Add platform detection and skip tests conditionally
- Document platform requirements in test class attributes
- Provide alternative test paths (e.g., TCP connectivity check if NFS mount unavailable)
- Run tests in CI/CD environment that matches production (Docker, Linux)

**Warning signs:**
- Tests pass locally on Windows but fail in Linux CI/CD
- SMB tests require manual `minikube tunnel` to pass
- NFS tests require pre-mounted filesystem

**Example fix:**
```csharp
[Fact]
public async Task SMB_FileOperations()
{
    // Check if minikube tunnel is active (SMB requires LoadBalancer)
    if (!await IsSmbAccessibleAsync())
    {
        // Skip instead of fail - document why
        Skip.If(true, "SMB requires 'minikube tunnel' on Windows. " +
                     "Run tunnel in separate admin terminal before running tests.");
    }

    await TestSmbOperationsAsync();
}

[Fact]
public async Task NFS_FileOperations()
{
    var nfsMountPath = "/mnt/nfs-test";

    if (!Directory.Exists(nfsMountPath))
    {
        // Fall back to TCP connectivity test
        var tcpConnectivity = await TestNfsTcpConnectivityAsync();
        tcpConnectivity.Should().BeTrue("NFS server should be reachable via TCP");

        Skip.If(true, "NFS mount not available. TCP connectivity verified. " +
                     "To test file operations, mount NFS: sudo mount -t nfs host:/data /mnt/nfs-test");
    }

    await TestNfsFileOperationsAsync(nfsMountPath);
}
```

## Code Examples

Verified patterns from official sources:

### xUnit Collection Fixture with HttpClient
```csharp
// Source: https://xunit.net/docs/shared-context
public class SimulatorCollectionFixture : IDisposable
{
    public HttpClient ApiClient { get; }
    public string ApiUrl { get; } = "http://file-simulator.local:30500";

    public SimulatorCollectionFixture()
    {
        ApiClient = new HttpClient
        {
            BaseAddress = new Uri(ApiUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Verify API is reachable
        var healthCheck = ApiClient.GetAsync("/api/health").Result;
        if (!healthCheck.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Control API not reachable at {ApiUrl}. Start simulator first.");
        }
    }

    public void Dispose()
    {
        ApiClient?.Dispose();
    }
}

[CollectionDefinition("Simulator")]
public class SimulatorCollection : ICollectionFixture<SimulatorCollectionFixture> { }
```

### Protocol Test with FluentAssertions
```csharp
// Source: https://fluentassertions.com/introduction
[Collection("Simulator")]
public class FtpProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public FtpProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FTP_Upload_CreatesFileWithCorrectContent()
    {
        // Arrange
        var fileName = $"test-{Guid.NewGuid():N}.txt";
        var content = "Test file content";
        var config = await GetConnectionInfoAsync();

        using var ftpClient = new AsyncFtpClient(
            config.Ftp.Host,
            config.Ftp.Username,
            config.Ftp.Password,
            config.Ftp.Port);

        // Act
        await ftpClient.Connect();
        await ftpClient.UploadStream(
            new MemoryStream(Encoding.UTF8.GetBytes(content)),
            $"/output/{fileName}",
            FtpRemoteExists.Overwrite);

        // Assert
        var files = await ftpClient.GetListing("/output");
        files.Should().Contain(f => f.Name == fileName);

        await using var downloadStream = new MemoryStream();
        await ftpClient.DownloadStream(downloadStream, $"/output/{fileName}");
        var downloaded = Encoding.UTF8.GetString(downloadStream.ToArray());

        downloaded.Should().Be(content);

        // Cleanup
        await ftpClient.DeleteFile($"/output/{fileName}");
        await ftpClient.Disconnect();
    }

    private async Task<ConnectionInfo> GetConnectionInfoAsync()
    {
        return await _fixture.ApiClient
            .GetFromJsonAsync<ConnectionInfo>("/api/connection-info");
    }
}
```

### Dynamic Server Lifecycle Test with Retry
```csharp
// Source: Adapted from existing TestConsole patterns + Polly documentation
[Collection("Simulator")]
public class FtpServerLifecycleTests
{
    private readonly SimulatorCollectionFixture _fixture;

    [Fact]
    public async Task FtpServer_CompleteLifecycle_CreatesConnectsAndDeletes()
    {
        var serverName = $"test-ftp-{Guid.NewGuid():N}";

        try
        {
            // Step 1: Create server
            var createResponse = await _fixture.ApiClient.PostAsJsonAsync(
                "/api/servers/ftp",
                new { name = serverName, username = "testuser", password = "testpass123" });

            createResponse.IsSuccessStatusCode.Should().BeTrue();

            var createResult = await createResponse.Content
                .ReadFromJsonAsync<ServerCreationResponse>();

            createResult.Should().NotBeNull();
            createResult!.Name.Should().Be(serverName);

            // Step 2: Wait for server to become ready (with retry)
            var isReady = await Policy
                .HandleResult<bool>(ready => !ready)
                .WaitAndRetryAsync(30, _ => TimeSpan.FromSeconds(2))
                .ExecuteAsync(async () =>
                {
                    var status = await _fixture.ApiClient
                        .GetFromJsonAsync<ServerStatus>($"/api/servers/{serverName}");
                    return status?.PodReady == true && status?.Status == "Running";
                });

            isReady.Should().BeTrue("server should become ready within 60 seconds");

            // Step 3: Test connectivity
            var serverInfo = await _fixture.ApiClient
                .GetFromJsonAsync<ServerStatus>($"/api/servers/{serverName}");

            using var ftpClient = new AsyncFtpClient(
                serverInfo!.Host,
                serverInfo.Username!,
                serverInfo.Password!,
                serverInfo.Port);

            await ftpClient.Connect();
            ftpClient.IsConnected.Should().BeTrue();
            await ftpClient.Disconnect();
        }
        finally
        {
            // Step 4: Always cleanup
            var deleteResponse = await _fixture.ApiClient
                .DeleteAsync($"/api/servers/{serverName}");

            // Verify deletion (404 is success - server no longer exists)
            deleteResponse.IsSuccessStatusCode.Should().BeTrue();
        }
    }
}
```

### JUnit XML Configuration
```xml
<!-- In FileSimulator.IntegrationTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="FluentAssertions" Version="6.12.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="JunitXml.TestLogger" Version="4.0.0" />
    <PackageReference Include="Polly" Version="8.2.0" />

    <!-- Protocol clients -->
    <PackageReference Include="FluentFTP" Version="50.0.1" />
    <PackageReference Include="SSH.NET" Version="2024.1.0" />
    <PackageReference Include="AWSSDK.S3" Version="3.7.305" />
    <PackageReference Include="SMBLibrary" Version="1.5.2" />
    <PackageReference Include="Confluent.Kafka" Version="2.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\FileSimulator.Client\FileSimulator.Client.csproj" />
  </ItemGroup>
</Project>
```

**Run with JUnit XML output:**
```bash
dotnet test tests/FileSimulator.IntegrationTests \
    --logger:"junit;LogFilePath=test-results/junit-integration-tests.xml"
```

## State of the Art

| Old Approach | Current Approach (2026) | When Changed | Impact |
|--------------|-------------------------|--------------|--------|
| Manual test scripts (PowerShell/Bash) | Automated xUnit integration tests with JUnit XML | 2023-2024 | CI/CD integration, automated regression testing |
| Separate tool per protocol (curl, ftp CLI) | Unified protocol client libraries in .NET | 2024+ | Consistent error handling, easier maintenance |
| No retry logic - tests fail on transients | Polly retry policies with exponential backoff | 2024+ | Reduced flaky test failures |
| Thread.Sleep for infrastructure delays | Async polling with structured retry policies | 2024+ | Faster tests, better error messages |
| TRX format (Microsoft-specific) | JUnit XML (cross-platform standard) | 2023+ | Works with all CI/CD systems |
| One test project per concern | Feature-based organization with shared fixtures | 2025+ | Better discoverability, less duplication |

**Deprecated/outdated:**
- **MSTest framework for integration tests**: Replaced by xUnit which has better test isolation and parallel execution
- **Synchronous test methods**: Use async/await throughout (all protocol clients are async)
- **Hard-coded configuration**: Use appsettings.test.json + environment variables for flexibility
- **No structured logging in tests**: xUnit captures ITestOutputHelper for test-specific logging

## Open Questions

Things that couldn't be fully resolved:

1. **NFS testing from Windows without mount**
   - What we know: NFS requires mounted filesystem for file operations; TCP connectivity can verify server is reachable
   - What's unclear: Whether to skip NFS file operation tests on Windows or provide alternative testing (in-cluster testing pod?)
   - Recommendation: Test TCP connectivity on Windows, document mount requirement, consider Linux-based CI/CD for full NFS testing

2. **SMB testing without minikube tunnel**
   - What we know: SMB requires LoadBalancer which needs `minikube tunnel` on Windows
   - What's unclear: Can we automate tunnel start/stop in tests, or should tests require manual tunnel setup?
   - Recommendation: Add pre-test check for SMB accessibility, skip with clear message if tunnel not active

3. **Kafka connectivity from Windows host**
   - What we know: Kafka has connectivity issues from Windows host in existing TestConsole
   - What's unclear: Is this Minikube networking issue, Kafka configuration, or Windows firewall?
   - Recommendation: Test Kafka from within Kubernetes cluster (sidecar pod) or document known limitation

4. **Cross-protocol file visibility timing**
   - What we know: Files uploaded via one protocol should be visible via another (shared PVC)
   - What's unclear: What is reasonable propagation delay? Does it vary by protocol combination?
   - Recommendation: Start with 500ms delay + 5 retry attempts (total ~2.5s), adjust based on empirical results

5. **Test execution time targets**
   - What we know: Integration tests are slower than unit tests
   - What's unclear: What is acceptable total execution time for 50+ integration tests?
   - Recommendation: Target <5 minutes for full suite, use parallel execution where safe (collection fixtures control parallelism)

## Sources

### Primary (HIGH confidence)
- [Integration tests in ASP.NET Core | Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0) - Official Microsoft documentation on integration testing
- [Testing in .NET - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/testing/) - Official .NET testing guidance
- [Sharing Context between Tests | xUnit.net](https://xunit.net/docs/shared-context) - Official xUnit documentation on fixtures
- [Application resiliency patterns - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/cloud-native/application-resiliency-patterns) - Official Polly/retry patterns

### Secondary (MEDIUM confidence)
- [ASP.NET Core Integration Testing Best Practises](https://antondevtips.com/blog/asp-net-core-integration-testing-best-practises) - Community best practices (2025)
- [Integration Testing in .NET: A Practical Guide | DEV Community](https://dev.to/tkarropoulos/integration-testing-in-net-a-practical-guide-to-tools-and-techniques-bch) - Practical guide with examples
- [xUnit Advanced Features: Fixtures | Medium](https://medium.com/@leogjorge/xunit-advanced-features-fixtures-6b0ca4d10469) - Deep dive on fixture patterns
- [Flaky tests, be gone | Evil Martians](https://evilmartians.com/chronicles/flaky-tests-be-gone-long-lasting-relief-chronic-ci-retry-irritation) - Best practices for handling flaky tests (Sept 2025)

### Tertiary (LOW confidence)
- [Retry Pattern in Java | Java Design Patterns](https://java-design-patterns.com/patterns/retry/) - Java-specific but principles apply
- [Cypress vs Selenium in 2026 | TheLinuxCode](https://thelinuxcode.com/cypress-vs-selenium-in-2026-what-actually-changes-your-test-suite-and-what-doesnt/) - Mentions retry best practices

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - xUnit + FluentAssertions is documented standard from Microsoft
- Architecture: HIGH - Collection fixtures and Polly are well-documented patterns
- Pitfalls: MEDIUM - Based on existing TestConsole patterns + general integration testing experience

**Research date:** 2026-02-05
**Valid until:** ~60 days (framework/library choices stable; best practices evolve slowly)

## Implementation Notes for Planner

Based on existing codebase analysis:

1. **Existing TestConsole has excellent coverage** - Can consolidate logic into xUnit tests
2. **E2ETests project uses Playwright** - Keep separate from integration tests (different concerns)
3. **FileSimulator.Client library exists** - Can reuse protocol client abstractions if available
4. **Control API already tested** - Connection info endpoint working, just needs structured test validation
5. **Dynamic server tests exist** - DynamicServerTests.cs has lifecycle patterns to replicate
6. **Kafka tests exist** - KafkaTests.cs has topic management and produce/consume patterns
7. **NAS tests exist** - NasServerTests.cs has multi-server testing patterns
8. **Platform-specific issues documented** - CLAUDE.md notes SMB tunnel requirement, NFS Windows limitations

**Key decision:** Create new `FileSimulator.IntegrationTests` project rather than converting TestConsole, because:
- TestConsole provides valuable CLI testing capability (keep it)
- Integration tests need structured reporting (JUnit XML)
- Integration tests need retry policies (TestConsole doesn't have)
- Integration tests run in CI/CD pipeline (different requirements than manual testing)
