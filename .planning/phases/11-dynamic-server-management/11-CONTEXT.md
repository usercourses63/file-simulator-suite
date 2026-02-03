# Phase 11: Dynamic Server Management - Context

**Gathered:** 2026-02-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Enable runtime creation and deletion of FTP, SFTP, and NAS servers through the dashboard UI. Support configuration export/import for environment replication. All dynamically created resources must have ownerReferences and update the ConfigMap for service discovery.

</domain>

<decisions>
## Implementation Decisions

### Server Creation Flow
- Full control over configuration options (all settings exposed, not just basics)
- NodePorts: auto-assign by default, user can override in advanced settings
- NAS directory selection: presets (input/output/backup) as quick options, with custom path available
- Progress shown in modal with deployment steps until complete (modal stays open)
- Error handling: show error in modal with retry/cancel options
- Batch creation mode available for power users
- Newly created servers appear in same grid with filter/toggle for dynamic vs static

### Deletion Behavior
- Simple confirm dialog ("Delete server X?" with Cancel/Delete)
- For NAS servers: ask each time whether to keep or delete files from Windows directory
- Static (Helm-deployed) servers cannot be deleted via UI — show "Managed by Helm" badge, no delete option
- Immediate deletion after confirmation (no grace period or undo)
- Multi-select delete supported (checkboxes to select multiple servers)

### Config Export/Import
- Export includes ALL servers (static and dynamic) — full state snapshot
- Credentials included in export (user responsible for securing the file)
- Import conflict handling: ask user per conflict (skip, replace, or rename)
- NodePort preservation: try to use exported port, auto-assign if conflict

### Server List UI
- Add button in header area, Delete button on each card, Export/Import in settings area
- Dynamic vs static servers: filter/toggle in grid, visual distinction chosen by Claude
- Click opens details panel with inline editable fields
- All settings editable after creation (including ports — system handles update)

### Claude's Discretion
- Creation flow initiation pattern (single "Add Server" button or per-type)
- Deletion progress feedback (toast vs status badge)
- Visual distinction style for dynamic servers (badge, border, icon)
- Batch creation UI design
- Multi-select UX implementation

</decisions>

<specifics>
## Specific Ideas

- Batch creation should feel like a power-user feature, not cluttering the simple add flow
- Export/import is primarily for replicating test environments across machines
- Inline edit should feel responsive — changes reflected immediately in the card

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 11-dynamic-server-management*
*Context gathered: 2026-02-03*
