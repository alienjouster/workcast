'use client';

import React, { useState, useRef, useEffect, useMemo, useCallback } from 'react';
import { api } from '@/lib/api';
import { Tooltip } from '@/components/ui/Tooltip';
import type {
  FilterState,
  FilterFeature,
  StatusTag,
  SuggestionFetchers,
  FilterBarProps,
  PopoverView,
  TriState,
} from './types';
import { Chip } from './Chip';
import { CheckRow } from './CheckRow';
import { TriStateCheckRow } from './TriStateCheckRow';
import { BackButton } from './BackButton';
import { TypeaheadPicker } from './TypeaheadPicker';
import { ScorePicker } from './ScorePicker';

// ── Public re-exports ─────────────────────────────────────────────────────────

export type { StatusTag, FilterFeature, FilterState, SuggestionFetchers } from './types';

// ── Helpers & constants ───────────────────────────────────────────────────────

export const EMPTY_FILTERS: FilterState = {
  boardIds: [],
  excludeBoardIds: [],
  titles: [],
  excludeTitles: [],
  statuses: [],
  locations: [],
  excludeLocations: [],
  companies: [],
  excludeCompanies: [],
  minScore: undefined,
  enabled: true,
};

/** True when the state has at least one active filter dimension (ignoring enabled flag). */
export function hasActiveFilters(f: FilterState): boolean {
  return (
    f.boardIds.length > 0 ||
    f.excludeBoardIds.length > 0 ||
    f.titles.length > 0 ||
    f.excludeTitles.length > 0 ||
    f.statuses.length > 0 ||
    f.locations.length > 0 ||
    f.excludeLocations.length > 0 ||
    f.companies.length > 0 ||
    f.excludeCompanies.length > 0 ||
    f.minScore !== undefined
  );
}

/**
 * Returns the filter state to pass to queries.
 * When filters are disabled, returns EMPTY_FILTERS so no filtering is applied.
 */
export function effectiveFilters(f: FilterState): FilterState {
  return f.enabled ? f : EMPTY_FILTERS;
}

const ALL_FEATURES: FilterFeature[] = ['board', 'status', 'title', 'location', 'company', 'score'];

function triCycle(
  included: string[],
  excluded: string[],
  val: string,
): { included: string[]; excluded: string[] } {
  if (included.includes(val))
    return { included: included.filter(x => x !== val), excluded: [...excluded, val] };
  if (excluded.includes(val))
    return { included, excluded: excluded.filter(x => x !== val) };
  return { included: [...included, val], excluded };
}

const ALL_MENU_ITEMS: { key: PopoverView; label: string; feature: FilterFeature; icon: React.ReactNode }[] = [
  {
    key: 'title', feature: 'title', label: 'Title',
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
        <path fillRule="evenodd" d="M2 4.75A.75.75 0 0 1 2.75 4h14.5a.75.75 0 0 1 0 1.5H2.75A.75.75 0 0 1 2 4.75ZM2 10a.75.75 0 0 1 .75-.75h14.5a.75.75 0 0 1 0 1.5H2.75A.75.75 0 0 1 2 10Zm0 5.25a.75.75 0 0 1 .75-.75h9.5a.75.75 0 0 1 0 1.5h-9.5A.75.75 0 0 1 2 15.25Z" clipRule="evenodd" />
      </svg>
    ),
  },
  {
    key: 'board', feature: 'board', label: 'Board',
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4 text-gray-400">
        <path d="M6.32 2.577a49.255 49.255 0 0 1 11.36 0c1.497.174 2.57 1.46 2.57 2.93V21a.75.75 0 0 1-1.085.67L12 18.089l-7.165 3.583A.75.75 0 0 1 3.75 21V5.507c0-1.47 1.073-2.756 2.57-2.93Z" />
      </svg>
    ),
  },
  {
    key: 'status', feature: 'status', label: 'Status',
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
        <path fillRule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z" clipRule="evenodd" />
      </svg>
    ),
  },
  {
    key: 'location', feature: 'location', label: 'Location',
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4 text-gray-400">
        <path fillRule="evenodd" d="m11.54 22.351.07.04.028.016a.76.76 0 0 0 .723 0l.028-.015.071-.041a16.975 16.975 0 0 0 1.144-.742 19.58 19.58 0 0 0 2.683-2.282c1.944-2.013 3.5-4.608 3.5-8.077a8.01 8.01 0 0 0-2.344-5.657 8.014 8.014 0 0 0-5.656-2.343 8.014 8.014 0 0 0-5.656 2.343A8.01 8.01 0 0 0 3.25 13.25c0 3.469 1.556 6.064 3.5 8.077a19.58 19.58 0 0 0 2.682 2.282 16.975 16.975 0 0 0 1.144.742ZM12 15.75a3 3 0 1 0 0-6 3 3 0 0 0 0 6Z" clipRule="evenodd" />
      </svg>
    ),
  },
  {
    key: 'company', feature: 'company', label: 'Company',
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
        <path fillRule="evenodd" d="M4 16.5v-13h-.25a.75.75 0 0 1 0-1.5h12.5a.75.75 0 0 1 0 1.5H16v13h.25a.75.75 0 0 1 0 1.5h-3.5a.75.75 0 0 1-.75-.75v-2.5a.75.75 0 0 0-.75-.75h-2.5a.75.75 0 0 0-.75.75v2.5a.75.75 0 0 1-.75.75h-3.5a.75.75 0 0 1 0-1.5H4Zm3-11a.5.5 0 0 1 .5-.5h1a.5.5 0 0 1 .5.5v1a.5.5 0 0 1-.5.5h-1a.5.5 0 0 1-.5-.5v-1ZM7.5 9a.5.5 0 0 0-.5.5v1a.5.5 0 0 0 .5.5h1a.5.5 0 0 0 .5-.5v-1a.5.5 0 0 0-.5-.5h-1ZM11 5.5a.5.5 0 0 1 .5-.5h1a.5.5 0 0 1 .5.5v1a.5.5 0 0 1-.5.5h-1a.5.5 0 0 1-.5-.5v-1Zm.5 3.5a.5.5 0 0 0-.5.5v1a.5.5 0 0 0 .5.5h1a.5.5 0 0 0 .5-.5v-1a.5.5 0 0 0-.5-.5h-1Z" clipRule="evenodd" />
      </svg>
    ),
  },
  {
    key: 'score', feature: 'score', label: 'Match Score',
    icon: (
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 text-gray-400">
        <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
      </svg>
    ),
  },
];

