using Microsoft.Playwright;

namespace FileSimulator.E2ETests.PageObjects;

/// <summary>
/// Page Object for the Kafka tab.
/// Provides access to topic management, message producer, message viewer, and consumer groups.
/// </summary>
public class KafkaPage
{
    private readonly IPage _page;

    public KafkaPage(IPage page)
    {
        _page = page;
    }

    // Main layout panels
    public ILocator KafkaTab => _page.Locator(".kafka-tab");
    public ILocator TopicsPanel => _page.Locator(".kafka-panel--topics");
    public ILocator MessagesPanel => _page.Locator(".kafka-panel--messages");
    public ILocator ConsumerGroupsPanel => _page.Locator(".kafka-panel--groups");

    // Health indicator
    public ILocator HealthIndicator => _page.Locator(".kafka-health");

    // Topic list
    public ILocator TopicList => _page.Locator(".topic-list");
    public ILocator TopicItems => TopicList.Locator(".topic-list__item");
    public ILocator CreateTopicButton => _page.Locator(".topic-list__create-btn");

    // Create topic form (overlay modal)
    public ILocator CreateTopicForm => _page.Locator(".create-topic-form");
    public ILocator TopicNameInput => _page.Locator("#topic-name");
    public ILocator PartitionsInput => _page.Locator("#topic-partitions");
    public ILocator SubmitTopicButton => _page.Locator(".create-topic-form__submit");
    public ILocator CancelTopicButton => _page.Locator(".create-topic-form__cancel");

    // Message producer
    public ILocator MessageProducer => _page.Locator(".message-producer");
    public ILocator MessageKeyInput => _page.Locator("#msg-key");
    public ILocator MessageValueInput => _page.Locator("#msg-value");
    public ILocator SendMessageButton => _page.Locator(".message-producer__submit");

    // View mode toggle
    public ILocator ProduceViewButton => _page.Locator(".kafka-view-toggle__btn").Filter(new() { HasText = "Produce" });
    public ILocator ConsumeViewButton => _page.Locator(".kafka-view-toggle__btn").Filter(new() { HasText = "Consume" });

    // Message viewer
    public ILocator MessageViewer => _page.Locator(".message-viewer");
    public ILocator MessageItems => MessageViewer.Locator(".message-viewer__item");

    // Consumer groups
    public ILocator ConsumerGroupList => _page.Locator(".kafka-groups-list");
    public ILocator ConsumerGroupItems => ConsumerGroupList.Locator(".consumer-group-detail");

    /// <summary>
    /// Get list of all topic names
    /// </summary>
    public async Task<List<string>> GetTopicListAsync()
    {
        var topics = new List<string>();
        var items = await TopicItems.AllAsync();

        foreach (var item in items)
        {
            var nameElement = item.Locator(".topic-list__item-name");
            var name = await nameElement.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(name))
            {
                topics.Add(name.Trim());
            }
        }

        return topics;
    }

    /// <summary>
    /// Create a new topic
    /// </summary>
    public async Task CreateTopicAsync(string name, int partitions = 1)
    {
        // Click create button
        await CreateTopicButton.ClickAsync();

        // Wait for form to appear
        await CreateTopicForm.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Fill form
        await TopicNameInput.FillAsync(name);
        await PartitionsInput.FillAsync(partitions.ToString());

        // Submit
        await SubmitTopicButton.ClickAsync();

        // Wait for form to close
        await CreateTopicForm.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
    }

    /// <summary>
    /// Delete a topic
    /// </summary>
    public async Task DeleteTopicAsync(string topicName)
    {
        var topicItem = TopicItems.Filter(new() { HasText = topicName }).First;

        // Click delete button (the "x" button)
        var deleteButton = topicItem.Locator(".topic-list__delete-btn");
        await deleteButton.ClickAsync();

        // Handle inline confirmation (Yes/No buttons appear)
        var confirmYes = topicItem.Locator(".topic-list__confirm-yes");
        await confirmYes.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await confirmYes.ClickAsync();

        // Wait for topic to be removed
        await _page.WaitForTimeoutAsync(2000);
    }

    /// <summary>
    /// Select a topic from the list
    /// </summary>
    public async Task SelectTopicAsync(string topicName)
    {
        var topicItem = TopicItems.Filter(new() { HasText = topicName }).First;
        var selectButton = topicItem.Locator(".topic-list__item-btn");
        await selectButton.ClickAsync();

        // Wait for messages panel to update
        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Produce a message to the selected topic
    /// </summary>
    public async Task ProduceMessageAsync(string topic, string? key = null, string? value = null)
    {
        // Select topic first
        await SelectTopicAsync(topic);

        // Switch to produce view if not already
        var produceClass = await ProduceViewButton.GetAttributeAsync("class");
        if (produceClass?.Contains("--active") != true)
        {
            await ProduceViewButton.ClickAsync();
        }

        // Fill message fields
        if (key != null)
        {
            await MessageKeyInput.FillAsync(key);
        }

        if (value != null)
        {
            await MessageValueInput.FillAsync(value);
        }

        // Send message
        await SendMessageButton.ClickAsync();

        // Wait for send to complete
        await _page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Get messages from the message viewer
    /// </summary>
    public async Task<List<string>> GetMessagesAsync(int count = 10)
    {
        // Switch to consume view
        var consumeClass = await ConsumeViewButton.GetAttributeAsync("class");
        if (consumeClass?.Contains("--active") != true)
        {
            await ConsumeViewButton.ClickAsync();
        }

        // Wait for messages to load (Kafka consumption can take a few seconds)
        await _page.WaitForTimeoutAsync(5000);

        var messages = new List<string>();
        var items = await MessageItems.AllAsync();

        var itemsToTake = Math.Min(count, items.Count);
        for (int i = 0; i < itemsToTake; i++)
        {
            var text = await items[i].TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
            {
                messages.Add(text.Trim());
            }
        }

        return messages;
    }

    /// <summary>
    /// Get list of consumer groups
    /// </summary>
    public async Task<List<string>> GetConsumerGroupsAsync()
    {
        var groups = new List<string>();
        var items = await ConsumerGroupItems.AllAsync();

        foreach (var item in items)
        {
            var nameElement = item.Locator(".consumer-group-detail__id");
            var name = await nameElement.TextContentAsync();
            if (!string.IsNullOrWhiteSpace(name))
            {
                groups.Add(name.Trim());
            }
        }

        return groups;
    }

    /// <summary>
    /// Check if Kafka is healthy
    /// </summary>
    public async Task<bool> IsKafkaHealthyAsync()
    {
        var classList = await HealthIndicator.Locator(".kafka-health__indicator").GetAttributeAsync("class");
        return classList?.Contains("--healthy") == true;
    }

    /// <summary>
    /// Wait for topic to appear in list
    /// </summary>
    public async Task WaitForTopicAsync(string topicName, int timeoutMs = 10000)
    {
        var topicItem = TopicItems.Filter(new() { HasText = topicName });
        await topicItem.First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Visible,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Wait for topic to be removed from list
    /// </summary>
    public async Task WaitForTopicRemovedAsync(string topicName, int timeoutMs = 10000)
    {
        var topicItem = TopicItems.Filter(new() { HasText = topicName });
        await topicItem.First.WaitForAsync(new()
        {
            State = WaitForSelectorState.Hidden,
            Timeout = timeoutMs
        });
    }

    /// <summary>
    /// Get message count from viewer
    /// </summary>
    public async Task<int> GetMessageCountAsync()
    {
        return await MessageItems.CountAsync();
    }
}
