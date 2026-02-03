---
phase: 10-kafka-integration
plan: 06
subsystem: dashboard
tags: [react, kafka, signalr, consumer-groups, messaging]
dependency-graph:
  requires: ["10-04", "10-05"]
  provides: ["MessageViewer with live/manual modes", "ConsumerGroupDetail with controls"]
  affects: []
tech-stack:
  added: []
  patterns: ["BEM CSS naming", "SignalR streaming toggle", "Expandable detail pattern"]
file-tracking:
  key-files:
    created:
      - src/dashboard/src/components/MessageViewer.tsx
      - src/dashboard/src/components/ConsumerGroupDetail.tsx
    modified:
      - src/dashboard/src/components/KafkaTab.tsx
      - src/dashboard/src/components/KafkaTab.css
decisions:
  - id: "produce-consume-toggle"
    choice: "Produce/Consume view toggle in center panel"
    why: "Single topic selection drives both producer and consumer views"
  - id: "live-manual-modes"
    choice: "Live (SignalR) and Manual (REST) message viewing modes"
    why: "Live for real-time monitoring, Manual for historical fetch"
  - id: "expandable-groups"
    choice: "Expandable consumer group detail pattern"
    why: "Conserves space while providing full partition-level detail on demand"
metrics:
  duration: "5 min"
  completed: "2026-02-03"
---

# Phase 10 Plan 06: Message Viewer and Consumer Group Monitoring Summary

**One-liner:** MessageViewer with live SignalR and manual REST modes, ConsumerGroupDetail with expandable partitions and offset reset/delete controls

## What Was Built

### 1. MessageViewer Component (Task 1)
Created `src/dashboard/src/components/MessageViewer.tsx`:
- **Live mode**: Streams messages via SignalR `useKafkaStream` hook
- **Manual mode**: Fetches messages via REST API with Refresh button
- Connection status indicator (Connected/Disconnected) for live mode
- Message display: partition:offset, optional key, value (pre-formatted), timestamp
- 50-message rolling buffer in live mode

### 2. ConsumerGroupDetail Component (Task 2)
Created `src/dashboard/src/components/ConsumerGroupDetail.tsx`:
- **Expandable header**: Shows groupId, state, member count, total lag
- **Members section**: Lists clientId and host for each member
- **Partitions table**: Topic, partition, current offset, high watermark, lag
- **Lag coloring**: Green (<=10), yellow (<=100), red (>100) at partition level
- **Reset offsets**: Only available for Empty state groups, topic/earliest|latest selection
- **Delete group**: Inline confirmation (Yes/No buttons)

### 3. KafkaTab Integration (Task 3)
Updated `src/dashboard/src/components/KafkaTab.tsx`:
- Added Produce/Consume view toggle in center panel
- Integrated MessageViewer when Consume mode selected
- Replaced simple consumer group list with ConsumerGroupDetail components
- Wired getGroupDetail, resetOffsets, deleteGroup from useKafka hook

### 4. CSS Styles (Task 3)
Extended `src/dashboard/src/components/KafkaTab.css`:
- View toggle button group styling
- Message viewer layout: header, controls, mode toggle, status, message list
- Message item styling: header row with offset/key/time, pre-formatted value
- Consumer group detail: expandable header, members list, partitions table
- Action buttons: reset form, delete confirmation, danger styling

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Produce/Consume toggle | View mode state in KafkaTab | Single topic selection drives both producer and consumer |
| Live/Manual modes | Toggle in MessageViewer header | Real-time monitoring vs historical fetch |
| Expandable groups | Click header to expand | Space-efficient with full detail on demand |
| Unicode arrows | `\u25BC` (down) / `\u25B6` (right) | Cross-platform expand/collapse indicators |

## Files Changed

| File | Change | Lines |
|------|--------|-------|
| MessageViewer.tsx | Created | +147 |
| ConsumerGroupDetail.tsx | Created | +261 |
| KafkaTab.tsx | Updated | +35/-20 |
| KafkaTab.css | Extended | +393 |

## Verification

```bash
npm run build --prefix src/dashboard
```

Build output:
- 904 modules transformed
- CSS: 34.61 kB (gzip 5.66 kB)
- JS: 868.57 kB (gzip 250.58 kB)
- Build time: 5.82s

## Success Criteria Status

- [x] MessageViewer switches between live streaming and manual refresh
- [x] Live mode shows connection status
- [x] Message list shows partition, offset, key, value, timestamp
- [x] Consumer group detail shows members and partition offsets
- [x] Reset offsets only available for Empty state groups
- [x] Delete requires confirmation
- [x] All lag values color-coded correctly

## Deviations from Plan

None - plan executed exactly as written.

## Integration Points

1. **MessageViewer -> useKafkaStream**: SignalR subscription for live messages
2. **MessageViewer -> useKafka.getMessages**: REST API for manual fetch
3. **ConsumerGroupDetail -> useKafka.getGroupDetail**: Fetch expanded group info
4. **ConsumerGroupDetail -> useKafka.resetOffsets**: Reset consumer offsets
5. **ConsumerGroupDetail -> useKafka.deleteGroup**: Delete consumer group

## Next Phase Readiness

Phase 10 Kafka Integration complete. All 6 plans executed:
- 10-01: Kafka infrastructure (ZooKeeper + Kafka pods)
- 10-02: Backend Kafka service (AdminClient, Producer)
- 10-03: SignalR hub for streaming messages
- 10-04: KafkaTab with TopicList, CreateTopicForm, MessageProducer
- 10-05: Topic management UI refinements
- 10-06: MessageViewer and ConsumerGroupDetail (this plan)

Ready for Phase 11: Dynamic Schema Registry.
