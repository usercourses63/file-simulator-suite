# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-01-29)

**Core value:** Dev systems must use identical PV/PVC configs as production OCP, with Windows test files visible via NFS.
**Current focus:** Phase 2: 7-Server Topology

## Current Position

Phase: 2 of 5 (7-Server Topology)
Plan: 2 of 3 (Deploy and Test 7 NAS Servers)
Status: Complete
Last activity: 2026-01-29 — Completed 02-02-PLAN.md

Progress: [████░░░░░░] 40%

## Performance Metrics

**Velocity:**
- Total plans completed: 4
- Average duration: 12.1 minutes
- Total execution time: 0.80 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-single-nas-validation | 2 | 34.4min | 17.2min |
| 02-7-server-topology | 2 | 13.5min | 6.8min |

**Recent Trend:**
- Last 5 plans: 01-01 (2.4min), 01-02 (32min), 02-01 (4.5min), 02-02 (9min)
- Trend: Phase 2 consistently faster than Phase 1; deployment/validation taking ~2x template creation

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Multiple NFS servers vs single with exports: Production has multiple physical NAS devices; dev must match topology (Pending)
- 7 total NAS servers (3 input, 1 backup, 3 output): Matches production network configuration (Pending)
- Windows directories as source of truth: Testers work on Windows; test files must be accessible via NFS (Pending)
- **[01-01] unfs3 vs kernel NFS:** Use unfs3 userspace NFS server because kernel NFS cannot export Windows mounts (Implemented)
- **[01-01] Init container sync pattern:** Use init container with rsync to copy Windows hostPath → emptyDir before NFS export (Implemented)
- **[01-01] NET_BIND_SERVICE capability:** Use minimal capability for port 2049 instead of privileged mode (Implemented)
- **[01-01] Disk-backed emptyDir:** Use default disk-backed (not memory) for file persistence across pod restarts (Implemented)
- **[01-02] Defer rpcbind to Phase 2:** Use -p (no portmap) mode for Phase 1; rpcbind caused CrashLoopBackOff, full NFS client mount not critical for pattern proof (Implemented)
- **[01-02] kubectl exec validation:** Accept kubectl exec as sufficient for Phase 1 instead of external NFS mount; validates critical path (Implemented)
- **[02-01] Range loop pattern from ftp-multi.yaml:** Use Helm range loop with $ root scope for multi-instance NAS deployments (Implemented)
- **[02-01] Unique fsid per server (1-7):** NAS-07 requirement for NFS filesystem identification, logged in container startup (Implemented)
- **[02-01] Per-server exportOptions:** EXP-05 requirement, allows read-only backup server demonstration (Implemented)
- **[02-01] NodePort range 32150-32156:** Sequential ports for 7 NAS servers, avoids conflicts with existing services (Implemented)
- **[02-02] kubectl exec validation sufficient:** Validated storage isolation and subdirectory mounts via kubectl exec; external NFS mount not required for Phase 2 (Implemented)
- **[02-02] Init container sync on pod start:** Test files created on Windows require pod restart to sync; init container only runs at pod start (Validated)

### Pending Todos

None yet.

### Blockers/Concerns

**Technical Risk (Phase 1) - RESOLVED:**
- Pattern validated (01-02 complete): Windows hostPath → emptyDir → NFS export works perfectly
- Resolved questions: NET_BIND_SERVICE sufficient, no need for CAP_DAC_READ_SEARCH; file ownership preserved via rsync
- New questions for Phase 2: rpcbind integration (why CrashLoopBackOff?), external NFS mount without privileged mode

**Resource Capacity (Phase 2) - VALIDATED:**
- 7 NAS pods: 448Mi request, 1.75Gi limit (revised from initial estimate)
- Fits comfortably in 8GB Minikube with room for microservices
- Deployment tested in 02-02: All 7 pods running stably with minimal CPU usage (<50m per pod)

**Sync Latency (Phase 3):**
- Current pattern: Init container one-time sync at pod start (proven in 01-02)
- May need sidecar continuous sync if 30-second visibility requirement applies to file additions (deferred to Phase 3)

**rpcbind Investigation (Phase 2):**
- rpcbind integration caused CrashLoopBackOff in 01-02 testing
- Deferred to Phase 2: investigate startup ordering, RPC registration, port configuration
- External NFS mount without privileged mode depends on rpcbind resolution

## Session Continuity

Last session: 2026-01-29 14:32 UTC — Plan 02-02 execution
Stopped at: Completed 02-02-PLAN.md (Deploy and Test 7 NAS Servers)
Resume file: None
Next: 02-03-PLAN.md (Update .NET Client Library for Multi-NAS)
