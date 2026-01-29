# Coding Conventions

**Analysis Date:** 2026-01-29

## Naming Patterns

**Files:**
- Protocol service implementations: `{Protocol}FileService.cs` (e.g., `FtpFileService`, `SftpFileService`)
- Options/configuration classes: `{Protocol}ServerOptions.cs` (e.g., `FtpServerOptions`)
- Extension methods: `ServiceCollectionExtensions.cs`, `FilePollingExtensions.cs`
- Records for data transfer: `RemoteFileInfo`, `FileDiscoveredEvent`

**Functions:**
- Async operations: `{Action}Async` suffix (e.g., `DiscoverFilesAsync`, `ReadFileAsync`, `DownloadFileAsync`, `UploadFileAsync`)
- Synchronous operations: no suffix (e.g., `GetClient()`, `GetConnection()`)
- Initialization methods: `Get{Resource}` (e.g., `GetClientAsync`, `GetFtpClientAsync`)
- Test methods: static methods at module level, e.g., `TestFtpAsync`, `TestSftpAsync`, `DisplayResults`

**Variables:**
- Private fields with underscore prefix: `_client`, `_options`, `_logger`, `_serviceProvider`
- Local variables in camelCase: `result`, `content`, `filePath`, `endpointName`
- Loop variables in camelCase: `i`, `file`, `entry`, `item`
- Temp/state tracking: `_processedFiles`, `_connectionLock`

**Types:**
- Interfaces: `I{FunctionName}` prefix (e.g., `IFileProtocolService`, `IFileDiscoveryHandler`)
- Enum values: PascalCase (e.g., `FileProtocol.FTP`, `FileProtocol.SFTP`)
- Configuration classes: PascalCase, property-based (e.g., `FileSimulatorOptions`, `FtpServerOptions`)
- Records: PascalCase (e.g., `RemoteFileInfo`, `FileDiscoveredEvent`)

## Code Style

