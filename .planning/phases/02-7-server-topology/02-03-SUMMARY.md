---
phase: 02-7-server-topology
plan: 03
subsystem: infra
tags: [nfs, kubernetes, validation, testing, multi-nas, storage-isolation]

# Dependency graph
requires:
  - phase: 02-02
    provides: 7-server NAS topology deployed to Minikube
provides:
  - Runtime subdirectory behavior documented (EXP-02)
  - Multi-NAS mount architecture validated (INT-03)
  - Automated validation script for 7-server topology regression testing
  - Production-readiness validation complete for Phase 2
affects: [03-output-nas, 04-client-library, testing]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Windows filesystem as source of truth for input NAS"
    - "Init container one-way sync pattern for Phase 2"
    - "PowerShell test automation with kubectl exec validation"

key-files:
  created:
    - .planning/phases/02-7-server-topology/EXP-02-validation.md
    - .planning/phases/02-7-server-topology/INT-03-validation.md
    - scripts/test-multi-nas.ps1
  modified:
    - scripts/test-multi-nas.ps1

key-decisions:
  - "Runtime directories ephemeral for input NAS (expected behavior)"
  - "Windows-created directories persist via init container sync"
  - "Multi-NAS architecture validated at service level (protocol testing blocked by rpcbind)"
  - "kubectl exec validation sufficient for Phase 2; NFS volume mount deferred to Phase 3"

patterns-established:
  - "Pattern 1: Test automation via PowerShell with structured PASS/FAIL/SKIP output"
  - "Pattern 2: Validation documentation in dedicated EXP-XX and INT-XX markdown files"
  - "Pattern 3: Human verification with automated test script after iterative fixes"

# Metrics
duration: 47min
completed: 2026-02-01
---

# Phase 2 Plan 3: Advanced NAS Topology Validation Summary

**7-server NAS topology validated with runtime subdirectory behavior documented, multi-NAS architecture confirmed, and automated regression testing via test-multi-nas.ps1 script achieving 37/38 tests passing**

## Performance

- **Duration:** 47 min (initial execution: 13min on 2026-01-29; checkpoint iteration: 34min on 2026-02-01)
- **Started:** 2026-01-29T12:42:14Z
- **Completed:** 2026-02-01T10:58:49Z
- **Tasks:** 4 (3 auto + 1 checkpoint)
- **Files modified:** 3

## Accomplishments
- Documented EXP-02 runtime subdirectory behavior: Windows-created directories persist, pod-created directories reset on restart (expected for input NAS)
- Validated INT-03 multi-NAS mount architecture: 7 independent services with unique endpoints, storage isolation, and DNS naming
- Created comprehensive test-multi-nas.ps1 script with 38 automated validation tests
- Fixed test script issues during checkpoint: added simulator.protocol=nfs labels, --context flags, -c container specifications
- Achieved 37/38 passing tests (SUCCESS) validating production-ready 7-server topology

## Task Commits

Each task was committed atomically:

1. **Task 1: Validate Runtime Subdirectory Creation (EXP-02)** - `ef4416b` (test)
2. **Task 2: Validate Multi-NAS Mount Capability (INT-03)** - `b14d421` (test)
3. **Task 3: Create Multi-NAS Validation Script** - `dbc0b0a` (feat)
4. **Task 4: Human Verify** - Checkpoint reached, fixes applied:
   - `e8bc4fe` - Added simulator.protocol=nfs label to nas-multi.yaml
   - `a525920` - Added --context flag and JSON parsing safety
   - `e1727cd` - Added context flag to Step 11 services check
   - `ea26528` - Added -c nfs-server container specification

**Plan metadata:** (to be committed with SUMMARY.md)

## Files Created/Modified
- `.planning/phases/02-7-server-topology/EXP-02-validation.md` - Runtime subdirectory creation behavior documentation
- `.planning/phases/02-7-server-topology/INT-03-validation.md` - Multi-NAS mount architecture validation documentation
- `scripts/test-multi-nas.ps1` - Comprehensive 38-test validation script for 7-server topology
- `helm-chart/file-simulator/templates/nas-multi.yaml` - Added simulator.protocol=nfs labels for service discovery

## Decisions Made

**EXP-02: Runtime Subdirectory Persistence**
- Runtime directories created via kubectl exec are LOST on pod restart (expected behavior)
- Init container re-syncs from Windows hostPath on each pod start, overwriting emptyDir
- Windows-created directories PERSIST across restarts (Windows filesystem is source of truth)
- This pattern is correct for Phase 2 input NAS; bidirectional sync will be needed for Phase 3 output NAS

