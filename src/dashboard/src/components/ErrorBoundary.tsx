import { ComponentType } from 'react';
import { ErrorBoundary as ReactErrorBoundary } from 'react-error-boundary';
import { ErrorFallback } from './ErrorFallback';

/**
 * Higher-order component to wrap a component with an error boundary.
 *
 * @param Component - Component to wrap
 * @param displayName - Optional display name for debugging
 * @returns Wrapped component with error boundary
 *
 * @example
 * const SafeServersTab = withErrorBoundary(ServersTab);
 */
export function withErrorBoundary<P extends object>(
  Component: ComponentType<P>,
  displayName?: string
): ComponentType<P> {
  const WrappedComponent = (props: P) => (
    <ReactErrorBoundary FallbackComponent={ErrorFallback}>
      <Component {...props} />
    </ReactErrorBoundary>
  );

  WrappedComponent.displayName = displayName || `withErrorBoundary(${Component.displayName || Component.name})`;

  return WrappedComponent;
}

/**
 * Error boundary wrapper component for wrapping sections of JSX.
 *
 * @example
 * <ErrorBoundaryWrapper>
 *   <SomeComponent />
 * </ErrorBoundaryWrapper>
 */
export function ErrorBoundaryWrapper({ children }: { children: React.ReactNode }) {
  return (
    <ReactErrorBoundary FallbackComponent={ErrorFallback}>
      {children}
    </ReactErrorBoundary>
  );
}

export default withErrorBoundary;
