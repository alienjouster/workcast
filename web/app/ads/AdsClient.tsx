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
import { useState, useMemo, useRef } from 'react';
import type { JobAd } from '@/types';

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
  const trashAds = trashQuery.data?.pages.flatMap((p) => p.items) ?? [];

  const latestAds = useMemo(
    () => adsQuery.data?.pages.flatMap((p) => p.items) ?? [],
    [adsQuery.data]
  );

  const committedAdsRef = useRef<JobAd[] | null>(null);
  const committedIdsRef = useRef<Set<string>>(new Set());
  const prevEfRef = useRef(ef);
  const [, forceUpdate] = useState(0);

  // Reset the committed snapshot whenever filter params change (synchronous, safe to do in render).
  if (prevEfRef.current !== ef) {
    prevEfRef.current = ef;
    committedAdsRef.current = null;
    committedIdsRef.current = new Set();
  }

  // Detect new ads synchronously during render (no useEffect lag).
  // We only inspect the first page: scrapes add new ads there, pagination appends new pages.
  // Set-membership avoids false negatives from pinned ads or non-scrapedAt sort orders.
  let pendingNewCount = 0;
  if (!adsQuery.isLoading && !adsQuery.isError && latestAds.length > 0) {
    if (committedAdsRef.current === null) {
      committedAdsRef.current = latestAds;
      committedIdsRef.current = new Set(latestAds.map((a) => a.id));
    } else {
      const firstPageItems = adsQuery.data?.pages[0]?.items ?? [];
      const newInFirstPage = firstPageItems.filter((a) => !committedIdsRef.current.has(a.id));
      if (newInFirstPage.length > 0) {
        pendingNewCount = newInFirstPage.length;
        // Keep the committed view current (deletions, scoring) while holding new ads back.
        // committedIdsRef is NOT updated so the new ads stay excluded.
        committedAdsRef.current = latestAds.filter((a) => committedIdsRef.current.has(a.id));
      } else {
        committedAdsRef.current = latestAds;
        committedIdsRef.current = new Set(latestAds.map((a) => a.id));
      }
    }
  }

  const displayedAds =
    pendingNewCount > 0 && committedAdsRef.current !== null ? committedAdsRef.current : latestAds;

  const handleRefreshNewAds = () => {
    committedAdsRef.current = latestAds;
    committedIdsRef.current = new Set(latestAds.map((a) => a.id));
    forceUpdate((n) => n + 1);
  };

  const handleLoadMore = () => {
    committedAdsRef.current = latestAds;
    committedIdsRef.current = new Set(latestAds.map((a) => a.id));
    forceUpdate((n) => n + 1);
    adsQuery.fetchNextPage();
  };

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
          {trashAds.length === 0 ? (
            <EmptyState
              title="Trash bin is empty"
              description={hasActiveFilters(trashFilters) ? 'No trashed ads match your current filters.' : 'No ads have been trashed.'}
            />
          ) : (
            <TrashTable ads={trashAds} />
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
      ) : displayedAds.length === 0 ? (
        <EmptyState
          title="No ads found"
          description="No job ads match your current filters. Try adjusting your search."
        />
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          <AdTable ads={displayedAds} pendingNewCount={pendingNewCount} onRefreshNewAds={handleRefreshNewAds} />
          {adsQuery.hasNextPage && (
            <div className="px-4 py-4 border-t border-gray-200 flex justify-center">
              <Button
                variant="secondary"
                onClick={handleLoadMore}
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
