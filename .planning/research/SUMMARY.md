# Project Research Summary

**Project:** File Simulator Suite - v2.0 Simulator Control Platform
**Domain:** Real-time monitoring and control platform for Kubernetes infrastructure testing
**Researched:** 2026-02-02
**Confidence:** HIGH

## Executive Summary

The File Simulator Suite v2.0 adds a comprehensive monitoring and control platform to the existing stable v1.0 infrastructure (7 NAS servers + 6 protocol servers). Research reveals that successful control platforms prioritize three capabilities: real-time observability via WebSocket streaming, self-service operations through golden paths, and comprehensive event tracking with audit trails. The recommended approach uses ASP.NET Core SignalR for real-time backend (native to existing .NET 9 stack), React + Vite for dashboard frontend, SQLite for embedded historical data, and minimal Kafka (single-broker Strimzi) for pub/sub testing.

The critical architectural decision is deploying the control plane in the same namespace as existing services (not separate cluster/namespace) to simplify RBAC and service discovery, while using resource quotas to prevent control platform features from starving existing FTP/SFTP/NAS servers. The stack minimizes new dependencies by leveraging built-in ASP.NET Core SignalR (no separate WebSocket server), embedded SQLite (no PostgreSQL container), and .NET KubernetesClient for dynamic resource management.

Key risks center on integration failures that break existing working infrastructure: WebSocket connection storms overwhelming existing servers during reconnection, Kafka's JVM memory consumption pushing Minikube over 8GB limit causing OOM kills, Windows FileSystemWatcher buffer overflows losing file events, and orphaned Kubernetes resources when ownerReferences not set properly. Mitigation requires careful resource planning (increase Minikube to 12GB if adding Kafka, configure minimal JVM heap 512MB), connection management patterns (exponential backoff, connection limits), and testing with v1.0 stability as success criterion.

## Key Findings

### Recommended Stack

ASP.NET Core 9.0 provides the foundation with built-in SignalR for real-time WebSocket communication, eliminating need for separate Node.js/Socket.IO server. React 19.x with Vite build tool delivers the fastest modern frontend development experience. SQLite via EF Core 10.0.2 provides embedded database (no separate container) perfect for development/testing simulators. Strimzi Kafka Operator (0.50.0) delivers minimal Kafka cluster in KRaft mode (no ZooKeeper) via standard Kubernetes patterns. .NET KubernetesClient (18.0.13) enables dynamic pod orchestration using official client library.

**Core technologies:**
- **ASP.NET Core SignalR 9.0**: Real-time WebSocket backend — built into existing .NET 9 stack, automatic connection management, hub pattern for broadcasting events
- **React 19.x + Vite 6.x**: Dashboard frontend UI — de facto standard for interactive dashboards, instant HMR, TypeScript native
- **EF Core + SQLite 10.0.2**: Historical data persistence — embedded database with zero configuration, no separate container, cross-platform
- **Strimzi Kafka 0.50.0**: Minimal Kafka for testing — Helm-based, single-broker mode, KRaft (no ZooKeeper), sufficient for pub/sub validation
- **KubernetesClient 18.0.13**: Dynamic orchestration — official .NET client, create/delete deployments at runtime, watch pod events

**Critical version notes:**
- All packages are 2025-2026 releases confirmed compatible with .NET 9/10 and React 19
- Avoid Create React App (deprecated 2023), use Vite
- Avoid ZooKeeper with Kafka (deprecated in 3.x+), use KRaft mode
- Avoid Redux Toolkit (overkill), use Zustand for client state + React Query for server state

### Expected Features

The platform serves three personas with distinct needs: developers debugging microservices ("Why isn't my app seeing the file?"), QA teams orchestrating test environments, and operations monitoring health. Research indicates that v2.0 must deliver observability (monitoring existing servers) and controllability (file operations, Kafka topics) as minimum viable offering, with advanced features like dynamic server management deferred until core is proven.

