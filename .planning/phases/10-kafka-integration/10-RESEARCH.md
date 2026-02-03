# Phase 10: Kafka Integration for Event Streaming - Research

**Researched:** 2026-02-03
**Domain:** Apache Kafka deployment, .NET Kafka client, Kubernetes orchestration
**Confidence:** MEDIUM

## Summary

This phase integrates Apache Kafka as a message broker simulator into the existing File Simulator Suite. The research focused on three areas: (1) Kafka+ZooKeeper deployment in Kubernetes using Bitnami container images, (2) .NET Kafka client library (Confluent.Kafka) for backend API integration, and (3) Provectus Kafka-UI for power-user management.

The user has locked the decision to use **ZooKeeper mode** (not KRaft), though Bitnami images now default to KRaft. This requires explicit configuration via `KAFKA_CFG_ZOOKEEPER_CONNECT` environment variable. A single-broker deployment with ZooKeeper sidecar in the same pod simplifies lifecycle management for development purposes.

The .NET backend will use **Confluent.Kafka** (v2.12+), the industry-standard .NET client. It provides Producer, Consumer, and AdminClient APIs for topic management and consumer group monitoring. The AdminClient supports all required operations: `CreateTopicsAsync`, `DeleteTopicsAsync`, `DescribeConsumerGroupsAsync`, `ListConsumerGroupOffsetsAsync`, and `AlterConsumerGroupOffsetsAsync`.

**Primary recommendation:** Deploy bitnami/kafka with bitnami/zookeeper as sidecar containers in a single pod, use Confluent.Kafka for all backend operations, and add Kafka-UI as a supplementary management interface.

## Standard Stack

### Core

| Library/Image | Version | Purpose | Why Standard |
|---------------|---------|---------|--------------|
| bitnami/kafka | 3.7+ | Kafka broker container | Well-maintained, supports both ZooKeeper and KRaft modes, non-root container |
| bitnami/zookeeper | 3.9+ | Metadata coordination | Matched versioning with Kafka, same vendor for compatibility |
| Confluent.Kafka | 2.12.0 | .NET Kafka client | Official Confluent client, wraps librdkafka, full AdminClient support |
| provectuslabs/kafka-ui | latest | Web management UI | 100M+ Docker pulls, active community, multi-cluster support |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| librdkafka.redist | (bundled) | Native Kafka library | Automatically included with Confluent.Kafka |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| bitnami/kafka | confluentinc/cp-kafka | Confluent images are heavier, bitnami is lighter for dev |
| ZooKeeper mode | KRaft mode | KRaft is newer/simpler but user explicitly chose ZooKeeper |
| Kafka-UI | Conduktor, AKHQ | Kafka-UI is simpler, lightweight, sufficient for dev testing |

**Installation (.NET):**
```bash
dotnet add package Confluent.Kafka --version 2.12.0
```

## Architecture Patterns

### Recommended Deployment Structure (Kubernetes)

```
kafka-zookeeper-pod/
  container: zookeeper (bitnami/zookeeper)
    port: 2181
    memory: 256Mi (128Mi heap)
  container: kafka (bitnami/kafka)
    ports: 9092 (internal), 9094 (external)
    memory: 768Mi (512Mi heap)
    depends: zookeeper ready

kafka-ui-pod/
  container: kafka-ui (provectuslabs/kafka-ui)
    port: 8080
    memory: 512Mi (256Mi heap)
    connects-to: kafka:9092
```

### Pattern 1: ZooKeeper Mode Configuration (Bitnami)

