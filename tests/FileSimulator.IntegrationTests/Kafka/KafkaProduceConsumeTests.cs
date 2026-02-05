using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FileSimulator.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Kafka;

/// <summary>
/// Tests Kafka message produce and consume operations via both API and direct client.
/// </summary>
[Collection("Simulator")]
public class KafkaProduceConsumeTests
{
    private readonly SimulatorCollectionFixture _fixture;
    private readonly string _bootstrapServers;

    public KafkaProduceConsumeTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
        _bootstrapServers = fixture.Configuration["Kafka:BootstrapServers"]
            ?? "file-simulator.local:30093";
    }

    /// <summary>
    /// Creates a test topic and returns its name.
    /// </summary>
    private async Task<string> CreateTestTopicAsync()
    {
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        var response = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/topics", new
        {
            name = topicName,
            partitions = 1,
            replicationFactor = 1
        });
        response.EnsureSuccessStatusCode();

        // Wait for topic to be ready
        await Task.Delay(1000);

        return topicName;
    }

    /// <summary>
    /// Deletes a topic.
    /// </summary>
    private async Task DeleteTopicAsync(string topicName)
    {
        try
        {
            await _fixture.ApiClient.DeleteAsync($"/api/kafka/topics/{topicName}");
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task Kafka_Produce_ViaApi()
    {
        // Arrange
        var topicName = await CreateTestTopicAsync();

        try
        {
            // Act
            var response = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/produce", new
            {
                topic = topicName,
                key = "test-key",
                value = "test-value"
            });

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Message produce should succeed. Status: {response.StatusCode}");
        }
        finally
        {
            // Cleanup
            await DeleteTopicAsync(topicName);
        }
    }

    [Fact]
    public async Task Kafka_Consume_ViaApi()
    {
        // Arrange
        var topicName = await CreateTestTopicAsync();
        var testKey = "api-consume-key";
        var testValue = "api-consume-value";

        try
        {
            // Act - Produce message
            var produceResponse = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/produce", new
            {
                topic = topicName,
                key = testKey,
                value = testValue
            });
            produceResponse.EnsureSuccessStatusCode();

            // Wait for message to be available
            await Task.Delay(500);

            // Act - Consume message
            var consumeResponse = await _fixture.ApiClient.GetAsync(
                $"/api/kafka/consume/{topicName}?count=1&timeout=10");
            consumeResponse.EnsureSuccessStatusCode();

            var content = await consumeResponse.Content.ReadAsStringAsync();
            var messages = JsonSerializer.Deserialize<List<JsonElement>>(content);

            // Assert
            messages.Should().NotBeNull("Messages should be returned");
            messages.Should().HaveCountGreaterThanOrEqualTo(1, "At least one message should be consumed");

            var message = messages![0];
            message.TryGetProperty("key", out var keyElement).Should().BeTrue("Message should have key");
            message.TryGetProperty("value", out var valueElement).Should().BeTrue("Message should have value");

            keyElement.GetString().Should().Be(testKey, "Key should match produced message");
            valueElement.GetString().Should().Be(testValue, "Value should match produced message");
        }
        finally
        {
            // Cleanup
            await DeleteTopicAsync(topicName);
        }
    }

    [Fact]
    public async Task Kafka_ProduceConsume_DirectClient()
    {
        // Arrange
        var topicName = await CreateTestTopicAsync();
        var testKey = "direct-key";
        var testValue = "direct-value";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            MessageTimeoutMs = 10000
        };

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"test-group-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        try
        {
            // Act - Produce
            using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
            var deliveryResult = await producer.ProduceAsync(topicName, new Message<string, string>
            {
                Key = testKey,
                Value = testValue
            });

            deliveryResult.Status.Should().Be(PersistenceStatus.Persisted,
                "Message should be persisted to Kafka");

            // Act - Consume
            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer.Subscribe(topicName);

            var consumed = false;
            var matchedKey = false;
            var matchedValue = false;

            var timeout = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < timeout && !consumed)
            {
                var consumeResult = consumer.Consume(TimeSpan.FromSeconds(2));
                if (consumeResult != null)
                {
                    consumed = true;
                    matchedKey = consumeResult.Message.Key == testKey;
                    matchedValue = consumeResult.Message.Value == testValue;
                    break;
                }
            }

            // Assert
            consumed.Should().BeTrue("Message should be consumed within 10 seconds");
            matchedKey.Should().BeTrue("Consumed key should match produced key");
            matchedValue.Should().BeTrue("Consumed value should match produced value");

            consumer.Close();
        }
        finally
        {
            // Cleanup
            await DeleteTopicAsync(topicName);
        }
    }

    [Fact]
    public async Task Kafka_ProduceMultiple_ConsumeAll()
    {
        // Arrange
        var topicName = await CreateTestTopicAsync();
        var messageCount = 5;
        var messages = Enumerable.Range(1, messageCount)
            .Select(i => (Key: $"key-{i}", Value: $"value-{i}"))
            .ToList();

        try
        {
            // Act - Produce all messages
            foreach (var (key, value) in messages)
            {
                var response = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/produce", new
                {
                    topic = topicName,
                    key = key,
                    value = value
                });
                response.EnsureSuccessStatusCode();
            }

            // Wait for messages to be available
            await Task.Delay(1000);

            // Act - Consume all messages
            var consumeResponse = await _fixture.ApiClient.GetAsync(
                $"/api/kafka/consume/{topicName}?count={messageCount}&timeout=10");
            consumeResponse.EnsureSuccessStatusCode();

            var content = await consumeResponse.Content.ReadAsStringAsync();
            var consumedMessages = JsonSerializer.Deserialize<List<JsonElement>>(content);

            // Assert
            consumedMessages.Should().NotBeNull("Messages should be returned");
            consumedMessages.Should().HaveCount(messageCount, $"All {messageCount} messages should be consumed");

            // Verify all messages were consumed in order
            for (int i = 0; i < messageCount; i++)
            {
                var message = consumedMessages![i];
                message.TryGetProperty("key", out var keyElement).Should().BeTrue($"Message {i} should have key");
                message.TryGetProperty("value", out var valueElement).Should().BeTrue($"Message {i} should have value");

                keyElement.GetString().Should().Be($"key-{i + 1}", $"Message {i} key should match");
                valueElement.GetString().Should().Be($"value-{i + 1}", $"Message {i} value should match");
            }
        }
        finally
        {
            // Cleanup
            await DeleteTopicAsync(topicName);
        }
    }

    [Fact]
    public async Task Kafka_FullCycle_ApiEndToEnd()
    {
        // Arrange
        var topicName = $"test-full-cycle-{Guid.NewGuid():N}";

        try
        {
            // Act - Create topic via API
            var createResponse = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/topics", new
            {
                name = topicName,
                partitions = 1,
                replicationFactor = 1
            });
            createResponse.IsSuccessStatusCode.Should().BeTrue("Topic creation should succeed");

            // Wait for topic to be ready
            await Task.Delay(1000);

            // Act - Produce message via API
            var produceResponse = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/produce", new
            {
                topic = topicName,
                key = "cycle-key",
                value = "cycle-value"
            });
            produceResponse.IsSuccessStatusCode.Should().BeTrue("Message produce should succeed");

            // Wait for message to be available
            await Task.Delay(500);

            // Act - Consume message via API
            var consumeResponse = await _fixture.ApiClient.GetAsync(
                $"/api/kafka/consume/{topicName}?count=1&timeout=10");
            consumeResponse.IsSuccessStatusCode.Should().BeTrue("Message consume should succeed");

            var content = await consumeResponse.Content.ReadAsStringAsync();
            var messages = JsonSerializer.Deserialize<List<JsonElement>>(content);

            // Assert - Verify message content
            messages.Should().NotBeNull("Messages should be returned");
            messages.Should().HaveCountGreaterThanOrEqualTo(1, "At least one message should be consumed");

            var message = messages![0];
            message.TryGetProperty("key", out var keyElement).Should().BeTrue("Message should have key");
            message.TryGetProperty("value", out var valueElement).Should().BeTrue("Message should have value");

            keyElement.GetString().Should().Be("cycle-key", "Key should match");
            valueElement.GetString().Should().Be("cycle-value", "Value should match");

            // Act - Delete topic via API
            var deleteResponse = await _fixture.ApiClient.DeleteAsync($"/api/kafka/topics/{topicName}");
            deleteResponse.IsSuccessStatusCode.Should().BeTrue("Topic deletion should succeed");

            // Wait for deletion to propagate
            await Task.Delay(1000);

            // Assert - Verify topic gone
            var listResponse = await _fixture.ApiClient.GetAsync("/api/kafka/topics");
            listResponse.EnsureSuccessStatusCode();

            var listContent = await listResponse.Content.ReadAsStringAsync();
            var topics = JsonSerializer.Deserialize<List<JsonElement>>(listContent);

            var topicNames = topics!.Select(t =>
            {
                if (t.ValueKind == JsonValueKind.String)
                    return t.GetString() ?? string.Empty;
                if (t.TryGetProperty("name", out var nameElement))
                    return nameElement.GetString() ?? string.Empty;
                return string.Empty;
            }).ToList();

            topicNames.Should().NotContain(topicName, "Deleted topic should not appear in list");
        }
        finally
        {
            // Cleanup (in case test failed before deletion)
            await DeleteTopicAsync(topicName);
        }
    }

    [Fact]
    public async Task Kafka_ConsumerGroup_CreatedPerRequest()
    {
        // Arrange
        var topicName = await CreateTestTopicAsync();
        var testKey = "group-test-key";
        var testValue = "group-test-value";

        try
        {
            // Produce a message
            var produceResponse = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/produce", new
            {
                topic = topicName,
                key = testKey,
                value = testValue
            });
            produceResponse.EnsureSuccessStatusCode();

            await Task.Delay(500);

            // Act - Consume from same topic twice (should use different consumer groups)
            var consumeResponse1 = await _fixture.ApiClient.GetAsync(
                $"/api/kafka/consume/{topicName}?count=1&timeout=10");
            consumeResponse1.IsSuccessStatusCode.Should().BeTrue("First consume should succeed");

            var consumeResponse2 = await _fixture.ApiClient.GetAsync(
                $"/api/kafka/consume/{topicName}?count=1&timeout=10");
            consumeResponse2.IsSuccessStatusCode.Should().BeTrue("Second consume should succeed");

            // Assert - Both should receive the message (different consumer groups)
            var content1 = await consumeResponse1.Content.ReadAsStringAsync();
            var messages1 = JsonSerializer.Deserialize<List<JsonElement>>(content1);
            messages1.Should().HaveCountGreaterThanOrEqualTo(1, "First consume should receive message");

            var content2 = await consumeResponse2.Content.ReadAsStringAsync();
            var messages2 = JsonSerializer.Deserialize<List<JsonElement>>(content2);
            messages2.Should().HaveCountGreaterThanOrEqualTo(1, "Second consume should receive message (different consumer group)");
        }
        finally
        {
            // Cleanup
            await DeleteTopicAsync(topicName);
        }
    }
}