**Must have (table stakes):**
- Real-time monitoring dashboard with health status for all protocol servers — users need instant visibility into what's working
- Protocol connectivity checks (FTP, SFTP, HTTP, S3, SMB, NFS) — essential validation that servers are reachable
- File browser UI with upload/download/delete operations — core file manipulation for testing scenarios
- Configuration export/import (JSON format) — environment reproducibility for QA teams
- Basic Kafka simulator (single broker) — pub/sub testing capability
- File event tracking via Windows directory watching — debugging visibility into file system activity
- Audit logging for all operations — compliance and debugging requirement

**Should have (competitive):**
- Dynamic server management (add/remove FTP/SFTP at runtime) — self-service without kubectl commands
- Server templates library (common configurations) — quick setup for standard topologies
- Historical metrics dashboard (7-day retention) — trend analysis for performance debugging
- Event filtering and search — finding relevant events in high-volume streams
- Kafka consumer group monitoring — debugging stuck consumers
- Message browser for Kafka — inspect message content without external tools

**Defer (v2+):**
- Resource auto-scaling based on load — requires usage data to tune thresholds
- Drift detection and auto-remediation — complex feature, low initial ROI
- Test execution correlation (linking metrics to test runs) — requires integration with external test frameworks
- Anomaly detection with ML — requires 30+ days historical data to train models
- Multi-cluster management — developers typically run single local cluster

**Anti-features (deliberately avoid):**
- Built-in authentication/SSO — use Kubernetes RBAC for cluster access, basic auth for external exposure only
- Real-time cross-protocol sync — each protocol can intentionally have different files for testing isolation
- Production-grade Kafka cluster (3+ brokers) — over-engineering for testing, adds complexity and cost
- Complex RBAC within platform — testing platform needs simple, fast operations

### Architecture Approach

The recommended architecture extends the existing Helm umbrella chart pattern with a new control-plane subchart, deploying all components in the same file-simulator namespace for simplified networking and RBAC. The frontend layer (React SPA) communicates with backend API via REST for CRUD operations and SignalR WebSocket for real-time updates. The backend layer (ASP.NET Core) integrates Kubernetes client for dynamic resource management, FileSystemWatcher for Windows directory monitoring, and health check services for protocol connectivity validation. All components share the existing PVC (hostPath to C:\simulator-data) for file operations.

**Major components:**
1. **React Dashboard** (Nginx container) — User interface for monitoring and control with real-time updates via SignalR client hooks
2. **Control API** (ASP.NET Core) — Backend orchestrator with SignalR hub, REST endpoints, Kubernetes client integration, health check workers
3. **SignalR Hub** — Real-time bi-directional communication broadcasting health status, file events, metrics updates to all connected dashboards
4. **Kubernetes Service** — Dynamic resource management creating/deleting FTP/SFTP/NAS deployments at runtime via KubernetesClient library
5. **File Watcher** — Windows directory monitoring with FileSystemWatcher detecting file changes and streaming events via SignalR
6. **Time-Series DB** — SQLite embedded database (dev) or Prometheus (production) for historical metrics with 7-day retention
7. **Kafka Broker** — Strimzi-managed single-broker cluster for event streaming and pub/sub testing workflows

**Key patterns:**
- SignalR with Redis backplane for multi-pod scale-out (optional for single-pod dev)
- Kubernetes dynamic resource management with ownerReferences for garbage collection
- React hooks for SignalR connection management with automatic reconnection
- Time-series metrics with batched writes (reduce database load 10-100x)
- File watcher with debouncing (1s delay to avoid event floods)
- Umbrella Helm chart pattern (single release, atomic deployments, unified configuration)

### Critical Pitfalls

Research identified integration risks as highest priority — failures that break existing stable v1.0 infrastructure. The top pitfalls focus on resource exhaustion, state synchronization, and missing cleanup patterns.

1. **WebSocket connection storms during reconnection** — Multiple clients reconnecting simultaneously trigger full state synchronization, overwhelming backend with duplicate queries and starving existing FTP/SFTP servers of CPU/memory. Prevention: exponential backoff with jitter, incremental state sync (send only changes since last connection), connection admission control (max 50 concurrent), separate resource quotas for control plane.

2. **Orphaned Kubernetes resources without ownerReferences** — Dynamically created FTP/SFTP servers remain after control plane deletion, consuming resources and causing port conflicts. Prevention: ALWAYS call `controllerutil.SetControllerReference()` before creating resources, validate in integration tests, use labels for resource tracking, implement finalizers for custom cleanup.

