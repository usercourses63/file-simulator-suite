using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using FluentFTP;
using Xunit;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// FTP protocol integration tests validating all file operations.
/// </summary>
[Collection("Simulator")]
public class FtpProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public FtpProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates unique test content with timestamp and GUID.
    /// </summary>
    private static string CreateTestContent() => TestHelpers.CreateTestContent("FTP Test");

    /// <summary>
    /// Gets FTP server configuration from connection info.
    /// </summary>
    private async Task<(string host, int port, string username, string password)> GetFtpConfigAsync()
    {
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var ftpServer = connectionInfo.GetServer("FTP");

        ftpServer.Should().NotBeNull("FTP server must be available in connection info");

        return (
            ftpServer!.Host,
            ftpServer.Port,
            ftpServer.Credentials.Username,
            ftpServer.Credentials.Password
        );
    }

    [Fact]
    public async Task FTP_CanConnect()
    {
        // Arrange
        var (host, port, username, password) = await GetFtpConfigAsync();
        using var client = new AsyncFtpClient(host, username, password, port);

        // Act
        await client.Connect();

        // Assert
        client.IsConnected.Should().BeTrue("FTP client should connect successfully");

        // Cleanup
        await client.Disconnect();
    }

    [Fact]
    public async Task FTP_Upload_CreatesFile()
    {
        // Arrange
        var (host, port, username, password) = await GetFtpConfigAsync();
        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-upload");
        var remotePath = $"/output/{fileName}";
        var content = CreateTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        try
        {
            // Act
            using var stream = new MemoryStream(bytes);
            var result = await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite);

            // Assert
            result.Should().Be(FtpStatus.Success, "Upload should succeed");

            // Verify file exists
            var exists = await client.FileExists(remotePath);
            exists.Should().BeTrue("Uploaded file should exist on server");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteFile(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            await client.Disconnect();
        }
    }

    [Fact]
    public async Task FTP_Download_ReturnsCorrectContent()
    {
        // Arrange
        var (host, port, username, password) = await GetFtpConfigAsync();
        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-download");
        var remotePath = $"/output/{fileName}";
        var expectedContent = CreateTestContent();
        var uploadBytes = System.Text.Encoding.UTF8.GetBytes(expectedContent);

        try
        {
            // Act - Upload first
            using (var uploadStream = new MemoryStream(uploadBytes))
            {
                await client.UploadStream(uploadStream, remotePath, FtpRemoteExists.Overwrite);
            }

            // Act - Download
            using var downloadStream = new MemoryStream();
            var downloadResult = await client.DownloadStream(downloadStream, remotePath);
            downloadResult.Should().BeTrue("Download should succeed");

            downloadStream.Position = 0; // Reset position after download
            var actualContent = System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());

            // Assert
            actualContent.Should().Be(expectedContent, "Downloaded content should match uploaded content");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteFile(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            await client.Disconnect();
        }
    }

    [Fact]
    public async Task FTP_List_ReturnsUploadedFile()
    {
        // Arrange
        var (host, port, username, password) = await GetFtpConfigAsync();
        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-list");
        var remotePath = $"/output/{fileName}";
        var content = CreateTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        try
        {
            // Act - Upload
            using (var stream = new MemoryStream(bytes))
            {
                await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite);
            }

            // Act - List
            var items = await client.GetListing("/output");

            // Assert
            items.Should().NotBeEmpty("Directory should contain files");
            var uploadedFile = items.FirstOrDefault(i => i.Name == fileName);
            uploadedFile.Should().NotBeNull($"File {fileName} should appear in directory listing");
            uploadedFile!.Type.Should().Be(FtpObjectType.File, "Item should be a file, not a directory");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteFile(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            await client.Disconnect();
        }
    }

    [Fact]
    public async Task FTP_Delete_RemovesFile()
    {
        // Arrange
        var (host, port, username, password) = await GetFtpConfigAsync();
        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-delete");
        var remotePath = $"/output/{fileName}";
        var content = CreateTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        try
        {
            // Act - Upload
            using (var stream = new MemoryStream(bytes))
            {
                await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite);
            }

            // Verify file exists
            var existsBefore = await client.FileExists(remotePath);
            existsBefore.Should().BeTrue("File should exist before deletion");

            // Act - Delete
            await client.DeleteFile(remotePath);

            // Assert
            var existsAfter = await client.FileExists(remotePath);
            existsAfter.Should().BeFalse("File should not exist after deletion");
        }
        finally
        {
            // Cleanup (in case deletion failed)
            try
            {
                await client.DeleteFile(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            await client.Disconnect();
        }
    }

    [Fact]
    public async Task FTP_FullCycle_CRUD()
    {
        // Arrange
        var (host, port, username, password) = await GetFtpConfigAsync();
        using var client = new AsyncFtpClient(host, username, password, port);
        await client.Connect();

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-crud");
        var remotePath = $"/output/{fileName}";
        var originalContent = CreateTestContent();
        var updatedContent = CreateTestContent();

        try
        {
            // CREATE
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(originalContent)))
            {
                var uploadResult = await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite);
                uploadResult.Should().Be(FtpStatus.Success, "Initial upload should succeed");
            }

            // READ
            using (var downloadStream = new MemoryStream())
            {
                await client.DownloadStream(downloadStream, remotePath);
                var readContent = System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());
                readContent.Should().Be(originalContent, "Read content should match original");
            }

            // UPDATE (overwrite with new content)
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(updatedContent)))
            {
                var updateResult = await client.UploadStream(stream, remotePath, FtpRemoteExists.Overwrite);
                updateResult.Should().Be(FtpStatus.Success, "Update should succeed");
            }

            // Verify UPDATE
            using (var downloadStream = new MemoryStream())
            {
                await client.DownloadStream(downloadStream, remotePath);
                var readContent = System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());
                readContent.Should().Be(updatedContent, "Content should be updated");
            }

            // DELETE
            await client.DeleteFile(remotePath);
            var existsAfterDelete = await client.FileExists(remotePath);
            existsAfterDelete.Should().BeFalse("File should not exist after deletion");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteFile(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            await client.Disconnect();
        }
    }
}
