'use client';

import { useState } from 'react';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { useJobAds, useMarkAllRead } from '@/lib/hooks/useJobAds';
import { AdTable } from '@/components/ads/AdTable';
import { TrashTable } from '@/components/ads/TrashTable';
import { FilterBar, FilterState, EMPTY_FILTERS } from '@/components/ads/FilterBar';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';

type View = 'ads' | 'trash';

export default function AdsPage() {
  const [view, setView] = useState<View>('ads');
  const [filters, setFilters] = useState<FilterState>(EMPTY_FILTERS);

  const { data: boards = [] } = useJobBoards();

  // Derive bool filters from status tags: if both sides of a pair are selected (or neither), pass undefined
  const deriveFlag = (trueTag: string, falseTag: string): boolean | undefined => {
    const t = filters.statuses.includes(trueTag as never);
    const f = filters.statuses.includes(falseTag as never);
    return t === f ? undefined : t;
  };

  const adsQuery = useJobAds({
    boardIds: filters.boardIds,
    locations: filters.locations,
    companies: filters.companies,
    isActive:  deriveFlag('active',   'inactive'),
    isRead:    deriveFlag('read',     'unread'),
    isPinned:  deriveFlag('pinned',   'unpinned'),
    minScore: filters.minScore,
    trashed: false,
  });
  const trashQuery = useJobAds({ trashed: true });
  const markAllRead = useMarkAllRead();

  // Scope mark-all-read to the single selected board if exactly one is active
  const markAllReadBoardId = filters.boardIds.length === 1 ? filters.boardIds[0] : undefined;

  const activeQuery = view === 'ads' ? adsQuery : trashQuery;
  const allAds = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const trashCount = trashQuery.data?.pages[0]?.totalCount ?? 0;

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Job Ads</h1>
          <p className="text-sm text-gray-500 mt-1">Browse all scraped job ads across all boards</p>
        </div>
        {view === 'ads' && (
          <Button
            variant="secondary"
            size="sm"
            onClick={() => markAllRead.mutate(markAllReadBoardId)}
            loading={markAllRead.isPending}
          >
            Mark all as read
          </Button>
        )}
      </div>

      {/* Tab toggle */}
      <div className="flex items-center gap-1 mb-4 border-b border-gray-200">
        <button
          onClick={() => setView('ads')}
          className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
            view === 'ads'
              ? 'border-indigo-600 text-indigo-600'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          Job Ads
        </button>
        <button
          onClick={() => setView('trash')}
          className={`flex items-center gap-1.5 px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
            view === 'trash'
              ? 'border-indigo-600 text-indigo-600'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
            <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z" clipRule="evenodd" />
          </svg>
          Trash bin
          {trashCount > 0 && (
            <span className="inline-flex items-center justify-center min-w-[1.25rem] h-4 px-1 rounded-full bg-gray-200 text-gray-600 text-xs font-medium">
              {trashCount > 999 ? '999+' : trashCount}
            </span>
          )}
        </button>
      </div>

      {/* Filters — only in ads view */}
      {view === 'ads' && (
        <div className="mb-4">
          <FilterBar filters={filters} onChange={setFilters} boards={boards} />
        </div>
      )}

      {activeQuery.isLoading ? (
        <LoadingSpinner />
      ) : activeQuery.error ? (
        <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
          {(activeQuery.error as Error).message}
        </div>
      ) : view === 'trash' ? (
        <>
          <TrashTable ads={allAds} />
          {trashQuery.hasNextPage && (
            <div className="px-4 py-4 flex justify-center">
              <Button
                variant="secondary"
                onClick={() => trashQuery.fetchNextPage()}
                loading={trashQuery.isFetchingNextPage}
              >
                Load more
              </Button>
            </div>
          )}
        </>
      ) : allAds.length === 0 ? (
        <EmptyState
          title="No ads found"
          description="No job ads match your current filters. Try adjusting your search."
        />
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          <AdTable ads={allAds} />
          {adsQuery.hasNextPage && (
            <div className="px-4 py-4 border-t border-gray-200 flex justify-center">
              <Button
                variant="secondary"
                onClick={() => adsQuery.fetchNextPage()}
                loading={adsQuery.isFetchingNextPage}
              >
                Load more
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
