'use client';

import { useState, useEffect, useRef } from 'react';
import { type FilterState, EMPTY_FILTERS } from '@/components/ads/FilterBar';

/**
 * Persists a FilterState in localStorage under the given key.
 * Each call site uses its own key, so different pages keep independent filter state.
 * Returns [filters, setFilters] — identical API to useState.
 */
export function useFilterState(storageKey: string): [FilterState, (f: FilterState) => void] {
  const [filters, setFilters] = useState<FilterState>(EMPTY_FILTERS);
  const isFirst = useRef(true);

  // Restore from localStorage after hydration (must not run on server).
  useEffect(() => {
    try {
      const stored = localStorage.getItem(storageKey);
      if (stored) setFilters({ ...EMPTY_FILTERS, ...JSON.parse(stored) });
    } catch {}
  }, [storageKey]);

  // Persist on every change, skipping the initial render.
  useEffect(() => {
    if (isFirst.current) { isFirst.current = false; return; }
    localStorage.setItem(storageKey, JSON.stringify(filters));
  }, [filters, storageKey]);

  return [filters, setFilters];
}
