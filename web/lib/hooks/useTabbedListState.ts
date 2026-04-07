import { useState } from 'react';
import { useFilterState } from './useFilterState';

/**
 * Encapsulates the repeated pattern of: a view toggle between a main list and a
 * trash bin, plus a filter state for each view. Used by the Ads and Applications
 * pages to eliminate structural duplication.
 */
export function useTabbedListState<V extends string>(
  defaultView: V,
  mainFilterKey: string,
  trashFilterKey: string,
) {
  const [view, setView] = useState<V>(defaultView);
  const [filters, setFilters] = useFilterState(mainFilterKey);
  const [trashFilters, setTrashFilters] = useFilterState(trashFilterKey);
  return { view, setView, filters, setFilters, trashFilters, setTrashFilters };
}
