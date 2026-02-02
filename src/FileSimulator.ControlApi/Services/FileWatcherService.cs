namespace FileSimulator.ControlApi.Services;

using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Hubs;
using FileSimulator.ControlApi.Models;
using System.Collections.Concurrent;

/// <summary>
/// Background service that monitors the simulator-data directory for file changes
/// and broadcasts events via SignalR. Uses polling as the primary mechanism
/// (more reliable on 9p/NFS mounts) with FileSystemWatcher as a supplementary source.
/// </summary>
public class FileWatcherService : BackgroundService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IHubContext<FileEventsHub> _hubContext;
    private readonly ILogger<FileWatcherService> _logger;

    // Polling state
    private readonly ConcurrentDictionary<string, FileSnapshot> _fileSnapshots = new();
    private const int PollIntervalMs = 2000; // 2 second polling interval

    // FileSystemWatcher (supplementary)
    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, Timer> _debouncers = new();
    private const int DebounceDelayMs = 500;

    // Event history
    private readonly object _bufferLock = new();
    private readonly List<FileEventDto> _eventHistory = new();
    private const int MaxHistorySize = 50;

    // Linux container path (mounted from Windows C:\simulator-data via Minikube)
    private string _basePath = "/mnt/simulator-data";

    private record FileSnapshot(string Path, long Size, DateTime Modified, bool IsDirectory);

    public FileWatcherService(
        IConfiguration configuration,
        IHubContext<FileEventsHub> hubContext,
        ILogger<FileWatcherService> logger)
    {
        _configuration = configuration;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Get base path from configuration (Linux path in container)
        _basePath = _configuration["FileWatcher:Path"] ?? "/mnt/simulator-data";

        if (!Directory.Exists(_basePath))
        {
            _logger.LogWarning(
                "FileWatcher base path does not exist: {Path}. Monitoring disabled.",
                _basePath);
            return;
        }

        _logger.LogInformation(
            "Starting FileWatcher on path: {Path} (polling mode)",
            _basePath);

        // Take initial snapshot
        TakeSnapshot();
        _logger.LogInformation(
            "Initial snapshot captured: {Count} files/directories",
            _fileSnapshots.Count);

        // Try to start FileSystemWatcher as supplementary source
        TryStartFileSystemWatcher();

        _logger.LogInformation(
            "FileWatcher started successfully. Poll interval: {PollMs}ms",
            PollIntervalMs);

        // Polling loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollIntervalMs, stoppingToken);
                DetectChanges();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling cycle");
            }
        }
    }

    private void TakeSnapshot()
    {
        _fileSnapshots.Clear();

        try
        {
            foreach (var entry in EnumerateFileSystem(_basePath))
            {
                _fileSnapshots[entry.Path] = entry;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error taking file system snapshot");
        }
    }

    private IEnumerable<FileSnapshot> EnumerateFileSystem(string path)
    {
        var results = new List<FileSnapshot>();

        // Enumerate top-level directories first
        string[] topDirs;
        try
        {
            topDirs = Directory.GetDirectories(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error listing top-level directories");
            return results;
        }

        foreach (var topDir in topDirs)
        {
            // Skip ignored directories at top level
            if (ShouldIgnorePath(topDir)) continue;

            try
            {
                var dirInfo = new DirectoryInfo(topDir);
                var relativePath = Path.GetRelativePath(_basePath, topDir);
                results.Add(new FileSnapshot(relativePath, 0, dirInfo.LastWriteTimeUtc, true));

                // Recursively enumerate subdirectories
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System
                };

                foreach (var dir in Directory.EnumerateDirectories(topDir, "*", options))
                {
                    if (ShouldIgnorePath(dir)) continue;
                    try
                    {
                        var info = new DirectoryInfo(dir);
                        var relPath = Path.GetRelativePath(_basePath, dir);
                        results.Add(new FileSnapshot(relPath, 0, info.LastWriteTimeUtc, true));
                    }
                    catch { /* Skip inaccessible directories */ }
                }

                foreach (var file in Directory.EnumerateFiles(topDir, "*", options))
                {
                    if (ShouldIgnorePath(file)) continue;
                    try
                    {
                        var info = new FileInfo(file);
                        var relPath = Path.GetRelativePath(_basePath, file);
                        results.Add(new FileSnapshot(relPath, info.Length, info.LastWriteTimeUtc, false));
                    }
                    catch { /* Skip inaccessible files */ }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error enumerating directory {Path}", topDir);
            }
        }

        // Also enumerate files at root level
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (ShouldIgnorePath(file)) continue;
                try
                {
                    var info = new FileInfo(file);
                    var relPath = Path.GetRelativePath(_basePath, file);
                    results.Add(new FileSnapshot(relPath, info.Length, info.LastWriteTimeUtc, false));
                }
                catch { /* Skip inaccessible files */ }
            }
        }
        catch { /* Ignore root file enumeration errors */ }

        return results;
    }

    private void DetectChanges()
    {
        var currentFiles = new Dictionary<string, FileSnapshot>();

        try
        {
            foreach (var entry in EnumerateFileSystem(_basePath))
            {
                currentFiles[entry.Path] = entry;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating file system during change detection");
            return;
        }

        // Detect created files (in current but not in snapshot)
        foreach (var (path, current) in currentFiles)
        {
            if (!_fileSnapshots.ContainsKey(path))
            {
                var fullPath = Path.Combine(_basePath, path);
                ProcessFileEvent(fullPath, "Created", null, current.IsDirectory);
                _fileSnapshots[path] = current;
            }
        }

        // Detect modified files (same path but different modified time or size)
        foreach (var (path, current) in currentFiles)
        {
            if (_fileSnapshots.TryGetValue(path, out var previous))
            {
                if (current.Modified != previous.Modified || current.Size != previous.Size)
                {
                    var fullPath = Path.Combine(_basePath, path);
                    ProcessFileEvent(fullPath, "Modified", null, current.IsDirectory);
                    _fileSnapshots[path] = current;
                }
            }
        }

        // Detect deleted files (in snapshot but not in current)
        var deletedPaths = _fileSnapshots.Keys.Except(currentFiles.Keys).ToList();
        foreach (var path in deletedPaths)
        {
            if (_fileSnapshots.TryRemove(path, out var deleted))
            {
                var fullPath = Path.Combine(_basePath, path);
                ProcessFileEvent(fullPath, "Deleted", null, deleted.IsDirectory);
            }
        }
    }

    private void TryStartFileSystemWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher
            {
                Path = _basePath,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size,
                IncludeSubdirectories = true,
                InternalBufferSize = 65536
            };

            _watcher.Created += OnFileSystemEvent;
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Deleted += OnFileSystemEvent;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;

            _watcher.EnableRaisingEvents = true;
            _logger.LogInformation("FileSystemWatcher started as supplementary event source");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "FileSystemWatcher failed to start (expected on 9p mounts). Polling will be used exclusively.");
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (ShouldIgnorePath(e.FullPath)) return;

            var eventType = e.ChangeType.ToString();
            var key = $"{e.FullPath}:{eventType}";

            if (_debouncers.TryRemove(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            var timer = new Timer(
                _ => {
                    var isDirectory = Directory.Exists(e.FullPath);
                    ProcessFileEvent(e.FullPath, eventType, null, isDirectory);

                    // Update snapshot to avoid duplicate from polling
                    var relativePath = Path.GetRelativePath(_basePath, e.FullPath);
                    if (eventType == "Deleted")
                    {
                        _fileSnapshots.TryRemove(relativePath, out FileSnapshot _);
                    }
                    else if (File.Exists(e.FullPath) || Directory.Exists(e.FullPath))
                    {
                        try
                        {
                            var info = isDirectory
                                ? (FileSystemInfo)new DirectoryInfo(e.FullPath)
                                : new FileInfo(e.FullPath);
                            var size = isDirectory ? 0 : ((FileInfo)info).Length;
                            _fileSnapshots[relativePath] = new FileSnapshot(
                                relativePath, size, info.LastWriteTimeUtc, isDirectory);
                        }
                        catch { /* Ignore - polling will catch it */ }
                    }
                },
                null,
                DebounceDelayMs,
                Timeout.Infinite);

            _debouncers[key] = timer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file system event for {Path}", e.FullPath);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            if (ShouldIgnorePath(e.FullPath)) return;

            var key = $"{e.FullPath}:Renamed";

            if (_debouncers.TryRemove(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            var timer = new Timer(
                _ => {
                    var isDirectory = Directory.Exists(e.FullPath);
                    ProcessFileEvent(e.FullPath, "Renamed", e.OldFullPath, isDirectory);

                    // Update snapshot
                    var oldRelativePath = Path.GetRelativePath(_basePath, e.OldFullPath);
                    var newRelativePath = Path.GetRelativePath(_basePath, e.FullPath);
                    _fileSnapshots.TryRemove(oldRelativePath, out FileSnapshot _);

                    if (File.Exists(e.FullPath) || Directory.Exists(e.FullPath))
                    {
                        try
                        {
                            var info = isDirectory
                                ? (FileSystemInfo)new DirectoryInfo(e.FullPath)
                                : new FileInfo(e.FullPath);
                            var size = isDirectory ? 0 : ((FileInfo)info).Length;
                            _fileSnapshots[newRelativePath] = new FileSnapshot(
                                newRelativePath, size, info.LastWriteTimeUtc, isDirectory);
                        }
                        catch { /* Ignore - polling will catch it */ }
                    }
                },
                null,
                DebounceDelayMs,
                Timeout.Infinite);

            _debouncers[key] = timer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling rename event for {Path}", e.FullPath);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        if (exception is InternalBufferOverflowException)
        {
            _logger.LogWarning("FileSystemWatcher buffer overflow. Polling will catch missed events.");
        }
        else
        {
            _logger.LogWarning(exception, "FileSystemWatcher error (polling continues)");
        }
    }

    private void ProcessFileEvent(string fullPath, string eventType, string? oldPath, bool isDirectory)
    {
        try
        {
            var relativePath = Path.GetRelativePath(_basePath, fullPath);
            var fileName = Path.GetFileName(fullPath);

            var fileEvent = new FileEventDto
            {
                Path = fullPath,
                RelativePath = relativePath,
                FileName = fileName,
                EventType = eventType,
                OldPath = oldPath,
                Timestamp = DateTime.UtcNow,
                Protocols = GetVisibleProtocols(fullPath),
                IsDirectory = isDirectory
            };

            // Add to circular buffer (check for duplicates within 1 second)
            lock (_bufferLock)
            {
                var isDuplicate = _eventHistory
                    .Where(e => e.RelativePath == relativePath && e.EventType == eventType)
                    .Any(e => (DateTime.UtcNow - e.Timestamp).TotalSeconds < 1);

                if (!isDuplicate)
                {
                    _eventHistory.Add(fileEvent);
                    if (_eventHistory.Count > MaxHistorySize)
                    {
                        _eventHistory.RemoveAt(0);
                    }

                    // Broadcast via SignalR
                    _hubContext.Clients.All.SendAsync("FileEvent", fileEvent);

                    _logger.LogInformation(
                        "File event: {EventType} {Path} [{Protocols}]",
                        eventType,
                        relativePath,
                        string.Join(", ", fileEvent.Protocols));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file event for {Path}", fullPath);
        }
    }

    private List<string> GetVisibleProtocols(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_basePath, fullPath);
        var parts = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        var topLevelDir = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";

        return topLevelDir switch
        {
            "nas-input-1" or "nas-input-2" or "nas-input-3" => new List<string> { "NAS" },
            "nas-output-1" or "nas-output-2" or "nas-output-3" => new List<string> { "NAS" },
            "nas-backup" => new List<string> { "NAS" },
            "ftpuser" => new List<string> { "FTP" },
            "nfs" => new List<string> { "NFS" },
            "input" or "output" or "processed" or "archive" =>
                new List<string> { "FTP", "SFTP", "HTTP", "S3", "SMB", "NFS" },
            _ => new List<string>()
        };
    }

    private bool ShouldIgnorePath(string path)
    {
        var relativePath = Path.GetRelativePath(_basePath, path);

        if (relativePath.Contains(".minio.sys", StringComparison.OrdinalIgnoreCase))
            return true;

        if (relativePath.Contains(".deleted", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Gets recent file events from the circular buffer.
    /// </summary>
    public List<FileEventDto> GetRecentEvents(int count)
    {
        lock (_bufferLock)
        {
            var takeCount = Math.Min(count, _eventHistory.Count);
            return _eventHistory
                .Skip(_eventHistory.Count - takeCount)
                .ToList();
        }
    }

    public override void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileSystemEvent;
            _watcher.Changed -= OnFileSystemEvent;
            _watcher.Deleted -= OnFileSystemEvent;
            _watcher.Renamed -= OnRenamed;
            _watcher.Error -= OnError;
            _watcher.Dispose();
        }

        foreach (var timer in _debouncers.Values)
        {
            timer.Dispose();
        }
        _debouncers.Clear();

        lock (_bufferLock)
        {
            _eventHistory.Clear();
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
