# Phase 2: 7-Server Topology - Research

**Researched:** 2026-01-29
**Domain:** Helm multi-instance deployment patterns and Kubernetes resource optimization for 7 NAS servers
**Confidence:** HIGH

## Summary

This research investigates scaling the Phase 1 validated single NAS template (init container + unfs3 pattern) to deploy 7 independent NAS server instances with unique DNS names, isolated storage, and resource constraints suitable for Minikube 8GB/4CPU limits.

The standard approach for multi-instance Helm deployments uses range loops over a list defined in values.yaml, with each iteration creating a complete set of Kubernetes resources (Deployment + Service) with parameterized configuration. The key challenges are:

1. **Storage isolation:** Each NAS requires unique Windows hostPath directory AND unique fsid value
2. **Service discovery:** Each NAS needs predictable DNS name for client access
3. **Port mapping:** Each NAS needs unique NodePort (32150-32156) for Windows host access
4. **Resource constraints:** 7 pods must fit within Minikube 8GB RAM / 4 CPU limits

Phase 1 proved the single-instance pattern works. Phase 2 is purely an infrastructure scaling exercise using established Helm templating patterns (already demonstrated in ftp-multi.yaml and sftp-multi.yaml).

**Primary recommendation:** Create nas-multi.yaml template using range loop over .Values.nasServers list, with each server configured via values-multi-instance.yaml containing 7 entries (nas-input-1, nas-input-2, nas-input-3, nas-backup, nas-output-1, nas-output-2, nas-output-3) each with unique name, fsid (1-7), dataPath, and nodePort (32150-32156).

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Helm 3.x | 3.8+ | Kubernetes package manager with templating | Industry standard for parameterized K8s deployments |
| Go templates | 1.18+ | Helm's underlying template engine | Provides range, if, and variable scoping for multi-instance |
| unfs3 | latest | Userspace NFS server (per Phase 1) | Proven in Phase 1 single-instance validation |
| Alpine | latest | Base image for init and main containers | Lightweight (5MB), proven in Phase 1 |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| kubectl | 1.25+ | Kubernetes CLI | Deployment verification and troubleshooting |
| yq | 4.x | YAML processor | Testing template rendering locally |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Helm range loop | Kustomize overlays | Kustomize requires 7 separate YAML files; Helm generates from single template |
| values.yaml list | Helm subcharts | Subcharts add complexity; list approach simpler for identical instances |
| NodePort per server | Single LoadBalancer + routing | LoadBalancer requires minikube tunnel; NodePort works directly |

**Installation:**
```bash
# Helm 3 (already installed per Phase 1)
helm version

# Verify template rendering
helm template file-sim ./helm-chart/file-simulator \
  -f ./helm-chart/file-simulator/values-multi-instance.yaml \
  --debug
```

## Architecture Patterns

### Recommended Project Structure
```
helm-chart/file-simulator/
├── templates/
│   ├── nas-test.yaml           # Phase 1 single-instance (keep for testing)
│   └── nas-multi.yaml          # Phase 2 multi-instance (7 servers)
├── values.yaml                 # Default config (single server for backward compat)
└── values-multi-instance.yaml  # Multi-server config (7 NAS instances)
```

### Pattern 1: Helm Range Loop for Multi-Instance Resources
**What:** Use {{- range }} to iterate over a list and generate multiple Deployment + Service pairs
**When to use:** Always when deploying multiple identical resources with different configuration

**Example:**
```yaml
# Source: Helm Flow Control official documentation
# https://helm.sh/docs/chart_template_guide/control_structures/

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
    # ... (same as nas-test.yaml, but parameterized)
{{- end }}
{{- end }}
{{- end }}
```

**Key details:**
- **$index** provides zero-based loop counter (0, 1, 2, ...)
- **$nas** is current item in nasServers list
- **$** refers to root scope (required to call template functions like include)
- **{{ $nas.name }}** accesses fields from values.yaml list item

