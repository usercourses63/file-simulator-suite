import { useState, useRef } from 'react';
import { useConfigExport } from '../hooks/useConfigExport';
import {
  ServerConfigurationExport,
  ConflictResolution
} from '../types/serverManagement';
import './ImportConfigDialog.css';

interface ImportConfigDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onImported: () => void;
  apiBaseUrl: string;
}

type ImportStep = 'select-file' | 'review-conflicts' | 'importing' | 'complete';

// Per-conflict resolution state
interface ConflictDecision {
  serverName: string;
  action: 'skip' | 'replace' | 'rename';
  newName: string;  // Only used if action === 'rename'
}

export function ImportConfigDialog({
  isOpen,
  onClose,
  onImported,
  apiBaseUrl
}: ImportConfigDialogProps) {
  const {
    isImporting,
    isValidating,
    error,
    validation,
    importResult,
    importWithResolutions,
    importFile,
    clearError,
    clearResults
  } = useConfigExport({ apiBaseUrl });

  const [step, setStep] = useState<ImportStep>('select-file');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [parsedConfig, setParsedConfig] = useState<ServerConfigurationExport | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Per-conflict decisions - key is serverName
  const [conflictDecisions, setConflictDecisions] = useState<Map<string, ConflictDecision>>(new Map());

  // Current conflict index being resolved (for stepping through conflicts)
  const [currentConflictIndex, setCurrentConflictIndex] = useState(0);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      setSelectedFile(file);
      const { config, validation } = await importFile(file);
      setParsedConfig(config);

      // If there are conflicts, initialize decisions and go to conflict resolution
      if (validation.conflicts.length > 0) {
        const initialDecisions = new Map<string, ConflictDecision>();
        validation.conflicts.forEach(conflict => {
          initialDecisions.set(conflict.serverName, {
            serverName: conflict.serverName,
            action: 'skip',  // Default to skip
            newName: `${conflict.serverName}-imported`
          });
        });
        setConflictDecisions(initialDecisions);
        setCurrentConflictIndex(0);
        setStep('review-conflicts');
      } else {
        // No conflicts - go straight to import
        await handleImportNoConflicts(config);
      }
    } catch (err) {
      // Error displayed in dialog
    }
  };

  const handleImportNoConflicts = async (config: ServerConfigurationExport) => {
    setStep('importing');
    try {
      await importWithResolutions(config, []);
      setStep('complete');
    } catch {
      setStep('select-file');
    }
  };

  // Update decision for a specific conflict
  const updateConflictDecision = (serverName: string, action: 'skip' | 'replace' | 'rename', newName?: string) => {
    setConflictDecisions(prev => {
      const next = new Map(prev);
      const current = next.get(serverName)!;
      next.set(serverName, {
        ...current,
        action,
        newName: newName ?? current.newName
      });
      return next;
    });
  };

  // Proceed to next conflict or start import
  const handleNextConflict = () => {
    if (!validation) return;

    if (currentConflictIndex < validation.conflicts.length - 1) {
      setCurrentConflictIndex(currentConflictIndex + 1);
    } else {
      // All conflicts reviewed - start import
      handleStartImport();
    }
  };

  // Go back to previous conflict
  const handlePrevConflict = () => {
    if (currentConflictIndex > 0) {
      setCurrentConflictIndex(currentConflictIndex - 1);
    }
  };

  const handleStartImport = async () => {
    if (!parsedConfig) return;

    setStep('importing');

    // Convert decisions map to array of ConflictResolution
    const resolutions: ConflictResolution[] = Array.from(conflictDecisions.values()).map(d => ({
      serverName: d.serverName,
      action: d.action,
      newName: d.action === 'rename' ? d.newName : undefined
    }));

    try {
      await importWithResolutions(parsedConfig, resolutions);
      setStep('complete');
    } catch {
      setStep('review-conflicts');
    }
  };

  const handleClose = () => {
    setStep('select-file');
    setSelectedFile(null);
    setParsedConfig(null);
    setConflictDecisions(new Map());
    setCurrentConflictIndex(0);
    clearError();
    clearResults();
    onClose();
  };

  const handleDone = () => {
    handleClose();
    onImported();
  };

  if (!isOpen) return null;

  // Get current conflict for resolution
  const currentConflict = validation?.conflicts[currentConflictIndex];
  const currentDecision = currentConflict ? conflictDecisions.get(currentConflict.serverName) : undefined;

  const renderStepContent = () => {
    switch (step) {
      case 'select-file':
        return (
          <div className="import-step-content">
            <div
              className="file-drop-zone"
              onClick={() => fileInputRef.current?.click()}
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M17 8l-5-5-5 5M12 3v12" />
              </svg>
              <p>Click to select a configuration file</p>
              <span className="file-hint">or drag and drop .json file</span>
            </div>
            <input
              ref={fileInputRef}
              type="file"
              accept=".json"
              onChange={handleFileSelect}
              style={{ display: 'none' }}
            />
            {isValidating && (
              <div className="validating-indicator">
                <div className="spinner"></div>
                <span>Analyzing configuration...</span>
              </div>
            )}
            {error && <div className="error-message">{error}</div>}
          </div>
        );

      case 'review-conflicts':
        if (!validation || !currentConflict || !currentDecision) return null;

        return (
          <div className="import-step-content">
            {/* File summary */}
            <div className="file-summary">
              <strong>{selectedFile?.name}</strong>
              <span className="summary-stats">
                {validation.willCreate.length} new, {validation.conflicts.length} conflicts
              </span>
            </div>

            {/* Progress indicator */}
            <div className="conflict-progress">
              <span>Conflict {currentConflictIndex + 1} of {validation.conflicts.length}</span>
              <div className="progress-bar">
                <div
                  className="progress-fill"
                  style={{ width: `${((currentConflictIndex + 1) / validation.conflicts.length) * 100}%` }}
                />
              </div>
            </div>

            {/* Current conflict card */}
            <div className="conflict-card">
              <div className="conflict-header">
                <span className="conflict-icon">!</span>
                <div className="conflict-info">
                  <strong>{currentConflict.serverName}</strong>
                  <span className="conflict-protocol">{currentConflict.protocol}</span>
                </div>
              </div>

              <p className="conflict-description">
                A server named <strong>{currentConflict.serverName}</strong> already exists.
                {currentConflict.existingNodePort && (
                  <> Currently on port {currentConflict.existingNodePort}.</>
                )}
              </p>

              {/* Per-conflict resolution options */}
              <div className="resolution-options">
                <label className={`resolution-option ${currentDecision.action === 'skip' ? 'selected' : ''}`}>
                  <input
                    type="radio"
                    name="resolution"
                    checked={currentDecision.action === 'skip'}
                    onChange={() => updateConflictDecision(currentConflict.serverName, 'skip')}
                  />
                  <div className="option-content">
                    <strong>Skip</strong>
                    <span>Keep existing server, don't import this one</span>
                  </div>
                </label>

                <label className={`resolution-option ${currentDecision.action === 'replace' ? 'selected' : ''}`}>
                  <input
                    type="radio"
                    name="resolution"
                    checked={currentDecision.action === 'replace'}
                    onChange={() => updateConflictDecision(currentConflict.serverName, 'replace')}
                  />
                  <div className="option-content">
                    <strong>Replace</strong>
                    <span>Delete existing server, import this one</span>
                  </div>
                </label>

                <label className={`resolution-option ${currentDecision.action === 'rename' ? 'selected' : ''}`}>
                  <input
                    type="radio"
                    name="resolution"
                    checked={currentDecision.action === 'rename'}
                    onChange={() => updateConflictDecision(currentConflict.serverName, 'rename')}
                  />
                  <div className="option-content">
                    <strong>Rename</strong>
                    <span>Import with a different name</span>
                  </div>
                </label>

                {currentDecision.action === 'rename' && (
                  <div className="rename-input">
                    <label>New name:</label>
                    <input
                      type="text"
                      value={currentDecision.newName}
                      onChange={e => updateConflictDecision(
                        currentConflict.serverName,
                        'rename',
                        e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, '-')
                      )}
                      placeholder="my-server-imported"
                    />
                  </div>
                )}
              </div>
            </div>

            {/* Quick summary of all decisions */}
            <div className="decisions-summary">
              <strong>Your decisions:</strong>
              <div className="decision-list">
                {Array.from(conflictDecisions.entries()).map(([name, decision], idx) => (
                  <span
                    key={name}
                    className={`decision-badge decision-badge--${decision.action} ${idx === currentConflictIndex ? 'current' : ''}`}
                    onClick={() => setCurrentConflictIndex(idx)}
                  >
                    {name}: {decision.action}
                  </span>
                ))}
              </div>
            </div>

            {error && <div className="error-message">{error}</div>}
          </div>
        );

      case 'importing':
        return (
          <div className="import-step-content import-step-content--centered">
            <div className="import-spinner"></div>
            <p>Importing configuration...</p>
          </div>
        );

      case 'complete':
        return (
          <div className="import-step-content">
            <div className="import-complete">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M22 11.08V12a10 10 0 11-5.93-9.14" />
                <path d="M22 4L12 14.01l-3-3" />
              </svg>
              <h3>Import Complete</h3>
            </div>

            {importResult && (
              <div className="import-results">
                {importResult.created.length > 0 && (
                  <div className="result-section result-section--success">
                    <strong>Created ({importResult.created.length}):</strong>
                    <div className="result-items">
                      {importResult.created.map(name => (
                        <span key={name}>{name}</span>
                      ))}
                    </div>
                  </div>
                )}

                {importResult.skipped.length > 0 && (
                  <div className="result-section result-section--skipped">
                    <strong>Skipped ({importResult.skipped.length}):</strong>
                    <div className="result-items">
                      {importResult.skipped.map(name => (
                        <span key={name}>{name}</span>
                      ))}
                    </div>
                  </div>
                )}

                {Object.keys(importResult.failed).length > 0 && (
                  <div className="result-section result-section--error">
                    <strong>Failed ({Object.keys(importResult.failed).length}):</strong>
                    <div className="result-items">
                      {Object.entries(importResult.failed).map(([name, error]) => (
                        <span key={name} title={error}>{name}</span>
                      ))}
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        );
    }
  };

  return (
    <div className="modal-overlay" onClick={handleClose}>
      <div className="import-config-dialog" onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Import Configuration</h2>
          <button type="button" className="modal-close" onClick={handleClose}>&times;</button>
        </div>

        <div className="modal-body">
          {renderStepContent()}
        </div>

        <div className="modal-footer">
          {step === 'select-file' && (
            <button type="button" className="btn btn--secondary" onClick={handleClose}>
              Cancel
            </button>
          )}

          {step === 'review-conflicts' && validation && (
            <>
              <button
                type="button"
                className="btn btn--secondary"
                onClick={handlePrevConflict}
                disabled={currentConflictIndex === 0}
              >
                Previous
              </button>
              <button
                type="button"
                className="btn btn--primary"
                onClick={handleNextConflict}
                disabled={isImporting}
              >
                {currentConflictIndex < validation.conflicts.length - 1 ? 'Next' : 'Import'}
              </button>
            </>
          )}

          {step === 'complete' && (
            <button type="button" className="btn btn--primary" onClick={handleDone}>
              Done
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

export default ImportConfigDialog;
