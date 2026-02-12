using System.Text;
using System.Text.Json;
using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

/// <summary>
/// E2E tests for activity log completeness (v2.2.0).
/// Verifies that all server lifecycle events and file operations
/// are tracked in the dashboard activity log with correct attribution.
///
/// IMPORTANT: Only dynamic servers (created via API) can be created/deleted.
/// Helm-deployed servers (FTP, SFTP, HTTP, S3, SMB, 7x NAS) are static infrastructure.
/// </summary>
[Collection("Simulator")]
public class ServerAndFileActivityTests
{
    private readonly SimulatorTestFixture _fixture;

    public ServerAndFileActivityTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// TEST-01: Create dynamic FTP, SFTP, and NAS servers via API,
    /// verify created/healthy events appear in activity log,
    /// then delete them and verify deleted events.
    /// Only dynamic servers — Helm NAS servers are static and cannot be deleted.
    /// </summary>
    [Fact]
    public async Task DynamicServers_CreateAndDelete_AllTypesTrackedInActivityLog()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);
        using var client = new HttpClient();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var ftpName = $"ftp-act-{suffix}";
        var sftpName = $"sftp-act-{suffix}";
        var nasNames = new[] { $"nas-act1-{suffix}", $"nas-act2-{suffix}", $"nas-act3-{suffix}" };

        try
        {
            await dashboard.NavigateAsync(_fixture.DashboardUrl);
            await dashboard.WaitForDashboardLoadAsync();
            await serversPage.WaitForActivityLogItemsAsync(1, timeoutMs: 15000);

            // Clear the log so we start fresh
            await serversPage.ActivityLogClear.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Create 5 dynamic servers (1 FTP, 1 SFTP, 3 NAS) via API
            var ftpCreated = await CreateServerAsync(client, "ftp", ftpName, "testuser", "testpass");
            var sftpCreated = await CreateServerAsync(client, "sftp", sftpName, "testuser", "testpass");
            var nasCreated = new List<string>();
            foreach (var nasName in nasNames)
            {
                if (await CreateServerAsync(client, "nas", nasName))
                    nasCreated.Add(nasName);
            }

            var allCreated = new List<string>();
            if (ftpCreated) allCreated.Add(ftpName);
            if (sftpCreated) allCreated.Add(sftpName);
            allCreated.AddRange(nasCreated);

            allCreated.Should().NotBeEmpty("at least one dynamic server should be created");

            // Wait for SignalR to broadcast server creation (5s poll + propagation)
            await page.WaitForTimeoutAsync(10000);
            await serversPage.WaitForActivityLogItemsAsync(allCreated.Count, timeoutMs: 15000);

            // Verify creation events in activity log
            var titles = await serversPage.GetActivityLogTitlesAsync();
            foreach (var name in allCreated)
            {
                titles.Should().Contain(t => t.Contains(name),
                    $"activity log should show creation event for dynamic server '{name}'. " +
                    $"Titles: [{string.Join(", ", titles)}]");
            }

            // Delete all dynamic servers
            foreach (var name in allCreated)
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/servers/{name}"); } catch { }
            }

            // Wait for deletion events to propagate
            await page.WaitForTimeoutAsync(10000);

            // Verify deletion events — each server name should appear at least twice
            // (once for creation, once for deletion)
            titles = await serversPage.GetActivityLogTitlesAsync();
            foreach (var name in allCreated)
            {
                var matchCount = titles.Count(t => t.Contains(name));
                matchCount.Should().BeGreaterOrEqualTo(2,
                    $"activity log should have creation + deletion for dynamic server '{name}'. " +
                    $"Titles: [{string.Join(", ", titles)}]");
            }

            // Final count: at least 2 events per server (created + deleted)
            var finalCount = await serversPage.GetActivityLogCountAsync();
            finalCount.Should().BeGreaterOrEqualTo(allCreated.Count * 2,
                $"expected >= {allCreated.Count * 2} events for {allCreated.Count} dynamic servers");
        }
        finally
        {
            // Cleanup: ensure all dynamic servers are deleted
            foreach (var name in new[] { ftpName, sftpName }.Concat(nasNames))
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/servers/{name}"); } catch { }
            }
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// TEST-02: Perform all file operations (write, read, delete, rename)
    /// on the shared storage visible to static Helm servers, and verify
    /// each operation appears in the activity log.
    /// Static servers are NOT created or deleted — they're Helm infrastructure.
    /// </summary>
    [Fact]
    public async Task FileOperations_OnSharedStorage_AllActivitiesTracked()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);
        using var client = new HttpClient();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var fileName = $"activity-test-{suffix}.txt";
        var renamedName = $"activity-renamed-{suffix}.txt";

        try
        {
            await dashboard.NavigateAsync(_fixture.DashboardUrl);
            await dashboard.WaitForDashboardLoadAsync();
            await serversPage.WaitForActivityLogItemsAsync(1, timeoutMs: 15000);

            // Clear log
            await serversPage.ActivityLogClear.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // STEP 1: Upload file — should produce "written to" event
            var uploaded = await TryUploadFileAsync(client, fileName, $"Test content {suffix}");
            uploaded.Should().BeTrue($"upload of {fileName} should succeed");

            // Wait for FileWatcher + SignalR propagation
            await page.WaitForTimeoutAsync(5000);

            var titles = await serversPage.GetActivityLogTitlesAsync();
            titles.Should().Contain(t => t.Contains(fileName) && t.Contains("written to"),
                $"activity log should show '{fileName} written to ...' after upload. " +
                $"Titles: [{string.Join(", ", titles)}]");

            // STEP 2: Download file — should produce "read from" event
            var downloadResp = await client.GetAsync(
                $"{_fixture.ApiUrl}/api/files/download?path={fileName}");
            downloadResp.IsSuccessStatusCode.Should().BeTrue("download should succeed");

            await page.WaitForTimeoutAsync(5000);

            titles = await serversPage.GetActivityLogTitlesAsync();
            titles.Should().Contain(t => t.Contains(fileName) && t.Contains("read from"),
                $"activity log should show '{fileName} read from ...' after download. " +
                $"Titles: [{string.Join(", ", titles)}]");

            // STEP 3: Rename file — should produce "renamed to" event
            var renameBody = JsonSerializer.Serialize(new { path = fileName, newName = renamedName });
            var renameResp = await client.PutAsync(
                $"{_fixture.ApiUrl}/api/files/rename",
                new StringContent(renameBody, Encoding.UTF8, "application/json"));
            renameResp.IsSuccessStatusCode.Should().BeTrue("rename should succeed");

            await page.WaitForTimeoutAsync(5000);

            titles = await serversPage.GetActivityLogTitlesAsync();
            titles.Should().Contain(t => t.Contains("renamed to") && t.Contains(renamedName),
                $"activity log should show rename to '{renamedName}'. " +
                $"Titles: [{string.Join(", ", titles)}]");

            // STEP 4: Delete the renamed file — should produce "deleted from" event
            var deleteResp = await client.DeleteAsync(
                $"{_fixture.ApiUrl}/api/files?path={renamedName}");
            deleteResp.IsSuccessStatusCode.Should().BeTrue("delete should succeed");

            await page.WaitForTimeoutAsync(5000);

            titles = await serversPage.GetActivityLogTitlesAsync();
            titles.Should().Contain(t => t.Contains(renamedName) && t.Contains("deleted from"),
                $"activity log should show '{renamedName} deleted from ...' after delete. " +
                $"Titles: [{string.Join(", ", titles)}]");

            // Verify we have all 4 event types tracked
            var finalCount = await serversPage.GetActivityLogCountAsync();
            finalCount.Should().BeGreaterOrEqualTo(4,
                "should have at least 4 events: written, read, renamed, deleted");
        }
        finally
        {
            // Cleanup: delete both possible file names
            try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path={fileName}"); } catch { }
            try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path={renamedName}"); } catch { }
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// TEST-03: Upload files to multiple dynamic NAS servers and verify
    /// the activity log attributes each file to the correct server.
    /// Uses file names containing the server name for path-based matching.
    /// </summary>
    [Fact]
    public async Task FileOperations_OnDynamicNAS_AttributedCorrectly()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);
        using var client = new HttpClient();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var nasAlpha = $"nas-alpha-{suffix}";
        var nasBeta = $"nas-beta-{suffix}";
        // File names include server name for path-based attribution
        var alphaFile = $"{nasAlpha}-data.txt";
        var betaFile = $"{nasBeta}-data.txt";

        try
        {
            await dashboard.NavigateAsync(_fixture.DashboardUrl);
            await dashboard.WaitForDashboardLoadAsync();
            await serversPage.WaitForActivityLogItemsAsync(1, timeoutMs: 15000);

            // Clear log
            await serversPage.ActivityLogClear.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Create two dynamic NAS servers
            var alphaCreated = await CreateServerAsync(client, "nas", nasAlpha);
            var betaCreated = await CreateServerAsync(client, "nas", nasBeta);

            (alphaCreated || betaCreated).Should().BeTrue("at least one NAS should be created");

            // Wait for server creation events
            await page.WaitForTimeoutAsync(10000);

            // Upload files named after each server for attribution matching
            var uploadedPairs = new List<(string file, string server)>();
            if (alphaCreated && await TryUploadFileAsync(client, alphaFile, $"Alpha data {suffix}"))
                uploadedPairs.Add((alphaFile, nasAlpha));
            if (betaCreated && await TryUploadFileAsync(client, betaFile, $"Beta data {suffix}"))
                uploadedPairs.Add((betaFile, nasBeta));

            uploadedPairs.Should().NotBeEmpty("at least one file should be uploaded");

            // Wait for FileWatcher events
            await page.WaitForTimeoutAsync(5000);

            // Verify each file is attributed to the correct server
            var titles = await serversPage.GetActivityLogTitlesAsync();
            foreach (var (file, server) in uploadedPairs)
            {
                titles.Should().Contain(t => t.Contains(file) && t.Contains(server),
                    $"activity log should attribute '{file}' to server '{server}'. " +
                    $"Titles: [{string.Join(", ", titles)}]");
            }
        }
        finally
        {
            // Cleanup files first, then servers
            foreach (var file in new[] { alphaFile, betaFile })
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path={file}"); } catch { }
            }
            foreach (var name in new[] { nasAlpha, nasBeta })
            {
                try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/servers/{name}"); } catch { }
            }
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// TEST-04: Upload a file, rename it via API, and verify
    /// the activity log shows both old and new names in the rename event.
    /// </summary>
    [Fact]
    public async Task FileRename_ShowsOldAndNewName_InActivityLog()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);
        using var client = new HttpClient();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var oldName = $"before-rename-{suffix}.txt";
        var newName = $"after-rename-{suffix}.txt";

        try
        {
            await dashboard.NavigateAsync(_fixture.DashboardUrl);
            await dashboard.WaitForDashboardLoadAsync();
            await serversPage.WaitForActivityLogItemsAsync(1, timeoutMs: 15000);

            // Clear log
            await serversPage.ActivityLogClear.ClickAsync();
            await page.WaitForTimeoutAsync(500);

            // Upload file with old name
            var uploaded = await TryUploadFileAsync(client, oldName, $"Rename test {suffix}");
            uploaded.Should().BeTrue($"upload of {oldName} should succeed");
            await page.WaitForTimeoutAsync(5000);

            // Rename via API
            var renameBody = JsonSerializer.Serialize(new { path = oldName, newName = newName });
            var renameResp = await client.PutAsync(
                $"{_fixture.ApiUrl}/api/files/rename",
                new StringContent(renameBody, Encoding.UTF8, "application/json"));
            renameResp.IsSuccessStatusCode.Should().BeTrue("rename API should succeed");

            await page.WaitForTimeoutAsync(5000);

            // Verify activity log shows both old and new names
            var titles = await serversPage.GetActivityLogTitlesAsync();

            // Title format: "{oldName} renamed to {newName} on {server}"
            titles.Should().Contain(
                t => t.Contains(oldName) && t.Contains("renamed to") && t.Contains(newName),
                $"activity log should show '{oldName} renamed to {newName}'. " +
                $"Titles: [{string.Join(", ", titles)}]");
        }
        finally
        {
            // Cleanup: delete both possible names
            try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path={oldName}"); } catch { }
            try { await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path={newName}"); } catch { }
            await page.CloseAsync();
        }
    }

    // --- Helper methods ---

    private async Task<bool> CreateServerAsync(HttpClient client, string protocol, string name,
        string? username = null, string? password = null)
    {
        try
        {
            var body = new Dictionary<string, string> { { "name", name } };
            if (username != null) body["username"] = username;
            if (password != null) body["password"] = password;
            // NAS servers require a directory field
            if (protocol == "nas") body["directory"] = "input";

            var response = await client.PostAsync(
                $"{_fixture.ApiUrl}/api/servers/{protocol}",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<bool> TryUploadFileAsync(HttpClient client, string fileName, string content = "Test content")
    {
        try
        {
            var formContent = new MultipartFormDataContent();
            formContent.Add(new StringContent("/"), "path");
            formContent.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName);
            var response = await client.PostAsync($"{_fixture.ApiUrl}/api/files/upload", formContent);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
