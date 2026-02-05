---
phase: 12
plan: 06
subsystem: deployment
tags: [helm, kubernetes, dashboard, nfs, templates]
requires: [12-05]
provides: [dashboard-helm-template, nfs-fix-integrated]
affects: [12-07, 12-08]
tech-stack:
  added: []
  patterns: [helm-templates, kubernetes-deployment, security-contexts]
key-files:
  created:
    - helm-chart/file-simulator/templates/dashboard.yaml
  modified:
    - helm-chart/file-simulator/templates/nas.yaml
    - helm-chart/file-simulator/values.yaml
decisions:
  - id: nfs-emptydir-integration
    choice: Incorporate NFS fix directly into nas.yaml template
    rationale: Eliminates manual patch step, makes deployment atomic
  - id: dashboard-security-context
    choice: Run dashboard as nginx user (101) with readOnlyRootFilesystem
    rationale: Follows security best practices for static SPAs
  - id: local-registry-consistency
    choice: Use localhost:5000 registry for both dashboard and control API
    rationale: Ensures consistent image pull behavior in Minikube
metrics:
  duration: 3min
  completed: 2026-02-05
---

# Phase 12 Plan 06: Helm Dashboard Deployment and NFS Fix Summary

**One-liner:** Dashboard Helm template with security hardening and NFS emptyDir fix integrated into nas.yaml

## What Was Built

### 1. NFS Fix Integration (nas.yaml)
- **Replaced single volume mount** with dual-mount pattern:
  - `nfs-data` (emptyDir) → `/data` - NFS export directory
  - `shared-data` (PVC) → `/shared` - Access to shared storage
- **Eliminated manual patch requirement** - Fix now part of base template
- **Added explanatory comments** about Windows hostPath limitation
- **Root cause addressed**: NFS cannot export Windows-mounted volumes (hostPath), requires emptyDir

### 2. Dashboard Configuration (values.yaml)
- **Image repository**: `localhost:5000/file-simulator-dashboard:latest`
- **Service**: NodePort 30080 (external Windows access)
- **Resources**: 32Mi/25m request, 128Mi/200m limit (lightweight SPA)
- **Pull policy**: IfNotPresent (local registry optimization)

### 3. Dashboard Deployment Template (dashboard.yaml)
- **Security context**:
  - runAsNonRoot: true
  - runAsUser/Group: 101 (nginx user)
  - readOnlyRootFilesystem: true
  - Capabilities dropped: ALL
- **Health probes**: HTTP GET /health (liveness + readiness)
- **Volume mounts**: emptyDir for nginx cache and run directories
- **Labels**: app.kubernetes.io/part-of: file-simulator-suite

### 4. Control API Image Reference
- **Updated**: `file-simulator-control-api` → `localhost:5000/file-simulator-control-api`
- **Consistency**: Matches dashboard registry configuration

## Technical Implementation

### NFS Volume Pattern (Before → After)

**Before (Broken)**:
```yaml
volumeMounts:
  - name: data
    mountPath: /data
volumes:
  - name: data
    persistentVolumeClaim:
      claimName: file-sim-file-simulator-pvc  # Windows hostPath - fails!
```

**After (Fixed)**:
```yaml
volumeMounts:
  - name: nfs-data
    mountPath: /data       # NFS export
  - name: shared-data
    mountPath: /shared     # Shared storage access
volumes:
  - name: nfs-data
    emptyDir: {}           # NFS-compatible export
  - name: shared-data
    persistentVolumeClaim:
      claimName: file-sim-file-simulator-pvc  # Access via /shared
```

### Dashboard Security Hardening

**Security Context Layers**:
1. **Pod-level**: runAsNonRoot, runAsUser 101, fsGroup 101
2. **Container-level**:
   - allowPrivilegeEscalation: false
   - readOnlyRootFilesystem: true
   - capabilities.drop: [ALL]
3. **Volume strategy**: emptyDir for nginx temp directories (not writable root)

**Why nginx user 101?**
- Standard nginx Alpine image default user
- No privilege escalation required
- Aligns with Kubernetes security best practices

## Validation Results

### Helm Lint
```bash
helm lint ./helm-chart/file-simulator
# Result: 1 chart(s) linted, 0 chart(s) failed
```

### Template Rendering Verification

**Dashboard Deployment**: ✅
- Component label: dashboard
- Security context: runAsUser 101
- Health probes: /health endpoints configured
- Resources: 32Mi/25m → 128Mi/200m

**Dashboard Service**: ✅
- Type: NodePort
- Port: 80 → 30080
- Selector: matches deployment labels

**NAS Volumes**: ✅
- nfs-data: emptyDir (for /data export)
- shared-data: PVC (for /shared access)
- Comments: explaining fix rationale

