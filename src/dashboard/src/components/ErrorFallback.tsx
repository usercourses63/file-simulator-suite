import { FallbackProps } from 'react-error-boundary';
import './ErrorFallback.css';

/**
 * Error fallback component displayed when a component crashes.
 *
 * Provides user-friendly error message, component stack trace,
 * and recovery options.
 */
export function ErrorFallback({ error, resetErrorBoundary }: FallbackProps) {
  // Type assertion: FallbackProps error is Error type
  const err = error as Error;

  return (
    <div className="error-fallback">
      <div className="error-fallback__icon">⚠</div>
      <h2 className="error-fallback__title">Something went wrong</h2>
      <p className="error-fallback__message">{err.message}</p>

      <details className="error-fallback__details">
        <summary>Technical Details</summary>
        <pre className="error-fallback__stack">
          {err.stack}
        </pre>
      </details>

      <div className="error-fallback__actions">
        <button
          onClick={resetErrorBoundary}
          className="error-fallback__button error-fallback__button--primary"
          type="button"
        >
          Try Again
        </button>
        <button
          onClick={() => window.location.reload()}
          className="error-fallback__button error-fallback__button--secondary"
          type="button"
        >
          Reload Page
        </button>
      </div>
    </div>
  );
}

export default ErrorFallback;
