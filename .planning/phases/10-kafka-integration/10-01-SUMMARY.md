---
phase: 10
plan: 01
subsystem: messaging
tags: [kafka, zookeeper, helm, kubernetes]

dependency_graph:
  requires: [phase-6-control-api]
  provides: [kafka-broker, kafka-ui, default-topics]
  affects: [phase-10-02-backend-service, phase-10-03-dashboard]

tech_stack:
  added:
    - bitnami/kafka:3.7
    - bitnami/zookeeper:3.9
    - provectuslabs/kafka-ui:latest
  patterns:
    - sidecar-container
    - post-install-hook

key_files:
  created:
    - helm-chart/file-simulator/templates/kafka.yaml
    - helm-chart/file-simulator/templates/kafka-ui.yaml
  modified:
    - helm-chart/file-simulator/values.yaml

decisions:
  - id: zookeeper-sidecar
    summary: "ZooKeeper runs as sidecar in same pod as Kafka for simplified lifecycle"
    rationale: "Eliminates separate ZK cluster management, startup ordering handled via probes"

metrics:
  duration: 8 min
  completed: 2026-02-03
---

# Phase 10 Plan 01: Kafka Helm Infrastructure Summary

**One-liner:** Helm templates for Kafka+ZooKeeper sidecar deployment with Kafka-UI management interface

## What Was Built

### Task 1: Kafka Deployment with ZooKeeper Sidecar
Created `helm-chart/file-simulator/templates/kafka.yaml` with:
- Single pod containing both ZooKeeper and Kafka containers
- ZooKeeper sidecar on port 2181 with readiness probe (`echo "ruok" | nc localhost 2181 | grep imok`)
- Kafka broker waiting for ZooKeeper via startup probe before starting
- Environment configuration for ZooKeeper mode (`KAFKA_CFG_ZOOKEEPER_CONNECT=localhost:2181`)
- Dual listener setup: PLAINTEXT (internal 9092), EXTERNAL (NodePort 9094)
- Topic init job as post-install Helm hook creating 3 default topics

### Task 2: Kafka-UI Deployment
Created `helm-chart/file-simulator/templates/kafka-ui.yaml` with:
- Provectus Kafka-UI for power-user cluster management
- Bootstrap servers configured to Kafka service (`file-sim-file-simulator-kafka:9092`)
- Dynamic configuration enabled for runtime changes
- Health probes on `/actuator/health`

### Task 3: Values Configuration
Updated `helm-chart/file-simulator/values.yaml` with:
- `kafka` section: images, resource limits, default topics, retention policy
- `kafkaUi` section: image, service ports, resource limits
- Memory budget: ~1.5GB total (Kafka 768Mi-1536Mi + ZK 256Mi-512Mi + UI 256Mi-512Mi)

## Key Implementation Details

### Sidecar Pattern
```yaml
containers:
  - name: zookeeper    # Starts first, readiness probe validates
  - name: kafka        # Startup probe waits for localhost:2181
```

### NodePort Allocation
| Service | Port | NodePort |
|---------|------|----------|
| Kafka internal | 9092 | 30092 |
| Kafka external | 9094 | 30094 |
| Kafka-UI | 8080 | 30093 |

### Default Topics
| Topic | Partitions | Purpose |
|-------|------------|---------|
| test-events | 3 | High-throughput event streaming |
| test-commands | 1 | Ordered command processing |
| test-notifications | 1 | Alert/notification delivery |

## Verification Results

```
helm lint helm-chart/file-simulator
==> Linting helm-chart/file-simulator
[INFO] Chart.yaml: icon is recommended
1 chart(s) linted, 0 chart(s) failed
```

Template rendering verified:
- Kafka deployment with ZooKeeper sidecar container
- Kafka-UI deployment with correct bootstrap server
- Topic init job with all 3 topics
- Services with correct NodePorts

## Commits

| Hash | Type | Description |
|------|------|-------------|
| 0f069ed | feat | Kafka + ZooKeeper sidecar Helm template |
| 8d0715b | feat | Kafka-UI Helm template |
| 19f9d46 | feat | kafka and kafkaUi configuration in values.yaml |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed retention value scientific notation**
- **Found during:** Task 1 verification
- **Issue:** Helm rendered `86400000` as `8.64e+07` (scientific notation)
- **Fix:** Added `| int64` pipe to force integer rendering
- **Files modified:** kafka.yaml
- **Commit:** Included in 0f069ed

## Next Phase Readiness

**Ready for Plan 10-02:** Backend Kafka service implementation
- Kafka broker accessible at `file-sim-file-simulator-kafka:9092`
- Topics will be created by init job on deployment
- Kafka-UI available for manual topic management and testing

**Pre-deployment note:** User must increase Minikube to 12GB before deploying Kafka (per STATE.md blocker)
