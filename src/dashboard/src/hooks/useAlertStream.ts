import { useEffect, useState, useRef } from 'react';
import * as signalR from '@microsoft/signalr';
import { Alert } from '../types/alert';

/**
 * Callback handlers for alert stream events.
 */
export interface AlertStreamHandlers {
  /** Called when a new alert is triggered */
  onAlertTriggered?: (alert: Alert) => void;
  /** Called when an alert is resolved */
  onAlertResolved?: (alert: Alert) => void;
}

/**
 * Result returned by useAlertStream hook.
 */
export interface UseAlertStreamResult {
  /** Whether WebSocket is connected */
  isConnected: boolean;
  /** Whether currently reconnecting */
  isReconnecting: boolean;
  /** Current reconnection attempt number (resets on success) */
  reconnectAttempt: number;
  /** Error message if connection failed */
  error: string | null;
}

/**
 * Custom hook for managing SignalR alert stream connections.
 *
 * @param hubUrl - URL of the alerts SignalR hub (e.g., "http://localhost:30500/hubs/alerts")
 * @param handlers - Callback handlers for alert events
 * @returns Connection state
 *
 * @example
 * const { isConnected, isReconnecting, error } = useAlertStream(
 *   'http://localhost:30500/hubs/alerts',
 *   {
 *     onAlertTriggered: (alert) => console.log('New alert:', alert),
 *     onAlertResolved: (alert) => console.log('Resolved:', alert)
 *   }
 * );
 */
export function useAlertStream(
  hubUrl: string,
  handlers: AlertStreamHandlers
): UseAlertStreamResult {
  const [isConnected, setIsConnected] = useState(false);
  const [isReconnecting, setIsReconnecting] = useState(false);
  const [reconnectAttempt, setReconnectAttempt] = useState(0);
  const [error, setError] = useState<string | null>(null);

  // Use ref to store connection to prevent recreating on re-renders
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Track if component is mounted to prevent state updates after unmount
  const isMountedRef = useRef(true);

  useEffect(() => {
    isMountedRef.current = true;

    // Build connection with automatic reconnection
    // Custom retry delays: 0, 2000, 5000, 10000, 30000 ms
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connectionRef.current = connection;

    // Connection lifecycle handlers
    connection.onreconnecting((err) => {
      if (!isMountedRef.current) return;

      setIsReconnecting(true);
      setIsConnected(false);
      setReconnectAttempt(prev => prev + 1);
      console.log('SignalR alert stream reconnecting...', err?.message);
    });

    connection.onreconnected((connectionId) => {
      if (!isMountedRef.current) return;

      setIsReconnecting(false);
      setIsConnected(true);
      setReconnectAttempt(0);
      setError(null);
      console.log('SignalR alert stream reconnected, connectionId:', connectionId);
    });

    connection.onclose((err) => {
      if (!isMountedRef.current) return;

      setIsConnected(false);
      setIsReconnecting(false);
      if (err) {
        setError(err.message);
        console.error('SignalR alert stream closed with error:', err);
      } else {
        console.log('SignalR alert stream closed');
      }
    });

    // Subscribe to AlertTriggered events
    if (handlers.onAlertTriggered) {
      connection.on('AlertTriggered', (alert: Alert) => {
        if (!isMountedRef.current) return;
        handlers.onAlertTriggered?.(alert);
      });
    }

    // Subscribe to AlertResolved events
    if (handlers.onAlertResolved) {
      connection.on('AlertResolved', (alert: Alert) => {
        if (!isMountedRef.current) return;
        handlers.onAlertResolved?.(alert);
      });
    }

    // Start connection
    connection.start()
      .then(() => {
        if (!isMountedRef.current) return;

        setIsConnected(true);
        setError(null);
        console.log('SignalR alert stream connected to', hubUrl);
      })
      .catch((err) => {
        if (!isMountedRef.current) return;

        setError(err.message);
        console.error('SignalR alert stream connection failed:', err);
      });

    // Cleanup on unmount
    return () => {
      isMountedRef.current = false;
      connection.off('AlertTriggered');
      connection.off('AlertResolved');
      connection.stop().catch(err => {
        console.warn('Error stopping SignalR alert stream:', err);
      });
    };
  }, [hubUrl, handlers]);

  return { isConnected, isReconnecting, reconnectAttempt, error };
}

export default useAlertStream;
