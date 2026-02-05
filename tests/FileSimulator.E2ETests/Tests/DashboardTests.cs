using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class DashboardTests
{
    private readonly SimulatorTestFixture _fixture;

    public DashboardTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_ShowsAllTabs()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var tabs = await dashboard.GetVisibleTabsAsync();

        tabs.Should().HaveCount(5);
        tabs.Should().Contain("Servers");
        tabs.Should().Contain("Files");
        tabs.Should().Contain("History");
        tabs.Should().Contain("Kafka");
        tabs.Should().Contain("Alerts");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Dashboard_DisplaysServerCount()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var serverCount = await dashboard.GetServerCountAsync();

        serverCount.Should().BeGreaterThan(0, "should have at least one server");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsConnectionStatus()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        var status = await dashboard.GetConnectionStatusAsync();

        status.Should().Be("connected", "SignalR should be connected");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Dashboard_TabNavigation_Works()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Servers tab should be active by default
        var isServersActive = await dashboard.IsTabActiveAsync("Servers");
        isServersActive.Should().BeTrue("Servers tab should be active by default");

        // Switch to Files tab
        await dashboard.SwitchToTabAsync("Files");
        var isFilesActive = await dashboard.IsTabActiveAsync("Files");
        isFilesActive.Should().BeTrue("Files tab should be active after switching");

        // Switch to History tab
        await dashboard.SwitchToTabAsync("History");
        var isHistoryActive = await dashboard.IsTabActiveAsync("History");
        isHistoryActive.Should().BeTrue("History tab should be active after switching");

        // Switch to Kafka tab
        await dashboard.SwitchToTabAsync("Kafka");
        var isKafkaActive = await dashboard.IsTabActiveAsync("Kafka");
        isKafkaActive.Should().BeTrue("Kafka tab should be active after switching");

        // Switch to Alerts tab
        await dashboard.SwitchToTabAsync("Alerts");
        var isAlertsActive = await dashboard.IsTabActiveAsync("Alerts");
        isAlertsActive.Should().BeTrue("Alerts tab should be active after switching");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Dashboard_ResponsiveLayout()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Test desktop size (1920x1080 - default)
        await page.SetViewportSizeAsync(1920, 1080);
        var tabsDesktop = await dashboard.GetVisibleTabsAsync();
        tabsDesktop.Should().HaveCount(5, "all tabs should be visible on desktop");

        // Test tablet size
        await page.SetViewportSizeAsync(768, 1024);
        var tabsTablet = await dashboard.GetVisibleTabsAsync();
        tabsTablet.Should().HaveCount(5, "all tabs should be visible on tablet");

        // Test mobile size
        await page.SetViewportSizeAsync(375, 667);
        var tabsMobile = await dashboard.GetVisibleTabsAsync();
        tabsMobile.Should().HaveCount(5, "all tabs should be visible on mobile");

        // Header should still be visible
        var headerVisible = await dashboard.HeaderTitle.IsVisibleAsync();
        headerVisible.Should().BeTrue("header should be visible on mobile");

        await page.CloseAsync();
    }
}
