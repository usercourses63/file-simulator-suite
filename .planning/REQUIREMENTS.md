# Requirements: v2.2.0 Activity Log Completeness

## Milestone Requirements

### Activity Log - Server Events (SRVLOG)
- [ ] **SRVLOG-01**: Activity log shows "server created" event for every dynamic server creation (FTP, SFTP, NAS)
- [ ] **SRVLOG-02**: Activity log shows "server healthy" event when dynamic servers become ready
- [ ] **SRVLOG-03**: Activity log shows "server deleted" event when dynamic servers are removed

### Activity Log - File Events (FILELOG)
- [ ] **FILELOG-01**: Activity log shows "file written to {server}" for file uploads on all servers (static + dynamic)
- [ ] **FILELOG-02**: Activity log shows "file read from {server}" when files are downloaded via the API
- [ ] **FILELOG-03**: Activity log shows "file deleted from {server}" for file deletions on all servers
- [ ] **FILELOG-04**: Activity log shows "file renamed from {old} to {new} on {server}" for file renames
- [ ] **FILELOG-05**: Server attribution uses protocol-based fallback when file path doesn't match a known server name

### API - File Operations (API)
- [ ] **API-01**: `PUT /api/files/rename` endpoint renames files and triggers FileSystemWatcher rename event with OldPath
- [ ] **API-02**: Download endpoint (`GET /api/files/download`) emits "Read" file event via SignalR after successful download

### E2E Tests (TEST)
- [ ] **TEST-01**: E2E test creates/deletes all server types (FTP, SFTP, 3x NAS) and verifies created/healthy/deleted events in activity log
- [ ] **TEST-02**: E2E test performs all file operations (write, read, delete, rename) on static servers and verifies each event in activity log
- [ ] **TEST-03**: E2E test uploads files to multiple dynamic NAS servers and verifies correct server attribution in activity log
- [ ] **TEST-04**: E2E test renames a file and verifies old and new name appear in activity log

### Infrastructure (INFRA)
- [ ] **INFRA-01**: Install-Simulator.ps1 updated with correct defaults (12GB RAM, 4 CPUs) and --kube-context on all kubectl commands
- [ ] **INFRA-02**: Version bumped to 2.2.0 in package.json, Chart.yaml, values.yaml, and CLAUDE.md

### Validation (VAL)
- [ ] **VAL-01**: Fresh Minikube install from scratch using Install-Simulator.ps1 succeeds with all pods healthy
- [ ] **VAL-02**: All E2E Playwright tests pass on fresh install
- [ ] **VAL-03**: TestConsole protocol tests pass on fresh install
- [ ] **VAL-04**: GitHub release v2.2.0 created with tag and release notes

## Future Requirements

(None deferred)

## Out of Scope

- Real-time file sync notifications (push from server to client) — activity log is poll-based via SignalR events
- File content diff/versioning — activity log tracks events, not content changes
- Activity log persistence to database — in-memory 50-event rolling buffer is sufficient for v2.2.0

## Traceability

| Requirement | Phase | Plan |
|-------------|-------|------|
| SRVLOG-01 | 16 | — |
| SRVLOG-02 | 16 | — |
| SRVLOG-03 | 16 | — |
| FILELOG-01 | 16 | — |
| FILELOG-02 | 15, 16 | — |
| FILELOG-03 | 16 | — |
| FILELOG-04 | 15, 16 | — |
| FILELOG-05 | 16 | — |
| API-01 | 15 | — |
| API-02 | 15 | — |
| TEST-01 | 18 | — |
| TEST-02 | 18 | — |
| TEST-03 | 18 | — |
| TEST-04 | 18 | — |
| INFRA-01 | 17 | — |
| INFRA-02 | 17 | — |
| VAL-01 | 19 | — |
| VAL-02 | 19 | — |
| VAL-03 | 19 | — |
| VAL-04 | 19 | — |

---
*Created: 2026-02-12 for milestone v2.2.0*
