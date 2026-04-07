'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { STALE_TIMES } from '@/lib/constants';
import type { TriState } from './types';
import { TriStateCheckRow } from './TriStateCheckRow';

interface TypeaheadPickerProps {
  cacheKey: string;
  placeholder: string;
  fetchFn: (q: string) => Promise<string[]>;
  included: string[];
  excluded: string[];
  onCycle: (value: string) => void;
  onExclude: (value: string) => void;
}

export function TypeaheadPicker({
  cacheKey,
  placeholder,
  fetchFn,
  included,
  excluded,
  onCycle,
  onExclude,
}: TypeaheadPickerProps) {
  const [q, setQ] = useState('');
  const { data: suggestions = [] } = useQuery({
    queryKey: ['distinct', cacheKey, q],
    queryFn: () => fetchFn(q),
    staleTime: STALE_TIMES.MEDIUM,
  });

  const trimmed = q.trim();
  const allSelected = [...included, ...excluded];
  const canAddRaw =
    trimmed.length > 0 &&
    !suggestions.some(s => s.toLowerCase() === trimmed.toLowerCase()) &&
    !allSelected.some(s => s.toLowerCase() === trimmed.toLowerCase());

  const displayList = [
    ...allSelected.filter(s => !suggestions.includes(s)),
    ...suggestions,
  ];

  return (
    <div className="p-3 w-64">
      <input
        autoFocus
        type="text"
        value={q}
        onChange={e => setQ(e.target.value)}
        onKeyDown={e => {
          if (e.key === 'Enter' && trimmed) {
            onCycle(trimmed);
            setQ('');
          }
        }}
        placeholder={placeholder}
        className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 mb-2"
      />
      {canAddRaw && (
        <div className="flex gap-1 mb-2">
          <button
            onClick={() => { onCycle(trimmed); setQ(''); }}
            className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded text-xs font-medium text-indigo-700 bg-indigo-50 hover:bg-indigo-100 transition-colors"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
              <path d="M8.75 3.75a.75.75 0 0 0-1.5 0v3.5h-3.5a.75.75 0 0 0 0 1.5h3.5v3.5a.75.75 0 0 0 1.5 0v-3.5h3.5a.75.75 0 0 0 0-1.5h-3.5v-3.5Z" />
            </svg>
            Include
          </button>
          <button
            onClick={() => { onExclude(trimmed); setQ(''); }}
            className="flex-1 flex items-center justify-center gap-1 py-1.5 rounded text-xs font-medium text-rose-700 bg-rose-50 hover:bg-rose-100 transition-colors"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
              <path d="M3.75 7.25a.75.75 0 0 0 0 1.5h8.5a.75.75 0 0 0 0-1.5h-8.5Z" />
            </svg>
            Exclude
          </button>
        </div>
      )}
      <p className="text-[10px] text-gray-400 mb-1.5 px-1">Click to include · click again to exclude · once more to clear</p>
      <div className="max-h-52 overflow-y-auto space-y-0.5">
        {displayList.map(item => {
          const state: TriState = included.includes(item) ? 'include' : excluded.includes(item) ? 'exclude' : 'none';
          return (
            <TriStateCheckRow
              key={item}
              label={item}
              state={state}
              onCycle={() => onCycle(item)}
            />
          );
        })}
        {displayList.length === 0 && !canAddRaw && (
          <p className="text-xs text-gray-400 px-2 py-2 italic">No results. Type to add a custom value.</p>
        )}
      </div>
    </div>
  );
}
