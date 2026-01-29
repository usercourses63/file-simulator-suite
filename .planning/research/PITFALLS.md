# Domain Pitfalls: NFS Server in Kubernetes with Windows-Mounted Filesystems

**Domain:** Containerized NFS servers exporting Windows-mounted directories in Kubernetes
**Researched:** 2026-01-29
**Context:** Multi-NAS deployment (7 servers) on Minikube/Hyper-V with Windows hostPath mounts

## Critical Pitfalls

Mistakes that cause rewrites, complete failures, or production parity breaks.

### Pitfall 1: NFS Cannot Export CIFS/9p/Overlay Filesystems

**What goes wrong:** NFS server container crashes with "exportfs: /data does not support NFS export" when the mount point is a CIFS share, 9p filesystem (Minikube mount), or overlayfs (Docker container layers).

**Why it happens:**
- **Root cause:** NFS export requires the underlying filesystem to provide filehandles for each file and support filehandle-based lookups. CIFS, 9p, and overlayfs do not provide these capabilities.
- **Technical detail:** The Linux kernel NFS module cannot create export tables on remote/virtual filesystems. This is a fundamental kernel limitation, not a configuration issue.
- **Specific to containers:** CoreOS and many container runtimes use overlayfs for container storage, which cannot be exported via NFS.

**Consequences:**
- NFS server pod fails to start or crashes immediately
- `exportfs -r` command fails during container initialization
- Complete inability to export Windows hostPath volumes directly
- Loss of Windows-as-source-of-truth architecture

**Prevention:**
1. **DO NOT** mount Windows directories directly as NFS export path
2. **DO NOT** use hostPath volumes directly mounted from Minikube's 9p mount
3. **DO** use intermediate local storage (emptyDir) within the container
4. **DO** implement file synchronization between hostPath and emptyDir
5. **VERIFY** filesystem type with `df -T` before attempting NFS export

**Detection:**
- Container CrashLoopBackOff immediately after deployment
- Logs show: "exportfs: /data does not support NFS export"
- Logs show: "exportfs: Cannot stat /data: Stale file handle"
- Pod events show repeated restart attempts

