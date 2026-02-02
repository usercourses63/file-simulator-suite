# Phase 8: File Operations and Event Streaming - Research

**Researched:** 2026-02-02
**Domain:** File system operations, real-time event streaming, React file browser UI
**Confidence:** HIGH

## Summary

Phase 8 implements browser-based file operations (browse, upload, download, delete) and real-time Windows directory event streaming. The standard stack consists of:

- **Backend**: FileSystemWatcher with debouncing, SignalR streaming, ASP.NET Core file upload/download endpoints
- **Frontend**: react-dropzone for upload, react-arborist for file tree, SignalR client for events
- **Architecture**: Windows directory monitoring → debounced events → SignalR broadcast → React UI updates

The most critical pitfall is **FileSystemWatcher buffer overflow** with Minikube 9p mounts. Windows + VirtualBox shared folders/9p network mounts have limited buffer size (64 KB max) and may not fire events reliably. Solution: Implement aggressive debouncing (500ms minimum), increase InternalBufferSize to 64 KB, and handle Error events gracefully.

**Primary recommendation:** Use IAsyncEnumerable<T> for SignalR streaming, react-dropzone for file uploads, react-arborist for file tree, and timer-based debouncing (500ms window) for FileSystemWatcher events. Implement protocol visibility via directory name mapping (not real-time probes) to avoid performance overhead.

## Standard Stack

The established libraries/tools for this domain:

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| **FileSystemWatcher** | .NET 9.0 | Windows directory monitoring | Built-in .NET, supports all change types, event-based |
| **@microsoft/signalr** | ^8.0.7 | Real-time event streaming | Already in project, bidirectional streaming support |
| **react-dropzone** | Latest | Drag-and-drop file upload | Most popular (7.8k stars), hook-based, HTML5-compliant |
| **react-arborist** | Latest | File tree browser | Complete solution, virtualized, keyboard nav, accessible |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **System.Reactive** | Latest | Rx throttling/debouncing | Alternative to timer-based debouncing (if preferred) |
| **Axios** | Latest | Upload progress tracking | If FormData progress events needed |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| react-arborist | MUI X Tree View | MUI adds heavy dependency, arborist is purpose-built for file trees |
| react-dropzone | react-drag-drop-files | Dropzone has 4x more adoption, better docs |
| IAsyncEnumerable | ChannelReader<T> | Both work; IAsyncEnumerable simpler for C# 9.0+ |
| Timer debouncing | System.Reactive Throttle | Rx adds dependency; timer is built-in and sufficient |

**Installation:**

Frontend:
```bash
cd src/dashboard
npm install react-dropzone react-arborist
```

Backend (no new packages needed):
- FileSystemWatcher: Built into System.IO
- SignalR: Already included in ASP.NET Core 9.0

## Architecture Patterns

### Recommended Project Structure

Backend:
```
src/FileSimulator.ControlApi/
├── Hubs/
│   ├── StatusHub.cs           # Existing (server status)
│   └── FileEventsHub.cs       # NEW (file events)
├── Services/
│   ├── FileWatcherService.cs  # NEW (FSW + debouncing)
│   └── ProtocolDiscovery.cs   # NEW (visibility detection)
├── Controllers/
│   └── FilesController.cs     # NEW (CRUD endpoints)
└── Models/
    ├── FileEventDto.cs        # NEW
    └── FileInfoDto.cs         # NEW
```

Frontend:
```
src/dashboard/src/
├── components/
│   ├── ConnectionStatus.tsx   # Existing
│   ├── FileBrowser.tsx        # NEW (main container)
│   ├── FileTree.tsx           # NEW (react-arborist wrapper)
│   ├── FileUploader.tsx       # NEW (react-dropzone wrapper)
│   ├── FileEventFeed.tsx      # NEW (scrolling event list)
│   └── ProtocolBadges.tsx     # NEW (visibility indicators)
├── hooks/
│   ├── useSignalR.ts          # Existing (status connection)
│   ├── useFileEvents.ts       # NEW (file events connection)
│   └── useFileOperations.ts   # NEW (upload/download/delete)
└── types/
    └── fileTypes.ts           # NEW (shared types)
```

### Pattern 1: FileSystemWatcher with Debouncing