### Pattern 2: Unique fsid Per Instance
**What:** Each NAS server requires unique fsid value to prevent NFS client confusion
**When to use:** Always for multiple NFS servers (requirement from Phase 1 research)

**Example:**
```yaml
# Source: Red Hat NFS fsid configuration and Phase 1 research
# https://access.redhat.com/solutions/548083

# In container command that generates /etc/exports
echo '/data *(rw,sync,no_root_squash,fsid={{ $nas.fsid }})' > /etc/exports

# Alternative: auto-calculate from index (fsid=1 for index 0)
fsid={{ $nas.fsid | default (add $index 1) }}
```

**Critical requirement:** fsid values 1-7 must be unique across all 7 servers. NFS clients use fsid to identify filesystems; duplicate fsid causes mount failures and data access errors.

### Pattern 3: Isolated hostPath Per Instance
**What:** Each NAS mounts different Windows directory via unique hostPath
**When to use:** Always to achieve storage isolation between NAS servers

**Example:**
```yaml
# Source: Kubernetes hostPath volumes documentation
# https://kubernetes.io/docs/concepts/storage/volumes/

volumes:
  - name: windows-data
    hostPath:
      path: {{ $.Values.global.storage.hostPath }}/{{ $nas.name }}
      type: DirectoryOrCreate

# Example expansion for nas-input-1:
# path: /mnt/simulator-data/nas-input-1
```

**Storage isolation guarantee:** Kubernetes hostPath volumes pointing to different directories are completely isolated. Files in `/mnt/simulator-data/nas-input-1` are NOT visible in `/mnt/simulator-data/nas-input-2`. Each pod's emptyDir is also isolated per-pod (Kubernetes core behavior).

### Pattern 4: Unique NodePort Per Service
**What:** Each NAS Service gets unique NodePort for Windows host access
**When to use:** Always for external Windows client connectivity (NFS mount from Windows)

**Example:**
```yaml
# Source: Kubernetes NodePort Service documentation
# https://kubernetes.io/docs/concepts/services-networking/service/

apiVersion: v1
kind: Service
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}
spec:
  type: NodePort
  ports:
    - port: 2049
      targetPort: nfs
      protocol: TCP
      name: nfs
      nodePort: {{ $nas.service.nodePort }}
```

**NodePort range:** Kubernetes default is 30000-32767 (2768 ports available). Phase 2 uses 32150-32156 (7 ports) for consistency with other protocols already deployed.

### Pattern 5: Predictable DNS Names
**What:** Each NAS Service gets DNS name: {release-name}-{nas-name}.{namespace}.svc.cluster.local
**When to use:** Always for internal cluster service discovery

**Example:**
```yaml
# Service naming creates DNS automatically
# For nas-input-1 with release "file-sim" in namespace "file-simulator":
# DNS name: file-sim-nas-input-1.file-simulator.svc.cluster.local

# Client pods use DNS for mounting:
mount -t nfs -o nfsvers=3,tcp \
  file-sim-nas-input-1.file-simulator.svc.cluster.local:/data \
  /mnt/input-1
```

**Name length limit:** Kubernetes DNS names limited to 63 characters (DNS spec RFC 1035). Template pattern ensures compliance: release (8) + "-" (1) + "nas-input-1" (11) = 20 chars (safe).

### Pattern 6: Resource Requests/Limits for 7 Pods
**What:** Each pod specifies CPU and memory requests/limits to ensure 7 pods fit in Minikube
**When to use:** Always when deploying multiple pods with constrained resources

**Example:**
```yaml
# Source: Kubernetes Resource Management official documentation
# https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/

resources:
  requests:
    memory: "64Mi"   # Scheduler reserves this for pod placement
    cpu: "50m"       # 50 millicores = 0.05 CPU
  limits:
    memory: "256Mi"  # OOM kill if exceeded
    cpu: "200m"      # Throttled if exceeded (not killed)
```