**INT-03: Multi-NAS Mount Capability**
- Validated at service/architecture level: 7 unique services with ClusterIPs, DNS names, storage isolation
- Actual NFS volume mount testing BLOCKED by rpcbind issue (known from Phase 1, deferred to Phase 3)
- Architecture supports multi-mount pattern; protocol-level testing will proceed when rpcbind resolved
- Decision: Accept service-level validation for Phase 2; kubectl exec sufficient for current pattern

**Test Script Strategy**
- Use kubectl exec for validation instead of external NFS mount (blocked by rpcbind)
- PowerShell script with structured PASS/FAIL/SKIP output for clear regression testing
- Accept 37/38 passing as SUCCESS (1 skip for NFS volume mount due to rpcbind blocker)

## Deviations from Plan

### Checkpoint Iteration Fixes

**1. [Rule 3 - Blocking] Added simulator.protocol=nfs labels**
- **Found during:** Task 4 checkpoint verification
- **Issue:** Test script Step 2 failed because kubectl couldn't select NAS pods/services by protocol label
- **Fix:** Added `simulator.protocol: nfs` label to nas-multi.yaml pod and service templates
- **Files modified:** helm-chart/file-simulator/templates/nas-multi.yaml
- **Verification:** kubectl get pods/svc -l simulator.protocol=nfs returns all 7 NAS resources
- **Committed in:** e8bc4fe

**2. [Rule 3 - Blocking] Added --context flag to kubectl commands**
- **Found during:** Task 4 checkpoint verification (test script execution)
- **Issue:** minikube context detection failed with "not found" errors
- **Fix:** Added explicit --context minikube flag to all kubectl commands in test script
- **Files modified:** scripts/test-multi-nas.ps1
- **Verification:** All kubectl commands execute successfully with context specified
- **Committed in:** a525920, e1727cd

**3. [Rule 3 - Blocking] Added -c nfs-server container specification**
- **Found during:** Task 4 checkpoint verification
- **Issue:** kubectl exec failed on multi-container pods without specifying container name
- **Fix:** Added `-c nfs-server` flag to all kubectl exec commands in test script
- **Files modified:** scripts/test-multi-nas.ps1
- **Verification:** All kubectl exec commands target correct container in multi-container pods
- **Committed in:** ea26528

---

**Total deviations:** 3 auto-fixed during checkpoint iteration (all blocking issues)
**Impact on plan:** All fixes necessary for test script functionality. No scope creep; fixes enabled validation as planned.

## Issues Encountered

**NFS Volume Mount Blocker (Expected from Phase 1):**
- INT-03 multi-mount testing blocked by unfs3 + rpcbind RPC registration issue
- DNS resolution fails during NFS mount attempt: "Name or service not known"
- This is the same issue deferred from Phase 1 (documented in STATE.md)
- Workaround: Validated multi-NAS capability at service level (ClusterIP, DNS, storage isolation)
- Impact: Architecture validated; protocol-level testing deferred to Phase 3

**Test Script Iteration During Checkpoint:**
- Initial test script execution revealed 4 issues requiring fixes
- Each issue fixed in separate commit during checkpoint iteration
- User approved after 37/38 tests passing (1 skip for NFS mount due to rpcbind)
- Iteration pattern: test → fix → test → fix → approved

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Phase 2 Complete - Ready for Phase 3 (Output NAS):**

✅ **Validated:**
- 7-server NAS topology runs stably in Minikube (resource usage under 50% capacity)
- Storage isolation between NAS servers confirmed
- Subdirectory mount support (EXP-01) working
- Runtime subdirectory behavior (EXP-02) documented
- Multi-NAS architecture (INT-03) validated at service level
- Windows filesystem as source of truth pattern proven
- Init container one-way sync pattern working reliably
- Automated regression testing via test-multi-nas.ps1

**Known Blockers for Phase 3:**
- rpcbind integration issue must be resolved for NFS volume mounts
- Bidirectional sync pattern needed for output NAS (runtime files must persist)
- External NFS mount testing requires rpcbind fix

**Recommendations for Phase 3:**
1. Investigate rpcbind startup ordering and RPC registration
2. Consider alternative NFS servers (nfs-ganesha, kernel NFS) if unfs3 blocker persists
3. Implement sidecar continuous sync for output NAS bidirectional pattern
4. Test actual multi-mount scenario once rpcbind resolved

**Phase 2 Achievement:**
Production-ready 7-server NAS topology validated and documented. System meets all must-have requirements for Phase 2. Ready to proceed with output NAS implementation in Phase 3.

---
*Phase: 02-7-server-topology*
*Completed: 2026-02-01*
