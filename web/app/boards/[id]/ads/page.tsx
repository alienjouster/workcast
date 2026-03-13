'use client';

import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useState } from 'react';
import { useJobAds } from '@/lib/hooks/useJobAds';
import { AdTable } from '@/components/ads/AdTable';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';

export default function BoardAdsPage() {
  const { id } = useParams<{ id: string }>();
  const [search, setSearch] = useState('');
  const [isActive, setIsActive] = useState<boolean | undefined>(undefined);

  const { data, isLoading, error, fetchNextPage, hasNextPage, isFetchingNextPage } =
    useJobAds({ boardId: id, search, isActive });

  const allAds = data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div>
      <div className="mb-6">
        <Link href={`/boards/${id}`} className="text-sm text-indigo-600 hover:underline">
          ← Back to board
        </Link>
        <h1 className="text-2xl font-bold text-gray-900 mt-2">Job Ads</h1>
      </div>

      {/* Filters */}
      <div className="flex items-center gap-3 mb-4">
        <input
          type="text"
          placeholder="Search title, company, location…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 w-64"
        />
        <select
          value={isActive === undefined ? '' : String(isActive)}
          onChange={(e) =>
            setIsActive(e.target.value === '' ? undefined : e.target.value === 'true')
          }
          className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        >
          <option value="">All ads</option>
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
          description="No job ads match your current filters."
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
