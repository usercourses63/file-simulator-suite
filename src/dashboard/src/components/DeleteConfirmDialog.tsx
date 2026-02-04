import { useState } from 'react';
import './DeleteConfirmDialog.css';

interface DeleteConfirmDialogProps {
  isOpen: boolean;
  serverName: string;
  serverNames?: string[];  // For batch delete
  isNasServer: boolean;
  isDeleting: boolean;
  onConfirm: (deleteData: boolean) => void;
  onCancel: () => void;
}

export function DeleteConfirmDialog({
  isOpen,
  serverName,
  serverNames,
  isNasServer,
  isDeleting,
  onConfirm,
  onCancel
}: DeleteConfirmDialogProps) {
  const [deleteFiles, setDeleteFiles] = useState(false);

  if (!isOpen) return null;

  // Determine display mode - use serverNames if provided (even for single)
  const useServerNames = serverNames && serverNames.length > 0;
  const isMultiple = useServerNames && serverNames.length > 1;

  // Get display name for single server delete
  const displayName = useServerNames
    ? serverNames[0]  // First name from batch (handles single-via-multiselect)
    : serverName;     // Direct single delete

  const title = isMultiple
    ? `Delete ${serverNames.length} servers?`
    : `Delete server "${displayName}"?`;

  const handleConfirm = () => {
    onConfirm(deleteFiles);
  };

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="delete-confirm-dialog" onClick={e => e.stopPropagation()}>
        <div className="dialog-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <path d="M3 6h18M8 6V4a2 2 0 012-2h4a2 2 0 012 2v2m3 0v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6h14z" />
          </svg>
        </div>

        <h3>{title}</h3>

        {isMultiple && serverNames && (
          <div className="server-list">
            {serverNames.map(name => (
              <span key={name} className="server-tag">{name}</span>
            ))}
          </div>
        )}

        <p className="warning-text">
          This will permanently delete the deployment and service.
          {isNasServer && ' The NAS directory may contain files.'}
        </p>

        {isNasServer && (
          <label className="delete-files-option">
            <input
              type="checkbox"
              checked={deleteFiles}
              onChange={e => setDeleteFiles(e.target.checked)}
              disabled={isDeleting}
            />
            Also delete files from Windows directory
          </label>
        )}

        <div className="dialog-actions">
          <button
            type="button"
            className="btn btn--secondary"
            onClick={onCancel}
            disabled={isDeleting}
          >
            Cancel
          </button>
          <button
            type="button"
            className="btn btn--danger"
            onClick={handleConfirm}
            disabled={isDeleting}
          >
            {isDeleting ? 'Deleting...' : 'Delete'}
          </button>
        </div>
      </div>
    </div>
  );
}

export default DeleteConfirmDialog;
