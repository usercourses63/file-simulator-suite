# Phase 1: Single NAS Validation - Research

**Researched:** 2026-01-29
**Domain:** Userspace NFS server deployment in Kubernetes without privileged mode
**Confidence:** HIGH

## Summary

This research investigates the unfs3 userspace NFS server pattern combined with init container + rsync to expose Windows directories via NFS in Kubernetes without privileged security contexts. The core challenge is that Linux NFS cannot directly export Windows-mounted CIFS/9p filesystems (kernel limitation), requiring a workaround pattern.

The standard approach is:
1. Use init container to rsync Windows hostPath -> emptyDir (Linux native filesystem)
2. Main container runs unfs3 userspace NFS server exporting the emptyDir
3. Configure with unique fsid per NAS instance to prevent server conflicts
4. Use minimal capabilities (NET_BIND_SERVICE, potentially DAC_READ_SEARCH) instead of privileged mode

This pattern successfully decouples the Windows mount from NFS export, working within kernel limitations while avoiding privileged containers.

**Primary recommendation:** Implement two-container pod with init container (rsync) + main container (unfs3 with -p -t -n 2049 flags) running with capabilities NET_BIND_SERVICE, avoiding privileged mode entirely.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| unfs3 | latest (from unfs3/unfs3 or macadmins/unfs3) | Userspace NFSv3 server | Only mature userspace NFS implementation that doesn't require kernel modules |
| alpine:latest | 3.x | Init container base | Lightweight, includes rsync via apk |
| rsync | 3.x | File synchronization | Standard Unix tool for preserving permissions/ownership during copy |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| busybox | latest | Debug/test container | For showmount -e verification tests |
| nfs-common | latest | NFS client tools | For mounting NFS exports from test pods |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| unfs3 | nfs-ganesha | Ganesha is more feature-rich but requires more complex configuration and often needs privileged mode for kernel interactions |
| unfs3 | erichough/nfs-server | Current failing solution - kernel NFS server that cannot export CIFS/9p mounts from Windows |
| rsync | cp -a | cp doesn't handle permissions/ownership as robustly, rsync has better error handling |

**Installation:**
```dockerfile
# Init container
FROM alpine:latest
RUN apk add --no-cache rsync

# NFS server container
FROM alpine:latest
RUN apk add --no-cache unfs3
```

## Architecture Patterns

### Recommended Project Structure (Helm Template)
```
templates/
├── nas-multi.yaml           # Multi-instance NAS deployments
│   ├── Deployment (2 containers: init + main)
│   ├── Service (ClusterIP with DNS name)
│   └── ConfigMap (exports configuration)
```

### Pattern 1: Init Container + rsync Sync
**What:** Two-container pod with init container performing one-time sync from hostPath to emptyDir before NFS server starts
**When to use:** When Windows directories need exposure via NFS (always in this project)

**Example:**
```yaml
# Source: Kubernetes official docs on init containers
# https://kubernetes.io/docs/concepts/workloads/pods/init-containers/
initContainers:
  - name: sync-windows-data
    image: alpine:latest
    command:
      - sh
      - -c
      - |
        apk add --no-cache rsync
        mkdir -p /nfs-data
        rsync -av --delete /windows-mount/ /nfs-data/
        echo "Sync complete: $(ls -la /nfs-data | wc -l) files"
    volumeMounts:
      - name: windows-data
        mountPath: /windows-mount
        readOnly: true
      - name: nfs-export
        mountPath: /nfs-data
volumes:
  - name: windows-data
    hostPath:
      path: /mnt/simulator-data/nas-test-1
      type: DirectoryOrCreate
  - name: nfs-export
    emptyDir: {}  # Default: disk-backed for persistence
```

### Pattern 2: unfs3 with Non-Privileged Configuration
**What:** Run unfs3 with specific flags to avoid portmapper and run on single port
**When to use:** Always for userspace NFS server deployment

