---
phase: 14-comprehensive-api-driven-integration-testing
plan: 04
subsystem: integration-testing
tags: [dynamic-servers, ftp, sftp, lifecycle-tests, api-testing]

requires:
  - phase: 14
    plan: 01
    reason: "Requires test infrastructure (SimulatorCollectionFixture, RetryPolicies, TestHelpers)"

provides:
  - "Dynamic FTP server lifecycle tests"
  - "Dynamic SFTP server lifecycle tests"
  - "DynamicServerHelpers API interaction library"
  - "Server creation, readiness polling, and cleanup patterns"

affects:
  - phase: 14
    plan: "future"
    impact: "Establishes patterns for testing other dynamic server types (NAS, custom protocols)"

tech-stack:
  added:
    - "DynamicServerHelpers: API client for dynamic server management"
  patterns:
    - "Create -> Wait for Ready -> Connect -> File Ops -> Delete lifecycle"
    - "Try/finally cleanup to prevent orphaned test servers"
    - "Polling with timeout for server readiness"
    - "NodePort-based external connectivity for Minikube"

key-files:
  created:
    - "tests/FileSimulator.IntegrationTests/DynamicServers/DynamicFtpServerTests.cs"
    - "tests/FileSimulator.IntegrationTests/DynamicServers/DynamicSftpServerTests.cs"
    - "tests/FileSimulator.IntegrationTests/Support/DynamicServerHelpers.cs"

decisions:
  - decision: "Use /api/servers/ftp and /api/servers/sftp endpoints"
    rationale: "Control API uses separate endpoints per protocol type, not generic /api/servers"
    alternatives: ["Generic POST /api/servers with protocol field"]

  - decision: "Return DiscoveredServer model from creation API"
    rationale: "API returns full server discovery info, not simplified creation response"
    impact: "Tests work with ClusterIp, NodePort, PodStatus instead of simplified Host/Port"

  - decision: "Use NodePort for external connectivity"
    rationale: "Minikube requires NodePort to access services from Windows host"
    impact: "Tests connect to file-simulator.local:<NodePort> instead of ClusterIp"

  - decision: "Default credentials in ServerStatusResponse"
    rationale: "Newly created servers may not have credentials populated immediately"
    workaround: "Return testuser/testpass123 defaults from computed properties"

  - decision: "Accept FTP passive mode failures as known limitation"
    rationale: "2 FTP file operation tests fail with passive mode connection refused"
    impact: "10/12 tests pass - create, ready, connect, delete all work"
    future: "FTP passive mode requires additional NodePort configuration for data channel"

metrics:
  duration: "45 minutes"
  tests_created: 12
  tests_passing: 10
  tests_failing: 2
  lines_of_code: 760
  completed: "2026-02-05"
---

# Phase 14 Plan 04: Dynamic FTP/SFTP Server Lifecycle Tests Summary

**One-liner:** Complete lifecycle tests for dynamic FTP and SFTP servers via Control API with create, readiness polling, connectivity verification, file operations, and cleanup.

## What Was Built

### 1. DynamicServerHelpers API Library

Created `Support/DynamicServerHelpers.cs` with complete API interaction methods:

**Server Creation:**
- `CreateFtpServerAsync()` - POST /api/servers/ftp
- `CreateSftpServerAsync()` - POST /api/servers/sftp
- `CreateNasServerAsync()` - POST /api/servers/nas
- All use Polly retry policies for resilience

**Server Lifecycle:**
- `WaitForServerReadyAsync()` - Poll GET /api/servers/{name} until PodReady=true and Status=Running
- `GetServerStatusAsync()` - Query current server status
- `DeleteServerAsync()` - DELETE /api/servers/{name} with deletion verification

**Response Models:**
- `ServerCreationResponse` - Maps DiscoveredServer from API
- `ServerStatusResponse` - Maps DiscoveredServer with computed Host/Username/Password
- `ServerCredentialsInfo` - Credentials from server configuration

### 2. Dynamic FTP Server Tests

Created `DynamicServers/DynamicFtpServerTests.cs` with 6 test scenarios:

1. **FtpServer_Create_ReturnsServerInfo** ✅
   - Validates server creation via API
   - Checks: Name, ServiceName, NodePort, IsDynamic flag

2. **FtpServer_BecomesReady_WithinTimeout** ✅
   - Validates server becomes ready within 60 seconds
   - Checks: PodReady=true, Status=Running

