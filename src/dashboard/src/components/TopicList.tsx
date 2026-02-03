import { useState } from 'react';
import type { TopicInfo } from '../types/kafka';

/**
 * Props for TopicList component.
 */
interface TopicListProps {
  /** List of Kafka topics to display */
  topics: TopicInfo[];
  /** Currently selected topic name, or null if none */
  selectedTopic: string | null;
  /** Callback when a topic is selected or deselected */
  onSelectTopic: (topic: string | null) => void;
  /** Callback to delete a topic (async, throws on error) */
  onDeleteTopic: (name: string) => Promise<void>;
  /** Callback when create topic button is clicked */
  onCreateClick: () => void;
}

/**
 * TopicList component displays Kafka topics with selection and delete capability.
 * Shows topic name and partition count, with inline delete confirmation.
 */
function TopicList({
  topics,
  selectedTopic,
  onSelectTopic,
  onDeleteTopic,
  onCreateClick
}: TopicListProps) {
  const [deletingTopic, setDeletingTopic] = useState<string | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null);

  const handleDelete = async (name: string) => {
    setDeletingTopic(name);
    try {
      await onDeleteTopic(name);
      if (selectedTopic === name) {
        onSelectTopic(null);
      }
    } catch (err) {
      console.error('Failed to delete topic:', err);
      alert(`Failed to delete topic: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setDeletingTopic(null);
      setConfirmDelete(null);
    }
  };

  return (
    <div className="topic-list">
      <header className="topic-list__header">
        <h3>Topics</h3>
        <button
          className="topic-list__create-btn"
          onClick={onCreateClick}
          type="button"
        >
          + Create Topic
        </button>
      </header>

      {topics.length === 0 ? (
        <div className="topic-list__empty">
          No topics found. Create one to get started.
        </div>
      ) : (
        <ul className="topic-list__items">
          {topics.map(topic => (
            <li
              key={topic.name}
              className={`topic-list__item ${selectedTopic === topic.name ? 'topic-list__item--selected' : ''}`}
            >
              <button
                className="topic-list__item-btn"
                onClick={() => onSelectTopic(selectedTopic === topic.name ? null : topic.name)}
                type="button"
              >
                <span className="topic-list__item-name">{topic.name}</span>
                <span className="topic-list__item-info">
                  {topic.partitionCount} partition{topic.partitionCount !== 1 ? 's' : ''}
                </span>
              </button>

              {confirmDelete === topic.name ? (
                <div className="topic-list__confirm">
                  <span>Delete?</span>
                  <button
                    className="topic-list__confirm-yes"
                    onClick={() => handleDelete(topic.name)}
                    disabled={deletingTopic === topic.name}
                    type="button"
                  >
                    {deletingTopic === topic.name ? '...' : 'Yes'}
                  </button>
                  <button
                    className="topic-list__confirm-no"
                    onClick={() => setConfirmDelete(null)}
                    type="button"
                  >
                    No
                  </button>
                </div>
              ) : (
                <button
                  className="topic-list__delete-btn"
                  onClick={(e) => {
                    e.stopPropagation();
                    setConfirmDelete(topic.name);
                  }}
                  title="Delete topic"
                  type="button"
                >
                  x
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default TopicList;
