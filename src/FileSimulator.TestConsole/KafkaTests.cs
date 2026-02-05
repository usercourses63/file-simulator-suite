using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using FileSimulator.TestConsole.Models;

namespace FileSimulator.TestConsole;

public static class KafkaTests
{
    public static async Task TestKafkaAsync(IConfiguration config, string apiBaseUrl)
    {
        AnsiConsole.Write(new Rule("[yellow]Kafka Integration Tests[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var results = new List<KafkaTestResult>();

        // Get Kafka configuration
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "file-simulator.local:30093";

        AnsiConsole.MarkupLine($"[grey]Bootstrap Servers: {bootstrapServers}[/]");
        AnsiConsole.MarkupLine($"[grey]Control API: {apiBaseUrl}[/]");
        AnsiConsole.WriteLine();

        await AnsiConsole.Status()
            .StartAsync("Running Kafka tests...", async ctx =>
            {
                // Test 1: Broker connectivity
                ctx.Status("Testing broker connectivity...");
                results.Add(await TestBrokerConnectivityAsync(bootstrapServers));

                // Create HttpClient for API tests
                using var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(apiBaseUrl),
                    Timeout = TimeSpan.FromSeconds(10)
                };

                // Test 2-4: Topic management via API
                ctx.Status("Testing topic management...");
                var topicResults = await TestTopicManagementAsync(httpClient);
                results.AddRange(topicResults);

                // Test 5-6: Direct produce/consume
                ctx.Status("Testing direct produce/consume...");
                var directResults = await TestProduceConsumeAsync(bootstrapServers);
                results.AddRange(directResults);

                // Test 7-8: API produce/consume
                ctx.Status("Testing API produce/consume...");
                var apiResults = await TestApiProduceConsumeAsync(httpClient);
                results.AddRange(apiResults);
            });

        // Display results
        DisplayKafkaResults(results);
    }

    private static async Task<KafkaTestResult> TestBrokerConnectivityAsync(string bootstrapServers)
    {
        var result = new KafkaTestResult { TestName = "Broker Connection" };
        var sw = Stopwatch.StartNew();

        try
        {
            var config = new AdminClientConfig
            {
                BootstrapServers = bootstrapServers,
                SocketTimeoutMs = 10000
            };

            using var adminClient = new AdminClientBuilder(config).Build();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));

            result.Success = metadata.Brokers.Count > 0;
            result.DurationMs = sw.ElapsedMilliseconds;

            if (result.Success)
            {
                var broker = metadata.Brokers.First();
                result.Details = $"Broker {broker.BrokerId}: {broker.Host}:{broker.Port}";
            }
            else
            {
                result.Error = "No brokers found";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.DurationMs = sw.ElapsedMilliseconds;
            result.Error = ex.Message;
        }

        return result;
    }

