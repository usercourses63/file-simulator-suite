# Phase 10: Kafka Integration for Event Streaming - Context

**Gathered:** 2026-02-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Deploy a minimal Kafka simulator in Minikube for pub/sub testing. Includes topic management, message production/consumption testing, and consumer group monitoring through the React dashboard. This phase does NOT include Schema Registry, Kafka Connect, or stream processing.

</domain>

<decisions>
## Implementation Decisions

### Kafka Deployment
- ZooKeeper mode (traditional), not KRaft
- Single broker deployment (sufficient for dev testing)
- ZooKeeper as sidecar in same pod as Kafka (simpler lifecycle)
- hostPath volume for persistence (topics/messages survive restarts)
- 512MB JVM heap (total pod ~1GB with overhead)
- Include Kafka UI (provectus/kafka-ui) for additional management (~256MB)
- Pre-configured default topics: test-events, test-commands, test-notifications

### Claude's Discretion (Deployment)
- External access method (NodePort vs port-forward)
- Default topic retention period
- Specific ZooKeeper heap allocation

### Topic Management UI
- Topic list shows: name + partitions + message count + consumer groups + last activity
- Default topics created on startup for convenience

### Claude's Discretion (Topics)
- Dashboard location (new Kafka tab vs integrated)
- Topic creation form complexity (simple vs standard vs advanced)

### Message Testing Interface
- Form-based producer: text area for body, optional key field, send button
- No validation on message format (accept any string)
- Message viewer: both live stream AND manual refresh option
- Rolling buffer of last 50 messages

### Consumer Group Monitoring
- Group list shows: Group ID + member count + total lag + state
- Lag visualization: number with color coding (green 0-10, yellow 10-100, red 100+)
- Full control actions: reset offsets + delete groups + force rebalance
- 5-second refresh interval (matches existing dashboard patterns)
- Partition-level detail: expandable view (click to see per-partition offsets/lag)

</decisions>

<specifics>
## Specific Ideas

- Match existing dashboard patterns (5-second refresh, SignalR for real-time)
- Kafka UI provides backup/power-user interface alongside our dashboard integration
- Default topics follow naming convention: test-{domain}
- Consumer lag thresholds: 0-10 green, 10-100 yellow, 100+ red

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope

</deferred>

---

*Phase: 10-kafka-integration*
*Context gathered: 2026-02-03*
