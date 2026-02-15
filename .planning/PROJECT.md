# File Simulator Suite - Multi-NAS Production Topology

## What This Is

A Kubernetes-based file protocol simulator with real-time monitoring and control platform. Replicates production OCP network topology (7 independent NAS servers, Kafka cluster, FTP/SFTP/HTTP/S3/SMB) that expose Windows directories, enabling systems under development to access test files through production-like PV/PVC mounts. Includes React-based dashboard for real-time monitoring, dynamic server management, and configuration control.

## Core Value

Development systems must connect to simulated NAS servers using identical PV/PVC configurations as production OCP, with test files written on Windows immediately visible through NFS mounts - zero deployment differences between dev and prod.

## Current State

**Shipped version:** v2.3.0 (2026-02-12) — Dashboard UI refactoring: protocol colors, NAS compact table, wider layout
**Previous:** v2.2.0 Activity Log Completeness (2026-02-12), v2.0 Simulator Control Platform (2026-02-05)

**What shipped:**
- React 19 dashboard with real-time monitoring via SignalR (5 hubs)
- File operations UI with FileSystemWatcher event streaming
- SQLite metrics persistence with 7-day retention and Recharts visualization
- Kafka broker + Zookeeper with topic management and produce/consume
- Dynamic FTP/SFTP/NAS server creation at runtime with ownerReferences
- Alerting system with health/disk/Kafka alerts and toast notifications
- 131+ integration tests covering all protocols, dynamic servers, and Kafka

**Tech stack:**
- Backend: ASP.NET Core 9, SignalR, EF Core + SQLite, Confluent.Kafka
- Frontend: React 19, Vite, TypeScript, Recharts
- Infrastructure: Kubernetes, Helm, Docker
- Testing: xUnit, FluentAssertions, JUnit XML

