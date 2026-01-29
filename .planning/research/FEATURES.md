# Feature Landscape: Multi-NAS Development Simulator

**Domain:** NAS/NFS development simulator for production topology replication
**Researched:** 2026-01-29
**Overall confidence:** HIGH

## Executive Summary

This research examines features needed for a development NAS simulator that replicates production's 7-NAS topology (3 input, 1 backup, 3 output). The simulator must expose Windows directories via NFS so testers can place files on Windows while systems under development access them through production-identical PV/PVC configurations.

Research reveals three feature categories: **table stakes** (core NFS functionality), **differentiators** (multi-NAS topology simulation), and **anti-features** (patterns to avoid). The existing codebase has proven patterns for multi-instance protocol servers that can be adapted for NAS.

## Table Stakes

Features users expect. Missing = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| NFS export per NAS | Each NAS server must export its directory | Low | `/data` export with `rw,sync,no_subtree_check,no_root_squash` options |
| Unique service DNS per NAS | Production uses different hostnames for each NAS | Low | `nas-input-1.svc`, `nas-input-2.svc`, etc. |
| Windows directory mapping | Testers place files on Windows, visible via NFS | Medium | Requires solution to hostPath export limitation |
| NFSv4 support | Production uses NFSv4 (single port, better security) | Low | Already supported by erichough/nfs-server |
| Subdirectory exports | Each NAS may export specific subdirectories | Low | `/data/sub-1`, `/data/sub-2` within main export |
| PV/PVC configuration match | Dev PV specs must match production exactly | Low | Same server/path/mountOptions in both environments |
| Persistent storage | Files survive pod restarts | Medium | Requires Windows-backed storage, not emptyDir |

**Implementation Status:**
- ✅ Single NFS server with NFSv4 exists (nas.yaml)
- ✅ Multi-instance pattern exists (values-multi-instance.yaml for FTP/SFTP)
- ❌ Windows directory mapping broken (current emptyDir workaround isolates from Windows)
- ❌ Multiple NAS servers not yet implemented

## Differentiators

Features that set this simulator apart. Not expected, but highly valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| 7 NAS topology (3+1+3) | Exact production topology match | Medium | Prevents "works in dev, fails in prod" scenarios |
| Role-based NAS naming | Semantic names (input-1, backup, output-1) | Low | Self-documenting configuration |
| Per-NAS directory isolation | Each NAS maps to separate Windows directory | Medium | `C:\simulator-data\nas-input-1\`, etc. |
| Cross-NAS file sharing simulation | Files written to output NAS visible in Windows | High | Validates end-to-end workflow |
| Independent NAS configuration | Each NAS can have different export options | Low | Some need ro, others rw; some need root_squash |
| NodePort per NAS | Each NAS accessible via unique port from host | Low | Port 32150-32156 for 7 servers |
| Configuration template library | Pre-built PV/PVC manifests for each NAS | Low | Copy-paste-edit for new projects |

**Key Differentiator:** Production parity - same number of NAS servers, same naming convention, same PV/PVC specs = zero deployment differences.

## Anti-Features

Features to explicitly NOT build. Common mistakes in this domain.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Single NAS with 7 exports | Production has 7 physical devices, not 7 exports on 1 device | Deploy 7 separate pods, each with own service |
| Shared storage between NAS servers | Production NAS servers are independent | Each NAS pod gets separate Windows subdirectory |
| Dynamic subdirectory provisioning | Adds complexity; production uses static exports | Pre-configure subdirectories in Windows |
| Load balancing across NAS servers | Production explicitly routes to specific NAS | Each PV/PVC specifies exact NAS server |
| NFS auto-discovery | Production requires explicit PV/PVC configuration | Provide example manifests, no magic |
| Cross-protocol file sync | NFS should not auto-sync with FTP/S3/SMB | Each protocol is independent; manual sync if needed |
| Real-time Windows filesystem events | Adds complexity; polling is sufficient | Systems poll directories periodically |

**Critical Anti-Pattern:** Treating 7 NAS servers as a cluster with shared state. Production systems connect to *specific* NAS servers by name for *specific* purposes. Dev must replicate this exactness.

## Feature Dependencies

```
Core NFS Export (each NAS)
    ↓
