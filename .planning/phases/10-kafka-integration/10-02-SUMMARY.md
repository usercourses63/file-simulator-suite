---
phase: 10-kafka-integration
plan: 02
subsystem: backend-kafka
tags: [confluent-kafka, admin-client, producer, topic-management, consumer-groups]

dependency-graph:
  requires: [10-01]
  provides:
    - kafka-admin-service
    - kafka-producer-service
    - kafka-dto-models
  affects: [10-03]

tech-stack:
  added:
    - Confluent.Kafka 2.12.0
  patterns:
    - AdminClientBuilder for Kafka admin operations
    - ProducerBuilder for message production
    - IOptions<KafkaOptions> for configuration injection

key-files:
  created:
    - src/FileSimulator.ControlApi/Models/KafkaModels.cs
    - src/FileSimulator.ControlApi/Services/IKafkaAdminService.cs
    - src/FileSimulator.ControlApi/Services/KafkaAdminService.cs
    - src/FileSimulator.ControlApi/Services/IKafkaProducerService.cs
    - src/FileSimulator.ControlApi/Services/KafkaProducerService.cs
  modified:
    - src/FileSimulator.ControlApi/FileSimulator.ControlApi.csproj

decisions:
  - id: kafka-admin-watermark
    choice: "Temporary consumer for watermark queries"
    reason: "AdminClient doesn't expose watermark offsets directly; create short-lived consumer per partition query"
  - id: kafka-producer-idempotence
    choice: "EnableIdempotence = true"
    reason: "Exactly-once delivery semantics for reliable message production"
  - id: kafka-consumer-id-property
    choice: "ConsumerId property (not MemberId)"
    reason: "Confluent.Kafka 2.12.0 MemberDescription uses ConsumerId for member identification"

metrics:
  duration: 4 min
  completed: 2026-02-03
---

# Phase 10 Plan 02: Backend Kafka Services Summary

**Confluent.Kafka admin and producer services for topic/consumer group management**

## What Was Built

### 1. Kafka DTO Models (KafkaModels.cs)

Data transfer objects for Kafka operations:

- `KafkaOptions` - Bootstrap servers configuration
- `TopicInfo` - Topic metadata (name, partitions, replication factor)
- `CreateTopicRequest` - Topic creation parameters
- `ConsumerGroupInfo` - Consumer group summary (id, state, member count, lag)
- `ConsumerGroupDetail` - Detailed group info with members and partitions
- `ConsumerGroupMember` - Group member info (consumer id, client id, host)
- `PartitionOffset` - Partition offset tracking for lag calculation
- `ProduceMessageRequest` - Message production request (topic, key, value)
- `ProduceMessageResult` - Production result (partition, offset, timestamp)
- `KafkaMessage` - Message for UI display
- `ResetOffsetsRequest` - Offset reset parameters

### 2. KafkaAdminService

Administrative operations using Confluent.Kafka AdminClient:

| Method | Description |
|--------|-------------|
| `GetTopicsAsync` | List all topics (excludes internal __ prefixed) |
| `GetTopicAsync` | Get specific topic by name |
| `CreateTopicAsync` | Create topic with partitions and replication |
| `DeleteTopicAsync` | Delete topic by name |
| `GetConsumerGroupsAsync` | List all consumer groups |
| `GetConsumerGroupDetailAsync` | Get group detail with members and lag |
| `ResetOffsetsAsync` | Reset offsets to earliest/latest (requires Empty state) |
| `DeleteConsumerGroupAsync` | Delete consumer group |
| `HealthCheckAsync` | Check broker connectivity |

### 3. KafkaProducerService

Message production using Confluent.Kafka IProducer:

| Method | Description |
|--------|-------------|
| `ProduceAsync` | Produce message with optional key to topic |

Configuration:
- `Acks.All` - Wait for all replicas
- `EnableIdempotence = true` - Exactly-once semantics

## Technical Details

### Consumer Lag Calculation

To calculate consumer lag, the service:
1. Gets committed offsets via `ListConsumerGroupOffsetsAsync`
2. Creates temporary consumer to query watermark offsets per partition
3. Computes lag as `highWatermark - currentOffset`

```csharp
private (long low, long high) GetWatermarkOffsets(string topic, int partition)
{
    using var consumer = new ConsumerBuilder<Ignore, Ignore>(new ConsumerConfig
    {
        BootstrapServers = _bootstrapServers,
        GroupId = $"watermark-query-{Guid.NewGuid()}"
    }).Build();

    var watermark = consumer.QueryWatermarkOffsets(
        new TopicPartition(topic, partition),
        TimeSpan.FromSeconds(5));

    return (watermark.Low.Value, watermark.High.Value);
}
```

### Offset Reset Constraints

The `ResetOffsetsAsync` method enforces:
- Consumer group must be in "Empty" state
- Topic must exist
- Supports "earliest" and "latest" targets

## Files Created

| File | Purpose |
|------|---------|
| `Models/KafkaModels.cs` | All Kafka DTOs |
| `Services/IKafkaAdminService.cs` | Admin service interface |
| `Services/KafkaAdminService.cs` | Admin service implementation |
| `Services/IKafkaProducerService.cs` | Producer service interface |
| `Services/KafkaProducerService.cs` | Producer service implementation |

## Commits

| Hash | Description |
|------|-------------|
| `3fda394` | Add Confluent.Kafka package and Kafka DTO models |
| `b21af41` | Implement KafkaAdminService for topic and consumer group management |
| `2c8a1d7` | Implement KafkaProducerService for message production |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed MemberDescription property name**

- **Found during:** Task 2
- **Issue:** Plan specified `m.MemberId` but Confluent.Kafka 2.12.0 uses `m.ConsumerId`
- **Fix:** Changed property access to `m.ConsumerId`
- **Files modified:** `Services/KafkaAdminService.cs`
- **Commit:** `b21af41`

## Integration Notes

### DI Registration (for 10-03)

Services need to be registered in Program.cs:

```csharp
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));
builder.Services.AddSingleton<IKafkaAdminService, KafkaAdminService>();
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
```

### Configuration (for 10-03)

appsettings.json:
```json
{
  "Kafka": {
    "BootstrapServers": "kafka:9092"
  }
}
```

## Next Phase Readiness

- Services ready for 10-03 REST API endpoints
- DI registration and configuration needed in Program.cs
- No blocking issues
