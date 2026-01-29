# Testing Patterns

**Analysis Date:** 2026-01-29

## Test Framework

**Runner:**
- No dedicated unit test framework (xUnit, NUnit, etc.) configured
- Manual integration testing via `FileSimulator.TestConsole` application
- Console-based test runner using Spectre.Console for rich output

**Assertion Library:**
- Basic boolean/equality checks in test console
- No formal assertion library (xUnit or similar)

**Run Commands:**
```bash
dotnet run --project src/FileSimulator.TestConsole/FileSimulator.TestConsole.csproj
dotnet run --project src/FileSimulator.TestConsole/FileSimulator.TestConsole.csproj -- --cross-protocol
```

## Test File Organization

**Location:**
- Tests are co-located in test console: `src/FileSimulator.TestConsole/Program.cs`
- Cross-protocol tests in separate file: `src/FileSimulator.TestConsole/CrossProtocolTest.cs`
- No dedicated unit test project (.Tests.csproj)

**Naming:**
- Test functions: `Test{Protocol}Async` (e.g., `TestFtpAsync`, `TestSftpAsync`)
- Test data classes: `TestResult`
- Extension methods for display: `StringExtensions`

**Structure:**
```
src/FileSimulator.TestConsole/
├── Program.cs              # Main test runner with all protocol tests
├── CrossProtocolTest.cs    # Cross-protocol integration tests
├── appsettings.json        # Test configuration
└── appsettings.*.json      # Environment-specific config
```

## Test Structure

**Suite Organization:**
- All protocol tests run sequentially in `Main` method
- Results collected in `List<TestResult>`
- Status updates via Spectre.Console status context
- Optional cross-protocol mode via `--cross-protocol` flag

**Pattern from Program.cs:**
```csharp
var results = new List<TestResult>();
var testContent = $"Test file created at {DateTime.UtcNow:O}\nThis is a test file for protocol validation.";
var testFileName = $"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";

// Run all protocol tests
await AnsiConsole.Status()
    .StartAsync("Running protocol tests...", async ctx =>
    {
        ctx.Status("Testing FTP...");
        results.Add(await TestFtpAsync(config, testContent, testFileName));

        ctx.Status("Testing SFTP...");
        results.Add(await TestSftpAsync(config, testContent, testFileName));

        // ... more protocol tests
    });

// Display results table
DisplayResults(results);

// Display summary
DisplaySummary(results);
```

**Setup Pattern:**
- Configuration loaded from `appsettings.json` at startup
- Test content generated with timestamp: `$"Test file created at {DateTime.UtcNow:O}"`
- Random filename per test run: `$"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt"`

**Teardown Pattern:**
- Files deleted after read verification in each protocol test
- Manual cleanup after test run via deletion operations
- No fixture teardown framework

**Assertion Pattern:**
- Boolean checks: `result.Connected`, `result.UploadSuccess`
- Content equality: `downloaded == content`
- File existence: `files.Any(i => i.Name == fileName)`
- Status codes: `response.IsSuccessStatusCode`
- Result collection: individual test results aggregated in `List<TestResult>`

## Test Operations by Protocol

**FTP Test Pattern:**
```csharp
static async Task<TestResult> TestFtpAsync(IConfiguration config, string content, string fileName)
{
    var result = new TestResult { Protocol = "FTP" };
    var host = config["FileSimulator:Ftp:Host"] ?? "localhost";
    var port = int.Parse(config["FileSimulator:Ftp:Port"] ?? "30021");

    try
    {
        using var client = new AsyncFtpClient(host, username, password, port);

        // Connect
        var sw = Stopwatch.StartNew();
        await client.Connect();
        result.ConnectMs = sw.ElapsedMilliseconds;
        result.Connected = true;

        // Upload
        var remotePath = $"{basePath}/{fileName}";
        var bytes = Encoding.UTF8.GetBytes(content);
        sw.Restart();
        await using (var stream = new MemoryStream(bytes))
        {
            await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite, true);
        }
        result.UploadMs = sw.ElapsedMilliseconds;
        result.UploadSuccess = true;

        // List/Discover
        sw.Restart();
        var items = await client.GetListing(basePath);
        result.ListMs = sw.ElapsedMilliseconds;
        result.ListSuccess = items.Any(i => i.Name == fileName);

        // Download/Read
        sw.Restart();
        await using (var stream = new MemoryStream())
        {
            await client.DownloadStream(stream, remotePath);
            stream.Position = 0;
            var downloaded = Encoding.UTF8.GetString(stream.ToArray());
            result.ReadSuccess = downloaded == content;
        }
        result.ReadMs = sw.ElapsedMilliseconds;

        // Delete
        sw.Restart();
        await client.DeleteFile(remotePath);
        result.DeleteMs = sw.ElapsedMilliseconds;
        result.DeleteSuccess = true;

        await client.Disconnect();
    }
    catch (Exception ex)
    {
        result.Error = ex.InnerException?.Message ?? ex.Message;
    }

    return result;
}
```

**SFTP Test Pattern:**
- Wraps sync-only SSH.NET client in `Task.Run()` for async context
- Same operation sequence: connect, upload, list, read, delete

**S3 Test Pattern:**
- Uses AWS SDK for operations
- Verifies bucket existence for connection test
- Lists objects with optional prefix filtering

**HTTP Test Pattern:**
- Health check via `/health` endpoint
- List via `/api/files/` JSON endpoint
- Read/write via WebDAV endpoints

**SMB Test Pattern:**
- Host resolution (localhost to IP address)
- SMB2 protocol with NTLM authentication
- Raw TCP fallback test for debugging network issues