**Example:**
```yaml
# Source: unfs3 man page (https://linux.die.net/man/8/unfsd)
# and macadmins/unfs3 Docker image patterns
containers:
  - name: nfs-server
    image: alpine:latest
    command:
      - sh
      - -c
      - |
        apk add --no-cache unfs3
        mkdir -p /etc
        echo '/data *(rw,sync,no_root_squash,fsid=1)' > /etc/exports
        unfsd -d -p -t -n 2049 -e /etc/exports
    # Flags explained:
    # -d: do not detach from terminal (required for container)
    # -p: do not register with portmapper (avoids rpcbind)
    # -t: TCP only (simplifies to single port, no UDP)
    # -n 2049: use port 2049 for NFS service
    # -e: specify exports file location
    ports:
      - containerPort: 2049
        protocol: TCP
    securityContext:
      capabilities:
        add:
          - NET_BIND_SERVICE  # Required for port 2049
        drop:
          - ALL  # Drop all other capabilities
      runAsNonRoot: false  # unfs3 needs some privileges
      allowPrivilegeEscalation: false
    volumeMounts:
      - name: nfs-export
        mountPath: /data
```

### Pattern 3: Unique fsid Per NAS Instance
**What:** Each NAS export uses unique fsid value to prevent server conflicts
**When to use:** Always when deploying multiple NAS instances

**Example:**
```yaml
# Source: Red Hat documentation on NFS fsid configuration
# https://access.redhat.com/solutions/548083
# Loop in Helm template to generate unique fsid per instance
{{- range $index, $nas := .Values.nasServers }}
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}-exports
data:
  exports: |
    /data *(rw,sync,no_root_squash,fsid={{ $nas.fsid | default (add $index 1) }})
{{- end }}
```

### Pattern 4: Service with Predictable DNS
**What:** Each NAS instance gets unique ClusterIP service with predictable DNS name
**When to use:** Always for service discovery within cluster

**Example:**
```yaml
# DNS name pattern: {release-name}-{nas-name}.{namespace}.svc.cluster.local
apiVersion: v1
kind: Service
metadata:
  name: {{ include "file-simulator.fullname" . }}-nas-test-1
  namespace: {{ include "file-simulator.namespace" . }}
spec:
  type: ClusterIP
  ports:
    - port: 2049
      targetPort: 2049
      protocol: TCP
      name: nfs
  selector:
    app.kubernetes.io/component: nas-test-1
```

### Anti-Patterns to Avoid
- **Using erichough/nfs-server or kernel NFS on Windows mounts:** Kernel NFS cannot export CIFS/9p filesystems, results in crashes or "stale file handle" errors
- **Using privileged: true:** Unnecessary with unfs3 userspace server, violates security requirements
- **Using emptyDir with medium: Memory:** Files won't persist across pod restarts (violates WIN-06 requirement)
- **Omitting -p flag on unfs3:** Will try to use rpcbind/portmapper, adds complexity and security surface
- **Sharing fsid values:** Multiple NFS servers with same fsid cause client confusion and mount failures

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| NFS server implementation | Custom NFSv3 protocol handler | unfs3 | NFS protocol is complex with edge cases around locking, statd, mountd - unfs3 is battle-tested |
| File sync with permissions | Custom copy script with cp | rsync -av | Preserving ownership, permissions, symlinks, and timestamps is error-prone; rsync handles all edge cases |
| Init container wait logic | Custom polling loop | Kubernetes init container guarantee | Init containers complete before main containers start by Kubernetes design |
| Health checking NFS | Custom protocol check | TCP liveness probe on 2049 | Simple TCP check sufficient for NFS availability, no need for protocol-level verification |
| Service discovery | IP addresses or environment variables | Kubernetes DNS | DNS names like nas-test-1.file-simulator.svc.cluster.local are more reliable than IPs |

**Key insight:** The init container + rsync + unfs3 pattern is well-established for exposing non-exportable filesystems via NFS. Custom implementations miss edge cases around permissions, locking, and protocol compliance.

## Common Pitfalls

### Pitfall 1: Attempting to Export Windows Mount Directly
**What goes wrong:** Kernel NFS servers (like erichough/nfs-server) cannot export CIFS or 9p mounted filesystems, resulting in crashes, "stale file handle" errors, or mount failures
**Why it happens:** Linux kernel NFS implementation requires local filesystems (ext4, xfs, etc.) and cannot handle remote filesystem semantics
**How to avoid:** Always use init container to copy Windows mount (hostPath) to emptyDir (local filesystem), then export the emptyDir
**Warning signs:**
- Error messages mentioning "stale file handle"
- NFS server pod crash loops
- Logs showing filesystem type errors

### Pitfall 2: Wrong emptyDir Configuration
**What goes wrong:** Using `emptyDir: {medium: Memory}` causes data loss on pod restarts
**Why it happens:** Memory-backed emptyDir uses tmpfs which is cleared on pod restart
**How to avoid:** Use default disk-backed emptyDir: `emptyDir: {}` or explicitly set `emptyDir: {medium: ""}`
**Warning signs:**
- Files disappear after pod restart
- Init container re-syncs but data not visible
- Memory usage spikes unexpectedly