**What:** Configure Kafka to use ZooKeeper instead of KRaft (default)
**When to use:** When ZooKeeper mode is required (as per user decision)
**Example:**
```yaml
# Source: https://github.com/bitnami/containers/blob/main/bitnami/kafka/README.md
env:
  - name: KAFKA_CFG_ZOOKEEPER_CONNECT
    value: "localhost:2181"  # Sidecar ZooKeeper
  - name: KAFKA_CFG_LISTENERS
    value: "PLAINTEXT://:9092,EXTERNAL://:9094"
  - name: KAFKA_CFG_ADVERTISED_LISTENERS
    value: "PLAINTEXT://kafka:9092,EXTERNAL://$(EXTERNAL_IP):9094"
  - name: KAFKA_CFG_LISTENER_SECURITY_PROTOCOL_MAP
    value: "PLAINTEXT:PLAINTEXT,EXTERNAL:PLAINTEXT"
  - name: KAFKA_CFG_INTER_BROKER_LISTENER_NAME
    value: "PLAINTEXT"
  - name: KAFKA_HEAP_OPTS
    value: "-Xmx512m -Xms512m"
```

### Pattern 2: AdminClient Topic Management

**What:** Create/delete topics and manage partitions via C# AdminClient
**When to use:** Topic management API endpoints
**Example:**
```csharp
// Source: https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/examples/AdminClient/Program.cs
using var adminClient = new AdminClientBuilder(new AdminClientConfig
{
    BootstrapServers = "kafka:9092"
}).Build();

// Create topic
await adminClient.CreateTopicsAsync(new TopicSpecification[]
{
    new TopicSpecification
    {
        Name = "test-events",
        NumPartitions = 3,
        ReplicationFactor = 1
    }
});

// Delete topic
await adminClient.DeleteTopicsAsync(new[] { "test-events" });

// Get metadata (topic list, partition info)
var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
foreach (var topic in metadata.Topics)
{
    Console.WriteLine($"{topic.Topic}: {topic.Partitions.Count} partitions");
}
```

### Pattern 3: Consumer Group Monitoring

**What:** List groups, describe members, get lag per partition
**When to use:** Consumer group monitoring dashboard
**Example:**
```csharp
// Source: https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.IAdminClient.html

// List all consumer groups
var groups = await adminClient.ListConsumerGroupsAsync();

// Describe specific groups
var descriptions = await adminClient.DescribeConsumerGroupsAsync(
    new[] { "my-consumer-group" });

foreach (var group in descriptions.ConsumerGroupDescriptions)
{
    Console.WriteLine($"Group: {group.GroupId}, State: {group.State}");
    Console.WriteLine($"Members: {group.Members.Count}");
}

// Get offsets for lag calculation
var offsets = await adminClient.ListConsumerGroupOffsetsAsync(
    new[] { new ConsumerGroupTopicPartitions("my-group",
        new[] { new TopicPartition("my-topic", 0) }) });

// Reset offsets (requires group to be inactive)
await adminClient.AlterConsumerGroupOffsetsAsync(
    new[] { new ConsumerGroupTopicPartitionOffsets("my-group",
        new[] { new TopicPartitionOffset("my-topic", 0, Offset.Beginning) }) });
```

### Pattern 4: Message Production for Testing

**What:** Produce messages to topics via API
**When to use:** Message testing interface
**Example:**
```csharp
// Source: https://docs.confluent.io/kafka-clients/dotnet/current/overview.html
var config = new ProducerConfig { BootstrapServers = "kafka:9092" };

using var producer = new ProducerBuilder<string, string>(config).Build();

var result = await producer.ProduceAsync("test-events",
    new Message<string, string>
    {
        Key = "optional-key",  // null if not provided
        Value = "message body from user"
    });

Console.WriteLine($"Delivered to {result.TopicPartitionOffset}");
```

### Pattern 5: Reading Last N Messages

