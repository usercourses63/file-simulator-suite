# Project Research Summary

**Project:** File Simulator Suite - Multi-NAS Capability
**Domain:** Development environment NAS/NFS simulation with Windows integration
**Researched:** 2026-01-29
**Confidence:** HIGH

## Executive Summary

The File Simulator Suite requires expanding from a single NFS server to 7 independent NAS servers that replicate production topology (3 input, 1 backup, 3 output). The core technical challenge is that **Linux NFS servers cannot export Windows-mounted directories** due to a fundamental kernel limitation: CIFS/9p filesystems lack the filehandle support required by NFS export tables.

The recommended approach is an **init container sync pattern with userspace NFS (unfs3)**. This architecture uses init containers to copy Windows hostPath data into local emptyDir volumes, which are then exported by unfs3 (a userspace NFS server that doesn't require privileged mode). Deploy 7 independent NAS pods via Helm range loop with per-instance PV/PVC storage isolation, following the project's proven multi-instance pattern from FTP/SFTP deployments.

The critical risk is bidirectional data synchronization: while the init container pattern solves the export limitation, it introduces sync delay and potential data loss on pod restart. Mitigation involves optional sidecar containers for continuous sync and clear documentation of the trade-offs. This architecture prioritizes production parity (7 independent NAS servers with semantic names) over perfect Windows integration.

## Key Findings

### Recommended Stack

The stack research identified a fundamental blocker: the current erichough/nfs-server cannot export Windows-mounted hostPath volumes because Linux kernel NFS requires filesystem features (encode_fh) that CIFS/9p don't provide. Multiple authoritative sources (SUSE, Red Hat, kernel docs) confirm this is an unsolvable kernel limitation.

**Core technologies:**
- **unfs3** (userspace NFS server): No privileged mode required, works in containers, avoids kernel export limitations
- **Init Container + rsync**: Copy Windows hostPath to local emptyDir before NFS export (bypasses export limitation)
- **StatefulSet**: Deploy 7 independent NAS pods with stable identities and per-instance volumes
- **emptyDir + hostPath volumes**: emptyDir for NFS export (exportable), hostPath for Windows access (read-only mount)
- **Per-instance PV/PVC**: Isolate storage between NAS servers (nas-input-1 cannot see nas-output-1 data)

**Critical version requirements:**
- Kubernetes 1.14+ for StatefulSet features
- Helm 3.8+ for range loop YAML separator handling
- Alpine 3.19 for rsync init containers

### Expected Features

Research reveals three categories: table stakes (users expect), differentiators (production parity value), and anti-features (common mistakes to avoid).

**Must have (table stakes):**
- NFS export per NAS server with unique DNS names
- Windows directory mapping (testers place files, systems read via NFS)
- NFSv4 support with subdirectory exports
- PV/PVC configuration matching production exactly
- Files survive pod restarts (not plain emptyDir)

**Should have (competitive):**
- 7 NAS topology exactly matching production (3 input + 1 backup + 3 output)
- Role-based semantic naming (nas-input-1, nas-backup, nas-output-2)
- Per-NAS directory isolation (separate Windows subdirectories)
- Independent NAS configuration (different export options per server)
- Configuration template library (pre-built PV/PVC manifests)

**Defer (v2+):**
- Dynamic subdirectory provisioning (static pre-configuration sufficient)
- Cross-protocol file sync (each protocol independent by design)
- Load balancing across NAS servers (production routes explicitly by name)
- Real-time Windows filesystem events (polling acceptable for dev environment)

**Critical anti-pattern identified:** Treating 7 NAS servers as a cluster with shared state. Production systems connect to specific NAS servers by name for specific purposes. Dev must replicate this exactness, not abstract it away.

### Architecture Approach

The architecture research evaluated multiple patterns and recommends **Helm range loop with per-instance PV/PVC**, following the project's existing ftp-multi.yaml and sftp-multi.yaml patterns. This proven approach generates 7 independent deployments from a single values array.

**Major components:**
1. **Values configuration** — Array of 7 NAS instances with name, nodePort, hostPath, storageSize parameters
2. **Storage template** — PV/PVC pairs per instance (nas-storage-multi.yaml) with unique hostPath bindings
3. **Deployment template** — StatefulSet or 7 Deployments with init container sync + unfs3 main container (nas-multi.yaml)
4. **Service template** — Per-instance ClusterIP services with predictable DNS (nas-input-1.file-simulator.svc.cluster.local)

**Data flow pattern:**
```
Windows: C:\simulator-data\nas-input-1\
    ↓ (Minikube 9p mount)
hostPath: /mnt/simulator-data/nas-input-1 (READ-ONLY)
    ↓ (Init container rsync)
emptyDir: /data (LOCAL FILESYSTEM)
    ↓ (unfs3 export)
NFS: nas-input-1:/data
```

**Critical design decisions:**
- **emptyDir for NFS exports**: Required because hostPath cannot be exported
- **PVC for Windows access**: Per-instance PVC mounts hostPath for file visibility
- **Init container sync**: One-time copy on pod startup (simple, good for testing)
- **Optional sidecar sync**: Continuous rsync for bidirectional (output NAS needs this)
- **Scope management in Helm**: Always use `$` for root context in range loops, `.` for iteration context

### Critical Pitfalls

Research identified 12 pitfalls across critical/moderate/minor severity. Top 5 with prevention strategies:

1. **NFS Cannot Export CIFS/9p Filesystems** — NFS crashes with "does not support NFS export" when hostPath is Windows-mounted. Prevention: Use emptyDir + sync pattern; never mount Windows directories directly as NFS export path; verify filesystem type with `df -T`.

2. **emptyDir Data Loss on Pod Restart** — Plain emptyDir loses all data on pod termination, breaking Windows-as-source-of-truth. Prevention: Implement bidirectional sync with rsync sidecar; document limitations prominently; test pod restart scenarios.

3. **Multiple NFS Servers with Conflicting fsid Values** — Duplicate fsid values cause mysterious performance degradation or mount wrong filesystem. Prevention: Assign unique fsid per NAS (nas-input-1=1, nas-input-2=2, etc.); template fsid in Helm based on instance index; test all 7 servers simultaneously.

4. **hostPath Pod Rescheduling to Different Node** — Pod rescheduled to different node loses Windows mount access (hostPath is node-specific). Prevention: Use nodeSelector to pin pods to Minikube node; configure StatefulSet with node affinity; document this requirement.

5. **NFS Server Requires Privileged Security Context** — erichough/nfs-server needs CAP_SYS_ADMIN, but security policies often prohibit this. Prevention: Use unfs3 instead (userspace, no privilege required); request SCC exception early if using privileged server; minimize privilege scope.

**Additional moderate pitfalls:**
- Static port requirements for RPC services (Minikube + Windows NodePort access)
- Minikube 9p mount performance degradation with >600 files
- exportfs state not persistent (use env vars, not runtime commands)
- NFSv3 vs NFSv4 compatibility (Windows clients may need explicit version)

## Implications for Roadmap

Based on research, suggested phase structure balances technical dependencies, risk mitigation, and incremental validation:

### Phase 1: Single Multi-NAS Deployment (Validation)
**Rationale:** Test the init container + unfs3 pattern with ONE NAS server before scaling to 7. Validates the core architecture without complexity of multiple instances.

**Delivers:**
- Helm templates for multi-NAS (nas-storage-multi.yaml, nas-multi.yaml)
- Init container rsync script with error handling
- Single test instance (nas-test-1) deployed and verified
- Windows directory sync confirmed working

**Addresses:**
- NFS export per server (table stakes feature)
- Windows directory mapping (table stakes feature)
- Avoid Pitfall #1 (cannot export hostPath directly)

**Technical validation checklist:**
- File written to Windows visible via NFS mount within 30 seconds
- Pod restart preserves data (init container re-syncs)
- unfs3 runs without privileged mode
- NFS mount succeeds from client pod

### Phase 2: Expand to 7 Independent NAS Servers
**Rationale:** With core pattern validated, scale to full production topology. This phase proves multi-instance Helm templating and resource isolation.

**Delivers:**
- 7 NAS server pods (nas-input-1/2/3, nas-backup, nas-output-1/2/3)
- Unique service DNS per NAS with sequential NodePorts (32150-32156)
- Per-instance PV/PVC with isolated Windows subdirectories
- Storage isolation verified (nas-input-1 cannot access nas-output-1 data)

**Uses:**
- StatefulSet with 7 replicas (ARCHITECTURE.md pattern)
- Helm range loop over nasServers array (existing ftp-multi.yaml pattern)
- Unique fsid per NAS (Pitfall #3 prevention)

**Avoids:**
- Pitfall #3 (duplicate fsid values causing performance degradation)
- Pitfall #4 (hostPath rescheduling via nodeAffinity)
- Single NAS with 7 exports anti-pattern (breaks production parity)

### Phase 3: Bidirectional Sync for Output NAS
**Rationale:** Input NAS servers only need one-way sync (Windows → emptyDir). Output NAS servers need bidirectional sync so systems can write files that testers retrieve on Windows.

**Delivers:**
- Sidecar rsync container for continuous sync (emptyDir → Windows)
- Bidirectional sync enabled for nas-output-1/2/3 and nas-backup
- Polling-based sync (inotify doesn't work over 9p mounts)
- Sync interval configurable (default 30 seconds)

**Implements:**
- Cross-NAS file sharing simulation (FEATURES.md differentiator)
- End-to-end workflow validation (system writes → tester retrieves)

**Avoids:**
- Pitfall #2 (data loss on pod restart) for output directories
- Real-time filesystem events anti-feature (polling sufficient)

### Phase 4: Configuration Templates and Documentation
**Rationale:** With working infrastructure, provide developer-friendly configuration templates and operational documentation.

**Delivers:**
- Pre-built PV/PVC manifests for each of 7 NAS servers
- ConfigMap with service discovery (nas-endpoints.json)
- Example microservice deployment using multiple NAS mounts
- Troubleshooting guide for common failure modes
- Windows directory preparation PowerShell script

**Addresses:**
- Configuration template library (FEATURES.md differentiator)
- PV/PVC configuration matching production (table stakes)

### Phase Ordering Rationale

- **Phase 1 before Phase 2**: Prove the workaround pattern works before investing in 7-instance complexity. If unfs3 + init container fails, we discover early with minimal rework.
- **Phase 2 before Phase 3**: Establish infrastructure (7 servers deployed) before adding sidecar complexity. Bidirectional sync is optional for input NAS, so defer until base working.
- **Phase 3 isolated**: Bidirectional sync adds failure modes (rsync loops, sync conflicts). Only enable for output NAS that need it.
- **Phase 4 last**: Documentation and templates only valuable once infrastructure proven stable.

**Critical path:** Phase 1 is the make-or-break phase. If Windows hostPath → emptyDir → NFS export chain fails, entire architecture needs rethinking (possibly external NFS server).

### Research Flags

**Phases needing deeper research during planning:**
- **Phase 1**: unfs3 configuration specifics (export options, pseudo-filesystem setup) — research if issues arise
- **Phase 3**: rsync bidirectional sync patterns (conflict handling, sync loops) — standard pattern but needs validation

**Phases with standard patterns (skip research-phase):**
- **Phase 2**: Multi-instance Helm templating is proven in project (ftp-multi.yaml, sftp-multi.yaml)
- **Phase 4**: Documentation and templates — no technical research needed

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | **HIGH** | Core limitation (cannot export hostPath) verified by multiple authoritative sources (SUSE, Red Hat, kernel docs). unfs3 solution validated by production use cases. |
| Features | **HIGH** | Table stakes identified from NFS/Kubernetes documentation. Production parity requirements clear from CLAUDE.md context. Anti-patterns documented in community reports. |
| Architecture | **HIGH** | Multi-instance pattern exists in codebase (ftp-multi.yaml). StatefulSet approach standard Kubernetes. Helm templating patterns well-documented. |
| Pitfalls | **HIGH** | All critical pitfalls (1-4) have authoritative sources and match observed behavior. Moderate pitfalls (5-9) have community evidence. Minor pitfalls documented in specs. |

**Overall confidence:** **HIGH**

### Gaps to Address

Despite high confidence, some areas need validation during implementation:

- **Sync latency trade-off**: Research indicates rsync polling is necessary (inotify doesn't work over 9p), but actual latency tolerance for testers needs validation. Acceptable range: 5-60 seconds. Test with realistic workflow: tester places file → system processes → output appears.

- **Resource capacity**: Calculations show 7 NAS pods fit in 8GB Minikube cluster (896Mi request, 3.5Gi limit), but actual resource usage under load unknown. Monitor with `kubectl top pods` during rollout; fallback is reduce pod limits or increase Minikube memory.

- **9p performance threshold**: Documentation states ">600 files" causes degradation, but exact threshold varies. Test with 100, 500, 1000, 5000 files to establish project-specific limits. Document findings in operational guide.

- **Windows NFS client compatibility**: Research suggests NFSv3/v4 version mismatches can cause issues, but specific mount options for Windows 10/11 NFS client not fully documented. Test mount options systematically; document working configuration.

**Recommended validation approach:** Prototype Phase 1 rapidly (1-2 days) to validate core assumptions before committing to full 7-instance architecture. If hostPath sync pattern proves unworkable, pivot to external NFS server on Windows host (WinNFSd or haneWIN NFS Server).

## Sources

### Primary (HIGH confidence)
- [SUSE Support: exportfs error - does not support NFS export](https://www.suse.com/support/kb/doc/?id=000021721) — Kernel limitation confirmed
- [NFS Ganesha FSAL_VFS documentation](https://github.com/nfs-ganesha/nfs-ganesha/wiki/VFS) — CIFS/NFS unsupported
- [Red Hat: How do I configure the fsid option in /etc/exports?](https://access.redhat.com/solutions/548083) — fsid collision behavior
- [Kubernetes: Persistent Volumes](https://kubernetes.io/docs/concepts/storage/persistent-volumes/) — PV/PVC patterns
- [Kubernetes: StatefulSets](https://kubernetes.io/docs/concepts/workloads/controllers/statefulset/) — Multi-instance deployment
- [unfs3 GitHub repository](https://github.com/unfs3/unfs3) — Userspace NFS server capabilities

### Secondary (MEDIUM confidence)
- [Minikube mount documentation](https://minikube.sigs.k8s.io/docs/handbook/mount/) — 9p filesystem characteristics
- [Linux Kernel: CONFIG_CIFS_NFSD_EXPORT](https://cateee.net/lkddb/web-lkddb/CIFS_NFSD_EXPORT.html) — Experimental export support
- [Helm: Flow Control in Templates](https://helm.sh/docs/chart_template_guide/control_structures/) — Range loop patterns
- [Medium: Creating multiple deployments with Helm](https://medium.com/@pasternaktal/creating-multiple-deployments-with-different-configurations-using-helm-4992f9f735fd) — Multi-instance examples
- [Earl C. Ruby III: Setting up NFS FSID](https://earlruby.org/2022/01/setting-up-nfs-fsid-for-multiple-networks/) — fsid best practices

### Tertiary (LOW confidence — informational)
- [Baeldung: NFS Shares with Subdirectories](https://www.baeldung.com/linux/nfs-shares-export-import) — Export patterns
- [Computing for Geeks: Configure NFS for Kubernetes](https://computingforgeeks.com/configure-nfs-as-kubernetes-persistent-volume-storage/) — Setup guide
- Community reports on 9p performance (Minikube GitHub issues) — Needs empirical validation

---
*Research completed: 2026-01-29*
*Ready for roadmap: yes*
