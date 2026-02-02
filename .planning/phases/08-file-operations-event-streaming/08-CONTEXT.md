# Phase 8 Context: File Operations and Event Streaming

*Captured from /gsd:discuss-phase 8 on 2026-02-02*

## Decisions

### File Browser UI
- **Navigation style**: Claude's discretion
- **File information displayed**: Minimal (name, size, modified date)
- **Browsing scope**: Sandboxed to C:\simulator-data only
- **Empty folder handling**: Claude's discretion

### Upload/Download Experience
- **Drag-and-drop**: Yes, supported
- **Multi-file handling**: Claude's discretion
- **Target servers**: Claude's discretion

### File Event Feed
- **Display style**: Live scrolling feed, newest at top
- **Event types**: All (Created, Modified, Deleted, Renamed)
- **History depth**: Last 50 events

### Multi-Protocol Visibility
- **Visibility UI**: Protocol badges on files
- **Detection method**: Claude's discretion

## Claude's Discretion Areas

Based on user selections, Claude has freedom to decide:

1. **File browser navigation style** - Tree view, breadcrumb, hybrid, etc.
2. **Empty folder display** - Show/hide/indicate
3. **Multi-file upload handling** - Sequential vs parallel, progress indicators
4. **Upload target** - Windows directory (auto-sync) vs specific protocol
5. **Protocol accessibility detection** - Directory mapping vs real-time probe

## Deferred Ideas

None specified.

## Phase Goal Reference

From ROADMAP.md success criteria:
1. User can browse Windows C:\simulator-data directory hierarchy through dashboard UI
2. User can upload files via browser to any protocol server and file appears in Windows directory
3. User can download files from any protocol server through browser
4. User can delete files across all protocols from dashboard with confirmation dialog
5. File event feed shows real-time arrivals/departures when files created/modified/deleted in Windows
6. Multi-protocol tracking shows which servers can see each file (e.g., "Visible via: FTP, SFTP, NAS-input-1")
