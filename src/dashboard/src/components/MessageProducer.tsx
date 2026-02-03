import { useState } from 'react';
import type { ProduceMessageRequest, ProduceMessageResult } from '../types/kafka';

/**
 * Props for MessageProducer component.
 */
interface MessageProducerProps {
  /** Topic to produce messages to */
  topic: string;
  /** Callback to produce a message (async, returns result or throws) */
  onProduce: (request: ProduceMessageRequest) => Promise<ProduceMessageResult>;
}

/**
 * MessageProducer component provides a form to produce messages to a Kafka topic.
 * Supports optional message key and required message body.
 */
function MessageProducer({ topic, onProduce }: MessageProducerProps) {
  const [key, setKey] = useState('');
  const [value, setValue] = useState('');
  const [sending, setSending] = useState(false);
  const [result, setResult] = useState<ProduceMessageResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!value.trim()) {
      setError('Message body is required');
      return;
    }

    setError(null);
    setResult(null);
    setSending(true);

    try {
      const res = await onProduce({
        topic,
        key: key.trim() || null,
        value: value.trim()
      });
      setResult(res);
      setValue('');  // Clear message after success
      // Keep key for convenience (often same key for related messages)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to send message');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="message-producer">
      <h4 className="message-producer__title">
        Produce Message to "{topic}"
      </h4>

      <form className="message-producer__form" onSubmit={handleSubmit}>
        <div className="message-producer__field">
          <label htmlFor="msg-key">Key (optional)</label>
          <input
            id="msg-key"
            type="text"
            value={key}
            onChange={e => setKey(e.target.value)}
            placeholder="Message key for partitioning"
            disabled={sending}
          />
        </div>

        <div className="message-producer__field">
          <label htmlFor="msg-value">Message Body</label>
          <textarea
            id="msg-value"
            value={value}
            onChange={e => setValue(e.target.value)}
            placeholder="Enter message content (any string)"
            rows={4}
            disabled={sending}
          />
        </div>

        {error && (
          <div className="message-producer__error">{error}</div>
        )}

        {result && (
          <div className="message-producer__success">
            Sent to partition {result.partition} @ offset {result.offset}
          </div>
        )}

        <button
          type="submit"
          className="message-producer__submit"
          disabled={sending || !value.trim()}
        >
          {sending ? 'Sending...' : 'Send Message'}
        </button>
      </form>
    </div>
  );
}

export default MessageProducer;
