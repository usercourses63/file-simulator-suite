---
phase: 11-dynamic-server-management
plan: 07
subsystem: dashboard
tags: [react, multi-select, batch-operations, delete]
dependency-graph:
  requires: [11-04]
  provides: [multi-select-servers, batch-delete, delete-confirm]
  affects: [11-09]
tech-stack:
  added: []
  patterns: [useState-set, batch-operations-bar, confirmation-dialog]
key-files:
  created:
    - src/dashboard/src/hooks/useMultiSelect.ts
    - src/dashboard/src/components/DeleteConfirmDialog.tsx
    - src/dashboard/src/components/DeleteConfirmDialog.css
    - src/dashboard/src/components/BatchOperationsBar.tsx
    - src/dashboard/src/components/BatchOperationsBar.css
  modified:
    - src/dashboard/src/components/ServerCard.tsx
    - src/dashboard/src/components/ServerGrid.tsx
    - src/dashboard/src/App.tsx
    - src/dashboard/src/App.css
decisions:
  - id: 11-07-01
    title: "Set<string> for multi-select state"
    rationale: "O(1) lookup for isSelected, natural add/delete operations"
  - id: 11-07-02
    title: "canSelect predicate defaults to isDynamic check"
    rationale: "Only dynamic servers can be deleted, Helm-managed are protected"
  - id: 11-07-03
    title: "Delete confirmation dialog asks about NAS files"
    rationale: "NAS servers may have user data that should be optionally cleaned"
  - id: 11-07-04
    title: "Dynamic servers identified by name prefix"
    rationale: "Helm-deployed servers use file-sim- prefix, dynamic ones don't"
metrics:
  duration: 7 min
  completed: 2026-02-03
---

# Phase 11 Plan 07: Multi-Select, Batch Operations, and Delete Summary

Multi-select with Set<string> state, batch delete bar, delete confirmation dialog with NAS file option, dynamic vs Helm badge distinction.

## What Was Built

### Task 1: Multi-Select Hook and Delete Dialog
Created reusable multi-select infrastructure:

1. **useMultiSelect.ts** (71 lines)
   - Generic hook with `<T extends { name: string; isDynamic?: boolean }>` constraint
   - `selectedIds: Set<string>` for O(1) selection checks
   - `canSelect` predicate defaults to `isDynamic === true`
   - Methods: `toggleSelect`, `selectAll`, `clearSelection`, `setSelected`, `isSelected`

2. **DeleteConfirmDialog.tsx** (95 lines)
   - Modal dialog for single and batch delete confirmation
   - Shows server name(s) in title and tag list
   - Checkbox for NAS servers: "Also delete files from Windows directory"
   - isDeleting state disables buttons during operation

3. **BatchOperationsBar.tsx** (56 lines)
   - Sticky bar at top when items selected
   - Shows "X server(s) selected"
   - Buttons: Select All, Clear, Delete (count)
   - Disappears when selectedCount === 0

### Task 2: ServerCard and ServerGrid Multi-Select Integration
Updated existing components:

1. **ServerCard.tsx**
   - Added props: `showCheckbox`, `isSelected`, `onToggleSelect`, `onDelete`, `isDynamic`, `managedBy`
   - Checkbox appears only for dynamic servers (top-left)
   - Delete button appears on hover (top-right, only for dynamic)
   - Badge system: "Dynamic" (blue) or "Helm" (gray)
   - Event handlers stop propagation to prevent card click

2. **ServerGrid.tsx**
   - Added multi-select props: `showMultiSelect`, `selectedIds`, `onToggleSelect`, `onDelete`
   - Added `dynamicInfo` prop for server dynamic/static info
   - Passes all props to ServerCard for each server

3. **App.tsx**
   - Integrated `useMultiSelect` hook with server list
   - Added `useServerManagement` for delete operations
   - `dynamicInfo` computed from server names (file-sim- prefix = Helm)
   - Delete handlers for single and batch operations
   - NAS detection via protocol or name pattern

4. **App.css**
   - `.server-card--selected` outline
   - `.server-card-checkbox` absolute positioning
   - `.server-card-delete` hidden by default, shown on hover
   - Badge styles: `.badge--dynamic`, `.badge--helm`

## Decisions Made

| ID | Decision | Rationale |
|----|----------|-----------|
| 11-07-01 | Set<string> for selection state | O(1) lookup performance, clean API |
| 11-07-02 | Default canSelect = isDynamic | Protect Helm-managed servers from deletion |
| 11-07-03 | Per-delete NAS file prompt | User controls whether data cleanup happens |
| 11-07-04 | Name prefix identifies source | Simple heuristic: file-sim- = Helm, else dynamic |

## Files Changed

| File | Change Type | Lines | Purpose |
|------|-------------|-------|---------|
| useMultiSelect.ts | created | 71 | Multi-select state management |
| DeleteConfirmDialog.tsx | created | 95 | Deletion confirmation modal |
| DeleteConfirmDialog.css | created | 61 | Dialog styles |
| BatchOperationsBar.tsx | created | 56 | Batch operations UI |
| BatchOperationsBar.css | created | 56 | Bar styles |
| ServerCard.tsx | modified | +80 | Checkbox, delete, badges |
| ServerGrid.tsx | modified | +30 | Multi-select prop forwarding |
| App.tsx | modified | +70 | State integration |
| App.css | modified | +70 | Multi-select styles |

## Verification Results

1. `npm run build` - SUCCESS
2. Dynamic servers show checkbox and delete button - VERIFIED
3. Static servers show "Helm" badge, no delete option - VERIFIED
4. Multi-select enables batch delete - VERIFIED
5. Delete dialog asks about files for NAS servers - VERIFIED

## Deviations from Plan

None - plan executed exactly as written.

## Next Phase Readiness

Plan 11-09 can proceed with:
- Multi-select infrastructure in place
- Delete confirmation pattern established
- Batch operations bar pattern reusable
- Dynamic vs static distinction working
