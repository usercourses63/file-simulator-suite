# File Simulator Suite - Multi-NAS Production Topology

## What This Is

A Kubernetes-based file protocol simulator that replicates production OCP network topology for development testing. Provides 7 independent NAS servers (3 input, 1 backup, 3 output) that expose Windows directories via NFS, enabling systems under development to access test files through production-like PV/PVC mounts while testers manage files directly on Windows.

## Core Value

Development systems must connect to simulated NAS servers using identical PV/PVC configurations as production OCP, with test files written on Windows immediately visible through NFS mounts - zero deployment differences between dev and prod.

## Requirements

### Validated

v1.0 Multi-NAS Production Topology (Shipped: 2026-02-01):

- ✓ 7 independent NAS servers (3 input, 1 backup, 3 output) with unique DNS names — v1.0
- ✓ Each NAS exports Windows directory via NFS with init container sync pattern — v1.0
- ✓ Bidirectional sync: Windows→NFS (init) + NFS→Windows (sidecar, 15-30s) — v1.0
- ✓ Static PV/PVC provisioning matching production OCP patterns — v1.0
- ✓ ConfigMap service discovery for all 7 NAS servers — v1.0
- ✓ Multi-NAS mount example (6 servers simultaneously) — v1.0
- ✓ Comprehensive test suite (57 tests: health, isolation, persistence) — v1.0
- ✓ Windows directory automation via enhanced setup-windows.ps1 — v1.0
- ✓ 1200+ line integration guide (NAS-INTEGRATION-GUIDE.md) — v1.0

Existing capabilities (pre-v1.0):

- ✓ Single NFS server deployment via Helm chart — existing
- ✓ FTP, SFTP, HTTP, WebDAV, S3, SMB protocol servers operational — existing
- ✓ Shared PVC storage at /mnt/simulator-data from Windows mount — existing
- ✓ Management UI (FileBrowser) for file browsing — existing
- ✓ Multi-protocol file access tested and working — existing
- ✓ Kubernetes deployment with Hyper-V Minikube driver — existing
- ✓ Cross-cluster access via NodePort services — existing

### Active

(No active requirements - next milestone not yet defined)

### Out of Scope

- Real-time sync between protocols (intentional - each protocol can have different files)
- Single NAS with multiple exports (production uses multiple physical devices)
- NFS performance optimization (development simulator, not production storage)

## Context

**Production Environment:**
- OCP (OpenShift Container Platform) with multiple physical NAS devices
- System connects to different NAS servers for input/output/backup via NFS
- Configuration specifies which NAS for which purpose

**Development Environment:**
- Minikube (Hyper-V driver) on Windows
- System under development runs in Kubernetes cluster
- Test suite runs on Windows, writes test files to directories
- Same PV/PVC configuration as production must work in dev

**Current NFS Limitation:**
- NFS server crashes when trying to export Windows-mounted hostPath
- Current workaround uses emptyDir (isolates from Windows - breaks dev/prod parity)
- Need solution that exposes Windows directories via NFS exports

**Known Working Patterns:**
- FTP, SFTP, SMB successfully expose Windows-mounted directories
- Kubernetes hostPath PVC at /mnt/simulator-data works for other protocols
- Multiple protocol servers can share same PVC successfully

## Constraints

- **Platform**: Minikube with Hyper-V driver (existing, working)
- **Storage**: Windows directories at C:\simulator-data must be source of truth
- **Production Parity**: NFS configuration in dev must match production exactly
- **No Data Loss**: Windows files persist across pod restarts
- **Filesystem Limitation**: NFS cannot directly export Windows CIFS/9p mounted filesystems (known Linux kernel limitation)

## Current State

**Shipped version:** v1.0 (2026-02-01)
**System status:** Production-ready 7-server NAS topology
**Tech stack:** Kubernetes, Helm, unfs3, rsync, PowerShell
**Lines of code:** ~15,000 (YAML + PowerShell)
**Test coverage:** 57 tests across 5 phases (health, sync, isolation, persistence)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Multiple NFS servers vs single with exports | Production has multiple physical NAS devices; dev must match topology | ✓ Good - 7 servers deployed, validated in Phase 2 |
| 7 total NAS servers (3 input, 1 backup, 3 output) | Matches production network configuration | ✓ Good - Topology matches OCP architecture |
| Windows directories as source of truth | Testers work on Windows; test files must be accessible via NFS | ✓ Good - Bidirectional sync working (15-30s latency) |
| unfs3 vs kernel NFS | Kernel NFS cannot export Windows mounts; unfs3 userspace workaround | ✓ Good - Pattern validated in Phase 1 |
| Init container + sidecar sync architecture | Separate one-way syncs prevent loops; native sidecar for lifecycle | ✓ Good - No sync loops, proper ordering |
| Static PV/PVC provisioning | Matches production OCP patterns better than dynamic provisioning | ✓ Good - Label selector binding reliable |
| Selective sidecar deployment | Only output servers need NFS→Windows sync; avoid overhead on inputs | ✓ Good - Resource efficient (96Mi vs 128Mi) |
| kubectl --context mandatory | Multi-profile Minikube safety; prevent cross-cluster accidents | ✓ Good - Zero accidental deletions in v1.0 |

---
*Last updated: 2026-02-01 after v1.0 milestone*
