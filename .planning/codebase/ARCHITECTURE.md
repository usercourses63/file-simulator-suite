# Architecture

**Analysis Date:** 2026-01-29

## Pattern Overview

**Overall:** Multi-protocol abstraction layer with unified interface

**Key Characteristics:**
- Adapter pattern: Each protocol (FTP, SFTP, S3, HTTP, SMB, NFS) has a dedicated service implementing common interface
- Dependency injection first: All services registered via extension methods for ASP.NET Core integration
- Connection pooling: Each protocol service manages its own connection lifecycle with thread-safe access via `SemaphoreSlim`
- Event-driven polling: Optional Quartz-based file discovery system that raises events on new files
- Environment-driven configuration: Supports Minikube, in-cluster Kubernetes, and localhost modes

## Layers

**Protocol Services:**
- Purpose: Handle protocol-specific file operations (read, write, list, delete)
- Location: `src/FileSimulator.Client/Services/FileProtocolServices.cs`
- Contains: `IFileProtocolService` interface, `FtpFileService`, `SftpFileService`, `S3FileService`, `HttpFileService`, `SmbFileService`, `NfsFileService`
- Depends on: Protocol-specific clients (FluentFTP, SSH.NET, AWSSDK.S3, HttpClient, SMBLibrary)
- Used by: `FileSimulatorClient`, `FilePollingService`, consumer applications

**Unified Client:**
- Purpose: Provide legacy single-instance protocol operations for backward compatibility
- Location: `src/FileSimulator.Client/FileSimulatorClient.cs`
- Contains: Per-protocol operation methods (`UploadViaFtpAsync`, `DownloadViaS3Async`, etc.), generic `UploadAsync`/`DownloadAsync` with protocol enum
- Depends on: All protocol client libraries, `FileSimulatorOptions`
- Used by: Existing applications that import the library

**Polling Service:**
- Purpose: Discover files at scheduled intervals and raise events for processing
- Location: `src/FileSimulator.Client/Services/FilePollingService.cs`
- Contains: `FilePollingService`, `FilePollingJob` (Quartz integration), `FileDiscoveredEvent`, `IFileDiscoveryHandler` interface
- Depends on: Protocol services, Quartz scheduler, logging
- Used by: Applications requiring automatic file discovery

**Dependency Injection Extensions:**
- Purpose: Register services in ASP.NET Core DI container
- Location: `src/FileSimulator.Client/Extensions/ServiceCollectionExtensions.cs`
- Contains: `AddFileSimulator*` methods for each protocol, combined registration methods, health check setup
- Depends on: Microsoft.Extensions.DependencyInjection, Polly for retry policies
- Used by: Application startup code

**Options/Configuration:**
- Purpose: Hold protocol connection details and polling configuration
- Location: `FileProtocolServices.cs` (FtpServerOptions, SftpServerOptions, etc.), `FileSimulatorClient.cs` (FileSimulatorOptions), `FilePollingService.cs` (FilePollingOptions)
- Contains: Per-protocol options classes, global `FileSimulatorOptions` with all 6 protocols, `FilePollingEndpoint` configuration
- Used by: All service classes

## Data Flow

**File Read Operation (with protocol abstraction):**

1. Application receives read request with protocol specification
2. Obtains `IFileProtocolService` instance for protocol (via DI or direct creation)
3. Calls `ReadFileAsync(remotePath, cancellationToken)`
4. Service establishes connection (or reuses via connection lock)
5. Returns byte array to application

**File Polling with Discovery:**

1. Quartz scheduler fires `FilePollingJob` per configured cron expression
2. Job calls `FilePollingService.PollEndpointAsync(endpointName)`
3. Service retrieves configured `FilePollingEndpoint` and gets appropriate protocol service
4. Calls `DiscoverFilesAsync(path, filePattern)` on protocol service
5. For each discovered file not in processed cache:
   - Creates `FileDiscoveredEvent`
   - Invokes all registered `IFileDiscoveryHandler` implementations
   - Handler can download file, move it, delete it, etc.
6. Files marked as processed to prevent re-notification

**Configuration Resolution:**

1. `FileSimulatorOptions` constructor parameters populate from `IConfiguration` section
2. Environment variables override config file values
3. Factory methods (`ForMinikube()`, `ForCluster()`) pre-configure based on deployment context
4. Individual protocol options resolved via `IOptions<TProtocolOptions>` pattern

**State Management:**

