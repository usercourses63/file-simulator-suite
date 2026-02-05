using Microsoft.Playwright;

namespace FileSimulator.E2ETests.PageObjects;

/// <summary>
/// Page Object for the History tab.
/// Provides access to time range selector, latency chart, and server sparklines.
/// </summary>
public class HistoryPage
{
    private readonly IPage _page;

    public HistoryPage(IPage page)
    {
        _page = page;
    }

    // Main containers
    public ILocator HistoryTab => _page.Locator(".history-tab");
    public ILocator TimeRangeSelector => _page.Locator(".time-range-selector");
    public ILocator LatencyChart => _page.Locator(".latency-chart, .recharts-wrapper");
    public ILocator ServerSparklines => _page.Locator(".server-sparklines");

    // Time range buttons
    public ILocator OneHourButton => TimeRangeSelector.GetByRole(AriaRole.Button, new() { Name = "1h" });
    public ILocator SixHourButton => TimeRangeSelector.GetByRole(AriaRole.Button, new() { Name = "6h" });
    public ILocator TwentyFourHourButton => TimeRangeSelector.GetByRole(AriaRole.Button, new() { Name = "24h" });
    public ILocator SevenDayButton => TimeRangeSelector.GetByRole(AriaRole.Button, new() { Name = "7d" });

    // Chart elements
    public ILocator ChartLines => LatencyChart.Locator(".recharts-line");
    public ILocator ChartTooltip => _page.Locator(".recharts-tooltip-wrapper");
    public ILocator LoadingIndicator => HistoryTab.Locator(".loading, .spinner");

    // Sparklines
    public ILocator SparklineItems => ServerSparklines.Locator(".sparkline-item, .server-sparkline");

    /// <summary>
    /// Select a time range
    /// </summary>
    public async Task SelectTimeRangeAsync(string range)
    {
        var button = range.ToLower() switch
        {
            "1h" => OneHourButton,
            "6h" => SixHourButton,
            "24h" => TwentyFourHourButton,
            "7d" => SevenDayButton,
            _ => throw new ArgumentException($"Unknown time range: {range}", nameof(range))
        };

        await button.ClickAsync();

        // Wait for chart to update
        await WaitForChartLoadAsync();
    }

    /// <summary>
    /// Get the currently selected time range
    /// </summary>
    public async Task<string> GetCurrentTimeRangeAsync()
    {
        // Look for active button
        var buttons = new[] { OneHourButton, SixHourButton, TwentyFourHourButton, SevenDayButton };
        var ranges = new[] { "1h", "6h", "24h", "7d" };

        for (int i = 0; i < buttons.Length; i++)
        {
            var classList = await buttons[i].GetAttributeAsync("class");
            if (classList?.Contains("active") == true || classList?.Contains("selected") == true)
            {
                return ranges[i];
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Wait for chart to load data
    /// </summary>
    public async Task WaitForChartLoadAsync(int timeoutMs = 10000)
    {
        // Wait for loading indicator to disappear
        try
        {
            await LoadingIndicator.WaitForAsync(new()
            {
                State = WaitForSelectorState.Hidden,
                Timeout = timeoutMs
            });
        }
        catch
        {
            // Loading indicator might not appear for cached data
        }

        // Wait for chart to be visible
        await LatencyChart.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });

        // Wait for chart lines to render
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Get list of servers shown in sparklines
    /// </summary>
    public async Task<List<string>> GetSparklineServersAsync()
    {
        var servers = new List<string>();
        var items = await SparklineItems.AllAsync();

        foreach (var item in items)
        {
            var nameElement = item.Locator(".sparkline-name, .server-name");
            var name = await nameElement.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(name))
            {
                servers.Add(name.Trim());
            }
        }

        return servers;
    }

    /// <summary>
    /// Check if chart has data (has visible chart lines)
    /// </summary>
    public async Task<bool> HasChartDataAsync()
    {
        var lineCount = await ChartLines.CountAsync();
        return lineCount > 0;
    }

    /// <summary>
    /// Get number of chart lines (one per server)
    /// </summary>
    public async Task<int> GetChartLineCountAsync()
    {
        return await ChartLines.CountAsync();
    }

    /// <summary>
    /// Hover over chart to show tooltip
    /// </summary>
    public async Task HoverOverChartAsync()
    {
        // Hover over the middle of the chart
        var box = await LatencyChart.BoundingBoxAsync();
        if (box != null)
        {
            await _page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
            await _page.WaitForTimeoutAsync(500);
        }
    }

    /// <summary>
    /// Check if chart tooltip is visible
    /// </summary>
    public async Task<bool> IsTooltipVisibleAsync()
    {
        return await ChartTooltip.IsVisibleAsync();
    }

    /// <summary>
    /// Click on a sparkline to filter chart by server
    /// </summary>
    public async Task ClickSparklineAsync(string serverName)
    {
        var sparkline = SparklineItems.Filter(new() { HasText = serverName }).First;
        await sparkline.ClickAsync();

        // Wait for chart to update
        await _page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Check if time range selector is visible
    /// </summary>
    public async Task<bool> IsTimeRangeSelectorVisibleAsync()
    {
        return await TimeRangeSelector.IsVisibleAsync();
    }

    /// <summary>
    /// Check if loading state is active
    /// </summary>
    public async Task<bool> IsLoadingAsync()
    {
        return await LoadingIndicator.IsVisibleAsync();
    }

    /// <summary>
    /// Get all available time range options
    /// </summary>
    public async Task<List<string>> GetAvailableTimeRangesAsync()
    {
        var ranges = new List<string>();
        var buttons = await TimeRangeSelector.Locator("button").AllAsync();

        foreach (var button in buttons)
        {
            var text = await button.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                ranges.Add(text.Trim());
            }
        }

        return ranges;
    }

    /// <summary>
    /// Check if chart is displaying data for a specific time range
    /// </summary>
    public async Task<bool> IsShowingTimeRangeAsync(string range)
    {
        var currentRange = await GetCurrentTimeRangeAsync();
        return currentRange.Equals(range, StringComparison.OrdinalIgnoreCase);
    }
}