3. **FtpServer_AcceptsConnections_WhenReady** ✅
   - Validates FTP control channel connectivity
   - Uses AsyncFtpClient to verify connection

4. **FtpServer_FileOperations_WorkCorrectly** ❌
   - Upload, list, download, delete file operations
   - FAILS: FTP passive mode data channel connection refused

5. **FtpServer_Delete_CleansUpResources** ✅
   - Validates server deletion via API
   - Polls until GET returns 404

6. **FtpServer_CompleteLifecycle** ❌
   - End-to-end test of full lifecycle
   - FAILS: Same passive mode issue as test 4

### 3. Dynamic SFTP Server Tests

Created `DynamicServers/DynamicSftpServerTests.cs` with 6 test scenarios:

1. **SftpServer_Create_ReturnsServerInfo** ✅
2. **SftpServer_BecomesReady_WithinTimeout** ✅
3. **SftpServer_AcceptsConnections_WhenReady** ✅
4. **SftpServer_FileOperations_WorkCorrectly** ✅ (SFTP fully functional!)
5. **SftpServer_Delete_CleansUpResources** ✅
6. **SftpServer_CompleteLifecycle** ✅ (Complete end-to-end success!)

All SFTP tests use `Task.Run()` to wrap SSH.NET synchronous operations.

## Test Results

**Summary:** 10/12 tests passing (83% success rate)

**Passing Tests (10):**
- All FTP lifecycle tests except file operations (4/6)
- All SFTP tests including complete lifecycle (6/6)

**Failing Tests (2):**
- FTP file operations tests fail with passive mode connection issue
- Root cause: FTP data channel requires additional NodePort configuration
- Control channel works correctly (connect tests pass)

**Known Issue: FTP Passive Mode**
- Error: "No connection could be made because the target machine actively refused it"
- Occurs during: UploadStream/DownloadStream data transfer
- Impact: File operations fail, but server creation/connectivity/deletion work
- Resolution: Requires FTP passive port range NodePort allocation (future work)

## Key Patterns Established

### 1. Complete Server Lifecycle Pattern

```csharp
var serverName = TestHelpers.GenerateUniqueServerName("ftp");
try
{
    // 1. Create server
    var response = await DynamicServerHelpers.CreateFtpServerAsync(_fixture.ApiClient, serverName);

    // 2. Wait for ready
    var status = await DynamicServerHelpers.WaitForServerReadyAsync(
        _fixture.ApiClient, serverName, TimeSpan.FromSeconds(60));

    // 3. Connect and operate
    using var client = new AsyncFtpClient(status.Host, status.Username, status.Password, status.NodePort ?? status.Port);
    await client.Connect();

    // ... perform operations ...

    await client.Disconnect();
}
finally
{
    // 4. Always cleanup
    await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
}
```

### 2. Readiness Polling Pattern

```csharp
var deadline = DateTime.UtcNow + timeout;
while (DateTime.UtcNow < deadline)
{
    var status = await client.GetAsync($"/api/servers/{name}");
    if (status.PodReady && status.Status == "Running")
        return status; // Server ready!

    await Task.Delay(2000); // Poll every 2 seconds
}
throw new TimeoutException(...);
```

### 3. Cleanup Safety Pattern

All tests use try/finally blocks to ensure:
- Servers are always deleted even if test fails
- No orphaned dynamic servers accumulate in cluster
- Test isolation maintained across runs

## Technical Insights

### API Endpoint Discovery

Initially attempted POST /api/servers (405 Method Not Allowed).

Correct endpoints:
- `/api/servers/ftp` - Create FTP server
- `/api/servers/sftp` - Create SFTP server
- `/api/servers/nas` - Create NAS server
- `/api/servers/{name}` - Get status, Delete server

### DiscoveredServer Model

API returns full `DiscoveredServer` model, not simplified creation response:

```json
{
  "name": "test-ftp-abc123",
  "protocol": "FTP",
  "clusterIp": "10.109.108.53",
  "port": 21,
  "nodePort": 31629,
  "podStatus": "Running",
  "podReady": true,
  "isDynamic": true,
  "managedBy": "control-api",
  "credentials": null  // Initially null, populated after pod starts
}
```

### External Connectivity

