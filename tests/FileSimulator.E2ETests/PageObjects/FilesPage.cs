using Microsoft.Playwright;

namespace FileSimulator.E2ETests.PageObjects;

/// <summary>
/// Page Object for the Files tab.
/// Provides access to file tree, upload functionality, and file operations.
/// </summary>
public class FilesPage
{
    private readonly IPage _page;

    public FilesPage(IPage page)
    {
        _page = page;
    }

    // Main locators
    public ILocator FileTree => _page.Locator(".file-tree");
    public ILocator FileUploader => _page.Locator(".file-uploader");
    public ILocator DropZone => _page.Locator("[class*='dropzone']");
    public ILocator BatchOperationsBar => _page.Locator(".batch-operations-bar");
    public ILocator FileEventFeed => _page.Locator(".file-event-feed");

    // Tree nodes (react-arborist)
    public ILocator TreeNodes => _page.Locator(".file-tree-node");
    public ILocator DirectoryNodes => _page.Locator(".file-tree-node");
    public ILocator FileNodes => _page.Locator(".file-tree-node");

    // File input for upload (react-dropzone places input inside dropzone)
    public ILocator FileInput => _page.Locator(".file-uploader input[type='file']");

    /// <summary>
    /// Get all file/folder names in a directory
    /// </summary>
    public async Task<List<string>> GetFilesInDirectoryAsync(string? path = null)
    {
        // If path provided, expand to that directory first
        if (path != null)
        {
            await ExpandDirectoryAsync(path);
        }

        // Get all visible file/folder names
        var nodes = await TreeNodes.AllAsync();
        var names = new List<string>();

        foreach (var node in nodes)
        {
            var nameElement = node.Locator(".file-tree-node__name");
            var name = await nameElement.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name.Trim());
            }
        }

        return names;
    }

    /// <summary>
    /// Expand a directory in the tree
    /// </summary>
    public async Task ExpandDirectoryAsync(string directoryName)
    {
        // In react-arborist, clicking the node toggles expand
        var directory = TreeNodes.Filter(new() { HasText = directoryName }).First;
        await directory.ClickAsync();

        // Wait for children to load
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Upload a file using file input
    /// </summary>
    public async Task UploadFileAsync(string localPath, string? targetDirectory = null)
    {
        // Select target directory if specified
        if (targetDirectory != null)
        {
            await SelectDirectoryAsync(targetDirectory);
        }

        // Set files on the file input element
        await FileInput.SetInputFilesAsync(localPath);

        // Wait for upload to complete
        await _page.WaitForTimeoutAsync(3000);

        // Refresh tree to show newly uploaded file
        await RefreshAsync();
    }

    /// <summary>
    /// Select a directory in the tree
    /// </summary>
    public async Task SelectDirectoryAsync(string directoryName)
    {
        var directory = TreeNodes.Filter(new() { HasText = directoryName }).First;
        await directory.ClickAsync();
    }

    /// <summary>
    /// Download a file (triggers browser download)
    /// </summary>
    public async Task DownloadFileAsync(string fileName)
    {
        var fileNode = TreeNodes.Filter(new() { HasText = fileName }).First;

        // Look for download button
        var downloadButton = fileNode.Locator("button[title='Download']");
        if (await downloadButton.CountAsync() > 0)
        {
            // Start waiting for download before clicking
            var downloadTask = _page.WaitForDownloadAsync();
            await downloadButton.ClickAsync();
            var download = await downloadTask;
            return;
        }
    }

    /// <summary>
    /// Delete a file or folder
    /// </summary>
    public async Task DeleteFileAsync(string fileName)
    {
        var fileNode = TreeNodes.Filter(new() { HasText = fileName }).First;

        // Look for delete button
        var deleteButton = fileNode.Locator("button[title='Delete']");
        if (await deleteButton.CountAsync() > 0)
        {
            // Set up dialog handler to accept the confirmation
            _page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            await deleteButton.ClickAsync();

            // Wait for deletion to complete and tree to refresh
            await _page.WaitForTimeoutAsync(2000);
        }
    }

    /// <summary>
    /// Select multiple files for batch operations
    /// </summary>
    public async Task SelectMultipleFilesAsync(List<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var fileNode = TreeNodes.Filter(new() { HasText = fileName }).First;

            // Look for checkbox
            var checkbox = fileNode.Locator("input[type='checkbox']");
            if (await checkbox.CountAsync() > 0)
            {
                await checkbox.CheckAsync();
            }
        }
    }

    /// <summary>
    /// Wait for a file to appear in the tree
    /// </summary>
    public async Task WaitForFileInListAsync(string fileName, int timeoutMs = 10000)
    {
        var fileNode = TreeNodes.Filter(new() { HasText = fileName });
        await fileNode.First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Get recent events from the file event feed
    /// </summary>
    public async Task<List<string>> GetRecentEventsAsync()
    {
        var events = new List<string>();
        var eventItems = await FileEventFeed.Locator(".file-event-feed__item").AllAsync();

        foreach (var item in eventItems)
        {
            var text = await item.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                events.Add(text.Trim());
            }
        }

        return events;
    }

    /// <summary>
    /// Check if batch operations bar is visible
    /// </summary>
    public async Task<bool> IsBatchOperationsBarVisibleAsync()
    {
        return await BatchOperationsBar.IsVisibleAsync();
    }

    /// <summary>
    /// Click batch delete button
    /// </summary>
    public async Task BatchDeleteAsync()
    {
        var deleteButton = BatchOperationsBar.GetByRole(AriaRole.Button, new() { Name = "Delete" });
        await deleteButton.ClickAsync();

        // Handle confirmation
        var confirmButton = _page.GetByRole(AriaRole.Button, new() { Name = "Delete" });
        if (await confirmButton.CountAsync() > 0)
        {
            await confirmButton.ClickAsync();
        }
    }

    /// <summary>
    /// Wait for file event to appear in feed
    /// </summary>
    public async Task WaitForEventAsync(string eventText, int timeoutMs = 5000)
    {
        var eventItem = FileEventFeed.Locator(".file-event-feed__item").Filter(new() { HasText = eventText });
        await eventItem.First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Refresh the file tree
    /// </summary>
    public async Task RefreshAsync()
    {
        var refreshButton = _page.Locator(".file-browser__refresh, button[title='Refresh']");
        if (await refreshButton.CountAsync() > 0)
        {
            await refreshButton.ClickAsync();
            await _page.WaitForTimeoutAsync(1000);
        }
    }
}
