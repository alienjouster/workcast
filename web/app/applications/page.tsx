'use client';

import { useState } from 'react';
import { useApplications } from '@/lib/hooks/useApplications';
import { ApplicationTable } from '@/components/applications/ApplicationTable';
import { ApplicationTrashTable } from '@/components/applications/ApplicationTrashTable';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';

type View = 'applications' | 'trash';

export default function ApplicationsPage() {
  const [view, setView] = useState<View>('applications');

  const appsQuery = useApplications({ trashed: false });
  const trashQuery = useApplications({ trashed: true });
  const totalAppsQuery = useApplications({ trashed: false });
  const totalTrashQuery = useApplications({ trashed: true });

  const activeQuery = view === 'applications' ? appsQuery : trashQuery;
  const allItems = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const totalAppsCount = totalAppsQuery.data?.pages[0]?.totalCount ?? 0;
  const totalTrashCount = totalTrashQuery.data?.pages[0]?.totalCount ?? 0;

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Applications</h1>
          <p className="text-sm text-gray-500 mt-1">Track your job application files</p>
        </div>
      </div>

      {/* Tab toggle */}
      <div className="flex items-center gap-1 mb-6 border-b border-gray-200">
        <button
          onClick={() => setView('applications')}
          className={`flex items-center gap-1.5 px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
            view === 'applications'
              ? 'border-indigo-600 text-indigo-600'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          Applications
          {totalAppsCount > 0 && (
            <span className="inline-flex items-center justify-center min-w-[1.25rem] h-4 px-1 rounded-full bg-gray-200 text-gray-600 text-xs font-medium">
              {totalAppsCount > 999 ? '999+' : totalAppsCount}
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
              {totalTrashCount > 999 ? '999+' : totalTrashCount}
            </span>
          )}
        </button>
      </div>

      {activeQuery.isLoading ? (
        <LoadingSpinner />
      ) : activeQuery.error ? (
        <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
          {(activeQuery.error as Error).message}
        </div>
      ) : view === 'trash' ? (
        <>
          {allItems.length === 0 ? (
            <EmptyState title="Trash bin is empty" description="No applications have been trashed." />
          ) : (
            <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
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
          title="No applications yet"
          description='Open a job ad and click "Apply to this job" to start tracking your application.'
        />
      ) : (
        <>
          <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
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
