import './BatchOperationsBar.css';

interface BatchOperationsBarProps {
  selectedCount: number;
  onDelete: () => void;
  onSelectAll: () => void;
  onClearSelection: () => void;
  isDeleting?: boolean;
}

export function BatchOperationsBar({
  selectedCount,
  onDelete,
  onSelectAll,
  onClearSelection,
  isDeleting
}: BatchOperationsBarProps) {
  if (selectedCount === 0) return null;

  return (
    <div className="batch-operations-bar">
      <span className="selection-count">
        {selectedCount} server{selectedCount !== 1 ? 's' : ''} selected
      </span>

      <div className="batch-actions">
        <button
          type="button"
          className="batch-btn batch-btn--secondary"
          onClick={onSelectAll}
          disabled={isDeleting}
        >
          Select All
        </button>
        <button
          type="button"
          className="batch-btn batch-btn--secondary"
          onClick={onClearSelection}
          disabled={isDeleting}
        >
          Clear
        </button>
        <button
          type="button"
          className="batch-btn batch-btn--danger"
          onClick={onDelete}
          disabled={isDeleting}
        >
          {isDeleting ? 'Deleting...' : `Delete (${selectedCount})`}
        </button>
      </div>
    </div>
  );
}

export default BatchOperationsBar;