### Pitfall 3: Insufficient Capabilities for Port 2049
**What goes wrong:** Container cannot bind to port 2049 without NET_BIND_SERVICE capability
**Why it happens:** Ports under 1024 are privileged and require specific capability
**How to avoid:** Add NET_BIND_SERVICE capability in securityContext, OR use unprivileged port with -n flag (e.g., -n 2049 with NET_BIND_SERVICE, or -n 20049 without)
**Warning signs:**
- "Permission denied" errors on port binding
- NFS server fails to start
- Logs showing "cannot bind to port 2049"

### Pitfall 4: Not Using -p Flag (Portmapper Registration)
**What goes wrong:** unfs3 tries to register with rpcbind/portmapper, adding complexity and potential for failures
**Why it happens:** Default unfs3 behavior is to register with portmapper for NFSv3 protocol compliance
**How to avoid:** Use -p flag to disable portmapper registration, combine with -t for TCP-only operation
**Warning signs:**
- Errors about rpcbind not available
- Multiple ports required for NFS operation
- Mount fails with "RPC: Port mapper failure"

### Pitfall 5: Duplicate fsid Values
**What goes wrong:** Multiple NFS servers with same fsid confuse NFS clients, causing mounts to fail or access wrong data
**Why it happens:** fsid is how NFS identifies filesystems; duplicates break client caching and file handle resolution
**How to avoid:** Assign unique fsid per NAS instance (use Helm index: `fsid={{ add $index 1 }}`), document values
**Warning signs:**
- Client mounts wrong NFS export
- Stale file handle errors
- Files from one NAS appearing in another mount

### Pitfall 6: Missing Sync on Init Container
**What goes wrong:** Init container succeeds but files not visible in NFS export
**Why it happens:** rsync source/destination paths incorrect, or volume mount paths mismatched
**How to avoid:** Use explicit paths with trailing slashes: `rsync -av --delete /source/ /dest/`, verify with `ls` before exit
**Warning signs:**
- Init container completes successfully
- NFS export is empty or outdated
- Windows files not visible via NFS mount

### Pitfall 7: no_root_squash Security Implications
**What goes wrong:** Client with root access can modify any files, create SUID binaries for privilege escalation
**Why it happens:** no_root_squash disables the security feature that maps root to nobody
**How to avoid:** Accept the risk for development environment, or use root_squash in production (but complicates testing)
**Warning signs:**
- Security audit warnings
- Unexpected file ownership changes
- Files owned by root that shouldn't be

## Code Examples

Verified patterns from official sources:

### Complete Pod Spec with Init Container + unfs3
```yaml
# Source: Kubernetes init containers documentation and unfs3 configuration patterns
# https://kubernetes.io/docs/concepts/workloads/pods/init-containers/
# https://linux.die.net/man/8/unfsd
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nas-test-1
spec:
  replicas: 1
  template:
    spec:
      # Init container: sync Windows -> emptyDir
      initContainers:
        - name: sync-windows-data
          image: alpine:latest
          command:
            - sh
            - -c
            - |
              set -e
              echo "Installing rsync..."
              apk add --no-cache rsync

              echo "Creating NFS export directory..."
              mkdir -p /nfs-data

              echo "Syncing from Windows mount to NFS export..."
              rsync -av --delete /windows-mount/ /nfs-data/

              echo "Sync complete. Files in export:"
              ls -la /nfs-data | head -20
              echo "Total files: $(find /nfs-data -type f | wc -l)"
          volumeMounts:
            - name: windows-data
              mountPath: /windows-mount
              readOnly: true
            - name: nfs-export
              mountPath: /nfs-data
          securityContext:
            runAsNonRoot: false
            allowPrivilegeEscalation: false

      # Main container: unfs3 NFS server
      containers:
        - name: nfs-server
          image: alpine:latest
          command:
            - sh
            - -c
            - |
              set -e
              echo "Installing unfs3..."
              apk add --no-cache unfs3

              echo "Creating exports file..."
              mkdir -p /etc
              echo '/data *(rw,sync,no_root_squash,fsid=1)' > /etc/exports
              cat /etc/exports

              echo "Starting NFS server..."
              echo "Flags: -d (foreground) -p (no portmap) -t (TCP only) -n 2049 (port)"
              unfsd -d -p -t -n 2049 -e /etc/exports
          ports:
            - name: nfs
              containerPort: 2049
              protocol: TCP
          volumeMounts:
            - name: nfs-export
              mountPath: /data
          securityContext:
            capabilities:
              add:
                - NET_BIND_SERVICE
              drop:
                - ALL
            runAsNonRoot: false
            allowPrivilegeEscalation: false
          livenessProbe:
            tcpSocket:
              port: 2049
            initialDelaySeconds: 30
            periodSeconds: 10
          readinessProbe:
            tcpSocket:
              port: 2049
            initialDelaySeconds: 10
            periodSeconds: 5
          resources:
            requests:
              memory: "64Mi"
              cpu: "50m"
            limits:
              memory: "256Mi"
              cpu: "200m"

      volumes:
        - name: windows-data
          hostPath:
            path: /mnt/simulator-data/nas-test-1
            type: DirectoryOrCreate
        - name: nfs-export
          emptyDir: {}  # Disk-backed (default)
```