3. **Windows FileSystemWatcher buffer overflow** — High-volume directories (1000+ files) exceed 8KB buffer causing InternalBufferOverflowException and lost events. Prevention: increase buffer to 64KB maximum, batch events with 100-200ms debounce, queue events to Channel for async processing, filter unnecessary events (ignore LastAccess, Security).

4. **Kafka consuming excessive memory in Minikube** — Single-broker Kafka with default 1GB heap plus off-heap memory consumes 1.5-2GB RAM, pushing Minikube over 8GB limit and causing OOM kills of existing services. Prevention: minimal JVM heap 512MB for dev, match container limits to heap, single partition/replica, configure `offsets.topic.replication.factor=1`, increase Minikube to 12GB if adding Kafka.

5. **React WebSocket state update race conditions** — Dashboard shows incorrect server state (FTP "running" when stopped) due to async updates overwriting with stale data. Prevention: fetch state then subscribe to WebSocket then refetch to catch gap events (3-step pattern), use functional state updates `setState(prev => merge(prev, update))`, sequence events with version numbers, implement event cache during initial load.

## Implications for Roadmap

Based on research, the recommended phase structure prioritizes foundation before features, with strict testing of v1.0 stability as success criterion at each phase. The control platform introduces 4 new pods (control API, dashboard, Kafka, Redis) plus dynamic resources, requiring careful resource management and validation that existing servers remain healthy.

### Phase 1: Backend API Foundation with SignalR
**Rationale:** Establish backend infrastructure and WebSocket patterns before building UI features. This phase validates that control plane can coexist with existing services without resource conflicts.

**Delivers:**
- ASP.NET Core Control API project with Dockerfile
- SignalR hub setup (without Redis for single-pod dev)
- Basic REST endpoints (list servers, health status)
- Kubernetes client integration (KubernetesClient NuGet)
- RBAC configuration (ServiceAccount, Role, RoleBinding)
- Helm subchart for control plane with resource limits
- Integration tests validating v1.0 servers remain healthy

**Addresses features:**
- Backend API with health check system (foundation for all features)
- Protocol connectivity checks (FTP, SFTP, HTTP, S3, SMB, NFS)

**Avoids pitfalls:**
- Pitfall #1 (connection storms): Implement connection management patterns from start
- Pitfall #5 (state races): Establish state synchronization patterns before UI complexity
- Pitfall #6 (RBAC): Configure permissions before attempting resource operations

**Resource planning:**
- Control API: 256Mi request, 1Gi limit, 200m CPU request, 1 CPU limit
- Deploy in file-simulator namespace, verify existing pods not evicted
- Test under load: 50 concurrent connections, existing servers remain responsive

**Complexity:** MEDIUM (Kubernetes client integration, RBAC configuration)
**Research flag:** NO — Standard ASP.NET Core + Kubernetes patterns are well-documented

---

### Phase 2: Real-Time Monitoring Dashboard
**Rationale:** Build on stable backend to deliver immediate value (visibility) before adding controllability. Dashboard validates SignalR integration and provides user feedback loop for subsequent features.

**Delivers:**
- React 19.x dashboard with Vite build
- SignalR client integration (custom useSignalR hook)
- Real-time health status display with connection state handling
- Metrics visualization (Recharts for line/pie charts)
- Nginx container for serving static files
- WebSocket connection management with exponential backoff

**Addresses features:**
- Real-time monitoring dashboard (WebSocket-based)
- Protocol connectivity checks (visualize health data from Phase 1)

**Uses stack:**
- React 19.x + Vite 6.x (frontend framework + build tool)
- @microsoft/signalr 9.0.0 (JavaScript client)
- recharts 3.6.0 (charting library)
- zustand 5.x (client state management)

**Implements architecture:**
- React Dashboard component with SignalR hooks
- Connection state handling (connecting/connected/disconnected)
- Real-time event streaming from backend to UI

**Avoids pitfalls:**
- Pitfall #7 (connection leaks): Implement cleanup function in useEffect from start
- Pitfall #5 (state races): Use 3-step pattern (fetch, subscribe, refetch) for initial load

