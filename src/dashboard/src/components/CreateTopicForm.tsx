import { useState } from 'react';
import type { CreateTopicRequest } from '../types/kafka';

/**
 * Props for CreateTopicForm component.
 */
interface CreateTopicFormProps {
  /** Callback to submit topic creation (async, throws on error) */
  onSubmit: (request: CreateTopicRequest) => Promise<void>;
  /** Callback to close the modal */
  onCancel: () => void;
}

/**
 * CreateTopicForm component provides a modal for creating Kafka topics.
 * Validates topic name format and partition count before submission.
 */
function CreateTopicForm({ onSubmit, onCancel }: CreateTopicFormProps) {
  const [name, setName] = useState('');
  const [partitions, setPartitions] = useState(1);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Validate topic name
    if (!name.trim()) {
      setError('Topic name is required');
      return;
    }
    if (!/^[a-zA-Z0-9._-]+$/.test(name)) {
      setError('Topic name can only contain letters, numbers, dots, underscores, and hyphens');
      return;
    }
    if (name.length > 249) {
      setError('Topic name must be 249 characters or less');
      return;
    }

    // Validate partitions
    if (partitions < 1 || partitions > 100) {
      setError('Partitions must be between 1 and 100');
      return;
    }

    setSubmitting(true);
    try {
      await onSubmit({
        name: name.trim(),
        partitions,
        replicationFactor: 1  // Single broker in dev environment, always 1
      });
      onCancel();  // Close form on success
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create topic');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="create-topic-overlay" onClick={onCancel}>
      <form
        className="create-topic-form"
        onClick={e => e.stopPropagation()}
        onSubmit={handleSubmit}
      >
        <header className="create-topic-form__header">
          <h3>Create Topic</h3>
          <button
            type="button"
            className="create-topic-form__close"
            onClick={onCancel}
          >
            x
          </button>
        </header>

        {error && (
          <div className="create-topic-form__error">
            {error}
          </div>
        )}

        <div className="create-topic-form__field">
          <label htmlFor="topic-name">Topic Name</label>
          <input
            id="topic-name"
            type="text"
            value={name}
            onChange={e => setName(e.target.value)}
            placeholder="e.g., my-topic"
            autoFocus
            disabled={submitting}
          />
          <span className="create-topic-form__hint">
            Letters, numbers, dots, underscores, hyphens
          </span>
        </div>

        <div className="create-topic-form__field">
          <label htmlFor="topic-partitions">Partitions</label>
          <input
            id="topic-partitions"
            type="number"
            min={1}
            max={100}
            value={partitions}
            onChange={e => setPartitions(parseInt(e.target.value) || 1)}
            disabled={submitting}
          />
          <span className="create-topic-form__hint">
            More partitions = more parallelism (1-100)
          </span>
        </div>

        <footer className="create-topic-form__footer">
          <button
            type="button"
            className="create-topic-form__cancel"
            onClick={onCancel}
            disabled={submitting}
          >
            Cancel
          </button>
          <button
            type="submit"
            className="create-topic-form__submit"
            disabled={submitting}
          >
            {submitting ? 'Creating...' : 'Create Topic'}
          </button>
        </footer>
      </form>
    </div>
  );
}

export default CreateTopicForm;
