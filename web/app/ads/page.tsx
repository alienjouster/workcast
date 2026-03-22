'use client';

import { useState } from 'react';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { useJobAds, useMarkAllRead } from '@/lib/hooks/useJobAds';
import { AdTable } from '@/components/ads/AdTable';
import { TrashTable } from '@/components/ads/TrashTable';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';

type View = 'ads' | 'trash';

export default function AdsPage() {
  const [view, setView] = useState<View>('ads');
  const [search, setSearch] = useState('');
  const [boardId, setBoardId] = useState<string | undefined>(undefined);
  const [isActive, setIsActive] = useState<boolean | undefined>(undefined);

  const { data: boards } = useJobBoards();
  const adsQuery = useJobAds({ boardId, search, isActive, trashed: false });
  const trashQuery = useJobAds({ trashed: true });
  const markAllRead = useMarkAllRead();

  const activeQuery = view === 'ads' ? adsQuery : trashQuery;
  const allAds = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const trashCount = trashQuery.data?.pages.flatMap((p) => p.items).length ?? 0;

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
            onClick={() => markAllRead.mutate(boardId)}
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
              {trashCount}
            </span>
          )}
        </button>
      </div>

      {/* Filters — only in ads view */}
      {view === 'ads' && (
        <div className="flex flex-wrap items-center gap-3 mb-4">
          <input
            type="text"
            placeholder="Search title, company, location…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 w-64"
          />
          <select
            value={boardId ?? ''}
            onChange={(e) => setBoardId(e.target.value || undefined)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="">All boards</option>
            {boards?.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name ?? b.url}
              </option>
            ))}
          </select>
          <select
            value={isActive === undefined ? '' : String(isActive)}
            onChange={(e) =>
              setIsActive(e.target.value === '' ? undefined : e.target.value === 'true')
            }
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="">All status</option>
            <option value="true">Active only</option>
            <option value="false">Inactive only</option>
          </select>
        </div>
      )}

      {activeQuery.isLoading ? (
        <LoadingSpinner />
      ) : activeQuery.error ? (
        <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
          {(activeQuery.error as Error).message}
        </div>
      ) : view === 'trash' ? (
        <TrashTable ads={allAds} />
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