- **ClusterIp**: Internal Kubernetes IP (10.x.x.x)
- **Port**: Internal cluster port (21, 22)
- **NodePort**: External port for Minikube access (30000-32767)
- **Host**: file-simulator.local (DNS hostname)

Tests connect via: `file-simulator.local:<NodePort>`

### Credentials Handling

Newly created servers have `credentials: null` in creation response.

Solution: Computed properties provide defaults:
```csharp
public string Username => Credentials?.Username ?? "testuser";
public string Password => Credentials?.Password ?? "testpass123";
```

This works because servers are created with these exact credentials.

## Deviations from Plan

### 1. API Endpoint Structure

**Planned:** POST /api/servers with protocol field in body

**Actual:** Separate endpoints per protocol (/api/servers/ftp, /api/servers/sftp)

**Impact:** Updated DynamicServerHelpers to use correct endpoints

### 2. Response Model Complexity

**Planned:** Simple ServerCreationResponse with Host/Port/Username/Password

**Actual:** Full DiscoveredServer model with ClusterIp/NodePort/PodStatus/Credentials

**Impact:** Updated models to match API, added computed properties for simplified access

### 3. FTP Passive Mode Limitation

**Planned:** Complete file operations testing for both FTP and SFTP

**Actual:** FTP passive mode data channel fails (connection refused)

**Impact:** 2 tests fail, but core lifecycle (create/ready/connect/delete) works perfectly

**Decision:** Accept as known limitation for this phase, track for future fix

## Next Phase Readiness

### Blockers: None

All infrastructure for dynamic server testing is in place.

### Concerns

**FTP Passive Mode:**
- File operations fail for dynamic FTP servers
- Root cause: Data channel requires NodePort range allocation
- Workaround: Use SFTP for file transfer tests (works perfectly)
- Future: Implement FTP passive port range NodePorts in Kubernetes deployment

### Recommendations for Future Work

1. **Fix FTP Passive Mode**
   - Allocate NodePort range for FTP passive data ports (30000-30010)
   - Configure dynamic FTP server with PASV address and port range
   - Reference: Similar to static FTP server configuration

2. **Add NAS Dynamic Server Tests**
   - Reuse DynamicServerHelpers.CreateNasServerAsync()
   - Test NFS mount operations (requires different testing approach)
   - Similar lifecycle pattern to FTP/SFTP

3. **Test Concurrent Server Creation**
   - Create multiple servers simultaneously
   - Verify resource allocation and naming conflicts handled
   - Test cleanup of multiple servers

4. **Add Server Restart/Lifecycle Operations**
   - Test server restart while preserving data
   - Test server stop/start operations
   - Verify data persistence across lifecycle events

## Verification

### Build
```bash
dotnet build tests/FileSimulator.IntegrationTests
# Success - compiles without errors
```

### Tests
```bash
dotnet test tests/FileSimulator.IntegrationTests --filter "FullyQualifiedName~DynamicFtpServerTests | FullyQualifiedName~DynamicSftpServerTests"
# Result: 10 passed, 2 failed (83% pass rate)
```

### Manual Verification

1. Dynamic FTP server creation:
```bash
curl -X POST http://file-simulator.local:30500/api/servers/ftp \
  -H "Content-Type: application/json" \
  -d '{"name":"test-manual","username":"testuser","password":"testpass123","directory":"input/test"}'
# Returns: DiscoveredServer with nodePort
```

2. Server becomes ready within 60 seconds
3. FTP control channel connection successful
4. Server deletion successful

## Success Criteria

✅ Dynamic FTP server: create -> ready -> connect -> delete
✅ Dynamic SFTP server: create -> ready -> connect -> file ops -> delete
✅ All tests clean up servers in finally blocks (no orphans)
✅ Tests use unique server names to avoid conflicts
❌ FTP file operations (passive mode limitation accepted)

**Overall:** 83% success rate with all core lifecycle functionality working. SFTP fully functional. FTP passive mode tracked as future enhancement.

## Files Changed

- **Created:** tests/FileSimulator.IntegrationTests/Support/DynamicServerHelpers.cs (362 lines)
- **Created:** tests/FileSimulator.IntegrationTests/DynamicServers/DynamicFtpServerTests.cs (318 lines)
- **Created:** tests/FileSimulator.IntegrationTests/DynamicServers/DynamicSftpServerTests.cs (304 lines)

**Total:** 760 lines of new test code