**Resource planning:**
- Dashboard (Nginx): 64Mi request, 128Mi limit, 50m CPU
- Total new: 320Mi request, 1.1Gi limit (within 8GB Minikube)

**Complexity:** MEDIUM (WebSocket state management, real-time UI updates)
**Research flag:** NO — React + SignalR patterns are well-established

---

### Phase 3: File Operations and Event Streaming
**Rationale:** Add file manipulation capabilities (controllability) and Windows directory watching. This phase introduces FileSystemWatcher complexity and requires careful buffer management.

**Delivers:**
- File browser UI component (tree view with breadcrumbs)
- Upload/download/delete operations with progress indicators
- Windows FileSystemWatcher service with debouncing
- File event streaming via SignalR (created/modified/deleted)
- Event log panel in dashboard (recent changes)
- Audit logging for file operations

**Addresses features:**
- File browser UI with upload/download/delete
- File event tracking (Windows directory watching)
- Audit logging for all operations

**Uses stack:**
- System.IO.FileSystemWatcher (built-in .NET)
- SignalR for event streaming
- React file upload components

**Implements architecture:**
- FileWatcherService (debounced event emission)
- File API endpoints (CRUD operations)
- Event log store (Zustand) in dashboard

**Avoids pitfalls:**
- Pitfall #3 (FileSystemWatcher overflow): 64KB buffer, batching, debounce 1s, async queue
- Pitfall #9 (silent sidecar failure): Add liveness probe for file watcher, heartbeat metrics

**Testing requirements:**
- Stress test: create 1000 files in 10s, verify all events captured
- Verify InternalBufferOverflowException handled gracefully
- Confirm existing NAS servers continue serving files during high event volume

**Complexity:** MEDIUM-HIGH (FileSystemWatcher tuning, event buffering)
**Research flag:** MAYBE — FileSystemWatcher with Windows + Minikube 9p mount may need empirical testing for buffer tuning

---

### Phase 4: Historical Metrics and Storage
**Rationale:** Add time-series data persistence for trend analysis. SQLite provides embedded solution without additional container complexity.

**Delivers:**
- EF Core DbContext with Metric entity
- SQLite database with migrations
- Metrics collection background worker (batched writes)
- REST API for historical queries (last 24h, last 7d)
- Historical trends dashboard page (line charts)
- 7-day retention policy with auto-cleanup

**Addresses features:**
- Historical data retention (7-day minimum)
- Usage metrics (connection counts, bandwidth, errors)

**Uses stack:**
- Microsoft.EntityFrameworkCore.Sqlite 10.0.2
- Background worker with batched writes (100 metrics per flush, 30s interval)

**Implements architecture:**
- Time-Series DB component (SQLite embedded)
- MetricsService with batched writes pattern
- Background worker for periodic collection

**Avoids pitfalls:**
- Batched writes prevent database lock under load (>100 writes/sec)
- SQLite file on PVC for persistence across pod restarts

**Resource planning:**
- SQLite database file: ~100MB for 7 days of 5s granularity metrics
- PVC storage sufficient (10Gi existing allocation)

**Complexity:** LOW-MEDIUM (EF Core setup, batch write pattern)
**Research flag:** NO — EF Core + SQLite is well-documented, established pattern

---

### Phase 5: Kafka Integration for Event Streaming
**Rationale:** Add pub/sub testing capability. Kafka introduces highest resource cost and requires careful memory management. Deploy after core features proven stable.

**Delivers:**
- Strimzi Kafka operator deployment (Helm)
- Single-broker Kafka cluster (KRaft mode)
- Topic creation/deletion API
- Kafka producer in Control API (file events to topic)
- Kafka consumer for event replay
- Basic topic management UI

**Addresses features:**
- Basic Kafka simulator (single broker)
- Topic creation/deletion UI

**Uses stack:**
- Strimzi Kafka Operator 0.50.0
- Kafka 3.9+ (KRaft mode, no ZooKeeper)
- Confluent.Kafka .NET client

**Implements architecture:**
- Kafka Broker component (StatefulSet)
- Event streaming integration (file events → Kafka → consumers)