    private static async Task<List<KafkaTestResult>> TestTopicManagementAsync(HttpClient client)
    {
        var results = new List<KafkaTestResult>();
        var topicName = $"test-topic-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        // Create topic
        var createResult = new KafkaTestResult { TestName = "Topic Create" };
        var sw = Stopwatch.StartNew();
        try
        {
            var createPayload = new
            {
                name = topicName,
                partitions = 1,
                replicationFactor = 1
            };

            var response = await client.PostAsJsonAsync("/api/kafka/topics", createPayload);
            createResult.Success = response.IsSuccessStatusCode;
            createResult.DurationMs = sw.ElapsedMilliseconds;
            createResult.Details = $"Topic: {topicName}";

            if (!createResult.Success)
            {
                createResult.Error = $"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
            }
        }
        catch (Exception ex)
        {
            createResult.Success = false;
            createResult.DurationMs = sw.ElapsedMilliseconds;
            createResult.Error = ex.Message;
        }
        results.Add(createResult);

        // List topics
        var listResult = new KafkaTestResult { TestName = "Topic List" };
        sw.Restart();
        try
        {
            var response = await client.GetAsync("/api/kafka/topics");
            listResult.Success = response.IsSuccessStatusCode;
            listResult.DurationMs = sw.ElapsedMilliseconds;

            if (listResult.Success)
            {
                var content = await response.Content.ReadAsStringAsync();
                var topicFound = content.Contains(topicName);
                listResult.Success = topicFound;
                listResult.Details = topicFound ? $"Found {topicName}" : $"{topicName} not found";

                if (!topicFound)
                {
                    listResult.Error = "Created topic not found in list";
                }
            }
            else
            {
                listResult.Error = $"HTTP {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            listResult.Success = false;
            listResult.DurationMs = sw.ElapsedMilliseconds;
            listResult.Error = ex.Message;
        }
        results.Add(listResult);

        // Delete topic
        var deleteResult = new KafkaTestResult { TestName = "Topic Delete" };
        sw.Restart();
        try
        {
            var response = await client.DeleteAsync($"/api/kafka/topics/{topicName}");
            deleteResult.Success = response.IsSuccessStatusCode;
            deleteResult.DurationMs = sw.ElapsedMilliseconds;
            deleteResult.Details = $"Topic: {topicName}";

            if (!deleteResult.Success)
            {
                deleteResult.Error = $"HTTP {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            deleteResult.Success = false;
            deleteResult.DurationMs = sw.ElapsedMilliseconds;
            deleteResult.Error = ex.Message;
        }
        results.Add(deleteResult);

        return results;
    }

    private static async Task<List<KafkaTestResult>> TestProduceConsumeAsync(string bootstrapServers)
    {
        var results = new List<KafkaTestResult>();
        var topicName = $"test-direct-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var timestamp = DateTime.UtcNow.ToString("O");
        var testKey = $"test-key-{timestamp}";
        var testValue = $"test-value-{timestamp}";

        List<string> topicsToCleanup = new() { topicName };

        try
        {
            // Create topic first
            var adminConfig = new AdminClientConfig { BootstrapServers = bootstrapServers };
            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = topicName,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            });

            // Wait a bit for topic to be ready
            await Task.Delay(1000);

            // Produce message
            var produceResult = new KafkaTestResult { TestName = "Direct Produce" };
            var sw = Stopwatch.StartNew();
            try
            {
                var producerConfig = new ProducerConfig
                {
                    BootstrapServers = bootstrapServers,
                    MessageTimeoutMs = 10000
                };

                using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

                var deliveryResult = await producer.ProduceAsync(topicName, new Message<string, string>
                {
                    Key = testKey,
                    Value = testValue
                });

                produceResult.Success = deliveryResult.Status == PersistenceStatus.Persisted;
                produceResult.DurationMs = sw.ElapsedMilliseconds;
                produceResult.Details = $"Offset: {deliveryResult.Offset}";

                if (!produceResult.Success)
                {
                    produceResult.Error = $"Status: {deliveryResult.Status}";
                }
            }
            catch (Exception ex)
            {
                produceResult.Success = false;
                produceResult.DurationMs = sw.ElapsedMilliseconds;
                produceResult.Error = ex.Message;
            }
            results.Add(produceResult);

            // Consume message
            var consumeResult = new KafkaTestResult { TestName = "Direct Consume" };
            sw.Restart();
            try
            {
                var consumerConfig = new ConsumerConfig
                {
                    BootstrapServers = bootstrapServers,
                    GroupId = $"test-group-{Guid.NewGuid():N}",
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    EnableAutoCommit = false
                };

                using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
                consumer.Subscribe(topicName);

                var consumed = false;
                var matchedKey = false;
                var matchedValue = false;

                // Try to consume with timeout
                var timeout = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < timeout && !consumed)
                {
                    var consumeResult2 = consumer.Consume(TimeSpan.FromSeconds(2));
                    if (consumeResult2 != null)
                    {
                        consumed = true;
                        matchedKey = consumeResult2.Message.Key == testKey;
                        matchedValue = consumeResult2.Message.Value == testValue;
                        break;
                    }
                }

                consumeResult.Success = consumed && matchedKey && matchedValue;
                consumeResult.DurationMs = sw.ElapsedMilliseconds;

                if (consumeResult.Success)
                {
                    consumeResult.Details = "Message verified";
                }
                else if (!consumed)
                {
                    consumeResult.Error = "No message received";
                }
                else
                {
                    consumeResult.Error = $"Message mismatch (key:{matchedKey}, value:{matchedValue})";
                }

                consumer.Close();
            }
            catch (Exception ex)
            {
                consumeResult.Success = false;
                consumeResult.DurationMs = sw.ElapsedMilliseconds;
                consumeResult.Error = ex.Message;
            }
            results.Add(consumeResult);
        }
        finally
        {
            // Cleanup topics
            await CleanupTopicsAsync(bootstrapServers, topicsToCleanup);
        }

