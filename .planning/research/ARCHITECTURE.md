# Architecture Patterns: Multi-Instance NFS Deployment

**Domain:** Kubernetes NFS Server Multi-Instance Deployment
**Researched:** 2026-01-29
**Context:** Expanding single NAS deployment to 7 independent NAS servers with separate storage paths
**Confidence:** HIGH

---

## Executive Summary

Deploying multiple NFS servers in Kubernetes requires careful orchestration of three interconnected systems: Helm templating for resource generation, PersistentVolume/PVC management for storage isolation, and DNS service naming for discovery. The project's existing multi-instance patterns (FTP/SFTP) provide proven templates, but NFS introduces additional complexity due to storage binding requirements and the NFS emptyDir fix constraint.

**Key Finding:** The existing `ftp-multi.yaml` and `sftp-multi.yaml` patterns are directly applicable to NAS deployment, with the critical addition of per-instance PV/PVC creation to achieve storage isolation.

**Recommended Approach:** Helm range loop pattern with per-instance PV/PVC creation, following the project's established naming conventions.

---

## Current State Analysis

### Existing Single-NAS Architecture

**Current deployment pattern:**
- **Deployment:** `file-sim-file-simulator-nas`
- **Service DNS:** `file-sim-file-simulator-nas.file-simulator.svc.cluster.local:2049`
- **Storage:** Shared PVC (`file-sim-file-simulator-pvc`) mounted at `/data`
- **NodePort:** 32149
- **Critical constraint:** NFS emptyDir fix required (cannot export hostPath volumes)

### Storage Architecture (Single Instance)

```yaml
PersistentVolume: file-sim-file-simulator-pv
  └─ hostPath: /mnt/simulator-data
     └─ Windows: C:\simulator-data

PersistentVolumeClaim: file-sim-file-simulator-pvc
  └─ Mounts to: All protocol pods

NAS Deployment:
  volumes:
    - name: data (PVC mount)
      mountPath: /data          # Cannot export (hostPath limitation)
    - name: nfs-data (emptyDir)
      mountPath: /exports        # NFS exports this
```

**Limitation:** Current architecture uses a single shared PVC for all protocols. The NFS emptyDir fix prevents direct export of Windows-mounted hostPath storage.

---

## Target Architecture: 7 Independent NAS Servers

### Requirements

1. **7 NAS deployments** with distinct names: `nas-input-1`, `nas-input-2`, `nas-output-1`, etc.
2. **7 services** with predictable DNS: `file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local:2049`
3. **7 separate PVCs** each mapping to different Windows subdirectory
4. **7 unique NodePorts** for external access
5. **Storage isolation** between NAS instances (no shared data)

### Windows Storage Mapping

```
C:\simulator-data\
  ├── nas-input-1\     → PV: nas-input-1-pv  → PVC: nas-input-1-pvc
  ├── nas-input-2\     → PV: nas-input-2-pv  → PVC: nas-input-2-pvc
  ├── nas-output-1\    → PV: nas-output-1-pv → PVC: nas-output-1-pvc
  ├── nas-output-2\    → PV: nas-output-2-pv → PVC: nas-output-2-pvc
  ├── nas-config\      → PV: nas-config-pv   → PVC: nas-config-pvc
  ├── nas-temp\        → PV: nas-temp-pv     → PVC: nas-temp-pvc
  └── nas-archive\     → PV: nas-archive-pv  → PVC: nas-archive-pvc
```

---

## Recommended Architecture Pattern

### Pattern: Helm Range Loop with Per-Instance Storage

This pattern follows the project's established multi-instance approach (validated in `ftp-multi.yaml` and `sftp-multi.yaml`) with storage isolation extensions.

**Components:**
1. **Values configuration** defining NAS instance array
2. **Storage template** creating PV/PVC pairs per instance
3. **NAS deployment template** iterating over instances
4. **Service template** creating per-instance DNS endpoints

### Component 1: Values Configuration

**File:** `values.yaml` or `values-multi-nas.yaml`

```yaml
nasServers:
  - name: nas-input-1
    enabled: true
    nodePort: 32150
    hostPath: /mnt/simulator-data/nas-input-1
    storageSize: 5Gi

  - name: nas-input-2
    enabled: true
    nodePort: 32151
    hostPath: /mnt/simulator-data/nas-input-2
    storageSize: 5Gi

  - name: nas-output-1
    enabled: true
    nodePort: 32152
    hostPath: /mnt/simulator-data/nas-output-1
    storageSize: 5Gi

  - name: nas-output-2
    enabled: true
    nodePort: 32153
    hostPath: /mnt/simulator-data/nas-output-2
    storageSize: 5Gi

  - name: nas-config
    enabled: true
    nodePort: 32154
    hostPath: /mnt/simulator-data/nas-config
    storageSize: 2Gi

  - name: nas-temp
    enabled: true
    nodePort: 32155
    hostPath: /mnt/simulator-data/nas-temp
    storageSize: 10Gi

  - name: nas-archive
    enabled: true
    nodePort: 32156
    hostPath: /mnt/simulator-data/nas-archive
    storageSize: 20Gi

# NFS server image (shared across all instances)
nas:
  image:
    repository: erichough/nfs-server
    tag: latest
    pullPolicy: IfNotPresent
  resources:
    requests:
      memory: "64Mi"
      cpu: "50m"
    limits:
      memory: "256Mi"
      cpu: "200m"
```

**Key design decisions:**
- **Per-instance `hostPath`**: Each NAS maps to separate Windows directory
- **Per-instance `nodePort`**: Sequential allocation (32150-32156) within Kubernetes NodePort range
- **Per-instance `storageSize`**: Flexible sizing based on purpose (temp/archive larger)
- **Shared image config**: All NAS instances use same container image
- **`enabled` flag**: Allows selective deployment (e.g., deploy 3 of 7 initially)

### Component 2: Storage Template

**File:** `templates/nas-storage-multi.yaml`

```yaml
{{- if .Values.nasServers }}
{{- range $index, $nas := .Values.nasServers }}
{{- if $nas.enabled }}
---
# PersistentVolume for {{ $nas.name }}
apiVersion: v1
kind: PersistentVolume
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}-pv
  labels:
    {{- include "file-simulator.labels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
    simulator.protocol: nas
    simulator.instance: "{{ $index }}"
spec:
  capacity:
    storage: {{ $nas.storageSize }}
  volumeMode: Filesystem
  accessModes:
    - ReadWriteMany
  persistentVolumeReclaimPolicy: Retain
  storageClassName: ""
  hostPath:
    path: {{ $nas.hostPath }}
    type: DirectoryOrCreate
---
# PersistentVolumeClaim for {{ $nas.name }}
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}-pvc
  namespace: {{ include "file-simulator.namespace" $ }}
  labels:
    {{- include "file-simulator.labels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
    simulator.protocol: nas
    simulator.instance: "{{ $index }}"
spec:
  accessModes:
    - ReadWriteMany
  storageClassName: ""
  volumeName: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}-pv
  resources:
    requests:
      storage: {{ $nas.storageSize }}
{{- end }}
{{- end }}
{{- end }}
```