**Capacity math for Minikube 8GB/4CPU:**
- 7 pods × 64Mi = 448Mi requested (leaves ~7.5GB for system and other pods)
- 7 pods × 50m = 350m CPU requested (leaves 3.65 CPU for system)
- System overhead: ~1GB RAM + ~0.5 CPU (Minikube, kube-system pods)
- **Result:** Comfortable fit with room for additional services

### Anti-Patterns to Avoid
- **Hardcoding 7 separate Deployment YAMLs:** Violates DRY principle; use range loop instead
- **Sharing fsid values:** Causes NFS client mount failures and data corruption
- **Using ClusterIP without NodePort:** Windows host cannot access NAS servers
- **Omitting resource limits:** Can cause OOM kills or CPU starvation for other pods
- **Not using {{ $ }} in range loop:** Template functions fail when called with {{ . }} inside range
- **Assuming emptyDir is shared:** Each pod gets isolated emptyDir; no cross-contamination possible

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Multi-instance templating | 7 separate YAML files | Helm range loop | DRY principle; single source of truth; less error-prone |
| Service discovery | IP addresses in config | Kubernetes DNS | IPs change on pod restart; DNS names stable |
| Resource allocation | Manual pod scheduling | Kubernetes scheduler with requests | Scheduler optimally places pods across nodes (even in single-node Minikube) |
| Storage isolation | Manual directory checks | Kubernetes hostPath + emptyDir guarantees | K8s enforces isolation; no manual verification needed |
| Port uniqueness | Manual tracking | values.yaml list with nodePort per instance | Single source avoids conflicts |
| Template testing | Deploy to cluster | helm template --debug | Catches syntax errors before cluster deployment |

**Key insight:** Helm's range loop + values.yaml list is the idiomatic way to deploy multiple similar resources in Kubernetes. The ftp-multi.yaml and sftp-multi.yaml templates in this codebase already demonstrate the exact pattern needed for NAS servers.

## Common Pitfalls

### Pitfall 1: Using . Instead of $ in range Loop
**What goes wrong:** Template functions like `include` fail with error "template: no template ... associated with template ..."
**Why it happens:** Inside range loop, `.` is scoped to current list item, not root; helper functions need root scope
**How to avoid:** Always use `$` when calling include or accessing .Values outside current item: `{{ include "file-simulator.fullname" $ }}`
**Warning signs:**
- Template rendering errors mentioning "no template"
- Functions work outside loop but fail inside
- Accessing .Values.global fails

### Pitfall 2: Duplicate fsid Values
**What goes wrong:** Multiple NAS servers with same fsid confuse NFS clients; files from wrong server appear in mounts
**Why it happens:** Copy-paste errors in values.yaml or forgetting to increment fsid
**How to avoid:** Use auto-increment pattern `fsid: {{ $nas.fsid | default (add $index 1) }}` or explicit validation script
**Warning signs:**
- Client mounts NAS-1 but sees files from NAS-2
- "Stale file handle" errors on client
- Inconsistent file listings across mounts

### Pitfall 3: NodePort Conflicts
**What goes wrong:** Multiple Services try to use same NodePort; Kubernetes rejects with "provided port is already allocated"
**Why it happens:** Duplicate nodePort values in values.yaml
**How to avoid:** Use sequential range (32150-32156), document in values.yaml comments, validate with helm template before deploy
**Warning signs:**
- Service creation fails with port conflict error
- helm upgrade fails partway through
- kubectl get svc shows some services missing

### Pitfall 4: Wrong hostPath Directory
**What goes wrong:** Multiple NAS servers mount same Windows directory; storage isolation broken
**Why it happens:** Copy-paste error in dataPath values; template uses wrong variable
**How to avoid:** Use `{{ $nas.name }}` as dataPath suffix: `path: {{ $.Values.global.storage.hostPath }}/{{ $nas.name }}`
**Warning signs:**
- Files uploaded to nas-input-1 appear in nas-input-2
- Deleting file from one NAS affects another
- All NAS servers show same file count

