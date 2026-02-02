# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-02-02)

**Core value:** Development systems must connect to simulated NAS servers using identical PV/PVC configurations as production OCP, with test files written on Windows immediately visible through NFS mounts - zero deployment differences between dev and prod.

**Current focus:** Phase 7 - Real-Time Monitoring Dashboard

## Current Position

Phase: 7 of 12 (Real-Time Monitoring Dashboard)
Plan: 3 of TBD (in progress)
Status: In progress
Last activity: 2026-02-02 - Completed 07-03-PLAN.md (ServerDetailsPanel and Protocol Info)

Progress: [■■■■■■░░░░░░] 50% (6 of 12 phases complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 15
- Average duration: 8.9 min
- Total execution time: 2.25 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1. NFS Pattern Validation | 3 | 38 min | 12.7 min |
| 2. Multi-NAS Architecture | 3 | 42 min | 14.0 min |
| 3. Bidirectional Sync | 2 | 20 min | 10.0 min |
| 4. Static PV/PVC Provisioning | 2 | 18 min | 9.0 min |
| 5. Comprehensive Testing | 1 | 6 min | 6.0 min |
| 6. Backend API Foundation | 3 | 17 min | 5.7 min |
| 7. Real-Time Monitoring Dashboard | 2 | 5 min | 2.5 min |

**Recent Trend:**
- Last 5 plans: [6.0, 6.0, 5.4, 3.0, 2.0] min
- Trend: Accelerating (efficient reuse of established patterns)

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- v1.0: 7 NAS servers (3 input, 1 backup, 3 output) matches production network configuration
- v1.0: Init container + sidecar sync architecture prevents loops, proper lifecycle ordering
- v1.0: kubectl --context mandatory for multi-profile Minikube safety
- v2.0: Control plane deploys in same file-simulator namespace (simplified RBAC and service discovery)
- v2.0: SignalR built into ASP.NET Core (no separate WebSocket server needed)
- v2.0: SQLite embedded database (no separate container, dev-appropriate)
- v2.0: Increase Minikube to 12GB before Phase 10 (Kafka requires ~1.5-2GB)
- Phase 6-01: KubernetesClient 18.0.13 (upgraded from 13.0.1 to fix security vulnerability)
- Phase 6-01: Non-root container user (appuser:1000) for Kubernetes security best practices
- Phase 6-01: CORS allow any origin for Phase 7 dashboard development
- Phase 6-03: TCP-level health checks (5s timeout) instead of protocol-specific for simplicity
- Phase 6-03: 5-second SignalR broadcast interval for Phase 7 dashboard real-time updates
- Phase 6-03: Label selector app.kubernetes.io/part-of=file-simulator-suite for server discovery
- Phase 7-01: React 19 for latest hooks and automatic memoization benefits
- Phase 7-01: Vite 6 over CRA for 10x faster dev server and native ESM
- Phase 7-01: Custom useSignalR hook over third-party packages for full control
- Phase 7-01: Reconnection retry intervals [0, 2, 5, 10, 30] seconds for backoff
- Phase 7-03: Plain text credentials for dev environment convenience
- Phase 7-03: Dual connection strings (cluster internal + Minikube external)

### Pending Todos

None yet.

### Blockers/Concerns

**Phase 6-12 (v2.0 control platform):**
- Resource constraints: Current Minikube 8GB sufficient for phases 6-9, must increase to 12GB before phase 10 (Kafka)
- Integration risk: Each phase must validate v1.0 servers (7 NAS + 6 protocols) remain responsive
- FileSystemWatcher tuning: Windows + Minikube 9p mount buffer overflow threshold needs empirical testing in Phase 8
- Kafka memory allocation: Minimal JVM heap (512MB vs 768MB) needs profiling under development workload in Phase 10
- ownerReferences validation: Phase 11 dynamic resources must set controller references to prevent orphaned pods

## Session Continuity

Last session: 2026-02-02 15:05:42 UTC
Stopped at: Completed 07-03-PLAN.md
Resume file: None
