---
created: 2026-02-12T13:45:38.778Z
title: Plan UI refactoring for Servers page layout and visual differentiation
area: ui
files:
  - src/dashboard/src/components/ServerGrid.tsx
  - src/dashboard/src/components/ServerCard.tsx
  - src/dashboard/src/components/ServerDetailsPanel.tsx
  - src/dashboard/src/components/ServerDetailsPanel.css
  - src/dashboard/src/components/ServerSparkline.tsx
  - src/dashboard/src/App.css
---

## Problem

The Servers page currently displays all servers in a uniform grid of identically-styled cards. With 7 NAS servers + 6 protocol servers + dynamic servers, the page feels cramped and it's hard to visually distinguish server types at a glance. User wants:

1. **Wider layout** — servers should use more screen real estate, not feel constrained
2. **Visual differentiation** — different server types should be distinguishable by color or other visual cues (e.g., NAS vs FTP vs SFTP vs S3 vs HTTP vs SMB, static vs dynamic)

## Ideas & Recommendations to Discuss

### Layout improvements
- **Full-width grid** — Remove sidebar constraints, use wider card layout or table view
- **Grouping by type** — Section headers: "NAS Servers", "Protocol Servers", "Dynamic Servers" with collapsible groups
- **List/table view toggle** — Option to switch between card grid and dense table view for power users
- **Responsive card sizing** — Larger cards on wider screens, more info visible per card

### Visual differentiation
- **Protocol color coding** — Assign distinct accent colors per protocol (e.g., NAS=blue, FTP=orange, SFTP=green, S3=purple, HTTP=teal, SMB=amber)
- **Color-coded left border or top stripe** — Subtle but effective visual grouping
- **Status-aware backgrounds** — Healthy=green tint, unhealthy=red tint, stopped=gray tint
- **Icon per protocol** — Distinct icons alongside colors for accessibility
- **Static vs Dynamic badge** — Visual indicator (e.g., pin icon for Helm-deployed, lightning for dynamic)

### Advanced ideas
- **Kanban-style columns** — Group by status (Running / Stopped / Error) instead of flat grid
- **Mini-map/overview** — Compact topology view showing server relationships
- **Drag-to-reorder** — Let users arrange cards to their preference
- **Favorites/pinning** — Pin frequently-used servers to top

## Solution

TBD — needs discussion to decide which combination of layout + color + grouping approach to pursue before implementation planning.
