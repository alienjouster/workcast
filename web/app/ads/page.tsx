'use client';

import { useState } from 'react';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { useJobAds, useMarkAllRead } from '@/lib/hooks/useJobAds';
import { AdTable } from '@/components/ads/AdTable';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';

export default function AdsPage() {
  const [search, setSearch] = useState('');
  const [boardId, setBoardId] = useState<string | undefined>(undefined);
  const [isActive, setIsActive] = useState<boolean | undefined>(undefined);

  const { data: boards } = useJobBoards();
  const { data, isLoading, error, fetchNextPage, hasNextPage, isFetchingNextPage } =
    useJobAds({ boardId, search, isActive });
  const markAllRead = useMarkAllRead();

  const allAds = data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Job Ads</h1>
          <p className="text-sm text-gray-500 mt-1">Browse all scraped job ads across all boards</p>
        </div>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => markAllRead.mutate(boardId)}
          loading={markAllRead.isPending}
        >
          Mark all as read
        </Button>
      </div>

      {/* Filters */}
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

      {isLoading ? (
        <LoadingSpinner />
      ) : error ? (
        <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">{error.message}</div>
      ) : allAds.length === 0 ? (
        <EmptyState
          title="No ads found"
          description="No job ads match your current filters. Try adjusting your search."
        />
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          <AdTable ads={allAds} />
          {hasNextPage && (
            <div className="px-4 py-4 border-t border-gray-200 flex justify-center">
              <Button
                variant="secondary"
                onClick={() => fetchNextPage()}
                loading={isFetchingNextPage}
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
