using FileSimulator.E2ETests.Fixtures;
using FileSimulator.E2ETests.PageObjects;
using FluentAssertions;
using Xunit;

namespace FileSimulator.E2ETests.Tests;

[Collection("Simulator")]
public class KafkaTests
{
    private readonly SimulatorTestFixture _fixture;

    public KafkaTests(SimulatorTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Kafka_DisplaysTopicList()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var kafkaPage = new KafkaPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();

        // Switch to Kafka tab
        await dashboard.SwitchToTabAsync("Kafka");

        // Wait for Kafka UI to load
        await page.WaitForTimeoutAsync(2000);

        // Check if topic list is visible
        var isTopicListVisible = await kafkaPage.TopicList.IsVisibleAsync();
        isTopicListVisible.Should().BeTrue("topic list should be visible");

        await page.CloseAsync();
    }

    [Fact]
    public async Task Kafka_CanCreateTopic()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var kafkaPage = new KafkaPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Kafka");
        await page.WaitForTimeoutAsync(2000);

        var testTopicName = $"e2e-test-topic-{Guid.NewGuid():N}";

        try
        {
            // Create topic
            await kafkaPage.CreateTopicAsync(testTopicName, partitions: 1);

            // Wait for topic to appear
            await kafkaPage.WaitForTopicAsync(testTopicName, timeoutMs: 10000);

            // Verify topic appears in list
            var topics = await kafkaPage.GetTopicListAsync();
            topics.Should().Contain(testTopicName);
        }
        finally
        {
            // Cleanup: delete the test topic
            try
            {
                await kafkaPage.DeleteTopicAsync(testTopicName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Kafka_CanDeleteTopic()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var kafkaPage = new KafkaPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Kafka");
        await page.WaitForTimeoutAsync(2000);

        var testTopicName = $"e2e-delete-topic-{Guid.NewGuid():N}";

        // Create topic first
        await kafkaPage.CreateTopicAsync(testTopicName, partitions: 1);
        await kafkaPage.WaitForTopicAsync(testTopicName, timeoutMs: 10000);

        // Now delete it
        await kafkaPage.DeleteTopicAsync(testTopicName);

        // Wait for deletion
        await page.WaitForTimeoutAsync(2000);

        // Verify topic is removed
        var topics = await kafkaPage.GetTopicListAsync();
        topics.Should().NotContain(testTopicName);

        await page.CloseAsync();
    }

    [Fact]
    public async Task Kafka_CanProduceMessage()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var kafkaPage = new KafkaPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Kafka");
        await page.WaitForTimeoutAsync(2000);

        var testTopicName = $"e2e-produce-topic-{Guid.NewGuid():N}";

        try
        {
            // Create topic
            await kafkaPage.CreateTopicAsync(testTopicName, partitions: 1);
            await kafkaPage.WaitForTopicAsync(testTopicName, timeoutMs: 10000);

            // Produce a message
            await kafkaPage.ProduceMessageAsync(
                topic: testTopicName,
                key: "test-key",
                value: "E2E test message"
            );

            // Wait for message to be sent
            await page.WaitForTimeoutAsync(2000);

            // If no exception, message was produced successfully
        }
        finally
        {
            // Cleanup
            try
            {
                await kafkaPage.DeleteTopicAsync(testTopicName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Kafka_CanViewMessages()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var kafkaPage = new KafkaPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Kafka");
        await page.WaitForTimeoutAsync(2000);

        var testTopicName = $"e2e-view-topic-{Guid.NewGuid():N}";

        try
        {
            // Create topic
            await kafkaPage.CreateTopicAsync(testTopicName, partitions: 1);
            await kafkaPage.WaitForTopicAsync(testTopicName, timeoutMs: 10000);

            // Produce a message
            await kafkaPage.ProduceMessageAsync(
                topic: testTopicName,
                key: "view-test-key",
                value: "Message for viewing"
            );

            await page.WaitForTimeoutAsync(2000);

            // Get messages from viewer
            var messages = await kafkaPage.GetMessagesAsync(count: 10);

            // Should have at least one message (the one we just produced)
            messages.Should().NotBeEmpty("should have messages in viewer");
        }
        finally
        {
            // Cleanup
            try
            {
                await kafkaPage.DeleteTopicAsync(testTopicName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        await page.CloseAsync();
    }

    [Fact]
    public async Task Kafka_ShowsConsumerGroups()
    {
        var page = await _fixture.Context.NewPageAsync();
        var dashboard = new DashboardPage(page);
        var kafkaPage = new KafkaPage(page);

        await dashboard.NavigateAsync(_fixture.DashboardUrl);
        await dashboard.WaitForDashboardLoadAsync();
        await dashboard.SwitchToTabAsync("Kafka");
        await page.WaitForTimeoutAsync(2000);

        // Check if consumer groups panel is visible
        var isGroupsPanelVisible = await kafkaPage.ConsumerGroupsPanel.IsVisibleAsync();
        isGroupsPanelVisible.Should().BeTrue("consumer groups panel should be visible");

        // Get consumer groups (might be empty)
        var groups = await kafkaPage.GetConsumerGroupsAsync();
        // No assertion on count - system may or may not have consumer groups

        await page.CloseAsync();
    }
}