**Pattern explanation:**
- **`{{- range $index, $nas := .Values.nasServers }}`**: Iterate over NAS instance array
- **`{{ include "file-simulator.fullname" $ }}-{{ $nas.name }}-pv`**: Generate unique PV name (e.g., `file-sim-file-simulator-nas-input-1-pv`)
- **`volumeName` binding**: PVC explicitly binds to PV by name (prevents dynamic provisioning)
- **`storageClassName: ""`**: Static binding (no StorageClass controller involved)
- **`YAML separators (---)`**: Required between resources to prevent Helm deployment issues

### Component 3: NAS Deployment Template

**File:** `templates/nas-multi.yaml`

```yaml
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
    simulator.protocol: nas
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
      {{- if $.Values.serviceAccount.create }}
      serviceAccountName: {{ include "file-simulator.serviceAccountName" $ }}
      {{- end }}
      containers:
        - name: nfs-server
          image: "{{ $.Values.nas.image.repository }}:{{ $.Values.nas.image.tag }}"
          imagePullPolicy: {{ $.Values.nas.image.pullPolicy }}
          ports:
            - name: nfs
              containerPort: 2049
              protocol: TCP
            - name: nfs-udp
              containerPort: 2049
              protocol: UDP
            - name: mountd
              containerPort: 32765
              protocol: TCP
            - name: mountd-udp
              containerPort: 32765
              protocol: UDP
            - name: statd
              containerPort: 32766
              protocol: TCP
            - name: statd-udp
              containerPort: 32766
              protocol: UDP
            - name: lockd
              containerPort: 32767
              protocol: TCP
            - name: lockd-udp
              containerPort: 32767
              protocol: UDP
            - name: rpcbind
              containerPort: 111
              protocol: TCP
            - name: rpcbind-udp
              containerPort: 111
              protocol: UDP
          env:
            - name: NFS_EXPORT_0
              value: "/data *(rw,sync,no_subtree_check,no_root_squash,fsid=0)"
            - name: NFS_DISABLE_VERSION_3
              value: "false"
            - name: NFS_LOG_LEVEL
              value: "DEBUG"
          volumeMounts:
            # CRITICAL: emptyDir for NFS exports (hostPath cannot be exported)
            - name: nfs-data
              mountPath: /data
            # Shared PVC for cross-protocol access (optional)
            - name: shared-data
              mountPath: /shared
          securityContext:
            privileged: true
            capabilities:
              add:
                - SYS_ADMIN
                - DAC_READ_SEARCH
          livenessProbe:
            tcpSocket:
              port: nfs
            initialDelaySeconds: 30
            periodSeconds: 10
          readinessProbe:
            tcpSocket:
              port: nfs
            initialDelaySeconds: 10
            periodSeconds: 5
          resources:
            {{- toYaml $.Values.nas.resources | nindent 12 }}
      volumes:
        # CRITICAL: emptyDir for NFS export (see NFS fix documentation)
        - name: nfs-data
          emptyDir: {}
        # Per-instance PVC for Windows storage access
        - name: shared-data
          persistentVolumeClaim:
            claimName: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}-pvc
      {{- with $.Values.nodeSelector }}
      nodeSelector:
        {{- toYaml . | nindent 8 }}
      {{- end }}
{{- end }}
{{- end }}
{{- end }}
```

**Critical design notes:**

1. **emptyDir volume (`nfs-data`)**: Required for NFS exports due to hostPath limitation. NFS cannot export Windows-mounted hostPath volumes.

2. **PVC volume (`shared-data`)**: Mounts per-instance PVC to `/shared` for:
   - Cross-protocol file access
   - Windows → Kubernetes file transfer
   - Backup/restore operations

3. **Scope management (`$` vs `.`)**:
   - `$`: Root context (release/chart values)
   - `.`: Current iteration context (current `$nas` object)
   - Use `$.Values.nas.image` (root) not `.Values.nas.image` (fails in loop)

4. **Port configuration**: All 10 NFS ports (TCP/UDP pairs) defined for full NFSv3/v4 compatibility

### Component 4: Service Template

**File:** `templates/nas-multi.yaml` (continued)

```yaml
{{- if .Values.nasServers }}
{{- range $index, $nas := .Values.nasServers }}
{{- if $nas.enabled }}
---
apiVersion: v1
kind: Service
metadata:
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}
  namespace: {{ include "file-simulator.namespace" $ }}
  labels:
    {{- include "file-simulator.labels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
    simulator.protocol: nas
spec:
  type: NodePort
  ports:
    - port: 2049
      targetPort: nfs
      protocol: TCP
      name: nfs
      nodePort: {{ $nas.nodePort }}
    - port: 32765
      targetPort: mountd
      protocol: TCP
      name: mountd
    - port: 32766
      targetPort: statd
      protocol: TCP
      name: statd
    - port: 32767
      targetPort: lockd
      protocol: TCP
      name: lockd
    - port: 111
      targetPort: rpcbind
      protocol: TCP
      name: rpcbind
  selector:
    {{- include "file-simulator.selectorLabels" $ | nindent 4 }}
    app.kubernetes.io/component: {{ $nas.name }}
{{- end }}
{{- end }}
{{- end }}
```

**Service naming resolution:**

| Instance | Service Name | DNS FQDN | NodePort |
|----------|--------------|----------|----------|
| nas-input-1 | `file-sim-file-simulator-nas-input-1` | `file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local` | 32150 |
| nas-input-2 | `file-sim-file-simulator-nas-input-2` | `file-sim-file-simulator-nas-input-2.file-simulator.svc.cluster.local` | 32151 |
| nas-output-1 | `file-sim-file-simulator-nas-output-1` | `file-sim-file-simulator-nas-output-1.file-simulator.svc.cluster.local` | 32152 |

**DNS naming pattern:** `{release}-{chart}-{instance-name}.{namespace}.svc.cluster.local`

---

## Alternative Architecture Patterns Considered

### Alternative 1: Single NFS Server with Multiple Exports

**Approach:** Deploy one NFS server pod with multiple export paths configured via environment variables.