Unique Service DNS per NAS
    ↓
Windows Directory Mapping per NAS
    ↓
PV/PVC Configuration per NAS
    ↓
Multi-NAS Application Deployment
```

**Key dependency:** Windows directory mapping is the foundation. Without solving the hostPath export limitation, each NAS can export but files won't be visible/editable on Windows.

## Configuration Patterns

### Pattern 1: Per-NAS PersistentVolume (Production and Dev)

**Production OCP:**
```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: nas-input-1-pv
spec:
  capacity:
    storage: 500Gi
  accessModes:
    - ReadWriteMany
  nfs:
    server: nas-input-1.corp.internal
    path: /vol1/data/sub-1
  mountOptions:
    - nfsvers=4.1
    - hard
    - noatime
```

**Development Minikube (target):**
```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: nas-input-1-pv
spec:
  capacity:
    storage: 10Gi  # Smaller, but same structure
  accessModes:
    - ReadWriteMany
  nfs:
    server: 172.25.201.3  # file-simulator cluster IP
    path: /data
  mountOptions:
    - nfsvers=4
    - port=32150  # NodePort for nas-input-1
    - nolock
    - soft
```

**Key point:** Only IP and port differ. Application PVC references work identically.

### Pattern 2: Role-Based NAS Selection

**Production pattern:**
```yaml
# Application deployment specifies which NAS for what purpose
volumeMounts:
  - name: ingest-storage
    mountPath: /mnt/input
  - name: output-storage
    mountPath: /mnt/output
volumes:
  - name: ingest-storage
    persistentVolumeClaim:
      claimName: nas-input-1-pvc  # Explicit NAS selection
  - name: output-storage
    persistentVolumeClaim:
      claimName: nas-output-1-pvc  # Different NAS
```

**Dev environment:** Same configuration works if PVCs exist with same names pointing to simulator NAS servers.

### Pattern 3: Subdirectory Isolation

**Production NAS exports:**
```
nas-input-1: /vol1/data/sub-1   (raw ingest files)
nas-input-1: /vol1/data/sub-2   (validated files)
nas-output-1: /vol2/data/results (processed outputs)
```

**Dev simulator structure:**
```
C:\simulator-data\
  nas-input-1\
    data\
      sub-1\   (tester places raw files here)
      sub-2\   (system moves validated files here)
  nas-output-1\
    data\
      results\ (system writes outputs here, tester retrieves)
