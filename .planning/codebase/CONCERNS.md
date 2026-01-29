# Codebase Concerns

**Analysis Date:** 2026-01-29

## Tech Debt

**Hardcoded Default Credentials Across Codebase:**
- Issue: Default credentials are hardcoded as fallback values in multiple service files, making them exposed in source code and vulnerable to accidental exposure.
- Files:
  - `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 227, 399, 575-576, 723, 1017)
  - `src/FileSimulator.Client/FileSimulatorClient.cs` (lines 343, 349, 353-354, 359, 365, 382, 387, 390-391, 400)
  - `src/FileSimulator.TestConsole/Program.cs` (lines 89, 153, 258, 313-314, 386)
  - `helm-chart/file-simulator/values.yaml` (lines 42, 74, 107, 144, 209, 258)
- Impact: Production deployments may inherit weak credentials; source code history exposes secrets; violates secure credential management practices.
- Fix approach: Move all credentials to environment variables with no hardcoded defaults, or use Kubernetes Secrets exclusively. Update Options classes to require explicit configuration. Implement validation to fail fast if credentials are not provided.

**SFTP Service Uses Blocking Synchronous Calls Wrapped in Task.Run:**
- Issue: `SftpFileService` (lines 252-296) uses synchronous `GetClient()` and wraps operations in `Task.Run()` instead of using true async patterns. This defeats the purpose of async/await and can lead to thread pool starvation.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 252-273, 275-377)
- Impact: Poor scalability under concurrent load; thread pool exhaustion; performance degradation with high connection churn; inconsistent with async FTP implementation.
- Fix approach: Make `GetClient()` async, use `await` throughout, or use a synchronous method consistently. SSH.NET's SftpClient doesn't support async, so either: 1) Keep sync internally but expose async properly, or 2) Use alternative SSH library with async support.

**Memory Leak Risk: File Polling State Never Expires:**
- Issue: `FilePollingService._processedFiles` (line 68) uses an unbounded `ConcurrentDictionary<string, HashSet<string>>` to track processed files. For long-running services, this grows indefinitely with no TTL or cleanup mechanism.
- Files: `src/FileSimulator.Client/Services/FilePollingService.cs` (lines 68, 106-142)
- Impact: Memory usage increases linearly with files processed; no recovery in 24/7 deployments; potential OutOfMemoryException in production.
- Fix approach: Implement bounded tracking (e.g., last N files per endpoint, file age-based expiration), or persist processed state to external storage (database, Redis). Add memory monitoring and periodic cleanup.

**Regex Pattern Compilation Not Cached:**
- Issue: `MatchesPattern()` methods compile regex on every file comparison instead of caching compiled patterns.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 207-213, 379-385, 558-564, 703-709, 989-995)
- Impact: CPU overhead; performance impact on large file discovery operations; same pattern compiled hundreds of times.
- Fix approach: Use static `Regex` cache or `Regex.Match()` with `RegexOptions.Compiled` at method level. Consider using `Glob.Net` library instead.

**No Test Coverage for Core Libraries:**
- Issue: No unit test project exists for `FileSimulator.Client`. Only manual test console (`FileSimulator.TestConsole`) available, which requires running services.
- Files: No test files found in repository
- Impact: Regression risk when modifying protocol services; no CI/CD verification; integration bugs not caught before deployment; difficult to refactor safely.
- Fix approach: Create `FileSimulator.Client.Tests` project using xUnit or NUnit. Mock `IOptions<T>` and protocol services. Add tests for: connection retry logic, file pattern matching, error handling, concurrent access.

## Known Bugs

**NFS Server Export Fails on Windows-Mounted HostPath:**
- Symptoms: NFS pod crashes immediately with `exportfs: /data does not support NFS export` error.
- Files: `helm-chart/file-simulator/templates/nas.yaml`
- Trigger: Deploying NFS server pointing to Minikube's hostPath mounted from Windows (`/mnt/simulator-data`).
- Workaround: Apply patch to use `emptyDir` for NFS daemon state and separate PVC for shared data (documented in README.md lines 1521-1563). Manual post-deployment patch required.

**SMB NTLM Authentication Fails Through TCP Proxies:**
- Symptoms: Cannot connect to SMB share through `kubectl port-forward` or `minikube service`.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 759-761, comments on 1009-1011)
- Trigger: Using SMBLibrary with port forwarding instead of direct NodePort access.
- Workaround: Requires direct IP:port access (uses minikube tunnel) or in-cluster DNS. NodePort access works but SMB protocol requires standard port 445.

**HTTP/WebDAV List Discovery JSON Parsing Brittle:**
- Symptoms: If nginx JSON format changes or endpoint differs, `HttpFileService.DiscoverFilesAsync()` fails silently with `JsonSerializerException`.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 620, 716)
- Trigger: Nginx version update or custom nginx configuration.
- Workaround: No workaround; requires code change. No error handling around JSON deserialization.

## Security Considerations

**Credentials Stored in Helm Values and ConfigMaps:**
- Risk: Helm values.yaml and appsettings.json contain plaintext passwords. If committed to version control or Helm history is accessible, credentials are exposed.
- Files:
  - `helm-chart/file-simulator/values.yaml` (all auth blocks)
  - `src/FileSimulator.TestConsole/appsettings*.json`
  - `src/FileSimulator.Client/Examples/appsettings*.json`
- Current mitigation: None. Passwords visible in source code, Helm history, ConfigMap spec.
- Recommendations:
  1. Use Kubernetes Secrets exclusively for credentials
  2. Update Helm templates to reference Secret keys, not values.yaml
  3. Implement kube-secrets or external-secrets operator
  4. Add `.gitignore` entries for appsettings.*.json
  5. Use sealed-secrets or similar for GitOps workflows

**No Authentication Between Microservice and Simulator:**
- Risk: Any pod in any namespace can access file simulator services. No RBAC or service-to-service authentication enforced.
- Files: All Helm service templates lack authentication
- Current mitigation: Network policies (not configured by default)
- Recommendations:
  1. Implement mTLS between clients and simulator services
  2. Add NetworkPolicies to restrict traffic by namespace
  3. Use OAuth2 proxy or similar for HTTP/WebDAV access
  4. Require API tokens for programmatic access

**HTTP Credentials Sent Over Plain HTTP:**
- Risk: `HttpFileService` sends credentials in plaintext if BaseUrl is http:// (lines 603, 606-609).
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 595-610)
- Current mitigation: `HttpServerOptions` requires explicit BaseUrl configuration; no default HTTPS enforcement.
- Recommendations: Force HTTPS in production, validate protocol in service, add certificate pinning, or use token-based auth instead of basic auth.

## Performance Bottlenecks

**FTP/SFTP File Discovery N+1 Pattern:**
- Problem: Each protocol's `DiscoverFilesAsync()` lists files then downloads their timestamps individually (especially SFTP with no bulk metadata).
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 118-137 FTP, 275-297 SFTP)
- Cause: SFTP doesn't support efficient bulk metadata retrieval; FTP requires multiple operations.
- Improvement path: Cache file listings between polls, implement server-side filtering where possible, reduce poll frequency, or batch metadata requests.

**S3 ListObjectsV2 Pagination Not Optimized:**
- Problem: `S3FileService.DiscoverFilesAsync()` (lines 432-471) lists ALL objects even if only a few match the pattern.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 432-471)
- Cause: No server-side prefix filtering; pattern matching happens client-side on all objects.
- Improvement path: Use S3 prefix optimization, implement pagination limits, add lazy evaluation, cache results with TTL.

**Unbounded Memory for Large File Reads:**
- Problem: `ReadFileAsync()` methods load entire file into `MemoryStream` (FTP: line 142, SFTP: line 304, S3: line 478, SMB: line 867).
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (multiple locations)
- Cause: No streaming support; array buffer limits in methods.
- Improvement path: Implement chunked streaming for large files, add size limit checks, use `IAsyncEnumerable<byte[]>` for streaming, implement progress callbacks.

## Fragile Areas

**Connection Management Inconsistent Across Protocols:**
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (79-220 FTP, 237-392 SFTP, 409-570 S3, 587-716 HTTP, 733-1003 SMB, 1028-1133 NFS)
- Why fragile: FTP uses async GetClientAsync with lock, SFTP uses sync GetClient wrapping in Task.Run, S3 creates client once in constructor, HTTP creates client once, SMB uses sync GetConnection in Task.Run, NFS checks directory existence on every call. Any refactoring of one affects portability.
- Safe modification: Extract connection logic to shared interface or base class, implement consistent async patterns, add integration tests for each protocol before refactoring.
- Test coverage: Manual test console only; no unit tests for connection retry logic or concurrent access patterns.

**File Polling State Not Persistent:**
- Files: `src/FileSimulator.Client/Services/FilePollingService.cs` (lines 68-143)
- Why fragile: Processed file tracking only in-memory. Pod restart resets state; same files reprocessed. No transaction semantics; race conditions if handlers fail.
- Safe modification: Require external state store before increasing polling frequency; add deterministic idempotency to handlers.
- Test coverage: No tests for polling state management or handler failure scenarios.

**SFTP Service Blocking Calls in Task.Run:**
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (lines 237-392)
- Why fragile: Pattern of wrapping sync in Task.Run is error-prone. Thread pool starvation risk under load. SSH.NET doesn't support true async natively.
- Safe modification: Either commit to sync pattern with semaphore-based pooling, or switch to async SSH library. Don't mix patterns.
- Test coverage: No load tests verifying thread pool behavior or concurrent connection limits.

## Scaling Limits

**In-Memory Processed Files Dictionary:**
- Current capacity: Unbounded; limited by available memory.
- Limit: At 1000 files/hour per endpoint, a 10GB container with 50% memory overhead would process ~50M files before OOMKill. In practice, hits limits much sooner due to .NET heap fragmentation.
- Scaling path: Implement Redis or database-backed processed file tracker, add expiration strategy (file age-based), or partition state by endpoint.

**Kubernetes Storage for All Protocols:**
- Current capacity: Configured PVC size (default 10Gi in values.yaml).
- Limit: Shared across all 6 protocols; no per-protocol limits. First protocol to fill storage blocks all others.
- Scaling path: Implement multi-tier storage (hot/cold), add storage quota per protocol, implement cleanup/archival jobs, monitor usage alerting.

**Connection Pool Saturation:**
- Current capacity: FTP/SFTP use single SemaphoreSlim(1); SMB uses single connection; HTTP creates unlimited HttpClients if not pooled.
- Limit: Cannot serve more than 1 concurrent operation per protocol (except HTTP). Burst traffic queues behind semaphore.
- Scaling path: Implement connection pools with configurable size, add queue backpressure, implement adaptive pooling based on load.

## Dependencies at Risk

**SSH.NET (Renci.SshNet) Version 2024.1.0:**
- Risk: Version 2024.1.0 may have breaking changes from older versions. Library not on active roadmap; maintenance minimal. No async support forces workarounds.
- Impact: SFTP operations; no async compatibility with modern patterns; if vulnerabilities found, upgrade difficult due to API changes.
- Migration plan: Evaluate libssh2 bindings or SSH.NET fork with async support. Consider async-first library like OpenSSH or paramiko ports.

**FluentFTP Version 50.0.1:**
- Risk: Relatively recent major version. Async API relatively new. Edge cases in data connection handling reported in issues.
- Impact: FTP operations; data corruption risk on passive mode transfers in certain network conditions.
- Migration plan: Monitor release notes; test with production file types and sizes; consider fallback to synchronous FTP if issues arise.

**SMBLibrary (No Official Support):**
- Risk: SMBLibrary is community-maintained, not officially supported. NTLM authentication has known issues with proxies. No async API.
- Impact: SMB connectivity through proxies fails; performance limitations; security fixes delayed.
- Migration plan: Evaluate SMB.net or switch to mount-based access (Windows.Storage API on Windows host). Document proxy limitations in deployment guide.

**NFS via Mounted Filesystem:**
- Risk: No NFS client library; relies on OS-level mount. Mount can break silently; no connection health checks in code.
- Impact: NFS service failures not detected by code; health checks only verify directory existence (line 1128).
- Migration plan: Implement NFS client library (NFSv4 .NET bindings), add connection state tracking, improve health check granularity.

## Missing Critical Features

**No Encryption for Data in Transit:**
- Problem: FTP, SFTP passwords sent with no TLS enforcement on HTTP. SMB traffic unencrypted.
- Blocks: Compliance (HIPAA, PCI-DSS); production deployments in untrusted networks.

**No Audit Logging:**
- Problem: Operations logged at DEBUG level; no structured audit trail of who accessed what files. No timestamps in operations.
- Blocks: Compliance; forensic analysis; security incident investigation.

**No Rate Limiting:**
- Problem: No throttling on file operations, discovery, or polling. Malicious or misconfigured client can overwhelm services.
- Blocks: Production multi-tenant scenarios; DoS protection.

## Test Coverage Gaps

**Protocol Service Connection Retry Logic:**
- What's not tested: Reconnection on network failure, concurrent access under lock contention, connection timeout behavior, graceful degradation.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (all GetClient methods)
- Risk: Retry storms, connection pool deadlocks, cascading failures not detected until production.
- Priority: High

**File Polling State Management:**
- What's not tested: Processed file tracking accuracy, memory growth over time, endpoint isolation (no cross-contamination), handler exception recovery.
- Files: `src/FileSimulator.Client/Services/FilePollingService.cs`
- Risk: Reprocessing of files, memory leaks, polling stalls.
- Priority: High

**Pattern Matching Edge Cases:**
- What's not tested: Patterns with special characters, Unicode filenames, empty patterns, null patterns, case sensitivity across protocols.
- Files: `src/FileSimulator.Client/Services/FileProtocolServices.cs` (all MatchesPattern methods)
- Risk: Files missed or matched incorrectly; inconsistent behavior across protocols.
- Priority: Medium

**Error Handling End-to-End:**
- What's not tested: Network failures during operations, partial file transfers, auth failures, malformed responses, cascading errors.
- Files: All service files
- Risk: Unhandled exceptions, incomplete state transitions, resource leaks.
- Priority: High

**Cross-Protocol File Sharing Consistency:**
- What's not tested: Same file uploaded via FTP, modified via HTTP, read via SMB. Consistency guarantees. Concurrent writes from multiple protocols.
- Files: Cross-protocol integration
- Risk: Data corruption, inconsistent state, lost updates.
- Priority: Medium

**Load and Stress Testing:**
- What's not tested: File discovery with 10K+ files, concurrent connections to multiple protocols, sustained 100+ ops/sec, large file handling (>1GB).
- Files: All protocol services
- Risk: Performance issues, connection exhaustion, memory corruption not detected in manual testing.
- Priority: Medium

---

*Concerns audit: 2026-01-29*