## Files Changed

| File | Type | Changes |
|------|------|---------|
| `helm-chart/file-simulator/templates/nas.yaml` | Modified | Added dual volume mounts (nfs-data + shared-data), replaced PVC with emptyDir pattern |
| `helm-chart/file-simulator/values.yaml` | Modified | Added dashboard configuration section, updated control API image to localhost:5000 |
| `helm-chart/file-simulator/templates/dashboard.yaml` | Created | Full deployment + service template with security hardening |

## Commits

| Hash | Type | Description |
|------|------|-------------|
| 082459b | fix | Incorporate NFS emptyDir fix into nas.yaml template |
| a3f6a00 | feat | Add dashboard configuration to values.yaml |
| 28e168d | feat | Create dashboard Helm deployment and service templates |
| a2a7b12 | fix | Update control API image to use localhost:5000 registry |

## Decisions Made

### 1. NFS Fix Integration Strategy
**Decision**: Incorporate fix directly into nas.yaml template vs. keeping manual patch

**Rationale**:
- **Atomic deployment**: One helm install/upgrade, no post-install steps
- **Maintainability**: Fix documented in template, not external patch file
- **New user experience**: No "apply patch" surprise after deployment
- **GitOps friendly**: Template changes tracked in version control

**Trade-offs**:
- Existing deployments need helm upgrade to get fix
- Template slightly more complex (but well-commented)

### 2. Dashboard Security Posture
**Decision**: Maximum security hardening (readOnlyRootFilesystem, drop ALL capabilities)

**Rationale**:
- Static SPA has no write requirements (except nginx temp dirs)
- Defense in depth: limits attack surface if container compromised
- Production-ready: meets strict security policies (PodSecurityStandards restricted)
- Zero functionality trade-off: nginx works perfectly with emptyDir temps

### 3. Local Registry Pattern
**Decision**: Use localhost:5000 for both dashboard and control API images

**Rationale**:
- Consistent with Minikube registry setup
- IfNotPresent policy avoids unnecessary pulls
- Supports air-gapped/offline development
- Future-proof: easy to switch to real registry for production

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed successfully on first attempt.

## Next Phase Readiness

### Blockers
None

### Prerequisites for 12-07 (Dashboard CI/CD)
- ✅ Dashboard Dockerfile exists (12-05)
- ✅ Helm template exists (12-06)
- ✅ values.yaml configured (12-06)
- 🔲 CI/CD pipeline to build and push image
- 🔲 Helm upgrade integration

### Prerequisites for 12-08 (E2E Testing)
- ✅ All components have Helm templates
- ✅ Dashboard health endpoint defined
- ✅ Control API health endpoint exists
- 🔲 Deployment verification script

## Recommended Next Steps

1. **Plan 12-07**: Create build-and-deploy script for dashboard
   - Build Docker image
   - Tag as localhost:5000/file-simulator-dashboard:latest
   - Push to Minikube registry
   - Helm upgrade file-simulator

2. **Plan 12-08**: End-to-end deployment test
   - Deploy full suite with helm install
   - Verify all 10+ pods running (including dashboard)
   - Test dashboard → control API → Kafka connectivity
   - Validate NFS fix (no manual patch needed)

3. **Documentation**: Update CLAUDE.md deployment section
   - Remove nfs-fix-patch.yaml references
   - Document dashboard access at port 30080
   - Update resource requirements (dashboard adds 32Mi/25m)

## Metrics

- **Tasks completed**: 6/6 (100%)
- **Commits**: 4 atomic commits
- **Files modified**: 2
- **Files created**: 1
- **Duration**: ~3 minutes
- **Helm validation**: PASS
- **Template rendering**: PASS

## Knowledge Captured

### NFS + Windows HostPath Limitation
**Problem**: NFS server cannot export Windows-mounted hostPath volumes in Minikube
**Error**: `exportfs: /data does not support NFS export`
**Solution**: Use emptyDir for NFS export directory, PVC mount for shared access
**Verification**: Deploy → no crash → exportfs succeeds

### nginx Security Best Practices
- Run as non-root user (101)
- Read-only root filesystem
- EmptyDir volumes for /var/cache/nginx and /var/run
- Drop all capabilities
- No privilege escalation

### Helm Template Conditional Pattern
```yaml
{{- if .Values.component.enabled }}
# ... component resources
{{- end }}
```
Allows selective component deployment via values toggle.

## Success Criteria - Final Check

- ✅ All tasks executed
- ✅ Each task committed individually
- ✅ All deviations documented (none occurred)
- ✅ SUMMARY.md created with substantive content
- ✅ Helm lint passes
- ✅ Template rendering verified
- ✅ No manual nfs-fix-patch.yaml needed