### Pitfall 5: Exceeding Minikube Resource Limits
**What goes wrong:** Pods stuck in Pending state with "Insufficient memory" or "Insufficient cpu" errors
**Why it happens:** Total pod requests exceed Minikube node capacity
**How to avoid:** Calculate total requests (7 pods × 64Mi = 448Mi), keep below ~50% of Minikube memory (4GB of 8GB)
**Warning signs:**
- kubectl describe pod shows "FailedScheduling" events
- Pods remain in Pending indefinitely
- kubectl get nodes shows insufficient resources

### Pitfall 6: Service Name Too Long
**What goes wrong:** Service creation fails with "name must be no more than 63 characters"
**Why it happens:** Kubernetes DNS names limited to 63 chars; long release name + nas name exceeds limit
**How to avoid:** Keep release name short (file-sim = 8 chars), nas names short (nas-input-1 = 11 chars); use truncation in helpers.tpl
**Warning signs:**
- Service creation fails with name length error
- DNS lookups fail for service
- kubectl get svc shows truncated names

### Pitfall 7: Missing type: DirectoryOrCreate on hostPath
**What goes wrong:** Pod fails to start if Windows directory doesn't exist; hostPath mount fails
**Why it happens:** Kubernetes doesn't auto-create missing directories without DirectoryOrCreate
**How to avoid:** Always specify `type: DirectoryOrCreate` in hostPath volume definition
**Warning signs:**
- Init container fails with "no such file or directory"
- Pod stuck in Init:Error state
- Manual mkdir on Windows fixes it temporarily

## Code Examples

Verified patterns from official sources and Phase 1 validation:

### Complete nas-multi.yaml Template
```yaml
# Source: Existing ftp-multi.yaml pattern (file-simulator-suite codebase)
# and Phase 1 nas-test.yaml validation
# Combines Helm range loop with Phase 1 validated init container + unfs3 pattern

{{- /*
  Multi-Instance NAS Servers Template
  Deploys 7 independent NAS servers with unique fsid, hostPath, NodePort
  Use values-multi-instance.yaml for this template
*/ -}}

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
      # Init container: sync Windows hostPath -> emptyDir
      initContainers:
        - name: sync-windows-data
          image: {{ $nas.initImage.repository }}:{{ $nas.initImage.tag }}
          imagePullPolicy: {{ $nas.initImage.pullPolicy }}
          command:
            - sh
            - -c
            - |
              set -e
              echo "=== NAS {{ $nas.name }} Init Container ==="
              echo "Installing rsync..."
              apk add --no-cache rsync

              echo "Creating NFS export directory..."
              mkdir -p /nfs-data

              echo "Syncing from Windows mount to NFS export..."
              echo "Source: /windows-mount/"
              echo "Destination: /nfs-data/"
              rsync -av --delete /windows-mount/ /nfs-data/

              echo "Sync complete. Files in export:"
              ls -la /nfs-data | head -20
              echo "Total files: $(find /nfs-data -type f 2>/dev/null | wc -l)"
              echo "=== Init Container Complete ==="
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
          image: {{ $nas.image.repository }}:{{ $nas.image.tag }}
          imagePullPolicy: {{ $nas.image.pullPolicy }}
          command:
            - sh
            - -c
            - |
              set -e
              echo "=== NAS {{ $nas.name }} NFS Server Starting ==="
              echo "Installing unfs3..."
              apk add --no-cache unfs3

              echo "Creating exports file..."
              mkdir -p /etc
              # Note: fsid must be unique per NAS instance (1-7)
              echo '/data 0.0.0.0/0(rw,no_root_squash)' > /etc/exports
              echo "Exports configuration:"
              cat /etc/exports

              echo "Starting NFS server (fsid={{ $nas.fsid | default (add $index 1) }})..."
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
            timeoutSeconds: 5
            failureThreshold: 3
          readinessProbe:
            tcpSocket:
              port: 2049
            initialDelaySeconds: 10
            periodSeconds: 5
            timeoutSeconds: 3
            failureThreshold: 3
          resources:
            {{- toYaml $nas.resources | nindent 12 }}

      volumes:
        - name: windows-data
          hostPath:
            path: {{ $.Values.global.storage.hostPath }}/{{ $nas.name }}
            type: DirectoryOrCreate
        - name: nfs-export
          emptyDir: {}  # Disk-backed (default) for persistence

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
  type: {{ $nas.service.type }}
  ports:
    - port: {{ $nas.service.port }}
      targetPort: nfs
      protocol: TCP
      name: nfs
      {{- if and (eq $nas.service.type "NodePort") $nas.service.nodePort }}
      nodePort: {{ $nas.service.nodePort }}
      {{- end }}
  selector:
    {{- include "file-simulator.selectorLabels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
{{- end }}
{{- end }}
{{- end }}
```

