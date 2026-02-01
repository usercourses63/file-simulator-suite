# Requirements: File Simulator Suite - Multi-NAS Production Topology

**Defined:** 2026-01-29
**Core Value:** Dev systems must use identical PV/PVC configs as production OCP, with Windows test files visible via NFS.

## v1 Requirements

### Multi-NAS Infrastructure

- [ ] **NAS-01**: Deploy 3 independent input NAS servers (nas-input-1, nas-input-2, nas-input-3)
- [ ] **NAS-02**: Deploy 1 independent backup NAS server (nas-backup)
- [ ] **NAS-03**: Deploy 3 independent output NAS servers (nas-output-1, nas-output-2, nas-output-3)
- [ ] **NAS-04**: Each NAS has unique Kubernetes service with predictable DNS name
- [ ] **NAS-05**: Each NAS exports /data via NFS protocol
- [ ] **NAS-06**: Each NAS uses unique NodePort (32150-32156) for external access
- [ ] **NAS-07**: Each NAS has unique fsid value to prevent server conflicts

### Windows Integration

- [ ] **WIN-01**: Each NAS maps to separate Windows directory (C:\simulator-data\nas-input-1\, etc.)
- [ ] **WIN-02**: Files placed in Windows directory visible via NFS mount within 30 seconds (requires continuous sync sidecar)
- [x] **WIN-03**: Files written via NFS mount appear in Windows directory (output NAS servers)
- [ ] **WIN-04**: Init container syncs Windows -> NFS on pod startup
- [x] **WIN-05**: Sidecar container provides continuous sync for output directories
- [ ] **WIN-06**: Files survive pod restarts without data loss
- [ ] **WIN-07**: Windows directory structure created automatically if missing

### NFS Export Configuration

- [ ] **EXP-01**: Each NAS supports subdirectory mounts (/data/sub-1, /data/sub-2, /data/sub-3)
- [ ] **EXP-02**: Each NAS allows runtime creation of custom subdirectories
- [ ] **EXP-03**: Each NAS export configured with rw,sync,no_root_squash options
- [ ] **EXP-04**: NFSv4 protocol supported with single-port operation
- [ ] **EXP-05**: Per-NAS export options configurable via Helm values

### System Integration

- [ ] **INT-01**: Example PV/PVC manifests provided for each NAS server
- [ ] **INT-02**: PV/PVC configuration matches production OCP patterns
- [ ] **INT-03**: System can mount multiple NAS servers simultaneously
- [ ] **INT-04**: Each PVC isolated (nas-input-1 data separate from nas-output-1)
- [ ] **INT-05**: Documentation shows how to configure multiple NFS mounts

### Deployment & Configuration

- [ ] **DEP-01**: All 7 NAS servers configured via single Helm values file
- [ ] **DEP-02**: Individual NAS servers can be enabled/disabled independently
- [ ] **DEP-03**: Helm chart supports configurable NAS instance counts
- [ ] **DEP-04**: Each NAS has configurable resource limits (CPU/memory)
- [ ] **DEP-05**: Deployment tested on Minikube Hyper-V with 8GB/4CPU

### Testing & Verification

- [ ] **TST-01**: Test script verifies all 7 NAS servers accessible
- [ ] **TST-02**: Test creates file on Windows, verifies visible via NFS mount
- [ ] **TST-03**: Test writes file via NFS mount, verifies appears on Windows
- [ ] **TST-04**: Cross-NAS isolation verified (files on nas-input-1 not on nas-input-2)
- [ ] **TST-05**: Pod restart test confirms files persist

## v2 Requirements

Deferred to future releases:

### Performance & Optimization
- **PERF-01**: Real-time sync (<1 second latency) using inotify
- **PERF-02**: Incremental sync optimization (only changed files)
- **PERF-03**: Compression for large file transfers

### Advanced Configuration
- **ADV-01**: Dynamic NAS provisioning (add servers without Helm upgrade)
- **ADV-02**: Per-subdirectory export options
- **ADV-03**: Load balancing across input NAS servers

### Monitoring
- **MON-01**: Sync status dashboard showing lag per NAS
- **MON-02**: File operation metrics (reads/writes per NAS)
- **MON-03**: Storage capacity alerts

## Out of Scope

| Feature | Reason |
|---------|--------|
| Single NAS with 7 exports | Production has 7 physical devices; dev must match topology |
| Cross-protocol automatic sync | Each protocol independent; same files not required |
| Production-grade performance | Development simulator; sub-30s sync acceptable |
| HA/failover between NAS | Development environment; single instance per NAS sufficient |
| NFS locking mechanisms | Development workflow; file locking not critical |

## Traceability

Mapping to phases:

| Requirement | Phase | Status |
|-------------|-------|--------|
| NAS-01 | Phase 2 | Complete |
| NAS-02 | Phase 2 | Complete |
| NAS-03 | Phase 2 | Complete |
| NAS-04 | Phase 1 | Complete |
| NAS-05 | Phase 1 | Complete |
| NAS-06 | Phase 2 | Complete |
| NAS-07 | Phase 1 | Complete |
| WIN-01 | Phase 1 | Complete |
| WIN-02 | Phase 3 | Pending |
| WIN-03 | Phase 3 | Complete |
| WIN-04 | Phase 1 | Complete |
| WIN-05 | Phase 3 | Complete |
| WIN-06 | Phase 1 | Complete |
| WIN-07 | Phase 1 | Complete |
| EXP-01 | Phase 2 | Complete |
| EXP-02 | Phase 2 | Complete |
| EXP-03 | Phase 1 | Complete |
| EXP-04 | Phase 1 | Complete |
| EXP-05 | Phase 2 | Complete |
| INT-01 | Phase 4 | Pending |
| INT-02 | Phase 4 | Pending |
| INT-03 | Phase 2 | Complete |
| INT-04 | Phase 2 | Complete |
| INT-05 | Phase 4 | Pending |
| DEP-01 | Phase 2 | Complete |
| DEP-02 | Phase 2 | Complete |
| DEP-03 | Phase 2 | Complete |
| DEP-04 | Phase 1 | Complete |
| DEP-05 | Phase 2 | Complete |
| TST-01 | Phase 5 | Pending |
| TST-02 | Phase 5 | Pending |
| TST-03 | Phase 5 | Pending |
| TST-04 | Phase 5 | Pending |
| TST-05 | Phase 5 | Pending |

**Coverage:**
- v1 requirements: 34 total
- Mapped to phases: 34 (100%)
- Unmapped: 0

---
*Requirements defined: 2026-01-29*
*Last updated: 2026-01-29 after WIN-02 moved to Phase 3 (requires continuous sync sidecar)*
