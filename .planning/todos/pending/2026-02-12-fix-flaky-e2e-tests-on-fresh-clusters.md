---
created: 2026-02-12T13:39:35.259Z
title: Fix flaky E2E tests on fresh clusters
area: testing
files:
  - tests/FileSimulator.E2ETests/Tests/DashboardTests.cs
---

## Problem

Two pre-existing E2E tests fail consistently on fresh Minikube clusters (discovered during v2.2.0 fresh install validation):

1. **`History_LoadsDataForRange`** — Times out waiting for chart data. Fresh clusters have no historical metrics data, so the History tab has nothing to render. The test expects data to be present.

2. **`Servers_CanViewServerDetails`** — Times out waiting for `.details-panel--open`. Likely a UI timing issue where the click doesn't register or the panel animation doesn't complete within the timeout window.

These are not v2.2.0 regressions — they existed before. They passed on the long-running dev cluster because it had accumulated historical data and warmer caches.

## Solution

1. **History test**: Either seed some metrics data before the test (via API calls to generate health samples), or adjust the assertion to accept an empty state gracefully (verify the chart container exists but allow zero data points).

2. **Server details test**: Increase timeout or add explicit wait-for-visible before asserting. May need to ensure the click target is fully rendered and not obscured by other elements.