**What:** Get recent messages from topic for display
**When to use:** Message viewer with rolling buffer
**Example:**
```csharp
// Source: https://forum.confluent.io/t/i-want-to-read-just-the-last-newest-message-in-a-kafka-topic/7536
var config = new ConsumerConfig
{
    BootstrapServers = "kafka:9092",
    GroupId = $"dashboard-viewer-{Guid.NewGuid()}", // Unique group per request
    AutoOffsetReset = AutoOffsetReset.Latest,
    EnableAutoCommit = false  // Don't commit - just viewing
};

using var consumer = new ConsumerBuilder<string, string>(config).Build();

// Get partition info
var metadata = adminClient.GetMetadata("my-topic", TimeSpan.FromSeconds(5));
var partitions = metadata.Topics[0].Partitions
    .Select(p => new TopicPartition("my-topic", p.PartitionId))
    .ToList();

// Query watermarks and seek to N messages before end
var messages = new List<MessageDto>();
foreach (var tp in partitions)
{
    var watermark = consumer.QueryWatermarkOffsets(tp, TimeSpan.FromSeconds(5));
    var startOffset = Math.Max(watermark.Low.Value, watermark.High.Value - 50);
    consumer.Assign(new TopicPartitionOffset(tp, startOffset));
}

// Consume until we have 50 messages or reach end
while (messages.Count < 50)
{
    var result = consumer.Consume(TimeSpan.FromMilliseconds(100));
    if (result == null) break;
    messages.Add(new MessageDto(result.Key, result.Value, result.Timestamp.UtcDateTime));
}
```

### Anti-Patterns to Avoid

- **Using KRaft configuration with ZooKeeper:** Don't set `KAFKA_CFG_PROCESS_ROLES` when using ZooKeeper mode - this enables KRaft
- **Hardcoded broker IDs with multiple replicas:** Let Kafka auto-generate broker IDs for single-broker dev setup
- **Synchronous Commit in high-throughput:** Use `StoreOffset` + background commit instead of blocking `Commit()`
- **Creating new consumer groups per message view:** Reuse consumer instances where possible, use unique groups only for isolated viewing

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Topic CRUD operations | Custom Kafka protocol implementation | AdminClient.CreateTopicsAsync/DeleteTopicsAsync | Protocol complexity, error handling |
| Consumer lag calculation | Manual offset tracking | AdminClient.ListConsumerGroupOffsetsAsync + QueryWatermarkOffsets | Race conditions, multi-partition complexity |
| Message serialization | Custom byte[] handling | Confluent.Kafka built-in serializers | Edge cases with encoding |
| Kafka UI | Custom topic browser | Kafka-UI (provectuslabs/kafka-ui) | Full-featured, maintained, 100M+ users |
| Readiness probes | Custom TCP checks | Kafka's built-in JMX or AdminClient.GetMetadata | Cluster state awareness |

**Key insight:** Kafka's protocol is complex. The AdminClient handles retries, leader discovery, and error mapping. Custom implementations miss edge cases around partition leadership changes and transient failures.

## Common Pitfalls

### Pitfall 1: ZooKeeper Not Ready Before Kafka Starts

**What goes wrong:** Kafka fails to connect to ZooKeeper, crashes on startup
**Why it happens:** In sidecar pattern, both containers start simultaneously
**How to avoid:** Use init container or readiness probe to wait for ZooKeeper port 2181
**Warning signs:** Kafka logs show "Connection refused" to localhost:2181

```yaml
# Prevention: ZooKeeper readiness probe
readinessProbe:
  exec:
    command: ['sh', '-c', 'echo "ruok" | nc localhost 2181 | grep imok']
  initialDelaySeconds: 5
  periodSeconds: 10
```

### Pitfall 2: ADVERTISED_LISTENERS Mismatch

**What goes wrong:** External clients can connect but can't produce/consume
**Why it happens:** Kafka returns broker addresses clients can't reach
**How to avoid:** Set EXTERNAL listener to actual NodePort/LoadBalancer address
**Warning signs:** Client connects then times out on metadata fetch

### Pitfall 3: Consumer Group State for Offset Reset

**What goes wrong:** AlterConsumerGroupOffsets fails with "group is not empty"
**Why it happens:** Can only alter offsets for inactive (Empty) groups
**How to avoid:** Check group state first, warn user if active
**Warning signs:** `GroupNotEmpty` exception from AdminClient

```csharp
// Check before reset
var description = await adminClient.DescribeConsumerGroupsAsync(new[] { groupId });
if (description.ConsumerGroupDescriptions[0].State != ConsumerGroupState.Empty)
{
    throw new InvalidOperationException("Group must be inactive to reset offsets");
}
```

### Pitfall 4: Memory Exhaustion in Small Clusters

