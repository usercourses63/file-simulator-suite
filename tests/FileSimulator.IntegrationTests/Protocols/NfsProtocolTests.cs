using System.Net.Sockets;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// Integration tests for NFS protocol.
/// NFS tests have two modes:
/// 1. TCP connectivity test - always runs if server is reachable
/// 2. File operation tests - only run if mount path is available
/// </summary>
[Collection("Simulator")]
public class NfsProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;
    private readonly bool _nfsMounted;
    private readonly bool _nfsTcpAccessible;
    private readonly string _host;
    private readonly int _port;
    private readonly string _mountPath;
    private readonly string _basePath;

    public NfsProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;

        // Get NFS server configuration from connection-info API
        var connectionInfo = _fixture.GetConnectionInfoAsync().GetAwaiter().GetResult();
        var nfsServers = connectionInfo.GetServers("NFS").ToList();

        if (!nfsServers.Any())
        {
            throw new InvalidOperationException("NFS server not found in connection-info");
        }

        var nfsServer = nfsServers.First();
        _host = nfsServer.Host;
        _port = nfsServer.Port;
        _basePath = nfsServer.Directory ?? "output";

        // Get platform-specific mount path
        _mountPath = PlatformHelpers.GetNfsMountPath();

        // Check TCP connectivity (always attempted)
        _nfsTcpAccessible = PlatformHelpers.TryTcpConnectAsync(_host, _port, TimeSpan.FromSeconds(5))
            .GetAwaiter().GetResult();

        // Check if NFS is mounted (for file operations)
        _nfsMounted = PlatformHelpers.IsNfsMountedAsync(_mountPath).GetAwaiter().GetResult();

        Console.WriteLine($"[NfsProtocolTests] Host: {_host}:{_port}, TCP: {_nfsTcpAccessible}, Mounted: {_nfsMounted}, Path: {_mountPath}");
    }

    [Fact]
    public async Task NFS_TcpConnectivity_ServerReachable()
    {
        // This test always runs - it verifies NFS server is listening
        // Arrange & Act
        using var client = new TcpClient();
        var connectTask = client.ConnectAsync(_host, _port);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

        var completedTask = await Task.WhenAny(connectTask, timeoutTask);

        // Assert
        completedTask.Should().Be(connectTask, "Connection should complete before timeout");
        connectTask.IsCompletedSuccessfully.Should().BeTrue(
            $"NFS server should be reachable at {_host}:{_port}");

        client.Connected.Should().BeTrue("TCP connection should be established");
    }

    [Fact]
    public async Task NFS_Mount_DirectoryExists()
    {
        if (!_nfsMounted)
        {
            var instructions = _nfsTcpAccessible
                ? $"TCP connectivity verified. Mount path '{_mountPath}' not available"
                : $"NFS server not reachable and mount path '{_mountPath}' not available";

            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", instructions)}");
            return;
        }

        // Arrange & Act
        var exists = Directory.Exists(_mountPath);

        // Assert
        exists.Should().BeTrue($"NFS mount path should exist at {_mountPath}");

        // Verify we can list contents
        var entries = Directory.GetFileSystemEntries(_mountPath);
        entries.Should().NotBeNull("Should be able to list mount directory contents");
    }

    [Fact]
    public async Task NFS_Upload_CreatesFile()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-upload");
        var testContent = TestHelpers.CreateTestContent("NFS upload test");
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var filePath = Path.Combine(fullBasePath, fileName);

        try
        {
            // Ensure base directory exists
            Directory.CreateDirectory(fullBasePath);

            // Act - Write file
            await File.WriteAllTextAsync(filePath, testContent);

            // Assert
            File.Exists(filePath).Should().BeTrue("File should exist after upload");

            var fileInfo = new FileInfo(filePath);
            fileInfo.Length.Should().BeGreaterThan(0, "File should have content");
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }

    [Fact]
    public async Task NFS_Download_ReturnsContent()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-download");
        var testContent = TestHelpers.CreateTestContent("NFS download test");
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var filePath = Path.Combine(fullBasePath, fileName);

        try
        {
            // Ensure base directory exists
            Directory.CreateDirectory(fullBasePath);

            // Upload test file
            await File.WriteAllTextAsync(filePath, testContent);

            // Act - Read file back
            var actualContent = await File.ReadAllTextAsync(filePath);

            // Assert
            actualContent.Should().Be(testContent,
                "Downloaded content should match uploaded content");
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }

    [Fact]
    public async Task NFS_List_ReturnsUploadedFile()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-list");
        var testContent = TestHelpers.CreateTestContent("NFS list test");
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var filePath = Path.Combine(fullBasePath, fileName);

        try
        {
            // Ensure base directory exists
            Directory.CreateDirectory(fullBasePath);

            // Upload test file
            await File.WriteAllTextAsync(filePath, testContent);

            // Act - List directory
            var files = Directory.GetFiles(fullBasePath, $"*{Path.GetExtension(fileName)}");

            // Assert
            files.Should().NotBeNull("Directory listing should succeed");
            files.Should().Contain(f => Path.GetFileName(f) == fileName,
                "Uploaded file should appear in directory listing");
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }

    [Fact]
    public async Task NFS_Delete_RemovesFile()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-delete");
        var testContent = TestHelpers.CreateTestContent("NFS delete test");
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var filePath = Path.Combine(fullBasePath, fileName);

        try
        {
            // Ensure base directory exists
            Directory.CreateDirectory(fullBasePath);

            // Upload test file
            await File.WriteAllTextAsync(filePath, testContent);
            File.Exists(filePath).Should().BeTrue("File should exist before deletion");

            // Act - Delete file
            File.Delete(filePath);

            // Assert
            File.Exists(filePath).Should().BeFalse("File should not exist after deletion");
        }
        finally
        {
            // Cleanup (in case assertion failed)
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }

    [Fact]
    public async Task NFS_FullCycle_CRUD()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-fullcycle");
        var testContent = TestHelpers.CreateTestContent("NFS full cycle test");
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var filePath = Path.Combine(fullBasePath, fileName);

        try
        {
            // Ensure base directory exists
            Directory.CreateDirectory(fullBasePath);

            // Create & Write
            await File.WriteAllTextAsync(filePath, testContent);
            File.Exists(filePath).Should().BeTrue("File should exist after creation");

            // Read
            var actualContent = await File.ReadAllTextAsync(filePath);
            actualContent.Should().Be(testContent, "Content should match");

            // List
            var files = Directory.GetFiles(fullBasePath);
            files.Should().Contain(f => Path.GetFileName(f) == fileName,
                "File should appear in directory listing");

            // Update
            var updatedContent = testContent + "\nUpdated at " + DateTime.UtcNow.ToString("O");
            await File.WriteAllTextAsync(filePath, updatedContent);
            var readUpdated = await File.ReadAllTextAsync(filePath);
            readUpdated.Should().Be(updatedContent, "Updated content should match");

            // Delete
            File.Delete(filePath);
            File.Exists(filePath).Should().BeFalse("File should not exist after deletion");
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }

    [Fact]
    public async Task NFS_SubdirectoryOperations_WorkCorrectly()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange
        var subdirName = $"test-subdir-{Guid.NewGuid():N}";
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-subdir");
        var testContent = TestHelpers.CreateTestContent("NFS subdirectory test");
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var subdirPath = Path.Combine(fullBasePath, subdirName);
        var filePath = Path.Combine(subdirPath, fileName);

        try
        {
            // Create subdirectory
            Directory.CreateDirectory(subdirPath);
            Directory.Exists(subdirPath).Should().BeTrue("Subdirectory should exist");

            // Write file in subdirectory
            await File.WriteAllTextAsync(filePath, testContent);
            File.Exists(filePath).Should().BeTrue("File should exist in subdirectory");

            // Read file
            var actualContent = await File.ReadAllTextAsync(filePath);
            actualContent.Should().Be(testContent, "Content should match");

            // List subdirectory
            var files = Directory.GetFiles(subdirPath);
            files.Should().Contain(f => Path.GetFileName(f) == fileName,
                "File should appear in subdirectory listing");

            // Delete file
            File.Delete(filePath);
            File.Exists(filePath).Should().BeFalse("File should be deleted");

            // Delete subdirectory
            Directory.Delete(subdirPath);
            Directory.Exists(subdirPath).Should().BeFalse("Subdirectory should be deleted");
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                if (Directory.Exists(subdirPath))
                {
                    Directory.Delete(subdirPath, recursive: true);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }

    [Fact]
    public async Task NFS_LargeFile_HandlesCorrectly()
    {
        if (!_nfsMounted)
        {
            Console.WriteLine($"[SKIP] {PlatformHelpers.GetSkipMessage("NFS", $"mount path '{_mountPath}' not available")}");
            return;
        }

        // Arrange - Create 1MB file content
        var fileName = TestHelpers.GenerateUniqueFileName("nfs-largefile");
        var contentSize = 1024 * 1024; // 1MB
        var testContent = new string('X', contentSize);
        var fullBasePath = Path.Combine(_mountPath, _basePath);
        var filePath = Path.Combine(fullBasePath, fileName);

        try
        {
            // Ensure base directory exists
            Directory.CreateDirectory(fullBasePath);

            // Act - Write large file
            await File.WriteAllTextAsync(filePath, testContent);

            // Assert
            File.Exists(filePath).Should().BeTrue("Large file should exist");

            var fileInfo = new FileInfo(filePath);
            fileInfo.Length.Should().BeGreaterThan(contentSize / 2,
                "File size should be approximately correct");

            // Read back and verify
            var actualContent = await File.ReadAllTextAsync(filePath);
            actualContent.Length.Should().Be(testContent.Length,
                "Large file content length should match");
        }
        finally
        {
            // Cleanup
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Cleanup best effort */ }
        }
    }
}