**Avoids pitfalls:**
- Pitfall #4 (Kafka memory): Minimal JVM heap 512MB, container limit 768Mi, single partition/replica, `offsets.topic.replication.factor=1`
- Pitfall #11 (disk retention): Set `retention.ms=86400000` (1 day) or `retention.bytes=1073741824` (1GB)

**Resource planning:**
- Kafka broker: 512Mi request, 768Mi limit, 250m CPU
- Strimzi operator: 128Mi request, 256Mi limit, 50m CPU
- **CRITICAL:** Increase Minikube to 12GB memory before this phase
- Combined total: ~2.2Gi new + 2.85Gi existing = 5Gi (within 12GB with headroom)

**Testing requirements:**
- Monitor `kubectl top pod` before/after Kafka deployment
- Verify existing FTP/SFTP/NAS servers not evicted
- Validate Kafka doesn't OOMKill in tight loop

**Complexity:** HIGH (JVM tuning, resource constraints, Strimzi operator)
**Research flag:** MAYBE — Kafka minimal resource allocation needs empirical validation in Minikube

---

### Phase 6: Dynamic Server Management
**Rationale:** Most complex feature requiring stable foundation. Enables self-service infrastructure without kubectl commands. Deploy last after all monitoring/control features proven.

**Delivers:**
- KubernetesService for creating/deleting Deployments + Services
- Server templates library (common FTP/SFTP/NAS configurations)
- Add/remove server UI flow with confirmation dialogs
- ownerReferences for garbage collection
- Safe operation guardrails (typing server name for destructive ops)
- Configuration export/import with Git-backed versioning

**Addresses features:**
- Dynamic server management (add/remove at runtime)
- Server templates library
- Configuration export/import (JSON)
- Safe operation guardrails

**Uses stack:**
- KubernetesClient 18.0.13 (.NET Kubernetes API client)
- RBAC with create/update/delete permissions

**Implements architecture:**
- Kubernetes Service component (dynamic resource management)
- Template library with best-practice configurations
- Golden paths pattern (curated templates make secure choice easiest)

**Avoids pitfalls:**
- Pitfall #2 (orphaned resources): ALWAYS set ownerReferences before creating resources, integration test validates cleanup
- Pitfall #6 (RBAC): Explicit Role with all needed verbs (get, list, watch, create, update, patch, delete)
- Pitfall #10 (config drift): Separate static (Helm) vs dynamic (control-plane) resources with labels, use `helm.sh/resource-policy: keep`

**Testing requirements:**
- Integration test: create server dynamically, delete parent, verify child deleted within 30s
- Verify `kubectl auth can-i create pods --as=system:serviceaccount:file-simulator:control-plane`
- Test NodePort allocation (prevent conflicts with existing servers)

**Complexity:** HIGH (Kubernetes API complexity, RBAC, resource lifecycle)
**Research flag:** NO — Official KubernetesClient documentation comprehensive, operator patterns established

---

### Phase 7: Production Readiness and Polish
**Rationale:** Final hardening for stability, error handling, monitoring, and documentation.

**Delivers:**
- Redis backplane for SignalR multi-pod scale-out
- Alert rules configuration (health check failures, buffer overflows)
- Comprehensive error boundaries in React
- User-friendly error messages (translate Kubernetes errors to user terms)
- End-to-end testing suite
- Documentation for Minikube setup, Helm deployment, troubleshooting

**Addresses:**
- Multi-pod scalability (Redis backplane)
- Alert rules and notifications
- Production logging and monitoring
- Deployment documentation

**Resource planning:**
- Redis: 128Mi request, 256Mi limit, 50m CPU
- Final total: ~5.5Gi within 12GB Minikube comfortably

**Complexity:** MEDIUM (Redis configuration, E2E testing, documentation)
**Research flag:** NO — Redis + SignalR backplane well-documented

---

### Phase Ordering Rationale

The roadmap follows dependency order and risk mitigation strategy:

1. **Backend foundation first** (Phase 1) enables iterative testing before UI complexity. Kubernetes client integration validates RBAC and resource management patterns before attempting dynamic operations.

