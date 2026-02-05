using Microsoft.Playwright;

namespace FileSimulator.E2ETests.PageObjects;

/// <summary>
/// Page Object for the Alerts tab and alert banner.
/// Provides access to active alerts, alert history, and banner controls.
/// </summary>
public class AlertsPage
{
    private readonly IPage _page;

    public AlertsPage(IPage page)
    {
        _page = page;
    }

    // Alert banner (appears at top when alerts are active)
    public ILocator AlertBanner => _page.Locator(".alert-banner");
    public ILocator BannerMessage => AlertBanner.Locator(".alert-banner__message");
    public ILocator CriticalCountBadge => AlertBanner.Locator(".alert-banner__count--critical");
    public ILocator WarningCountBadge => AlertBanner.Locator(".alert-banner__count--warning");
    public ILocator InfoCountBadge => AlertBanner.Locator(".alert-banner__count--info");
    public ILocator DismissBannerButton => AlertBanner.Locator("button.dismiss-btn, button[aria-label='Dismiss']");

    // Alerts tab content
    public ILocator AlertsTab => _page.Locator(".alerts-tab");
    public ILocator ActiveAlertsList => AlertsTab.Locator(".active-alerts-list, .alerts-active");
    public ILocator AlertHistoryList => AlertsTab.Locator(".alert-history-list, .alerts-history");

    // Alert items
    public ILocator AlertItems => _page.Locator(".alert-item");
    public ILocator ActiveAlertItems => ActiveAlertsList.Locator(".alert-item");
    public ILocator HistoryAlertItems => AlertHistoryList.Locator(".alert-item");

    // Filters
    public ILocator SeverityFilter => AlertsTab.Locator("select[name='severity'], select.severity-filter");
    public ILocator TypeFilter => AlertsTab.Locator("select[name='type'], select.type-filter");
    public ILocator SearchInput => AlertsTab.Locator("input[type='search'], input[placeholder*='Search']");

    // Acknowledge button (for active alerts)
    public ILocator AcknowledgeButtons => _page.Locator("button").Filter(new() { HasText = "Acknowledge" });

    /// <summary>
    /// Get active alerts with severity, message, and time
    /// </summary>
    public async Task<List<AlertInfo>> GetActiveAlertsAsync()
    {
        var alerts = new List<AlertInfo>();
        var items = await ActiveAlertItems.AllAsync();

        foreach (var item in items)
        {
            var alert = await ParseAlertItemAsync(item);
            alerts.Add(alert);
        }

        return alerts;
    }

    /// <summary>
    /// Get alert history
    /// </summary>
    public async Task<List<AlertInfo>> GetAlertHistoryAsync()
    {
        var alerts = new List<AlertInfo>();
        var items = await HistoryAlertItems.AllAsync();

        foreach (var item in items)
        {
            var alert = await ParseAlertItemAsync(item);
            alerts.Add(alert);
        }

        return alerts;
    }

    /// <summary>
    /// Parse an alert item into AlertInfo
    /// </summary>
    private async Task<AlertInfo> ParseAlertItemAsync(ILocator item)
    {
        var severity = "Unknown";
        var classList = await item.GetAttributeAsync("class");
        if (classList?.Contains("--critical") == true) severity = "Critical";
        else if (classList?.Contains("--warning") == true) severity = "Warning";
        else if (classList?.Contains("--info") == true) severity = "Info";

        var titleElement = item.Locator(".alert-title, .alert__title");
        var title = await titleElement.TextContentAsync() ?? "";

        var messageElement = item.Locator(".alert-message, .alert__message");
        var message = await messageElement.TextContentAsync() ?? "";

        var timeElement = item.Locator(".alert-time, .alert__time");
        var time = await timeElement.TextContentAsync() ?? "";

        return new AlertInfo
        {
            Severity = severity,
            Title = title.Trim(),
            Message = message.Trim(),
            Time = time.Trim()
        };
    }

    /// <summary>
    /// Get alert banner text
    /// </summary>
    public async Task<string?> GetAlertBannerTextAsync()
    {
        if (!await AlertBanner.IsVisibleAsync())
        {
            return null;
        }

        return await BannerMessage.TextContentAsync();
    }

    /// <summary>
    /// Check if alert banner is visible
    /// </summary>
    public async Task<bool> IsAlertBannerVisibleAsync()
    {
        return await AlertBanner.IsVisibleAsync();
    }

    /// <summary>
    /// Get critical alert count from banner
    /// </summary>
    public async Task<int> GetCriticalCountFromBannerAsync()
    {
        if (!await CriticalCountBadge.IsVisibleAsync())
        {
            return 0;
        }

        var text = await CriticalCountBadge.TextContentAsync();
        var match = System.Text.RegularExpressions.Regex.Match(text ?? "", @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var count))
        {
            return count;
        }

        return 0;
    }

    /// <summary>
    /// Dismiss alert banner
    /// </summary>
    public async Task DismissAlertBannerAsync()
    {
        if (await DismissBannerButton.IsVisibleAsync())
        {
            await DismissBannerButton.ClickAsync();

            // Wait for banner to disappear
            await AlertBanner.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 2000 });
        }
    }

    /// <summary>
    /// Acknowledge an alert by index
    /// </summary>
    public async Task AcknowledgeAlertAsync(int index = 0)
    {
        var buttons = await AcknowledgeButtons.AllAsync();
        if (index < buttons.Count)
        {
            await buttons[index].ClickAsync();

            // Wait for operation to complete
            await _page.WaitForTimeoutAsync(500);
        }
    }

    /// <summary>
    /// Filter alerts by severity
    /// </summary>
    public async Task FilterBySeverityAsync(string severity)
    {
        await SeverityFilter.SelectOptionAsync(severity.ToLower());

        // Wait for filter to apply
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Filter alerts by type
    /// </summary>
    public async Task FilterByTypeAsync(string type)
    {
        await TypeFilter.SelectOptionAsync(type);

        // Wait for filter to apply
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Search alerts
    /// </summary>
    public async Task SearchAlertsAsync(string query)
    {
        await SearchInput.FillAsync(query);

        // Wait for search to apply
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Get total alert count
    /// </summary>
    public async Task<int> GetAlertCountAsync()
    {
        return await AlertItems.CountAsync();
    }

    /// <summary>
    /// Check if alerts tab is showing loading state
    /// </summary>
    public async Task<bool> IsLoadingAsync()
    {
        var loadingIndicator = AlertsTab.Locator(".loading, .spinner");
        return await loadingIndicator.IsVisibleAsync();
    }
}

/// <summary>
/// Alert information parsed from UI
/// </summary>
public class AlertInfo
{
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}
