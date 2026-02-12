# Roadmap: File Simulator Suite

## Milestones

- ✅ **v1.0 Multi-NAS Production Topology** - Phases 1-5 (shipped 2026-02-01)
- ✅ **v2.0 Simulator Control Platform** - Phases 6-14 (shipped 2026-02-05)
- 🔄 **v2.2.0 Activity Log Completeness** - Phases 15-19 (in progress)

## Phases

<details>
<summary>v1.0 Multi-NAS Production Topology (Phases 1-5) - SHIPPED 2026-02-01</summary>

### Phase 1: NFS Pattern Validation
**Goal**: Validate init container + unfs3 pattern for exposing Windows directories via NFS
**Plans**: 3 plans

Plans:
- [x] 01-01: Initial NFS architecture research and proof of concept
- [x] 01-02: Windows directory sync mechanism implementation
- [x] 01-03: Basic health checks and validation testing

### Phase 2: Multi-NAS Architecture
**Goal**: Deploy 7 independent NAS servers matching production topology
**Plans**: 3 plans

Plans:
- [x] 02-01: Helm chart refactoring for multi-instance support
- [x] 02-02: ConfigMap service discovery implementation
- [x] 02-03: Server isolation and validation testing

### Phase 3: Bidirectional Sync
**Goal**: Implement bidirectional sync (Windows->NFS + NFS->Windows)
**Plans**: 2 plans

Plans:
- [x] 03-01: Init container sync (Windows->NFS)
- [x] 03-02: Sidecar sync (NFS->Windows) with selective deployment

### Phase 4: Static PV/PVC Provisioning
**Goal**: Deliver production-matching static PV/PVC manifests
**Plans**: 2 plans

Plans:
- [x] 04-01: Static PV/PVC template creation
- [x] 04-02: Multi-NAS mount example and documentation

### Phase 5: Comprehensive Testing
**Goal**: Validate all 7 servers with comprehensive test suite
**Plans**: 1 plan

Plans:
- [x] 05-01: 57-test suite covering health, sync, isolation, persistence

</details>

<details>
<summary>v2.0 Simulator Control Platform (Phases 6-14) - SHIPPED 2026-02-05</summary>

**Milestone Goal:** Transform the simulator into an observable, controllable platform with real-time monitoring, dynamic server management, and Kafka integration - enabling self-service test environment orchestration.

**Archive:** See `.planning/milestones/v2.0-ROADMAP.md` for full phase details.

- [x] Phase 6: Backend API Foundation (3/3 plans) - 2026-02-02
- [x] Phase 7: Real-Time Monitoring Dashboard (4/4 plans) - 2026-02-02
- [x] Phase 8: File Operations and Event Streaming (6/6 plans) - 2026-02-02
- [x] Phase 9: Historical Metrics and Storage (6/6 plans) - 2026-02-03
- [x] Phase 10: Kafka Integration for Event Streaming (7/7 plans) - 2026-02-03
- [x] Phase 11: Dynamic Server Management (10/10 plans) - 2026-02-04
- [x] Phase 12: Alerting and Production Readiness (10/10 plans) - 2026-02-05
- [x] Phase 13: TestConsole Modernization and Release (8/8 plans) - 2026-02-05
- [x] Phase 14: Comprehensive API-Driven Integration Testing (10/10 plans) - 2026-02-05

</details>

### v2.2.0 Activity Log Completeness (Phases 15-19)

**Milestone Goal:** Make the activity log a complete audit trail of all server and file operations, add rename API, write comprehensive E2E tests, and validate fresh install from scratch.

### Phase 15: Backend API Enhancements
**Goal**: Add rename endpoint and read event emission to Control API
**Requirements**: API-01, API-02
**Success Criteria**:
1. `PUT /api/files/rename` renames files and FileSystemWatcher emits Renamed event with OldPath
2. `GET /api/files/download` broadcasts "Read" FileEvent via SignalR after successful download
3. Both endpoints return appropriate HTTP status codes and error messages

Plans:
- [x] 15-01: Add rename endpoint and read event emission to FilesController

### Phase 16: Frontend Activity Log Completeness
**Goal**: Handle all file event types in activity log and improve server attribution
**Requirements**: SRVLOG-01, SRVLOG-02, SRVLOG-03, FILELOG-01, FILELOG-02, FILELOG-03, FILELOG-04, FILELOG-05
**Success Criteria**:
1. Activity log shows created/healthy/deleted events for all dynamic server types
2. Activity log shows written/read/deleted/renamed events for all files with server attribution
3. Files on static servers attributed via protocol-based fallback (NAS storage, FTP server, shared storage)
4. Renamed events show both old and new file names