### NFS Export File with Recommended Options
```bash
# Source: Linux exports(5) man page
# https://linux.die.net/man/5/exports
# Format: <path> <client>(<options>)

# Production-like configuration (less secure, matches OCP)
/data *(rw,sync,no_root_squash,fsid=1)

# Options explained:
# rw           - read-write access
# sync         - synchronous writes (slower but safer)
# no_root_squash - root on client = root on server (SECURITY RISK)
# fsid=1       - unique filesystem identifier (increment per NAS)

# More secure alternative (if testing allows):
/data *(rw,sync,root_squash,all_squash,anonuid=1000,anongid=1000,fsid=1)
```

### rsync Command with Full Preservation
```bash
# Source: rsync man page and archive mode documentation
# https://linux.die.net/man/1/rsync
# https://www.zyxware.com/articles/3660/how-to-preserve-permissions-ownership-timestamp-archive-mode-in-rsync-using-rsync-a

# Archive mode (-a) includes: -rlptgoD
# -r: recursive
# -l: copy symlinks as symlinks
# -p: preserve permissions
# -t: preserve modification times
# -g: preserve group
# -o: preserve owner (needs root)
# -D: preserve device files and special files

rsync -av --delete /windows-mount/ /nfs-data/

# Additional options for troubleshooting:
# --progress: show progress during transfer
# --verbose: increase verbosity
# --dry-run: test without making changes
# --itemize-changes: show detailed changes

# Verification after sync:
ls -la /nfs-data
find /nfs-data -type f | wc -l
```

### Helm Template Pattern for Multi-Instance NAS
```yaml
# Source: Existing ftp-multi.yaml pattern in codebase
# Adapted for NAS with init container pattern

{{- if .Values.nasServers }}
{{- range $index, $nas := .Values.nasServers }}
{{- if $nas.enabled }}
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}
  namespace: {{ include "file-simulator.namespace" $ }}
  labels:
    {{- include "file-simulator.labels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
    simulator.protocol: nfs
    simulator.instance: "{{ $index }}"
spec:
  replicas: 1
  selector:
    matchLabels:
      {{- include "file-simulator.selectorLabels" $ | nindent 6 }}
      app.kubernetes.io/component: {{ $nas.name }}
  template:
    metadata:
      labels:
        {{- include "file-simulator.selectorLabels" $ | nindent 8 }}
        app.kubernetes.io/component: {{ $nas.name }}
    spec:
      initContainers:
        - name: sync-windows-data
          image: {{ $.Values.nas.initImage.repository }}:{{ $.Values.nas.initImage.tag }}
          command:
            - sh
            - -c
            - |
              set -e
              apk add --no-cache rsync
              mkdir -p /nfs-data
              rsync -av --delete /windows-mount/ /nfs-data/
              echo "Synced $(find /nfs-data -type f | wc -l) files"
          volumeMounts:
            - name: windows-data
              mountPath: /windows-mount
              readOnly: true
            - name: nfs-export
              mountPath: /nfs-data

      containers:
        - name: nfs-server
          image: {{ $nas.image.repository }}:{{ $nas.image.tag }}
          command:
            - sh
            - -c
            - |
              apk add --no-cache unfs3
              echo '/data *(rw,sync,no_root_squash,fsid={{ $nas.fsid | default (add $index 1) }})' > /etc/exports
              unfsd -d -p -t -n 2049 -e /etc/exports
          ports:
            - name: nfs
              containerPort: 2049
              protocol: TCP
          volumeMounts:
            - name: nfs-export
              mountPath: /data
          securityContext:
            capabilities:
              add:
                - NET_BIND_SERVICE
              drop:
                - ALL
          resources:
            {{- toYaml $nas.resources | nindent 12 }}

      volumes:
        - name: windows-data
          hostPath:
            path: {{ $.Values.global.storage.hostPath }}/{{ $nas.name }}
            type: DirectoryOrCreate
        - name: nfs-export
          emptyDir: {}
---
apiVersion: v1
kind: Service
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}
  namespace: {{ include "file-simulator.namespace" $ }}
  labels:
    {{- include "file-simulator.labels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
spec:
  type: ClusterIP
  ports:
    - port: 2049
      targetPort: nfs
      protocol: TCP
      name: nfs
  selector:
    {{- include "file-simulator.selectorLabels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
{{- end }}
{{- end }}
{{- end }}
```

