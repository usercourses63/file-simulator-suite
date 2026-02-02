# Roadmap: File Simulator Suite

## Milestones

- ✅ **v1.0 Multi-NAS Production Topology** - Phases 1-5 (shipped 2026-02-01)
- 🚧 **v2.0 Simulator Control Platform** - Phases 6-12 (in progress)

## Phases

<details>
<summary>✅ v1.0 Multi-NAS Production Topology (Phases 1-5) - SHIPPED 2026-02-01</summary>

### Phase 1: NFS Pattern Validation
**Goal**: Validate init container + unfs3 pattern for exposing Windows directories via NFS
**Plans**: 3 plans

Plans:
- [x] 01-01: Initial NFS architecture research and proof of concept
- [x] 01-02: Windows directory sync mechanism implementation
- [x] 01-03: Basic health checks and validation testing

### Phase 2: Multi-NAS Architecture
**Goal**: Deploy 7 independent NAS servers matching production topology
**Plans**: 3 plans

Plans:
- [x] 02-01: Helm chart refactoring for multi-instance support
- [x] 02-02: ConfigMap service discovery implementation
- [x] 02-03: Server isolation and validation testing

### Phase 3: Bidirectional Sync
**Goal**: Implement bidirectional sync (Windows→NFS + NFS→Windows)
**Plans**: 2 plans

Plans:
- [x] 03-01: Init container sync (Windows→NFS)
- [x] 03-02: Sidecar sync (NFS→Windows) with selective deployment

### Phase 4: Static PV/PVC Provisioning
**Goal**: Deliver production-matching static PV/PVC manifests
**Plans**: 2 plans

Plans:
- [x] 04-01: Static PV/PVC template creation
- [x] 04-02: Multi-NAS mount example and documentation

### Phase 5: Comprehensive Testing
**Goal**: Validate all 7 servers with comprehensive test suite
**Plans**: 1 plan

Plans:
- [x] 05-01: 57-test suite covering health, sync, isolation, persistence

</details>

### 🚧 v2.0 Simulator Control Platform (In Progress)

**Milestone Goal:** Transform the simulator into an observable, controllable platform with real-time monitoring, dynamic server management, and Kafka integration - enabling self-service test environment orchestration.

#### Phase 6: Backend API Foundation
**Goal**: Establish backend control plane with SignalR WebSocket support and Kubernetes integration
**Depends on**: Phase 5 (v1.0 stable infrastructure)
**Requirements**: BACK-01, BACK-02, BACK-03, BACK-06, BACK-07
**Success Criteria** (what must be TRUE):
  1. Control API pod deploys successfully in file-simulator namespace without evicting existing services
  2. SignalR hub accepts WebSocket connections and broadcasts health status updates
  3. KubernetesClient can list all existing protocol servers (FTP, SFTP, NAS) via K8s API
  4. RBAC ServiceAccount has verified permissions to read pods/deployments/services
  5. v1.0 servers (7 NAS + 6 protocols) remain fully operational with no performance degradation
**Plans**: 3 plans

Plans:
- [ ] 06-01-PLAN.md — ASP.NET Core project with SignalR and Dockerfile
- [ ] 06-02-PLAN.md — Kubernetes RBAC and Helm deployment templates
- [ ] 06-03-PLAN.md — SignalR hub, K8s discovery, health checks, and status broadcasting

#### Phase 7: Real-Time Monitoring Dashboard
**Goal**: Deliver React dashboard with real-time health monitoring and protocol connectivity visualization
**Depends on**: Phase 6 (backend API operational)
**Requirements**: DASH-01, DASH-02, DASH-03, DASH-04, DASH-08, MON-01, MON-02, MON-03, BACK-04
**Success Criteria** (what must be TRUE):
  1. Dashboard displays health status for all 13 servers (7 NAS + 6 protocols) updated every 5 seconds
  2. User can see real-time server status changes (stopped → running → healthy) without page refresh
  3. Connection quality metrics show latency and success rate for each protocol
  4. Dashboard reconnects automatically when WebSocket disconnects and displays accurate state
  5. Protocol-specific details panel shows FTP/SFTP/HTTP/S3/SMB/NFS connection information
**Plans**: 4 plans

Plans:
- [ ] 07-01-PLAN.md — Vite React TypeScript project with SignalR hook and types
- [ ] 07-02-PLAN.md — Core dashboard components (ConnectionStatus, SummaryHeader, ServerCard, ServerGrid)
- [ ] 07-03-PLAN.md — Protocol details panel with connection info and copy-to-clipboard
- [ ] 07-04-PLAN.md — Complete CSS styling and human verification checkpoint

#### Phase 8: File Operations and Event Streaming
**Goal**: Enable file operations through UI and implement Windows directory event tracking
**Depends on**: Phase 7 (dashboard operational)
**Requirements**: FILE-01, FILE-02, FILE-03, FILE-04, FILE-05, FILE-06, FILE-07, MON-04, MON-05, MON-06, MON-07, BACK-05
**Success Criteria** (what must be TRUE):
  1. User can browse Windows C:\simulator-data directory hierarchy through dashboard UI
  2. User can upload files via browser to any protocol server and file appears in Windows directory
  3. User can download files from any protocol server through browser
  4. User can delete files across all protocols from dashboard with confirmation dialog
  5. File event feed shows real-time arrivals/departures when files created/modified/deleted in Windows
  6. Multi-protocol tracking shows which servers can see each file (e.g., "Visible via: FTP, SFTP, NAS-input-1")
**Plans**: TBD

