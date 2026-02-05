# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-02-05

### Added

#### Control API (Phase 6)
- REST API for all simulator operations (`/api/*`)
- Connection info endpoint with multiple formats (JSON, env, YAML, .NET)
- Health check endpoints for all services (`/api/servers/{id}/health`)
- SignalR hub for real-time server status updates
- Server discovery with Kubernetes label selectors
- TCP-level health checks with 5-second timeout

#### Real-Time Dashboard (Phase 7)
- React 19 dashboard with Vite 6 build tooling
- Server status grid with live health indicators
- Custom `useSignalR` hook with automatic reconnection
- Responsive CSS Grid layout with mobile support
- Connection status indicator with retry backoff

#### File Operations (Phase 8)
- File browser with hierarchical tree view (react-arborist)
- Drag-and-drop file upload (react-dropzone, 100MB limit)
- Real-time file event streaming via SignalR
- FileSystemWatcher with 500ms debounce and 64KB buffer
- Batch file operations (delete, download)

#### Historical Metrics (Phase 9)
- SQLite embedded database for metrics persistence
- 5-minute sample collection for all servers
- Hourly rollup aggregation with P95 latency calculation
- 7-day data retention with automatic cleanup
- Time-series charts with Recharts library
- Server sparklines with click-to-history navigation

#### Kafka Integration (Phase 10)
- Kafka broker accessible at NodePort 30093
- ZooKeeper sidecar pattern for simplified lifecycle
- Topic management via API (create, list, delete)
- Message produce via REST API with idempotent producer
- Message consume via REST API and SignalR streaming
- Consumer group monitoring with lag indicators
- Live and manual message viewing modes

#### Dynamic Server Management (Phase 11)
- Create FTP servers on-demand via API/dashboard
- Create SFTP servers on-demand via API/dashboard
- Create NAS servers on-demand via API/dashboard
- Automatic Kubernetes deployment provisioning
- NodePort allocation in 31000-31999 range
- Server configuration import/export with conflict resolution
- Multi-select bulk operations with delete protection for Helm servers
- ConfigMap auto-update on server changes

#### Alerting System (Phase 12)
- Three severity levels: Info, Warning, Critical
- Disk space monitoring with configurable thresholds (1GB default)
- Server health alerts after 3 consecutive failures
- Kafka broker connectivity alerts
- Alert deduplication by type and source
- Sonner toast notifications with severity-based durations
- Sticky alert banner for critical issues
- Alert history with SQLite persistence and 7-day retention
- Filterable alerts table with pagination

#### Production Readiness (Phase 12)
- Multi-stage Docker build (27.3MB final image)
- nginx:alpine for static file serving with SPA routing
- Redis backplane option for multi-replica scale-out
- Error boundaries for tab-level error isolation
- NFS emptyDir fix integrated into Helm chart

#### Testing (Phase 13)
- TestConsole API-driven configuration with fallback
- `--api-url` and `--require-api` command-line flags
- Multi-NAS server testing (all 7 servers)
- Dynamic server lifecycle testing (`--dynamic` flag)
- Kafka integration tests
- Playwright E2E test suite for dashboard
- `Start-Simulator.ps1` for local development with `-Wait` flag
- `Run-E2ETests.ps1` orchestration script

### Changed
- TestConsole fetches configuration from Control API by default
- Enhanced error handling with structured validation errors (FluentValidation)
- Improved logging with structured logging throughout
- RBAC expanded for dynamic server deployment operations
- Dashboard deployed as separate container from Control API

### Fixed
- Kafka exception handling for external listeners
- Dashboard validation error display with field names
- Dynamic NAS server configuration export
- Reserved `$Host` variable conflict in Setup-Hosts.ps1

## [1.0.0] - 2026-02-01

### Added

#### Multi-NAS Topology
- 7 independent NAS servers matching production network configuration
  - 3 input servers (nas-input-1, nas-input-2, nas-input-3)
  - 1 backup server (nas-backup)
  - 3 output servers (nas-output-1, nas-output-2, nas-output-3)
- Unique DNS names for each server (e.g., `file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local`)
- Dedicated NodePorts per server (32049-32055)

#### Bidirectional Windows Sync
- Init container pattern for Windows-to-NFS synchronization
- Sidecar container for NFS-to-Windows synchronization (15-30s intervals)
- Proper lifecycle ordering prevents sync loops

#### Static PV/PVC Provisioning
- 14 pre-created PV/PVC manifests matching OCP deployment patterns
- Label selectors for explicit volume binding
- `hostPath` volumes with Windows mount integration

#### Protocol Servers (v1.0 baseline)
- FTP server (vsftpd) at NodePort 30021
- SFTP server (OpenSSH) at NodePort 30022
- HTTP/WebDAV server (nginx) at NodePort 30088
- S3/MinIO at NodePort 30900
- SMB server (Samba) at NodePort 30445
- NFS server at NodePort 32049

#### Infrastructure
- Helm chart with configurable values
- FileBrowser management UI at NodePort 30080
- Shared PVC with hostPath for Windows file access
- ServiceAccount with appropriate RBAC

#### Testing
- 57-test validation suite
- Health, isolation, and persistence tests
- PowerShell test scripts

#### Documentation
- NAS Integration Guide with copy-paste templates
- Architecture diagrams
- Deployment notes

### Fixed
- NFS export of Windows-mounted hostPath volumes (emptyDir workaround)

[2.0.0]: https://github.com/usercourses63/file-simulator-suite/compare/v1.0...v2.0.0
[1.0.0]: https://github.com/usercourses63/file-simulator-suite/releases/tag/v1.0