**Sources:**
- [SUSE: exportfs returns error "does not support NFS export"](https://www.suse.com/support/kb/doc/?id=000021721)
- [Linux NFS: NFS re-export](https://wiki.linux-nfs.org/wiki/index.php/NFS_re-export)
- [Arch Linux: Can't export overlayfs with NFS](https://bbs.archlinux.org/viewtopic.php?id=192585)

---

### Pitfall 2: emptyDir Data Loss on Pod Restart

**What goes wrong:** Using emptyDir as workaround for export limitation loses all data when pod restarts, breaking "Windows as source of truth" requirement and causing test file loss.

**Why it happens:**
- **Root cause:** emptyDir is ephemeral storage tied to pod lifetime. When pod terminates (crash, eviction, scaling, upgrade), emptyDir is permanently deleted.
- **Kubernetes design:** emptyDir is explicitly designed for temporary storage within a pod's lifecycle, not for persistent data.
- **Container restart != pod restart:** Container restarts within the same pod preserve emptyDir, but pod restarts do not.

**Consequences:**
- Test files placed by testers on Windows not visible after NFS server pod restart
- Files written via NFS mount disappear on pod recreation
- Development environment behavior diverges from production (data persistence)
- Loss of production parity: prod NAS survives restarts, dev simulator doesn't
- Confusion and frustration: "Why did my test files disappear?"

**Prevention:**
1. **NEVER** use plain emptyDir for NFS export path without sync mechanism
2. **DO** implement bidirectional sync between hostPath and emptyDir (e.g., rsync sidecar)
3. **DO** use StatefulSet with persistent volumes for NFS server pods
4. **DO** document emptyDir limitations prominently if used temporarily
5. **TEST** pod restart scenarios explicitly during development

**Detection:**
- Files disappear after `kubectl delete pod` or pod eviction
- NFS clients see "Stale file handle" errors after server pod restarts
- New files on Windows not visible via NFS after pod recreation
- Files written via NFS mount don't appear in Windows directories

**Sources:**
- [Kubernetes: Volumes - emptyDir](https://kubernetes.io/docs/concepts/storage/volumes/)
- [Understanding Kubernetes Volumes: emptyDir, hostPath, NFS](https://getting-started-with-kubernetes.hashnode.dev/understanding-kubernetes-volumes-using-emptydir-hostpath-and-nfs-for-persistent-storage)

---

### Pitfall 3: Multiple NFS Servers with Conflicting FSIDs

**What goes wrong:** When deploying 7 independent NAS servers, using duplicate fsid values or omitting fsid causes mysterious performance degradation, mount failures, or complete NFS server hangs over hours or days.

**Why it happens:**
- **Root cause:** NFS uses fsid (filesystem ID) to uniquely identify exported filesystems. Duplicate fsids confuse clients about which export they're accessing.
- **Silent failure:** Configuration appears to work initially, but degradation is gradual and hard to debug.
- **Export table confusion:** When two exports have the same fsid, only the first export actually gets mounted regardless of which was requested.

**Consequences:**
- NFS servers slowly grind to a halt (hours to days after deployment)
- Clients mount wrong export (mount nas-input-1 but get nas-input-2's data)
- Intermittent "Stale file handle" errors
- Performance degradation that's hard to diagnose
- Production parity broken: multiple NAS servers don't work independently

**Prevention:**
1. **ASSIGN** unique fsid values to each NAS server (e.g., nas-input-1 uses fsid=1, nas-input-2 uses fsid=2, etc.)
2. **DOCUMENT** fsid allocation in Helm chart comments or values.yaml
3. **AVOID** fsid=0 (reserved for NFSv4 root filesystem) unless creating root export
4. **TEMPLATE** fsid values in Helm chart based on instance name/number
5. **TEST** all 7 NAS servers simultaneously, not one at a time

**Example configuration:**
```yaml
# nas-input-1
NFS_EXPORT_0: "/data *(rw,sync,no_subtree_check,fsid=1)"

# nas-input-2
NFS_EXPORT_0: "/data *(rw,sync,no_subtree_check,fsid=2)"

# nas-backup
NFS_EXPORT_0: "/data *(rw,sync,no_subtree_check,fsid=10)"

# nas-output-1
NFS_EXPORT_0: "/data *(rw,sync,no_subtree_check,fsid=20)"
```

**Detection:**
- NFS server becomes unresponsive over time
- Mount attempts succeed but access wrong filesystem
- Logs show multiple exports with same fsid
- `showmount -e` shows exports but mounts fail or mount wrong path
- Cross-mounting: mounting server A gives server B's files

**Sources:**
- [Earl C. Ruby III: Setting up NFS FSID for multiple networks](https://earlruby.org/2022/01/setting-up-nfs-fsid-for-multiple-networks/)
- [Red Hat: How do I configure the fsid option in /etc/exports?](https://access.redhat.com/solutions/548083)
- [SUSE: NFS mounting incorrect NFS export](https://www.suse.com/support/kb/doc/?id=000017897)

---

### Pitfall 4: hostPath Pod Rescheduling to Different Node

**What goes wrong:** When using hostPath volumes, if a pod is rescheduled to a different Kubernetes node, it loses access to the Windows-mounted directory because hostPath is node-specific.

**Why it happens:**
- **Root cause:** hostPath volumes mount directories from the node's filesystem. Each node has its own filesystem tree; Windows mount only exists on the Minikube node where it was configured.
- **Kubernetes scheduling:** Pods can be rescheduled to any node in the cluster (eviction, node drain, scaling).
- **Minikube specificity:** Single-node Minikube hides this issue, but multi-node clusters expose it immediately.

**Consequences:**
- Pod starts on different node with empty hostPath directory
- NFS exports appear empty to clients
- Test files inaccessible until pod returns to original node
- Production environment (multi-node OCP) behaves differently than dev
- Manual intervention required to force pod back to correct node

**Prevention:**
1. **USE** node affinity to pin pods to the node with Windows mount
2. **CONFIGURE** `nodeSelector` or `nodeAffinity` in pod spec:
   ```yaml
   nodeSelector:
     kubernetes.io/hostname: minikube
   ```
3. **USE** PodAffinity with `topologyKey: kubernetes.io/hostname` for multi-pod scenarios
4. **DOCUMENT** node affinity requirements in deployment documentation
5. **TEST** pod rescheduling scenarios (delete pod, drain node if multi-node)

**Detection:**
- NFS exports suddenly empty after pod restart
- Pod running on different node than before (check `kubectl get pod -o wide`)
- Windows directory exists but pod cannot see it
- Mount path exists but shows no files

**Sources:**
- [Kubernetes: Assigning Pods to Nodes](https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/)
- [Kubernetes HostPath and Pod Scheduling](https://arun944.hashnode.dev/kubernetes-volumes-hostpath)
- [DEV Community: Difference between emptyDir and hostPath](https://dev.to/techworld_with_nana/difference-between-emptydir-and-hostpath-volume-types-in-kubernetes-286g)

---

## Moderate Pitfalls

Mistakes that cause delays, debugging sessions, or technical debt.

### Pitfall 5: NFS Server Requires Privileged Security Context

**What goes wrong:** NFS server pods fail to start or function without `privileged: true` or `CAP_SYS_ADMIN` capability, but granting these breaks security policies in restricted environments (OCP with SCCs, PSPs).

**Why it happens:**
- **Root cause:** NFS server needs to perform mount operations inside the container (mounting nfsd pseudo-filesystem, lockd, etc.). Linux requires CAP_SYS_ADMIN for mount operations.
- **Security trade-off:** CAP_SYS_ADMIN is one of the most powerful capabilities; security policies often prohibit it.
- **Container limitation:** Unlike running NFS on bare metal, containerized NFS must mount filesystems within a restricted environment.

**Consequences:**
- NFS server pod crashes with permission denied errors
- Security team rejects deployment due to privileged containers
- Cannot deploy to production OpenShift with default SCCs
- Workaround (allowPrivilegedContainer) increases security risk

**Prevention:**
1. **REQUEST** security policy exception early in project (don't discover at deployment time)
2. **DOCUMENT** security requirements and justification for privileged container
3. **CREATE** dedicated SecurityContextConstraint (OpenShift) or PodSecurityPolicy (Kubernetes)
4. **MINIMIZE** privilege scope: use `CAP_SYS_ADMIN` instead of full `privileged: true` if possible
5. **CONSIDER** running NFS outside cluster if security policies are too restrictive

**Example configuration:**
```yaml
securityContext:
  privileged: false
  capabilities:
    add:
      - SYS_ADMIN
      - DAC_READ_SEARCH
```

**Detection:**
- Container logs show: "mount: /proc/fs/nfsd: permission denied"
- Pod events show: "Error: container has runAsNonRoot and image will run as root"
- Security admission controller rejects pod creation
- `kubectl describe pod` shows "PodSecurityPolicy: unable to admit pod"

**Sources:**
- [Kubernetes: Security Context Best Practices](https://www.wiz.io/academy/kubernetes-security-context-best-practices)
- [ehough/docker-nfs-server GitHub](https://github.com/ehough/docker-nfs-server)
- [kubesec.io: CAP_SYS_ADMIN](https://kubesec.io/basics/containers-securitycontext-capabilities-add-index-sys-admin/)

---

### Pitfall 6: Static Port Requirements for Minikube + Windows

**What goes wrong:** NFS protocol uses dynamic RPC ports by default. When accessing NFS from Windows host through NodePort, dynamic ports cause connection timeouts because only explicit NodePorts are accessible.

**Why it happens:**
- **Root cause:** NFS uses RPC (Remote Procedure Call) which traditionally allocates random ports for mountd, statd, lockd services.
- **Minikube networking:** Only explicitly defined NodePort services are accessible from Windows host. Dynamic ports aren't exposed.
- **Windows firewall:** Even if ports were exposed, Windows firewall would block undefined ports.

**Consequences:**
- Mount commands hang or timeout
- `mount.nfs: Connection timed out` errors
- Works inside cluster but not from Windows host
- Intermittent failures: sometimes works, sometimes doesn't

**Prevention:**
1. **CONFIGURE** NFS server to use static ports:
   ```env
   NFSD_PORT=2049
   MOUNTD_PORT=32765
   STATD_PORT=32766
   LOCKD_PORT=32767
   ```
2. **EXPOSE** all NFS ports as NodePorts in Service:
   ```yaml
   ports:
     - port: 2049    # NFS
     - port: 32765   # mountd
     - port: 32766   # statd
     - port: 32767   # lockd
     - port: 111     # rpcbind
   ```
3. **TEST** from Windows host: `showmount -e <minikube-ip>`
4. **DOCUMENT** port requirements for testers/developers

**Detection:**
- Mount hangs indefinitely
- `rpcinfo -p <server>` shows dynamic ports
- `tcpdump` shows connection attempts to non-exposed ports
- Works with `kubectl port-forward` but not NodePort

**Sources:**
- [GitHub: minikube nfs mount / bad option](https://github.com/kubernetes/minikube/issues/8514)
- [Deploying Dynamic NFS Provisioning in Kubernetes](https://www.exxactcorp.com/blog/Troubleshooting/deploying-dynamic-nfs-provisioning-in-kubernetes)

---

### Pitfall 7: Minikube 9P Mount Performance Degradation

**What goes wrong:** When using `minikube mount` with 9p filesystem for Windows-to-Minikube directory sharing, performance degrades significantly with directories containing >600 files, causing NFS operations to timeout or hang.

**Why it happens:**
- **Root cause:** 9p protocol (Plan 9 filesystem) is not optimized for high file counts. Minikube uses 9p for cross-platform directory mounting.
- **Hyper-V limitation:** Hyper-V driver uses 9p internally, compounding performance issues.
- **Metadata operations:** NFS heavily uses metadata operations (stat, readdir) which are slow over 9p.

**Consequences:**
- NFS directory listings take 10+ seconds
- File operations timeout
- Developer experience degrades as test file count grows
- Cannot scale to realistic test datasets (thousands of files)

**Prevention:**
1. **LIMIT** file count in mounted directories (use subdirectories)
2. **STRUCTURE** test data hierarchically (split into subdirectories)
3. **CONSIDER** Docker driver instead of Hyper-V if WSL2 available (better performance)
4. **DOCUMENT** performance characteristics and file count limits
5. **IMPLEMENT** file count monitoring/alerting in test suites

**Detection:**
- Slow `ls` commands inside Minikube VM
- NFS operations timeout
- Mounting NFS share takes >30 seconds
- `df -T` shows 9p filesystem type on mount point

**Sources:**
- [Minikube: Mounting filesystems](https://minikube.sigs.k8s.io/docs/handbook/mount/)
- [GitHub: Minikube mount does not work (Windows 10 / Hyper-V)](https://github.com/kubernetes/minikube/issues/13535)
- [TheLinuxCode: Kubernetes Minikube 2026 Playbook](https://thelinuxcode.com/kubernetes-minikube-a-pragmatic-2026-playbook/)

---

### Pitfall 8: exportfs State Not Persistent Across Container Restarts

**What goes wrong:** Changes made to NFS exports via `exportfs` command inside running container are lost on container restart unless persisted to `/etc/exports` or configuration.

**Why it happens:**
- **Root cause:** `exportfs` updates in-memory kernel export table and `/var/lib/nfs/etab` file, but these are ephemeral in containers.
- **Container design:** Containers start fresh from image; runtime changes are lost.
- **NFS design:** `exportfs` was designed for persistent VMs/bare metal, not ephemeral containers.

**Consequences:**
- Manual export changes lost on pod restart
- Debugging confusion: "It worked yesterday, now it's broken"
- Export configuration drift between environments
- Manual intervention required after every restart

**Prevention:**
1. **CONFIGURE** exports via environment variables (e.g., `NFS_EXPORT_0`) that persist in pod spec
2. **NEVER** manually run `exportfs` inside running containers (won't persist)
3. **USE** ConfigMap for complex export configurations mounted at `/etc/exports.d/`
4. **VERIFY** exports are defined in persistent configuration, not runtime commands
5. **AUTOMATE** export configuration in container entrypoint script

**Detection:**
- Exports work, then disappear after pod restart
- `showmount -e` shows no exports after restart
- Container logs show exports configured, but external checks fail
- Manual `exportfs -r` required after every restart

**Sources:**
- [TrinixSoft: NFS Server Maintenance - Reloading exports](https://blog.trinixcs.com/index.php/2019/05/17/nfs-server-maintenance-reloading-the-etc-exports-without-restarting-the-server/)
- [exportfs man page](https://linux.die.net/man/8/exportfs)

---

### Pitfall 9: NFS Version Compatibility (NFSv3 vs NFSv4)

**What goes wrong:** Windows NFS client behavior differs between NFSv3 and NFSv4. WinNFSd (Windows NFS server) only supports NFSv3. Mismatched versions cause mount failures or permission issues.

**Why it happens:**
- **Root cause:** NFSv3 and NFSv4 are different protocols with different authentication, locking, and mount semantics.
- **Windows specificity:** Windows NFS client historically better supports NFSv3. NFSv4 support improved but has quirks.
- **Default configuration:** Most NFS server containers default to NFSv4; Windows clients may need NFSv3.

**Consequences:**
- Mount succeeds but files not accessible
- Permission denied errors even with `no_root_squash`
- UID/GID mapping issues
- Windows client hangs on mount

**Prevention:**
1. **SPECIFY** NFS version explicitly in client mount options:
   ```powershell
   # Windows: force NFSv3
   mount -o vers=3 \\192.168.1.100\data Z:
   ```
2. **ENABLE** both NFSv3 and NFSv4 in server configuration (don't disable versions unless necessary)
3. **TEST** with both protocol versions during development
4. **DOCUMENT** recommended mount options for Windows clients
5. **CONFIGURE** server: `NFS_DISABLE_VERSION_3=false` (enable v3)

**Detection:**
- Mount fails with protocol errors
- `showmount` works but `mount` fails
- Works from Linux, fails from Windows
- Logs show version negotiation failures

**Sources:**
- [GitHub: Minikube NFS mount to WinNFSd](https://github.com/winnfsd/winnfsd/issues/68)
- [Minikube: Add functionality for nfs mounts/shares](https://github.com/kubernetes/minikube/issues/2580)

---

## Minor Pitfalls

Mistakes that cause annoyance, confusion, or require workarounds but are easily fixable.

### Pitfall 10: DNS Resolution for NFS Servers in Kubernetes

**What goes wrong:** NFS clients cannot mount using Kubernetes service DNS names (e.g., `nas-input-1.file-simulator.svc.cluster.local`); must use IP addresses instead.

**Why it happens:**
- **Root cause:** NFS mount happens at kernel level, which doesn't use Kubernetes DNS resolver (CoreDNS).
- **Workaround limitation:** Even with `nfs.svc.cluster.local` FQDN, kernel DNS resolution may fail or timeout.

**Consequences:**
- Must hardcode IP addresses in PV definitions
- IP changes on service recreation break mounts
- PV definitions not portable between clusters

**Prevention:**
1. **USE** StatefulSet with headless service for stable DNS names
2. **CONFIGURE** PV with service ClusterIP (more stable than pod IP)
3. **DOCUMENT** that NFS uses IP, not DNS, in architecture docs
4. **ACCEPT** this limitation: it's NFS + Kubernetes design, not fixable

**Detection:**
- Mount attempts fail with "Name or service not known"
- `nslookup` resolves DNS name but mount still fails
- Must use IP address for mount to succeed

**Sources:**
- [GitHub: nfs: Failed to resolve server nfs-server.default.svc.cluster.local](https://github.com/kubernetes/minikube/issues/3417)
- [GitHub: Kubernetes Service not working with NFS between two pods](https://github.com/kubernetes/kubernetes/issues/74266)

---

### Pitfall 11: ReadWriteMany Access Mode Required for Multi-Pod Access

**What goes wrong:** If multiple pods need to access the same NFS export simultaneously, PVC must have `accessMode: ReadWriteMany` (RWX). Using `ReadWriteOnce` (RWO) causes scheduling failures.

**Why it happens:**
- **Root cause:** Kubernetes enforces access mode restrictions. RWO means "only one node can mount this volume at a time."
- **NFS capability:** NFS inherently supports concurrent access, but Kubernetes access modes are separate from filesystem capabilities.

**Consequences:**
- Second pod fails to schedule with "PVC is already in use"
- Multi-pod applications fail to deploy
- Confusion: NFS supports concurrent access but Kubernetes blocks it

**Prevention:**
1. **SET** `accessModes: [ReadWriteMany]` in PersistentVolume definition
2. **VERIFY** StorageClass supports RWX if using dynamic provisioning
3. **TEST** multi-pod scenarios during development
4. **DOCUMENT** access mode requirements in PV/PVC examples

**Example:**
```yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: nas-input-1-pv
spec:
  capacity:
    storage: 10Gi
  accessModes:
    - ReadWriteMany  # Required for NFS
  nfs:
    server: 192.168.49.2
    path: /data
```

**Detection:**
- Pod stuck in Pending state
- Events show: "PersistentVolumeClaim is already in use"
- Works with one pod, fails with two

**Sources:**
- [OpenEBS: Provisioning NFS PVCs](https://openebs.io/docs/Solutioning/read-write-many/nfspvc)
- [Kubernetes: Use NFS Storage](https://docs.mirantis.com/mke/3.4/ops/deploy-apps-k8s/persistent-storage/use-nfs-storage.html)

---

### Pitfall 12: no_root_squash Security Implications

**What goes wrong:** Using `no_root_squash` export option (required for development) creates security risk if NFS server is accessible outside trusted network.

**Why it happens:**
- **Root cause:** `no_root_squash` allows NFS clients to access files as root, bypassing normal UID/GID restrictions.
- **Development necessity:** Containers often run as root; without `no_root_squash`, all file operations fail with permission denied.
- **Security trade-off:** Convenience vs. security.

**Consequences:**
- Any NFS client can access/modify all files as root
- Accidental file deletion by test scripts
- Security audit failures
- Cannot deploy to production with this configuration

**Prevention:**
1. **DOCUMENT** that `no_root_squash` is development-only configuration
2. **RESTRICT** NFS access using firewall rules (only allow cluster network)
3. **USE** `root_squash` in production (map root to nobody)
4. **CONFIGURE** container to run as non-root user in production
5. **EDUCATE** team about security implications

**Production configuration:**
```bash
# Development
/data *(rw,sync,no_subtree_check,no_root_squash,fsid=1)

# Production
/data trusted-network(rw,sync,no_subtree_check,root_squash,fsid=1)
```

**Detection:**
- Security scanner flags privileged NFS exports
- Files owned by root accumulate on NFS server
- Accidental deletions or modifications

**Sources:**
- [Red Hat: The /etc/exports Configuration File](https://access.redhat.com/documentation/en-us/red_hat_enterprise_linux/5/html/deployment_guide/s1-nfs-server-config-exports)
- [exports man page](https://linux.die.net/man/5/exports)

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Initial NFS deployment | Pitfall #1: Cannot export Windows hostPath directly | Use emptyDir + sync pattern or accept emptyDir data loss during POC |
| Single server → 7 servers | Pitfall #3: Duplicate fsid values | Template fsid values based on server index (input-1=1, input-2=2, etc.) |
| Production deployment | Pitfall #5: Privileged container blocked by SCCs | Request SCC exception early; document security requirements |
| Windows client testing | Pitfall #9: NFSv3 vs NFSv4 compatibility | Explicitly configure protocol version; enable both on server |
| Multi-pod applications | Pitfall #11: ReadWriteOnce vs ReadWriteMany | Set accessMode: ReadWriteMany in all PV definitions |
| Performance tuning | Pitfall #7: 9p filesystem performance limits | Structure test data hierarchically; document file count limits |
| High availability | Pitfall #4: hostPath node affinity | Use StatefulSet + nodeAffinity or PV/PVC instead of hostPath |

---

## Architectural Decision Record: NFS + Windows Mount

**Problem:** NFS cannot directly export Windows-mounted CIFS/9p filesystems due to kernel limitations.

**Options Evaluated:**

1. **Direct export of hostPath** ❌
   - Blocked by kernel limitation (Pitfall #1)

2. **emptyDir without sync** ❌
   - Data loss on pod restart (Pitfall #2)
   - Breaks Windows-as-source-of-truth requirement

3. **emptyDir + rsync sidecar** ✅ Recommended
   - Pros: Works around export limitation, preserves Windows source of truth, survives container restart
   - Cons: Data loss on pod restart, sync latency, complexity
   - Use case: Development environment where occasional data loss acceptable

4. **StatefulSet + PersistentVolume** ⚠️ Partial solution
   - Pros: Data persists across pod restarts
   - Cons: Still cannot mount Windows directories directly; requires separate sync mechanism

5. **Run NFS outside cluster** ✅ Production alternative
   - Pros: No container limitations, better performance, real NAS simulation
   - Cons: Complexity, infrastructure requirements

**Recommendation for File Simulator Suite:**
- **Development (Minikube):** emptyDir + rsync sidecar + nodeAffinity (accept risk of data loss on pod restart)
- **Production (OCP):** Run NFS on dedicated RHEL nodes or use real NAS hardware

---

## Quick Reference: Troubleshooting Checklist

When NFS server fails to start or function:

- [ ] **Check filesystem type:** `df -T /data` - is it CIFS/9p/overlayfs? (Pitfall #1)
- [ ] **Check security context:** Does pod have `CAP_SYS_ADMIN` or `privileged: true`? (Pitfall #5)
- [ ] **Check fsid uniqueness:** Are any two servers using same fsid? (Pitfall #3)
- [ ] **Check node affinity:** Is pod on node with Windows mount? `kubectl get pod -o wide` (Pitfall #4)
- [ ] **Check exports configuration:** Is `NFS_EXPORT_0` env var set correctly?
- [ ] **Check port accessibility:** Can you telnet to 2049, 32765, 32766, 32767 from client? (Pitfall #6)
- [ ] **Check NFS version:** Are client and server using compatible versions (v3 vs v4)? (Pitfall #9)
- [ ] **Check access mode:** Is PVC using `ReadWriteMany` for multi-pod access? (Pitfall #11)

---

## Confidence Assessment

| Pitfall | Confidence | Evidence |
|---------|------------|----------|
| #1: Cannot export CIFS/9p/overlay | **HIGH** | Multiple authoritative sources (SUSE, Red Hat, kernel docs), matches observed behavior |
| #2: emptyDir data loss | **HIGH** | Kubernetes documentation, design specification |
| #3: fsid conflicts | **HIGH** | Multiple production incident reports, expert blog posts |
| #4: hostPath node affinity | **HIGH** | Kubernetes design, documented behavior |
| #5: Privileged security context | **HIGH** | NFS implementation requirement, security documentation |
| #6: Static port requirements | **MEDIUM** | Community reports, Minikube-specific limitation |
| #7: 9p performance degradation | **MEDIUM** | Minikube documentation, community experience reports |
| #8: exportfs state persistence | **HIGH** | NFS documentation, container design patterns |
| #9: NFS version compatibility | **MEDIUM** | Windows client behavior, community workarounds |
| #10: DNS resolution | **MEDIUM** | Kubernetes/NFS interaction, documented limitation |
| #11: RWX access mode | **HIGH** | Kubernetes specification, PVC access mode design |
| #12: no_root_squash security | **HIGH** | NFS security documentation, best practices |

---

## Gaps and Open Questions

**Verified with authoritative sources:**
- All critical pitfalls (#1-#4) verified with official documentation or expert sources
- Moderate pitfalls (#5-#9) have community evidence and technical explanations
- Minor pitfalls (#10-#12) documented in Kubernetes and NFS specifications

**Remaining uncertainties:**
- Performance benchmarks for 9p vs other mount types (LOW confidence - need empirical testing)
- Exact file count threshold for 9p degradation (documented as ">600" but varies by use case)
- Best practice for rsync sidecar implementation (no authoritative pattern found)

**Recommended validation:**
- Test fsid conflict scenarios explicitly (verify timing and symptoms)
- Benchmark 9p performance with realistic file counts for this project
- Prototype rsync sidecar pattern and measure sync latency/reliability

---

## Sources

### Critical Pitfall Sources
- [SUSE: exportfs returns error "does not support NFS export"](https://www.suse.com/support/kb/doc/?id=000021721)
- [Linux NFS: NFS re-export](https://wiki.linux-nfs.org/wiki/index.php/NFS_re-export)
- [Arch Linux: Can't export overlayfs with NFS](https://bbs.archlinux.org/viewtopic.php?id=192585)
- [Kubernetes: Volumes - emptyDir](https://kubernetes.io/docs/concepts/storage/volumes/)
- [Earl C. Ruby III: Setting up NFS FSID for multiple networks](https://earlruby.org/2022/01/setting-up-nfs-fsid-for-multiple-networks/)
- [Red Hat: How do I configure the fsid option in /etc/exports?](https://access.redhat.com/solutions/548083)
- [Kubernetes: Assigning Pods to Nodes](https://kubernetes.io/docs/concepts/scheduling-eviction/assign-pod-node/)

### Moderate Pitfall Sources
- [Kubernetes: Security Context Best Practices](https://www.wiz.io/academy/kubernetes-security-context-best-practices)
- [ehough/docker-nfs-server GitHub](https://github.com/ehough/docker-nfs-server)
- [GitHub: minikube nfs mount / bad option](https://github.com/kubernetes/minikube/issues/8514)
- [Minikube: Mounting filesystems](https://minikube.sigs.k8s.io/docs/handbook/mount/)
- [TheLinuxCode: Kubernetes Minikube 2026 Playbook](https://thelinuxcode.com/kubernetes-minikube-a-pragmatic-2026-playbook/)
- [TrinixSoft: NFS Server Maintenance](https://blog.trinixcs.com/index.php/2019/05/17/nfs-server-maintenance-reloading-the-etc-exports-without-restarting-the-server/)
- [GitHub: Minikube NFS mount to WinNFSd](https://github.com/winnfsd/winnfsd/issues/68)

### Minor Pitfall Sources
- [GitHub: nfs: Failed to resolve server](https://github.com/kubernetes/minikube/issues/3417)
- [OpenEBS: Provisioning NFS PVCs](https://openebs.io/docs/Solutioning/read-write-many/nfspvc)
- [Red Hat: The /etc/exports Configuration File](https://access.redhat.com/documentation/en-us/red_hat_enterprise_linux/5/html/deployment_guide/s1-nfs-server-config-exports)

### Additional Context
- [Kubernetes: HostPath and Pod Scheduling](https://arun944.hashnode.dev/kubernetes-volumes-hostpath)
- [Understanding Kubernetes Volumes: emptyDir, hostPath, NFS](https://getting-started-with-kubernetes.hashnode.dev/understanding-kubernetes-volumes-using-emptydir-hostpath-and-nfs-for-persistent-storage)
- [Linux Kernel: Overlay Filesystem](https://docs.kernel.org/filesystems/overlayfs.html)
