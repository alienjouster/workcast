'use client';

import React, { useState, useRef, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import type { JobBoard } from '@/types';
import { api } from '@/lib/api';

// ── Types ────────────────────────────────────────────────────────────────────

export type StatusTag = 'active' | 'inactive' | 'read' | 'unread' | 'pinned' | 'unpinned';

export interface FilterState {
  boardIds: string[];
  statuses: StatusTag[];
  locations: string[];
  companies: string[];
  minScore: number | undefined;
}

export const EMPTY_FILTERS: FilterState = {
  boardIds: [],
  statuses: [],
  locations: [],
  companies: [],
  minScore: undefined,
};

type PopoverView = 'menu' | 'board' | 'status' | 'location' | 'company' | 'score';

interface FilterBarProps {
  filters: FilterState;
  onChange: (f: FilterState) => void;
  boards: JobBoard[];
}

// ── Chip ─────────────────────────────────────────────────────────────────────

function Chip({ label, onRemove }: { label: string; onRemove: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-indigo-100 pl-3 pr-1.5 py-1 text-xs font-medium text-indigo-800">
      {label}
      <button
        onClick={onRemove}
        className="rounded-full p-0.5 hover:bg-indigo-200 transition-colors"
        aria-label="Remove filter"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3 h-3">
          <path d="M5.28 4.22a.75.75 0 0 0-1.06 1.06L6.94 8l-2.72 2.72a.75.75 0 1 0 1.06 1.06L8 9.06l2.72 2.72a.75.75 0 1 0 1.06-1.06L9.06 8l2.72-2.72a.75.75 0 0 0-1.06-1.06L8 6.94 5.28 4.22Z" />
        </svg>
      </button>
    </span>
  );
}

// ── Checkbox row ─────────────────────────────────────────────────────────────

function CheckRow({ label, checked, onToggle }: { label: string; checked: boolean; onToggle: () => void }) {
  return (
    <button
      onClick={onToggle}
      className={`flex items-center gap-2 w-full text-left px-2 py-1.5 rounded text-sm hover:bg-gray-50 ${checked ? 'text-indigo-700 font-medium' : 'text-gray-700'}`}
    >
      <span className={`w-4 h-4 rounded border flex items-center justify-center shrink-0 ${checked ? 'bg-indigo-600 border-indigo-600' : 'border-gray-300'}`}>
        {checked && (
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 12 12" fill="none" className="w-3 h-3">
            <path d="M1 6l3.5 3.5L11 2" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        )}
      </span>
      {label}
    </button>
  );
}

// ── Back button ───────────────────────────────────────────────────────────────

function BackButton({ onClick }: { onClick: () => void }) {
  return (
    <button onClick={onClick} className="flex items-center gap-1 text-xs text-gray-400 hover:text-gray-600 mb-2">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3 h-3">
        <path fillRule="evenodd" d="M9.78 4.22a.75.75 0 0 1 0 1.06L7.06 8l2.72 2.72a.75.75 0 1 1-1.06 1.06L5.47 8.53a.75.75 0 0 1 0-1.06l3.25-3.25a.75.75 0 0 1 1.06 0Z" clipRule="evenodd" />
      </svg>
      Back
    </button>
  );
}

// ── Typeahead picker ──────────────────────────────────────────────────────────

function TypeaheadPicker({
  type,
  selected,
  onToggle,
}: {
  type: 'locations' | 'companies';
  selected: string[];
  onToggle: (value: string) => void;
}) {
  const [q, setQ] = useState('');
  const { data: suggestions = [] } = useQuery({
    queryKey: ['distinct', type, q],
    queryFn: () => type === 'locations' ? api.ads.distinctLocations(q) : api.ads.distinctCompanies(q),
    staleTime: 30_000,
  });

  const trimmed = q.trim();
  const canAddRaw =
    trimmed.length > 0 &&
    !suggestions.some(s => s.toLowerCase() === trimmed.toLowerCase()) &&
    !selected.some(s => s.toLowerCase() === trimmed.toLowerCase());

  // Show selected values that are not in the current suggestion list first, then suggestions
  const displayList = [
    ...selected.filter(s => !suggestions.includes(s)),
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
            onToggle(trimmed);
            setQ('');
          }
        }}
        placeholder={type === 'locations' ? 'Search locations…' : 'Search companies…'}
        className="w-full rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 mb-2"
      />
      <div className="max-h-52 overflow-y-auto space-y-0.5">
        {displayList.map(item => (
          <CheckRow
            key={item}
            label={item}
            checked={selected.includes(item)}
            onToggle={() => onToggle(item)}
          />
        ))}
        {canAddRaw && (
          <button
            onClick={() => { onToggle(trimmed); setQ(''); }}
            className="flex items-center gap-2 w-full text-left px-2 py-1.5 rounded text-sm text-indigo-600 hover:bg-indigo-50 font-medium"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4 shrink-0 text-indigo-400">
              <path d="M8.75 3.75a.75.75 0 0 0-1.5 0v3.5h-3.5a.75.75 0 0 0 0 1.5h3.5v3.5a.75.75 0 0 0 1.5 0v-3.5h3.5a.75.75 0 0 0 0-1.5h-3.5v-3.5Z" />
            </svg>
            Add "{trimmed}"
          </button>
        )}
        {displayList.length === 0 && !canAddRaw && (
          <p className="text-xs text-gray-400 px-2 py-2 italic">No results. Type to add a custom value.</p>
        )}
      </div>
    </div>
  );
}

