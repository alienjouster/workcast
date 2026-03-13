'use client';

import { useParams } from 'next/navigation';
import Link from 'next/link';
import { useScrapeRun } from '@/lib/hooks/useScrapeRuns';
import { Badge } from '@/components/ui/Badge';
import { Card, CardHeader, CardBody } from '@/components/ui/Card';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';

export default function RunDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data: run, isLoading, error } = useScrapeRun(id);

  if (isLoading) return <LoadingSpinner />;
  if (error || !run) {
    return (
      <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
        {error?.message ?? 'Run not found'}
      </div>
    );
  }

  const duration = run.finishedAt
    ? `${((new Date(run.finishedAt).getTime() - new Date(run.startedAt).getTime()) / 1000).toFixed(0)}s`
    : 'In progress…';

  return (
    <div className="space-y-6">
      <div>
        <Link
          href={`/boards/${run.jobBoardId}`}
          className="text-sm text-indigo-600 hover:underline"
        >
          ← Back to board
        </Link>
        <div className="flex items-center gap-3 mt-2">
          <h1 className="text-2xl font-bold text-gray-900">Scrape Run</h1>
          <Badge status={run.status} />
        </div>
        <p className="text-sm text-gray-500 font-mono mt-1">{run.id}</p>
      </div>

      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Run Summary</h2>
        </CardHeader>
        <CardBody>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
            <div>
              <span className="text-xs text-gray-500">Started</span>
              <p className="font-medium">{new Date(run.startedAt).toLocaleString()}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Duration</span>
              <p className="font-medium">{duration}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Trigger</span>
              <p className="font-medium capitalize">{run.triggeredBy}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Pages Scraped</span>
              <p className="font-medium">{run.pagesScraped}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Ads Found</span>
              <p className="font-medium">{run.adsFound}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">New Ads</span>
              <p className="font-medium text-green-600">+{run.adsNew}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Errors</span>
              <p className={`font-medium ${run.errors.length > 0 ? 'text-red-600' : 'text-gray-600'}`}>
                {run.errors.length}
              </p>
            </div>
          </div>
        </CardBody>
      </Card>

      {run.errors.length > 0 && (
        <Card>
          <CardHeader>
            <h2 className="font-semibold text-gray-900">Error Log</h2>
          </CardHeader>
          <CardBody className="p-0">
            <div className="divide-y divide-gray-200">
              {run.errors.map((err, idx) => (
                <div key={idx} className="px-6 py-4">
                  <div className="flex items-start justify-between gap-4 mb-1">
                    <a
                      href={err.page}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-sm text-indigo-600 hover:underline truncate"
                    >
                      {err.page}
                    </a>
                    <span className="text-xs text-gray-400 shrink-0">
                      {new Date(err.timestamp).toLocaleTimeString()}
                    </span>
                  </div>
                  <p className="text-sm text-red-700 bg-red-50 rounded px-2 py-1 font-mono">
                    {err.message}
                  </p>
                </div>
              ))}
            </div>
          </CardBody>
        </Card>
      )}
    </div>
  );
}
