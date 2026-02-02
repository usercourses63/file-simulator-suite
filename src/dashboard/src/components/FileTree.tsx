import { useEffect, useState, useCallback } from 'react';
import { Tree, NodeRendererProps } from 'react-arborist';
import type { FileNode } from '../types/fileTypes';
import ProtocolBadges from './ProtocolBadges';

interface FileTreeProps {
  nodes: FileNode[];
  onSelect?: (node: FileNode | null) => void;
  onDelete?: (node: FileNode) => void;
  onDownload?: (node: FileNode) => void;
  onLoadChildren?: (path: string) => Promise<FileNode[]>;
  height?: number;
}

// Format bytes to human-readable
function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

// Format date to locale string
function formatDate(isoDate: string): string {
  try {
    return new Date(isoDate).toLocaleString();
  } catch {
    return isoDate;
  }
}

/**
 * File tree browser using react-arborist.
 * Supports lazy loading of children on expand.
 */
export function FileTree({
  nodes,
  onSelect,
  onDelete,
  onDownload,
  height = 400,
}: FileTreeProps) {
  // Convert FileNode[] to react-arborist format
  // react-arborist expects 'children' to be an array or undefined
  const [treeData, setTreeData] = useState<FileNode[]>(nodes);

  useEffect(() => {
    setTreeData(nodes);
  }, [nodes]);

  const handleSelect = useCallback((selected: { data: FileNode }[]) => {
    if (onSelect) {
      onSelect(selected.length > 0 ? selected[0].data : null);
    }
  }, [onSelect]);

  // Custom node renderer
  const Node = ({ node, style, dragHandle }: NodeRendererProps<FileNode>) => {
    const data = node.data;

    const handleDeleteClick = (e: React.MouseEvent) => {
      e.stopPropagation();
      if (onDelete) {
        onDelete(data);
      }
    };

    const handleDownloadClick = (e: React.MouseEvent) => {
      e.stopPropagation();
      if (onDownload && !data.isDirectory) {
        onDownload(data);
      }
    };

    return (
      <div
        className="file-tree-node"
        style={style}
        ref={dragHandle}
        onClick={() => node.isInternal && node.toggle()}
      >
        <span className="file-tree-node__icon">
          {data.isDirectory ? (node.isOpen ? '📂' : '📁') : '📄'}
        </span>

        <span className="file-tree-node__name" title={data.id}>
          {data.name}
        </span>

        {!data.isDirectory && data.size !== undefined && (
          <span className="file-tree-node__size">
            {formatBytes(data.size)}
          </span>
        )}

        <span className="file-tree-node__modified" title={formatDate(data.modified)}>
          {formatDate(data.modified).split(',')[0]}
        </span>

        <ProtocolBadges protocols={data.protocols} size="small" />

        <div className="file-tree-node__actions">
          {!data.isDirectory && (
            <button
              className="file-tree-node__action"
              onClick={handleDownloadClick}
              title="Download"
            >
              ⬇
            </button>
          )}
          <button
            className="file-tree-node__action file-tree-node__action--delete"
            onClick={handleDeleteClick}
            title="Delete"
          >
            🗑
          </button>
        </div>
      </div>
    );
  };

  return (
    <div className="file-tree">
      <Tree
        data={treeData}
        openByDefault={false}
        width="100%"
        height={height}
        indent={24}
        rowHeight={36}
        onSelect={handleSelect}
        childrenAccessor="children"
        idAccessor="id"
      >
        {Node}
      </Tree>
    </div>
  );
}

export default FileTree;
