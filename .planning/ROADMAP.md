# Roadmap: File Simulator Suite

## Milestones

- ✅ **v1.0 Multi-NAS Production Topology** - Phases 1-5 (shipped 2026-02-01)
- ✅ **v2.0 Simulator Control Platform** - Phases 6-14 (shipped 2026-02-05)
- ✅ **v2.2.0 Activity Log Completeness** - Phases 15-19 (shipped 2026-02-12)
- 🔄 **v2.3.0 Dashboard UI Refactoring** - Phases 20-25 (in progress)

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

<details>
<summary>v2.2.0 Activity Log Completeness (Phases 15-19) - SHIPPED 2026-02-12</summary>

**Milestone Goal:** Make the activity log a complete audit trail of all server and file operations, add rename API, write comprehensive E2E tests, and validate fresh install from scratch.

- [x] Phase 15: Backend API Enhancements (1/1 plans) - 2026-02-12
- [x] Phase 16: Frontend Activity Log Completeness (1/1 plans) - 2026-02-12
- [x] Phase 17: Version Bump + Infrastructure (1/1 plans) - 2026-02-12
- [x] Phase 18: Comprehensive E2E Tests (2/2 plans) - 2026-02-12
- [x] Phase 19: Fresh Install Validation + Release (2/2 plans) - 2026-02-12

</details>

### v2.3.0 Dashboard UI Refactoring (Phases 20-25)

**Milestone Goal:** Refactor the Servers page for better visual differentiation, wider layout, compact NAS display, and directory-based NAS grouping — making 14+ servers instantly scannable at a glance. Secondary improvements to History, Files, Kafka, and Alerts tabs.

### Phase 20: Wider Layout and Collapsible Sidebar
**Goal**: Remove layout constraints and make activity sidebar collapsible for maximum server grid space
**Requirements**: LAYOUT-01, LAYOUT-02, LAYOUT-03, LAYOUT-04
**Success Criteria**:
1. Main content area no longer capped at 1400px — uses full viewport width
2. Activity sidebar has a toggle button that collapses/expands it
3. Server grid fills entire width when sidebar is collapsed
4. Grid adapts from single column (mobile) to 6+ cards per row (ultra-wide)

Plans:
- [x] 20-01: Remove max-width cap, add sidebar toggle, update responsive grid

### Phase 21: Protocol Color System
**Goal**: Apply protocol-specific color tinting to server cards and unify the color system
**Requirements**: VIS-01, VIS-02, VIS-03, VIS-04
**Success Criteria**:
1. Each protocol card has a subtle tinted background (FTP=purple, SFTP=pink, HTTP=blue, S3=orange, SMB=green, NAS=teal)
2. Static (Helm) and dynamic servers have distinct visual badges
3. Protocol badge colors match card tinting (single source of truth for protocol colors)
4. Health states (healthy/unhealthy/stopped) are more visually distinct (beyond left border)

Plans:
- [x] 21-01: Add protocol-tinted card backgrounds and unified color variables
- [x] 21-02: Add static/dynamic badges and enhanced health state indicators

### Phase 22: NAS Compact Table View
**Goal**: Replace NAS card grid with compact grouped table rows that show all 7 NAS servers without scrolling
**Requirements**: NAS-01, NAS-02, NAS-03, NAS-04, NAS-05
**Success Criteria**:
1. NAS servers render as compact table rows instead of full-size cards
2. Rows grouped under Input (3), Output (3), and Backup (1) sub-headers
3. Sub-group headers display server count and aggregate health (e.g., "Input Servers (3) — All Healthy")
4. All 7 NAS visible without scrolling at 1920x1080
5. Clicking a NAS row expands to show sparkline and additional metrics

Plans:
- [x] 22-01: Create NasTable component with grouped rows and sub-headers
- [x] 22-02: Add expandable row details with sparkline integration

### Phase 23: Server Details Panel Adaptation
**Goal**: Ensure details panel works with both protocol cards and NAS table rows
**Requirements**: DETAILS-01, DETAILS-02
**Success Criteria**:
1. Clicking a protocol server card opens the details panel (existing behavior preserved)
2. Clicking a NAS table row opens the same details panel with full server info
3. Details panel shows: name, protocol, health, ports, endpoints, and actions

Plans:
- [x] 23-01: Wire NAS row click to existing details panel, verify all info displayed

### Phase 24: Secondary Tab Improvements
**Goal**: Quick layout and data quality improvements for History, Files, Kafka, and Alerts tabs
**Requirements**: HIST-01, HIST-02, FILES-01, KAFKA-01, ALERTS-01
**Plans**: 2 plans
**Success Criteria**:
1. History server dropdown only shows currently deployed servers (no deleted dynamic servers)
2. History chart legend only includes servers with data in selected range
3. Files tab sidebar increased to 400px
4. Kafka side panels use 300px+ width on wide viewports
5. Alerts table columns use flexible widths without hardcoded max-widths

Plans:
- [x] 24-01: History tab server filter cleanup and legend optimization
- [x] 24-02: Files, Kafka, Alerts tab minor layout adjustments

### Phase 25: E2E Tests + Version Bump + Release
**Goal**: Validate all UI changes with E2E tests and release v2.3.0
**Requirements**: TEST-01, TEST-02, TEST-03
**Success Criteria**:
1. E2E test verifies protocol-tinted colors on server cards
2. E2E test verifies all 7 NAS servers visible in compact view
3. E2E test verifies details panel opens from both card and NAS row
4. All existing E2E tests still pass (no regression)
5. Version bumped to 2.3.0 and release created

Plans:
- [ ] 25-01: Write E2E tests for new UI components
- [ ] 25-02: Version bump, build, deploy, and create release

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
| 19. Fresh Install Validation + Release | v2.2.0 | 2/2 | Complete | 2026-02-12 |
| 20. Wider Layout and Collapsible Sidebar | v2.3.0 | 1/1 | Complete | 2026-02-12 |
| 21. Protocol Color System | v2.3.0 | 2/2 | Complete | 2026-02-12 |
| 22. NAS Compact Table View | v2.3.0 | 2/2 | Complete | 2026-02-12 |
| 23. Server Details Panel Adaptation | v2.3.0 | 1/1 | Complete | 2026-02-12 |
| 24. Secondary Tab Improvements | v2.3.0 | 2/2 | Complete | 2026-02-12 |
| 25. E2E Tests + Version Bump + Release | v2.3.0 | 0/2 | Pending | — |

---
*Last updated: 2026-02-12 - Phase 24 complete*