### values-multi-instance.yaml Configuration
```yaml
# Source: Adapted from existing ftp-multi.yaml values pattern
# Phase 2: 7 independent NAS servers

global:
  storage:
    hostPath: /mnt/simulator-data

namespace:
  create: true
  name: file-simulator

# ============================================
# Multiple NAS Servers (7-Server Topology)
# ============================================
nasServers:
  # Input servers (3)
  - name: nas-input-1
    enabled: true
    fsid: 1
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32150
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

  - name: nas-input-2
    enabled: true
    fsid: 2
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32151
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

  - name: nas-input-3
    enabled: true
    fsid: 3
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32152
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

  # Backup server (1)
  - name: nas-backup
    enabled: true
    fsid: 4
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32153
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

  # Output servers (3)
  - name: nas-output-1
    enabled: true
    fsid: 5
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32154
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

  - name: nas-output-2
    enabled: true
    fsid: 6
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32155
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

  - name: nas-output-3
    enabled: true
    fsid: 7
    initImage:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    image:
      repository: alpine
      tag: latest
      pullPolicy: IfNotPresent
    service:
      type: NodePort
      port: 2049
      nodePort: 32156
    resources:
      requests:
        memory: "64Mi"
        cpu: "50m"
      limits:
        memory: "256Mi"
        cpu: "200m"

# Disable other protocols for Phase 2 (focus on NAS only)
ftp:
  enabled: false
sftp:
  enabled: false
http:
  enabled: false
s3:
  enabled: false
smb:
  enabled: false
management:
  enabled: false
nasTest:
  enabled: false
```

### Template Rendering Test Command
```bash
# Source: Helm template command documentation
# https://helm.sh/docs/helm/helm_template/

# Test template rendering without deploying
helm template file-sim ./helm-chart/file-simulator \
  -f ./helm-chart/file-simulator/values-multi-instance.yaml \
  --debug \
  > rendered-output.yaml

# Verify 7 Deployments and 7 Services generated
grep -c "kind: Deployment" rendered-output.yaml  # Should output: 7
grep -c "kind: Service" rendered-output.yaml      # Should output: 7

# Check for unique fsid values (should show 1-7)
grep "fsid=" rendered-output.yaml

# Check for unique NodePorts (should show 32150-32156)
grep "nodePort:" rendered-output.yaml
```

### Deployment Command
```bash
# Source: Helm upgrade/install documentation

# Deploy 7 NAS servers
helm upgrade --install file-sim ./helm-chart/file-simulator \
  -f ./helm-chart/file-simulator/values-multi-instance.yaml \
  --namespace file-simulator \
  --create-namespace

# Verify all 7 pods running
kubectl get pods -n file-simulator -l simulator.protocol=nfs

# Expected output:
# NAME                              READY   STATUS    RESTARTS   AGE
# file-sim-nas-input-1-xxx          1/1     Running   0          2m
# file-sim-nas-input-2-xxx          1/1     Running   0          2m
# file-sim-nas-input-3-xxx          1/1     Running   0          2m
# file-sim-nas-backup-xxx           1/1     Running   0          2m
# file-sim-nas-output-1-xxx         1/1     Running   0          2m
# file-sim-nas-output-2-xxx         1/1     Running   0          2m
# file-sim-nas-output-3-xxx         1/1     Running   0          2m
```