```

**Implementation:** Each NAS server exports `/data`, which contains `sub-1/`, `sub-2/`, etc. Kubernetes mounts can specify subPath.

## Windows Integration Requirements

| Requirement | Rationale | Implementation Status |
|-------------|-----------|----------------------|
| Bidirectional visibility | Tester writes files, system reads via NFS; system writes files, tester reads on Windows | ❌ Broken by emptyDir workaround |
| Directory structure matches | `C:\simulator-data\nas-input-1\` maps to `nas-input-1:/data` | ✅ Structure exists on Windows side |
| Immediate propagation | File written on Windows visible to NFS client within seconds | ⚠️ Depends on NFS client cache settings |
| Permission compatibility | Windows permissions don't block Linux NFS access | ⚠️ Needs testing with no_root_squash |
| Cross-process safety | Multiple systems can write to same NAS simultaneously | ✅ NFS file locking handles this |

**Critical gap:** Current emptyDir workaround breaks bidirectional visibility. Production pattern requires Windows as source of truth.

## MVP Recommendation

For MVP, prioritize:

1. **7 NAS server pods** - Matches production topology (critical for config parity)
2. **Unique service DNS per NAS** - `nas-input-1`, `nas-input-2`, ..., `nas-backup`, `nas-output-1`, ...
3. **Per-NAS Windows directories** - `C:\simulator-data\nas-input-1\`, etc.
4. **Solve Windows hostPath export** - Critical blocker; alternative: bind mount, sidecar sync, or accept limitation
5. **Example PV/PVC manifests** - Pre-built for each of 7 NAS servers
6. **One subdirectory per NAS** - Start with single `/data` export, expand to `/data/sub-1` etc. later

Defer to post-MVP:
- Multiple subdirectory exports per NAS: Can add later via export configuration
- Cross-protocol sync: Not required; each protocol independent by design
- Dynamic provisioning: Production uses static PVs; match that pattern
- Load balancing: Production doesn't do this; don't add complexity

## Complexity Assessment

| Feature | Complexity | Blockers | Effort |
|---------|-----------|----------|--------|
| Deploy 7 NAS pods | Low | None | Copy existing nas.yaml 7 times with name variations |
| Unique service per NAS | Low | None | Service template with name/nodePort parameters |
| Per-NAS Windows directories | Low | None | Create directories: `mkdir C:\simulator-data\nas-*` |
| Solve hostPath export limitation | **High** | Linux kernel limitation on Windows-mounted FS | Research bind mounts, overlayfs, or accept read-only from Windows |
| PV/PVC manifest generation | Low | None | Template with 7 variations (input-1, input-2, ..., output-3) |
| Subdirectory exports | Low | Export config | Add multiple NFS_EXPORT_N environment variables |
| Configuration docs | Low | None | Document each NAS's purpose, ports, example usage |

**Critical path:** Solving Windows hostPath export limitation. Without this, testers cannot place files on Windows for systems to consume via NFS (one-way only: system writes, tester retrieves).

## Known Technical Limitations

### Linux Kernel Limitation: NFS Cannot Export Windows-Mounted Filesystems

**Root cause:** The NFS server daemon (kernel nfsd) cannot export filesystems that don't support native Linux export tables, lock files, and state management. Windows CIFS/9p mounts fall into this category.

**Evidence:** Current deployment shows:
```
exportfs: /data does not support NFS export
----> ERROR: /usr/sbin/exportfs failed
```

**Current workaround:** emptyDir volume for `/data` (NFS export) + PVC volume at `/shared` (Windows access). This breaks bidirectional visibility.

**Possible solutions:**

1. **Bind mount with overlayfs (unexplored):** Layer a Linux-native filesystem over Windows mount
2. **Sidecar sync container (complex):** rsync/inotify to sync Windows directory → emptyDir
3. **Accept limitation (impacts workflow):** System writes to NFS → tester retrieves via Management UI, not Windows Explorer
4. **Windows NFS Server (architectural change):** Export from Windows host instead of Linux container

**Recommendation:** Research #1 (overlayfs) first, fall back to #2 (sidecar sync) if needed. Solution #3 degrades developer experience significantly.

## Research Sources

### High Confidence (Official Documentation)

- [Red Hat: NFS Server Configuration](https://docs.redhat.com/en/documentation/red_hat_enterprise_linux/7/html/storage_administration_guide/nfs-serverconfig) - NFS export options and configuration
- [Linux man page: exports(5)](https://linux.die.net/man/5/exports) - NFS export table reference
- [Kubernetes Storage: NFS Persistent Volumes (OKD)](https://docs.okd.io/latest/storage/persistent_storage/persistent-storage-nfs.html) - K8s NFS PV configuration
- [Microsoft Learn: Azure NFS Volume](https://learn.microsoft.com/en-us/azure/aks/azure-nfs-volume) - NFS PV creation patterns
- [Canonical: Use NFS for Persistent Volumes on MicroK8s](https://microk8s.io/docs/how-to-nfs) - NFS mount options and configuration

### Medium Confidence (Technical Guides and Examples)

- [OneUpTime: How to Use NAS Storage with Kubernetes](https://oneuptime.com/blog/post/2025-12-15-how-to-use-nas-storage-with-kubernetes/view) - NFS integration patterns
- [Kubernetes-SIGs: NFS Subdir External Provisioner](https://github.com/kubernetes-sigs/nfs-subdir-external-provisioner) - Dynamic subdirectory provisioning
- [Computing for Geeks: Configure NFS for Kubernetes](https://computingforgeeks.com/configure-nfs-as-kubernetes-persistent-volume-storage/) - Setup guide
- [Baeldung: NFS Shares Export/Import with Subdirectories](https://www.baeldung.com/linux/nfs-shares-export-import) - Subdirectory export patterns
- [GoLinuxCloud: 10 Practical NFS Export Examples](https://www.golinuxcloud.com/nfs-exports-options-examples/) - Export options reference

### Medium Confidence (Industry Best Practices)

- [ThinkPalm: Distributed NAS Deployment Best Practices](https://thinkpalm.com/blogs/how-to-deploy-distributed-nas-successfully-best-practices-and-business-outcomes/) - Enterprise patterns
- [NetApp: 5 NAS Backup Strategies](https://www.netapp.com/learn/cbs-blg-5-nas-backup-strategies-and-their-pros-and-cons/) - 3-2-1 backup pattern
- [Commvault: NAS Backup Best Practices](https://www.commvault.com/explore/nas-backup) - Multi-server backup patterns
- [EngiFeared: NAS for Developers](https://engifeared.com/nas-guide/) - Development environment NAS usage

### Low Confidence (Community Discussion)

- [Enov8: Test Environment Setup](https://www.enov8.com/blog/test-environments-why-you-need-one-and-how-to-set-it-up/) - General test environment patterns
- [MIMIC Server Simulator](https://www.gambitcomm.com/site/server-simulator.php) - Server simulation concepts
- [9to5IT: Test Lab Design](https://9to5it.com/designing-test-lab/) - Lab topology design

## Gap Analysis

### Areas with High Confidence
- ✅ NFS export configuration options (crossmnt, no_subtree_check, no_root_squash)
- ✅ NFSv4 pseudo-filesystem approach with fsid=0
- ✅ Kubernetes PV/PVC configuration patterns for NFS
- ✅ Multiple NAS server configuration (separate PVs per server)
- ✅ Production best practices (3-2-1 backup, role-based NAS selection)
- ✅ Subdirectory export patterns

### Areas with Medium Confidence
- ⚠️ Windows-to-Linux NFS integration specifics (some articles, not fully tested)
- ⚠️ Development simulator patterns (general concepts, not NAS-specific)
- ⚠️ File synchronization latency in Minikube hostPath mounts
- ⚠️ Permission mapping between Windows and NFS (no_root_squash behavior)

### Areas Requiring Further Research
- ❌ Overlayfs or bind mount solutions for Windows hostPath export limitation
- ❌ Sidecar container patterns for directory sync (inotify, rsync)
- ❌ Performance characteristics of 7 NAS pods on single Minikube node
- ❌ NFS client caching behavior with Windows-backed storage
- ❌ Cross-NAS file visibility patterns in production (do teams actually need this?)

## Recommendations for Roadmap

### Phase Structure Implications

**Phase 1: Basic Multi-NAS Infrastructure**
- Deploy 7 NAS server pods (copy existing pattern)
- Unique service DNS per NAS
- Per-NAS Windows directories
- Example PV/PVC manifests

**Phase 2: Windows Integration**
- Research and implement solution to hostPath export limitation
- Test bidirectional file visibility
- Validate permissions with no_root_squash

**Phase 3: Advanced Features**
- Multiple subdirectory exports per NAS
- Per-NAS export option customization (ro vs rw, root_squash variants)
- Configuration template library

**Rationale:** Phase 1 can proceed with limitation acknowledged (system writes to NFS, tester retrieves via Management UI). Phase 2 solves bidirectional visibility. Phase 3 adds production-like configuration flexibility.

### Research Flags

- **Phase 2 likely needs deeper research:** Windows hostPath export limitation solution
- **Phase 1 unlikely to need research:** Multi-instance pattern already proven in FTP/SFTP

### Success Criteria

MVP achieves success when:
- ✅ Developer runs `kubectl apply -f nas-input-1-pvc.yaml` and it works identically to production
- ✅ 7 NAS servers deployed, each with unique service name and NodePort
- ✅ Application pod can mount `nas-input-1:/data` and `nas-output-1:/data` simultaneously
- ✅ Configuration matches production (only IP and port differ)
- ⚠️ Bidirectional Windows visibility (Phase 2 goal; acknowledge limitation in Phase 1)

---

**Next steps:** Use this feature landscape to inform requirements definition and phase breakdown. The critical decision is whether to accept Windows visibility limitation in MVP or delay MVP until Windows integration is solved.
