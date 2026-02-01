---
phase: 04-configuration-templates
plan: 02
subsystem: configuration
tags: [configmap, service-discovery, windows-automation, nas]

dependency_graph:
  requires:
    - 02-01-PLAN.md  # 7 NAS server deployment with NodePorts
    - values-multi-nas.yaml  # NAS server names and NodePort assignments
  provides:
    - nas-endpoints-configmap.yaml  # Service discovery for all 7 NAS servers
    - Enhanced setup-windows.ps1  # NAS directory automation
  affects:
    - 04-03-PLAN.md  # Example deployments will use this ConfigMap
    - Phase 5  # Documentation will reference ConfigMap usage

tech_stack:
  added: []
  patterns:
    - ConfigMap for service discovery
    - envFrom injection pattern
    - PowerShell directory automation

key_files:
  created:
    - helm-chart/file-simulator/examples/configmap/nas-endpoints-configmap.yaml
  modified:
    - scripts/setup-windows.ps1

decisions:
  - id: CFG-01
    what: ConfigMap includes both DNS names and NodePorts
    why: Applications need cluster-internal DNS for NFS mounts and external NodePorts for Windows/external access
    alternatives: [DNS only, NodePorts only, Separate ConfigMaps]
    impact: Single ConfigMap provides complete service discovery

  - id: CFG-02
    what: Minikube IP as placeholder requiring substitution
    why: Minikube IP changes on restart; cannot be hardcoded in version-controlled manifest
    alternatives: [Hardcoded IP, Helm value substitution, Post-deployment script]
    impact: User must substitute MINIKUBE_IP before applying ConfigMap

  - id: CFG-03
    what: ConfigMap namespace set to default
    why: Applications deploy to their own namespace; example uses default as common case
    alternatives: [file-simulator namespace, Template with namespace placeholder]
    impact: Users must adjust namespace or apply to their application namespace

  - id: CFG-04
    what: NAS directory creation integrated into setup-windows.ps1
    why: Single script ensures all prerequisites (base + NAS directories) created before deployment
    alternatives: [Separate script, Manual creation, Helm pre-install hook]
    impact: Seamless user experience; one script creates entire Windows environment

  - id: CFG-05
    what: README.txt in each NAS directory with purpose
    why: Clarifies which NAS server role (input/output/backup) each directory represents
    alternatives: [No documentation, Single README, File naming convention]
    impact: Self-documenting directories; reduces confusion about NAS topology

metrics:
  duration: 4 minutes
  completed: 2026-02-01
---

# Phase 4 Plan 2: NAS Endpoints ConfigMap and Windows Setup Summary

**One-liner:** ConfigMap with all 7 NAS service endpoints and automated Windows directory creation for NAS servers

## What Was Built

Created service discovery ConfigMap and enhanced Windows setup script for NAS infrastructure.

### 1. NAS Endpoints ConfigMap (helm-chart/file-simulator/examples/configmap/nas-endpoints-configmap.yaml)

**Purpose:** Centralized connection details for all 7 NAS servers

**Contents:**
- DNS names: `file-sim-nas-{name}.file-simulator.svc.cluster.local`
- NFS port: `2049` (standard)
- Export path: `/data` (unfs3 pattern)
- NodePorts: `32150-32156` (sequential assignment)
- PVC names: `nas-{name}-pvc` references
- Metadata: NAS count, namespace, Minikube IP placeholder

**Usage pattern:**
```yaml
envFrom:
  - configMapRef:
      name: file-simulator-nas-endpoints
```

All 7 NAS server endpoints injected as environment variables with prefix `NAS_{SERVER}_{FIELD}`.

**Validation:** Passes `kubectl apply --dry-run=client` validation

### 2. Enhanced setup-windows.ps1

**New functionality:**
- Creates all 7 NAS directories: `nas-input-1/2/3`, `nas-backup`, `nas-output-1/2/3`
- Generates `README.txt` in each directory with:
  - Server name
  - Creation timestamp
  - Purpose/role description
  - Usage instructions
- Updated directory structure summary
- Incremented from 5 to 6 steps