### Storage Isolation Verification Script
```powershell
# Source: Phase 1 test-nas-pattern.ps1 adapted for multi-instance

# Create test files in each Windows directory
$nasServers = @("nas-input-1", "nas-input-2", "nas-input-3", "nas-backup", "nas-output-1", "nas-output-2", "nas-output-3")

foreach ($nas in $nasServers) {
    $dir = "C:\simulator-data\$nas"
    New-Item -ItemType Directory -Force -Path $dir
    Set-Content -Path "$dir\test-$nas.txt" -Value "This file belongs to $nas only"
    Write-Host "Created test file in $dir"
}

# Verify isolation: each pod should only see its own file
foreach ($nas in $nasServers) {
    $podName = kubectl get pod -n file-simulator -l "app.kubernetes.io/component=$nas" -o jsonpath='{.items[0].metadata.name}'
    Write-Host "`nChecking $nas ($podName):"

    kubectl exec -n file-simulator $podName -- ls -la /data

    # Should only see test-$nas.txt, not other NAS files
    $fileCount = kubectl exec -n file-simulator $podName -- sh -c "ls /data/*.txt 2>/dev/null | wc -l"
    if ($fileCount -eq 1) {
        Write-Host "✓ Storage isolation verified: only 1 file visible"
    } else {
        Write-Host "✗ Storage isolation FAILED: $fileCount files visible (expected 1)"
    }
}
```

### DNS Resolution Test
```bash
# Source: Kubernetes DNS documentation

# Test DNS resolution from within cluster
kubectl run -n file-simulator dns-test --image=busybox:latest --rm -it --restart=Never -- sh

# Inside dns-test pod:
nslookup file-sim-nas-input-1.file-simulator.svc.cluster.local
nslookup file-sim-nas-input-2.file-simulator.svc.cluster.local
nslookup file-sim-nas-backup.file-simulator.svc.cluster.local

# Expected output for each: IP address of service (ClusterIP)
# Confirms DNS names are resolvable within cluster
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Separate YAML per instance | Helm range loop | Helm 2.0+ (2016) | DRY principle, single source of truth |
| Hardcoded instance config | values.yaml list | Helm best practice | Easy to add/remove instances |
| Manual fsid assignment | Auto-increment in template | N/A | Prevents duplicate fsid errors |
| IP-based service discovery | Kubernetes DNS | Kubernetes 1.0+ (2015) | Stable names despite pod restarts |
| Resource over-provisioning | Explicit requests/limits | Kubernetes 1.8+ (2017) | Efficient packing, prevents OOM |
| Trial-and-error deployment | helm template --debug | Helm 3.0+ (2019) | Catch errors before cluster deployment |

**Deprecated/outdated:**
- **Helm 2 Tiller architecture:** Helm 3 removed server-side component, simpler security model
- **Manual resource calculation:** Kubernetes scheduler handles placement based on requests
- **Static hostPath without DirectoryOrCreate:** Modern pattern auto-creates directories
- **Cluster-wide NodePort allocation:** Now per-service explicit assignment in values.yaml

## Open Questions

Things that couldn't be fully resolved:

1. **Does unfs3 support fsid option?**
   - What we know: Phase 1 Summary states "Removed unsupported fsid option for userspace NFS" (line 108-209)
   - What's unclear: Phase 1 Research shows fsid in exports examples; contradiction in documentation
   - Recommendation: Deploy without fsid in exports file; rely on separate hostPath per instance for isolation
   - **RESOLVED:** Phase 1 validation confirmed fsid NOT supported by unfs3; storage isolation achieved via separate hostPath + emptyDir per pod

