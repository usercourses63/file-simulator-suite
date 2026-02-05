using Microsoft.Playwright;

namespace FileSimulator.E2ETests.PageObjects;

/// <summary>
/// Page Object for the Servers tab.
/// Provides access to server grid, create server dialog, and server operations.
/// </summary>
public class ServersPage
{
    private readonly IPage _page;

    public ServersPage(IPage page)
    {
        _page = page;
    }

    // Locators
    public ILocator ServerGridContainer => _page.Locator(".server-grid-container");
    public ILocator NasServersSection => _page.Locator(".server-section").Filter(new() { HasText = "NAS Servers" });
    public ILocator ProtocolServersSection => _page.Locator(".server-section").Filter(new() { HasText = "Protocol Servers" });

    public ILocator ServerCards => _page.Locator(".server-card");
    public ILocator CreateServerButton => _page.GetByRole(AriaRole.Button, new() { Name = "Add Server" });
    public ILocator CreateServerModal => _page.Locator(".create-server-modal");
    public ILocator DeleteConfirmDialog => _page.Locator(".delete-confirm-dialog");
    public ILocator ServerDetailsPanel => _page.Locator(".server-details-panel");

    // Create Server Modal fields
    public ILocator ProtocolSelect => CreateServerModal.Locator("select[name='protocol']");
    public ILocator NameInput => CreateServerModal.GetByLabel("Server Name", new() { Exact = false });
    public ILocator UsernameInput => CreateServerModal.GetByLabel("Username", new() { Exact = false });
    public ILocator PasswordInput => CreateServerModal.GetByLabel("Password", new() { Exact = false });
    public ILocator SubmitButton => CreateServerModal.GetByRole(AriaRole.Button, new() { Name = "Create Server" });
    public ILocator CancelButton => CreateServerModal.GetByRole(AriaRole.Button, new() { Name = "Cancel" });

    /// <summary>
    /// Get all server names currently displayed in the grid
    /// </summary>
    public async Task<List<string>> GetAllServerNamesAsync()
    {
        var cards = await ServerCards.AllAsync();
        var names = new List<string>();

        foreach (var card in cards)
        {
            var nameElement = card.Locator(".server-card__name");
            var name = await nameElement.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name.Trim());
            }
        }

        return names;
    }

    /// <summary>
    /// Get a server card by name
    /// </summary>
    public ILocator GetServerCard(string serverName)
    {
        return _page.Locator(".server-card").Filter(new() { HasText = serverName });
    }

    /// <summary>
    /// Get the health status of a specific server
    /// </summary>
    public async Task<string> GetServerHealthAsync(string serverName)
    {
        var card = GetServerCard(serverName);
        var healthDot = card.Locator(".health-dot");
        var classList = await healthDot.GetAttributeAsync("class");

        if (classList?.Contains("health-dot--healthy") == true)
            return "healthy";
        if (classList?.Contains("health-dot--degraded") == true)
            return "degraded";
        if (classList?.Contains("health-dot--unhealthy") == true)
            return "unhealthy";

        return "unknown";
    }

    /// <summary>
    /// Get the status text of a specific server (e.g., "Running", "Stopped")
    /// </summary>
    public async Task<string?> GetServerStatusAsync(string serverName)
    {
        var card = GetServerCard(serverName);
        var statusElement = card.Locator(".server-card__status, .health-text");
        return await statusElement.TextContentAsync();
    }

    /// <summary>
    /// Open the create server dialog
    /// </summary>
    public async Task OpenCreateServerDialogAsync()
    {
        await CreateServerButton.ClickAsync();
        await CreateServerModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Fill server details in the create server dialog
    /// </summary>
    public async Task FillServerDetailsAsync(string name, string protocol, string? username = null, string? password = null)
    {
        // Select protocol first (this may show/hide fields)
        if (protocol.ToLower() != "ftp") // ftp is default
        {
            await ProtocolSelect.SelectOptionAsync(protocol.ToLower());
        }

        // Fill name
        await NameInput.FillAsync(name);

        // Fill credentials if provided and visible
        if (username != null)
        {
            await UsernameInput.FillAsync(username);
        }

        if (password != null)
        {
            await PasswordInput.FillAsync(password);
        }
    }

    /// <summary>
    /// Submit the server creation form
    /// </summary>
    public async Task SubmitServerCreationAsync()
    {
        await SubmitButton.ClickAsync();
    }

    /// <summary>
    /// Wait for a server to appear in the list (with timeout)
    /// </summary>
    public async Task WaitForServerInListAsync(string serverName, int timeoutMs = 60000)
    {
        await GetServerCard(serverName).WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Select a server to show details panel
    /// </summary>
    public async Task SelectServerAsync(string serverName)
    {
        var card = GetServerCard(serverName);
        await card.ClickAsync();

        // Wait for details panel to open
        await ServerDetailsPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Delete a server using the delete button
    /// </summary>
    public async Task DeleteServerAsync(string serverName)
    {
        var card = GetServerCard(serverName);

        // Hover to show delete button (for dynamic servers)
        await card.HoverAsync();

        // Click delete button
        var deleteButton = card.Locator(".server-card__delete-btn, button[title='Delete server']");
        await deleteButton.ClickAsync();

        // Wait for confirmation dialog
        await DeleteConfirmDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Confirm deletion (without deleting data)
        var confirmButton = DeleteConfirmDialog.GetByRole(AriaRole.Button, new() { Name = "Delete" });
        await confirmButton.ClickAsync();
    }

    /// <summary>
    /// Get server details from the details panel
    /// </summary>
    public async Task<Dictionary<string, string>> GetServerDetailsAsync()
    {
        var details = new Dictionary<string, string>();

        // Wait for panel to be visible
        await ServerDetailsPanel.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Extract key details (adjust selectors based on actual panel structure)
        var nameElement = ServerDetailsPanel.Locator(".server-details__name, h2");
        var name = await nameElement.TextContentAsync();
        if (name != null) details["name"] = name.Trim();

        var protocolElement = ServerDetailsPanel.Locator(".server-details__protocol");
        var protocol = await protocolElement.First.TextContentAsync();
        if (protocol != null) details["protocol"] = protocol.Trim();

        return details;
    }

    /// <summary>
    /// Close the server details panel
    /// </summary>
    public async Task CloseDetailsPanelAsync()
    {
        var closeButton = ServerDetailsPanel.Locator("button.close-btn, button[aria-label='Close']");
        await closeButton.ClickAsync();

        // Wait for panel to be hidden
        await ServerDetailsPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Check if create server modal shows a validation error
    /// </summary>
    public async Task<bool> HasValidationErrorAsync()
    {
        var errorElement = CreateServerModal.Locator(".error-message, .validation-error, .field-error");
        return await errorElement.CountAsync() > 0;
    }

    /// <summary>
    /// Get validation error text from create server modal
    /// </summary>
    public async Task<string?> GetValidationErrorAsync()
    {
        var errorElement = CreateServerModal.Locator(".error-message, .validation-error, .field-error").First;
        return await errorElement.TextContentAsync();
    }

    /// <summary>
    /// Count servers in NAS section
    /// </summary>
    public async Task<int> GetNasServerCountAsync()
    {
        var nasCards = await NasServersSection.Locator(".server-card").CountAsync();
        return nasCards;
    }

    /// <summary>
    /// Count servers in Protocol section
    /// </summary>
    public async Task<int> GetProtocolServerCountAsync()
    {
        var protocolCards = await ProtocolServersSection.Locator(".server-card").CountAsync();
        return protocolCards;
    }
}
