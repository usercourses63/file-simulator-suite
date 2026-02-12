# Requirements: v2.3.0 Dashboard UI Refactoring

## Milestone Requirements

### Servers - Visual Differentiation (VIS) — Priority: HIGH
- [ ] **VIS-01**: Server cards have protocol-tinted backgrounds using existing color palette (FTP=#8b5cf6, SFTP=#ec4899, HTTP=#3b82f6, S3=#f97316, SMB=#22c55e, NAS=#14b8a6)
- [ ] **VIS-02**: Static (Helm) vs Dynamic servers visually differentiated with subtle badge or icon
- [ ] **VIS-03**: Protocol badge colors consistent with card tinting (unified color system)
- [ ] **VIS-04**: Health status (healthy/unhealthy/stopped) visually distinct beyond just left border color

### Servers - Layout (LAYOUT) — Priority: HIGH
- [ ] **LAYOUT-01**: Remove 1400px max-width cap on main content area for wider server grid
- [ ] **LAYOUT-02**: Activity sidebar collapsible with toggle button (default expanded)
- [ ] **LAYOUT-03**: Server grid uses full available width when sidebar is collapsed
- [ ] **LAYOUT-04**: Responsive grid adapts gracefully from mobile (single column) to ultra-wide (6+ cards per row)

### Servers - NAS Grouping (NAS) — Priority: HIGH
- [ ] **NAS-01**: NAS servers displayed as compact table rows instead of full cards
- [ ] **NAS-02**: NAS servers grouped by directory function (Input / Output / Backup) with visual sub-group headers
- [ ] **NAS-03**: Sub-group headers show server count and aggregate health status
- [ ] **NAS-04**: All 7 NAS servers visible without scrolling on standard viewport (1920x1080)
- [ ] **NAS-05**: NAS table rows expandable to show sparkline and additional details on demand

### Servers - Details Panel (DETAILS) — Priority: HIGH
- [ ] **DETAILS-01**: Clicking any server (protocol card or NAS table row) opens details panel showing all current information
- [ ] **DETAILS-02**: Details panel must show at minimum: server name, protocol, health status, ports, endpoints, and available actions (start/stop/restart/delete)

### History Tab - Cleanup (HIST) — Priority: LOW
- [ ] **HIST-01**: Server filter dropdown excludes deleted/removed dynamic servers (only shows currently deployed servers)
- [ ] **HIST-02**: Chart legend only shows servers that have data points in the selected time range

### Files Tab - Layout (FILES) — Priority: LOW
- [ ] **FILES-01**: File activity sidebar width increased from 350px to 400px for better readability of file paths and event details

### Kafka Tab - Layout (KAFKA) — Priority: LOW
- [ ] **KAFKA-01**: Side panels (Topics, Consumer Groups) use wider width when viewport allows (300px+ instead of fixed 280px)

### Alerts Tab - Layout (ALERTS) — Priority: LOW
- [ ] **ALERTS-01**: Alert table columns use flexible widths instead of hardcoded max-width constraints (250px title, 400px message)

### Testing (TEST) — Priority: MEDIUM
- [ ] **TEST-01**: E2E test verifies protocol-tinted colors are visible on server cards
- [ ] **TEST-02**: E2E test verifies NAS compact view shows all 7 NAS servers without scrolling
- [ ] **TEST-03**: E2E test verifies server details panel opens from both protocol card click and NAS table row click

## Future Requirements

(None deferred)

## Out of Scope

- Drag-to-reorder server cards — would require persisted user preferences
- Kanban-style server grouping by status — current grid with health colors is sufficient
- Mini-map/topology view — complex visualization not needed for v2.3.0
- Dark mode / theme switching — separate milestone
- Server favorites/pinning — would require user preference storage
- Resizable sidebar via drag handle — simple toggle (expand/collapse) is sufficient

## Traceability

| Requirement | Phase | Plan |
|-------------|-------|------|
| VIS-01 | 21 | — |
| VIS-02 | 21 | — |
| VIS-03 | 21 | — |
| VIS-04 | 21 | — |
| LAYOUT-01 | 20 | — |
| LAYOUT-02 | 20 | — |
| LAYOUT-03 | 20 | — |
| LAYOUT-04 | 20 | — |
| NAS-01 | 22 | — |
| NAS-02 | 22 | — |
| NAS-03 | 22 | — |
| NAS-04 | 22 | — |
| NAS-05 | 22 | — |
| DETAILS-01 | 23 | — |
| DETAILS-02 | 23 | — |
| HIST-01 | 24 | — |
| HIST-02 | 24 | — |
| FILES-01 | 24 | — |
| KAFKA-01 | 24 | — |
| ALERTS-01 | 24 | — |
| TEST-01 | 25 | — |
| TEST-02 | 25 | — |
| TEST-03 | 25 | — |

---
*Created: 2026-02-12 for milestone v2.3.0*
