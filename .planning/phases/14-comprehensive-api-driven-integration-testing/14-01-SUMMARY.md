---
phase: 14-comprehensive-api-driven-integration-testing
plan: 01
subsystem: testing
tags: [xunit, integration-tests, polly, fluent-assertions]

dependency-graph:
  requires: []
  provides: [integration-test-framework, collection-fixture, retry-policies]
  affects: [14-02, 14-03, 14-04, 14-05, 14-06, 14-07, 14-08, 14-09]

tech-stack:
  added: [JunitXml.TestLogger, Polly, Polly.Extensions]
  patterns: [collection-fixture, retry-policy]

key-files:
  created:
    - tests/FileSimulator.IntegrationTests/FileSimulator.IntegrationTests.csproj
    - tests/FileSimulator.IntegrationTests/appsettings.test.json
    - tests/FileSimulator.IntegrationTests/xunit.runner.json
    - tests/FileSimulator.IntegrationTests/Fixtures/SimulatorCollectionFixture.cs
    - tests/FileSimulator.IntegrationTests/Fixtures/SimulatorCollection.cs
    - tests/FileSimulator.IntegrationTests/Support/RetryPolicies.cs
    - tests/FileSimulator.IntegrationTests/Support/TestHelpers.cs
    - tests/FileSimulator.IntegrationTests/Models/ConnectionInfo.cs
    - tests/FileSimulator.IntegrationTests/SmokeTests.cs
  modified: []

decisions:
  - id: dec-14-01-01
    desc: "Non-parallel test execution for shared infrastructure"
    why: "Tests share Kubernetes infrastructure; parallel execution causes conflicts"
  - id: dec-14-01-02
    desc: "ConnectionInfo model uses servers array with GetServer helper methods"
    why: "API returns servers as array, not per-protocol objects"

metrics:
  duration: 6 min
  completed: 2026-02-05
---

# Phase 14 Plan 01: Test Infrastructure Setup Summary

**One-liner:** xUnit test project with collection fixture validating Control API connectivity and Polly retry policies for resilient integration testing.

## What Changed

### Integration Test Project Created
- Created `FileSimulator.IntegrationTests` xUnit project targeting .NET 9
- Added NuGet packages: xunit 2.9.0, FluentAssertions 6.12.2, Polly 8.5.2, JunitXml.TestLogger 4.0.254
- Added protocol client packages for future tests: FluentFTP, SSH.NET, AWSSDK.S3, SMBLibrary, Confluent.Kafka
- Configuration in `appsettings.test.json` with timeouts and retry settings

### Collection Fixture Infrastructure
- `SimulatorCollectionFixture` validates API connectivity on construction
- Provides shared `HttpClient` with configured timeout (30s)
- Caches `ConnectionInfoResponse` for efficient test access
- Throws `InvalidOperationException` if API unreachable (fail-fast)

### Retry Policies
- `HttpRetryPolicy`: Exponential backoff (2, 4, 8 seconds) for HTTP requests
- `ServerReadinessPolicy`: 30 attempts with 2-second delay for dynamic server polling
- `TcpConnectivityPolicy`: 5 attempts with 1-second delay for TCP connections

### Test Helpers
- `GenerateUniqueFileName(prefix)`: Creates unique test filenames with GUID
- `GenerateUniqueServerName(type)`: Creates K8s-compliant names (max 20 chars)
- `WaitForServerReadyAsync`: Polls server API until PodReady=true and Status=Running
- `GetConnectionInfoAsync`: Fetches and deserializes connection info

### Smoke Tests (12 tests)
- API health check validation
- Connection info response structure validation
- Protocol server presence (FTP, SFTP, HTTP, S3, SMB, NFS)
- Endpoints configuration validation
- Retry policy execution verification
- Test helper unit tests

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ConnectionInfo model mismatch with actual API response**
- **Found during:** Task 3 verification
- **Issue:** Plan assumed per-protocol objects (Ftp, Sftp, etc.), but API returns `servers` array
- **Fix:** Rewrote ConnectionInfoResponse with `List<ServerInfo>` and helper methods (GetServer, GetServers, GetServerByName)
- **Files modified:** Models/ConnectionInfo.cs, SmokeTests.cs
- **Commit:** 477aa38

## Decisions Made

| ID | Decision | Why |
|----|----------|-----|
| dec-14-01-01 | Non-parallel test execution | Tests share Kubernetes infrastructure; parallel execution would cause conflicts |
| dec-14-01-02 | servers array with helper methods | Aligns with actual API response structure, provides convenient lookup methods |

## Commits

| Hash | Message |
|------|---------|
| be7cff4 | feat(14-01): create xUnit integration test project with dependencies |
| 9ae0c2f | feat(14-01): add collection fixture and retry policies infrastructure |
| 9872a56 | test(14-01): add smoke tests to validate fixture and API connectivity |
| 477aa38 | fix(14-01): align ConnectionInfo model with actual API response structure |

## Test Results

```
Passed!  - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 127 ms
```

All 12 smoke tests pass against running simulator:
- ApiHealth_ReturnsSuccess
- ConnectionInfo_ReturnsValidResponse
- ConnectionInfo_HasFtpServer
- ConnectionInfo_HasSftpServer
- ConnectionInfo_HasHttpServer
- ConnectionInfo_HasS3Server
- ConnectionInfo_HasSmbServer
- ConnectionInfo_HasNfsServers
- ConnectionInfo_HasEndpoints
- RetryPolicy_HandlesTransientFailures
- TestHelpers_GenerateUniqueFileName_CreatesUniqueNames
- TestHelpers_GenerateUniqueServerName_RespectsLengthLimit

## Next Phase Readiness

**Ready for 14-02:** Test infrastructure is complete and all smoke tests pass.

**Dependencies provided:**
- SimulatorCollectionFixture for shared HttpClient and connection info
- RetryPolicies for resilient protocol testing
- ConnectionInfoResponse model with server lookup helpers
- TestHelpers for file/server name generation

**No blockers identified.**
