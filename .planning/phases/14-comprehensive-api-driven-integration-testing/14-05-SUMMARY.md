---
phase: 14-comprehensive-api-driven-integration-testing
plan: 05
subsystem: integration-testing
tags: [dynamic-servers, nas, nfs, lifecycle-testing, api-testing]

requires:
  - "14-01: Test infrastructure with fixture and helpers"

provides:
  - "Dynamic NAS server lifecycle test coverage"
  - "TCP connectivity validation for NFS"
  - "Platform-aware file operations testing"

affects:
  - "14-06: Future dynamic server tests will follow same patterns"

tech-stack:
  added: []
  patterns:
    - "DynamicServerHelpers usage for server management"
    - "TCP connectivity testing with retry policies"
    - "Graceful test skipping when platform features unavailable"

key-files:
  created:
    - "tests/FileSimulator.IntegrationTests/DynamicServers/NasServerLifecycleTests.cs"

decisions:
  - id: use-dynamic-server-helpers
    status: implemented
    rationale: "Reuse existing DynamicServerHelpers instead of duplicating server management logic"

  - id: tcp-only-connectivity
    status: implemented
    rationale: "NFS requires mount which may not be available in all test environments. TCP connectivity is always testable."

  - id: optional-file-operations
    status: implemented
    rationale: "File operations require Windows mount at C:\\simulator-data\\{servername}. Test gracefully skips if unavailable."

metrics:
  duration: "25 minutes"
  completed: "2026-02-05"
  tests-added: 7
---

# Phase 14 Plan 05: Dynamic NAS Server Lifecycle Tests Summary

**One-liner:** Complete lifecycle tests for dynamic NAS servers with TCP connectivity and optional file operations

## What Was Built

Implemented comprehensive integration tests for dynamic NAS (NFS) server lifecycle management via the Control API:

### Test Coverage (7 tests)

1. **NasServer_Create_ReturnsServerInfo**
   - Creates NAS server via API
   - Validates response contains name, host, port
   - Verifies cleanup in finally block

2. **NasServer_BecomesReady_WithinTimeout**
   - Creates NAS server with directory: "input/test-dynamic-nas"
   - Polls until Status="Running" and PodReady=true
   - Validates 60-second timeout sufficient for readiness

3. **NasServer_TcpPort_IsAccessible**
   - Creates and waits for NAS server
   - Tests TCP connectivity to NFS port using TcpClient
   - Uses RetryPolicies.TcpConnectivityPolicy for resilience
   - Always passes when server is ready (platform-independent)

4. **NasServer_ReadOnly_ConfigurationApplied**
   - Creates NAS server with readOnly: true
   - Validates server accepts configuration
   - Confirms server becomes running

5. **NasServer_Delete_CleansUpResources**
   - Creates and readies NAS server
   - Deletes server via API
   - Polls until server returns 404 (fully deleted)
   - Validates cleanup completes within 60 seconds

6. **NasServer_CompleteLifecycle**
   - End-to-end test: Create → Wait → TCP Connect → Delete
   - Validates full lifecycle in single test
   - Confirms each step succeeds

7. **NasServer_FileOperations_WhenMountAvailable**
   - Checks if mount path exists: C:\\simulator-data\\{serverName}
   - If unavailable: Gracefully skips with console message
   - If available: Write file, read file, verify content, delete file
   - Includes retry logic for Minikube mount sync delays
   - Platform-aware testing

### Key Patterns Established

**Server Management:**
```csharp
var server = await DynamicServerHelpers.CreateNasServerAsync(
    _fixture.ApiClient,
    serverName,
    "input/test-dynamic-nas");

var status = await DynamicServerHelpers.WaitForServerReadyAsync(
    _fixture.ApiClient,
    serverName,
    TimeSpan.FromSeconds(60));

await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
```

**TCP Connectivity with Retry:**
```csharp
var policy = RetryPolicies.TcpConnectivityPolicy(maxAttempts: 5, delaySeconds: 2);
var connected = await policy.ExecuteAsync(async () =>
{
    return await TestTcpConnectivityAsync(server.Host, server.Port);
});
connected.Should().BeTrue($"Should be able to connect to NFS port");
```

**Cleanup Safety:**
```csharp
var serverName = TestHelpers.GenerateUniqueServerName("nas");
try
{
    // test logic
}
finally
{
    await DynamicServerHelpers.DeleteServerAsync(_fixture.ApiClient, serverName);
}
```

