export type ActivityEventType =
  | 'server-created'
  | 'server-deleted'
  | 'server-healthy'
  | 'server-down'
  | 'file-created'
  | 'file-deleted'
  | 'file-modified'
  | 'file-read'
  | 'alert-triggered'
  | 'alert-resolved'
  | 'mount-state-change';

export type ActivitySeverity = 'info' | 'success' | 'warning' | 'critical';

export interface ActivityEvent {
  id: string;
  type: ActivityEventType;
  timestamp: string;
  title: string;
  detail?: string;
  severity: ActivitySeverity;
}