        return results;
    }

    private static async Task<List<KafkaTestResult>> TestApiProduceConsumeAsync(HttpClient client)
    {
        var results = new List<KafkaTestResult>();
        var topicName = $"test-api-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        try
        {
            // Create topic via API
            var createPayload = new
            {
                name = topicName,
                partitions = 1,
                replicationFactor = 1
            };

            var createResponse = await client.PostAsJsonAsync("/api/kafka/topics", createPayload);
            if (!createResponse.IsSuccessStatusCode)
            {
                results.Add(new KafkaTestResult
                {
                    TestName = "API Produce",
                    Success = false,
                    Error = "Failed to create test topic"
                });
                results.Add(new KafkaTestResult
                {
                    TestName = "API Consume",
                    Success = false,
                    Error = "Failed to create test topic"
                });
                return results;
            }

            // Wait for topic to be ready
            await Task.Delay(1000);

            // Produce message via API
            var produceResult = new KafkaTestResult { TestName = "API Produce" };
            var sw = Stopwatch.StartNew();
            try
            {
                var producePayload = new
                {
                    topic = topicName,
                    key = "api-key",
                    value = "api-value"
                };

                var response = await client.PostAsJsonAsync("/api/kafka/produce", producePayload);
                produceResult.Success = response.IsSuccessStatusCode;
                produceResult.DurationMs = sw.ElapsedMilliseconds;
                produceResult.Details = $"Topic: {topicName}";

                if (!produceResult.Success)
                {
                    produceResult.Error = $"HTTP {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                produceResult.Success = false;
                produceResult.DurationMs = sw.ElapsedMilliseconds;
                produceResult.Error = ex.Message;
            }
            results.Add(produceResult);

            // Consume message via API
            var consumeResult = new KafkaTestResult { TestName = "API Consume" };
            sw.Restart();
            try
            {
                var response = await client.GetAsync($"/api/kafka/consume/{topicName}?count=1&timeout=10");
                consumeResult.Success = response.IsSuccessStatusCode;
                consumeResult.DurationMs = sw.ElapsedMilliseconds;

                if (consumeResult.Success)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messages = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(content);

                    if (messages != null && messages.Count > 0)
                    {
                        var message = messages[0];
                        var keyMatch = message.ContainsKey("key") && message["key"].GetString() == "api-key";
                        var valueMatch = message.ContainsKey("value") && message["value"].GetString() == "api-value";

                        consumeResult.Success = keyMatch && valueMatch;
                        consumeResult.Details = consumeResult.Success ? "Message verified" : "Message mismatch";

                        if (!consumeResult.Success)
                        {
                            consumeResult.Error = $"Key match: {keyMatch}, Value match: {valueMatch}";
                        }
                    }
                    else
                    {
                        consumeResult.Success = false;
                        consumeResult.Error = "No messages received";
                    }
                }
                else
                {
                    consumeResult.Error = $"HTTP {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                consumeResult.Success = false;
                consumeResult.DurationMs = sw.ElapsedMilliseconds;
                consumeResult.Error = ex.Message;
            }
            results.Add(consumeResult);

            // Cleanup topic via API
            try
            {
                await client.DeleteAsync($"/api/kafka/topics/{topicName}");
            }
            catch
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Failed to cleanup topic {topicName}[/]");
            }
        }
        catch (Exception ex)
        {
            results.Add(new KafkaTestResult
            {
                TestName = "API Produce",
                Success = false,
                Error = ex.Message
            });
            results.Add(new KafkaTestResult
            {
                TestName = "API Consume",
                Success = false,
                Error = ex.Message
            });
        }

        return results;
    }

    private static async Task CleanupTopicsAsync(string bootstrapServers, List<string> topics)
    {
        if (topics.Count == 0) return;

        try
        {
            var adminConfig = new AdminClientConfig { BootstrapServers = bootstrapServers };
            using var adminClient = new AdminClientBuilder(adminConfig).Build();

            await adminClient.DeleteTopicsAsync(topics);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Failed to cleanup topics: {ex.Message}[/]");
        }
    }

    private static void DisplayKafkaResults(List<KafkaTestResult> results)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Kafka Test Results[/]").RuleStyle("grey"));

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Test[/]")
            .AddColumn("[bold]Status[/]")
            .AddColumn("[bold]Duration[/]")
            .AddColumn("[bold]Details[/]");

        foreach (var result in results)
        {
            var status = result.Success ? "[green]PASS[/]" : "[red]FAIL[/]";
            var duration = $"[cyan]{result.DurationMs}ms[/]";
            var details = result.Success ? result.Details : (result.Error ?? "Unknown error");

            table.AddRow(result.TestName, status, duration, details.Truncate(60));
        }

        AnsiConsole.Write(table);

        // Summary
        var passed = results.Count(r => r.Success);
        var failed = results.Count - passed;

        AnsiConsole.WriteLine();
        var summaryColor = failed == 0 ? "green" : "yellow";
        AnsiConsole.MarkupLine($"[bold {summaryColor}]Summary: {passed}/{results.Count} tests passed[/]");

        if (failed > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Failed tests:[/]");
            foreach (var result in results.Where(r => !r.Success))
            {
                AnsiConsole.MarkupLine($"  [red]- {result.TestName}[/]: {result.Error ?? "Unknown error"}");
            }
        }

        AnsiConsole.WriteLine();
    }
}
