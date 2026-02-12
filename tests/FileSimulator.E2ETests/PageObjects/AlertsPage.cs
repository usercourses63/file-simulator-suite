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
    public ILocator DismissBannerButton => AlertBanner.Locator("button");

    // Alerts tab content
    public ILocator AlertsTab => _page.Locator(".alerts-tab");

    // Stats section
    public ILocator StatsSection => AlertsTab.Locator(".alerts-tab__stats");
    public ILocator StatCards => StatsSection.Locator(".stat-card");

    // Filters
    public ILocator SeverityFilter => _page.Locator("#severity-filter");
    public ILocator TypeFilter => _page.Locator("#type-filter");
    public ILocator SearchInput => _page.Locator("#search-filter");

    // Alerts table
    public ILocator AlertsTable => AlertsTab.Locator(".alerts-tab__table");
    public ILocator AlertTableRows => AlertsTable.Locator("tbody tr");

    // Alert items (table rows) - used for counting
    public ILocator AlertItems => AlertTableRows;

    // Active and history sections - both use the same table; filter by status column
    public ILocator ActiveAlertsList => AlertsTab;
    public ILocator AlertHistoryList => AlertsTab;
    public ILocator ActiveAlertItems => AlertsTable.Locator("tbody tr").Filter(new() { Has = _page.Locator(".alert-status--active") });
    public ILocator HistoryAlertItems => AlertsTable.Locator("tbody tr").Filter(new() { Has = _page.Locator(".alert-status--resolved") });

    // Loading state
    public ILocator LoadingIndicator => AlertsTab.Locator(".alerts-tab__loading, .loading-spinner");

    /// <summary>
    /// Get active alerts with severity, message, and time
    /// </summary>
    public async Task<List<AlertInfo>> GetActiveAlertsAsync()
    {
        var alerts = new List<AlertInfo>();
        var rows = await AlertTableRows.AllAsync();

        foreach (var row in rows)
        {
            // Check if this row has an active status
            var statusElement = row.Locator(".alert-status--active");
            if (await statusElement.CountAsync() > 0)
            {
                var alert = await ParseAlertRowAsync(row);
                alerts.Add(alert);
            }
        }

        return alerts;
    }

    /// <summary>
    /// Get alert history
    /// </summary>
    public async Task<List<AlertInfo>> GetAlertHistoryAsync()
    {
        var alerts = new List<AlertInfo>();
        var rows = await AlertTableRows.AllAsync();

        foreach (var row in rows)
        {
            var alert = await ParseAlertRowAsync(row);
            alerts.Add(alert);
        }

        return alerts;
    }

    /// <summary>
    /// Parse a table row into AlertInfo
    /// </summary>
    private async Task<AlertInfo> ParseAlertRowAsync(ILocator row)
    {
        var severity = "Unknown";

        // Check for alert badge severity class
        var badge = row.Locator(".alert-badge");
        if (await badge.CountAsync() > 0)
        {
            var badgeClass = await badge.First.GetAttributeAsync("class");
            if (badgeClass?.Contains("alert-badge--critical") == true) severity = "Critical";
            else if (badgeClass?.Contains("alert-badge--warning") == true) severity = "Warning";
            else if (badgeClass?.Contains("alert-badge--info") == true) severity = "Info";
        }

        // Get title from the title cell
        var titleElement = row.Locator(".alerts-tab__title");
        var title = "";
        if (await titleElement.CountAsync() > 0)
            title = await titleElement.TextContentAsync() ?? "";

        // Get message from the message cell
        var messageElement = row.Locator(".alerts-tab__message");
        var message = "";
        if (await messageElement.CountAsync() > 0)
            message = await messageElement.TextContentAsync() ?? "";

        // Get time from the row (look for time/date cell)
        var cells = await row.Locator("td").AllAsync();
        var time = cells.Count > 5 ? (await cells[5].TextContentAsync() ?? "") : "";

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
    /// Get total alert count from table rows
    /// </summary>
    public async Task<int> GetAlertCountAsync()
    {
        return await AlertTableRows.CountAsync();
    }

    /// <summary>
    /// Check if alerts tab is showing loading state
    /// </summary>
    public async Task<bool> IsLoadingAsync()
    {
        return await LoadingIndicator.IsVisibleAsync();
    }

    /// <summary>
    /// Get stat card value by severity
    /// </summary>
    public async Task<int> GetStatCardValueAsync(string severity)
    {
        var card = StatsSection.Locator($".stat-card--{severity.ToLower()}");
        if (await card.CountAsync() > 0)
        {
            var valueElement = card.Locator(".stat-card__value");
            var text = await valueElement.TextContentAsync();
            if (int.TryParse(text?.Trim(), out var value))
                return value;
        }
        return 0;
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