**Preserves existing:**
- Base directory creation (input, output, temp, config)
- Minikube mount configuration
- Environment file generation
- Helper scripts (FTP, SFTP, S3, SMB)
- Helm deployment option

## Verification Results

All verification criteria met:

1. **ConfigMap completeness:** 7 NAS servers × 5 fields = 35 data entries
2. **kubectl validation:** Dry-run successful, valid ConfigMap syntax
3. **NodePort matching:** All ports (32150-32156) match values-multi-nas.yaml
4. **Directory automation:** All 7 NAS directories created with README.txt
5. **Script integrity:** Existing functionality preserved, no syntax errors

## Decisions Made

### CFG-01: ConfigMap Includes Both DNS Names and NodePorts

**Decision:** Include both cluster-internal DNS names and external NodePorts in ConfigMap

**Rationale:**
- Applications mounting NFS use DNS names: `file-sim-nas-input-1.file-simulator.svc.cluster.local`
- External tools (Windows NFS client) use NodePorts: `<minikube-ip>:32150`
- Single ConfigMap provides complete service discovery for all access patterns

**Alternatives considered:**
- DNS only: Requires separate documentation for external access
- NodePorts only: No cluster-internal discovery
- Separate ConfigMaps: Redundant, harder to maintain

**Impact:** Applications get all connection details from single source

### CFG-02: Minikube IP as Placeholder Requiring Substitution

**Decision:** ConfigMap contains `<minikube-ip>` placeholder; users substitute with actual IP

**Rationale:**
- Minikube IP changes on profile restart/recreation
- Hardcoded IP would break after Minikube operations
- Version control shouldn't contain machine-specific values

**Substitution pattern:**
```bash
export MINIKUBE_IP=$(minikube ip -p file-simulator)
sed -i "s/<minikube-ip>/$MINIKUBE_IP/g" nas-endpoints-configmap.yaml
```

**Alternatives considered:**
- Hardcoded IP: Breaks on restart
- Helm value substitution: Requires Helm; ConfigMap is standalone kubectl manifest
- Post-deployment script: Adds complexity

**Impact:** One-time manual substitution before first apply; documented in ConfigMap header

### CFG-03: ConfigMap Namespace Set to Default

**Decision:** Example ConfigMap uses `namespace: default`

**Rationale:**
- Applications deploy to their own namespaces
- `default` is most common namespace for examples
- ConfigMaps are namespace-scoped; must be in same namespace as consuming pods

**User action:** Adjust namespace in manifest or apply to application namespace:
```bash
kubectl apply -f nas-endpoints-configmap.yaml -n <app-namespace>
```

**Alternatives considered:**
- `file-simulator` namespace: NAS infrastructure namespace; applications shouldn't deploy there
- Template with placeholder: Overcomplicates simple example

**Impact:** Clear example; users understand namespace requirement

### CFG-04: NAS Directory Creation Integrated into setup-windows.ps1

**Decision:** Add NAS directory creation as step 2 in existing setup-windows.ps1

**Rationale:**
- Single script creates entire Windows environment
- Ensures NAS directories exist before Minikube mount
- Maintains setup script as single entry point
- Follows existing pattern (base directories in step 1, NAS directories in step 2)

**Alternatives considered:**
- Separate script: Requires running multiple scripts; easy to forget
- Manual creation: Error-prone; inconsistent directory structure
- Helm pre-install hook: Runs in cluster, not on Windows host

**Impact:** Seamless setup experience; one script, all prerequisites

### CFG-05: README.txt in Each NAS Directory with Purpose

**Decision:** Generate README.txt in each NAS directory documenting server role

**Rationale:**
- 7 directories with cryptic names (`nas-input-1`, `nas-output-2`) need context
- README clarifies which directories are for input files, output files, or backup
- Self-documenting; reduces need to reference external documentation
- Timestamps show when directory was created

**Content structure:**
```
NAS Server: nas-input-1
Created: 2026-02-01 10:30:00
Purpose: Input files for processing

This directory is mounted into the nas-input-1 NAS server pod.
Files placed here will be available via NFS mount after pod restart.
```