// ── FilterBar ─────────────────────────────────────────────────────────────────

export function FilterBar({
  filters,
  onChange,
  features = ALL_FEATURES,
  boards = [],
  suggestionFetchers,
}: FilterBarProps) {
  const [popover, setPopover] = useState<PopoverView | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);

  const fetchers: Required<SuggestionFetchers> = useMemo(() => ({
    titles:    suggestionFetchers?.titles    ?? ((q) => api.ads.distinctTitles(q)),
    locations: suggestionFetchers?.locations ?? ((q) => api.ads.distinctLocations(q)),
    companies: suggestionFetchers?.companies ?? ((q) => api.ads.distinctCompanies(q)),
  }), [suggestionFetchers]);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node))
        setPopover(null);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const update = useCallback(
    (patch: Partial<FilterState>) => onChange({ ...filters, ...patch }),
    [filters, onChange]
  );

  const toggle = <T extends string>(arr: T[], val: T): T[] =>
    arr.includes(val) ? arr.filter(x => x !== val) : [...arr, val];

  const getBoardName = useCallback(
    (id: string) => boards.find(b => b.id === id)?.name ?? boards.find(b => b.id === id)?.url ?? id,
    [boards]
  );

  const active = hasActiveFilters(filters);

  const MENU_ITEMS = useMemo(
    () => ALL_MENU_ITEMS.filter(item => features.includes(item.feature)),
    [features]
  );

  return (
    <div className="relative" ref={containerRef}>
      <div className="flex flex-wrap items-center gap-2">

        {/* 1. Label */}
        <span className="text-xs font-medium text-gray-500 shrink-0">Filters</span>

        {/* 2. Toggle slider — only when there are active filters */}
        {active && (
          <Tooltip content={filters.enabled ? 'Disable all filters' : 'Enable all filters'} position="top" wrapperAs="span">
            <button
              role="switch"
              aria-checked={filters.enabled}
              onClick={() => onChange({ ...filters, enabled: !filters.enabled })}
              className="relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500"
              style={{ backgroundColor: filters.enabled ? '#6366f1' : '#d1d5db' }}
            >
              <span
                className={`pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow-sm ring-0 transition-transform duration-200 ${filters.enabled ? 'translate-x-4' : 'translate-x-0'}`}
              />
            </button>
          </Tooltip>
        )}

        {/* 3. Add filter button */}
        <button
          onClick={() => setPopover(p => p === null ? 'menu' : null)}
          className="inline-flex items-center gap-1 rounded-full border border-dashed border-gray-300 px-3 py-1 text-xs font-medium text-gray-500 hover:border-indigo-400 hover:text-indigo-600 transition-colors shrink-0"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
            <path d="M8.75 3.75a.75.75 0 0 0-1.5 0v3.5h-3.5a.75.75 0 0 0 0 1.5h3.5v3.5a.75.75 0 0 0 1.5 0v-3.5h3.5a.75.75 0 0 0 0-1.5h-3.5v-3.5Z" />
          </svg>
          Add filter
        </button>

        {/* 4. Chips — dimmed when filters are disabled */}
        <div className={`flex flex-wrap items-center gap-2 ${!filters.enabled && active ? 'opacity-50 pointer-events-none' : ''}`}>

          {/* Title chips */}
          {features.includes('title') && [
            ...filters.titles.map(t => ({ val: t, exclude: false })),
            ...filters.excludeTitles.map(t => ({ val: t, exclude: true })),
          ].sort((a, b) => a.val.localeCompare(b.val)).map(({ val, exclude }) => (
            <Chip
              key={`t-${val}`}
              label={val}
              variant={exclude ? 'exclude' : 'include'}
              onToggleVariant={() => exclude
                ? update({ excludeTitles: filters.excludeTitles.filter(x => x !== val), titles: [...filters.titles, val] })
                : update({ titles: filters.titles.filter(x => x !== val), excludeTitles: [...filters.excludeTitles, val] })
              }
              onRemove={() => exclude
                ? update({ excludeTitles: filters.excludeTitles.filter(x => x !== val) })
                : update({ titles: filters.titles.filter(x => x !== val) })
              }
            />
          ))}

          {/* Board chips */}
          {features.includes('board') && [
            ...filters.boardIds.map(id => ({ val: id, exclude: false, label: `Board: ${getBoardName(id)}` })),
            ...filters.excludeBoardIds.map(id => ({ val: id, exclude: true, label: `Board: ${getBoardName(id)}` })),
          ].sort((a, b) => a.label.localeCompare(b.label)).map(({ val, exclude, label }) => (
            <Chip
              key={`b-${val}`}
              label={label}
              variant={exclude ? 'exclude' : 'include'}
              onToggleVariant={() => exclude
                ? update({ excludeBoardIds: filters.excludeBoardIds.filter(x => x !== val), boardIds: [...filters.boardIds, val] })
                : update({ boardIds: filters.boardIds.filter(x => x !== val), excludeBoardIds: [...filters.excludeBoardIds, val] })
              }
              onRemove={() => exclude
                ? update({ excludeBoardIds: filters.excludeBoardIds.filter(x => x !== val) })
                : update({ boardIds: filters.boardIds.filter(x => x !== val) })
              }
            />
          ))}

          {/* Status chips */}
          {features.includes('status') && filters.statuses.map(s => (
            <Chip
              key={s}
              label={s === 'active' ? 'Active' : s === 'inactive' ? 'Inactive' : s === 'read' ? 'Read' : s === 'unread' ? 'Unread' : s === 'pinned' ? 'Pinned' : 'Unpinned'}
              onRemove={() => update({ statuses: filters.statuses.filter(x => x !== s) })}
            />
          ))}

          {/* Location chips */}
          {features.includes('location') && [
            ...filters.locations.map(l => ({ val: l, exclude: false })),
            ...filters.excludeLocations.map(l => ({ val: l, exclude: true })),
          ].sort((a, b) => a.val.localeCompare(b.val)).map(({ val, exclude }) => (
            <Chip
              key={`l-${val}`}
              label={val}
              variant={exclude ? 'exclude' : 'include'}
              onToggleVariant={() => exclude
                ? update({ excludeLocations: filters.excludeLocations.filter(x => x !== val), locations: [...filters.locations, val] })
                : update({ locations: filters.locations.filter(x => x !== val), excludeLocations: [...filters.excludeLocations, val] })
              }
              onRemove={() => exclude
                ? update({ excludeLocations: filters.excludeLocations.filter(x => x !== val) })
                : update({ locations: filters.locations.filter(x => x !== val) })
              }
            />
          ))}

          {/* Company chips */}
          {features.includes('company') && [
            ...filters.companies.map(c => ({ val: c, exclude: false })),
            ...filters.excludeCompanies.map(c => ({ val: c, exclude: true })),
          ].sort((a, b) => a.val.localeCompare(b.val)).map(({ val, exclude }) => (
            <Chip
              key={`c-${val}`}
              label={val}
              variant={exclude ? 'exclude' : 'include'}
              onToggleVariant={() => exclude
                ? update({ excludeCompanies: filters.excludeCompanies.filter(x => x !== val), companies: [...filters.companies, val] })
                : update({ companies: filters.companies.filter(x => x !== val), excludeCompanies: [...filters.excludeCompanies, val] })
              }
              onRemove={() => exclude
                ? update({ excludeCompanies: filters.excludeCompanies.filter(x => x !== val) })
                : update({ companies: filters.companies.filter(x => x !== val) })
              }
            />
          ))}

          {/* Score chip */}
          {features.includes('score') && filters.minScore !== undefined && (
            <Chip label={`Match ≥ ${filters.minScore}%`} onRemove={() => update({ minScore: undefined })} />
          )}

        </div>{/* end chips wrapper */}

        {/* 5. Clear all */}
        {active && (
          <button
            onClick={() => onChange(EMPTY_FILTERS)}
            className="text-xs text-gray-400 hover:text-red-500 transition-colors shrink-0"
          >
            Clear all
          </button>
        )}
      </div>{/* end outer flex wrapper */}

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

            {popover === 'title' && (
              <div>
                <div className="px-3 pt-3 pb-0">
                  <BackButton onClick={() => setPopover('menu')} />
                </div>
                <TypeaheadPicker
                  cacheKey="titles"
                  placeholder="Search titles…"
                  fetchFn={fetchers.titles}
                  included={filters.titles}
                  excluded={filters.excludeTitles}
                  onCycle={val => {
                    const { included, excluded } = triCycle(filters.titles, filters.excludeTitles, val);
                    update({ titles: included, excludeTitles: excluded });
                  }}
                  onExclude={val => {
                    update({
                      titles: filters.titles.filter(x => x !== val),
                      excludeTitles: filters.excludeTitles.includes(val) ? filters.excludeTitles : [...filters.excludeTitles, val],
                    });
                  }}
                />
              </div>
            )}

            {popover === 'board' && features.includes('board') && (
              <div className="p-3 w-64">
                <BackButton onClick={() => setPopover('menu')} />
                <p className="text-[10px] text-gray-400 mb-1.5 px-1">Click to include · click again to exclude · once more to clear</p>
                <div className="max-h-56 overflow-y-auto space-y-0.5">
                  {boards.map(board => {
                    const state: TriState = filters.boardIds.includes(board.id)
                      ? 'include'
                      : filters.excludeBoardIds.includes(board.id)
                      ? 'exclude'
                      : 'none';
                    return (
                      <TriStateCheckRow
                        key={board.id}
                        label={board.name ?? board.url}
                        state={state}
                        onCycle={() => {
                          const { included, excluded } = triCycle(filters.boardIds, filters.excludeBoardIds, board.id);
                          update({ boardIds: included, excludeBoardIds: excluded });
                        }}
                      />
                    );
                  })}
                </div>
              </div>
            )}

            {popover === 'status' && features.includes('status') && (
              <div className="p-3 w-52">
                <BackButton onClick={() => setPopover('menu')} />
                <div className="space-y-2">
                  {([
                    { group: 'Visibility', items: [{ key: 'active' as StatusTag, label: 'Active' }, { key: 'inactive' as StatusTag, label: 'Inactive' }] },
                    { group: 'Read state',  items: [{ key: 'read' as StatusTag,   label: 'Read'   }, { key: 'unread' as StatusTag,   label: 'Unread'   }] },
                    { group: 'Pinned',      items: [{ key: 'pinned' as StatusTag, label: 'Pinned' }, { key: 'unpinned' as StatusTag, label: 'Unpinned' }] },
                  ]).map(({ group, items }) => (
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
                  cacheKey="locations"
                  placeholder="Search locations…"
                  fetchFn={fetchers.locations}
                  included={filters.locations}
                  excluded={filters.excludeLocations}
                  onCycle={val => {
                    const { included, excluded } = triCycle(filters.locations, filters.excludeLocations, val);
                    update({ locations: included, excludeLocations: excluded });
                  }}
                  onExclude={val => {
                    update({
                      locations: filters.locations.filter(x => x !== val),
                      excludeLocations: filters.excludeLocations.includes(val) ? filters.excludeLocations : [...filters.excludeLocations, val],
                    });
                  }}
                />
              </div>
            )}

            {popover === 'company' && (
              <div>
                <div className="px-3 pt-3 pb-0">
                  <BackButton onClick={() => setPopover('menu')} />
                </div>
                <TypeaheadPicker
                  cacheKey="companies"
                  placeholder="Search companies…"
                  fetchFn={fetchers.companies}
                  included={filters.companies}
                  excluded={filters.excludeCompanies}
                  onCycle={val => {
                    const { included, excluded } = triCycle(filters.companies, filters.excludeCompanies, val);
                    update({ companies: included, excludeCompanies: excluded });
                  }}
                  onExclude={val => {
                    update({
                      companies: filters.companies.filter(x => x !== val),
                      excludeCompanies: filters.excludeCompanies.includes(val) ? filters.excludeCompanies : [...filters.excludeCompanies, val],
                    });
                  }}
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
