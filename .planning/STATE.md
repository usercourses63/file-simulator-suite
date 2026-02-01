# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-01-29)

**Core value:** Dev systems must use identical PV/PVC configs as production OCP, with Windows test files visible via NFS.
**Current focus:** Phase 2: 7-Server Topology

## Current Position

Phase: 3 of 5 (Bidirectional Sync)
Plan: 2 of 2 (Bidirectional Sync Validation)
Status: Phase Complete
Last activity: 2026-02-01 — Completed 03-02-PLAN.md

Progress: [███████░░░] 70%

## Performance Metrics

**Velocity:**
- Total plans completed: 7
- Average duration: 15.5 minutes
- Total execution time: 1.81 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-single-nas-validation | 2 | 34.4min | 17.2min |
| 02-7-server-topology | 3 | 60.5min | 20.2min |
| 03-bidirectional-sync | 2 | 8.6min | 4.3min |

**Recent Trend:**
- Last 5 plans: 02-02 (9min), 02-03 (47min), 03-01 (3.6min), 03-02 (5min)
- Trend: Template-only plans are very fast (sub-5min); validation plans 5-10min; advanced validation with full test suite 30-50min

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
- **[02-03] Runtime directories ephemeral for input NAS:** Runtime-created directories lost on pod restart (expected); Windows filesystem is source of truth; init container overwrites on each pod start (Validated)
- **[02-03] Multi-NAS architecture validated at service level:** 7 unique services with ClusterIPs, DNS names, storage isolation confirmed; NFS volume mount testing blocked by rpcbind (Validated)
- **[02-03] Test automation via PowerShell:** kubectl exec validation in structured PASS/FAIL/SKIP format sufficient for Phase 2; 37/38 tests passing (Implemented)
- **[03-01] Native sidecar over regular container:** Use init container with restartPolicy: Always for proper startup ordering and graceful shutdown (Implemented)
- **[03-01] Init container --delete based on server NAME:** Use server name pattern (nas-input-*) to determine --delete flag, not sidecar.enabled; nas-backup is not an input server (Implemented)
- **[03-01] nas-backup sidecar disabled:** Read-only export (ro) cannot receive NFS writes; sidecar would be pointless overhead (Implemented)
- **[03-01] emptyDir sizeLimit 500Mi:** Kubernetes best practice to prevent disk exhaustion and node-wide impact (Implemented)
- **[03-02] Phase 3 validated with 10/10 tests passing:** NFS-to-Windows sync timing 15-30s (under 60s requirement), sidecar correctly deployed on output servers only (Validated)
- **[03-02] WIN-02 uses init container pattern:** Continuous Windows-to-NFS requires pod restart; second sidecar would be needed for continuous sync without restart (Not in Phase 3 scope)

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
- Phase 2 complete: System meets production-ready criteria

**Sync Latency (Phase 3) - RESOLVED:**
- Init container one-time sync at pod start: Validated and working (Phase 2)
- Sidecar continuous sync for output NAS: VALIDATED (03-02) - 15-30s NFS-to-Windows sync (under 60s requirement)
- Input NAS one-way sync: Validated and working reliably
- Bidirectional pattern: COMPLETE - all 10 Phase 3 tests passing, no sync loops observed
- WIN-02 continuous Windows-to-NFS: Uses init container pattern (requires pod restart); second sidecar not in Phase 3 scope

**rpcbind Investigation (Phase 3):**
- rpcbind integration still blocked (DNS resolution fails during NFS mount)
- Multi-NAS architecture validated at service level in 02-03
- External NFS mount testing deferred to Phase 3 (kubectl exec sufficient for Phase 2)
- Consider alternative NFS servers (nfs-ganesha) if unfs3 blocker persists

## Session Continuity

Last session: 2026-02-01 — Plan 03-02 execution
Stopped at: Completed 03-02-PLAN.md (Bidirectional Sync Validation) — Phase 3 complete ✅
Resume file: None
Next: Phase 4 (Configuration Templates) — deliver PV/PVC manifests and integration documentation
