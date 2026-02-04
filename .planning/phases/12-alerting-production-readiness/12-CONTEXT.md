# Phase 12: Alerting and Production Readiness - Context

**Gathered:** 2026-02-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement alerting system for health/disk/Kafka monitoring, Redis backplane for SignalR scale-out, configuration persistence, AND full production containerization: all components run as Kubernetes pods accessible from outside cluster via `file-simulator.local` DNS. This is the extended scope combining original alerting goals with production deployment.

</domain>

<decisions>
## Implementation Decisions

### Alert Behavior & Notifications
- Toast notifications (corner) for new alerts + persistent banner at top for unresolved alerts
- Severity-based toast duration: Info=5s, Warning=10s, Critical=until dismissed
- No sound - visual only (dev environment)
- Dedicated Alerts tab alongside Servers/Files/History/Kafka
- Auto-resolve when condition clears (no manual acknowledgment workflow)

### Alert Thresholds & Retention
- Claude's discretion: disk space threshold and alert retention period
- Claude's discretion: Alerts tab filtering/search UI design

### Containerization Approach
- Multi-stage Dockerfile for dashboard (Node build -> nginx serve)
- Local registry for container images (run registry container, push images)
- nginx /health endpoint for K8s readiness/liveness probes
- Claude's discretion: API URL configuration approach
- Claude's discretion: caching headers configuration

### External Access & DNS
- Dynamic NAS servers get dedicated DNS: nas-{name}.file-simulator.local
- Reserved NodePort range 32150-32199 for dynamic NAS servers
- Kafka uses kafka.file-simulator.local:30094
- Connection-info API includes all servers (static and dynamic)
- Claude's discretion: Setup-Hosts.ps1 update mechanism for dynamic entries

### Deployment Automation
- Deploy-Production.ps1 with optional -Clean flag (delete+recreate cluster vs keep)
- Build all container images as part of deploy script
- Fix NFS issue in Helm chart itself (no separate patch needed)
- Stop on first error (fail fast)
- Verbose output showing each step
- Verification script tests ALL endpoints + protocols including dynamic NAS/servers

### Claude's Discretion
- Disk space threshold for alerts
- Alert history retention period
- Alerts tab filtering/search UI design
- API URL configuration approach for dashboard
- nginx caching headers
- Setup-Hosts.ps1 update mechanism

</decisions>

<specifics>
## Specific Ideas

- Verification script must test dynamic NAS and other dynamically created servers, not just static ones
- NFS fix should be incorporated into Helm chart so patch file is no longer needed
- External access table from plan:
  | Service | DNS Name | Port |
  |---------|----------|------|
  | Dashboard | file-simulator.local | 30080 |
  | Control API | file-simulator.local | 30500 |
  | FTP | file-simulator.local | 30021 |
  | SFTP | file-simulator.local | 30022 |
  | HTTP | file-simulator.local | 30088 |
  | S3 API | file-simulator.local | 30900 |
  | Kafka | kafka.file-simulator.local | 30094 |
  | Dynamic NAS | nas-{name}.file-simulator.local | 32150-32199 |

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope

</deferred>

---

*Phase: 12-alerting-production-readiness*
*Context gathered: 2026-02-04*