**Formatting:**
- Implicit usings enabled (`ImplicitUsings` in .csproj)
- Latest C# language version enabled (`LangVersion: latest`)
- Nullable reference types enabled (`Nullable: enable`)
- .NET 9.0 target framework
- 4-space indentation (standard C# convention)
- Opening braces on same line

**Linting:**
- No `.editorconfig` or `.eslintrc` found - using default .NET conventions
- No StyleCop configuration detected

## Import Organization

**Order:**
1. System and core namespaces: `System`, `System.Collections`, `System.Net`, etc.
2. External NuGet packages: `Amazon.S3`, `FluentFTP`, `Renci.SshNet`, `Microsoft.*`, etc.
3. Project namespaces: `FileSimulator.Client.*`

**Path Aliases:**
- No global using aliases or path aliases configured
- Full namespace paths used throughout

**Example from `FileProtocolServices.cs`:**
```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using SMBLibrary;
using SMBLibrary.Client;

namespace FileSimulator.Client.Services;
```

## Error Handling

**Patterns:**
- Throw `ArgumentException` for invalid protocol names or unknown endpoints
- Throw `IOException` for file operation failures with descriptive messages
- Throw `ArgumentNullException` for null constructor parameters
- Wrap protocol-specific errors in context: e.g., `Failed to download file: {remotePath}`
- Try-catch in job execution to prevent job failure from stopping scheduler
- Health checks return boolean false on exception rather than throwing

**Example from `FtpFileService`:**
```csharp
public async Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default)
{
    var client = await GetClientAsync(ct);
    using var stream = new MemoryStream();

    var success = await client.DownloadStream(stream, remotePath, token: ct);
    if (!success)
        throw new IOException($"Failed to download file: {remotePath}");

    _logger.LogInformation("FTP read {Size} bytes from {Path}", stream.Length, remotePath);
    return stream.ToArray();
}
```

**Pattern in FilePollingJob:**
```csharp
foreach (var handler in _handlers)
{
    try
    {
        await handler.HandleFileDiscoveredAsync(evt, context.CancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Handler {Handler} failed for file {File}",
            handler.GetType().Name, evt.FileInfo.Name);
    }
}
```

## Logging

**Framework:** `Microsoft.Extensions.Logging` (ILogger<T>)

**Patterns:**
- Injected via dependency injection: `ILogger<{ServiceName}> _logger`
- Log levels used consistently:
  - `LogInformation`: Successful operations (connect, read, write, delete)
  - `LogDebug`: File discovery results, polling status
  - `LogWarning`: Failed health checks, missing mounts
  - `LogError`: Operation failures, exception handling

**Examples from codebase:**
```csharp
// Information level
_logger.LogInformation("Connected to FTP server {Host}:{Port}", _options.Host, _options.Port);
_logger.LogInformation("FTP read {Size} bytes from {Path}", stream.Length, remotePath);

// Debug level
_logger.LogDebug("FTP discovered {Count} files in {Path}", files.Count(), path);
_logger.LogDebug("Polling endpoint: {EndpointName}", endpointName);

// Warning level
_logger.LogWarning(ex, "FTP health check failed");
_logger.LogWarning("NFS mount path does not exist: {Path}", _options.MountPath);

// Error level
_logger.LogError(ex, "Failed to poll endpoint {Name}", endpoint.Name);
_logger.LogError(ex, "Handler {Handler} failed for file {File}", handler.GetType().Name, evt.FileInfo.Name);
```

## Comments

**When to Comment:**
- XML documentation (`///`) for public interfaces, classes, and public methods
- No inline comments in implementation - code should be self-documenting
- Configuration/options classes have XML docs explaining purpose

**JSDoc/TSDoc:**
- Not used (C# uses XML documentation instead)

**Example from IFileProtocolService:**
```csharp
/// <summary>
/// Common interface for all file protocol operations
/// </summary>
public interface IFileProtocolService
{
    string ProtocolName { get; }

    /// <summary>
    /// Discover/list files in a directory (for polling)
    /// </summary>
    Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(string path, string? pattern = null, CancellationToken ct = default);

    /// <summary>
    /// Read/download a file
    /// </summary>
    Task<byte[]> ReadFileAsync(string remotePath, CancellationToken ct = default);

    // ... more methods with XML docs
}
```

## Function Design

**Size:**
- Protocol service methods are 5-50 lines
- Complex operations (SMB file reads) have chunking logic inline
- No artificial extraction to helper methods

**Parameters:**
- Always include `CancellationToken ct = default` for async operations
- Options passed via `IOptions<TOptions>` in constructors
- Paths as strings (no Path type abstraction)
- Pattern matching as nullable string: `string? pattern = null`

**Return Values:**
- File content as `byte[]` for memory reads
- File listings as `IEnumerable<RemoteFileInfo>` (lazy enumeration)
- Boolean `true`/`false` for health checks
- Task for fire-and-forget operations with side effects
- Records for structured return data

**Example function signature pattern:**
```csharp
public async Task<IEnumerable<RemoteFileInfo>> DiscoverFilesAsync(
    string path,
    string? pattern = null,
    CancellationToken ct = default)

public async Task<byte[]> ReadFileAsync(
    string remotePath,
    CancellationToken ct = default)

public async Task WriteFileAsync(
    string remotePath,
    byte[] content,
    CancellationToken ct = default)

public async Task<bool> HealthCheckAsync(
    CancellationToken ct = default)
```

## Module Design

**Exports:**
- Public interface: `IFileProtocolService` - implemented by each protocol service
- Public abstract class: None (uses inheritance from base implementations)
- Public records: `RemoteFileInfo`, `FileDiscoveredEvent`
- Public static extensions: `ServiceCollectionExtensions`, `FilePollingExtensions`
- Configuration classes: `*ServerOptions`, `FilePollingOptions`

**Barrel Files:**
- Namespace-based organization (no barrel export files)
- Each service lives in single file per protocol

**Region Organization:**
- `#region Interfaces` - at top for contracts
- `#region {Protocol} Service` - service implementation
- `#region Polling Configuration` - related types
- `#region Service Registration Extensions` - DI helpers

**Example from `FileProtocolServices.cs`:**
```csharp
#region Interfaces
// IFileProtocolService and RemoteFileInfo
#endregion

#region FTP Service
// FtpFileService and FtpServerOptions
#endregion

#region SFTP Service
// SftpFileService and SftpServerOptions
#endregion

// ... more protocols
```

## Connection Management

**Pattern:**
- Lazy initialization: client created on first use
- Connection pooling via semaphore for concurrent access
- Thread-safe access with `SemaphoreSlim(1, 1)` for mutual exclusion
- Auto-reconnection on disconnection
- Timeout configuration in client setup

**Example from `FtpFileService`:**
```csharp
private async Task<AsyncFtpClient> GetClientAsync(CancellationToken ct)
{
    await _connectionLock.WaitAsync(ct);
    try
    {
        if (_client == null || !_client.IsConnected)
        {
            _client?.Dispose();
            _client = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);
            _client.Config.ConnectTimeout = 30000;
            _client.Config.ReadTimeout = 60000;
            _client.Config.DataConnectionConnectTimeout = 30000;

            await _client.AutoConnect(ct);
            _logger.LogInformation("Connected to FTP server {Host}:{Port}", _options.Host, _options.Port);
        }
        return _client;
    }
    finally
    {
        _connectionLock.Release();
    }
}
```

## Resource Cleanup

**Pattern:**
- Implement `IDisposable` for protocol services holding resources
- Explicit resource disposal in `Dispose()` method
- Options classes are simple POCOs without disposal
- Logging/configuration cleanup handled by DI container

**Example:**
```csharp
public void Dispose()
{
    _client?.Dispose();
    _connectionLock.Dispose();
}
```

## Dependency Injection

**Convention:**
- Services registered as singletons for shared connection pooling
- Options registered via `services.Configure<T>(configuration.GetSection(...))`
- `ILogger<T>` injected into all services
- `IServiceProvider` injected into service orchestrators
- Extension methods on `IServiceCollection` for registration

**Example registration pattern:**
```csharp
public static IServiceCollection AddFtpFileService(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.Configure<FtpServerOptions>(configuration.GetSection("FileSimulator:Ftp"));
    services.AddSingleton<FtpFileService>();
    services.AddSingleton<IFileProtocolService>(sp => sp.GetRequiredService<FtpFileService>());
    return services;
}
```

---

*Convention analysis: 2026-01-29*
