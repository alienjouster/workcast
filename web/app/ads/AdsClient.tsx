'use client';

import { useState } from 'react';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { useJobAds, useMarkAllRead } from '@/lib/hooks/useJobAds';
import { useFilterState } from '@/lib/hooks/useFilterState';
import { AdTable } from '@/components/ads/AdTable';
import { TrashTable } from '@/components/ads/TrashTable';
import { FilterBar, hasActiveFilters, effectiveFilters, type FilterState } from '@/components/ads/FilterBar';
import { NewJobAdModal } from '@/components/ads/NewJobAdModal';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { BADGE_OVERFLOW } from '@/lib/constants';

type View = 'ads' | 'trash';

export function AdsClient() {
  const [view, setView] = useState<View>('ads');
  const [showNewAdModal, setShowNewAdModal] = useState(false);
  const [filters, setFilters] = useFilterState('workcast:ads-filters');
  const [trashFilters, setTrashFilters] = useFilterState('workcast:ads-trash-filters');

  const { data: boards = [] } = useJobBoards();

  // Derive bool filters from status tags: if both sides of a pair are selected (or neither), pass undefined
  const deriveFlag = (f: FilterState, trueTag: string, falseTag: string): boolean | undefined => {
    const t = f.statuses.includes(trueTag as never);
    const ff = f.statuses.includes(falseTag as never);
    return t === ff ? undefined : t;
  };

  const ef = effectiveFilters(filters);
  const etf = effectiveFilters(trashFilters);

  const adsQuery = useJobAds({
    boardIds: ef.boardIds,
    excludeBoardIds: ef.excludeBoardIds,
    titles: ef.titles,
    excludeTitles: ef.excludeTitles,
    locations: ef.locations,
    excludeLocations: ef.excludeLocations,
    companies: ef.companies,
    excludeCompanies: ef.excludeCompanies,
    isActive:  deriveFlag(ef, 'active',   'inactive'),
    isRead:    deriveFlag(ef, 'read',     'unread'),
    isPinned:  deriveFlag(ef, 'pinned',   'unpinned'),
    minScore: ef.minScore,
    trashed: false,
  });

  const trashQuery = useJobAds({
    boardIds: etf.boardIds,
    excludeBoardIds: etf.excludeBoardIds,
    titles: etf.titles,
    excludeTitles: etf.excludeTitles,
    locations: etf.locations,
    excludeLocations: etf.excludeLocations,
    companies: etf.companies,
    excludeCompanies: etf.excludeCompanies,
    isActive:  deriveFlag(etf, 'active',   'inactive'),
    isRead:    deriveFlag(etf, 'read',     'unread'),
    isPinned:  deriveFlag(etf, 'pinned',   'unpinned'),
    minScore: etf.minScore,
    trashed: true,
  }, { poll: false });

  // Only fetch a separate unfiltered total when filters are active — otherwise
  // adsQuery already returns the same data and a second request would be redundant.
  const adsFiltersActive = hasActiveFilters(filters);
  const trashFiltersActive = hasActiveFilters(trashFilters);
  const totalAdsQuery = useJobAds({ trashed: false }, { poll: false, enabled: adsFiltersActive });
  const totalTrashQuery = useJobAds({ trashed: true }, { poll: false, enabled: trashFiltersActive });
  const markAllRead = useMarkAllRead();

  // Scope mark-all-read to the single selected board if exactly one is active
  const markAllReadBoardId = filters.boardIds.length === 1 ? filters.boardIds[0] : undefined;

  const activeQuery = view === 'ads' ? adsQuery : trashQuery;
  const allAds = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
  // When no filters active, adsQuery already holds the unfiltered total.
  const totalAdsCount = adsFiltersActive
    ? (totalAdsQuery.data?.pages[0]?.totalCount ?? 0)
    : (adsQuery.data?.pages[0]?.totalCount ?? 0);
  const filteredAdsCount = adsQuery.data?.pages[0]?.totalCount ?? 0;
  const totalTrashCount = trashFiltersActive
    ? (totalTrashQuery.data?.pages[0]?.totalCount ?? 0)
    : (trashQuery.data?.pages[0]?.totalCount ?? 0);
  const filteredTrashCount = trashQuery.data?.pages[0]?.totalCount ?? 0;


  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Job Ads</h1>
          <p className="text-sm text-gray-500 mt-1">Browse all scraped job ads across all boards</p>
        </div>
        <div className="flex items-center gap-2">
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
          <Button
            variant="primary"
            size="sm"
            onClick={() => setShowNewAdModal(true)}
          >
            New Job Ad
          </Button>
        </div>
      </div>

      {/* Tab toggle */}
      <div className="flex items-center gap-1 mb-4 border-b border-gray-200">
        <button
          onClick={() => setView('ads')}
          className={`flex items-center gap-1.5 px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
            view === 'ads'
              ? 'border-indigo-600 text-indigo-600'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          Job Ads
          {totalAdsCount > 0 && (
            <span className="inline-flex items-center justify-center min-w-[1.25rem] h-4 px-1 rounded-full bg-gray-200 text-gray-600 text-xs font-medium">
              {totalAdsCount > BADGE_OVERFLOW ? `${BADGE_OVERFLOW}+` : totalAdsCount}
            </span>
          )}
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
          {totalTrashCount > 0 && (
            <span className="inline-flex items-center justify-center min-w-[1.25rem] h-4 px-1 rounded-full bg-gray-200 text-gray-600 text-xs font-medium">
              {totalTrashCount > BADGE_OVERFLOW ? `${BADGE_OVERFLOW}+` : totalTrashCount}
            </span>
          )}
        </button>
      </div>

      {/* Filters */}
      <div className="mb-4">
        {view === 'ads' && (
          <>
            <FilterBar filters={filters} onChange={setFilters} boards={boards} />
            {hasActiveFilters(filters) && !adsQuery.isLoading && (
              <p className="mt-2 text-sm text-gray-500">
                Displaying <span className="font-medium text-gray-700">{filteredAdsCount}</span> ads out of <span className="font-medium text-gray-700">{totalAdsCount}</span>
              </p>
            )}
          </>
        )}
        {view === 'trash' && (
          <>
            <FilterBar filters={trashFilters} onChange={setTrashFilters} boards={boards} />
            {hasActiveFilters(trashFilters) && !trashQuery.isLoading && (
              <p className="mt-2 text-sm text-gray-500">
                Displaying <span className="font-medium text-gray-700">{filteredTrashCount}</span> ads out of <span className="font-medium text-gray-700">{totalTrashCount}</span>
              </p>
            )}
          </>
        )}
      </div>

      {activeQuery.isLoading ? (
        <LoadingSpinner />
      ) : activeQuery.error ? (
        <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
          {(activeQuery.error as Error).message}
        </div>
      ) : view === 'trash' ? (
        <>
          {allAds.length === 0 ? (
            <EmptyState
              title="Trash bin is empty"
              description={hasActiveFilters(trashFilters) ? 'No trashed ads match your current filters.' : 'No ads have been trashed.'}
            />
          ) : (
            <TrashTable ads={allAds} />
          )}
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
      {showNewAdModal && (
        <NewJobAdModal onClose={() => setShowNewAdModal(false)} />
      )}
    </div>
  );
}
