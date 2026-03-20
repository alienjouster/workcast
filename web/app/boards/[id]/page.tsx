'use client';

import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useState } from 'react';
import {
  useJobBoard,
  useUpdateBoard,
  useDeleteBoard,
  useRefreshBoard,
  useReanalyzeBoard,
} from '@/lib/hooks/useJobBoards';
import { useScrapeRuns } from '@/lib/hooks/useScrapeRuns';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Card, CardHeader, CardBody } from '@/components/ui/Card';
import { ScraperConfigView } from '@/components/boards/ScraperConfigView';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';

export default function BoardDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data: board, isLoading, error } = useJobBoard(id);
  const { data: runs } = useScrapeRuns(id, 10);

  const updateBoard = useUpdateBoard(id);
  const deleteBoard = useDeleteBoard();
  const refreshBoard = useRefreshBoard();
  const reanalyzeBoard = useReanalyzeBoard();

  const [editingName, setEditingName] = useState(false);
  const [nameValue, setNameValue] = useState('');
  const [editingUrl, setEditingUrl] = useState(false);
  const [urlValue, setUrlValue] = useState('');
  const [editingCron, setEditingCron] = useState(false);
  const [cronValue, setCronValue] = useState('');

  if (isLoading) return <LoadingSpinner />;
  if (error || !board) {
    return (
      <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
        {error?.message ?? 'Board not found'}
      </div>
    );
  }

  async function handleDelete() {
    if (!confirm(`Delete "${board!.name ?? board!.url}"? This cannot be undone.`)) return;
    await deleteBoard.mutateAsync(id);
    router.push('/boards');
  }

  async function handleSaveName() {
    await updateBoard.mutateAsync({ name: nameValue });
    setEditingName(false);
  }

  async function handleSaveUrl() {
    await updateBoard.mutateAsync({ url: urlValue });
    setEditingUrl(false);
  }

  async function handleSaveCron() {
    await updateBoard.mutateAsync({ scheduleCron: cronValue });
    setEditingCron(false);
  }

  async function handleTogglePause() {
    await updateBoard.mutateAsync({
      status: board!.status === 'paused' ? 'active' : 'paused',
    });
  }

  function formatDate(iso: string | null) {
    if (!iso) return '—';
    return new Date(iso).toLocaleString();
  }

  function runDuration(run: { startedAt: string; finishedAt: string | null }) {
    if (!run.finishedAt) return 'Running…';
    const ms = new Date(run.finishedAt).getTime() - new Date(run.startedAt).getTime();
    return `${(ms / 1000).toFixed(0)}s`;
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <div className="flex items-center gap-3 mb-1">
            <h1 className="text-2xl font-bold text-gray-900">
              {board.name ?? board.url}
            </h1>
            <Badge status={board.status} />
          </div>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="danger"
            size="sm"
            onClick={handleDelete}
            loading={deleteBoard.isPending}
          >
            Delete
          </Button>
        </div>
      </div>

      {/* Info Card */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Board Details</h2>
        </CardHeader>
        <CardBody className="space-y-4">
          {/* Name */}
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Name</label>
            {editingName ? (
              <div className="flex items-center gap-2">
                <input
                  className="rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  value={nameValue}
                  onChange={(e) => setNameValue(e.target.value)}
                  autoFocus
                />
                <Button size="sm" variant="primary" onClick={handleSaveName} loading={updateBoard.isPending}>
                  Save
                </Button>
                <Button size="sm" variant="secondary" onClick={() => setEditingName(false)}>
                  Cancel
                </Button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <span className="text-sm text-gray-900">{board.name ?? '—'}</span>
                <button
                  onClick={() => { setNameValue(board.name ?? ''); setEditingName(true); }}
                  className="text-xs text-indigo-500 hover:underline"
                >
                  Edit
                </button>
              </div>
            )}
          </div>

          {/* URL */}
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">URL</label>
            {editingUrl ? (
              <div className="flex items-center gap-2">
                <input
                  className="rounded border border-gray-300 px-2 py-1 text-sm font-mono w-96 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  value={urlValue}
                  onChange={(e) => setUrlValue(e.target.value)}
                  autoFocus
                />
                <Button size="sm" variant="primary" onClick={handleSaveUrl} loading={updateBoard.isPending}>
                  Save
                </Button>
                <Button size="sm" variant="secondary" onClick={() => setEditingUrl(false)}>
                  Cancel
                </Button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <a
                  href={board.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-sm font-mono text-indigo-600 hover:underline"
                >
                  {board.url}
                </a>
                <button
                  onClick={() => { setUrlValue(board.url); setEditingUrl(true); }}
                  className="text-xs text-indigo-500 hover:underline"
                >
                  Edit
                </button>
              </div>
            )}
          </div>

          {/* Schedule */}
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Schedule (cron)</label>
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-2">
                {editingCron ? (
                  <>
                    <input
                      className="rounded border border-gray-300 px-2 py-1 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
                      value={cronValue}
                      onChange={(e) => setCronValue(e.target.value)}
                      autoFocus
                    />
                    <Button size="sm" variant="primary" onClick={handleSaveCron} loading={updateBoard.isPending}>
                      Save
                    </Button>
                    <Button size="sm" variant="secondary" onClick={() => setEditingCron(false)}>
                      Cancel
                    </Button>
                  </>
                ) : (
                  <>
                    <span className="text-sm font-mono text-gray-900">{board.scheduleCron}</span>
                    <button
                      onClick={() => { setCronValue(board.scheduleCron); setEditingCron(true); }}
                      className="text-xs text-indigo-500 hover:underline"
                    >
                      Edit
                    </button>
                  </>
                )}
              </div>
              <button
                role="switch"
                aria-checked={board.status !== 'paused'}
                onClick={handleTogglePause}
                disabled={updateBoard.isPending}
                className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:opacity-50 ${
                  board.status !== 'paused' ? 'bg-indigo-600' : 'bg-gray-200'
                }`}
              >
                <span
                  className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                    board.status !== 'paused' ? 'translate-x-5' : 'translate-x-0'
                  }`}
                />
              </button>
            </div>
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-sm">
            <div>
              <span className="text-xs text-gray-500">Status</span>
              <p className="mt-0.5"><Badge status={board.status} /></p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Ad Count</span>
              <p className="font-medium">{board.adCount}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Last Scraped</span>
              <p className="font-medium">{formatDate(board.lastScrapedAt)}</p>
            </div>
            <div>
              <span className="text-xs text-gray-500">Created</span>
              <p className="font-medium">{formatDate(board.createdAt)}</p>
            </div>
          </div>

          <div className="pt-2">
            <Link
              href={`/boards/${id}/ads`}
              className="text-sm text-indigo-600 hover:underline"
            >
              View all {board.adCount} ads →
            </Link>
          </div>
        </CardBody>
      </Card>

      {/* Scraper Config */}
      {board.scraperConfig && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <h2 className="font-semibold text-gray-900">Scraper Configuration</h2>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => reanalyzeBoard.mutate(id)}
                loading={reanalyzeBoard.isPending}
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  className="w-4 h-4 mr-1.5 inline-block"
                >
                  <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
                </svg>
                Re-analyze
              </Button>
            </div>
          </CardHeader>
          <CardBody>
            <ScraperConfigView boardId={id} config={board.scraperConfig} />
          </CardBody>
        </Card>
      )}

      {board.status === 'pending' && !board.scraperConfig && (
        <div className="rounded-md bg-yellow-50 border border-yellow-200 p-4 text-sm text-yellow-800">
          Board analysis is in progress. The scraper config will appear here once analysis completes.
        </div>
      )}

      {/* Run History */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <h2 className="font-semibold text-gray-900">Recent Scrape Runs</h2>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => refreshBoard.mutate(id)}
              loading={refreshBoard.isPending}
            >
              Manual Refresh
            </Button>
          </div>
        </CardHeader>
        <CardBody className="p-0">
          {!runs || runs.length === 0 ? (
            <p className="text-sm text-gray-500 px-6 py-4">No runs yet.</p>
          ) : (
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50">
                <tr>
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
                    <td className="px-4 py-3"><Badge status={run.status} /></td>
                    <td className="px-4 py-3 text-gray-600">
                      {new Date(run.startedAt).toLocaleString()}
                    </td>
                    <td className="px-4 py-3 text-gray-600">{runDuration(run)}</td>
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
          )}
        </CardBody>
      </Card>
    </div>
  );
}