**What goes wrong:** Kafka or ZooKeeper OOM killed by Kubernetes
**Why it happens:** JVM heap defaults are too high for dev environments
**How to avoid:** Explicitly set KAFKA_HEAP_OPTS and ZOO_HEAP_SIZE
**Warning signs:** Pod restart with OOMKilled reason

```yaml
# Kafka: 512MB heap, ~768MB total with overhead
env:
  - name: KAFKA_HEAP_OPTS
    value: "-Xmx512m -Xms512m"
# ZooKeeper: 128MB heap, ~256MB total
env:
  - name: ZOO_HEAP_SIZE
    value: "128"
```

### Pitfall 5: Topic Auto-Creation Confusion

**What goes wrong:** Topics appear with wrong partition count
**Why it happens:** auto.create.topics.enable defaults to true
**How to avoid:** Disable auto-creation, create topics explicitly via API
**Warning signs:** Topics with 1 partition when you expected 3

```yaml
env:
  - name: KAFKA_CFG_AUTO_CREATE_TOPICS_ENABLE
    value: "false"
```

## Code Examples

### Complete ControlApi Kafka Service Pattern

```csharp
// Source: Pattern matching existing ControlApi services
public interface IKafkaAdminService
{
    Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct);
    Task CreateTopicAsync(string name, int partitions, CancellationToken ct);
    Task DeleteTopicAsync(string name, CancellationToken ct);
    Task<IEnumerable<ConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken ct);
    Task<ConsumerGroupDetail> GetConsumerGroupDetailAsync(string groupId, CancellationToken ct);
    Task ResetOffsetsAsync(string groupId, string topic, long offset, CancellationToken ct);
}

public class KafkaAdminService : IKafkaAdminService, IDisposable
{
    private readonly IAdminClient _adminClient;
    private readonly ILogger<KafkaAdminService> _logger;

    public KafkaAdminService(IOptions<KafkaOptions> options, ILogger<KafkaAdminService> logger)
    {
        _logger = logger;
        _adminClient = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = options.Value.BootstrapServers
        }).Build();
    }

    public async Task<IEnumerable<TopicInfo>> GetTopicsAsync(CancellationToken ct)
    {
        var metadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));
        return metadata.Topics
            .Where(t => !t.Topic.StartsWith("__")) // Exclude internal topics
            .Select(t => new TopicInfo
            {
                Name = t.Topic,
                PartitionCount = t.Partitions.Count,
                ReplicationFactor = t.Partitions.FirstOrDefault()?.Replicas.Length ?? 1
            });
    }

    public void Dispose() => _adminClient?.Dispose();
}
```

### Kafka-UI Environment Configuration

