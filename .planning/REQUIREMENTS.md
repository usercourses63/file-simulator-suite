# Requirements: File Simulator Suite

**Defined:** 2026-02-02
**Core Value:** Development systems must connect to simulated NAS servers using identical PV/PVC configurations as production OCP, with test files written on Windows immediately visible through NFS mounts - zero deployment differences between dev and prod.

## v2.0 Requirements: Simulator Control Platform

Requirements for real-time monitoring and control platform enabling observable, controllable testing infrastructure.

### Backend Infrastructure

- [ ] **BACK-01**: ASP.NET Core REST API for configuration and control operations
- [ ] **BACK-02**: SignalR WebSocket hub for real-time bi-directional communication
- [ ] **BACK-03**: SQLite database with EF Core for historical data persistence
- [ ] **BACK-04**: Health check endpoints for all protocol servers (FTP, SFTP, HTTP, S3, SMB, NAS, Kafka)
- [ ] **BACK-05**: FileSystemWatcher integration for Windows directory monitoring
- [ ] **BACK-06**: KubernetesClient integration for dynamic resource management
- [ ] **BACK-07**: RBAC ServiceAccount with permissions for deployment/service CRUD operations
- [ ] **BACK-08**: Configuration persistence across pod restarts

### Frontend Dashboard

- [ ] **DASH-01**: React 19 + Vite SPA with TypeScript
- [ ] **DASH-02**: SignalR client connection with reconnection logic
- [ ] **DASH-03**: Server health status grid showing all protocol servers
- [ ] **DASH-04**: Real-time event feed display (WebSocket-driven updates)
- [ ] **DASH-05**: File browser interface with directory navigation
- [ ] **DASH-06**: Usage metrics visualization (charts using Recharts)
- [ ] **DASH-07**: Historical data timeline (7-day retention display)
- [ ] **DASH-08**: Protocol-specific connection details panel

### Real-Time Monitoring

- [ ] **MON-01**: Protocol-specific health checks (TCP + application-level validation)
- [ ] **MON-02**: Real-time server status updates (5-second granularity)
- [ ] **MON-03**: Connection quality metrics (latency, success rate)
- [ ] **MON-04**: File event streaming (arrivals and departures)
- [ ] **MON-05**: Windows directory watcher with debouncing (prevent buffer overflow)
- [ ] **MON-06**: Protocol access logging (track which protocol accessed which files)
- [ ] **MON-07**: Multi-protocol event correlation (trace file journey)
- [ ] **MON-08**: Historical event retention (7 days in SQLite)

### File Operations

- [ ] **FILE-01**: Browse Windows directories through UI (C:\simulator-data hierarchy)
- [ ] **FILE-02**: Download files from any protocol server through browser
- [ ] **FILE-03**: Upload files to any protocol server through UI
- [ ] **FILE-04**: Delete files across protocols from dashboard
- [ ] **FILE-05**: File metadata display (size, modified date, permissions)
- [ ] **FILE-06**: Bulk file operations (multi-select support)
- [ ] **FILE-07**: Cross-protocol file tracking (show which protocols can see file)

### Kafka Integration

- [ ] **KAFKA-01**: Strimzi Kafka Operator deployment (single broker, KRaft mode)
- [ ] **KAFKA-02**: Minimal resource configuration (768Mi-1GB heap, no ZooKeeper)
- [ ] **KAFKA-03**: Topic creation through UI with partition/replication configuration
- [ ] **KAFKA-04**: Topic deletion through UI
- [ ] **KAFKA-05**: Topic listing with metadata (partitions, replication factor, retention)
- [ ] **KAFKA-06**: Basic pub/sub testing (produce messages to topics)
- [ ] **KAFKA-07**: Consume messages from topics (view message content)
- [ ] **KAFKA-08**: Consumer group monitoring (offsets, lag, members)
- [ ] **KAFKA-09**: Health checks for Kafka broker and topics

### Dynamic Control

- [ ] **CTRL-01**: Add new FTP server instance at runtime through UI
- [ ] **CTRL-02**: Add new SFTP server instance at runtime through UI
- [ ] **CTRL-03**: Add new NAS server instance at runtime through UI
- [ ] **CTRL-04**: Remove server instances dynamically (with orphaned resource cleanup)
- [ ] **CTRL-05**: Kubernetes ownerReferences for all dynamically created resources
- [ ] **CTRL-06**: ConfigMap updates for service discovery when servers added/removed
- [ ] **CTRL-07**: Server lifecycle management (start/stop/restart individual servers)
- [ ] **CTRL-08**: Resource quota enforcement (prevent runaway server creation)
- [ ] **CTRL-09**: Validation and guardrails (prevent invalid configurations)

### Configuration Management

- [ ] **CFG-01**: Export current simulator configuration to JSON file
- [ ] **CFG-02**: Import configuration from JSON file
- [ ] **CFG-03**: Configuration validation before import
- [ ] **CFG-04**: Configuration templates for common scenarios (basic, multi-nas, kafka-enabled)
- [ ] **CFG-05**: Version control friendly format (readable JSON with comments)
- [ ] **CFG-06**: Audit log of configuration changes (who, what, when)
- [ ] **CFG-07**: Team configuration sharing (export includes all servers + settings)

### Alerting & Notifications

- [ ] **ALERT-01**: Server health degradation alerts (connection failures, high latency)
- [ ] **ALERT-02**: File system alerts (disk space warnings for Windows directories)
- [ ] **ALERT-03**: Event volume anomaly detection (unusual file activity)
- [ ] **ALERT-04**: Kafka broker health alerts
- [ ] **ALERT-05**: Real-time notification display in dashboard (toast notifications)
- [ ] **ALERT-06**: Alert history log (what triggered, when resolved)

## Future Requirements (v2.1+)

Deferred to future releases. Tracked but not in current roadmap.

### Advanced Monitoring

- **MON-ADV-01**: Prometheus metrics export for production observability
- **MON-ADV-02**: Grafana dashboard templates
- **MON-ADV-03**: Distributed tracing integration (OpenTelemetry)

### Enhanced Control

- **CTRL-ADV-01**: Multi-pod control plane with Redis backplane
- **CTRL-ADV-02**: Batch server operations (add/remove multiple servers)
- **CTRL-ADV-03**: Server templates (preconfigured FTP, SFTP setups)

### Authentication

- **AUTH-01**: JWT-based authentication for API access
- **AUTH-02**: Role-based access control (read-only vs admin)
- **AUTH-03**: OAuth integration for team authentication

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Production-grade Kafka cluster | Testing simulator needs minimal resources (single broker sufficient) |
| Real-time sync between protocols | Intentional design - each protocol has independent view |
| Advanced Kafka features (Schema Registry, Kafka Connect) | Adds significant complexity, not needed for basic pub/sub testing |
| Multi-cluster management | Single Minikube cluster sufficient for development testing |
| Backup/restore for simulator data | Windows directories are source of truth, no separate backup needed |
| Performance optimization for large files | Development simulator, not production storage system |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| (To be filled by roadmapper) | | |

**Coverage:**
- v2.0 requirements: 52 total
- Mapped to phases: (pending)
- Unmapped: (pending)

---
*Requirements defined: 2026-02-02*
*Last updated: 2026-02-02 after initial definition*
