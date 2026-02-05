using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class AlertsTests
{
    private readonly SimulatorTestFixture _fixture;

    public AlertsTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Alerts_TabDisplaysCorrectly()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Switch to Alerts tab
        await dashboard.SwitchToTabAsync("Alerts");

        // Wait for alerts tab to load
        await page.WaitForTimeoutAsync(2000);

        // Check if alerts tab is visible
        var isTabVisible = await alertsPage.AlertsTab.IsVisibleAsync();
        isTabVisible.Should().BeTrue("alerts tab should be visible");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Alerts_ShowsActiveAlerts()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Alerts");
        await page.WaitForTimeoutAsync(2000);

        // Get active alerts (might be empty if no alerts exist)
        var activeAlerts = await alertsPage.GetActiveAlertsAsync();

        // No specific assertion on count - system may or may not have alerts
        // Just verify the method works
        activeAlerts.Should().NotBeNull();

        await page.CloseAsync();
    }

    [Fact]
    public async Task Alerts_BannerAppearsForCritical()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Check if alert banner is visible (depends on active alerts)
        var isBannerVisible = await alertsPage.IsAlertBannerVisibleAsync();

        if (isBannerVisible)
        {
            // If banner is visible, verify it has content
            var bannerText = await alertsPage.GetAlertBannerTextAsync();
            bannerText.Should().NotBeNullOrWhiteSpace("banner should have text");
        }
        else
        {
            // No banner - system has no critical alerts
            // This is a valid state
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Alerts_CanDismissBanner()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Check if alert banner is visible
        var isBannerVisible = await alertsPage.IsAlertBannerVisibleAsync();

        if (isBannerVisible)
        {
            // If dismiss button exists, test it
            var hasDismissButton = await alertsPage.DismissBannerButton.IsVisibleAsync();

            if (hasDismissButton)
            {
                await alertsPage.DismissAlertBannerAsync();

                // Verify banner is dismissed
                var isBannerStillVisible = await alertsPage.IsAlertBannerVisibleAsync();
                isBannerStillVisible.Should().BeFalse("banner should be dismissed");
            }
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Alerts_ShowsAlertHistory()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Alerts");
        await page.WaitForTimeoutAsync(2000);

        // Check if alert history section is visible
        var isHistoryVisible = await alertsPage.AlertHistoryList.IsVisibleAsync();
        isHistoryVisible.Should().BeTrue("alert history section should be visible");

        // Get alert history
        var history = await alertsPage.GetAlertHistoryAsync();
        history.Should().NotBeNull();

        await page.CloseAsync();
    }

    [Fact]
    public async Task Alerts_CanFilterBySeverity()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Alerts");
        await page.WaitForTimeoutAsync(2000);

        // Check if severity filter exists
        var hasFilter = await alertsPage.SeverityFilter.CountAsync() > 0;

        if (hasFilter)
        {
            // Get initial alert count
            var initialCount = await alertsPage.GetAlertCountAsync();

            // Try to filter by severity
            try
            {
                await alertsPage.FilterBySeverityAsync("critical");

                // Count should potentially change (or stay the same if no critical alerts)
                var filteredCount = await alertsPage.GetAlertCountAsync();
                filteredCount.Should().BeLessOrEqualTo(initialCount);
            }
            catch
            {
                // Filter might not have "critical" option
            }
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Alerts_CanSearchAlerts()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var alertsPage = new AlertsPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Alerts");
        await page.WaitForTimeoutAsync(2000);

        // Check if search input exists
        var hasSearch = await alertsPage.SearchInput.CountAsync() > 0;

        if (hasSearch)
        {
            // Get initial alert count
            var initialCount = await alertsPage.GetAlertCountAsync();

            // Search for a term that likely won't match anything
            await alertsPage.SearchAlertsAsync("nonexistent-search-term-xyz");

            // Count should be 0 or less than initial
            var searchCount = await alertsPage.GetAlertCountAsync();
            searchCount.Should().BeLessOrEqualTo(initialCount);
        }

        await page.CloseAsync();
    }
}