**Alternatives considered:**
- No documentation: Users must reference topology documentation
- Single README: Harder to find relevant info when in specific directory
- File naming convention: Less flexible, no timestamp/instructions

**Impact:** User-friendly setup; directories self-explain their purpose

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

**Phase 4 Plan 3 (Example Deployments) ready to proceed:**
- ConfigMap available for envFrom injection in example deployments
- Windows directories auto-created by setup script
- Service discovery mechanism established

**Blockers:** None

**Concerns:** None

## Technical Notes

### ConfigMap Service Discovery Pattern

Applications have two ways to consume NAS endpoints:

**1. Volume mounts (PV/PVC):**
```yaml
volumes:
  - name: nas-input-1
    persistentVolumeClaim:
      claimName: nas-input-1-pvc
volumeMounts:
  - name: nas-input-1
    mountPath: /mnt/input-1
```

**2. Environment variables (ConfigMap):**
```yaml
envFrom:
  - configMapRef:
      name: file-simulator-nas-endpoints
```

ConfigMap complements PV/PVC by providing:
- NodePort values for external access
- PVC name references for programmatic PVC creation
- Metadata for multi-NAS orchestration logic

### Windows Setup Enhancement

**Step numbering:**
- Step 1: Base directories (input, output, temp, config)
- Step 2: NAS directories (nas-input-1/2/3, nas-backup, nas-output-1/2/3)
- Step 3: Minikube mount configuration
- Step 4: Environment file generation
- Step 5: Helper scripts
- Step 6: Helm deployment (optional)

**Directory structure after setup:**
```
C:\simulator-data\
├── input\           # General files (original pattern)
├── output\          # General files (original pattern)
├── temp\            # General files (original pattern)
├── config\          # Helper scripts (original pattern)
├── nas-input-1\     # NEW: NAS server 1 (input)
├── nas-input-2\     # NEW: NAS server 2 (input)
├── nas-input-3\     # NEW: NAS server 3 (input)
├── nas-backup\      # NEW: Backup server (read-only)
├── nas-output-1\    # NEW: NAS server 1 (output)
├── nas-output-2\    # NEW: NAS server 2 (output)
└── nas-output-3\    # NEW: NAS server 3 (output)
```

### Minikube IP Substitution

**Command sequence:**
```powershell
# Get Minikube IP
$MINIKUBE_IP = minikube ip -p file-simulator

# Substitute in ConfigMap (PowerShell)
(Get-Content nas-endpoints-configmap.yaml) -replace '<minikube-ip>', $MINIKUBE_IP | Set-Content nas-endpoints-configmap.yaml

# Apply to application namespace
kubectl apply -f nas-endpoints-configmap.yaml -n <app-namespace>
```

**Bash alternative:**
```bash
export MINIKUBE_IP=$(minikube ip -p file-simulator)
sed -i "s/<minikube-ip>/$MINIKUBE_IP/g" nas-endpoints-configmap.yaml
kubectl apply -f nas-endpoints-configmap.yaml -n <app-namespace>
```

## Implementation Summary

**Commits:**
- `83192fc` - feat(04-02): create NAS endpoints ConfigMap for service discovery
- `fc0bfbc` - feat(04-02): enhance setup-windows.ps1 with NAS directory automation

**Files created:**
1. `helm-chart/file-simulator/examples/configmap/nas-endpoints-configmap.yaml` (107 lines)

**Files modified:**
1. `scripts/setup-windows.ps1` (55 lines added, 14 modified)

**Total changes:**
- 162 lines added
- 14 lines modified
- 2 commits
- 2 tasks completed
- 0 deviations

**Duration:** 4 minutes (start: 1769950110, end: 1769950333, delta: 223 seconds)

## Links

- **Plan:** `.planning/phases/04-configuration-templates/04-02-PLAN.md`
- **Research:** `.planning/phases/04-configuration-templates/04-RESEARCH.md`
- **Dependencies:** `helm-chart/file-simulator/values-multi-nas.yaml`
- **Next:** `.planning/phases/04-configuration-templates/04-03-PLAN.md` (Example Deployments)
