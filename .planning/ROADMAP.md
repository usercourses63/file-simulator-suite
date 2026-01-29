# Roadmap: File Simulator Suite - Multi-NAS Production Topology

## Overview

This roadmap transforms the existing single NFS server into a 7-server production topology simulator that replicates OCP network architecture. We validate the init container + unfs3 pattern with a single instance, scale to full 7-server topology, add bidirectional sync for output NAS servers, and deliver configuration templates for developer consumption. Each phase addresses the core technical challenge: Linux NFS cannot export Windows-mounted filesystems, requiring a sync-based workaround.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Single NAS Validation** - Prove init container + unfs3 pattern works
- [ ] **Phase 2: 7-Server Topology** - Scale to production-matching infrastructure
- [ ] **Phase 3: Bidirectional Sync** - Enable output file retrieval on Windows
- [ ] **Phase 4: Configuration Templates** - Deliver developer-ready integration artifacts
- [ ] **Phase 5: Testing Suite** - Validate topology isolation and persistence

## Phase Details

### Phase 1: Single NAS Validation
**Goal**: Prove init container + unfs3 pattern successfully exposes Windows directories via NFS without privileged mode
**Depends on**: Nothing (first phase)
**Requirements**: NAS-04, NAS-05, NAS-07, WIN-01, WIN-04, WIN-06, WIN-07, EXP-03, EXP-04, DEP-04
**Success Criteria** (what must be TRUE):
  1. Single NAS pod (nas-test-1) deployed and running without privileged security context
  2. File written to Windows directory C:\simulator-data\nas-test-1\ appears via NFS mount within 30 seconds
  3. Pod restart preserves Windows files (init container re-syncs on startup)
  4. NFS client can mount nas-test-1:/data and list files
  5. unfs3 exports /data with rw,sync,no_root_squash options
**Plans**: 2 plans

Plans:
- [ ] 01-01-PLAN.md — Create Helm template for nas-test-1 with init container + unfs3 pattern
- [ ] 01-02-PLAN.md — Deploy and validate NAS pattern (includes human verification checkpoint)

### Phase 2: 7-Server Topology
**Goal**: Deploy 7 independent NAS servers with unique DNS names and isolated storage matching production OCP configuration
**Depends on**: Phase 1
**Requirements**: NAS-01, NAS-02, NAS-03, NAS-06, WIN-02, EXP-01, EXP-02, EXP-05, INT-03, INT-04, DEP-01, DEP-02, DEP-03, DEP-05
**Success Criteria** (what must be TRUE):
  1. 7 NAS pods deployed (nas-input-1/2/3, nas-backup, nas-output-1/2/3) with stable DNS names
  2. Each NAS accessible via unique NodePort (32150-32156) from Windows host
  3. Each NAS backed by separate Windows directory (C:\simulator-data\nas-input-1\, etc.)
  4. Files in nas-input-1 NOT visible in nas-input-2 (storage isolation verified)
  5. Each NAS has unique fsid value preventing server conflicts
  6. All 7 servers operational simultaneously under Minikube 8GB/4CPU constraints
**Plans**: TBD

Plans:
- [ ] 02-01: TBD

### Phase 3: Bidirectional Sync
**Goal**: Enable output NAS servers to sync files written via NFS mount back to Windows for tester retrieval
**Depends on**: Phase 2
**Requirements**: WIN-03, WIN-05
**Success Criteria** (what must be TRUE):
  1. System writes file via NFS to nas-output-1:/data, file appears in C:\simulator-data\nas-output-1\ within 60 seconds
  2. Sidecar rsync container runs continuously in output NAS pods (nas-output-1/2/3, nas-backup)
  3. Input NAS servers remain one-way sync only (no sidecar overhead)
  4. Bidirectional sync interval configurable via Helm values (default 30 seconds)
  5. No sync loops or file corruption after 100 write cycles
**Plans**: TBD

Plans:
- [ ] 03-01: TBD

### Phase 4: Configuration Templates
**Goal**: Deliver ready-to-use PV/PVC manifests and integration documentation for systems under development
**Depends on**: Phase 2
**Requirements**: INT-01, INT-02, INT-05
**Success Criteria** (what must be TRUE):
  1. Pre-built PV/PVC YAML manifests exist for each of 7 NAS servers
  2. Example microservice deployment mounts 3+ NAS servers simultaneously
  3. ConfigMap contains service discovery endpoints for all NAS servers
  4. Documentation explains how to replicate production OCP PV/PVC configs
  5. Windows directory preparation PowerShell script creates all 7 directories automatically
**Plans**: TBD

Plans:
- [ ] 04-01: TBD

### Phase 5: Testing Suite
**Goal**: Validate topology correctness, isolation guarantees, and persistence across restarts
**Depends on**: Phase 3
**Requirements**: TST-01, TST-02, TST-03, TST-04, TST-05
**Success Criteria** (what must be TRUE):
  1. Automated test script verifies all 7 NAS servers respond to health check
  2. Round-trip test (Windows -> NFS -> Windows) passes for input and output NAS
  3. Cross-NAS isolation test confirms files on one server not visible on others
  4. Pod restart test demonstrates files persist after killing all NAS pods
  5. Test suite executable as single command: ./scripts/test-multi-nas.ps1
**Plans**: TBD

Plans:
- [ ] 05-01: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Single NAS Validation | 0/2 | Planned | - |
| 2. 7-Server Topology | 0/1 | Not started | - |
| 3. Bidirectional Sync | 0/1 | Not started | - |
| 4. Configuration Templates | 0/1 | Not started | - |
| 5. Testing Suite | 0/1 | Not started | - |