- **Connection state:** Each protocol service maintains single connection instance with lazy initialization
- **Processed files:** `FilePollingService` uses `ConcurrentDictionary<endpointName, HashSet<filePath>>` for in-memory tracking
- **Thread safety:** `SemaphoreSlim` gates connection creation/reuse in each protocol service

## Key Abstractions

**IFileProtocolService:**
- Purpose: Abstract protocol differences behind common interface
- Examples: `FtpFileService`, `SftpFileService`, `S3FileService`, `HttpFileService`, `SmbFileService`, `NfsFileService`
- Pattern: Each implements 8 async methods (`DiscoverFilesAsync`, `ReadFileAsync`, `DownloadFileAsync`, `WriteFileAsync`, `UploadFileAsync`, `DeleteFileAsync`, `HealthCheckAsync`)

**RemoteFileInfo:**
- Purpose: Protocol-agnostic file metadata representation
- Pattern: Record with required `FullPath`, `Name`, optional `Size`, `ModifiedAt`, `IsDirectory`
- Used by: Discovery results, passed to event handlers

**FileDiscoveredEvent:**
- Purpose: Notification event for polling-based discovery
- Pattern: Contains endpoint name, protocol, file info, discovered timestamp
- Used by: Event handlers to react to new files

**FilePollingEndpoint:**
- Purpose: Configuration for one polling source
- Pattern: Specifies protocol, path, file pattern, cron schedule, post-processing behavior

## Entry Points

**FileSimulatorClient (legacy):**
- Location: `src/FileSimulator.Client/FileSimulatorClient.cs`
- Triggers: Direct instantiation with `FileSimulatorOptions`
- Responsibilities: Protocol selection via enum, connection management for backward compatibility

**Protocol Services (modern):**
- Location: `src/FileSimulator.Client/Services/FileProtocolServices.cs`
- Triggers: Dependency injection via `IFileProtocolService` or specific service type
- Responsibilities: Single protocol operation, connection pooling, error handling

**FilePollingService:**
- Location: `src/FileSimulator.Client/Services/FilePollingService.cs`
- Triggers: Quartz scheduler or manual call to `PollEndpointAsync()`
- Responsibilities: Coordinate discovery across endpoints, track processed files, invoke handlers

**TestConsole Application:**
- Location: `src/FileSimulator.TestConsole/Program.cs`
- Triggers: Manual execution with optional `--cross-protocol` flag
- Responsibilities: Test each protocol with upload/list/download/delete, report performance metrics

## Error Handling

**Strategy:** Protocol-first error handling with logging

**Patterns:**

- **Connection failures:** Each service catches connection errors, logs warning, returns false on `HealthCheckAsync()`. No automatic reconnect on read/write - caller must retry
- **File operation failures:** Throw `IOException` with context (protocol, path, status code). Caller decides retry logic
- **Parsing/format errors:** S3 path parsing handles empty keys, HTTP expects JSON response. Malformed data raises exception
- **Polling errors:** `FilePollingService.PollEndpointAsync()` catches and logs endpoint failures, continues polling other endpoints
- **Thread safety:** All connection access protected by `SemaphoreSlim` with configurable timeout (default 30sec for FTP)

**Logging levels:**
- `LogInformation`: Successful operations (connection, upload, download)
- `LogDebug`: Discovery results, batch operations
- `LogWarning`: Health check failures, connection loss
- `LogError`: Poll endpoint failures

## Cross-Cutting Concerns

**Logging:** ILogger injected into each service. Files use pattern `{Protocol} {operation} {path}` (e.g., "FTP uploaded /output/file.txt")

**Validation:**
- Path validation (SMB converts `/` to `\`, S3 parses bucket/key)
- File pattern matching: `MatchesPattern()` converts glob (`*`, `?`) to regex
- Null/empty checks on required config values

**Authentication:**
- FTP/SFTP: Username/password stored in options, connection includes credentials
- S3: Access key/secret key, endpoint URL for MinIO support
- HTTP: Optional BasicAuth credentials
- SMB: Domain/username/password, custom port support for NodePort
- NFS: Mount path only (uses OS-level auth via mount)

**Health Checks:** Separate `HealthCheckAsync()` on each service, registered via `AddFileSimulatorHealthChecks()` extension

**Resource Cleanup:** All services implementing `IDisposable`. FTP/SFTP/S3/HTTP clients disposed. SMB disconnects. NFS has no cleanup.