```yaml
env:
  - name: NFS_EXPORT_0
    value: "/input1 *(rw,sync,no_subtree_check)"
  - name: NFS_EXPORT_1
    value: "/input2 *(rw,sync,no_subtree_check)"
  # ... 7 exports total
```

**Storage approach:**
- Single PVC with subdirectories (subPath mounts)
- One service endpoint, clients specify different mount paths

**Pros:**
- Fewer Kubernetes resources (1 deployment vs 7)
- Single service endpoint simplifies discovery
- Lower resource overhead

**Cons:**
- **Single point of failure**: One pod crash affects all "NAS servers"
- **Complex mount syntax**: Clients must know subdirectory structure
- **Harder to scale**: Cannot scale individual NAS instances
- **Storage isolation unclear**: All subdirectories in one PVC
- **Does not match production model**: Production likely has separate NAS devices

**Verdict:** REJECTED. Does not meet requirement for "7 independent NAS servers." Production fidelity requires separate service endpoints.

### Alternative 2: StatefulSet with Dynamic PVC Provisioning

**Approach:** Use StatefulSet with volumeClaimTemplates for automatic PVC creation.

```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: nas-server
spec:
  replicas: 7
  volumeClaimTemplates:
    - metadata:
        name: data
      spec:
        accessModes: ["ReadWriteMany"]
        resources:
          requests:
            storage: 5Gi
```

**Pros:**
- Automatic PVC creation per replica
- Kubernetes manages PVC lifecycle
- Stable pod identities (nas-server-0, nas-server-1, ...)

**Cons:**
- **Cannot control hostPath per PVC**: volumeClaimTemplates use StorageClass provisioner, but hostPath is static (no dynamic provisioner)
- **Naming convention mismatch**: Generates `nas-server-0` not `nas-input-1` (semantic names required)
- **Cannot vary storage size**: All PVCs get same size (requirement: nas-archive needs 20Gi, nas-config needs 2Gi)
- **Cannot disable individual instances**: replicas=7 deploys all or none (requirement: enabled flag per instance)

**Verdict:** REJECTED. StatefulSet is designed for homogeneous replicas. Requirements need heterogeneous instances with distinct configurations.

### Alternative 3: Separate Helm Releases per NAS

**Approach:** Deploy 7 separate Helm releases using single-instance chart.

```bash
helm install nas-input-1 ./helm-chart/file-simulator \
  --set nas.enabled=true \
  --set nas.service.nodePort=32150

helm install nas-input-2 ./helm-chart/file-simulator \
  --set nas.enabled=true \
  --set nas.service.nodePort=32151

# ... 5 more helm install commands
```

**Pros:**
- Uses existing single-instance templates (no code changes)
- Each NAS is completely isolated (separate release lifecycle)
- Easy to upgrade/rollback individual NAS instances

**Cons:**
- **Operational complexity**: 7 helm commands to deploy/upgrade/delete
- **Configuration drift risk**: Each release has separate values, hard to keep synchronized
- **Namespace pollution**: 7 releases create 7x resources (7 ServiceAccounts, 7 shared PVCs if not careful)
- **No atomic deployment**: Deploying all 7 is not a single transaction
- **Harder to template**: Cannot loop over NAS instances in CI/CD pipelines

**Verdict:** REJECTED for initial implementation. Could be useful for operational scenarios (e.g., rolling restart of one NAS), but multi-instance template is cleaner for standard deployment.

---

## Storage Architecture Deep Dive

### Challenge: NFS Cannot Export hostPath Volumes

**Root cause:** The `erichough/nfs-server` container expects to export directories within the container filesystem. When a hostPath volume is mounted, the underlying filesystem is NTFS (Windows), which NFS server cannot export due to:
- Extended attribute incompatibility
- Permission model mismatch
- Filesystem feature differences

**Current workaround (single NAS):**
```yaml
volumes:
  - name: nfs-data
    emptyDir: {}          # NFS exports this
  - name: shared-data
    persistentVolumeClaim:
      claimName: shared-pvc  # Access to Windows files
```

**Implication for multi-NAS:**
- Each NAS instance MUST use emptyDir for NFS exports
- Each NAS instance CAN mount per-instance PVC for Windows access
- Files must be copied from Windows PVC to emptyDir to be NFS-exported

### Data Flow Pattern

```
Windows Host: C:\simulator-data\nas-input-1\
       ↓ (Minikube mount)
Kubernetes hostPath: /mnt/simulator-data/nas-input-1
       ↓ (PV/PVC)
NAS Pod Volume: /shared (PVC mount)
       ↓ (Copy/sync process)
NAS Pod Volume: /data (emptyDir)
       ↓ (NFS export)
Kubernetes Clients: mount nas-input-1:/ → /data
```

**Copy mechanisms (pick one):**

1. **InitContainer approach:** Copy files on pod startup
```yaml
initContainers:
  - name: sync-data
    image: busybox
    command: ['sh', '-c', 'cp -r /shared/* /data/']
    volumeMounts:
      - name: shared-data
        mountPath: /shared
      - name: nfs-data
        mountPath: /data
```

2. **Sidecar approach:** Continuous sync with inotify
```yaml
containers:
  - name: file-sync
    image: alpine
    command: ['sh', '-c', 'while true; do rsync -av /shared/ /data/; sleep 10; done']
    volumeMounts:
      - name: shared-data
        mountPath: /shared
      - name: nfs-data
        mountPath: /data
```

3. **Application-level approach:** Microservices write directly to NFS-mounted emptyDir
   - Pro: No copy overhead
   - Con: Loses Windows access (files not in C:\simulator-data)

**Recommendation for Phase 1:** InitContainer approach (simplest, good for testing)
**Recommendation for Production:** Sidecar with rsync (keeps Windows in sync)

### Storage Isolation Verification

**Per-instance PV/PVC ensures:**
- NAS instance `nas-input-1` cannot access `nas-output-1` files
- Windows directories remain separate
- Storage quotas enforced per instance (storageSize in PVC)

**Validation test:**
```bash
# Write to nas-input-1 from Windows
echo "test" > C:\simulator-data\nas-input-1\test.txt

# Mount nas-input-1 in pod
kubectl run -it test --image=alpine --rm -- sh
mount nas-input-1.file-simulator.svc.cluster.local:/ /mnt
ls /mnt  # Should see test.txt

# Mount nas-input-2 in same pod
mount nas-input-2.file-simulator.svc.cluster.local:/ /mnt2
ls /mnt2  # Should NOT see test.txt (different storage)
```

---

## DNS Service Discovery

### Service Naming Convention

Kubernetes service DNS follows RFC 1035 naming: `<service>.<namespace>.svc.<cluster-domain>`

