'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { isActiveRunStatus } from '@/types';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { useAllScrapeRuns } from '@/lib/hooks/useScrapeRuns';
import { AddBoardForm } from '@/components/boards/AddBoardForm';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { Card, CardBody } from '@/components/ui/Card';
import { Tooltip } from '@/components/ui/Tooltip';

function timeAgo(iso: string) {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 60) return 'just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;
  return `${Math.floor(months / 12)}y ago`;
}

function runDuration(run: { startedAt: string; finishedAt: string | null }) {
  if (!run.finishedAt) return '—';
  const ms = new Date(run.finishedAt).getTime() - new Date(run.startedAt).getTime();
  return `${(ms / 1000).toFixed(0)}s`;
}

type Tab = 'boards' | 'runs';

export function BoardsClient() {
  const router = useRouter();
  const [activeTab, setActiveTab] = useState<Tab>('boards');
  const [showForm, setShowForm] = useState(false);
  const { data: boards, isLoading: boardsLoading, error: boardsError } = useJobBoards();
  const { data: runs, isLoading: runsLoading } = useAllScrapeRuns(100);

  const isLoading = boardsLoading || (activeTab === 'runs' && runsLoading);

  if (isLoading) return <LoadingSpinner />;
  if (boardsError) {
    return (
      <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
        Failed to load boards: {boardsError.message}
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Job Boards</h1>
          <p className="text-sm text-gray-500 mt-1">
            {boards?.length ?? 0} board{boards?.length !== 1 ? 's' : ''} registered
          </p>
        </div>
        {activeTab === 'boards' && (
          <Button variant="primary" onClick={() => setShowForm((v) => !v)}>
            {showForm ? 'Cancel' : '+ Add Board'}
          </Button>
        )}
      </div>

      {/* Tabs */}
      <div className="border-b border-gray-200 mb-6">
        <nav className="-mb-px flex gap-6">
          <button
            onClick={() => { setActiveTab('boards'); setShowForm(false); }}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'boards'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Boards
          </button>
          <button
            onClick={() => { setActiveTab('runs'); setShowForm(false); }}
            className={`pb-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === 'runs'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Recent Scrape Runs
          </button>
        </nav>
      </div>

      {/* Boards tab */}
      {activeTab === 'boards' && (
        <>
          {showForm && (
            <Card className="mb-6">
              <CardBody>
                <h2 className="text-base font-semibold text-gray-900 mb-4">Register New Board</h2>
                <AddBoardForm onClose={() => setShowForm(false)} />
              </CardBody>
            </Card>
          )}

          {!boards || boards.length === 0 ? (
            <EmptyState
              title="No boards registered"
              description="Register a job board URL and let Workcast handle the rest — AI-driven analysis, scraping, and job ad extraction."
              action={
                <Button variant="primary" onClick={() => setShowForm(true)}>
                  + Add Your First Board
                </Button>
              }
            />
          ) : (
            <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
              <table className="min-w-full divide-y divide-gray-200 text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Name</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Ads</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Last scraped</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Schedule</th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {boards.map((board) => (
                    <tr
                      key={board.id}
                      className="hover:bg-gray-50 cursor-pointer"
                      onClick={() => router.push(`/boards/${board.id}`)}
                    >
                      <td className="px-4 py-3">
                        <div className="font-medium text-gray-900">{board.name ?? board.url}</div>
                        {board.name && (
                          <Tooltip content={board.url} position="bottom" wrapperAs="span" wrap tooltipClassName="max-w-xs">
                            <div className="text-xs text-gray-400 truncate max-w-xs">{board.url}</div>
                          </Tooltip>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <Badge status={board.status} />
                      </td>
                      <td className="px-4 py-3 text-gray-700">
                        {board.adCount}
                      </td>
                      <td className="px-4 py-3 text-gray-500">
                        {board.hasActiveRun ? (
                          <span className="inline-flex items-center gap-1.5 text-indigo-600">
                            <svg className="w-3.5 h-3.5 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                            </svg>
                            Running
                          </span>
                        ) : board.lastScrapedAt ? (
                          <Tooltip content={new Date(board.lastScrapedAt).toLocaleString()} position="top" wrapperAs="span">
                            <span>{timeAgo(board.lastScrapedAt)}</span>
                          </Tooltip>
                        ) : (
                          <span className="italic">Never</span>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className="font-mono text-xs bg-gray-100 px-2 py-0.5 rounded">{board.scheduleCron}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}

      {/* Recent Scrape Runs tab */}
      {activeTab === 'runs' && (
        <>
          {!runs || runs.length === 0 ? (
            <EmptyState
              title="No scrape runs yet"
              description="Scrape runs will appear here once a board has been scraped."
            />
          ) : (
            <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
              <table className="min-w-full divide-y divide-gray-200 text-sm">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Board</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Started</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Duration</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Pages</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Ads Found</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">New</th>
                    <th className="px-4 py-3 text-left font-medium text-gray-500">Trigger</th>
                    <th className="px-4 py-3"></th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {runs.map((run) => (
                    <tr key={run.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3">
                        <button
                          onClick={() => router.push(`/boards/${run.jobBoardId}`)}
                          className="text-indigo-600 hover:underline text-sm font-medium text-left"
                        >
                          {run.boardName ?? run.jobBoardId}
                        </button>
                      </td>
                      <td className="px-4 py-3"><Badge status={run.status} /></td>
                      <td className="px-4 py-3 text-gray-600">
                        <Tooltip content={new Date(run.startedAt).toLocaleString()} position="top" wrapperAs="span">
                          <span className="cursor-default">{timeAgo(run.startedAt)}</span>
                        </Tooltip>
                      </td>
                      <td className="px-4 py-3 text-gray-600">
                        {isActiveRunStatus(run.status) ? (
                          <span className="inline-flex items-center gap-1.5">
                            <svg className="animate-spin h-3.5 w-3.5 text-indigo-500" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
                              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                            </svg>
                            {run.status === 'processing' ? 'Running…' : `${run.status.charAt(0).toUpperCase() + run.status.slice(1)}…`}
                          </span>
                        ) : runDuration(run)}
                      </td>
                      <td className="px-4 py-3">{run.pagesScraped}</td>
                      <td className="px-4 py-3">{run.adsFound}</td>
                      <td className="px-4 py-3 text-green-600">+{run.adsNew}</td>
                      <td className="px-4 py-3 text-gray-500 capitalize">{run.triggeredBy}</td>
                      <td className="px-4 py-3">
                        <Link
                          href={`/runs/${run.id}`}
                          className="text-indigo-600 hover:underline text-xs"
                        >
                          Details
                        </Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </>
      )}
    </div>
  );
}
