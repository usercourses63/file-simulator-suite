---
phase: 10-kafka-integration
plan: 04
subsystem: frontend
tags: [typescript, react, kafka, signalr, hooks]
dependency-graph:
  requires:
    - 10-03 (Kafka REST API and SignalR hub)
  provides:
    - TypeScript types for Kafka DTOs
    - useKafka hook for REST API operations
    - useKafkaStream hook for SignalR streaming
    - KafkaTab component with basic UI
  affects:
    - 10-05 (enhanced topic management UI)
    - 10-06 (message producer/viewer components)
tech-stack:
  added: []
  patterns:
    - Custom hooks for API operations (useKafka)
    - Custom hooks for SignalR streaming (useKafkaStream)
    - Tab-based navigation pattern for Kafka
    - BEM-style CSS classes
key-files:
  created:
    - src/dashboard/src/types/kafka.ts
    - src/dashboard/src/hooks/useKafka.ts
    - src/dashboard/src/hooks/useKafkaStream.ts
    - src/dashboard/src/components/KafkaTab.tsx
    - src/dashboard/src/components/KafkaTab.css
  modified:
    - src/dashboard/src/App.tsx
decisions:
  - "Lag color thresholds: green <= 10, yellow <= 100, red > 100"
  - "5-second auto-refresh interval for topics and consumer groups"
  - "50-message default rolling buffer for streaming messages"
  - "Separate CSS file for KafkaTab (KafkaTab.css) not App.css"
metrics:
  duration: 3 min
  completed: 2026-02-03
---

# Phase 10 Plan 04: Frontend Kafka Foundation Summary

Frontend TypeScript types, hooks for API and streaming, and basic KafkaTab UI integrated into navigation.

## What Was Built

### 1. TypeScript Types (kafka.ts)
- **TopicInfo**: Topic metadata with partition count, replication factor
- **CreateTopicRequest**: Request body for topic creation
- **ConsumerGroupInfo**: Group summary for list view
- **ConsumerGroupDetail**: Full group info with members and partitions
- **ConsumerGroupMember**: Individual consumer member details
- **PartitionOffset**: Per-partition offset tracking
- **ProduceMessageRequest/Result**: Message production types
- **KafkaMessage**: Message for display/streaming
- **ResetOffsetsRequest**: Offset reset options
- **LagLevel + getLagLevel()**: Helper for lag color coding

### 2. useKafka Hook (useKafka.ts)
REST API operations with auto-refresh:
- **Topics**: list, create, delete
- **Consumer Groups**: list, detail, reset offsets, delete
- **Messages**: get recent messages, produce message
- **Health**: Kafka connectivity status
- Auto-refresh every 5 seconds
- Error handling for all operations

### 3. useKafkaStream Hook (useKafkaStream.ts)
SignalR real-time streaming:
- Connect to `/hubs/kafka` SignalR endpoint
- Subscribe/unsubscribe to topics
- Receive `KafkaMessage` events
- 50-message rolling buffer (configurable)
- Automatic reconnection with backoff

### 4. KafkaTab Component (KafkaTab.tsx)
Basic UI skeleton:
- Health status indicator (green/yellow/red)
- Topics section: list with partition counts
- Consumer Groups section: list with lag indicators
- Loading/error/empty states
- Responsive grid layout

### 5. App.tsx Integration
- Added `KafkaTab` import
- Extended `activeTab` type to include `'kafka'`
- Added Kafka button to header navigation
- Rendered `KafkaTab` when active

## Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| Separate useKafkaStream hook | Distinct from REST API; manages WebSocket lifecycle |
| 5-second refresh interval | Matches status hub update frequency |
| 50-message buffer default | Balance between history visibility and memory |
| CSS in separate KafkaTab.css | Keeps styles modular, easier maintenance |

## Files Changed

| File | Change Type | Purpose |
|------|-------------|---------|
| src/dashboard/src/types/kafka.ts | Created | TypeScript interfaces matching backend DTOs |
| src/dashboard/src/hooks/useKafka.ts | Created | REST API hook for Kafka operations |
| src/dashboard/src/hooks/useKafkaStream.ts | Created | SignalR streaming hook |
| src/dashboard/src/components/KafkaTab.tsx | Created | Main Kafka tab component |
| src/dashboard/src/components/KafkaTab.css | Created | Component-specific styles |
| src/dashboard/src/App.tsx | Modified | Add Kafka tab to navigation |

## Commits

| Hash | Type | Description |
|------|------|-------------|
| fe87e65 | feat | Add Kafka TypeScript types |
| fb58ccf | feat | Add useKafka and useKafkaStream hooks |
| cd439b0 | feat | Add KafkaTab component and integrate into App |

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

### Prerequisites for 10-05
- Kafka tab visible in navigation
- Topics and consumer groups display correctly
- Health status shows when Kafka is running

### Integration Points
- useKafka hook ready for extended operations
- useKafkaStream ready for message viewer component
- KafkaTab structure ready for additional sections

### Testing Notes
- Requires Kafka deployment (10-01) to be running
- Backend API (10-02, 10-03) must be deployed
- Verify: Navigate to Kafka tab, see loading -> topics list