**Project's generated names:**
- Pattern: `{{ .Release.Name }}-{{ .Chart.Name }}-{{ instance-name }}`
- Example: `file-sim-file-simulator-nas-input-1`

**Full DNS FQDN:**
- Pattern: `{{ .Release.Name }}-{{ .Chart.Name }}-{{ instance-name }}.{{ namespace }}.svc.cluster.local`
- Example: `file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local:2049`

### DNS Name Length Constraints

Kubernetes enforces 63-character limit per DNS label (per RFC 1035).

**Character budget:**
```
file-sim (8) + - (1) + file-simulator (14) + - (1) + nas-input-1 (11) = 35 characters
Remaining budget: 63 - 35 = 28 characters for longer instance names
```

**Safe instance name length:** Up to 28 characters (total service name ≤ 63)

**Validation:**
```yaml
nasServers:
  - name: nas-very-long-instance-name-test  # 32 chars
    # Total: file-sim-file-simulator-nas-very-long-instance-name-test = 56 chars ✓

  - name: nas-extremely-long-instance-name-that-exceeds-limit  # 52 chars
    # Total: 77 chars ✗ EXCEEDS LIMIT
```

**Mitigation:** Template helper to truncate names:
```yaml
{{- define "file-simulator.nas-name" -}}
{{- $fullname := printf "%s-%s" (include "file-simulator.fullname" .) .name }}
{{- $fullname | trunc 63 | trimSuffix "-" }}
{{- end }}
```

### Client Configuration Patterns

**Pattern 1: Hardcoded DNS (not recommended)**
```yaml
env:
  - name: NAS_INPUT_1_HOST
    value: "file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local"
```

**Pattern 2: ConfigMap with service discovery**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: nas-endpoints
data:
  nas-servers.json: |
    {
      "input": [
        "file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local:2049",
        "file-sim-file-simulator-nas-input-2.file-simulator.svc.cluster.local:2049"
      ],
      "output": [
        "file-sim-file-simulator-nas-output-1.file-simulator.svc.cluster.local:2049",
        "file-sim-file-simulator-nas-output-2.file-simulator.svc.cluster.local:2049"
      ]
    }
```

**Pattern 3: Kubernetes Service discovery (DNS SRV records)**
```bash
# Query all NAS services with label selector
kubectl get svc -n file-simulator -l simulator.protocol=nas -o json | \
  jq -r '.items[] | "\(.metadata.name).\(.metadata.namespace).svc.cluster.local"'
```

**Recommendation:** Pattern 2 (ConfigMap) with Helm template generation for DRY principle.

---

## NodePort Allocation Strategy

### Kubernetes NodePort Range

Default range: 30000-32767 (2768 available ports)

**Current project allocation:**
- Management: 30180
- FTP: 30021, 30022 (multi-instance)
- SFTP: 30122, 30123 (multi-instance)
- HTTP: 30088
- S3: 30900, 30901 (API + Console)
- SMB: LoadBalancer (port 445)
- NAS: 32149 (single instance)

**Available for multi-NAS:** 32150-32767 (618 ports)

### Allocation Strategy for 7 NAS Instances

**Option 1: Sequential allocation (recommended)**
```yaml
nasServers:
  - name: nas-input-1
    nodePort: 32150  # Base + 0
  - name: nas-input-2
    nodePort: 32151  # Base + 1
  - name: nas-output-1
    nodePort: 32152  # Base + 2
  # ...
```

**Pros:**
- Predictable pattern
- Easy to calculate (base=32150, port=base+index)
- No gaps in allocation

**Cons:**
- Adding instances requires updating values (manual port assignment)
- Port conflicts if multiple chart installations

**Option 2: Offset allocation**
```yaml
nasServers:
  - name: nas-input-1
    nodePort: 32200  # Input block: 32200-32299
  - name: nas-input-2
    nodePort: 32201
  - name: nas-output-1
    nodePort: 32300  # Output block: 32300-32399
  - name: nas-output-2
    nodePort: 32301
```

**Pros:**
- Semantic grouping (inputs separate from outputs)
- Easier to add instances within block (room for growth)

**Cons:**
- Wastes port space (gaps between blocks)
- More complex documentation

**Option 3: Dynamic allocation (Kubernetes 1.27+)**
```yaml
service:
  type: NodePort
  ports:
    - port: 2049
      # nodePort omitted - Kubernetes assigns automatically
```

**Pros:**
- No manual port management
- No conflicts
- Uses Kubernetes StaticSubrange feature (collision avoidance)

**Cons:**
- Unpredictable port assignments (client config harder)
- Requires Kubernetes 1.27+ (Minikube may not support)
- External access documentation difficult (ports change on redeploy)

**Recommendation:** Option 1 (Sequential) for Minikube/development, Option 3 (Dynamic) for production if Kubernetes 1.27+ available.

### Windows Firewall Considerations

If accessing NFS from Windows host (e.g., via `mount -o nolock 192.168.59.100:32150 Z:`):

**Required firewall rules:**
```powershell
# Allow NFS NodePort range
New-NetFirewallRule -DisplayName "Kubernetes NFS NodePort Range" `
  -Direction Inbound -Action Allow -Protocol TCP -LocalPort 32150-32200

# Allow NFSv3/v4 RPC ports (if exposed as NodePort)
New-NetFirewallRule -DisplayName "Kubernetes NFS RPC Ports" `
  -Direction Inbound -Action Allow -Protocol TCP -LocalPort 32765-32767,111
```

---

## Build Order and Incremental Deployment

### Phase 1: Template Infrastructure (Non-Disruptive)

**Goal:** Create multi-instance templates without affecting existing single-instance deployment.

**Tasks:**
1. Create `templates/nas-storage-multi.yaml` (PV/PVC generation)
2. Create `templates/nas-multi.yaml` (Deployment/Service generation)
3. Add `nasServers: []` to `values.yaml` (empty array, disabled by default)
4. Create `values-multi-nas.yaml` with 7 NAS instance definitions

**Validation:**
```bash
# Verify templates render correctly
helm template file-sim ./helm-chart/file-simulator \
  --namespace file-simulator \
  --debug

# Should still render single NAS (nasServers array empty)

# Test multi-NAS rendering
helm template file-sim ./helm-chart/file-simulator \
  -f ./helm-chart/file-simulator/values-multi-nas.yaml \
  --namespace file-simulator \
  --debug | grep "kind: Deployment"

# Should render 7 NAS deployments
```

**Safety:** Single-instance NAS remains operational. New templates are dormant (empty array).

### Phase 2: Single Multi-Instance Deployment Test

**Goal:** Deploy ONE multi-instance NAS to validate pattern before scaling to 7.

**Values override:**
```yaml
# values-test-multi-nas.yaml
nas:
  enabled: false  # Disable single-instance NAS