**Graceful Platform-Aware Testing:**
```csharp
if (!Directory.Exists(mountPath))
{
    Console.WriteLine($"[Skip] NAS mount path not available. Skipping file operations test.");
    return; // Test passes but skips file operations
}
// Proceed with file operations...
```

## Technical Approach

### NAS-Specific Considerations

**NFS Port Testing:**
- NFS protocol uses TCP (port 2049 or NodePort mapped)
- TCP connectivity confirms server is accepting connections
- More reliable than attempting NFS mount in test environment

**Directory Configuration:**
- Dynamic NAS servers support: "input", "output", "backup"
- Directory path: C:\\simulator-data\\{serverName}
- May take several seconds for Minikube mount to sync

**Read-Only Support:**
- NAS servers accept readOnly: true configuration
- Passed to Helm as server configuration
- Test validates API accepts parameter (server behavior tested separately)

### Test Infrastructure Usage

**Reused Components:**
- DynamicServerHelpers: Server CRUD operations
- TestHelpers: Unique name generation, test content creation
- RetryPolicies: TcpConnectivityPolicy for resilient connectivity tests
- SimulatorCollectionFixture: Shared HTTP client

**No Duplicated Code:**
- ServerCreationResponse defined in DynamicServerHelpers
- ServerStatusResponse defined in DynamicServerHelpers
- Server lifecycle methods centralized
- Cleanup logic consistent across all tests

## Testing Notes

### Cannot Run Yet

The test project has pre-existing compilation errors in unrelated Protocol test files:
- FtpProtocolTests.cs: FluentFTP API changes
- SftpProtocolTests.cs: Missing types
- SmbProtocolTests.cs: Missing types and API changes
- NfsProtocolTests.cs: Missing Xunit.Skip references

These are from previous incomplete work and block compilation.

### Verification Performed

✅ Code structure follows established patterns
✅ Uses DynamicServerHelpers consistently
✅ All tests have try/finally cleanup
✅ Unique server names prevent conflicts
✅ TCP connectivity test is platform-independent
✅ File operations test skips gracefully when mount unavailable
✅ Retry policies used for resilient connectivity testing

### When Tests Can Run

Tests will execute once pre-existing Protocol test compilation errors are fixed. Expected behavior:
- All 7 tests should pass when file-simulator deployed
- File operations test may skip if mount not available
- Each test should complete within 90 seconds
- No orphaned servers left after test suite

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Reused DynamicServerHelpers**
- **Found during:** Task implementation
- **Issue:** Plan suggested creating helper methods locally, but DynamicServerHelpers already existed with better implementation
- **Fix:** Used existing DynamicServerHelpers.CreateNasServerAsync, WaitForServerReadyAsync, DeleteServerAsync, GetServerStatusAsync
- **Files modified:** NasServerLifecycleTests.cs
- **Commit:** 2f1a1f0

**2. [Rule 2 - Missing Critical] Removed duplicate model classes**
- **Found during:** Initial compilation attempt
- **Issue:** ServerCreationResponse and ServerStatusResponse already defined in DynamicServerHelpers.cs
- **Fix:** Removed duplicate class definitions from test file
- **Files modified:** NasServerLifecycleTests.cs
- **Commit:** 2f1a1f0

## Files Created

```
tests/FileSimulator.IntegrationTests/
└── DynamicServers/
    └── NasServerLifecycleTests.cs (302 lines)
        - 7 test methods
        - 1 helper method (TestTcpConnectivityAsync)
        - Complete lifecycle coverage
```

## Next Phase Readiness

### Blockers

**Pre-existing compilation errors in Protocol tests:**
- Must be fixed before running any integration tests
- Not related to this plan's work
- Affects entire test project compilation

### Ready For

- 14-06: Can proceed with additional dynamic server tests (if needed)
- Other phase 14 plans once Protocol tests are fixed

### Lessons for Future Plans

1. **Reuse over duplication:** Check for existing helpers before creating new ones
2. **Platform-aware testing:** Design tests to skip gracefully when platform features unavailable
3. **Cleanup discipline:** Always use try/finally for resource cleanup
4. **Retry policies:** Use for all network connectivity tests
5. **Compilation hygiene:** Pre-existing errors block all new work - fix or remove broken tests

## Completion Evidence

**Git commit:** 2f1a1f0
**Files added:** 1
**Lines added:** 302
**Test coverage:** 7 lifecycle tests for dynamic NAS servers
**Pattern compliance:** ✅ Uses established helpers and patterns
**Cleanup safety:** ✅ All tests use try/finally blocks
