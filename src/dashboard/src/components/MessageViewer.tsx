import { useState, useEffect } from 'react';
import { useKafkaStream } from '../hooks/useKafkaStream';
import type { KafkaMessage } from '../types/kafka';

/**
 * Props for MessageViewer component.
 */
interface MessageViewerProps {
  /** Topic to view messages from */
  topic: string;
  /** SignalR hub URL for live streaming */
  hubUrl: string;
  /** Function to fetch messages manually */
  getMessages: (topic: string, count?: number) => Promise<KafkaMessage[]>;
}

/**
 * MessageViewer component displays Kafka messages with two modes:
 * - Live: Real-time streaming via SignalR
 * - Manual: On-demand fetch via REST API
 *
 * Messages display partition, offset, key (optional), value, and timestamp.
 */
function MessageViewer({ topic, hubUrl, getMessages }: MessageViewerProps) {
  const [mode, setMode] = useState<'live' | 'manual'>('live');
  const [manualMessages, setManualMessages] = useState<KafkaMessage[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Live stream hook (only active in live mode)
  const {
    messages: liveMessages,
    isConnected,
    error: streamError,
    clearMessages
  } = useKafkaStream({
    hubUrl,
    topic,
    maxMessages: 50,
    enabled: mode === 'live'
  });

  // Fetch messages manually
  const handleRefresh = async () => {
    setLoading(true);
    setError(null);
    try {
      const msgs = await getMessages(topic, 50);
      setManualMessages(msgs);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch messages');
    } finally {
      setLoading(false);
    }
  };

  // Initial fetch in manual mode
  useEffect(() => {
    if (mode === 'manual') {
      handleRefresh();
    }
  }, [mode, topic]);

  // Clear when switching to live
  useEffect(() => {
    if (mode === 'live') {
      clearMessages();
    }
  }, [mode, clearMessages]);

  const messages = mode === 'live' ? liveMessages : manualMessages;
  const displayError = mode === 'live' ? streamError : error;

  return (
    <div className="message-viewer">
      <header className="message-viewer__header">
        <h4>Messages: {topic}</h4>
        <div className="message-viewer__controls">
          <div className="message-viewer__mode-toggle">
            <button
              className={`message-viewer__mode-btn ${mode === 'live' ? 'message-viewer__mode-btn--active' : ''}`}
              onClick={() => setMode('live')}
              type="button"
            >
              Live
            </button>
            <button
              className={`message-viewer__mode-btn ${mode === 'manual' ? 'message-viewer__mode-btn--active' : ''}`}
              onClick={() => setMode('manual')}
              type="button"
            >
              Manual
            </button>
          </div>
          {mode === 'manual' && (
            <button
              className="message-viewer__refresh-btn"
              onClick={handleRefresh}
              disabled={loading}
              type="button"
            >
              {loading ? 'Loading...' : 'Refresh'}
            </button>
          )}
          {mode === 'live' && (
            <span className={`message-viewer__status message-viewer__status--${isConnected ? 'connected' : 'disconnected'}`}>
              {isConnected ? 'Connected' : 'Disconnected'}
            </span>
          )}
        </div>
      </header>

      {displayError && (
        <div className="message-viewer__error">{displayError}</div>
      )}

      <div className="message-viewer__list">
        {messages.length === 0 ? (
          <div className="message-viewer__empty">
            {mode === 'live'
              ? 'Waiting for messages...'
              : 'No messages found. Try refreshing.'}
          </div>
        ) : (
          messages.map((msg, idx) => (
            <div key={`${msg.partition}-${msg.offset}-${idx}`} className="message-viewer__item">
              <div className="message-viewer__item-header">
                <span className="message-viewer__item-offset">
                  P{msg.partition}:O{msg.offset}
                </span>
                {msg.key && (
                  <span className="message-viewer__item-key">Key: {msg.key}</span>
                )}
                <span className="message-viewer__item-time">
                  {new Date(msg.timestamp).toLocaleTimeString()}
                </span>
              </div>
              <pre className="message-viewer__item-value">{msg.value}</pre>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default MessageViewer;