**What:** Monitor Windows directory for changes, debounce multiple events for same file, broadcast via SignalR

**When to use:** Windows directory monitoring where single operations generate multiple events

**Example:**
```csharp
// Source: Patterns from multiple community sources verified against official docs
public class FileWatcherService : BackgroundService
{
    private readonly FileSystemWatcher _watcher;
    private readonly IHubContext<FileEventsHub> _hubContext;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly Dictionary<string, Timer> _debounceTimers = new();
    private readonly object _lock = new();

    public FileWatcherService(
        IConfiguration config,
        IHubContext<FileEventsHub> hubContext,
        ILogger<FileWatcherService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        var path = config["WindowsDataPath"] ?? @"C:\simulator-data";

        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size,
            IncludeSubdirectories = true,
            InternalBufferSize = 65536, // 64 KB - maximum for network shares
            EnableRaisingEvents = false // Start in ExecuteAsync
        };

        _watcher.Created += OnFileSystemEvent;
        _watcher.Changed += OnFileSystemEvent;
        _watcher.Deleted += OnFileSystemEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("FileWatcher started monitoring {Path}", _watcher.Path);
        return Task.CompletedTask;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        DebouncedBroadcast(e.FullPath, e.ChangeType.ToString());
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        DebouncedBroadcast(e.FullPath, "Renamed", e.OldFullPath);
    }

    private void DebouncedBroadcast(string path, string eventType, string? oldPath = null)
    {
        lock (_lock)
        {
            var key = $"{path}:{eventType}";

            // Cancel existing timer for this path+event
            if (_debounceTimers.TryGetValue(key, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create new timer - broadcast after 500ms of silence
            _debounceTimers[key] = new Timer(async _ =>
            {
                lock (_lock)
                {
                    _debounceTimers.Remove(key);
                }

                await BroadcastFileEvent(path, eventType, oldPath);
            }, null, 500, Timeout.Infinite);
        }
    }

    private async Task BroadcastFileEvent(string path, string eventType, string? oldPath)
    {
        try
        {
            var evt = new FileEventDto
            {
                Path = path,
                EventType = eventType,
                OldPath = oldPath,
                Timestamp = DateTime.UtcNow,
                Protocols = GetVisibleProtocols(path)
            };

            await _hubContext.Clients.All.SendAsync("FileEvent", evt);
            _logger.LogDebug("Broadcast {EventType} for {Path}", eventType, path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast file event for {Path}", path);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        if (e.GetException() is InternalBufferOverflowException)
        {
            _logger.LogCritical("FileSystemWatcher buffer overflow! Events lost.");
            // Could implement recovery logic here (restart watcher, etc.)
        }
        else
        {
            _logger.LogError(e.GetException(), "FileSystemWatcher error");
        }
    }

    private List<string> GetVisibleProtocols(string fullPath)
    {
        // Extract directory name to determine protocol visibility
        // Example: C:\simulator-data\ftp-input\file.txt → visible via FTP
        // This is directory mapping approach (decided in CONTEXT.md)
        // Real-time probing avoided for performance

        var relativePath = Path.GetRelativePath(_watcher.Path, fullPath);
        var topDir = relativePath.Split(Path.DirectorySeparatorChar)[0];

        return topDir switch
        {
            "ftp-input" or "ftp-output" => new List<string> { "FTP" },
            "sftp-data" => new List<string> { "SFTP" },
            "s3-bucket" => new List<string> { "S3" },
            "http-files" => new List<string> { "HTTP" },
            "smb-share" => new List<string> { "SMB" },
            "nfs-export" => new List<string> { "NFS" },
            "shared" => new List<string> { "FTP", "SFTP", "HTTP", "S3", "SMB", "NFS" },
            _ => new List<string>()
        };
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        lock (_lock)
        {
            foreach (var timer in _debounceTimers.Values)
            {
                timer.Dispose();
            }
            _debounceTimers.Clear();
        }
        base.Dispose();
    }
}
```

### Pattern 2: SignalR Streaming with IAsyncEnumerable

**What:** Stream file events from server to clients in real-time

**When to use:** Push updates to multiple clients without polling

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming?view=aspnetcore-9.0
public class FileEventsHub : Hub
{
    private readonly ILogger<FileEventsHub> _logger;