```yaml
# Source: https://github.com/provectus/kafka-ui
env:
  - name: KAFKA_CLUSTERS_0_NAME
    value: "file-simulator"
  - name: KAFKA_CLUSTERS_0_BOOTSTRAPSERVERS
    value: "kafka:9092"
  - name: DYNAMIC_CONFIG_ENABLED
    value: "true"
  - name: SERVER_PORT
    value: "8080"
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ZooKeeper required | KRaft mode (ZooKeeper-less) | Kafka 3.3+ (2022) | Simpler deployment, but user chose ZooKeeper |
| Separate ZK cluster | Sidecar or embedded ZK | Common pattern | Easier dev lifecycle |
| KAFKA_ENABLE_KRAFT | KAFKA_CFG_PROCESS_ROLES | Bitnami 3.x | New env var names |
| ALLOW_PLAINTEXT_LISTENER | Removed in Bitnami | 2024 | Plaintext allowed by default |

**Deprecated/outdated:**
- `KAFKA_ENABLE_KRAFT`: Removed, use `KAFKA_CFG_PROCESS_ROLES` for KRaft
- `ALLOW_PLAINTEXT_LISTENER`: Removed from Bitnami images
- `KAFKA_ZOOKEEPER_CONNECT`: Changed to `KAFKA_CFG_ZOOKEEPER_CONNECT`

## Open Questions

1. **External Access Method**
   - What we know: NodePort (30xxx) works, port-forward is simpler for dev
   - What's unclear: Whether Windows clients need direct Kafka access or only via ControlApi
   - Recommendation: Use NodePort 30092 for external access, keep 9092 internal; add to Claude's Discretion

2. **Default Topic Retention**
   - What we know: Production uses days/weeks, dev can use hours
   - What's unclear: User's preference for test data persistence
   - Recommendation: 24 hours (86400000ms) - enough to survive overnight, not fill disk; Claude's Discretion

3. **Live Message Streaming Implementation**
   - What we know: SignalR can push updates, consumer can poll
   - What's unclear: Whether to use dedicated consumer per client or shared broadcast
   - Recommendation: Shared consumer per topic with SignalR broadcast (matches existing patterns)

## Sources

### Primary (HIGH confidence)
- [Confluent.Kafka NuGet 2.12.0](https://www.nuget.org/packages/Confluent.Kafka/) - Version and package info
- [confluent-kafka-dotnet GitHub](https://github.com/confluentinc/confluent-kafka-dotnet) - API examples, AdminClient usage
- [IAdminClient Interface Docs](https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/_site/api/Confluent.Kafka.IAdminClient.html) - Full method listing

### Secondary (MEDIUM confidence)
- [Bitnami Kafka README](https://github.com/bitnami/containers/blob/main/bitnami/kafka/README.md) - Container configuration
- [Bitnami ZooKeeper](https://hub.docker.com/r/bitnami/zookeeper/) - ZooKeeper container
- [Provectus Kafka-UI GitHub](https://github.com/provectus/kafka-ui) - UI configuration
- [Confluent .NET Overview](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html) - Producer/Consumer patterns

### Tertiary (LOW confidence)
- [ConfigZen Kafka Pitfalls](https://configzen.com/blog/common-pitfalls-apache-kafka-kubernetes) - Kubernetes deployment issues (WebSearch, not verified)
- [Confluent Forum - Last N messages](https://forum.confluent.io/t/i-want-to-read-just-the-last-newest-message-in-a-kafka-topic/7536) - Offset seeking pattern

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official packages, well-documented
- Architecture: MEDIUM - Sidecar pattern is common but ZooKeeper mode less documented in Bitnami
- Pitfalls: MEDIUM - Mix of official docs and community experience
- AdminClient API: HIGH - Official interface documentation

**Research date:** 2026-02-03
**Valid until:** 2026-03-03 (30 days - Kafka ecosystem is stable)

---

## Appendix: Helm Values Structure (Recommendation)

Based on existing `values.yaml` patterns in this project:

```yaml
# ============================================
# Kafka Message Broker
# ============================================
kafka:
  enabled: true

  image:
    repository: bitnami/kafka
    tag: "3.7"
    pullPolicy: IfNotPresent

  zookeeper:
    image:
      repository: bitnami/zookeeper
      tag: "3.9"
    resources:
      requests:
        memory: "256Mi"
        cpu: "100m"
      limits:
        memory: "512Mi"
        cpu: "500m"
    heap: "128"  # ZOO_HEAP_SIZE in MB

  service:
    type: NodePort
    port: 9092
    nodePort: 30092
    externalPort: 9094
    externalNodePort: 30094

  # JVM heap for broker
  heap: "512m"  # -Xmx/-Xms

  # Default topics created on startup
  defaultTopics:
    - name: test-events
      partitions: 3
    - name: test-commands
      partitions: 1
    - name: test-notifications
      partitions: 1

  # Topic retention (Claude's Discretion - recommend 24h)
  retention:
    ms: 86400000  # 24 hours

  resources:
    requests:
      memory: "768Mi"
      cpu: "200m"
    limits:
      memory: "1536Mi"
      cpu: "1"

# ============================================
# Kafka UI (Provectus)
# ============================================
kafkaUi:
  enabled: true

  image:
    repository: provectuslabs/kafka-ui
    tag: latest
    pullPolicy: IfNotPresent

  service:
    type: NodePort
    port: 8080
    nodePort: 30093

  resources:
    requests:
      memory: "256Mi"
      cpu: "100m"
    limits:
      memory: "512Mi"
      cpu: "500m"
```
