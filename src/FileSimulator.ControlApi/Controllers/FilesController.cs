namespace FileSimulator.ControlApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using FileSimulator.ControlApi.Models;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly string _basePath;
    private readonly ILogger<FilesController> _logger;

    // Hidden directories to exclude
    private static readonly HashSet<string> HiddenDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".minio.sys", ".deleted"
    };

    public FilesController(IConfiguration config, ILogger<FilesController> logger)
    {
        _basePath = config["FileWatcher:Path"] ?? "/mnt/simulator-data";
        _logger = logger;
    }

    /// <summary>
    /// Get directory tree listing
    /// </summary>
    /// <param name="path">Relative path from base directory (empty for root)</param>
    /// <returns>List of FileNodeDto for immediate children</returns>
    [HttpGet("tree")]
    public IActionResult GetTree([FromQuery] string path = "")
    {
        if (!ValidatePath(path, out var fullPath))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return BadRequest(new { error = "Invalid path" });
        }

        if (!Directory.Exists(fullPath))
        {
            return NotFound(new { error = "Directory not found" });
        }

        try
        {
            var nodes = new List<FileNodeDto>();

            // Get directories first
            var directories = Directory.GetDirectories(fullPath)
                .Select(dir => new DirectoryInfo(dir))
                .Where(dir => !HiddenDirs.Contains(dir.Name))
                .OrderBy(dir => dir.Name);

            foreach (var dir in directories)
            {
                var relativePath = Path.GetRelativePath(_basePath, dir.FullName);
                nodes.Add(new FileNodeDto
                {
                    Id = relativePath.Replace('\\', '/'),
                    Name = dir.Name,
                    IsDirectory = true,
                    Size = null,
                    Modified = dir.LastWriteTimeUtc.ToString("O"),
                    Protocols = GetVisibleProtocols(dir.FullName),
                    Children = null
                });
            }

            // Get files
            var files = Directory.GetFiles(fullPath)
                .Select(file => new FileInfo(file))
                .OrderBy(file => file.Name);

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(_basePath, file.FullName);
                nodes.Add(new FileNodeDto
                {
                    Id = relativePath.Replace('\\', '/'),
                    Name = file.Name,
                    IsDirectory = false,
                    Size = file.Length,
                    Modified = file.LastWriteTimeUtc.ToString("O"),
                    Protocols = GetVisibleProtocols(file.FullName),
                    Children = null
                });
            }

            _logger.LogDebug("Listed {Count} items in {Path}", nodes.Count, path);
            return Ok(nodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing directory: {Path}", path);
            return StatusCode(500, new { error = "Failed to list directory" });
        }
    }

    /// <summary>
    /// Upload a file
    /// </summary>
    /// <param name="file">File to upload</param>
    /// <param name="path">Target directory path (optional, default root)</param>
    /// <returns>Created FileNodeDto</returns>
    [HttpPost("upload")]
    [RequestSizeLimit(104857600)] // 100 MB
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string path = "")
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file provided" });
        }

        if (!ValidatePath(path, out var targetDir))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return BadRequest(new { error = "Invalid path" });
        }

        if (!Directory.Exists(targetDir))
        {
            return NotFound(new { error = "Target directory not found" });
        }

        try
        {
            // Sanitize filename
            var fileName = Path.GetFileName(file.FileName);
            var targetPath = Path.Combine(targetDir, fileName);

            // Save file
            using (var stream = new FileStream(targetPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var fileInfo = new FileInfo(targetPath);
            var relativePath = Path.GetRelativePath(_basePath, targetPath);

            var node = new FileNodeDto
            {
                Id = relativePath.Replace('\\', '/'),
                Name = fileName,
                IsDirectory = false,
                Size = fileInfo.Length,
                Modified = fileInfo.LastWriteTimeUtc.ToString("O"),
                Protocols = GetVisibleProtocols(targetPath),
                Children = null
            };

            _logger.LogInformation("Uploaded file: {FileName} ({Size} bytes) to {Path}",
                fileName, fileInfo.Length, path);

            return Ok(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to upload file" });
        }
    }

    /// <summary>
    /// Download a file
    /// </summary>
    /// <param name="path">Relative path to file</param>
    /// <returns>File stream</returns>
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "Path is required" });
        }

        if (!ValidatePath(path, out var fullPath))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return BadRequest(new { error = "Invalid path" });
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound(new { error = "File not found" });
        }

        if (Directory.Exists(fullPath))
        {
            return BadRequest(new { error = "Cannot download a directory" });
        }

        try
        {
            var fileName = Path.GetFileName(fullPath);
            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

            _logger.LogInformation("Downloading file: {Path}", path);

            return File(stream, "application/octet-stream", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading file: {Path}", path);
            return StatusCode(500, new { error = "Failed to download file" });
        }
    }

    /// <summary>
    /// Delete a file or directory
    /// </summary>
    /// <param name="path">Relative path to file or directory</param>
    /// <param name="recursive">Required if deleting directory (safety check)</param>
    /// <returns>204 No Content on success</returns>
    [HttpDelete]
    public IActionResult Delete([FromQuery] string path, [FromQuery] bool recursive = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BadRequest(new { error = "Path is required" });
        }

        if (!ValidatePath(path, out var fullPath))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return BadRequest(new { error = "Invalid path" });
        }

        try
        {
            if (Directory.Exists(fullPath))
            {
                if (!recursive)
                {
                    return BadRequest(new { error = "recursive=true required to delete directory" });
                }

                Directory.Delete(fullPath, recursive: true);
                _logger.LogInformation("Deleted directory: {Path} (recursive)", path);
            }
            else if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
                _logger.LogInformation("Deleted file: {Path}", path);
            }
            else
            {
                return NotFound(new { error = "File or directory not found" });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting: {Path}", path);
            return StatusCode(500, new { error = "Failed to delete" });
        }
    }

    /// <summary>
    /// Validate path stays within base directory (prevent path traversal)
    /// </summary>
    private bool ValidatePath(string relativePath, out string fullPath)
    {
        fullPath = Path.GetFullPath(Path.Combine(_basePath, relativePath));
        return fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get protocols that can access this path (same logic as FileWatcherService)
    /// </summary>
    private List<string> GetVisibleProtocols(string fullPath)
    {
        var relativePath = Path.GetRelativePath(_basePath, fullPath);
        var topDir = relativePath.Split(Path.DirectorySeparatorChar)[0];

        return topDir switch
        {
            var d when d.StartsWith("nas-", StringComparison.OrdinalIgnoreCase) => new() { "NAS" },
            "ftpuser" => new() { "FTP" },
            "nfs" => new() { "NFS" },
            "input" or "output" or "processed" or "archive" =>
                new() { "FTP", "SFTP", "HTTP", "S3", "SMB", "NFS" },
            _ => new()
        };
    }
}
