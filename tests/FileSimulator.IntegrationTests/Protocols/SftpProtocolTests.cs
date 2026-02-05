using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Renci.SshNet;
using Xunit;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// SFTP protocol integration tests validating all file operations.
/// </summary>
[Collection("Simulator")]
public class SftpProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public SftpProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates unique test content with timestamp and GUID.
    /// </summary>
    private static string CreateTestContent() => TestHelpers.CreateTestContent("SFTP Test");

    /// <summary>
    /// Gets SFTP server configuration from connection info.
    /// </summary>
    private async Task<(string host, int port, string username, string password)> GetSftpConfigAsync()
    {
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var sftpServer = connectionInfo.GetServer("SFTP");

        sftpServer.Should().NotBeNull("SFTP server must be available in connection info");

        return (
            sftpServer!.Host,
            sftpServer.Port,
            sftpServer.Credentials.Username,
            sftpServer.Credentials.Password
        );
    }

    [Fact]
    public async Task SFTP_CanConnect()
    {
        // Arrange
        var (host, port, username, password) = await GetSftpConfigAsync();

        // SSH.NET is synchronous, so wrap in Task.Run
        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);

            // Act
            client.Connect();

            // Assert
            client.IsConnected.Should().BeTrue("SFTP client should connect successfully");

            // Cleanup
            client.Disconnect();
        });
    }

    [Fact]
    public async Task SFTP_Upload_CreatesFile()
    {
        // Arrange
        var (host, port, username, password) = await GetSftpConfigAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("sftp-upload");
        var remotePath = $"/data/output/{fileName}";
        var content = CreateTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();

            try
            {
                // Act
                using var stream = new MemoryStream(bytes);
                client.UploadFile(stream, remotePath, true);

                // Assert
                var exists = client.Exists(remotePath);
                exists.Should().BeTrue("Uploaded file should exist on server");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (client.Exists(remotePath))
                    {
                        client.DeleteFile(remotePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                client.Disconnect();
            }
        });
    }

    [Fact]
    public async Task SFTP_Download_ReturnsCorrectContent()
    {
        // Arrange
        var (host, port, username, password) = await GetSftpConfigAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("sftp-download");
        var remotePath = $"/data/output/{fileName}";
        var expectedContent = CreateTestContent();
        var uploadBytes = System.Text.Encoding.UTF8.GetBytes(expectedContent);

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();

            try
            {
                // Act - Upload first
                using (var uploadStream = new MemoryStream(uploadBytes))
                {
                    client.UploadFile(uploadStream, remotePath, true);
                }

                // Act - Download
                using var downloadStream = new MemoryStream();
                client.DownloadFile(remotePath, downloadStream);
                var actualContent = System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());

                // Assert
                actualContent.Should().Be(expectedContent, "Downloaded content should match uploaded content");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (client.Exists(remotePath))
                    {
                        client.DeleteFile(remotePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                client.Disconnect();
            }
        });
    }

    [Fact]
    public async Task SFTP_List_ReturnsUploadedFile()
    {
        // Arrange
        var (host, port, username, password) = await GetSftpConfigAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("sftp-list");
        var remotePath = $"/data/output/{fileName}";
        var content = CreateTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();

            try
            {
                // Act - Upload
                using (var stream = new MemoryStream(bytes))
                {
                    client.UploadFile(stream, remotePath, true);
                }

                // Act - List
                var items = client.ListDirectory("/data/output");

                // Assert
                items.Should().NotBeEmpty("Directory should contain files");
                var uploadedFile = items.FirstOrDefault(i => i.Name == fileName);
                uploadedFile.Should().NotBeNull($"File {fileName} should appear in directory listing");
                uploadedFile!.IsRegularFile.Should().BeTrue("Item should be a regular file");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (client.Exists(remotePath))
                    {
                        client.DeleteFile(remotePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                client.Disconnect();
            }
        });
    }

    [Fact]
    public async Task SFTP_Delete_RemovesFile()
    {
        // Arrange
        var (host, port, username, password) = await GetSftpConfigAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("sftp-delete");
        var remotePath = $"/data/output/{fileName}";
        var content = CreateTestContent();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();

            try
            {
                // Act - Upload
                using (var stream = new MemoryStream(bytes))
                {
                    client.UploadFile(stream, remotePath, true);
                }

                // Verify file exists
                var existsBefore = client.Exists(remotePath);
                existsBefore.Should().BeTrue("File should exist before deletion");

                // Act - Delete
                client.DeleteFile(remotePath);

                // Assert
                var existsAfter = client.Exists(remotePath);
                existsAfter.Should().BeFalse("File should not exist after deletion");
            }
            finally
            {
                // Cleanup (in case deletion failed)
                try
                {
                    if (client.Exists(remotePath))
                    {
                        client.DeleteFile(remotePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                client.Disconnect();
            }
        });
    }

    [Fact]
    public async Task SFTP_FullCycle_CRUD()
    {
        // Arrange
        var (host, port, username, password) = await GetSftpConfigAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("sftp-crud");
        var remotePath = $"/data/output/{fileName}";
        var originalContent = CreateTestContent();
        var updatedContent = CreateTestContent();

        await Task.Run(() =>
        {
            using var client = new SftpClient(host, port, username, password);
            client.Connect();

            try
            {
                // CREATE
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(originalContent)))
                {
                    client.UploadFile(stream, remotePath, true);
                }
                client.Exists(remotePath).Should().BeTrue("File should exist after creation");

                // READ
                using (var downloadStream = new MemoryStream())
                {
                    client.DownloadFile(remotePath, downloadStream);
                    var readContent = System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());
                    readContent.Should().Be(originalContent, "Read content should match original");
                }

                // UPDATE (overwrite with new content)
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(updatedContent)))
                {
                    client.UploadFile(stream, remotePath, true);
                }

                // Verify UPDATE
                using (var downloadStream = new MemoryStream())
                {
                    client.DownloadFile(remotePath, downloadStream);
                    var readContent = System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());
                    readContent.Should().Be(updatedContent, "Content should be updated");
                }

                // DELETE
                client.DeleteFile(remotePath);
                var existsAfterDelete = client.Exists(remotePath);
                existsAfterDelete.Should().BeFalse("File should not exist after deletion");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (client.Exists(remotePath))
                    {
                        client.DeleteFile(remotePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                client.Disconnect();
            }
        });
    }
}
