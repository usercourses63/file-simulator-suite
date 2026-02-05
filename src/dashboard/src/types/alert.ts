// Alert severity levels
export type AlertSeverity = "Info" | "Warning" | "Critical";

// Alert entity matching backend model
export interface Alert {
  id: string;
  type: string;
  severity: AlertSeverity;
  title: string;
  message: string;
  source: string;
  triggeredAt: string;
  resolvedAt: string | null;
  isResolved: boolean;
}

// Alert statistics for dashboard
export interface AlertStats {
  totalCount: number;
  infoCount: number;
  warningCount: number;
  criticalCount: number;
  byType: Record<string, number>;
}