2. **Monitoring before control** (Phase 2 before 3-6) delivers immediate value (visibility) and establishes feedback loop for development. Users see existing infrastructure health before adding new capabilities.

3. **File operations before Kafka** (Phase 3 before 5) prioritizes core file simulator features. FileSystemWatcher complexity isolated from Kafka resource constraints.

4. **Metrics after events** (Phase 4 after 3) builds on event streaming infrastructure. Time-series data storage requires understanding event patterns first.

5. **Kafka late in roadmap** (Phase 5) due to resource cost and complexity. Deploy after core features proven stable to avoid destabilizing v1.0.

6. **Dynamic management last** (Phase 6) as most complex feature requiring stable backend, UI, and Kubernetes integration. Depends on patterns established in earlier phases.

7. **Continuous validation** — Each phase tests that existing v1.0 FTP/SFTP/NAS servers remain healthy. v1.0 stability is success criterion, not just new feature functionality.

**Dependency chain:**
```
Phase 1 (Backend API) → enables → Phase 2 (Dashboard UI)
Phase 1 (Backend API) → enables → Phase 3 (File Operations)
Phase 3 (File Events) → enhances → Phase 2 (Dashboard)
Phase 2 (Dashboard) + Phase 1 (Backend) → enables → Phase 4 (Metrics)
Phase 1 (Backend) → enables → Phase 5 (Kafka) [independent track]
Phase 1 (Backend) + Phase 2 (Dashboard) → enables → Phase 6 (Dynamic Mgmt)
All phases → enables → Phase 7 (Production)
```

**Critical path:** Phase 1 → Phase 2 → Phase 3 → Phase 6 (monitoring + control features)
**Parallel track:** Phase 5 (Kafka) can develop in parallel with Phases 3-4 after Phase 1 complete

### Research Flags

Phases requiring deeper research or empirical validation during planning:

- **Phase 3 (File Operations):** MAYBE — FileSystemWatcher buffer overflow threshold with Windows + Minikube 9p mount needs empirical testing. Recommendation: Stress test with 1000+ files created in 10s burst, measure actual buffer usage and event loss patterns.

- **Phase 5 (Kafka Integration):** MAYBE — Kafka minimum viable resource allocation (512MB vs 768MB vs 1GB heap) needs profiling under development workload in Minikube. Recommendation: Deploy with conservative 512MB, monitor actual memory usage with `kubectl top pod`, adjust if OOMKilled.

Phases with standard patterns (skip research-phase):

