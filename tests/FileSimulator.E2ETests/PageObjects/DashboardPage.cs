using Microsoft.Playwright;

namespace FileSimulator.E2ETests.PageObjects;

/// <summary>
/// Page Object for the main Dashboard page.
/// Provides navigation between tabs and access to common header elements.
/// </summary>
public class DashboardPage
{
    private readonly IPage _page;

    public DashboardPage(IPage page)
    {
        _page = page;
    }

    // Locators using role-based selectors
    public ILocator HeaderTitle => _page.GetByRole(AriaRole.Heading, new() { Name = "File Simulator Suite" });
    public ILocator ConnectionStatus => _page.Locator(".connection-status");

    // Tab buttons
    public ILocator ServersTab => _page.GetByRole(AriaRole.Button, new() { Name = "Servers" });
    public ILocator FilesTab => _page.GetByRole(AriaRole.Button, new() { Name = "Files" });
    public ILocator HistoryTab => _page.GetByRole(AriaRole.Button, new() { Name = "History" });
    public ILocator KafkaTab => _page.GetByRole(AriaRole.Button, new() { Name = "Kafka" });
    public ILocator AlertsTab => _page.GetByRole(AriaRole.Button, new() { Name = "Alerts" });

    // Summary header
    public ILocator SummaryHeader => _page.Locator(".summary-header");

    // Add Server button
    public ILocator AddServerButton => _page.GetByRole(AriaRole.Button, new() { Name = "Add Server" });

    /// <summary>
    /// Navigate to the dashboard and wait for initial load
    /// </summary>
    public async Task NavigateAsync(string baseUrl)
    {
        await _page.GotoAsync(baseUrl);
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Wait for dashboard to complete SignalR connection and receive initial data
    /// </summary>
    public async Task WaitForDashboardLoadAsync(int timeoutMs = 10000)
    {
        // Wait for connection status to show connected
        await _page.WaitForSelectorAsync(".connection-status--connected, .connection-status--reconnecting",
            new() { Timeout = timeoutMs });

        // Wait for summary header to appear (indicates data loaded)
        await SummaryHeader.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
    }

    /// <summary>
    /// Switch to a specific tab and wait for content to load
    /// </summary>
    public async Task SwitchToTabAsync(string tabName)
    {
        var tab = tabName.ToLower() switch
        {
            "servers" => ServersTab,
            "files" => FilesTab,
            "history" => HistoryTab,
            "kafka" => KafkaTab,
            "alerts" => AlertsTab,
            _ => throw new ArgumentException($"Unknown tab: {tabName}", nameof(tabName))
        };

        await tab.ClickAsync();

        // Wait for tab to be active
        await _page.WaitForSelectorAsync($".header-tab--active >> text={tabName}",
            new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Get the total server count from the summary header
    /// </summary>
    public async Task<int> GetServerCountAsync()
    {
        var summaryText = await SummaryHeader.TextContentAsync();

        // Parse text like "13 servers running" or "13 servers (12 healthy, 1 unhealthy)"
        var match = System.Text.RegularExpressions.Regex.Match(summaryText ?? "", @"(\d+)\s+server");

        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
        {
            return count;
        }

        throw new InvalidOperationException($"Could not parse server count from: {summaryText}");
    }

    /// <summary>
    /// Get the current connection status (connected, reconnecting, disconnected)
    /// </summary>
    public async Task<string> GetConnectionStatusAsync()
    {
        var classList = await ConnectionStatus.GetAttributeAsync("class");

        if (classList?.Contains("connection-status--connected") == true)
            return "connected";

        if (classList?.Contains("connection-status--reconnecting") == true)
            return "reconnecting";

        return "disconnected";
    }

    /// <summary>
    /// Check if a specific tab is currently active
    /// </summary>
    public async Task<bool> IsTabActiveAsync(string tabName)
    {
        var tab = tabName.ToLower() switch
        {
            "servers" => ServersTab,
            "files" => FilesTab,
            "history" => HistoryTab,
            "kafka" => KafkaTab,
            "alerts" => AlertsTab,
            _ => throw new ArgumentException($"Unknown tab: {tabName}", nameof(tabName))
        };

        var classList = await tab.GetAttributeAsync("class");
        return classList?.Contains("header-tab--active") == true;
    }

    /// <summary>
    /// Get all visible tab names
    /// </summary>
    public async Task<List<string>> GetVisibleTabsAsync()
    {
        var tabs = new List<string>();
        var tabButtons = await _page.Locator(".header-tab").AllAsync();

        foreach (var button in tabButtons)
        {
            var text = await button.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                tabs.Add(text.Trim());
            }
        }

        return tabs;
    }
}
