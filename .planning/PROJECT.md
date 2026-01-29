# File Simulator Suite - Multi-NAS Production Topology

## What This Is

A Kubernetes-based file protocol simulator that replicates production OCP network topology for development testing. Provides 7 independent NAS servers (3 input, 1 backup, 3 output) that expose Windows directories via NFS, enabling systems under development to access test files through production-like PV/PVC mounts while testers manage files directly on Windows.

## Core Value

Development systems must connect to simulated NAS servers using identical PV/PVC configurations as production OCP, with test files written on Windows immediately visible through NFS mounts - zero deployment differences between dev and prod.

## Requirements

### Validated

Existing capabilities from current codebase:

- ✓ Single NFS server deployment via Helm chart — existing
- ✓ FTP, SFTP, HTTP, WebDAV, S3, SMB protocol servers operational — existing
- ✓ Shared PVC storage at /mnt/simulator-data from Windows mount — existing
- ✓ Management UI (FileBrowser) for file browsing — existing
- ✓ Multi-protocol file access tested and working — existing
- ✓ Kubernetes deployment with Hyper-V Minikube driver — existing
- ✓ Cross-cluster access via NodePort services — existing

### Active

Production topology simulation for development testing:

- [ ] 7 independent NAS servers (3 input, 1 backup, 3 output) deployed as separate pods
- [ ] Each NAS server exports Windows directory via NFS (files written on Windows visible via NFS mount)
- [ ] Each NAS has unique service DNS name (nas-input-1, nas-input-2, etc.)
- [ ] System under development can mount each NAS via separate PV/PVC
- [ ] Test files placed in C:\simulator-data\nas-input-1\ immediately accessible via NFS mount
- [ ] Files written by system via NFS mount appear in corresponding Windows directory
- [ ] NFS servers survive pod restarts without losing Windows directory mapping
- [ ] Configuration matches production OCP topology (7 NAS devices)

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

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Multiple NFS servers vs single with exports | Production has multiple physical NAS devices; dev must match topology | — Pending |
| 7 total NAS servers (3 input, 1 backup, 3 output) | Matches production network configuration | — Pending |
| Windows directories as source of truth | Testers work on Windows; test files must be accessible via NFS | — Pending |

---
*Last updated: 2026-01-29 after initialization*
