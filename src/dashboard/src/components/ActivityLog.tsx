import type { ActivityEvent, ActivityEventType } from '../types/activity';
import './ActivityLog.css';

interface ActivityLogProps {
  events: ActivityEvent[];
  onClear: () => void;
}

const iconMap: Record<ActivityEventType, string> = {
  'server-created': '+',
  'server-deleted': '-',
  'server-healthy': '\u2713',
  'server-down': '!',
  'file-created': '+',
  'file-deleted': '-',
  'file-modified': '~',
  'file-read': '\u2192',
  'file-renamed': '\u2194',
  'alert-triggered': '\u26A0',
  'alert-resolved': '\u2713',
  'mount-state-change': '\u25CF',
};

function getIconCategory(type: ActivityEventType): string {
  if (type.startsWith('server-')) return 'server';
  if (type.startsWith('file-')) return 'file';
  if (type.startsWith('alert-')) return 'alert';
  return 'mount';
}

function formatTime(isoDate: string): string {
  try {
    return new Date(isoDate).toLocaleTimeString();
  } catch {
    return isoDate;
  }
}

export function ActivityLog({ events, onClear }: ActivityLogProps) {
  return (
    <div className="activity-log">
      <div className="activity-log__header">
        <h3 className="activity-log__title">Activity Log</h3>
        {events.length > 0 && (
          <button
            className="activity-log__clear"
            onClick={onClear}
            type="button"
          >
            Clear
          </button>
        )}
      </div>

      <div className="activity-log__list">
        {events.length === 0 ? (
          <div className="activity-log__empty">
            Waiting for activity...
          </div>
        ) : (
          events.map(event => (
            <div
              key={event.id}
              className={`activity-log__item activity-log__item--${event.severity}`}
            >
              <span className={`activity-log__icon activity-log__icon--${getIconCategory(event.type)}`}>
                {iconMap[event.type] || '?'}
              </span>
              <div className="activity-log__body">
                <div className="activity-log__event-title" title={event.title}>
                  {event.title}
                </div>
                {event.detail && (
                  <div className="activity-log__detail" title={event.detail}>
                    {event.detail}
                  </div>
                )}
              </div>
              <span className="activity-log__time">
                {formatTime(event.timestamp)}
              </span>
            </div>
          ))
        )}
      </div>

      <div className="activity-log__footer">
        {events.length} event{events.length !== 1 ? 's' : ''}
      </div>
    </div>
  );
}

export default ActivityLog;
