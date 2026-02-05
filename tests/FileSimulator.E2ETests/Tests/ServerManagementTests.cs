using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class ServerManagementTests
{
    private readonly SimulatorTestFixture _fixture;

    public ServerManagementTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Servers_DisplaysAllConfiguredServers()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var serverNames = await serversPage.GetAllServerNamesAsync();

        serverNames.Should().NotBeEmpty("should have configured servers");
        serverNames.Should().Contain(name => name.Contains("nas"), "should have NAS servers");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Servers_ShowsHealthStatus()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var serverNames = await serversPage.GetAllServerNamesAsync();
        serverNames.Should().NotBeEmpty();

        // Check health status of first server
        var firstServer = serverNames[0];
        var health = await serversPage.GetServerHealthAsync(firstServer);

        health.Should().BeOneOf("healthy", "degraded", "unhealthy", "unknown");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Servers_CanViewServerDetails()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var serverNames = await serversPage.GetAllServerNamesAsync();
        serverNames.Should().NotBeEmpty();

        // Click first server to open details panel
        var firstServer = serverNames[0];
        await serversPage.SelectServerAsync(firstServer);

        var details = await serversPage.GetServerDetailsAsync();
        details.Should().ContainKey("name");

        // Close details panel
        await serversPage.CloseDetailsPanelAsync();

        await page.CloseAsync();
    }

    [Fact]
    public async Task Servers_CanCreateDynamicServer()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var testServerName = $"test-ftp-{Guid.NewGuid():N}";

        try
        {
            // Open create server dialog
            await serversPage.OpenCreateServerDialogAsync();

            // Fill server details
            await serversPage.FillServerDetailsAsync(
                name: testServerName,
                protocol: "ftp",
                username: "testuser",
                password: "testpass"
            );

            // Submit
            await serversPage.SubmitServerCreationAsync();

            // Wait for server to appear in list
            await serversPage.WaitForServerInListAsync(testServerName, timeoutMs: 60000);

            // Verify server appears
            var serverNames = await serversPage.GetAllServerNamesAsync();
            serverNames.Should().Contain(testServerName);
        }
        finally
        {
            // Cleanup: delete the test server
            try
            {
                await serversPage.DeleteServerAsync(testServerName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Servers_CanDeleteDynamicServer()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var testServerName = $"test-sftp-{Guid.NewGuid():N}";

        // Create server first
        await serversPage.OpenCreateServerDialogAsync();
        await serversPage.FillServerDetailsAsync(
            name: testServerName,
            protocol: "sftp",
            username: "testuser",
            password: "testpass"
        );
        await serversPage.SubmitServerCreationAsync();
        await serversPage.WaitForServerInListAsync(testServerName, timeoutMs: 60000);

        // Now delete it
        await serversPage.DeleteServerAsync(testServerName);

        // Wait a moment for deletion to process
        await page.WaitForTimeoutAsync(2000);

        // Verify server is removed
        var serverNames = await serversPage.GetAllServerNamesAsync();
        serverNames.Should().NotContain(testServerName);

        await page.CloseAsync();
    }

    [Fact]
    public async Task Servers_CreateServerValidation()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var serversPage = new ServersPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Open create server dialog
        await serversPage.OpenCreateServerDialogAsync();

        // Try to submit with empty name
        await serversPage.FillServerDetailsAsync(
            name: "",
            protocol: "ftp"
        );
        await serversPage.SubmitServerCreationAsync();

        // Should show validation error
        var hasError = await serversPage.HasValidationErrorAsync();
        hasError.Should().BeTrue("empty name should show validation error");

        // Cancel dialog
        await serversPage.CancelButton.ClickAsync();

        await page.CloseAsync();
    }
}