// ── Score picker ──────────────────────────────────────────────────────────────

function ScorePicker({ value, onChange }: { value: number | undefined; onChange: (v: number | undefined) => void }) {
  const [local, setLocal] = useState(value ?? 70);

  return (
    <div className="p-4 w-60">
      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">Minimum match score</p>
      <div className="flex gap-2 mb-4">
        {[70, 80, 90].map(preset => (
          <button
            key={preset}
            onClick={() => { setLocal(preset); onChange(preset); }}
            className={`flex-1 py-1.5 rounded-md text-sm font-medium border transition-colors ${
              value === preset
                ? 'bg-indigo-600 text-white border-indigo-600'
                : 'border-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
          >
            ≥{preset}%
          </button>
        ))}
      </div>
      <div className="space-y-1.5">
        <div className="flex justify-between text-xs text-gray-500">
          <span>0%</span>
          <span className="font-semibold text-indigo-700">≥ {local}%</span>
          <span>100%</span>
        </div>
        <input
          type="range"
          min={0}
          max={100}
          step={5}
          value={local}
          onChange={e => setLocal(Number(e.target.value))}
          onMouseUp={() => onChange(local)}
          onTouchEnd={() => onChange(local)}
          className="w-full accent-indigo-600"
        />
      </div>
      {value !== undefined && (
        <button
          onClick={() => onChange(undefined)}
          className="mt-3 w-full text-xs text-gray-400 hover:text-red-500 text-center transition-colors"
        >
          Clear score filter
        </button>
      )}
    </div>
  );
}

// ── FilterBar ─────────────────────────────────────────────────────────────────

export function FilterBar({ filters, onChange, boards }: FilterBarProps) {
  const [popover, setPopover] = useState<PopoverView | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node))
        setPopover(null);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const update = (patch: Partial<FilterState>) => onChange({ ...filters, ...patch });

  const toggle = <T extends string>(arr: T[], val: T): T[] =>
    arr.includes(val) ? arr.filter(x => x !== val) : [...arr, val];

  const getBoardName = (id: string) => boards.find(b => b.id === id)?.name ?? boards.find(b => b.id === id)?.url ?? id;

  const hasFilters =
    filters.boardIds.length > 0 ||
    filters.statuses.length > 0 ||
    filters.locations.length > 0 ||
    filters.companies.length > 0 ||
    filters.minScore !== undefined;

  const MENU_ITEMS: { key: PopoverView; label: string; icon: React.ReactNode }[] = [
    {
      key: 'board',
      label: 'Board',
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4 text-gray-400">
          <path d="M6.32 2.577a49.255 49.255 0 0 1 11.36 0c1.497.174 2.57 1.46 2.57 2.93V21a.75.75 0 0 1-1.085.67L12 18.089l-7.165 3.583A.75.75 0 0 1 3.75 21V5.507c0-1.47 1.073-2.756 2.57-2.93Z" />
        </svg>
      ),
    },
    {
      key: 'status',
      label: 'Status',
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
          <path fillRule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z" clipRule="evenodd" />
        </svg>
      ),
    },
    {
      key: 'location',
      label: 'Location',
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4 text-gray-400">
          <path fillRule="evenodd" d="m11.54 22.351.07.04.028.016a.76.76 0 0 0 .723 0l.028-.015.071-.041a16.975 16.975 0 0 0 1.144-.742 19.58 19.58 0 0 0 2.683-2.282c1.944-2.013 3.5-4.608 3.5-8.077a8.01 8.01 0 0 0-2.344-5.657 8.014 8.014 0 0 0-5.656-2.343 8.014 8.014 0 0 0-5.656 2.343A8.01 8.01 0 0 0 3.25 13.25c0 3.469 1.556 6.064 3.5 8.077a19.58 19.58 0 0 0 2.682 2.282 16.975 16.975 0 0 0 1.144.742ZM12 15.75a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" clipRule="evenodd" />
        </svg>
      ),
    },
    {
      key: 'company',
      label: 'Company',
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
          <path fillRule="evenodd" d="M4 16.5v-13h-.25a.75.75 0 0 1 0-1.5h12.5a.75.75 0 0 1 0 1.5H16v13h.25a.75.75 0 0 1 0 1.5h-3.5a.75.75 0 0 1-.75-.75v-2.5a.75.75 0 0 0-.75-.75h-2.5a.75.75 0 0 0-.75.75v2.5a.75.75 0 0 1-.75.75h-3.5a.75.75 0 0 1 0-1.5H4Zm3-11a.5.5 0 0 1 .5-.5h1a.5.5 0 0 1 .5.5v1a.5.5 0 0 1-.5.5h-1a.5.5 0 0 1-.5-.5v-1ZM7.5 9a.5.5 0 0 0-.5.5v1a.5.5 0 0 0 .5.5h1a.5.5 0 0 0 .5-.5v-1a.5.5 0 0 0-.5-.5h-1ZM11 5.5a.5.5 0 0 1 .5-.5h1a.5.5 0 0 1 .5.5v1a.5.5 0 0 1-.5.5h-1a.5.5 0 0 1-.5-.5v-1Zm.5 3.5a.5.5 0 0 0-.5.5v1a.5.5 0 0 0 .5.5h1a.5.5 0 0 0 .5-.5v-1a.5.5 0 0 0-.5-.5h-1Z" clipRule="evenodd" />
        </svg>
      ),
    },
    {
      key: 'score',
      label: 'Match Score',
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
          <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
        </svg>
      ),
    },
  ];

  return (
    <div className="relative" ref={containerRef}>
      <div className="flex flex-wrap items-center gap-2">
        {/* Active chips */}
        {filters.boardIds.map(id => (
          <Chip key={id} label={`Board: ${getBoardName(id)}`} onRemove={() => update({ boardIds: filters.boardIds.filter(x => x !== id) })} />
        ))}
        {filters.statuses.map(s => (
          <Chip key={s} label={s === 'active' ? 'Active' : 'Inactive'} onRemove={() => update({ statuses: filters.statuses.filter(x => x !== s) })} />
        ))}
        {filters.locations.map(l => (
          <Chip key={l} label={l} onRemove={() => update({ locations: filters.locations.filter(x => x !== l) })} />
        ))}
        {filters.companies.map(c => (
          <Chip key={c} label={c} onRemove={() => update({ companies: filters.companies.filter(x => x !== c) })} />
        ))}
        {filters.minScore !== undefined && (
          <Chip label={`Match ≥ ${filters.minScore}%`} onRemove={() => update({ minScore: undefined })} />
        )}

        {/* Add filter button */}
        <button
          onClick={() => setPopover(p => p === null ? 'menu' : null)}
          className="inline-flex items-center gap-1 rounded-full border border-dashed border-gray-300 px-3 py-1 text-xs font-medium text-gray-500 hover:border-indigo-400 hover:text-indigo-600 transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
            <path d="M8.75 3.75a.75.75 0 0 0-1.5 0v3.5h-3.5a.75.75 0 0 0 0 1.5h3.5v3.5a.75.75 0 0 0 1.5 0v-3.5h3.5a.75.75 0 0 0 0-1.5h-3.5v-3.5Z" />
          </svg>
          Add filter
        </button>

        {hasFilters && (
          <button
            onClick={() => onChange(EMPTY_FILTERS)}
            className="text-xs text-gray-400 hover:text-red-500 transition-colors"
          >
            Clear all
          </button>
        )}
      </div>

      {/* Popover */}
      {popover && (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setPopover(null)} />
          <div className="absolute top-full left-0 z-20 mt-2 rounded-lg border border-gray-200 bg-white shadow-lg overflow-hidden">

            {popover === 'menu' && (
              <div className="py-1 w-52">
                <p className="px-4 pt-2 pb-1 text-[11px] font-semibold text-gray-400 uppercase tracking-wide">Filter by</p>
                {MENU_ITEMS.map(({ key, label, icon }) => (
                  <button
                    key={key}
                    onClick={() => setPopover(key)}
                    className="flex items-center gap-3 w-full text-left px-4 py-2.5 text-sm text-gray-700 hover:bg-gray-50"
                  >
                    {icon}
                    {label}
                  </button>
                ))}
              </div>
            )}

            {popover === 'board' && (
              <div className="p-3 w-64">
                <BackButton onClick={() => setPopover('menu')} />
                <div className="max-h-56 overflow-y-auto space-y-0.5">
                  {boards.map(board => (
                    <CheckRow
                      key={board.id}
                      label={board.name ?? board.url}
                      checked={filters.boardIds.includes(board.id)}
                      onToggle={() => update({ boardIds: toggle(filters.boardIds, board.id) })}
                    />
                  ))}
                </div>
              </div>
            )}

            {popover === 'status' && (
              <div className="p-3 w-52">
                <BackButton onClick={() => setPopover('menu')} />
                <div className="space-y-2">
                  {([
                    { group: 'Visibility', items: [{ key: 'active', label: 'Active' }, { key: 'inactive', label: 'Inactive' }] },
                    { group: 'Read state',  items: [{ key: 'read',   label: 'Read'   }, { key: 'unread',   label: 'Unread'   }] },
                    { group: 'Pinned',      items: [{ key: 'pinned', label: 'Pinned' }, { key: 'unpinned', label: 'Unpinned' }] },
                  ] as { group: string; items: { key: StatusTag; label: string }[] }[]).map(({ group, items }) => (
                    <div key={group}>
                      <p className="px-2 mb-0.5 text-[10px] font-semibold text-gray-400 uppercase tracking-wide">{group}</p>
                      {items.map(({ key, label }) => (
                        <CheckRow
                          key={key}
                          label={label}
                          checked={filters.statuses.includes(key)}
                          onToggle={() => update({ statuses: toggle(filters.statuses, key) })}
                        />
                      ))}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {popover === 'location' && (
              <div>
                <div className="px-3 pt-3 pb-0">
                  <BackButton onClick={() => setPopover('menu')} />
                </div>
                <TypeaheadPicker
                  type="locations"
                  selected={filters.locations}
                  onToggle={l => update({ locations: toggle(filters.locations, l) })}
                />
              </div>
            )}

            {popover === 'company' && (
              <div>
                <div className="px-3 pt-3 pb-0">
                  <BackButton onClick={() => setPopover('menu')} />
                </div>
                <TypeaheadPicker
                  type="companies"
                  selected={filters.companies}
                  onToggle={c => update({ companies: toggle(filters.companies, c) })}
                />
              </div>
            )}

            {popover === 'score' && (
              <div>
                <div className="px-4 pt-3 pb-0">
                  <BackButton onClick={() => setPopover('menu')} />
                </div>
                <ScorePicker
                  value={filters.minScore}
                  onChange={v => update({ minScore: v })}
                />
              </div>
            )}

          </div>
        </>
      )}
    </div>
  );
}