nasServers:
  - name: nas-test-1
    enabled: true
    nodePort: 32150
    hostPath: /mnt/simulator-data/nas-test-1
    storageSize: 1Gi
```

**Deployment:**
```bash
# Create test directory on Windows
mkdir C:\simulator-data\nas-test-1

# Deploy with test values
helm upgrade --install file-sim ./helm-chart/file-simulator \
  --kube-context=file-simulator \
  -f ./helm-chart/file-simulator/values-test-multi-nas.yaml \
  --namespace file-simulator

# Verify deployment
kubectl --context=file-simulator get pods -n file-simulator -l app.kubernetes.io/component=nas-test-1

# Test NFS mount from client pod
kubectl run -it nfs-client --image=alpine --rm -- sh
apk add nfs-utils
mount -t nfs4 -o vers=4.0 file-sim-file-simulator-nas-test-1.file-simulator.svc.cluster.local:/ /mnt
echo "test" > /mnt/test.txt
ls /mnt
```

**Validation criteria:**
- ✓ Pod starts successfully
- ✓ PV/PVC bound correctly
- ✓ Service DNS resolves
- ✓ NFS mount succeeds from client pod
- ✓ File write/read operations work

### Phase 3: Expand to 3 NAS Instances

**Goal:** Validate multi-instance scaling and resource naming collision avoidance.

**Values:**
```yaml
nas:
  enabled: false

nasServers:
  - name: nas-input-1
    enabled: true
    nodePort: 32150
    hostPath: /mnt/simulator-data/nas-input-1
    storageSize: 5Gi

  - name: nas-output-1
    enabled: true
    nodePort: 32151
    hostPath: /mnt/simulator-data/nas-output-1
    storageSize: 5Gi

  - name: nas-temp
    enabled: true
    nodePort: 32152
    hostPath: /mnt/simulator-data/nas-temp
    storageSize: 10Gi
```

**Deployment:**
```bash
# Create directories
mkdir C:\simulator-data\nas-input-1
mkdir C:\simulator-data\nas-output-1
mkdir C:\simulator-data\nas-temp

# Deploy 3 instances
helm upgrade --install file-sim ./helm-chart/file-simulator \
  --kube-context=file-simulator \
  -f ./helm-chart/file-simulator/values-multi-nas.yaml \
  --set nasServers[3].enabled=false \
  --set nasServers[4].enabled=false \
  --set nasServers[5].enabled=false \
  --set nasServers[6].enabled=false \
  --namespace file-simulator
```

**Validation:**
- ✓ All 3 pods running
- ✓ No resource name collisions
- ✓ Each service has unique DNS name
- ✓ Each NAS has isolated storage (cross-mount test)

### Phase 4: Full 7-Instance Deployment

**Goal:** Deploy production configuration with all 7 NAS instances.

**Deployment:**
```bash
# Create all directories
mkdir C:\simulator-data\nas-input-1
mkdir C:\simulator-data\nas-input-2
mkdir C:\simulator-data\nas-output-1
mkdir C:\simulator-data\nas-output-2
mkdir C:\simulator-data\nas-config
mkdir C:\simulator-data\nas-temp
mkdir C:\simulator-data\nas-archive

# Deploy all 7
helm upgrade --install file-sim ./helm-chart/file-simulator \
  --kube-context=file-simulator \
  -f ./helm-chart/file-simulator/values-multi-nas.yaml \
  --namespace file-simulator

# Monitor rollout
kubectl --context=file-simulator rollout status deployment \
  -n file-simulator -l simulator.protocol=nas --watch

# Validate all services
kubectl --context=file-simulator get svc -n file-simulator \
  -l simulator.protocol=nas -o wide
```

**Validation:**
- ✓ 7 pods running (READY 1/1)
- ✓ 7 services created with unique NodePorts
- ✓ DNS resolution for all 7 services
- ✓ Storage isolation (mount all 7, verify no cross-contamination)
- ✓ Resource utilization within cluster capacity

### Phase 5: Integration with Existing Protocols

**Goal:** Update FTP/SFTP/HTTP/S3/SMB to use multi-NAS endpoints.

**Example (FTP connecting to nas-input-1):**
```yaml
ftp:
  enabled: true
  volumeMounts:
    - name: nas-input-1
      mountPath: /home/vsftpd/input
  volumes:
    - name: nas-input-1
      nfs:
        server: file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local
        path: /
```

**Incremental approach:**
1. Update one protocol (e.g., FTP) to use one NAS
2. Test file operations
3. Expand to remaining protocols
4. Update client library connection strings

---

## Helm Template Implementation Patterns

### Pattern: Scope Management in Range Loops

**Problem:** Inside `{{- range }}`, `.Values` refers to current iteration item, not root values.

**Solution:** Use `$` for root context.

```yaml
{{- range $index, $nas := .Values.nasServers }}
  # WRONG: .Values.nas.image (undefined - no .Values in iteration scope)
  image: "{{ .Values.nas.image.repository }}"

  # CORRECT: $.Values.nas.image ($ = root context)
  image: "{{ $.Values.nas.image.repository }}"

  # CORRECT: Helper functions always use root context
  name: {{ include "file-simulator.fullname" $ }}-{{ $nas.name }}
{{- end }}
```

### Pattern: YAML Document Separators

**Problem:** Helm may only deploy last resource if `---` separators missing.

**Solution:** Always use `---` between resources in multi-resource templates.

```yaml
{{- range .Values.nasServers }}
---  # CRITICAL: Separator before each resource
apiVersion: v1
kind: Service
# ...
{{- end }}
```

### Pattern: Conditional Instance Deployment

**Requirement:** Support `enabled: false` to skip instances without removing from values.

**Implementation:**
```yaml
{{- range $index, $nas := .Values.nasServers }}
{{- if $nas.enabled }}  # Only deploy if enabled
---
apiVersion: apps/v1
kind: Deployment
# ...
{{- end }}  # Close if
{{- end }}  # Close range
```

### Pattern: Label Consistency

**Labels for filtering/selection:**
```yaml
labels:
  app.kubernetes.io/component: {{ $nas.name }}  # Unique per instance
  simulator.protocol: nas                        # Common to all NAS
  simulator.instance: "{{ $index }}"            # Numeric index
