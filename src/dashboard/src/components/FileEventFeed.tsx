import type { FileEvent } from '../types/fileTypes';
import ProtocolBadges from './ProtocolBadges';

interface FileEventFeedProps {
  events: FileEvent[];
  isConnected: boolean;
  onClear?: () => void;
}

// Event type icons
const eventIcons: Record<string, string> = {
  Created: '+',
  Modified: '~',
  Deleted: '-',
  Renamed: '->',
};

// Event type colors (CSS class suffixes)
const eventColors: Record<string, string> = {
  Created: 'created',
  Modified: 'modified',
  Deleted: 'deleted',
  Renamed: 'renamed',
};

// Format time for display
function formatTime(isoDate: string): string {
  try {
    const date = new Date(isoDate);
    return date.toLocaleTimeString();
  } catch {
    return isoDate;
  }
}

// Extract short path for display
function formatPath(event: FileEvent): string {
  const path = event.relativePath || event.fileName;
  if (event.eventType === 'Renamed' && event.oldPath) {
    const oldName = event.oldPath.split(/[/\\]/).pop() || event.oldPath;
    return `${oldName} -> ${event.fileName}`;
  }
  return path;
}

/**
 * Live scrolling feed of file system events.
 * Shows newest events at top, max 50 items.
 */
export function FileEventFeed({ events, isConnected, onClear }: FileEventFeedProps) {
  return (
    <div className="file-event-feed">
      <div className="file-event-feed__header">
        <h3 className="file-event-feed__title">
          File Activity
          <span className={`file-event-feed__status ${isConnected ? 'file-event-feed__status--connected' : ''}`}>
            {isConnected ? 'Live' : 'Disconnected'}
          </span>
        </h3>
        {events.length > 0 && onClear && (
          <button
            className="file-event-feed__clear"
            onClick={onClear}
            type="button"
          >
            Clear
          </button>
        )}
      </div>

      <div className="file-event-feed__list">
        {events.length === 0 ? (
          <div className="file-event-feed__empty">
            {isConnected
              ? 'Waiting for file activity...'
              : 'Connecting to file events...'}
          </div>
        ) : (
          events.map((event, index) => (
            <div
              key={`${event.path}-${event.timestamp}-${index}`}
              className={`file-event-feed__item file-event-feed__item--${eventColors[event.eventType] || 'default'}`}
            >
              <span className="file-event-feed__icon" title={event.eventType}>
                {eventIcons[event.eventType] || '?'}
              </span>

              <span className="file-event-feed__type">
                {event.eventType}
              </span>

              <span className="file-event-feed__path" title={event.path}>
                {event.isDirectory ? '\u{1F4C1} ' : ''}
                {formatPath(event)}
              </span>

              <span className="file-event-feed__time">
                {formatTime(event.timestamp)}
              </span>

              <ProtocolBadges protocols={event.protocols} size="small" />
            </div>
          ))
        )}
      </div>

      <div className="file-event-feed__footer">
        Showing {events.length} of 50 max events
      </div>
    </div>
  );
}

export default FileEventFeed;