### Testing NFS Mount from Client Pod
```yaml
# Source: Kubernetes NFS mounting documentation
apiVersion: v1
kind: Pod
metadata:
  name: nfs-test-client
spec:
  containers:
    - name: test
      image: busybox:latest
      command:
        - sh
        - -c
        - |
          # Install NFS client tools
          apk add --no-cache nfs-utils

          # Create mount point
          mkdir -p /mnt/nfs

          # Test NFS server connectivity
          echo "Testing NFS server connectivity..."
          nc -zv file-sim-nas-test-1.file-simulator.svc.cluster.local 2049

          # Mount NFS export
          echo "Mounting NFS export..."
          mount -t nfs -o nfsvers=3,tcp file-sim-nas-test-1.file-simulator.svc.cluster.local:/data /mnt/nfs

          # List files
          echo "Files in NFS mount:"
          ls -la /mnt/nfs

          # Test write
          echo "Testing write access..."
          echo "test" > /mnt/nfs/test-file.txt
          cat /mnt/nfs/test-file.txt

          # Keep pod alive
          sleep 3600
      securityContext:
        capabilities:
          add:
            - SYS_ADMIN  # Required for mount operation
        privileged: true  # Client needs privilege for mount
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| erichough/nfs-server (kernel NFS) | unfs3 (userspace NFS) | 2026 (this project) | Enables Windows mount export without privileged mode |
| Direct export of hostPath | Init container + emptyDir pattern | Long-established | Works around kernel limitation on CIFS/9p export |
| privileged: true | capabilities: [NET_BIND_SERVICE] | Kubernetes 1.16+ | Reduces security attack surface |
| NFSv3 with rpcbind | NFSv3 with -p flag (no portmapper) | Always possible | Simplifies deployment, single port operation |
| Multiple ports (111, 2049, mountd, statd) | Single port 2049 with -p -t flags | N/A | Easier firewall rules, simpler networking |
| emptyDir medium: Memory | emptyDir (disk-backed) | N/A | Files persist across pod restarts |

**Deprecated/outdated:**
- **erichough/nfs-server for Windows mounts:** Kernel NFS cannot export CIFS/9p filesystems, replaced by unfs3 userspace server
- **Privileged containers for NFS:** Unnecessary with userspace implementation and NET_BIND_SERVICE capability
- **rpcbind/portmapper for NFSv3:** Can be disabled with -p flag, simpler single-port operation
- **NFSv4 attempts with unfs3:** unfs3 is NFSv3 only, don't try to configure NFSv4 (use different server if needed)

## Open Questions

Things that couldn't be fully resolved:

1. **Does unfs3 require CAP_DAC_READ_SEARCH capability?**
   - What we know: CAP_DAC_READ_SEARCH allows bypassing file read permissions; userspace servers typically don't need it
   - What's unclear: Whether unfs3 specifically needs it for serving files owned by different users
   - Recommendation: Start without it, add only if permission errors occur; test with files owned by various uids/gids

2. **What's the performance impact of rsync on every pod restart?**
   - What we know: rsync is efficient (only copies changed files), but large directories take time
   - What's unclear: How many files are typically in Windows directories, sync duration
   - Recommendation: Add timing logs to init container, consider readiness probe delay based on observed sync times

3. **Should we use Alpine or different base image for unfs3?**
   - What we know: Alpine is lightweight, has unfs3 in apk repos
   - What's unclear: If pre-built unfs3 images (macadmins/unfs3) are more stable/tested
   - Recommendation: Start with Alpine + apk install for transparency, switch to pre-built if issues arise

4. **How to handle Windows file ownership mapping to Linux uid/gid?**
   - What we know: Windows files mounted in Minikube may have unexpected uid/gid
   - What's unclear: Does rsync preserve incorrect ownership, causing NFS client access issues
   - Recommendation: Test with `ls -ln` in both Windows mount and NFS export; may need rsync --no-owner --no-group flags

5. **Is there a race condition with 30-second file visibility requirement?**
   - What we know: Files appear in Windows directory, rsync happens at pod start or manually triggered
   - What's unclear: How files appear "within 30 seconds" if rsync only runs at pod start
   - Recommendation: Clarify requirement - either files are static (synced at start) or need continuous sync (sidecar pattern instead of init)

## Sources

### Primary (HIGH confidence)
- [unfs3 official repository](https://github.com/unfs3/unfs3) - Core userspace NFS server implementation
- [unfsd man page documentation](https://linux.die.net/man/8/unfsd) - Command-line options and configuration
- [Kubernetes Init Containers official documentation](https://kubernetes.io/docs/concepts/workloads/pods/init-containers/) - Init container patterns
- [Linux capabilities man page](https://man7.org/linux/man-pages/man7/capabilities.7.html) - NET_BIND_SERVICE and capability system
- [rsync man page](https://linux.die.net/man/1/rsync) - Archive mode and preservation options

### Secondary (MEDIUM confidence)
- [Red Hat: NFS fsid configuration](https://access.redhat.com/solutions/548083) - Unique fsid requirement and format
- [Kubernetes securityContext documentation](https://kubernetes.io/docs/tasks/configure-pod-container/security-context/) - Capabilities and security settings
- [Understanding NFS Port 2049](https://www.howtouselinux.com/post/nfs-port) - NFS port requirements
- [NFS exports options (GoLinuxCloud)](https://www.golinuxcloud.com/unix-linux-nfs-mount-options-example/) - Export option meanings
- [Kubernetes hostPath permissions guide](https://copyprogramming.com/howto/how-can-i-set-the-hostpath-volume-permission-on-kubernetes) - hostPath vs emptyDir behavior
- [rsync preserve permissions guide](https://www.zyxware.com/articles/3660/how-to-preserve-permissions-ownership-timestamp-archive-mode-in-rsync-using-rsync-a) - Archive mode details

### Secondary (Linux/NFS Protocol)
- [NFSv3 vs NFSv4 differences](https://community.netapp.com/t5/Tech-ONTAP-Blogs/NFSv3-and-NFSv4-What-s-the-difference/ba-p/441316) - Single port vs multiple ports
- [showmount and exportfs usage](https://www.jamescoyle.net/how-to/1019-view-available-exports-on-an-nfs-server) - Verification commands
- [NFS health check with nfsstat and rpcinfo](https://docs.oracle.com/cd/E19455-01/806-0916/6ja8539fs/index.html) - Troubleshooting tools

### Tertiary (LOW confidence - general patterns)
- [The Geek Diary: NFS no_root_squash security](https://www.thegeekdiary.com/basic-nfs-security-nfs-no_root_squash-and-suid/) - Security implications (not 2026)
- [HackTricks: NFS privilege escalation](https://book.hacktricks.xyz/linux-hardening/privilege-escalation/nfs-no_root_squash-misconfiguration-pe) - Attack vectors with no_root_squash
- [Minikube mount documentation](https://minikube.sigs.k8s.io/docs/handbook/mount/) - Windows directory mounting (9p limitations)

## Metadata

**Confidence breakdown:**
- Standard stack (unfs3, rsync, init container): HIGH - Official documentation and established patterns
- Architecture (init + main container, two volumes): HIGH - Kubernetes standard practice, verified in official docs
- Pitfalls (Windows mount export, emptyDir config): HIGH - Based on kernel NFS limitations (documented) and Kubernetes volume behavior
- Security (capabilities, non-privileged): MEDIUM - Capability system well-documented, but unfs3-specific needs require testing
- Performance (rsync timing, file counts): LOW - Depends on actual usage patterns and Windows directory sizes

**Research date:** 2026-01-29
**Valid until:** 2026-03-01 (30 days) - unfs3 is mature and stable, patterns unlikely to change rapidly

**Notes:**
- Kubernetes API stable (used features in v1.16+)
- unfs3 last updated 2024, no active development but stable
- rsync is decades-old stable Unix tool
- Pattern applies to any Kubernetes cluster (Minikube, OpenShift, cloud providers)