- **Phase 1 (Backend API):** Well-documented ASP.NET Core + SignalR + KubernetesClient patterns, official Microsoft Learn documentation comprehensive
- **Phase 2 (Dashboard):** React 19 + SignalR client well-established, multiple production examples available
- **Phase 4 (Metrics):** EF Core + SQLite standard pattern, batched writes common optimization
- **Phase 6 (Dynamic Management):** Kubernetes operator patterns mature, ownerReferences well-documented in official guides
- **Phase 7 (Production):** Redis backplane for SignalR documented by Microsoft, E2E testing with Playwright standard practice

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | All technologies verified from official sources (Microsoft Learn, npm, NuGet), versions confirmed compatible with .NET 9/10 and React 19, released 2025-2026 |
| Features | HIGH | Based on current platform engineering trends, verified monitoring tools (Grafana, Confluent, AKHQ), extensive research on control plane patterns and real-time dashboards |
| Architecture | HIGH | SignalR + Kubernetes patterns validated in production systems (G-Research, multiple Medium articles), umbrella chart pattern official Helm best practice |
| Pitfalls | HIGH | Critical pitfalls (#1-#6) verified with authoritative sources (Microsoft Learn, Kubernetes docs) or production postmortems, resource constraints quantified against v1.0 baseline |

**Overall confidence:** HIGH

Research is comprehensive with authoritative sources for all major decisions. Stack choices align with existing .NET 9 infrastructure, minimizing new dependencies. Architecture patterns proven in production real-time systems. Pitfalls grounded in documented failures and official best practices.

### Gaps to Address

Areas requiring validation during implementation (not blockers, but need attention):

- **WebSocket connection limit threshold:** Exact limit before performance degradation varies by backend technology and hardware. Recommendation: Load test with artillery.io or similar tool, measure at 50/100/200 concurrent connections, establish baseline before deploying to users.

- **FileSystemWatcher buffer tuning:** Default 64KB buffer may need adjustment based on Windows + Minikube 9p mount performance characteristics. Recommendation: Start with 64KB, monitor for InternalBufferOverflowException, empirically determine optimal batch size (100-500ms debounce).

- **Kafka resource allocation:** 512MB heap is conservative; actual minimum may be lower or require adjustment. Recommendation: Profile Kafka under realistic development workload (10-20 topics, 100-500 messages/min), use `kubectl top pod` and JVM heap metrics to right-size.

- **React state management patterns:** Multiple concurrent WebSocket streams (health events + file events + metrics updates) may require optimization. Recommendation: Prototype with Zustand + React Query, measure render performance with React DevTools Profiler, optimize selective subscriptions if needed.

- **v1.0 regression testing:** Each phase must verify existing FTP/SFTP/NAS servers remain healthy. Recommendation: Establish baseline metrics (connection latency, file transfer throughput) before Phase 1, regression test after each phase deployment.

**Validation activities per phase:**
- Phase 1: Load test 50 concurrent WebSocket connections
- Phase 2: WebSocket reconnection storm test (disconnect 10 clients, reconnect simultaneously)
- Phase 3: Stress test FileSystemWatcher with 1000+ files in 10s burst
- Phase 4: Validate batched writes prevent SQLite lock under load
- Phase 5: Profile Kafka memory usage under dev workload
- Phase 6: Integration test dynamic resource cleanup (ownerReferences)
- Phase 7: End-to-end test all features with v1.0 servers active

## Sources

### Primary (HIGH confidence)

**Stack Research:**
- Microsoft Learn: ASP.NET Core SignalR production hosting and scaling
- Strimzi.io: Official Apache Kafka on Kubernetes documentation
- Kubernetes Client GitHub: Official C# client repository and documentation
- Microsoft Learn: FileSystemWatcher class and monitoring best practices
- React official documentation: React 19 + TypeScript setup guides
- Vite official documentation: Build tool configuration

**Features Research:**
- Grafana.com: Real-time monitoring dashboard patterns
- Confluent: Kafka management and control center features
- AKHQ GitHub: Open-source Kafka UI capabilities
- BrowserStack: Testing platform monitoring dashboard best practices
- Platform Engineering Org: 2026 observability tools evaluation
- CNCF Blog: Platform control trends and autonomous enterprise forecast

**Architecture Research:**
- Medium (Mahdi Karimipour): Scalable real-time messaging with SignalR, React, .NET, Kubernetes
- Microsoft Learn: SignalR Redis backplane configuration
- Strimzi Quickstarts: Single-broker Kafka deployment guides
- GitHub kubernetes-client/csharp: Official examples for dynamic resource management
- Helm official documentation: Umbrella chart best practices

**Pitfalls Research:**
- Medium (Voodoo Engineering): WebSockets on production with Node.js (40k+ connections)
- Kubernetes Official Docs: Garbage collection and ownerReferences
- Microsoft Learn: FileSystemWatcher buffer overflow handling
- Confluent Documentation: Kafka memory configuration for Kubernetes
- React community blogs: State update race conditions in real-time apps

### Secondary (MEDIUM confidence)

- Medium articles on production WebSocket scaling (not official but multiple sources align)
- Community Kafka deployment guides (verified against Strimzi official docs)
- React state management comparisons (Zustand vs Redux ecosystem consensus)
- Helm chart refactoring patterns (verified against Codefresh best practices)

### Tertiary (LOW confidence)

- None — all critical decisions backed by official documentation or multiple aligned sources

**Source quality verification:**
- All stack versions verified on official package registries (NuGet, npm)
- Architecture patterns cross-referenced with Microsoft Learn + production case studies
- Pitfalls validated against official Kubernetes/ASP.NET Core documentation
- Feature requirements based on 2026 platform engineering trends from CNCF + industry leaders

---
*Research completed: 2026-02-02*
*Ready for roadmap: yes*