Plans:
- [x] 16-01: Handle Renamed and Read events in App.tsx, update types and ActivityLog component

### Phase 17: Version Bump + Infrastructure
**Goal**: Update all version references to 2.2.0 and fix install script defaults
**Requirements**: INFRA-01, INFRA-02
**Success Criteria**:
1. All version references show 2.2.0 (package.json, Chart.yaml, values.yaml, CLAUDE.md)
2. Dashboard header badge displays v2.2.0
3. Install-Simulator.ps1 uses 12GB RAM, 4 CPUs, and --kube-context on all kubectl commands

Plans:
- [x] 17-01: Version bump and Install-Simulator.ps1 fixes

### Phase 18: Comprehensive E2E Tests
**Goal**: Write and pass E2E tests covering all server and file activity scenarios
**Requirements**: TEST-01, TEST-02, TEST-03, TEST-04
**Success Criteria**:
1. Test creates 5 dynamic servers (FTP, SFTP, 3x NAS) and verifies activity log events
2. Test performs all file operations on static servers and verifies each event type
3. Test verifies correct server attribution for files on dynamic NAS servers
4. Test verifies file rename shows old and new name
5. All tests pass on deployed cluster

Plans:
- [x] 18-01: Write ServerAndFileActivityTests.cs with 4 test methods
- [x] 18-02: Run tests and iterate on fixes

### Phase 19: Fresh Install Validation + Release
**Goal**: Validate entire system from scratch and publish v2.2.0 release
**Requirements**: VAL-01, VAL-02, VAL-03, VAL-04
**Success Criteria**:
1. Minikube deleted and recreated from scratch via Install-Simulator.ps1
2. All E2E Playwright tests pass on fresh install
3. TestConsole protocol tests pass on fresh install
4. GitHub release v2.2.0 published with tag and release notes

Plans:
- [ ] 19-01: Delete Minikube, fresh install, build and load images
- [ ] 19-02: Run all tests and create release

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. NFS Pattern Validation | v1.0 | 3/3 | Complete | 2026-01-29 |
| 2. Multi-NAS Architecture | v1.0 | 3/3 | Complete | 2026-01-30 |
| 3. Bidirectional Sync | v1.0 | 2/2 | Complete | 2026-01-31 |
| 4. Static PV/PVC Provisioning | v1.0 | 2/2 | Complete | 2026-02-01 |
| 5. Comprehensive Testing | v1.0 | 1/1 | Complete | 2026-02-01 |
| 6. Backend API Foundation | v2.0 | 3/3 | Complete | 2026-02-02 |
| 7. Real-Time Monitoring Dashboard | v2.0 | 4/4 | Complete | 2026-02-02 |
| 8. File Operations and Event Streaming | v2.0 | 6/6 | Complete | 2026-02-02 |
| 9. Historical Metrics and Storage | v2.0 | 6/6 | Complete | 2026-02-03 |
| 10. Kafka Integration for Event Streaming | v2.0 | 7/7 | Complete | 2026-02-03 |
| 11. Dynamic Server Management | v2.0 | 10/10 | Complete | 2026-02-04 |
| 12. Alerting and Production Readiness | v2.0 | 10/10 | Complete | 2026-02-05 |
| 13. TestConsole Modernization and Release | v2.0 | 8/8 | Complete | 2026-02-05 |
| 14. Comprehensive API-Driven Integration Testing | v2.0 | 10/10 | Complete | 2026-02-05 |
| 15. Backend API Enhancements | v2.2.0 | 1/1 | Complete | 2026-02-12 |
| 16. Frontend Activity Log Completeness | v2.2.0 | 1/1 | Complete | 2026-02-12 |
| 17. Version Bump + Infrastructure | v2.2.0 | 1/1 | Complete | 2026-02-12 |
| 18. Comprehensive E2E Tests | v2.2.0 | 2/2 | Complete | 2026-02-12 |
| 19. Fresh Install Validation + Release | v2.2.0 | 0/2 | Not Started | — |

---
*Last updated: 2026-02-12 - v2.2.0 milestone initialized*