```

**Use cases:**
- Select all NAS: `kubectl get pods -l simulator.protocol=nas`
- Select specific NAS: `kubectl get pods -l app.kubernetes.io/component=nas-input-1`
- Select by index: `kubectl get pods -l simulator.instance=0`

### Pattern: Resource Name Truncation

**Kubernetes 63-character name limit:**
```yaml
{{- define "file-simulator.nas-fullname" -}}
{{- $base := include "file-simulator.fullname" . -}}
{{- $full := printf "%s-%s" $base .instance -}}
{{- $full | trunc 63 | trimSuffix "-" -}}
{{- end }}
```

**Usage:**
```yaml
metadata:
  name: {{ include "file-simulator.nas-fullname" (dict "instance" $nas.name "Chart" $.Chart "Release" $.Release "Values" $.Values) }}
```

---

## Comparison: Single vs Multi-Instance Deployment

| Aspect | Single Instance (Current) | Multi-Instance (Target) |
|--------|---------------------------|-------------------------|
| **Deployments** | 1 (`nas`) | 7 (`nas-input-1`, `nas-input-2`, ...) |
| **Services** | 1 DNS name | 7 DNS names |
| **Storage** | 1 PVC (shared with all protocols) | 7 PVCs (isolated per NAS) |
| **Windows Mapping** | C:\simulator-data (entire tree) | C:\simulator-data\nas-input-1 (per-instance subdirs) |
| **NodePorts** | 32149 (single port) | 32150-32156 (7 ports) |
| **Resource Usage** | 1x pod resources | 7x pod resources (~450Mi memory, 350m CPU) |
| **Fault Isolation** | Failure affects all NFS access | Failure affects one NAS (others remain operational) |
| **Scaling** | Vertical (increase pod resources) | Horizontal (add more NAS instances) |
| **Configuration** | Simple (single set of values) | Complex (array of 7 configs) |
| **Client Discovery** | Hardcoded service name | Dynamic (iterate nasServers array or ConfigMap) |
| **Deployment Complexity** | One helm command | One helm command (same, but longer values file) |

---

## Risk Analysis and Mitigations

### Risk 1: Resource Exhaustion

**Scenario:** 7 NAS pods exceed Minikube cluster capacity.

**Impact:** Pods stuck in Pending state, cluster unstable.

**Probability:** MEDIUM (current cluster: 8GB RAM, 4 CPU)

**Mitigation:**
- **Current resource allocation per NAS:** 64Mi request, 256Mi limit, 50m CPU request, 200m CPU limit
- **Total for 7 NAS instances:** 448Mi request, 1.75Gi limit, 350m CPU request, 1.4 CPU limit
- **Remaining capacity:** 8GB - 2.85Gi (current) - 1.75Gi (NAS) = 3.4Gi available ✓
- **Action:** Monitor with `kubectl top pods -n file-simulator` during rollout
- **Fallback:** Reduce NAS pod limits to 128Mi, or increase Minikube memory to 12GB

### Risk 2: NodePort Exhaustion

**Scenario:** 7 NAS instances require 7 NodePorts, potential conflicts with other services.

**Impact:** Service creation fails, NAS inaccessible from outside cluster.

**Probability:** LOW (2768 total NodePorts available, project uses ~20)

**Mitigation:**
- **Reserved range:** 32150-32200 (50 ports reserved for NAS expansion)
- **Documentation:** Update NodePort allocation table in project README
- **Validation:** Pre-deployment check script to verify NodePort availability
- **Fallback:** Use dynamic NodePort allocation (omit nodePort field)

### Risk 3: DNS Name Length Exceeds 63 Characters

**Scenario:** Instance name too long, combined with release/chart name exceeds limit.

**Impact:** Service creation fails with validation error.

**Probability:** LOW (current names well under limit)

**Mitigation:**
- **Character budget:** 63 - 35 (base) = 28 chars available for instance name
- **Validation:** Helm template helper to truncate and warn
- **Naming convention:** Enforce short instance names (max 20 chars recommended)
- **Example safe names:** `nas-input-1` (11 chars), `nas-out-1` (10 chars)

### Risk 4: Storage Mount Path Not Created

**Scenario:** Windows directory `C:\simulator-data\nas-input-1` doesn't exist before deployment.

**Impact:** PV hostPath creation fails (even with DirectoryOrCreate), pod stuck in ContainerCreating.

**Probability:** HIGH (manual directory creation required)

**Mitigation:**
- **Pre-deployment script:** PowerShell script to create all required directories
```powershell
$nasInstances = @('nas-input-1', 'nas-input-2', 'nas-output-1', 'nas-output-2', 'nas-config', 'nas-temp', 'nas-archive')
foreach ($nas in $nasInstances) {
  $path = "C:\simulator-data\$nas"
  if (-not (Test-Path $path)) {
    New-Item -ItemType Directory -Path $path -Force
    Write-Host "Created: $path"
  }
}
```
- **Documentation:** Add to deployment runbook
- **Validation:** Check all directories exist before `helm upgrade`

### Risk 5: NFS emptyDir Data Loss

**Scenario:** NAS pod restarts, emptyDir volume erased, NFS-exported files lost.

**Impact:** Clients lose access to files until pod restarts and data re-synced from PVC.

**Probability:** MEDIUM (pod restarts on node failure, OOMKill, or update)

**Mitigation:**
- **InitContainer sync:** Copy from PVC to emptyDir on pod startup (see Storage Architecture)
- **Sidecar sync:** Continuous rsync from PVC to emptyDir (eventual consistency)
- **Client retry logic:** Clients must handle transient NFS failures
- **Monitoring:** Alert on pod restarts, automated data sync verification
- **Alternative:** Investigate NFS export from PVC (requires different NFS server image that supports NTFS/FUSE)

### Risk 6: Helm Range Loop Deployment Bug

**Scenario:** Helm only deploys last resource from range loop (historical issue).

**Impact:** Only `nas-archive` (last instance) deploys, other 6 instances missing.

**Probability:** LOW (fixed in modern Helm versions, but documented as past issue)

**Mitigation:**
- **YAML separators:** Ensure `---` between all resources in range loop
- **Post-deployment validation:** Count deployed resources (`kubectl get deploy -l simulator.protocol=nas | wc -l` should be 7)
- **Helm version:** Use Helm 3.8+ (project uses Helm 3.x)

---

## Monitoring and Observability

### Health Check Strategy

**Per-NAS health indicators:**
1. **Pod status:** Running, Ready 1/1
2. **TCP probe:** NFS port 2049 responding
3. **Mount test:** Client pod can mount NFS share
4. **File operation test:** Create/read/delete file via NFS

**Monitoring script:**
```bash
#!/bin/bash
# check-nas-health.sh

NAS_INSTANCES=("nas-input-1" "nas-input-2" "nas-output-1" "nas-output-2" "nas-config" "nas-temp" "nas-archive")
NAMESPACE="file-simulator"

