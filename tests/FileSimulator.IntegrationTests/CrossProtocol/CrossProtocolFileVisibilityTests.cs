using Amazon.S3;
using Amazon.S3.Model;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using FluentFTP;
using Polly;
using Renci.SshNet;
using Xunit;

namespace FileSimulator.IntegrationTests.CrossProtocol;

/// <summary>
/// Tests cross-protocol file visibility demonstrating shared storage.
/// Validates that files uploaded via one protocol are visible via other protocols.
/// </summary>
[Collection("Simulator")]
public class CrossProtocolFileVisibilityTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public CrossProtocolFileVisibilityTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Waits for a file to become visible via a list operation with retries.
    /// </summary>
    private async Task<bool> WaitForFileVisibility(
        Func<Task<IEnumerable<string>>> listFunc,
        string fileName,
        TimeSpan timeout)
    {
        var policy = Policy
            .HandleResult<bool>(visible => !visible)
            .WaitAndRetryAsync(10, _ => TimeSpan.FromMilliseconds(500));

        return await policy.ExecuteAsync(async () =>
        {
            var files = await listFunc();
            return files.Any(f => f.Contains(fileName));
        });
    }

    [Fact]
    public async Task FtpToSftp_FileVisibility()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var ftpServer = connectionInfo.GetServer("FTP");
        var sftpServer = connectionInfo.GetServer("SFTP");

        ftpServer.Should().NotBeNull("FTP server must be available");
        sftpServer.Should().NotBeNull("SFTP server must be available");

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-to-sftp");
        var content = TestHelpers.CreateTestContent("FTP to SFTP test");

        using var ftpClient = new AsyncFtpClient(
            ftpServer!.Host,
            ftpServer.Credentials.Username,
            ftpServer.Credentials.Password,
            ftpServer.Port);
        ftpClient.Config.DataConnectionType = FtpDataConnectionType.AutoActive;
        ftpClient.Config.DataConnectionConnectTimeout = 10000;
        ftpClient.Config.ConnectTimeout = 10000;

        await ftpClient.Connect();

        try
        {
            // Act - Upload via FTP
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);
            await ftpClient.UploadStream(stream, $"/output/{fileName}", FtpRemoteExists.Overwrite);

            // Wait for filesystem sync
            await Task.Delay(500);

            // Assert - File visible via SFTP
            var fileVisible = await WaitForFileVisibility(async () =>
            {
                return await Task.Run(() =>
                {
                    using var sftpClient = new SftpClient(
                        sftpServer!.Host,
                        sftpServer.Port,
                        sftpServer.Credentials.Username,
                        sftpServer.Credentials.Password);
                    sftpClient.Connect();
                    var files = sftpClient.ListDirectory("/data/output")
                        .Where(f => !f.IsDirectory)
                        .Select(f => f.Name);
                    sftpClient.Disconnect();
                    return files;
                });
            }, fileName, TimeSpan.FromSeconds(5));

            fileVisible.Should().BeTrue($"File {fileName} should be visible via SFTP within 5 seconds");
        }
        finally
        {
            // Cleanup
            try
            {
                await ftpClient.DeleteFile($"/output/{fileName}");
            }
            catch
            {
                // Ignore cleanup errors
            }
            await ftpClient.Disconnect();
        }
    }

    [Fact]
    public async Task SftpToHttp_FileVisibility()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var sftpServer = connectionInfo.GetServer("SFTP");
        var httpServer = connectionInfo.GetServer("HTTP");

        sftpServer.Should().NotBeNull("SFTP server must be available");
        httpServer.Should().NotBeNull("HTTP server must be available");

        var fileName = TestHelpers.GenerateUniqueFileName("sftp-to-http");
        var content = TestHelpers.CreateTestContent("SFTP to HTTP test");

        try
        {
            // Act - Upload via SFTP
            await Task.Run(() =>
            {
                using var sftpClient = new SftpClient(
                    sftpServer!.Host,
                    sftpServer.Port,
                    sftpServer.Credentials.Username,
                    sftpServer.Credentials.Password);
                sftpClient.Connect();
                var bytes = System.Text.Encoding.UTF8.GetBytes(content);
                using var stream = new MemoryStream(bytes);
                sftpClient.UploadFile(stream, $"/data/output/{fileName}", true);
                sftpClient.Disconnect();
            });

            // Wait for filesystem sync
            await Task.Delay(500);

            // Assert - File visible via HTTP
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var fileVisible = await WaitForFileVisibility(async () =>
            {
                var response = await httpClient.GetAsync($"http://{httpServer!.Host}:{httpServer.Port}/api/files/output");
                if (!response.IsSuccessStatusCode) return Enumerable.Empty<string>();

                var json = await response.Content.ReadAsStringAsync();
                var fileList = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);
                return fileList?.Select(f => f.GetProperty("name").GetString() ?? string.Empty) ?? Enumerable.Empty<string>();
            }, fileName, TimeSpan.FromSeconds(5));

            fileVisible.Should().BeTrue($"File {fileName} should be visible via HTTP within 5 seconds");
        }
        finally
        {
            // Cleanup
            try
            {
                await Task.Run(() =>
                {
                    using var sftpClient = new SftpClient(
                        sftpServer!.Host,
                        sftpServer.Port,
                        sftpServer.Credentials.Username,
                        sftpServer.Credentials.Password);
                    sftpClient.Connect();
                    sftpClient.DeleteFile($"/data/output/{fileName}");
                    sftpClient.Disconnect();
                });
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task FtpToWebDav_FileVisibility()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var ftpServer = connectionInfo.GetServer("FTP");
        var webdavServer = connectionInfo.GetServer("WebDAV");

        ftpServer.Should().NotBeNull("FTP server must be available");
        webdavServer.Should().NotBeNull("WebDAV server must be available");

        var fileName = TestHelpers.GenerateUniqueFileName("ftp-to-webdav");
        var content = TestHelpers.CreateTestContent("FTP to WebDAV test");

        using var ftpClient = new AsyncFtpClient(
            ftpServer!.Host,
            ftpServer.Credentials.Username,
            ftpServer.Credentials.Password,
            ftpServer.Port);
        ftpClient.Config.DataConnectionType = FtpDataConnectionType.AutoActive;
        ftpClient.Config.DataConnectionConnectTimeout = 10000;
        ftpClient.Config.ConnectTimeout = 10000;

        await ftpClient.Connect();

        try
        {
            // Act - Upload via FTP
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);
            await ftpClient.UploadStream(stream, $"/output/{fileName}", FtpRemoteExists.Overwrite);

            // Wait for filesystem sync
            await Task.Delay(500);

            // Assert - File visible via WebDAV
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{webdavServer!.Credentials.Username}:{webdavServer.Credentials.Password}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var fileVisible = await WaitForFileVisibility(async () =>
            {
                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"http://{webdavServer.Host}:{webdavServer.Port}/output/"));
                if (!response.IsSuccessStatusCode) return Enumerable.Empty<string>();

                var html = await response.Content.ReadAsStringAsync();
                return html.Contains(fileName) ? new[] { fileName } : Enumerable.Empty<string>();
            }, fileName, TimeSpan.FromSeconds(5));

            fileVisible.Should().BeTrue($"File {fileName} should be visible via WebDAV within 5 seconds");
        }
        finally
        {
            // Cleanup
            try
            {
                await ftpClient.DeleteFile($"/output/{fileName}");
            }
            catch
            {
                // Ignore cleanup errors
            }
            await ftpClient.Disconnect();
        }
    }

    [Fact]
    public async Task S3ToFtp_FileVisibility()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var s3Server = connectionInfo.GetServer("S3");
        var ftpServer = connectionInfo.GetServer("FTP");

        s3Server.Should().NotBeNull("S3 server must be available");
        ftpServer.Should().NotBeNull("FTP server must be available");

        var fileName = TestHelpers.GenerateUniqueFileName("s3-to-ftp");
        var content = TestHelpers.CreateTestContent("S3 to FTP test");

        var s3Config = new AmazonS3Config
        {
            ServiceURL = $"http://{s3Server!.Host}:{s3Server.Port}",
            ForcePathStyle = true
        };
        using var s3Client = new AmazonS3Client(
            s3Server.Credentials.Username, // S3 access key is stored in Username
            s3Server.Credentials.Password,  // S3 secret key is stored in Password
            s3Config);

        try
        {
            // Act - Upload via S3
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = "output",
                Key = fileName,
                ContentBody = content
            });

            // Wait for filesystem sync
            await Task.Delay(500);

            // Assert - File visible via FTP
            using var ftpClient = new AsyncFtpClient(
                ftpServer!.Host,
                ftpServer.Credentials.Username,
                ftpServer.Credentials.Password,
                ftpServer.Port);
            ftpClient.Config.DataConnectionType = FtpDataConnectionType.AutoActive;
            ftpClient.Config.DataConnectionConnectTimeout = 10000;
            ftpClient.Config.ConnectTimeout = 10000;
            await ftpClient.Connect();

            var fileVisible = await WaitForFileVisibility(async () =>
            {
                var items = await ftpClient.GetListing("/output");
                return items.Where(i => !i.Type.HasFlag(FtpObjectType.Directory)).Select(i => i.Name);
            }, fileName, TimeSpan.FromSeconds(5));

            fileVisible.Should().BeTrue($"File {fileName} should be visible via FTP within 5 seconds");

            await ftpClient.Disconnect();
        }
        finally
        {
            // Cleanup
            try
            {
                await s3Client.DeleteObjectAsync("output", fileName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task AllProtocols_SharedStorageConsistency()
    {
        // Arrange
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var ftpServer = connectionInfo.GetServer("FTP");
        var sftpServer = connectionInfo.GetServer("SFTP");
        var httpServer = connectionInfo.GetServer("HTTP");
        var webdavServer = connectionInfo.GetServer("WebDAV");

        ftpServer.Should().NotBeNull("FTP server must be available");
        sftpServer.Should().NotBeNull("SFTP server must be available");
        httpServer.Should().NotBeNull("HTTP server must be available");
        webdavServer.Should().NotBeNull("WebDAV server must be available");

        var fileName = TestHelpers.GenerateUniqueFileName("all-protocols");
        var content = TestHelpers.CreateTestContent("All protocols consistency test");

        using var ftpClient = new AsyncFtpClient(
            ftpServer!.Host,
            ftpServer.Credentials.Username,
            ftpServer.Credentials.Password,
            ftpServer.Port);
        await ftpClient.Connect();

        try
        {
            // Act - Upload via FTP
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);
            await ftpClient.UploadStream(stream, $"/output/{fileName}", FtpRemoteExists.Overwrite);

            // Wait for filesystem sync
            await Task.Delay(500);

            // Assert - Visible via SFTP
            var visibleViaSftp = await WaitForFileVisibility(async () =>
            {
                return await Task.Run(() =>
                {
                    using var sftpClient = new SftpClient(
                        sftpServer!.Host,
                        sftpServer.Port,
                        sftpServer.Credentials.Username,
                        sftpServer.Credentials.Password);
                    sftpClient.Connect();
                    var files = sftpClient.ListDirectory("/data/output")
                        .Where(f => !f.IsDirectory)
                        .Select(f => f.Name);
                    sftpClient.Disconnect();
                    return files;
                });
            }, fileName, TimeSpan.FromSeconds(5));

            visibleViaSftp.Should().BeTrue($"File {fileName} should be visible via SFTP");

            // Assert - Visible via HTTP
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var visibleViaHttp = await WaitForFileVisibility(async () =>
            {
                var response = await httpClient.GetAsync($"http://{httpServer!.Host}:{httpServer.Port}/api/files/output");
                if (!response.IsSuccessStatusCode) return Enumerable.Empty<string>();

                var json = await response.Content.ReadAsStringAsync();
                var fileList = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(json);
                return fileList?.Select(f => f.GetProperty("name").GetString() ?? string.Empty) ?? Enumerable.Empty<string>();
            }, fileName, TimeSpan.FromSeconds(5));

            visibleViaHttp.Should().BeTrue($"File {fileName} should be visible via HTTP");

            // Assert - Visible via WebDAV
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{webdavServer!.Credentials.Username}:{webdavServer.Credentials.Password}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            var visibleViaWebDav = await WaitForFileVisibility(async () =>
            {
                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"http://{webdavServer.Host}:{webdavServer.Port}/output/"));
                if (!response.IsSuccessStatusCode) return Enumerable.Empty<string>();

                var html = await response.Content.ReadAsStringAsync();
                return html.Contains(fileName) ? new[] { fileName } : Enumerable.Empty<string>();
            }, fileName, TimeSpan.FromSeconds(5));

            visibleViaWebDav.Should().BeTrue($"File {fileName} should be visible via WebDAV");

            // Download via each protocol and verify content
            // Download via FTP
            using var ftpDownloadStream = new MemoryStream();
            await ftpClient.DownloadStream(ftpDownloadStream, $"/output/{fileName}");
            var ftpContent = System.Text.Encoding.UTF8.GetString(ftpDownloadStream.ToArray());
            ftpContent.Should().Be(content, "FTP download should match original content");

            // Download via SFTP
            var sftpContent = await Task.Run(() =>
            {
                using var sftpClient = new SftpClient(
                    sftpServer!.Host,
                    sftpServer.Port,
                    sftpServer.Credentials.Username,
                    sftpServer.Credentials.Password);
                sftpClient.Connect();
                using var downloadStream = new MemoryStream();
                sftpClient.DownloadFile($"/data/output/{fileName}", downloadStream);
                sftpClient.Disconnect();
                return System.Text.Encoding.UTF8.GetString(downloadStream.ToArray());
            });
            sftpContent.Should().Be(content, "SFTP download should match original content");

            // Download via HTTP
            var httpResponse = await httpClient.GetAsync($"http://{httpServer!.Host}:{httpServer.Port}/output/{fileName}");
            httpResponse.EnsureSuccessStatusCode();
            var httpContent = await httpResponse.Content.ReadAsStringAsync();
            httpContent.Should().Be(content, "HTTP download should match original content");

            // Download via WebDAV
            var webdavResponse = await httpClient.GetAsync($"http://{webdavServer.Host}:{webdavServer.Port}/output/{fileName}");
            webdavResponse.EnsureSuccessStatusCode();
            var webdavContent = await webdavResponse.Content.ReadAsStringAsync();
            webdavContent.Should().Be(content, "WebDAV download should match original content");
        }
        finally
        {
            // Cleanup
            try
            {
                await ftpClient.DeleteFile($"/output/{fileName}");
            }
            catch
            {
                // Ignore cleanup errors
            }
            await ftpClient.Disconnect();
        }
    }
}
