'use client';

import { useApplications } from '@/lib/hooks/useApplications';
import { useTabbedListState } from '@/lib/hooks/useTabbedListState';
import { FilterBar, hasActiveFilters, effectiveFilters } from '@/components/ads/FilterBar';
import { ApplicationTable } from '@/components/applications/ApplicationTable';
import { ApplicationTrashTable } from '@/components/applications/ApplicationTrashTable';
import { ApplicationStatsTab } from '@/components/applications/ApplicationStatsTab';
import { TabToggle, TrashTabIcon } from '@/components/ui/TabToggle';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { api } from '@/lib/api';

type View = 'applications' | 'trash' | 'stats';

export function ApplicationsClient() {
  const { view, setView, filters, setFilters, trashFilters, setTrashFilters } = useTabbedListState<View>(
    'applications',
    'workcast:applications-filters',
    'workcast:applications-trash-filters',
  );

  const activeFilters = view === 'applications' ? filters : trashFilters;
  const setActiveFilters = view === 'applications' ? setFilters : setTrashFilters;

  const ef = effectiveFilters(filters);
  const etf = effectiveFilters(trashFilters);

  const appsQuery = useApplications({
    titles: ef.titles,
    excludeTitles: ef.excludeTitles,
    locations: ef.locations,
    excludeLocations: ef.excludeLocations,
    companies: ef.companies,
    excludeCompanies: ef.excludeCompanies,
    minScore: ef.minScore,
    trashed: false,
  });

  const trashQuery = useApplications({
    titles: etf.titles,
    excludeTitles: etf.excludeTitles,
    locations: etf.locations,
    excludeLocations: etf.excludeLocations,
    companies: etf.companies,
    excludeCompanies: etf.excludeCompanies,
    minScore: etf.minScore,
    trashed: true,
  });

  const appsFiltered = hasActiveFilters(filters);
  const trashFiltered = hasActiveFilters(trashFilters);

  const totalAppsQuery = useApplications({ trashed: false }, { enabled: appsFiltered });
  const totalTrashQuery = useApplications({ trashed: true }, { enabled: trashFiltered });

  const activeQuery = view === 'trash' ? trashQuery : appsQuery;
  const allItems = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const filteredAppsCount = appsQuery.data?.pages[0]?.totalCount ?? 0;
  const totalAppsCount = appsFiltered ? (totalAppsQuery.data?.pages[0]?.totalCount ?? 0) : filteredAppsCount;
  const filteredTrashCount = trashQuery.data?.pages[0]?.totalCount ?? 0;
  const totalTrashCount = trashFiltered ? (totalTrashQuery.data?.pages[0]?.totalCount ?? 0) : filteredTrashCount;

  const suggestionFetchers = {
    titles:    (q: string) => api.applications.distinctTitles(q),
    locations: (q: string) => api.applications.distinctLocations(q),
    companies: (q: string) => api.applications.distinctCompanies(q),
  };

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Applications</h1>
          <p className="text-sm text-gray-500 mt-1">Track your job application files</p>
        </div>
      </div>

      <TabToggle
        tabs={[
          { key: 'applications', label: 'Applications', count: totalAppsCount },
          { key: 'trash', label: 'Trash bin', count: totalTrashCount, icon: TrashTabIcon },
          { key: 'stats', label: 'Stats' },
        ]}
        activeKey={view}
        onChange={(key) => setView(key as View)}
      />

      {/* Filters — hidden on the Stats tab */}
      {view !== 'stats' && (
        <div className="mb-4">
          <FilterBar
            filters={activeFilters}
            onChange={setActiveFilters}
            features={['title', 'location', 'company', 'score']}
            suggestionFetchers={suggestionFetchers}
          />
          {view === 'applications' && hasActiveFilters(filters) && !appsQuery.isLoading && (
            <p className="mt-2 text-sm text-gray-500">
              Displaying <span className="font-medium text-gray-700">{filteredAppsCount}</span> applications out of <span className="font-medium text-gray-700">{totalAppsCount}</span>
            </p>
          )}
          {view === 'trash' && hasActiveFilters(trashFilters) && !trashQuery.isLoading && (
            <p className="mt-2 text-sm text-gray-500">
              Displaying <span className="font-medium text-gray-700">{filteredTrashCount}</span> applications out of <span className="font-medium text-gray-700">{totalTrashCount}</span>
            </p>
          )}
        </div>
      )}

      {view === 'stats' ? (
        <ApplicationStatsTab />
      ) : activeQuery.isLoading ? (
        <LoadingSpinner />
      ) : activeQuery.error ? (
        <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
          {(activeQuery.error as Error).message}
        </div>
      ) : view === 'trash' ? (
        <>
          {allItems.length === 0 ? (
            <EmptyState
              title="Trash bin is empty"
              description={hasActiveFilters(trashFilters) ? 'No trashed applications match your current filters.' : 'No applications have been trashed.'}
            />
          ) : (
            <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
              <ApplicationTrashTable applications={allItems} />
            </div>
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
      ) : allItems.length === 0 ? (
        <EmptyState
          title={hasActiveFilters(filters) ? 'No applications match your filters' : 'No applications yet'}
          description={hasActiveFilters(filters) ? 'Try adjusting your filters.' : 'Open a job ad and click "Apply to this job" to start tracking your application.'}
        />
      ) : (
        <>
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm">
            <ApplicationTable applications={allItems} />
          </div>
          {appsQuery.hasNextPage && (
            <div className="px-4 py-4 flex justify-center">
              <Button
                variant="secondary"
                onClick={() => appsQuery.fetchNextPage()}
                loading={appsQuery.isFetchingNextPage}
              >
                Load more
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
