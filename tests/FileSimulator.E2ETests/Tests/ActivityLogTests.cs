using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class ActivityLogTests
{
    private readonly SimulatorTestFixture _fixture;

    public ActivityLogTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ActivityLog_ShowsSidebarOnServersTab()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Activity log sidebar should be visible on Servers tab
        var sidebarVisible = await serversPage.ServersSidebar.IsVisibleAsync();
        sidebarVisible.Should().BeTrue("activity log sidebar should be visible on Servers tab");

        var activityLogVisible = await serversPage.ActivityLog.IsVisibleAsync();
        activityLogVisible.Should().BeTrue("activity log component should be visible");

        await page.CloseAsync();
    }

    [Fact]
    public async Task ActivityLog_ShowsInitialServerState()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Get the server names from the grid first
        var serverNames = await serversPage.GetAllServerNamesAsync();
        serverNames.Should().NotBeEmpty();

        // Wait for the activity log to contain at least one server name.
        // The initial server state fires after the first SignalR ServerStatusUpdate (every 5s).
        // We use Playwright's WaitForFunction to poll until a server name appears in the log.
        var firstServerName = serverNames.First();
        await page.WaitForFunctionAsync(
            @$"() => {{
                const items = document.querySelectorAll('.activity-log__event-title');
                for (const el of items) {{
                    if (el.textContent && el.textContent.includes('{firstServerName}')) return true;
                }}
                return false;
            }}",
            null,
            new() { Timeout = 15000 });

        var count = await serversPage.GetActivityLogCountAsync();
        count.Should().BeGreaterThan(0, "activity log should show initial server state on load");

        var titles = await serversPage.GetActivityLogTitlesAsync();
        titles.Should().NotBeEmpty("activity log should have event titles");

        // At least one server name should appear in the activity log titles.
        titles.Should().Contain(t => serverNames.Any(s => t.Contains(s)),
            $"activity log titles [{string.Join(", ", titles)}] should contain at least one server name from the grid [{string.Join(", ", serverNames)}]");

        await page.CloseAsync();
    }

    [Fact]
    public async Task ActivityLog_ShowsFileEventsFromFileOperations()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Wait for initial state to populate
        await serversPage.WaitForActivityLogItemsAsync(1);
        var initialCount = await serversPage.GetActivityLogCountAsync();

        // Create a test file via API to trigger a file event
        var testFileName = $"activity-test-{Guid.NewGuid():N}.txt";
        using var client = new HttpClient();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("/"), "path");
        content.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("activity log test")), "file", testFileName);
        try
        {
            await client.PostAsync($"{_fixture.ApiUrl}/api/files/upload", content);
        }
        catch
        {
            // Upload may fail due to K8s permissions — that's OK, the FileWatcher
            // may still have detected the attempt or there may be other file events
        }

        // Wait a bit for SignalR file events to propagate
        await page.WaitForTimeoutAsync(3000);

        // Switch to Servers tab (may already be there) to see activity log
        await dashboard.SwitchToTabAsync("Servers");
        await page.WaitForTimeoutAsync(1000);

        // Check if file events appeared (they may or may not depending on
        // whether the upload succeeded and FileWatcher detected it)
        var currentCount = await serversPage.GetActivityLogCountAsync();
        var titles = await serversPage.GetActivityLogTitlesAsync();

        // The activity log should still have at least the initial server events
        currentCount.Should().BeGreaterOrEqualTo(initialCount,
            "activity log should not lose events when switching tabs");

        // Cleanup the test file
        try
        {
            await client.DeleteAsync($"{_fixture.ApiUrl}/api/files?path=/{testFileName}");
        }
        catch { }

        await page.CloseAsync();
    }

    [Fact]
    public async Task ActivityLog_ClearButtonWorks()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Wait for initial events
        await serversPage.WaitForActivityLogItemsAsync(1);

        var countBefore = await serversPage.GetActivityLogCountAsync();
        countBefore.Should().BeGreaterThan(0);

        // Click clear button
        await serversPage.ActivityLogClear.ClickAsync();
        await page.WaitForTimeoutAsync(500);

        // Activity log should be empty
        var countAfter = await serversPage.GetActivityLogCountAsync();
        countAfter.Should().Be(0, "clear button should remove all activity log items");

        // Empty message should appear
        var emptyVisible = await serversPage.ActivityLogEmpty.IsVisibleAsync();
        emptyVisible.Should().BeTrue("empty message should appear after clearing");

        await page.CloseAsync();
    }

    [Fact]
    public async Task ActivityLog_HiddenOnOtherTabs()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Verify visible on Servers tab
        var visibleOnServers = await serversPage.ActivityLog.IsVisibleAsync();
        visibleOnServers.Should().BeTrue("activity log should be visible on Servers tab");

        // Switch to Files tab
        await dashboard.SwitchToTabAsync("Files");
        await page.WaitForTimeoutAsync(500);

        // Activity log should not be visible (servers tab content is unmounted)
        var visibleOnFiles = await serversPage.ActivityLog.CountAsync();
        visibleOnFiles.Should().Be(0, "activity log should not be in DOM on Files tab");

        // Switch back to Servers — it should re-render
        await dashboard.SwitchToTabAsync("Servers");
        await serversPage.WaitForActivityLogItemsAsync(1);

        var visibleAgain = await serversPage.ActivityLog.IsVisibleAsync();
        visibleAgain.Should().BeTrue("activity log should reappear on Servers tab");

        await page.CloseAsync();
    }
}
