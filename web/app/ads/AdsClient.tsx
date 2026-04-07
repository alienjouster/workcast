'use client';

import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { useJobAds, useMarkAllRead } from '@/lib/hooks/useJobAds';
import { useTabbedListState } from '@/lib/hooks/useTabbedListState';
import { hasActiveFilters, effectiveFilters, type FilterState } from '@/components/ads/FilterBar';
import { AdTable } from '@/components/ads/AdTable';
import { TrashTable } from '@/components/ads/TrashTable';
import { FilterBar } from '@/components/ads/FilterBar';
import { NewJobAdModal } from '@/components/ads/NewJobAdModal';
import { TabToggle, TrashTabIcon } from '@/components/ui/TabToggle';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { useState } from 'react';

type View = 'ads' | 'trash';

export function AdsClient() {
  const [showNewAdModal, setShowNewAdModal] = useState(false);
  const { view, setView, filters, setFilters, trashFilters, setTrashFilters } = useTabbedListState<View>(
    'ads',
    'workcast:ads-filters',
    'workcast:ads-trash-filters',
  );

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

  const adsFiltersActive = hasActiveFilters(filters);
  const trashFiltersActive = hasActiveFilters(trashFilters);
  const totalAdsQuery = useJobAds({ trashed: false }, { poll: false, enabled: adsFiltersActive });
  const totalTrashQuery = useJobAds({ trashed: true }, { poll: false, enabled: trashFiltersActive });
  const markAllRead = useMarkAllRead();

  const markAllReadBoardId = filters.boardIds.length === 1 ? filters.boardIds[0] : undefined;

  const activeQuery = view === 'ads' ? adsQuery : trashQuery;
  const allAds = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
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

      <TabToggle
        tabs={[
          { key: 'ads', label: 'Job Ads', count: totalAdsCount },
          { key: 'trash', label: 'Trash bin', count: totalTrashCount, icon: TrashTabIcon },
        ]}
        activeKey={view}
        onChange={(key) => setView(key as View)}
      />

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
