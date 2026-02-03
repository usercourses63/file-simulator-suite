import { useState, useCallback, useMemo } from 'react';

interface MultiSelectItem {
  name: string;
  isDynamic?: boolean;
}

interface UseMultiSelectReturn<T extends MultiSelectItem> {
  selectedIds: Set<string>;
  selectedItems: T[];
  selectedCount: number;
  isSelected: (id: string) => boolean;
  toggleSelect: (id: string) => void;
  selectAll: () => void;
  clearSelection: () => void;
  setSelected: (ids: string[]) => void;
}

export function useMultiSelect<T extends MultiSelectItem>(
  items: T[],
  canSelect: (item: T) => boolean = (item) => item.isDynamic === true
): UseMultiSelectReturn<T> {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const toggleSelect = useCallback((id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const selectAll = useCallback(() => {
    const selectableIds = items
      .filter(canSelect)
      .map(item => item.name);
    setSelectedIds(new Set(selectableIds));
  }, [items, canSelect]);

  const clearSelection = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  const setSelected = useCallback((ids: string[]) => {
    setSelectedIds(new Set(ids));
  }, []);

  const isSelected = useCallback((id: string) => selectedIds.has(id), [selectedIds]);

  const selectedItems = useMemo(
    () => items.filter(item => selectedIds.has(item.name)),
    [items, selectedIds]
  );

  return {
    selectedIds,
    selectedItems,
    selectedCount: selectedIds.size,
    isSelected,
    toggleSelect,
    selectAll,
    clearSelection,
    setSelected
  };
}

export default useMultiSelect;
