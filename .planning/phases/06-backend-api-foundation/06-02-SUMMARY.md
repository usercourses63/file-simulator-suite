---
phase: 06-backend-api-foundation
plan: 02
subsystem: infra
tags: [kubernetes, helm, rbac, control-plane, asp.net-core]

# Dependency graph
requires:
  - phase: 01-nfs-pattern-validation
    provides: Kubernetes cluster setup, Helm chart structure
  - phase: 06-01-backend-api-foundation
    provides: ASP.NET Core control API project structure
provides:
  - Helm templates for Control API deployment with RBAC
  - ServiceAccount with read-only Kubernetes API permissions
  - NodePort 30500 for external Windows host access
  - Resource-constrained deployment (256Mi/1Gi) coexisting with v1.0 servers
affects: [06-03-kubernetes-client, 07-react-dashboard, 11-dynamic-server-creation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Namespace-scoped RBAC with Role (not ClusterRole) for security"
    - "ServiceAccount per service for fine-grained permissions"
    - "Checksum annotation triggers pod restart on config changes"

key-files:
  created:
    - helm-chart/file-simulator/templates/control-api-rbac.yaml
    - helm-chart/file-simulator/templates/control-api.yaml
  modified:
    - helm-chart/file-simulator/values.yaml

key-decisions:
  - "Use Role (not ClusterRole) scoped to file-simulator namespace for security"
  - "Read-only permissions for Phase 6 (create/update/delete deferred to Phase 11)"
  - "NodePort 30500 avoids conflicts with existing protocol servers"
  - "256Mi request, 1Gi limit maintains cluster budget (~12% request, ~48% limit of 8GB)"

patterns-established:
  - "ServiceAccount naming: {release}-{chart}-{component}"
  - "RBAC rules defined in values.yaml for flexibility"
  - "Security context: runAsNonRoot with user 1000"
  - "Health probes on /health endpoint with separate liveness/readiness timing"

# Metrics
duration: 3min
completed: 2026-02-02
---

# Phase 6 Plan 2: Kubernetes Deployment Configuration Summary

**Helm templates deploy Control API with namespace-scoped RBAC (read-only K8s API access), NodePort 30500, and 256Mi/1Gi resource limits coexisting with 7 NAS + 6 protocol v1.0 servers**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-02T12:39:42Z
- **Completed:** 2026-02-02T12:42:43Z
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- Created Helm templates for Control API deployment with RBAC
- Namespace-scoped Role grants read permissions for pods, services, deployments, configmaps
- Service exposes NodePort 30500 for Windows host access
- Resource limits ensure Control API coexists with existing v1.0 servers without evictions

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Control API configuration to values.yaml** - `c6d093b` (feat)
2. **Task 2: Create RBAC template (ServiceAccount, Role, RoleBinding)** - `c829243` (feat)
3. **Task 3: Create Deployment and Service template** - `b2740b2` (feat)

## Files Created/Modified
- `helm-chart/file-simulator/values.yaml` - Added controlApi section with image, service, RBAC, resource configuration
- `helm-chart/file-simulator/templates/control-api-rbac.yaml` - ServiceAccount, Role, RoleBinding for K8s API access
- `helm-chart/file-simulator/templates/control-api.yaml` - Deployment with security context, health probes, Service with NodePort

## Decisions Made

**1. Use Role instead of ClusterRole**
- Scoped to file-simulator namespace only
- Control API only needs to discover servers in its own namespace
- Follows principle of least privilege
- Easier RBAC troubleshooting (namespace-scoped)

**2. Read-only permissions for Phase 6**
- get, list, watch on pods/services/deployments/configmaps
- No create/update/delete permissions yet
- Phase 11 will extend to dynamic server management

**3. NodePort 30500 for external access**
- Avoids conflicts with existing services (30021 FTP, 30022 SFTP, 30088 HTTP, etc.)
- Allows Windows host access: http://$(minikube -p file-simulator ip):30500
- Phase 7 React dashboard will connect through this port

**4. Resource limits maintain cluster budget**
- Control API: 256Mi request, 1Gi limit
- v1.0 servers: ~706Mi request, ~2.85Gi limit
- Total: ~962Mi request (~12% of 8GB), ~3.85Gi limit (~48% of 8GB)
- Sufficient headroom for Phase 7-9 additions

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for Phase 6 Plan 3 (Kubernetes Client Integration):**
- Helm templates render without errors (`helm lint` passes)
- RBAC resources properly structured (ServiceAccount → Role → RoleBinding)
- Resource budget verified (control-api shows 256Mi/1Gi in template output)
- NodePort 30500 assigned and verified

**Verification commands for deployment (after Plan 3):**
```bash
# Deploy with Helm
helm upgrade --install file-sim ./helm-chart/file-simulator \
  --kube-context=file-simulator \
  --namespace file-simulator

# Verify RBAC
kubectl --context=file-simulator get serviceaccount,role,rolebinding -n file-simulator | grep control-api

# Verify resource limits
kubectl --context=file-simulator get deployment file-sim-file-simulator-control-api -n file-simulator -o yaml | grep -A 5 resources

# Test external access (after pod is running)
curl http://$(minikube -p file-simulator ip):30500/health
```

**Next steps:**
- Plan 3 will implement KubernetesClient to consume these RBAC permissions
- Plan 4 will build discovery service using the K8s API
- Phase 7 will create React dashboard consuming NodePort 30500

---
*Phase: 06-backend-api-foundation*
*Completed: 2026-02-02*