    public FileEventsHub(ILogger<FileEventsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Client can call this method to request file event history
    public async IAsyncEnumerable<FileEventDto> GetRecentEvents(
        int count = 50,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // In production, fetch from event store/cache
        // For Phase 8, maintain in-memory circular buffer
        var events = GetEventHistory(count);

        foreach (var evt in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
            await Task.Delay(10, cancellationToken); // Throttle to avoid overwhelming client
        }
    }

    private List<FileEventDto> GetEventHistory(int count)
    {
        // TODO: Implement circular buffer in FileWatcherService
        return new List<FileEventDto>();
    }
}

// Register in Program.cs
builder.Services.AddSingleton<FileWatcherService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FileWatcherService>());

app.MapHub<FileEventsHub>("/hubs/fileevents");
```

### Pattern 3: React File Upload with Progress

**What:** Drag-and-drop file upload with progress tracking

**When to use:** User uploads files from browser to Windows directory

**Example:**
```typescript
// Source: https://react-dropzone.js.org/ + community patterns
import { useCallback, useState } from 'react';
import { useDropzone } from 'react-dropzone';

interface FileUploadProgress {
  fileName: string;
  progress: number;
  status: 'uploading' | 'complete' | 'error';
}

export function FileUploader() {
  const [uploads, setUploads] = useState<FileUploadProgress[]>([]);

  const uploadFile = useCallback(async (file: File) => {
    const formData = new FormData();
    formData.append('file', file);

    setUploads(prev => [...prev, {
      fileName: file.name,
      progress: 0,
      status: 'uploading'
    }]);

    try {
      const response = await fetch('/api/files/upload', {
        method: 'POST',
        body: formData,
        // Note: fetch() doesn't support progress tracking natively
        // For progress, use XMLHttpRequest or Axios
      });

      if (response.ok) {
        setUploads(prev => prev.map(u =>
          u.fileName === file.name
            ? { ...u, progress: 100, status: 'complete' }
            : u
        ));
      } else {
        throw new Error('Upload failed');
      }
    } catch (error) {
      setUploads(prev => prev.map(u =>
        u.fileName === file.name
          ? { ...u, status: 'error' }
          : u
      ));
    }
  }, []);

  const onDrop = useCallback((acceptedFiles: File[]) => {
    acceptedFiles.forEach(uploadFile);
  }, [uploadFile]);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    multiple: true,
    maxSize: 104857600, // 100 MB
  });

  return (
    <div className="file-uploader">
      <div
        {...getRootProps()}
        className={`dropzone ${isDragActive ? 'dropzone--active' : ''}`}
      >
        <input {...getInputProps()} />
        {isDragActive ? (
          <p>Drop files here...</p>
        ) : (
          <p>Drag files here or click to browse</p>
        )}
      </div>

      {uploads.length > 0 && (
        <div className="upload-list">
          {uploads.map(upload => (
            <div key={upload.fileName} className="upload-item">
              <span>{upload.fileName}</span>
              <span>{upload.status}</span>
              {upload.status === 'uploading' && (
                <progress value={upload.progress} max={100} />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
```

### Pattern 4: File Tree Browser

**What:** Hierarchical file/folder browser with virtualization

**When to use:** Display C:\simulator-data directory structure

**Example:**
```typescript
// Source: https://github.com/brimdata/react-arborist
import { Tree } from 'react-arborist';
import { useState, useEffect } from 'react';

interface FileNode {
  id: string;
  name: string;
  isDirectory: boolean;
  size?: number;
  modified?: string;
  protocols: string[];
  children?: FileNode[];
}

export function FileTree() {
  const [data, setData] = useState<FileNode[]>([]);
  const [selectedFile, setSelectedFile] = useState<FileNode | null>(null);

  useEffect(() => {
    fetchFileTree();
  }, []);

  const fetchFileTree = async () => {
    const response = await fetch('/api/files/tree');
    const tree = await response.json();
    setData(tree);
  };

  const handleSelect = (nodes: any[]) => {
    if (nodes.length > 0) {
      setSelectedFile(nodes[0].data);
    }
  };

  const handleDelete = async (node: FileNode) => {
    if (!confirm(`Delete ${node.name}?`)) return;

    await fetch(`/api/files?path=${encodeURIComponent(node.id)}`, {
      method: 'DELETE',
    });

    // Refresh tree
    fetchFileTree();
  };

  return (
    <div className="file-tree">
      <Tree
        initialData={data}
        openByDefault={false}
        width={400}
        height={600}
        indent={24}
        rowHeight={32}
        onSelect={handleSelect}
      >
        {({ node, style, dragHandle }) => (
          <div style={style} ref={dragHandle} className="tree-node">
            <span className="tree-node__icon">
              {node.data.isDirectory ? '📁' : '📄'}
            </span>
            <span className="tree-node__name">{node.data.name}</span>
            {node.data.size && (
              <span className="tree-node__size">
                {formatBytes(node.data.size)}
              </span>
            )}
            <div className="tree-node__protocols">
              {node.data.protocols.map(p => (
                <span key={p} className="protocol-badge">{p}</span>
              ))}
            </div>
            <button
              className="tree-node__delete"
              onClick={() => handleDelete(node.data)}
            >
              🗑️
            </button>
          </div>
        )}
      </Tree>

      {selectedFile && (
        <div className="file-details">
          <h3>{selectedFile.name}</h3>
          <p>Size: {selectedFile.size ? formatBytes(selectedFile.size) : 'N/A'}</p>
          <p>Modified: {selectedFile.modified}</p>
          <p>Protocols: {selectedFile.protocols.join(', ')}</p>
          {!selectedFile.isDirectory && (
            <button onClick={() => downloadFile(selectedFile.id)}>
              Download
            </button>
          )}
        </div>
      )}
    </div>
  );
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

async function downloadFile(path: string) {
  const response = await fetch(`/api/files/download?path=${encodeURIComponent(path)}`);
  const blob = await response.blob();
  const url = window.URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = path.split('/').pop() || 'download';
  a.click();
  window.URL.revokeObjectURL(url);
}
```

### Pattern 5: File Event Feed with SignalR

**What:** Live scrolling feed of file events, newest at top

**When to use:** Show real-time file system activity

**Example:**
```typescript
// Source: Custom pattern using existing useSignalR hook
import { useState, useEffect, useRef } from 'react';
import * as signalR from '@microsoft/signalr';

interface FileEvent {
  path: string;
  eventType: 'Created' | 'Modified' | 'Deleted' | 'Renamed';
  oldPath?: string;
  timestamp: string;
  protocols: string[];
}

export function FileEventFeed() {
  const [events, setEvents] = useState<FileEvent[]>([]);
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const maxEvents = 50;

  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/fileevents')
      .withAutomaticReconnect()
      .build();

    conn.on('FileEvent', (event: FileEvent) => {
      setEvents(prev => [event, ...prev].slice(0, maxEvents));
    });

    conn.start().then(() => {
      console.log('Connected to FileEvents hub');
    }).catch(err => {
      console.error('Failed to connect to FileEvents hub:', err);
    });

    setConnection(conn);

    return () => {
      conn.stop();
    };
  }, []);

  return (
    <div className="file-event-feed">
      <h3>Recent File Activity</h3>
      <div className="event-list">
        {events.map((evt, idx) => (
          <div key={`${evt.path}-${evt.timestamp}-${idx}`} className="event-item">
            <span className={`event-type event-type--${evt.eventType.toLowerCase()}`}>
              {evt.eventType}
            </span>
            <span className="event-path">
              {evt.eventType === 'Renamed'
                ? `${evt.oldPath} → ${evt.path}`
                : evt.path
              }
            </span>
            <span className="event-time">
              {new Date(evt.timestamp).toLocaleTimeString()}
            </span>
            <div className="event-protocols">
              {evt.protocols.map(p => (
                <span key={p} className="protocol-badge protocol-badge--small">
                  {p}
                </span>
              ))}
            </div>
          </div>
        ))}
      </div>
      {events.length === 0 && (
        <p className="event-list--empty">No recent file activity</p>
      )}
    </div>
  );
}
```

### Anti-Patterns to Avoid

- **Polling for file events:** Use FileSystemWatcher + SignalR push, not HTTP polling
- **No debouncing:** Single save operation can fire 3-4 events; always debounce
- **Synchronous FSW handlers:** Long operations block thread pool; use Task.Run or queue to channel
- **Ignoring Error event:** Buffer overflows are SILENT without Error handler
- **Real-time protocol probes:** Testing FTP/SFTP connectivity on every file event kills performance
- **Buffering entire file in memory:** Use streaming for large files (>64 KB threshold)

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| File tree virtualization | Custom virtual scrolling | react-arborist | Handles 10k+ nodes, keyboard nav, accessibility |
| Drag-and-drop upload | Custom drag handlers | react-dropzone | HTML5 compliant, file validation, multi-file |
| Event debouncing | Custom throttle logic | Timer-based debouncer | FSW fires multiple events per operation; proven pattern |
| File upload progress | Custom XHR wrapper | Axios with onUploadProgress | Handles FormData, cancellation, error recovery |
| SignalR reconnection | Custom retry logic | withAutomaticReconnect() | Built-in exponential backoff, configurable |

**Key insight:** FileSystemWatcher is notoriously tricky with network mounts. Community patterns (timer debouncing, 64 KB buffer, Error event handling) prevent production issues.

## Common Pitfalls

### Pitfall 1: FileSystemWatcher Buffer Overflow on Network Mounts

**What goes wrong:** Buffer overflows when monitoring Windows directory mounted via Minikube 9p/VirtualBox shared folders, causing lost events

**Why it happens:**
- Default 8 KB buffer fills instantly with rapid changes
- Network mounts have 64 KB maximum (not 128 KB like local drives)
- 9p filesystem and VirtualBox shared folders may not fire FSW events reliably

**How to avoid:**
```csharp
_watcher.InternalBufferSize = 65536; // 64 KB - maximum for network shares
_watcher.Error += OnError; // MUST handle this event

private void OnError(object sender, ErrorEventArgs e)
{
    if (e.GetException() is InternalBufferOverflowException)
    {
        _logger.LogCritical("FileSystemWatcher buffer overflow! Events lost.");
        // Restart watcher or implement polling fallback
    }
}
```

**Warning signs:**
- Events stop firing after rapid file changes
- InternalBufferOverflowException in logs
- Silent event loss (no exception if Error event not handled)

### Pitfall 2: Multiple Events Per File Operation

**What goes wrong:** Single user action (save file) fires 3-4 Changed events

**Why it happens:** Applications update files in stages (create, write metadata, write content, close)

**How to avoid:** Implement 500ms debouncing timer that resets on each event
```csharp
private void DebouncedBroadcast(string path, string eventType)
{
    // Cancel existing timer, create new one
    // Only broadcast after 500ms of silence
}
```

**Warning signs:**
- Event feed shows duplicate events for same file
- SignalR clients receive flood of redundant updates

### Pitfall 3: Blocking FileSystemWatcher Thread Pool

**What goes wrong:** Long-running operations in FSW event handlers block thread pool, causing buffer overruns

**Why it happens:** FSW handlers run on thread pool threads; blocking them prevents processing new events

**How to avoid:**
```csharp
private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
{
    // WRONG: await BroadcastFileEvent(e.FullPath, e.ChangeType);

    // CORRECT: Queue work, return immediately
    DebouncedBroadcast(e.FullPath, e.ChangeType.ToString());
}
```

**Warning signs:**
- Buffer overflows under load
- FSW event handlers taking >100ms

### Pitfall 4: Trusting User-Supplied Filenames

**What goes wrong:** Path traversal attacks (../../etc/passwd) or malicious filenames

**Why it happens:** User controls filename in upload; can craft malicious paths

**How to avoid:**
```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads
string untrustedFileName = Path.GetFileName(pathName);
var safeFileName = Path.GetRandomFileName(); // Use random name
var targetPath = Path.Combine(_uploadPath, safeFileName);

// Validate stays within bounds
if (!targetPath.StartsWith(_uploadPath))
{
    throw new InvalidOperationException("Invalid path");
}
```

**Warning signs:**
- Files appearing outside upload directory
- Path traversal attempts in logs

### Pitfall 5: SignalR Connection State Assumptions

**What goes wrong:** Client assumes connection is always available; fails silently on disconnect

**Why it happens:** Network interruptions, server restarts, idle timeouts

**How to avoid:**
```typescript
const conn = new signalR.HubConnectionBuilder()
  .withUrl('/hubs/fileevents')
  .withAutomaticReconnect() // Exponential backoff
  .build();

conn.onreconnecting(() => {
  console.log('Reconnecting...');
  // Show UI indicator
});

conn.onreconnected(() => {
  console.log('Reconnected');
  // Fetch missed events
});
```

**Warning signs:**
- Events stop appearing in UI
- No error messages (connection silently dies)

## Code Examples

Verified patterns from official sources:

### File Upload Endpoint (Buffered)

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads
[HttpPost("upload")]
[RequestSizeLimit(104857600)] // 100 MB
public async Task<IActionResult> UploadFile([FromForm] IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest("No file provided");

    // Validate file extension
    var permittedExtensions = new[] { ".txt", ".pdf", ".jpg", ".png" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
        return BadRequest("Invalid file type");

    // Generate safe filename
    var safeFileName = Path.GetRandomFileName() + ext;
    var targetPath = Path.Combine(_config["WindowsDataPath"], "uploads", safeFileName);

    // Ensure directory exists
    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

    // Save file
    using (var stream = System.IO.File.Create(targetPath))
    {
        await file.CopyToAsync(stream);
    }

    _logger.LogInformation("File uploaded: {FileName} -> {TargetPath}",
        file.FileName, safeFileName);

    return Ok(new { fileName = safeFileName, size = file.Length });
}
```

### File Download Endpoint

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads
[HttpGet("download")]
public IActionResult DownloadFile([FromQuery] string path)
{
    // Validate path
    var basePath = _config["WindowsDataPath"];
    var fullPath = Path.Combine(basePath, path);

    if (!fullPath.StartsWith(basePath))
        return BadRequest("Invalid path");

    if (!System.IO.File.Exists(fullPath))
        return NotFound();

    var memory = new MemoryStream();
    using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
    {
        stream.CopyTo(memory);
    }
    memory.Position = 0;

    var contentType = "application/octet-stream";
    var fileName = Path.GetFileName(fullPath);

    return File(memory, contentType, fileName);
}
```

### File Tree Endpoint

```csharp
[HttpGet("tree")]
public IActionResult GetFileTree([FromQuery] string? path = null)
{
    var basePath = _config["WindowsDataPath"];
    var targetPath = string.IsNullOrEmpty(path)
        ? basePath
        : Path.Combine(basePath, path);

    if (!targetPath.StartsWith(basePath))
        return BadRequest("Invalid path");

    if (!Directory.Exists(targetPath))
        return NotFound();

    var nodes = new List<FileNodeDto>();

    // Add directories
    foreach (var dir in Directory.GetDirectories(targetPath))
    {
        var dirInfo = new DirectoryInfo(dir);
        nodes.Add(new FileNodeDto
        {
            Id = Path.GetRelativePath(basePath, dir),
            Name = dirInfo.Name,
            IsDirectory = true,
            Modified = dirInfo.LastWriteTime.ToString("o"),
            Protocols = GetVisibleProtocols(dir)
        });
    }

    // Add files
    foreach (var file in Directory.GetFiles(targetPath))
    {
        var fileInfo = new FileInfo(file);
        nodes.Add(new FileNodeDto
        {
            Id = Path.GetRelativePath(basePath, file),
            Name = fileInfo.Name,
            IsDirectory = false,
            Size = fileInfo.Length,
            Modified = fileInfo.LastWriteTime.ToString("o"),
            Protocols = GetVisibleProtocols(file)
        });
    }

    return Ok(nodes);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Polling filesystem | FileSystemWatcher + SignalR | Always (since .NET 1.1) | Real-time vs 5s delay |
| ChannelReader<T> | IAsyncEnumerable<T> | C# 8.0 (2019) | Simpler syntax, less boilerplate |
| react-beautiful-dnd | hello-pangea/dnd or dnd-kit | 2023 | Original unmaintained |
| Class components | React hooks | React 16.8 (2019) | Cleaner, less code |
| XMLHttpRequest | fetch API | ES6 (2015) | Simpler API (but no progress events) |

**Deprecated/outdated:**
- **Rx Throttle for debouncing**: Still works but adds dependency; timer-based sufficient for Phase 8
- **CRA (Create React App)**: Replaced by Vite 6 (already in project)
- **react-beautiful-dnd**: Archived; use hello-pangea/dnd fork or dnd-kit

## Open Questions

Things that couldn't be fully resolved:

1. **FileSystemWatcher reliability on Minikube 9p mounts**
   - What we know: 9p/VirtualBox shared folders have known FSW issues; 64 KB max buffer
   - What's unclear: Will FSW fire events reliably for this specific Minikube + Windows + Hyper-V setup?
   - Recommendation: Implement FSW but add polling fallback option (environment flag). Test empirically in Phase 8.

2. **Optimal debounce window for Minikube mounts**
   - What we know: 500ms is community standard for local drives
   - What's unclear: Does 9p mount latency require longer window (1000ms)?
   - Recommendation: Start with 500ms, make configurable (appsettings.json), adjust if seeing duplicates

3. **Empty folder display strategy**
   - What we know: react-arborist doesn't mandate empty folder handling
   - What's unclear: Best UX for empty folders (hide, show with indicator, show count)
   - Recommendation: Show empty folders with gray text "(empty)", clickable to upload into

## Sources

### Primary (HIGH confidence)

- [Microsoft Learn - FileSystemWatcher Class](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-9.0) - Official .NET 9.0 API documentation
- [Microsoft Learn - SignalR Streaming](https://learn.microsoft.com/en-us/aspnet/core/signalr/streaming?view=aspnetcore-9.0) - Official ASP.NET Core SignalR streaming guide
- [Microsoft Learn - ASP.NET Core File Uploads](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0) - Official file upload best practices
- [react-dropzone GitHub](https://github.com/react-dropzone/react-dropzone) - Official react-dropzone repository
- [react-arborist GitHub](https://github.com/brimdata/react-arborist) - Official react-arborist repository

### Secondary (MEDIUM confidence)

- [Learn To Use FileSystemWatcher in .NET 9](https://www.c-sharpcorner.com/article/learn-to-use-filesystemwatcher-in-net-9/) - Community best practices
- [FileSystemWatcher Buffer Overflow Prevention](https://www.dotnet-guide.com/filesystemwatcher-class.html) - Debouncing patterns
- [Top 5 Drag-and-Drop Libraries for React in 2026](https://puckeditor.com/blog/top-5-drag-and-drop-libraries-for-react) - Library comparison
- [7 Best React Tree View Components For React App (2026 Update)](https://reactscript.com/best-tree-view/) - Tree view library comparison
- [Using react-arborist to create tree components for React](https://blog.logrocket.com/using-react-arborist-create-tree-components/) - Implementation guide
- [Integrating SignalR with React TypeScript and ASP.NET Core](https://www.roundthecode.com/dotnet-tutorials/integrating-signalr-with-react-typescript-and-asp-net-core) - Integration patterns

### Tertiary (LOW confidence - for context only)

- [FileSystemWatcher and network shares GitHub Issue](https://github.com/dotnet/runtime/issues/16924) - Known network mount issues
- [Minikube 9p mount issues](https://github.com/kubernetes/minikube/issues/1753) - Known 9p filesystem limitations
- [FileSystemWatcher running in webjob buffer overflow](https://learn.microsoft.com/en-us/answers/questions/1526786/filesystemwatcher-running-in-webjob-and-monitoring) - Network mount buffer overflow reports

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Libraries verified via official docs and GitHub repositories
- Architecture: HIGH - Patterns verified against official Microsoft documentation and community standards
- Pitfalls: MEDIUM - FileSystemWatcher network mount issues documented but project-specific behavior unknown

**Research date:** 2026-02-02
**Valid until:** 2026-03-02 (30 days - stable technology stack)

**Notes for planner:**
- Existing project already has React 19, Vite 6, TypeScript, SignalR client configured
- Backend is ASP.NET Core 9.0 with SignalR hub infrastructure (StatusHub exists)
- No new backend packages needed (FileSystemWatcher is built-in)
- Frontend needs: react-dropzone, react-arborist
- CRITICAL: Implement FSW Error event handler from day 1 (buffer overflow detection)
- User decided protocol visibility via directory mapping (not real-time probes) - document directory structure in plan
