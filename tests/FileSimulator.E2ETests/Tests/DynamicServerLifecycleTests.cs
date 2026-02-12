using System.Text;
using System.Text.Json;
using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class DynamicServerLifecycleTests
{
    private readonly SimulatorTestFixture _fixture;

    public DynamicServerLifecycleTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task FullLifecycle_CreatesServersUploadsFilesDeletesAll_ActivityLogTracksEverything()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);
        using var client = new HttpClient();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var ftpName = $"ftp-e2e-{suffix}";
        var sftpName = $"sftp-e2e-{suffix}";
        var nasName = $"nas-e2e-{suffix}";

        // File names include server name so path-matching links them to servers
        var ftpFile = $"{ftpName}-testfile.txt";
        var sftpFile = $"{sftpName}-testfile.txt";
        var nasFile = $"{nasName}-testfile.txt";

        try
        {
            // Navigate and wait for initial load
            await dashboard.NavigateAsync(_fixture.DashboardUrl);
            await dashboard.WaitForDashboardLoadAsync();
            await serversPage.WaitForActivityLogItemsAsync(1, timeoutMs: 15000);

            // Clear the log so we start fresh
            await serversPage.ActivityLogClear.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // ================================================================
            // PHASE 1: Create dynamic servers via API
            // ================================================================

            var ftpCreated = await CreateServerAsync(client, "ftp", ftpName, "testuser", "testpass");
            var sftpCreated = await CreateServerAsync(client, "sftp", sftpName, "testuser", "testpass");
            var nasCreated = await CreateServerAsync(client, "nas", nasName);

            (ftpCreated || sftpCreated || nasCreated).Should().BeTrue(
                "at least one dynamic server should be created successfully");

            var createdServers = new List<string>();
            if (ftpCreated) createdServers.Add(ftpName);
            if (sftpCreated) createdServers.Add(sftpName);
            if (nasCreated) createdServers.Add(nasName);

            // Wait for SignalR to broadcast updated server list (every 5s)
            await page.WaitForTimeoutAsync(8000);
            await serversPage.WaitForActivityLogItemsAsync(createdServers.Count, timeoutMs: 15000);

            // Verify server creation events — title is the server name
            var titles = await serversPage.GetActivityLogTitlesAsync();
            foreach (var serverName in createdServers)
            {
                titles.Should().Contain(t => t.Contains(serverName),
                    $"activity log should show creation event for '{serverName}'. " +
                    $"Titles: [{string.Join(", ", titles)}]");
            }

            // ================================================================
            // PHASE 2: Upload files named after each server
            // Title format: "filename written to servername"
            // ================================================================

            var uploadedFiles = new List<(string file, string server)>();
            if (ftpCreated && await TryUploadFileAsync(client, ftpFile))
                uploadedFiles.Add((ftpFile, ftpName));
            if (sftpCreated && await TryUploadFileAsync(client, sftpFile))
                uploadedFiles.Add((sftpFile, sftpName));
            if (nasCreated && await TryUploadFileAsync(client, nasFile))
                uploadedFiles.Add((nasFile, nasName));

            if (uploadedFiles.Count > 0)
            {
                // Wait for FileWatcher events + SignalR propagation
                await page.WaitForTimeoutAsync(8000);

                // Verify file write events: "filename written to servername"
                titles = await serversPage.GetActivityLogTitlesAsync();
                foreach (var (file, server) in uploadedFiles)
                {
                    titles.Should().Contain(t => t.Contains(file) && t.Contains(server),
                        $"activity log should show '{file} written to {server}'. " +
                        $"Titles: [{string.Join(", ", titles)}]");
                }

                // ================================================================
                // PHASE 3: Read files via API (verify content)
                // Note: file reads don't trigger FileWatcher events
                // ================================================================

                foreach (var (file, server) in uploadedFiles)
                {
                    var response = await client.GetAsync(
                        $"{_fixture.ApiUrl}/api/files/download?path=/{file}");
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        content.Should().Contain("Test content",
                            $"downloaded {file} from {server} should contain uploaded content");
                    }
                }

                // ================================================================
                // PHASE 4: Delete files — "filename deleted from servername"
                // ================================================================

                var deletedFiles = new List<(string file, string server)>();
                foreach (var (file, server) in uploadedFiles)
                {
                    try
                    {
                        var resp = await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path=/{file}");
                        if (resp.IsSuccessStatusCode) deletedFiles.Add((file, server));
                    }
                    catch { }
                }

                if (deletedFiles.Count > 0)
                {
                    await page.WaitForTimeoutAsync(5000);

                    titles = await serversPage.GetActivityLogTitlesAsync();
                    foreach (var (file, server) in deletedFiles)
                    {
                        titles.Should().Contain(t => t.Contains(file) && t.Contains("deleted from") && t.Contains(server),
                            $"activity log should show '{file} deleted from {server}'. " +
                            $"Titles: [{string.Join(", ", titles)}]");
                    }
                }
            }

            // ================================================================
            // PHASE 5: Delete dynamic servers via API
            // ================================================================

            foreach (var serverName in createdServers)
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/servers/{serverName}"); } catch { }
            }

            // Wait for SignalR to broadcast updated server list
            await page.WaitForTimeoutAsync(8000);

            // Verify server deletion events — each server name appears >= 2 times
            titles = await serversPage.GetActivityLogTitlesAsync();
            foreach (var serverName in createdServers)
            {
                var matchCount = titles.Count(t => t.Contains(serverName));
                matchCount.Should().BeGreaterOrEqualTo(2,
                    $"activity log should have creation + deletion for '{serverName}'. " +
                    $"Titles: [{string.Join(", ", titles)}]");
            }

            // ================================================================
            // PHASE 6: Verify final activity log state
            // ================================================================

            var finalCount = await serversPage.GetActivityLogCountAsync();
            var minExpected = createdServers.Count * 2; // created + deleted
            finalCount.Should().BeGreaterOrEqualTo(minExpected,
                $"activity log should have >= {minExpected} events " +
                $"(created + deleted for {createdServers.Count} servers)");
        }
        finally
        {
            foreach (var fileName in new[] { ftpFile, sftpFile, nasFile })
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path=/{fileName}"); } catch { }
            }
            foreach (var serverName in new[] { ftpName, sftpName, nasName })
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/servers/{serverName}"); } catch { }
            }
            await page.CloseAsync();
        }
    }

    private async Task<bool> CreateServerAsync(HttpClient client, string protocol, string name,
        string? username = null, string? password = null)
    {
        try
        {
            var body = new Dictionary<string, string> { { "name", name } };
            if (username != null) body["username"] = username;
            if (password != null) body["password"] = password;

            var response = await client.PostAsync(
                $"{_fixture.ApiUrl}/api/servers/{protocol}",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<bool> TryUploadFileAsync(HttpClient client, string fileName)
    {
        try
        {
            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent("/"), "path");
            formContent.Add(new ByteArrayContent(Encoding.UTF8.GetBytes($"Test content for {fileName}")), "file", fileName);
            var response = await client.PostAsync($"{_fixture.ApiUrl}/api/files/upload", formContent);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
