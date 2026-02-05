using System.Net.Http.Headers;
using System.Text;
using FileSimulator.IntegrationTests.Fixtures;
using FileSimulator.IntegrationTests.Support;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Protocols;

/// <summary>
/// WebDAV protocol integration tests validating all file operations.
/// WebDAV is implemented via a dedicated ugeek/webdav container on port 30089.
/// </summary>
[Collection("Simulator")]
public class WebDavProtocolTests
{
    private readonly SimulatorCollectionFixture _fixture;

    public WebDavProtocolTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates unique test content with timestamp and GUID.
    /// </summary>
    private static string CreateTestContent() => TestHelpers.CreateTestContent("WebDAV Test");

    /// <summary>
    /// Gets WebDAV client with basic authentication configured.
    /// Uses the dedicated WebDAV server (not the HTTP server).
    /// </summary>
    private async Task<(string baseUrl, HttpClient client, string username, string password)> GetWebDavClientAsync()
    {
        var connectionInfo = await _fixture.GetConnectionInfoAsync();
        var webdavServer = connectionInfo.GetServer("WebDAV");

        webdavServer.Should().NotBeNull("WebDAV server must be available in connection info");

        var baseUrl = $"http://{webdavServer!.Host}:{webdavServer.Port}";
        var username = webdavServer.Credentials.Username;
        var password = webdavServer.Credentials.Password;

        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };

        // Add basic auth header
        var authBytes = Encoding.ASCII.GetBytes($"{username}:{password}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        return (baseUrl, client, username, password);
    }

    [Fact]
    public async Task WebDAV_CanConnect()
    {
        // Arrange
        var (_, client, _, _) = await GetWebDavClientAsync();

        try
        {
            // Act
            var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/output/"));

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue("WebDAV client should connect successfully with authentication");
        }
        finally
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task WebDAV_Upload_PUT_CreatesFile()
    {
        // Arrange
        var (_, client, _, _) = await GetWebDavClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("webdav-upload");
        var remotePath = $"/output/{fileName}";
        var content = CreateTestContent();

        try
        {
            // Act
            var request = new HttpRequestMessage(HttpMethod.Put, remotePath)
            {
                Content = new StringContent(content)
            };
            var response = await client.SendAsync(request);

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue("File upload via PUT should succeed");

            // Verify file exists
            var getResponse = await client.GetAsync(remotePath);
            getResponse.IsSuccessStatusCode.Should().BeTrue("Uploaded file should be accessible via GET");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteAsync(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task WebDAV_Download_GET_ReturnsContent()
    {
        // Arrange
        var (_, client, _, _) = await GetWebDavClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("webdav-download");
        var remotePath = $"/output/{fileName}";
        var expectedContent = CreateTestContent();

        try
        {
            // Act - Upload first
            var putRequest = new HttpRequestMessage(HttpMethod.Put, remotePath)
            {
                Content = new StringContent(expectedContent)
            };
            await client.SendAsync(putRequest);

            // Act - Download
            var getResponse = await client.GetAsync(remotePath);
            getResponse.IsSuccessStatusCode.Should().BeTrue("File download should succeed");

            var actualContent = await getResponse.Content.ReadAsStringAsync();

            // Assert
            actualContent.Should().Be(expectedContent, "Downloaded content should match uploaded content");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteAsync(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task WebDAV_List_ReturnsUploadedFile()
    {
        // Arrange
        var (_, client, _, _) = await GetWebDavClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("webdav-list");
        var remotePath = $"/output/{fileName}";
        var content = CreateTestContent();

        try
        {
            // Act - Upload
            var putRequest = new HttpRequestMessage(HttpMethod.Put, remotePath)
            {
                Content = new StringContent(content)
            };
            await client.SendAsync(putRequest);

            // Act - List directory (GET on directory returns HTML listing)
            var listResponse = await client.GetAsync("/output/");
            listResponse.IsSuccessStatusCode.Should().BeTrue("Directory listing should succeed");

            var listingHtml = await listResponse.Content.ReadAsStringAsync();

            // Assert
            listingHtml.Should().Contain(fileName, "Directory listing should contain uploaded file");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteAsync(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task WebDAV_Delete_RemovesFile()
    {
        // Arrange
        var (_, client, _, _) = await GetWebDavClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("webdav-delete");
        var remotePath = $"/output/{fileName}";
        var content = CreateTestContent();

        try
        {
            // Act - Upload
            var putRequest = new HttpRequestMessage(HttpMethod.Put, remotePath)
            {
                Content = new StringContent(content)
            };
            await client.SendAsync(putRequest);

            // Verify file exists
            var getResponseBefore = await client.GetAsync(remotePath);
            getResponseBefore.IsSuccessStatusCode.Should().BeTrue("File should exist before deletion");

            // Act - Delete
            var deleteResponse = await client.DeleteAsync(remotePath);
            deleteResponse.IsSuccessStatusCode.Should().BeTrue("File deletion should succeed");

            // Assert
            var getResponseAfter = await client.GetAsync(remotePath);
            getResponseAfter.IsSuccessStatusCode.Should().BeFalse("File should not exist after deletion");
            getResponseAfter.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound, "Deleted file should return 404");
        }
        finally
        {
            // Cleanup (in case deletion failed)
            try
            {
                await client.DeleteAsync(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }

    [Fact]
    public async Task WebDAV_FullCycle_CRUD()
    {
        // Arrange
        var (_, client, _, _) = await GetWebDavClientAsync();
        var fileName = TestHelpers.GenerateUniqueFileName("webdav-crud");
        var remotePath = $"/output/{fileName}";
        var originalContent = CreateTestContent();
        var updatedContent = CreateTestContent();

        try
        {
            // CREATE
            var createRequest = new HttpRequestMessage(HttpMethod.Put, remotePath)
            {
                Content = new StringContent(originalContent)
            };
            var createResponse = await client.SendAsync(createRequest);
            createResponse.IsSuccessStatusCode.Should().BeTrue("File creation should succeed");

            // READ
            var readResponse1 = await client.GetAsync(remotePath);
            readResponse1.IsSuccessStatusCode.Should().BeTrue("File read should succeed");
            var readContent1 = await readResponse1.Content.ReadAsStringAsync();
            readContent1.Should().Be(originalContent, "Read content should match original");

            // UPDATE
            var updateRequest = new HttpRequestMessage(HttpMethod.Put, remotePath)
            {
                Content = new StringContent(updatedContent)
            };
            var updateResponse = await client.SendAsync(updateRequest);
            updateResponse.IsSuccessStatusCode.Should().BeTrue("File update should succeed");

            // Verify UPDATE
            var readResponse2 = await client.GetAsync(remotePath);
            var readContent2 = await readResponse2.Content.ReadAsStringAsync();
            readContent2.Should().Be(updatedContent, "Content should be updated");

            // DELETE
            var deleteResponse = await client.DeleteAsync(remotePath);
            deleteResponse.IsSuccessStatusCode.Should().BeTrue("File deletion should succeed");

            // Verify DELETE
            var readResponse3 = await client.GetAsync(remotePath);
            readResponse3.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound, "Deleted file should return 404");
        }
        finally
        {
            // Cleanup
            try
            {
                await client.DeleteAsync(remotePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
            client.Dispose();
        }
    }
}