**Lines of code:** ~60,600 (C# + TypeScript + Tests)

## Requirements

### Validated

v2.3.0 Dashboard UI Refactoring (Shipped: 2026-02-12):

- ✓ Protocol-tinted card backgrounds with unified color system (7 protocols) — v2.3.0
- ✓ Static/Dynamic server badges (Helm crosshair, Dynamic lightning bolt) — v2.3.0
- ✓ Health state background tints (red down, yellow degraded) — v2.3.0
- ✓ Full-width layout with collapsible activity sidebar — v2.3.0
- ✓ NAS compact table view with Input/Output/Backup grouping — v2.3.0
- ✓ Expandable NAS rows with sparkline charts — v2.3.0
- ✓ Details panel works from both protocol cards and NAS rows — v2.3.0
- ✓ History tab filtered to deployed servers with data-aware legend — v2.3.0
- ✓ Files/Kafka/Alerts layout improvements (wider sidebars, flexible columns) — v2.3.0
- ✓ 4 new E2E tests validating UI changes, 54 total tests passing — v2.3.0

v2.2.0 Activity Log Completeness (Shipped: 2026-02-12):

- ✓ Complete activity log for dynamic server lifecycle events — v2.2.0
- ✓ File rename API endpoint (`PUT /api/files/rename`) — v2.2.0
- ✓ Download endpoint emits Read file events via SignalR — v2.2.0
- ✓ Protocol-based server attribution fallback — v2.2.0
- ✓ E2E tests for all server types and file operations — v2.2.0
- ✓ Fresh Minikube install validation with updated defaults — v2.2.0

v2.0 Simulator Control Platform (Shipped: 2026-02-05):

- ✓ React 19 monitoring dashboard with real-time updates via SignalR — v2.0
- ✓ ASP.NET Core backend API with 5 SignalR WebSocket hubs — v2.0
- ✓ Health/connectivity monitoring for all 13+ protocol servers — v2.0
- ✓ File event streaming via FileSystemWatcher with 500ms debouncing — v2.0
- ✓ File browser for Windows directories with upload/download/delete — v2.0
- ✓ SQLite metrics with 7-day retention and Recharts visualization — v2.0
- ✓ Alerting system with health/disk/Kafka alerts and toast notifications — v2.0
- ✓ Dynamic FTP/SFTP/NAS server management with ownerReferences — v2.0
- ✓ Kafka broker + Zookeeper with topic management — v2.0
- ✓ Kafka produce/consume and consumer group monitoring — v2.0
- ✓ Configuration import/export with validation — v2.0
- ✓ 131+ integration tests with JUnit XML export — v2.0

v1.0 Multi-NAS Production Topology (Shipped: 2026-02-01):

- ✓ 7 independent NAS servers (3 input, 1 backup, 3 output) with unique DNS names — v1.0
- ✓ Each NAS exports Windows directory via NFS with init container sync pattern — v1.0
- ✓ Bidirectional sync: Windows→NFS (init) + NFS→Windows (sidecar, 15-30s) — v1.0
- ✓ Static PV/PVC provisioning matching production OCP patterns — v1.0
- ✓ ConfigMap service discovery for all 7 NAS servers — v1.0
- ✓ Multi-NAS mount example (6 servers simultaneously) — v1.0
- ✓ Comprehensive test suite (57 tests: health, isolation, persistence) — v1.0
- ✓ Windows directory automation via enhanced setup-windows.ps1 — v1.0
- ✓ 1200+ line integration guide (NAS-INTEGRATION-GUIDE.md) — v1.0

Existing capabilities (pre-v1.0):

- ✓ Single NFS server deployment via Helm chart — existing
- ✓ FTP, SFTP, HTTP, WebDAV, S3, SMB protocol servers operational — existing
- ✓ Shared PVC storage at /mnt/simulator-data from Windows mount — existing
- ✓ Management UI (FileBrowser) for file browsing — existing
- ✓ Multi-protocol file access tested and working — existing
- ✓ Kubernetes deployment with Hyper-V Minikube driver — existing
- ✓ Cross-cluster access via NodePort services — existing

### Active

(No active milestone — use `/gsd:new-milestone` to start next)

### Out of Scope

- Real-time sync between protocols (intentional - each protocol can have different files)
- Single NAS with multiple exports (production uses multiple physical devices)
- NFS performance optimization (development simulator, not production storage)

## Context

**Production Environment:**
- OCP (OpenShift Container Platform) with multiple physical NAS devices
- System connects to different NAS servers for input/output/backup via NFS
- Configuration specifies which NAS for which purpose

**Development Environment:**
- Minikube (Hyper-V driver) on Windows
- System under development runs in Kubernetes cluster
- Test suite runs on Windows, writes test files to directories
- Same PV/PVC configuration as production must work in dev

**Current NFS Limitation:**
- NFS server crashes when trying to export Windows-mounted hostPath
- Current workaround uses emptyDir (isolates from Windows - breaks dev/prod parity)
- Need solution that exposes Windows directories via NFS exports

**Known Working Patterns:**
- FTP, SFTP, SMB successfully expose Windows-mounted directories
- Kubernetes hostPath PVC at /mnt/simulator-data works for other protocols
- Multiple protocol servers can share same PVC successfully

## Constraints

- **Platform**: Minikube with Hyper-V driver (existing, working)
- **Storage**: Windows directories at C:\simulator-data must be source of truth
- **Production Parity**: NFS configuration in dev must match production exactly
- **No Data Loss**: Windows files persist across pod restarts
- **Filesystem Limitation**: NFS cannot directly export Windows CIFS/9p mounted filesystems (known Linux kernel limitation)

## Technical Context

**Platform:** Minikube with Hyper-V driver
**Storage:** Windows directories at C:\simulator-data as source of truth
**Production parity:** NFS configuration in dev matches production OCP exactly

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Multiple NFS servers vs single with exports | Production has multiple physical NAS devices; dev must match topology | ✓ Good - 7 servers deployed, validated in Phase 2 |
| 7 total NAS servers (3 input, 1 backup, 3 output) | Matches production network configuration | ✓ Good - Topology matches OCP architecture |
| Windows directories as source of truth | Testers work on Windows; test files must be accessible via NFS | ✓ Good - Bidirectional sync working (15-30s latency) |
| unfs3 vs kernel NFS | Kernel NFS cannot export Windows mounts; unfs3 userspace workaround | ✓ Good - Pattern validated in Phase 1 |
| Init container + sidecar sync architecture | Separate one-way syncs prevent loops; native sidecar for lifecycle | ✓ Good - No sync loops, proper ordering |
| Static PV/PVC provisioning | Matches production OCP patterns better than dynamic provisioning | ✓ Good - Label selector binding reliable |
| Selective sidecar deployment | Only output servers need NFS→Windows sync; avoid overhead on inputs | ✓ Good - Resource efficient (96Mi vs 128Mi) |
| kubectl --context mandatory | Multi-profile Minikube safety; prevent cross-cluster accidents | ✓ Good - Zero accidental deletions in v1.0 |

| CSS custom properties for protocol colors | Single source of truth for 7 protocol tints (0.06 alpha) | ✓ Good - Consistent theming across cards, badges, table rows |
| CSS grid rows for NasTable | BEM-styled grid rows vs HTML table for compact NAS layout | ✓ Good - Flexible, maintainable, consistent with codebase |
| Verification-only Phase 23 | Phase 22 already wired NAS row click to details panel | ✓ Good - Avoided unnecessary code changes |
| minikube image load over registry push | Minikube registry addon unavailable | ✓ Good - Simpler workflow, no port-forwarding needed |
| File reads emit events from API layer | FileWatcher only detects filesystem mutations; downloads need explicit SignalR broadcast from FilesController | ✓ Good |
| New rename API endpoint | `PUT /api/files/rename` calls `File.Move()` → triggers existing FileSystemWatcher.OnRenamed | ✓ Good |
| Protocol-based server attribution | Fallback when file path doesn't contain server name; uses `fe.protocols` array | ✓ Good |
| Version 2.2.0 (not 2.1.1) | New API endpoint + new event type = minor version bump | ✓ Good |

---
*Last updated: 2026-02-15 after v2.3.0 milestone completed*