for nas in "${NAS_INSTANCES[@]}"; do
  echo "Checking $nas..."

  # Pod status
  POD_STATUS=$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=$nas -o jsonpath='{.items[0].status.phase}')
  echo "  Pod Status: $POD_STATUS"

  # TCP probe
  POD_NAME=$(kubectl get pod -n $NAMESPACE -l app.kubernetes.io/component=$nas -o jsonpath='{.items[0].metadata.name}')
  kubectl exec -n $NAMESPACE $POD_NAME -- nc -zv localhost 2049 2>&1 | grep -q "succeeded" && echo "  NFS Port: OK" || echo "  NFS Port: FAIL"

  # Mount test (from test pod)
  kubectl run nfs-test-$nas --image=alpine --rm -i --restart=Never -- sh -c "
    apk add --quiet nfs-utils && \
    mount -t nfs4 -o vers=4.0 file-sim-file-simulator-$nas.file-simulator.svc.cluster.local:/ /mnt && \
    echo '  Mount Test: OK' || echo '  Mount Test: FAIL'
  " 2>/dev/null

  echo ""
done
```

### Resource Utilization Tracking

**Key metrics per NAS:**
- Memory usage (vs 256Mi limit)
- CPU usage (vs 200m limit)
- Network I/O (NFS transfer rates)
- Storage usage (emptyDir size)

**Prometheus metrics (if enabled):**
```yaml
# ServiceMonitor for NAS instances
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: nas-servers
  namespace: file-simulator
spec:
  selector:
    matchLabels:
      simulator.protocol: nas
  endpoints:
    - port: metrics
      interval: 30s
```

### Troubleshooting Checklist

**Symptom: Pod stuck in Pending**
- Check: `kubectl describe pod -n file-simulator <pod-name>`
- Likely cause: Insufficient cluster resources or PVC not bound
- Action: Verify PV exists (`kubectl get pv`), check node capacity (`kubectl top nodes`)

**Symptom: Pod CrashLoopBackOff**
- Check: `kubectl logs -n file-simulator <pod-name>`
- Likely cause: NFS export failure (hostPath issue) or missing emptyDir volume
- Action: Verify emptyDir volume in deployment spec, check NFS_EXPORT_0 env var

**Symptom: NFS mount fails from client**
- Check: `showmount -e <nas-service-dns-name>` from client pod
- Likely cause: NFS not exporting directory or firewall blocking
- Action: Verify NFS_EXPORT_0 matches mount path, check network policies

**Symptom: File written to Windows not visible in NFS**
- Likely cause: No sync process from PVC to emptyDir
- Action: Implement InitContainer or sidecar sync (see Storage Architecture)

**Symptom: Service DNS not resolving**
- Check: `nslookup file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local` from client pod
- Likely cause: Service not created or selector mismatch
- Action: Verify service exists (`kubectl get svc -n file-simulator`), check label selectors match deployment

---

## Client Library Integration

### NfsFileService Configuration

**Current (single instance):**
```csharp
public class NfsServerOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 2049;
    public string MountPath { get; set; } = "/";
}
```

**Multi-instance approach (option 1: array):**
```csharp
public class NfsServerOptions
{
    public NfsEndpoint[] Endpoints { get; set; } = Array.Empty<NfsEndpoint>();
}

public class NfsEndpoint
{
    public string Name { get; set; }  // "nas-input-1"
    public string Host { get; set; }  // DNS name
    public int Port { get; set; } = 2049;
    public string MountPath { get; set; } = "/";
}
```

**Configuration (appsettings.json):**
```json
{
  "FileSimulator": {
    "Nfs": {
      "Endpoints": [
        {
          "Name": "nas-input-1",
          "Host": "file-sim-file-simulator-nas-input-1.file-simulator.svc.cluster.local",
          "Port": 2049,
          "MountPath": "/"
        },
        {
          "Name": "nas-output-1",
          "Host": "file-sim-file-simulator-nas-output-1.file-simulator.svc.cluster.local",
          "Port": 2049,
          "MountPath": "/"
        }
      ]
    }
  }
}
```

**Service registration:**
```csharp
services.Configure<NfsServerOptions>(configuration.GetSection("FileSimulator:Nfs"));

// Register one NfsFileService per endpoint
foreach (var endpoint in nfsOptions.Endpoints)
{
    services.AddKeyedSingleton<INfsFileService, NfsFileService>(endpoint.Name, sp =>
        new NfsFileService(Options.Create(endpoint), sp.GetRequiredService<ILogger<NfsFileService>>()));
}
```

**Usage:**
```csharp
public class FileProcessor
{
    private readonly IKeyedServiceProvider _serviceProvider;

    public async Task ProcessInputFileAsync(string fileName)
    {
        // Get specific NAS instance
        var nasInput1 = _serviceProvider.GetRequiredKeyedService<INfsFileService>("nas-input-1");
        var files = await nasInput1.DiscoverFilesAsync("/", "*.txt", CancellationToken.None);

        foreach (var file in files)
        {
            var content = await nasInput1.ReadFileAsync(file.FullPath, CancellationToken.None);
            // Process...

            // Write to output NAS
            var nasOutput1 = _serviceProvider.GetRequiredKeyedService<INfsFileService>("nas-output-1");
            await nasOutput1.WriteFileAsync($"/processed-{file.Name}", content, CancellationToken.None);
        }
    }
}
```

**Alternative approach (option 2: named options):**
```csharp
services.Configure<NfsServerOptions>("nas-input-1", config.GetSection("Nfs:Endpoints:0"));
services.Configure<NfsServerOptions>("nas-output-1", config.GetSection("Nfs:Endpoints:1"));

// Register with named options
services.AddSingleton<INfsFileService>(sp => new NfsFileService(
    sp.GetRequiredService<IOptionsMonitor<NfsServerOptions>>().Get("nas-input-1"),
    sp.GetRequiredService<ILogger<NfsFileService>>()
));
```

---

## Production Considerations

### High Availability

**Current design:** Single replica per NAS instance (replicas: 1)

**HA enhancement:**
```yaml
spec:
  replicas: 2  # Multiple replicas
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 1
```

**Challenge:** NFS requires stable server identity. Multiple replicas create multiple NFS servers, not HA cluster.

**True HA solutions:**
1. **NFS Ganesha in HA mode:** Requires shared storage backend (Ceph, GlusterFS)
2. **Persistent IP with pod anti-affinity:** Ensures one active replica per node
3. **External NFS load balancer:** Distribute client requests across replicas

**Recommendation:** For Minikube/dev environment, single replica sufficient. Production should use external enterprise NFS solution (NetApp, Dell EMC).

### Security Hardening

**Current NFS export:** `*(rw,sync,no_subtree_check,no_root_squash)` (allows all clients, root access)

**Hardened export:**
```yaml
env:
  - name: NFS_EXPORT_0
    value: "/data 10.244.0.0/16(rw,sync,no_subtree_check,root_squash,all_squash,anonuid=1000,anongid=1000)"
    # 10.244.0.0/16 = Kubernetes pod CIDR (restrict to cluster pods only)
    # root_squash = Map root to anonymous user (prevent root access)
    # all_squash = Map all users to anonymous user (prevent UID spoofing)
    # anonuid/anongid = Specific user ID for anonymous user
