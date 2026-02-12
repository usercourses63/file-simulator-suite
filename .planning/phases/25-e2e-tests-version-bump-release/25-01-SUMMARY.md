---
phase: 25-e2e-tests-version-bump-release
plan: 01
subsystem: testing
tags: [playwright, e2e, page-object-model, css-selectors, nas-table, protocol-tinting]

# Dependency graph
requires:
  - phase: 22-nas-compact-table-view
    provides: NasTable component with .nas-table__row CSS classes
  - phase: 21-protocol-color-system
    provides: server-card--protocol-{type} CSS classes on ServerCard
  - phase: 23-server-details-panel-adaptation
    provides: Details panel opens from both cards and NAS rows
provides:
  - 4 new E2E tests validating v2.3.0 UI features (TEST-01, TEST-02, TEST-03)
  - Updated ServersPage page object with NAS table locators
  - Fixed GetNasServerCountAsync for table-based NAS view
affects: [25-02-version-bump-release]

# Tech tracking
tech-stack:
  added: []
  patterns: [NAS table locator pattern using .nas-table__row instead of .server-card]

key-files:
  created: []
  modified:
    - tests/FileSimulator.E2ETests/PageObjects/ServersPage.cs
    - tests/FileSimulator.E2ETests/Tests/ServerManagementTests.cs

key-decisions:
  - "GetNasServerCountAsync uses .nas-table__row count (not .server-card) since NAS servers render as table rows"
  - "GetAllServerNamesAsync returns only protocol card names; separate GetNasServerNamesAsync for NAS names"
  - "Protocol tint test uses regex matching for server-card--protocol-{type} CSS classes"

patterns-established:
  - "NAS table locators: use .nas-table__row for rows, .nas-table__cell--name for names, .nas-table__group-header for groups"
  - "SelectNasServerAsync uses Nth(index).ClickAsync() then waits for .details-panel--open"

# Metrics
duration: 2min
completed: 2026-02-12
---

# Phase 25 Plan 01: E2E Tests for v2.3.0 UI Features Summary

**4 Playwright E2E tests validating protocol-tinted cards, NAS compact table view, and details panel access from both cards and NAS rows**

## Performance

- **Duration:** 2 min
- **Started:** 2026-02-12T16:30:08Z
- **Completed:** 2026-02-12T16:32:33Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Updated ServersPage page object with NAS table locators (NasTable, NasTableRows, NasTableGroupHeaders, NasTableRowDetail)
- Added 5 new helper methods for NAS table and protocol card inspection
- Added 4 new E2E tests covering TEST-01 (protocol tinting), TEST-02 (NAS compact view), TEST-03 (details panel from cards and NAS rows)
- Fixed existing test assertion that assumed NAS servers render as cards

## Task Commits

Each task was committed atomically:

1. **Task 1: Update ServersPage page object with NAS table and protocol tint locators** - `6bd02e1` (feat)
2. **Task 2: Add E2E tests for protocol tinting, NAS compact view, and details panel** - `e7ca24b` (feat)

## Files Created/Modified
- `tests/FileSimulator.E2ETests/PageObjects/ServersPage.cs` - Added NAS table locators, fixed GetNasServerCountAsync, added GetNasServerNamesAsync/SelectNasServerAsync/GetProtocolCardClassesAsync/GetAllProtocolCardProtocolClassesAsync
- `tests/FileSimulator.E2ETests/Tests/ServerManagementTests.cs` - Added 4 new tests (Servers_ProtocolCards_HaveColorTinting, Servers_NasCompactView_ShowsAllNasServers, Servers_DetailsPanel_OpensFromProtocolCard, Servers_DetailsPanel_OpensFromNasTableRow)

## Decisions Made
- GetNasServerCountAsync uses `.nas-table__row` count instead of `.server-card` since NAS servers now render as compact table rows
- GetAllServerNamesAsync returns only protocol card names; separate GetNasServerNamesAsync method handles NAS names
- Protocol tint test uses regex matching for `server-card--protocol-(ftp|sftp|http|s3|smb|nfs|nas)` to validate any protocol type

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed Servers_DisplaysAllConfiguredServers NAS assertion**
- **Found during:** Task 2 (Adding new tests)
- **Issue:** Existing test asserted `serverNames.Should().Contain(name => name.Contains("nas"))` but NAS servers no longer render as `.server-card` elements (they use `.nas-table__row`), so GetAllServerNamesAsync would not include NAS names
- **Fix:** Changed assertion to use `GetNasServerCountAsync()` which correctly counts `.nas-table__row` elements
- **Files modified:** tests/FileSimulator.E2ETests/Tests/ServerManagementTests.cs
- **Verification:** Build succeeds, assertion logic matches actual DOM structure
- **Committed in:** e7ca24b (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug fix)
**Impact on plan:** Essential fix to prevent existing test from failing with new NAS table UI. No scope creep.

## Issues Encountered
None

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- All 4 E2E tests compile successfully
- Ready for 25-02 version bump and release
- Tests can be run against deployed cluster with `USE_EXISTING_SIMULATOR=true dotnet test --filter "FullyQualifiedName~ServerManagementTests"`

## Self-Check: PASSED

All files verified present, all commit hashes found in git log.

---
*Phase: 25-e2e-tests-version-bump-release*
*Completed: 2026-02-12*