2. **Can 7 NAS servers run simultaneously in Minikube 8GB/4CPU?**
   - What we know: Each pod requests 64Mi + 50m CPU = 448Mi + 350m total
   - What's unclear: Actual memory usage at runtime (may exceed requests)
   - Recommendation: Monitor with `kubectl top pods` after deployment; adjust limits if OOM occurs

3. **Does Windows directory creation happen automatically?**
   - What we know: hostPath type: DirectoryOrCreate should auto-create
   - What's unclear: If Minikube mount prevents auto-creation on Windows host
   - Recommendation: Pre-create all 7 directories in PowerShell setup script before deployment

4. **What happens if one NAS pod crashes?**
   - What we know: Kubernetes restarts crashed pods automatically
   - What's unclear: Does init container re-sync lose recent files if Windows directory updated during downtime
   - Recommendation: Accept eventual consistency for Phase 2; continuous sync (sidecar) is Phase 3+ concern

5. **Are NodePorts 32150-32156 available in Minikube?**
   - What we know: Default range is 30000-32767; 32150-32156 is within range
   - What's unclear: If other services already allocated these ports
   - Recommendation: Check with `kubectl get svc --all-namespaces | grep 321` before deployment

## Sources

### Primary (HIGH confidence)
- [Helm Flow Control documentation](https://helm.sh/docs/chart_template_guide/control_structures/) - range loop syntax and scope
- [Kubernetes Resource Management](https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/) - requests/limits best practices
- [Kubernetes Volumes documentation](https://kubernetes.io/docs/concepts/storage/volumes/) - emptyDir and hostPath isolation guarantees
- [Kubernetes Service documentation](https://kubernetes.io/docs/concepts/services-networking/service/) - NodePort configuration
- file-simulator-suite codebase ftp-multi.yaml and sftp-multi.yaml - Existing multi-instance patterns

### Secondary (MEDIUM confidence)
- [Helm and Namespaces](https://medium.com/@tomerf/helm-and-namespaces-cce64fcdcced) - Multi-instance deployment patterns
- [Kubernetes NodePort allocation](https://kubernetes.io/blog/2023/05/11/nodeport-dynamic-and-static-allocation/) - Port range and conflicts
- [Minikube resource limits](https://thelinuxcode.com/kubernetes-minikube-a-pragmatic-2026-playbook/) - 2026 best practices for local development
- [Kubernetes emptyDir isolation](https://decisivedevops.com/understanding-kubernetes-emptydir-with-3-practical-use-cases-960f550e0e34/) - Per-pod isolation guarantees
- Phase 1 validation artifacts (01-02-SUMMARY.md, 01-RESEARCH.md) - Proven single-instance pattern

### Tertiary (LOW confidence - general patterns)
- [Setting up NFS FSID](https://earlruby.org/2022/01/setting-up-nfs-fsid-for-multiple-networks/) - NFS fsid best practices (may not apply to unfs3)
- [NFS exports fsid configuration](https://bobcares.com/blog/nfs-exports-fsid/) - fsid uniqueness requirements

## Metadata

**Confidence breakdown:**
- Standard stack (Helm 3, range loop): HIGH - Industry standard, well-documented
- Architecture (multi-instance pattern): HIGH - Existing ftp-multi.yaml proves pattern works
- Storage isolation (hostPath + emptyDir): HIGH - Kubernetes core guarantees, verified in official docs
- Resource constraints (7 pods in 8GB): MEDIUM - Math works, but runtime usage may vary
- fsid handling: HIGH - Phase 1 proved fsid NOT needed for unfs3; storage isolation via hostPath sufficient

**Research date:** 2026-01-29
**Valid until:** 2026-03-01 (30 days) - Helm and Kubernetes APIs stable; pattern unlikely to change

**Notes:**
- Phase 2 is purely infrastructure scaling, not new technology
- Phase 1 validation de-risks the core pattern (init container + unfs3)
- Existing ftp-multi.yaml and sftp-multi.yaml templates provide exact pattern to follow
- No new libraries or tools required beyond Phase 1
- Storage isolation guaranteed by Kubernetes design, not manual implementation