```

**Additional security:**
- **NetworkPolicy:** Restrict NFS pod traffic to specific namespaces/pods
- **PodSecurityPolicy:** Remove `privileged: true` if possible (NFS server may require)
- **Secret management:** Store NFS export config in Secret, not ConfigMap

### Backup and Disaster Recovery

**Backup strategy:**
1. **Windows-level backup:** C:\simulator-data\nas-* directories backed up via Windows Backup or Veeam
2. **Kubernetes-level backup:** PVC snapshots (requires CSI driver with snapshot support)
3. **Application-level backup:** Microservices copy files to S3 (nas-archive instance)

**Recovery procedure:**
1. Restore Windows directories from backup
2. Restart Minikube mount (`minikube start --mount`)
3. Redeploy NAS instances (`helm upgrade --install`)
4. Validate data integrity (hash comparison)

### Performance Tuning

**Current bottlenecks:**
- **hostPath I/O:** Windows NTFS performance limits
- **emptyDir sync:** InitContainer copy on every pod restart
- **Network:** NodePort introduces NAT overhead

**Optimizations:**
1. **Use SSD for Windows storage:** C:\simulator-data on SSD drive
2. **Sidecar sync instead of InitContainer:** Incremental sync (rsync) vs full copy
3. **NFSv4 mount options:** `vers=4.1,rsize=1048576,wsize=1048576` (larger buffer sizes)
4. **Async exports:** `async` instead of `sync` (faster writes, but risk of data loss on crash)

---

## Summary and Recommendations

### Recommended Architecture

**Pattern:** Helm range loop with per-instance PV/PVC (following existing ftp-multi.yaml pattern)

**Key components:**
1. Values array defining 7 NAS instances with unique names, NodePorts, hostPaths
2. Storage template creating PV/PVC pairs per instance
3. Deployment template with emptyDir for NFS exports + PVC for Windows access
4. Service template exposing each NAS with predictable DNS names

### Build Order

1. **Phase 1:** Create templates (non-disruptive)
2. **Phase 2:** Deploy single test instance (validate pattern)
3. **Phase 3:** Expand to 3 instances (validate scaling)
4. **Phase 4:** Full 7-instance deployment
5. **Phase 5:** Integrate with existing protocols (FTP/SFTP mount NAS shares)

### Critical Success Factors

- ✓ **YAML separators (`---`)**: Required between all resources in range loop
- ✓ **Scope management (`$` vs `.`)**: Always use `$` for root context in loops
- ✓ **emptyDir volumes**: Required for NFS exports (hostPath cannot be exported)
- ✓ **Per-instance PVC**: Ensures storage isolation between NAS instances
- ✓ **Windows directory pre-creation**: Must exist before PV hostPath binding
- ✓ **NodePort allocation**: Sequential 32150-32156 to avoid conflicts

### Open Questions for Phase-Specific Research

1. **Windows → emptyDir sync mechanism:** InitContainer (simple) vs Sidecar (continuous) vs Application-level (no sync)?
2. **Client discovery pattern:** Hardcoded DNS vs ConfigMap vs Kubernetes service discovery API?
3. **Resource limits:** Do 7 NAS instances fit in 8GB Minikube cluster or need memory increase?
4. **NFS mount options:** Which NFSv4 options provide best performance in Minikube?

---

## Sources

### Official Kubernetes Documentation
- [Persistent Volumes | Kubernetes](https://kubernetes.io/docs/concepts/storage/persistent-volumes/)
- [Service | Kubernetes](https://kubernetes.io/docs/concepts/services-networking/service/)
- [Kubernetes 1.27: NodePort Dynamic and Static Allocation](https://kubernetes.io/blog/2023/05/11/nodeport-dynamic-and-static-allocation/)

### Helm Best Practices
- [General Conventions | Helm](https://helm.sh/docs/chart_best_practices/conventions/)
- [Flow Control | Helm](https://helm.sh/docs/chart_template_guide/control_structures/)

### Multi-Instance Deployment Patterns
- [Creating multiple deployments with different configurations using Helm | Medium](https://medium.com/@pasternaktal/creating-multiple-deployments-with-different-configurations-using-helm-4992f9f735fd)
- [Loops in Helm Charts | Medium](https://mustafaak4096.medium.com/loops-in-helm-charts-259e1b9e8422)
- [Helm Loops Explained: A Practical Helm Hack](https://alexandre-vazquez.com/helm-loops-helm-chart-tricks-1/)

### NFS in Kubernetes
- [Deploying NFS Server in Kubernetes | GitHub](https://github.com/appscode/third-party-tools/blob/master/storage/nfs/README.md)
- [Integrating multiple NFS Server · Issue #153](https://github.com/kubernetes-sigs/nfs-subdir-external-provisioner/issues/153)
- [How To Configure NFS For Kubernetes Persistent Storage | ComputingForGeeks](https://computingforgeeks.com/configure-nfs-as-kubernetes-persistent-volume-storage/)

### Storage Patterns
- [Connect Multiple Pods to the same PVC with different mount path | Medium](https://sneegdhk.medium.com/connect-multiple-pods-to-the-same-pvc-with-different-mount-path-kubernetes-021e1f4e8946)
- [Kubernetes Persistent Volume: Examples & Best Practices | Loft](https://www.loft.sh/blog/kubernetes-persistent-volumes-examples-and-best-practices)

### NodePort and Networking
- [Kubernetes NodePorts - Static and Dynamic Assignments | Layer5](https://layer5.io/blog/kubernetes/kubernetes-nodeports-static-and-dynamic-assignments)
- [Why Kubernetes NodePort Services Range From 30000 – 32767 | Baeldung](https://www.baeldung.com/ops/kubernetes-nodeport-range)
- [NodePort :: The Kubernetes Networking Guide](https://www.tkng.io/services/nodeport/)

---

**Document Version:** 1.0
**Last Updated:** 2026-01-29
**Next Review:** After Phase 2 completion (single multi-instance deployment test)
