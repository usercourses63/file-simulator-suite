import { Toaster, toast } from 'sonner';
import { Alert } from '../types/alert';

/**
 * Toast notification component for alerts.
 * Wraps Sonner's Toaster with custom configuration.
 */
export function AlertToaster() {
  return (
    <Toaster
      position="bottom-right"
      richColors
      closeButton
      visibleToasts={5}
    />
  );
}

/**
 * Show a toast notification for an alert.
 * Duration and style are based on alert severity.
 *
 * @param alert - Alert to display as toast
 *
 * @example
 * showAlert({
 *   id: '123',
 *   type: 'ServerDown',
 *   severity: 'Critical',
 *   title: 'Server Offline',
 *   message: 'FTP server is not responding',
 *   source: 'file-sim-ftp-1',
 *   triggeredAt: '2026-02-05T10:00:00Z',
 *   resolvedAt: null,
 *   isResolved: false
 * });
 */
export function showAlert(alert: Alert): void {
  // Determine duration based on severity
  // Info: 5 seconds
  // Warning: 10 seconds
  // Critical: Infinity (must be manually dismissed)
  const duration = alert.severity === 'Info'
    ? 5000
    : alert.severity === 'Warning'
    ? 10000
    : Infinity;

  // Format the alert message
  const message = `${alert.title}: ${alert.message}`;
  const description = `Source: ${alert.source}`;

  // Show toast based on severity
  switch (alert.severity) {
    case 'Info':
      toast.info(message, {
        description,
        duration
      });
      break;

    case 'Warning':
      toast.warning(message, {
        description,
        duration
      });
      break;

    case 'Critical':
      toast.error(message, {
        description,
        duration,
        action: {
          label: 'Dismiss',
          onClick: () => {
            // Toast auto-closes when action is clicked
          }
        }
      });
      break;

    default:
      // Fallback to info
      toast(message, {
        description,
        duration: 5000
      });
  }
}

export default AlertToaster;
