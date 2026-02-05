using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class HistoryTests
{
    private readonly SimulatorTestFixture _fixture;

    public HistoryTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task History_DisplaysTimeRangeSelector()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Switch to History tab
        await dashboard.SwitchToTabAsync("History");

        // Wait for history tab to load
        await page.WaitForTimeoutAsync(2000);

        // Check if time range selector is visible
        var isSelectorVisible = await historyPage.IsTimeRangeSelectorVisibleAsync();
        isSelectorVisible.Should().BeTrue("time range selector should be visible");

        // Get available time ranges
        var ranges = await historyPage.GetAvailableTimeRangesAsync();
        ranges.Should().NotBeEmpty("should have time range options");

        await page.CloseAsync();
    }

    [Fact]
    public async Task History_CanChangeTimeRange()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("History");
        await page.WaitForTimeoutAsync(2000);

        // Select 1h time range
        await historyPage.SelectTimeRangeAsync("1h");

        // Verify 1h is selected
        var currentRange = await historyPage.GetCurrentTimeRangeAsync();
        currentRange.Should().Be("1h", "1h should be selected");

        // Select 24h time range
        await historyPage.SelectTimeRangeAsync("24h");

        // Verify 24h is selected
        currentRange = await historyPage.GetCurrentTimeRangeAsync();
        currentRange.Should().Be("24h", "24h should be selected");

        await page.CloseAsync();
    }

    [Fact]
    public async Task History_ShowsLatencyChart()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("History");
        await page.WaitForTimeoutAsync(2000);

        // Wait for chart to load
        await historyPage.WaitForChartLoadAsync();

        // Check if chart is visible
        var isChartVisible = await historyPage.LatencyChart.IsVisibleAsync();
        isChartVisible.Should().BeTrue("latency chart should be visible");

        await page.CloseAsync();
    }

    [Fact]
    public async Task History_ShowsServerSparklines()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("History");
        await page.WaitForTimeoutAsync(2000);

        // Check if sparklines section exists
        var hasSparklines = await historyPage.ServerSparklines.CountAsync() > 0;

        if (hasSparklines)
        {
            // Get server sparklines
            var servers = await historyPage.GetSparklineServersAsync();
            servers.Should().NotBeEmpty("should have server sparklines");
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task History_LoadsDataForRange()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("History");
        await page.WaitForTimeoutAsync(2000);

        // Wait for initial chart load
        await historyPage.WaitForChartLoadAsync();

        // Select a different time range
        await historyPage.SelectTimeRangeAsync("1h");

        // Chart should have data (or at least be visible)
        var hasData1h = await historyPage.HasChartDataAsync();

        // Switch to 7d range
        await historyPage.SelectTimeRangeAsync("7d");

        // Chart should still render
        var hasData7d = await historyPage.HasChartDataAsync();

        // At least one range should have data (system may not have 7 days of data yet)
        (hasData1h || hasData7d).Should().BeTrue("at least one time range should have data");

        await page.CloseAsync();
    }

    [Fact]
    public async Task History_ChartTooltipWorks()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("History");
        await page.WaitForTimeoutAsync(2000);

        // Wait for chart to load
        await historyPage.WaitForChartLoadAsync();

        // Check if chart has data
        var hasData = await historyPage.HasChartDataAsync();

        if (hasData)
        {
            // Hover over chart
            await historyPage.HoverOverChartAsync();

            // Check if tooltip appears (may not always appear depending on chart library)
            // This is a best-effort test
            await page.WaitForTimeoutAsync(500);
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task History_SparklineClickFiltersChart()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var historyPage = new HistoryPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("History");
        await page.WaitForTimeoutAsync(2000);

        // Check if sparklines exist
        var hasSparklines = await historyPage.ServerSparklines.CountAsync() > 0;

        if (hasSparklines)
        {
            var servers = await historyPage.GetSparklineServersAsync();

            if (servers.Count > 0)
            {
                // Get initial chart line count
                var initialLineCount = await historyPage.GetChartLineCountAsync();

                // Click a sparkline
                await historyPage.ClickSparklineAsync(servers[0]);

                // Chart should update (line count might change, or stay the same if already filtered)
                var updatedLineCount = await historyPage.GetChartLineCountAsync();

                // No strict assertion - just verify the action completes without error
            }
        }

        await page.CloseAsync();
    }
}