Plans:
- [ ] 08-01: [TBD during planning]
- [ ] 08-02: [TBD during planning]
- [ ] 08-03: [TBD during planning]

#### Phase 9: Historical Metrics and Storage
**Goal**: Add time-series data persistence with 7-day retention and historical trend visualization
**Depends on**: Phase 7 (monitoring data collection established)
**Requirements**: BACK-03, DASH-06, DASH-07, MON-08, ALERT-06
**Success Criteria** (what must be TRUE):
  1. SQLite database persists health metrics with 5-second granularity for 7 days
  2. Historical trends dashboard shows connection counts, latency, and errors over time
  3. User can query metrics for specific time ranges (last 1h, last 24h, last 7d)
  4. Database survives pod restarts with data intact
  5. Auto-cleanup removes data older than 7 days to prevent unbounded growth
**Plans**: TBD

Plans:
- [ ] 09-01: [TBD during planning]
- [ ] 09-02: [TBD during planning]

#### Phase 10: Kafka Integration for Event Streaming
**Goal**: Deploy minimal Kafka simulator for pub/sub testing with topic management
**Depends on**: Phase 6 (backend API for Kafka integration)
**Requirements**: KAFKA-01, KAFKA-02, KAFKA-03, KAFKA-04, KAFKA-05, KAFKA-06, KAFKA-07, KAFKA-08, KAFKA-09
**Success Criteria** (what must be TRUE):
  1. Single-broker Kafka cluster deploys successfully in KRaft mode (no ZooKeeper)
  2. User can create topics through UI with specified partition count and replication factor
  3. User can produce test messages to topics via dashboard
  4. User can consume messages from topics and view message content in UI
  5. Consumer group monitoring shows offset lag and active members
  6. Kafka broker health check shows green status and existing v1.0 servers remain responsive
**Plans**: TBD

Plans:
- [ ] 10-01: [TBD during planning]
- [ ] 10-02: [TBD during planning]
- [ ] 10-03: [TBD during planning]

#### Phase 11: Dynamic Server Management
**Goal**: Enable runtime addition/removal of FTP, SFTP, and NAS servers with configuration management
**Depends on**: Phase 6 (Kubernetes client integration), Phase 7 (UI foundation)
**Requirements**: CTRL-01, CTRL-02, CTRL-03, CTRL-04, CTRL-05, CTRL-06, CTRL-07, CTRL-08, CTRL-09, CFG-01, CFG-02, CFG-03, CFG-04, CFG-05, CFG-06, CFG-07
**Success Criteria** (what must be TRUE):
  1. User can add new FTP server through UI and it deploys within 30 seconds with unique NodePort
  2. User can add new SFTP server through UI with custom configuration
  3. User can add new NAS server through UI and it appears in ConfigMap service discovery
  4. User can remove server through UI and all resources (deployment, service, PVC) are deleted within 60 seconds
  5. All dynamically created resources have ownerReferences pointing to control plane pod
  6. User can export current configuration to JSON file downloadable from browser
  7. User can import configuration JSON and simulator recreates all servers automatically
  8. ConfigMap updates when servers added/removed so applications can discover new endpoints
**Plans**: TBD

Plans:
- [ ] 11-01: [TBD during planning]
- [ ] 11-02: [TBD during planning]
- [ ] 11-03: [TBD during planning]

#### Phase 12: Alerting and Production Readiness
**Goal**: Implement alerting system, Redis backplane for scale-out, and production hardening
**Depends on**: All previous phases (complete platform)
**Requirements**: ALERT-01, ALERT-02, ALERT-03, ALERT-04, ALERT-05, BACK-08
**Success Criteria** (what must be TRUE):
  1. Server health degradation triggers real-time toast notification in dashboard
  2. File system alerts warn when Windows directory space drops below 1GB
  3. Kafka broker health alerts show when broker becomes unavailable
  4. Alert history log shows all triggered alerts with timestamps and resolution status
  5. Redis backplane enables multiple control plane pods to broadcast to all dashboards
  6. Configuration persists across pod restarts (survives delete + redeploy)
  7. Comprehensive error boundaries prevent single component failure from crashing entire dashboard
**Plans**: TBD

Plans:
- [ ] 12-01: [TBD during planning]
- [ ] 12-02: [TBD during planning]

## Progress

**Execution Order:**
v2.0 phases execute sequentially: 6 → 7 → 8 → 9 → 10 → 11 → 12

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. NFS Pattern Validation | v1.0 | 3/3 | Complete | 2026-01-29 |
| 2. Multi-NAS Architecture | v1.0 | 3/3 | Complete | 2026-01-30 |
| 3. Bidirectional Sync | v1.0 | 2/2 | Complete | 2026-01-31 |
| 4. Static PV/PVC Provisioning | v1.0 | 2/2 | Complete | 2026-02-01 |
| 5. Comprehensive Testing | v1.0 | 1/1 | Complete | 2026-02-01 |
| 6. Backend API Foundation | v2.0 | 3/3 | Complete | 2026-02-02 |
| 7. Real-Time Monitoring Dashboard | v2.0 | 4/4 | Complete | 2026-02-02 |
| 8. File Operations and Event Streaming | v2.0 | 0/TBD | Not started | - |
| 9. Historical Metrics and Storage | v2.0 | 0/TBD | Not started | - |
| 10. Kafka Integration for Event Streaming | v2.0 | 0/TBD | Not started | - |
| 11. Dynamic Server Management | v2.0 | 0/TBD | Not started | - |
| 12. Alerting and Production Readiness | v2.0 | 0/TBD | Not started | - |

---
*Last updated: 2026-02-02 after Phase 7 completion*