**NFS Test Pattern:**
- Checks mount path existence
- Falls back to TCP connection test if mount unavailable
- File operations via mounted filesystem

## Test Result Tracking

**TestResult Class:**
```csharp
public class TestResult
{
    public string Protocol { get; set; } = "";
    public bool Connected { get; set; }
    public long ConnectMs { get; set; }
    public bool? UploadSuccess { get; set; }
    public long? UploadMs { get; set; }
    public bool? ListSuccess { get; set; }
    public long? ListMs { get; set; }
    public bool? ReadSuccess { get; set; }
    public long? ReadMs { get; set; }
    public bool? DeleteSuccess { get; set; }
    public long? DeleteMs { get; set; }
    public string? Error { get; set; }
}
```

**Metrics Tracked:**
- Connection time in milliseconds
- Operation time per stage (upload, list, read, delete)
- Boolean success/failure for each operation
- Error message if operation fails
- Nullable booleans for optional operations (HTTP is read-only)

## Mocking

**Framework:** No mocking framework detected (not applicable)

**Patterns:**
- Real protocol clients used in tests (FluentFTP, SSH.NET, AWS SDK, etc.)
- Direct connections to test servers required
- No stubbing or test doubles

**Integration Testing Approach:**
- Tests connect to actual running services
- Tests require services to be deployed/running
- Tests verify end-to-end functionality

## Fixtures and Factories

**Test Data:**
- Generate random filename: `$"test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt"`
- Generate timestamped content: `$"Test file created at {DateTime.UtcNow:O}\nThis is a test file for protocol validation."`

**Location:**
- Test data generated inline in each `Test{Protocol}Async` method
- Configuration loaded from `appsettings.json` at application startup

**Configuration Example from appsettings.json:**
```json
{
  "FileSimulator": {
    "Ftp": {
      "Host": "localhost",
      "Port": 30021,
      "Username": "ftpuser",
      "Password": "ftppass123",
      "BasePath": "/output"
    },
    "Sftp": {
      "Host": "localhost",
      "Port": 30022,
      "Username": "sftpuser",
      "Password": "sftppass123",
      "BasePath": "/data/output"
    },
    // ... more protocols
  }
}
```

## Coverage

**Requirements:** Not enforced (no coverage tools configured)

**View Coverage:** Not applicable (no coverage measurement)

## Test Types

**Integration Tests:**
- Scope: Full protocol implementation from client to server
- Approach: Real client connections to actual servers
- Tests: Each protocol has dedicated test function
- Validation: Compare uploaded content with downloaded content

**End-to-End Tests:**
- Scope: Complete operation sequences
- Approach: All 5 operations per protocol (connect, upload, list, read, delete)
- Cross-protocol mode: Validates file sharing between protocols

**Not Used:**
- Unit tests (no xUnit/NUnit project)
- E2E tests with Selenium or similar
- Load/performance testing beyond elapsed time measurement

**Cross-Protocol Testing:**
`CrossProtocolTest.cs` validates:
- Upload file via one protocol
- Read via different protocol
- Verify content integrity across protocols

## Test Display and Reporting

**Output Format:**
- Rich console output via Spectre.Console
- Formatted table showing all protocols and operation timings
- Color-coded results: green for pass, red for fail, grey for N/A

**Display Functions:**
```csharp
static void DisplayResults(List<TestResult> results)
{
    // Creates formatted table with:
    // - Protocol name
    // - Connect time
    // - Upload/List/Read/Delete operation times
    // - Total time (ms)
    // - Error message if present
}

static void DisplaySummary(List<TestResult> results)
{
    // Shows:
    // - Passed/Failed/Total counts
    // - List of failed protocols with error messages
}
```

**Example Table Output:**
```
┌──────────────┬─────────┬────────┬───────┬───────┬────────┬───────────┐
│ Protocol     │ Connect │ Upload │ List  │ Read  │ Delete │ Total (ms)│
├──────────────┼─────────┼────────┼───────┼───────┼────────┼───────────┤
│ FTP          │ 45ms    │ 123ms  │ 12ms  │ 8ms   │ 5ms    │ 193       │
│ SFTP         │ 52ms    │ 115ms  │ 10ms  │ 6ms   │ 4ms    │ 187       │
│ S3/MinIO     │ 38ms    │ 95ms   │ 8ms   │ 7ms   │ 3ms    │ 151       │
└──────────────┴─────────┴────────┴───────┴───────┴────────┴───────────┘
```

## Running Tests

**Standard Test Run:**
```bash
cd src/FileSimulator.TestConsole
dotnet run
```

**Cross-Protocol Test Mode:**
```bash
cd src/FileSimulator.TestConsole
dotnet run -- --cross-protocol
# or
dotnet run -- -x
```

**With Custom Configuration:**
```bash
# Set environment before running
set DOTNET_ENVIRONMENT=Production
dotnet run
```

**Test Prerequisites:**
1. All protocol servers must be deployed and running
2. Network connectivity to server endpoints
3. Valid credentials configured in appsettings.json
4. Required base paths created on servers
5. For NFS: mount path must exist on client (or server reachable via TCP)

## Debugging Tests

**Verbose Logging:**
- Configuration shows which appsettings file is loaded
- Each protocol test shows detailed error messages
- Spectre.Console markup provides colored output for visibility
- SMB tests include TCP fallback diagnostics

**Example Debug Output:**
```
[grey]Config loaded from: /path/to/bin[/]
[grey]HTTP BaseUrl: http://localhost:30088[/]
[grey]Connecting to SMB server 192.168.1.100:445...[/]
[grey]SMB2 Connect returned: true[/]
[grey]Connected, attempting login...[/]
```

---

*Testing analysis: 2026-01-29*
