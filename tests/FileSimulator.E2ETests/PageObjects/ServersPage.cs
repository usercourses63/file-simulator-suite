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
    public ILocator DeleteConfirmDialog => _page.Locator(".delete-confirm-dialog, .modal-overlay");
    public ILocator ServerDetailsPanel => _page.Locator(".details-panel");

    // Create Server Modal fields
    public ILocator NameInput => CreateServerModal.Locator("#server-name");
    public ILocator SubmitButton => CreateServerModal.Locator("button[type='submit']");
    public ILocator CancelButton => CreateServerModal.Locator("button.btn--secondary");

    /// <summary>
    /// Get all server names currently displayed in the grid
    /// </summary>
    public async Task<List<string>> GetAllServerNamesAsync()
    {
        var cards = await ServerCards.AllAsync();
        var names = new List<string>();

        foreach (var card in cards)
        {
            var nameElement = card.Locator(".server-name");
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
        var healthDot = card.Locator(".status-dot");
        var classList = await healthDot.GetAttributeAsync("class");

        if (classList?.Contains("status-dot--healthy") == true)
            return "healthy";
        if (classList?.Contains("status-dot--degraded") == true)
            return "degraded";
        if (classList?.Contains("status-dot--down") == true)
            return "unhealthy";

        return "unknown";
    }

    /// <summary>
    /// Get the status text of a specific server (e.g., "Running", "Stopped")
    /// </summary>
    public async Task<string?> GetServerStatusAsync(string serverName)
    {
        var card = GetServerCard(serverName);
        var statusElement = card.Locator(".status-text");
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
        // Select protocol using protocol buttons
        var protocolUpper = protocol.ToUpper();
        if (protocolUpper == "NAS") protocolUpper = "NAS";
        var protocolButton = CreateServerModal.GetByRole(AriaRole.Button, new() { Name = protocolUpper, Exact = true });
        var isActive = await protocolButton.GetAttributeAsync("class");
        if (isActive?.Contains("protocol-btn--active") != true)
        {
            await protocolButton.ClickAsync();
        }

        // Fill name
        await NameInput.FillAsync(name);

        // Fill credentials if provided and visible
        if (username != null)
        {
            var usernameInput = CreateServerModal.Locator($"#{protocol.ToLower()}-username");
            if (await usernameInput.CountAsync() > 0)
            {
                await usernameInput.FillAsync(username);
            }
        }

        if (password != null)
        {
            var passwordInput = CreateServerModal.Locator($"#{protocol.ToLower()}-password");
            if (await passwordInput.CountAsync() > 0)
            {
                await passwordInput.FillAsync(password);
            }
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
        await _page.Locator(".details-panel--open").WaitForAsync(new() { State = WaitForSelectorState.Visible });
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
        var deleteButton = card.Locator(".server-card-delete, button[title='Delete server']");
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

        // Extract key details from panel
        var nameElement = ServerDetailsPanel.Locator(".panel-title");
        var name = await nameElement.TextContentAsync();
        if (name != null) details["name"] = name.Trim();

        var protocolElement = ServerDetailsPanel.Locator(".panel-protocol");
        if (await protocolElement.CountAsync() > 0)
        {
            var protocol = await protocolElement.First.TextContentAsync();
            if (protocol != null) details["protocol"] = protocol.Trim();
        }

        return details;
    }

    /// <summary>
    /// Close the server details panel
    /// </summary>
    public async Task CloseDetailsPanelAsync()
    {
        var closeButton = ServerDetailsPanel.Locator(".panel-close");
        await closeButton.ClickAsync();

        // Wait for panel to be hidden
        await ServerDetailsPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Check if create server modal shows a validation error
    /// </summary>
    public async Task<bool> HasValidationErrorAsync()
    {
        var errorElement = CreateServerModal.Locator(".modal-error, .name-unavailable, .form-input--error");
        return await errorElement.CountAsync() > 0;
    }

    /// <summary>
    /// Get validation error text from create server modal
    /// </summary>
    public async Task<string?> GetValidationErrorAsync()
    {
        var errorElement = CreateServerModal.Locator(".modal-error, .name-unavailable").First;
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
