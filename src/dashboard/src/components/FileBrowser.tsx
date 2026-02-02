import { useState, useEffect, useCallback } from 'react';
import type { FileNode } from '../types/fileTypes';
import { useFileOperations } from '../hooks/useFileOperations';
import FileTree from './FileTree';
import FileUploader from './FileUploader';

interface FileBrowserProps {
  apiBaseUrl: string;
}

/**
 * Main file browser container.
 * Combines file tree, upload, and operations.
 */
export function FileBrowser({ apiBaseUrl }: FileBrowserProps) {
  const { fetchTree, uploadFile, downloadFile, deleteFile, isLoading, error } = useFileOperations(apiBaseUrl);
  const [nodes, setNodes] = useState<FileNode[]>([]);
  const [selectedNode, setSelectedNode] = useState<FileNode | null>(null);
  const [currentPath, setCurrentPath] = useState<string>('');

  // Load initial tree
  const loadTree = useCallback(async (path?: string) => {
    const data = await fetchTree(path);
    setNodes(data);
  }, [fetchTree]);

  useEffect(() => {
    loadTree();
  }, [loadTree]);

  const handleUpload = useCallback(async (file: File) => {
    const result = await uploadFile(file, currentPath || undefined);
    if (result.success) {
      // Refresh tree after upload
      await loadTree(currentPath || undefined);
    }
    return result;
  }, [uploadFile, currentPath, loadTree]);

  const handleDownload = useCallback(async (node: FileNode) => {
    if (!node.isDirectory) {
      await downloadFile(node.id);
    }
  }, [downloadFile]);

  const handleDelete = useCallback(async (node: FileNode) => {
    const message = node.isDirectory
      ? `Delete folder "${node.name}" and all contents?`
      : `Delete file "${node.name}"?`;

    if (!window.confirm(message)) {
      return;
    }

    const result = await deleteFile(node.id, node.isDirectory);
    if (result.success) {
      // Clear selection if deleted node was selected
      if (selectedNode?.id === node.id) {
        setSelectedNode(null);
      }
      // Refresh tree
      await loadTree(currentPath || undefined);
    } else {
      alert(result.message || 'Delete failed');
    }
  }, [deleteFile, selectedNode, currentPath, loadTree]);

  const handleSelect = useCallback((node: FileNode | null) => {
    setSelectedNode(node);
    // If selecting a directory, could update currentPath for upload target
    if (node?.isDirectory) {
      setCurrentPath(node.id);
    }
  }, []);

  const handleRefresh = useCallback(() => {
    loadTree(currentPath || undefined);
  }, [loadTree, currentPath]);

  const handleNavigateUp = useCallback(() => {
    if (currentPath) {
      const parts = currentPath.split(/[/\\]/);
      parts.pop();
      const newPath = parts.join('/');
      setCurrentPath(newPath);
      loadTree(newPath || undefined);
    }
  }, [currentPath, loadTree]);

  return (
    <div className="file-browser">
      <div className="file-browser__header">
        <h2 className="file-browser__title">File Browser</h2>
        <div className="file-browser__breadcrumb">
          <button
            className="file-browser__nav-btn"
            onClick={handleNavigateUp}
            disabled={!currentPath}
            title="Go up"
          >
            ..
          </button>
          <span className="file-browser__path">
            /{currentPath || ''}
          </span>
        </div>
        <button
          className="file-browser__refresh"
          onClick={handleRefresh}
          disabled={isLoading}
          title="Refresh"
        >
          Refresh
        </button>
      </div>

      {error && (
        <div className="file-browser__error">
          {error}
        </div>
      )}

      <FileUploader
        onUpload={handleUpload}
        targetPath={currentPath}
        disabled={isLoading}
      />

      <div className="file-browser__tree-container">
        {isLoading && nodes.length === 0 ? (
          <div className="file-browser__loading">Loading...</div>
        ) : nodes.length === 0 ? (
          <div className="file-browser__empty">
            No files found. Drop files above to upload.
          </div>
        ) : (
          <FileTree
            nodes={nodes}
            onSelect={handleSelect}
            onDelete={handleDelete}
            onDownload={handleDownload}
            height={400}
          />
        )}
      </div>

      {selectedNode && (
        <div className="file-browser__details">
          <h3>{selectedNode.name}</h3>
          <p>Path: {selectedNode.id}</p>
          <p>Type: {selectedNode.isDirectory ? 'Directory' : 'File'}</p>
          {selectedNode.size !== undefined && (
            <p>Size: {selectedNode.size} bytes</p>
          )}
          <p>Modified: {selectedNode.modified}</p>
          <p>Protocols: {selectedNode.protocols.join(', ') || 'None'}</p>
        </div>
      )}
    </div>
  );
}

export default FileBrowser;
