namespace FileSimulator.ControlApi.Services;

using Microsoft.AspNetCore.SignalR;
using FileSimulator.ControlApi.Hubs;
using FileSimulator.ControlApi.Models;
using System.Collections.Concurrent;

/// <summary>
/// Background service that monitors the Windows simulator-data directory
/// for file system changes and broadcasts events via SignalR.
/// Implements debouncing to avoid event storms during rapid file changes.
/// </summary>
public class FileWatcherService : BackgroundService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IHubContext<FileEventsHub> _hubContext;
    private readonly ILogger<FileWatcherService> _logger;

    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, Timer> _debouncers = new();
    private readonly object _bufferLock = new();
    private readonly List<FileEventDto> _eventHistory = new();
    private const int MaxHistorySize = 50;
    private const int DebounceDelayMs = 500;

    // Linux container path (mounted from Windows C:\simulator-data via Minikube)
    private string _basePath = "/mnt/simulator-data";

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
            "Starting FileWatcher on path: {Path}",
            _basePath);

        // Create and configure FileSystemWatcher
        _watcher = new FileSystemWatcher
        {
            Path = _basePath,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size,
            IncludeSubdirectories = true,
            InternalBufferSize = 65536 // 64 KB - maximum for network shares
        };

        // Wire up event handlers
        _watcher.Created += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;

        // Start monitoring
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation(
            "FileWatcher started successfully. Debounce delay: {DebounceMs}ms",
            DebounceDelayMs);

        // Keep service running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Skip unwanted directories
            if (ShouldIgnorePath(e.FullPath))
            {
                return;
            }

            var eventType = e.ChangeType.ToString();
            var key = $"{e.FullPath}:{eventType}";

            // Cancel existing timer for this key if present
            if (_debouncers.TryRemove(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create new debounce timer
            var timer = new Timer(
                _ => ProcessFileEvent(e.FullPath, eventType, null),
                null,
                DebounceDelayMs,
                Timeout.Infinite);

            _debouncers[key] = timer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling file system event for {Path}",
                e.FullPath);
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            // Skip unwanted directories
            if (ShouldIgnorePath(e.FullPath))
            {
                return;
            }

            var key = $"{e.FullPath}:Renamed";

            // Cancel existing timer for this key if present
            if (_debouncers.TryRemove(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create new debounce timer with old path for rename events
            var timer = new Timer(
                _ => ProcessFileEvent(e.FullPath, "Renamed", e.OldFullPath),
                null,
                DebounceDelayMs,
                Timeout.Infinite);

            _debouncers[key] = timer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling rename event for {Path}",
                e.FullPath);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();

        // Check for buffer overflow specifically
        if (exception is InternalBufferOverflowException)
        {
            _logger.LogCritical(exception,
                "FileSystemWatcher buffer overflow! Events may have been lost. " +
                "Consider increasing InternalBufferSize or reducing file activity.");
        }
        else
        {
            _logger.LogError(exception,
                "FileSystemWatcher error occurred");
        }
    }

    private void ProcessFileEvent(string fullPath, string eventType, string? oldPath)
    {
        try
        {
            var relativePath = Path.GetRelativePath(_basePath, fullPath);
            var fileName = Path.GetFileName(fullPath);
            var isDirectory = Directory.Exists(fullPath);

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

            // Add to circular buffer
            lock (_bufferLock)
            {
                _eventHistory.Add(fileEvent);
                if (_eventHistory.Count > MaxHistorySize)
                {
                    _eventHistory.RemoveAt(0);
                }
            }

            // Broadcast via SignalR
            _hubContext.Clients.All.SendAsync("FileEvent", fileEvent);

            _logger.LogDebug(
                "Broadcast {EventType} event for {Path} to protocols: [{Protocols}]",
                eventType,
                relativePath,
                string.Join(", ", fileEvent.Protocols));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing file event for {Path}",
                fullPath);
        }
    }

    private List<string> GetVisibleProtocols(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_basePath, fullPath);
        var topLevelDir = relativePath.Split(Path.DirectorySeparatorChar)[0].ToLowerInvariant();

        // Map directories to protocol visibility based on actual C:\simulator-data structure
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

        // Ignore MinIO internal directories
        if (relativePath.Contains(".minio.sys", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Ignore deleted file directories
        if (relativePath.Contains(".deleted", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets recent file events from the circular buffer.
    /// </summary>
    /// <param name="count">Number of events to retrieve (max 50)</param>
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
        // Dispose FileSystemWatcher
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

        // Dispose all debounce timers
        foreach (var timer in _debouncers.Values)
        {
            timer.Dispose();
        }
        _debouncers.Clear();

        // Clear event history
        lock (_bufferLock)
        {
            _eventHistory.Clear();
        }

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
