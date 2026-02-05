using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FileSimulator.IntegrationTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace FileSimulator.IntegrationTests.Kafka;

/// <summary>
/// Tests Kafka topic management operations via both API and direct client.
/// </summary>
[Collection("Simulator")]
public class KafkaTopicManagementTests
{
    private readonly SimulatorCollectionFixture _fixture;
    private readonly string _bootstrapServers;

    public KafkaTopicManagementTests(SimulatorCollectionFixture fixture)
    {
        _fixture = fixture;
        _bootstrapServers = fixture.Configuration["Kafka:BootstrapServers"]
            ?? "file-simulator.local:30093";
    }

    /// <summary>
    /// Generates a unique topic name for testing.
    /// </summary>
    private static string GenerateTopicName() => $"test-topic-{Guid.NewGuid():N}";

    [Fact]
    public async Task Kafka_CreateTopic_ViaApi()
    {
        // Arrange
        var topicName = GenerateTopicName();

        try
        {
            // Act
            var response = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/topics", new
            {
                name = topicName,
                partitions = 1,
                replicationFactor = 1
            });

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Topic creation should succeed. Status: {response.StatusCode}");
        }
        finally
        {
            // Cleanup
            try
            {
                await _fixture.ApiClient.DeleteAsync($"/api/kafka/topics/{topicName}");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Kafka_ListTopics_ContainsCreatedTopic()
    {
        // Arrange
        var topicName = GenerateTopicName();

        try
        {
            // Act - Create topic
            var createResponse = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/topics", new
            {
                name = topicName,
                partitions = 1,
                replicationFactor = 1
            });
            createResponse.EnsureSuccessStatusCode();

            // Wait for topic to be created
            await Task.Delay(1000);

            // Act - List topics
            var listResponse = await _fixture.ApiClient.GetAsync("/api/kafka/topics");
            listResponse.EnsureSuccessStatusCode();

            var content = await listResponse.Content.ReadAsStringAsync();
            var topics = JsonSerializer.Deserialize<List<JsonElement>>(content);

            // Assert
            topics.Should().NotBeNull("Topics list should be returned");
            var topicNames = topics!.Select(t =>
            {
                if (t.ValueKind == JsonValueKind.String)
                    return t.GetString() ?? string.Empty;
                if (t.TryGetProperty("name", out var nameElement))
                    return nameElement.GetString() ?? string.Empty;
                return string.Empty;
            }).ToList();

            topicNames.Should().Contain(topicName, "Created topic should appear in list");
        }
        finally
        {
            // Cleanup
            try
            {
                await _fixture.ApiClient.DeleteAsync($"/api/kafka/topics/{topicName}");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Kafka_DeleteTopic_RemovesTopic()
    {
        // Arrange
        var topicName = GenerateTopicName();

        // Create topic
        var createResponse = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/topics", new
        {
            name = topicName,
            partitions = 1,
            replicationFactor = 1
        });
        createResponse.EnsureSuccessStatusCode();

        // Wait for topic to be created
        await Task.Delay(1000);

        // Act - Delete topic
        var deleteResponse = await _fixture.ApiClient.DeleteAsync($"/api/kafka/topics/{topicName}");

        // Assert
        deleteResponse.IsSuccessStatusCode.Should().BeTrue(
            $"Topic deletion should succeed. Status: {deleteResponse.StatusCode}");

        // Wait for deletion to propagate
        await Task.Delay(1000);

        // Verify topic no longer exists
        var listResponse = await _fixture.ApiClient.GetAsync("/api/kafka/topics");
        listResponse.EnsureSuccessStatusCode();

        var content = await listResponse.Content.ReadAsStringAsync();
        var topics = JsonSerializer.Deserialize<List<JsonElement>>(content);

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

    [Fact]
    public async Task Kafka_CreateTopic_DirectClient()
    {
        // Arrange
        var topicName = GenerateTopicName();
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = _bootstrapServers
        };

        using var adminClient = new AdminClientBuilder(adminConfig).Build();

        try
        {
            // Act
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = topicName,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            });

            // Wait for topic to be created
            await Task.Delay(1000);

            // Assert - Verify via metadata
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
            var topicExists = metadata.Topics.Any(t => t.Topic == topicName);

            topicExists.Should().BeTrue($"Topic {topicName} should exist in cluster metadata");
        }
        finally
        {
            // Cleanup
            try
            {
                await adminClient.DeleteTopicsAsync(new[] { topicName });
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task Kafka_BrokerConnectivity()
    {
        // Arrange
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = _bootstrapServers,
            SocketTimeoutMs = 10000
        };

        using var adminClient = new AdminClientBuilder(adminConfig).Build();

        // Act
        var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));

        // Assert
        metadata.Should().NotBeNull("Metadata should be retrieved");
        metadata.Brokers.Should().NotBeEmpty("At least one broker should be available");

        var broker = metadata.Brokers.First();
        broker.BrokerId.Should().BeGreaterThanOrEqualTo(0, "Broker should have valid ID");
        broker.Host.Should().NotBeNullOrEmpty("Broker should have host");
        broker.Port.Should().BeGreaterThan(0, "Broker should have valid port");
    }

    [Fact]
    public async Task Kafka_CreateMultipleTopics_AllSucceed()
    {
        // Arrange
        var topicNames = Enumerable.Range(1, 3)
            .Select(_ => GenerateTopicName())
            .ToList();

        try
        {
            // Act - Create all topics
            foreach (var topicName in topicNames)
            {
                var response = await _fixture.ApiClient.PostAsJsonAsync("/api/kafka/topics", new
                {
                    name = topicName,
                    partitions = 1,
                    replicationFactor = 1
                });
                response.EnsureSuccessStatusCode();
            }

            // Wait for topics to be created
            await Task.Delay(2000);

            // Assert - All topics exist
            var listResponse = await _fixture.ApiClient.GetAsync("/api/kafka/topics");
            listResponse.EnsureSuccessStatusCode();

            var content = await listResponse.Content.ReadAsStringAsync();
            var topics = JsonSerializer.Deserialize<List<JsonElement>>(content);

            var existingTopicNames = topics!.Select(t =>
            {
                if (t.ValueKind == JsonValueKind.String)
                    return t.GetString() ?? string.Empty;
                if (t.TryGetProperty("name", out var nameElement))
                    return nameElement.GetString() ?? string.Empty;
                return string.Empty;
            }).ToList();

            foreach (var topicName in topicNames)
            {
                existingTopicNames.Should().Contain(topicName, $"Topic {topicName} should exist");
            }
        }
        finally
        {
            // Cleanup all topics
            foreach (var topicName in topicNames)
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
        }
    }
}
