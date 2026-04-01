'use client';

import cronstrue from 'cronstrue';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useState, useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { isActiveRunStatus, type UpdateJobBoardRequest } from '@/types';
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
import { Tooltip } from '@/components/ui/Tooltip';

const CRON_FIELDS = [
  { label: 'min',  min: 0, max: 59 },
  { label: 'hour', min: 0, max: 23 },
  { label: 'dom',  min: 1, max: 31 },
  { label: 'mon',  min: 1, max: 12 },
  { label: 'dow',  min: 0, max: 7  },
] as const;

function isValidCronPart(value: string, min: number, max: number): boolean {
  if (!value || value === '*') return true;
  if (/^\*\/\d+$/.test(value)) return true;
  if (/^\d+$/.test(value)) { const n = +value; return n >= min && n <= max; }
  if (/^\d+-\d+$/.test(value)) {
    const [a, b] = value.split('-').map(Number);
    return a >= min && b <= max && a <= b;
  }
  if (/^\d+(,\d+)+$/.test(value)) {
    return value.split(',').map(Number).every((n) => n >= min && n <= max);
  }
  return false;
}

function CronEditor({
  value,
  onChange,
  onSave,
  onCancel,
  isPending,
}: {
  value: string;
  onChange: (v: string) => void;
  onSave: () => void;
  onCancel: () => void;
  isPending: boolean;
}) {
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);
  const rawParts = value.trim().split(/\s+/);
  const parts = CRON_FIELDS.map((_, i) => rawParts[i] ?? '*');

  function setPart(index: number, val: string) {
    const next = [...parts];
    next[index] = val || '*';
    onChange(next.join(' '));
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>, index: number) {
    // On backspace in an empty/wildcard field, go back to previous
    if (e.key === 'Backspace' && (e.currentTarget.value === '' || e.currentTarget.value === '*') && index > 0) {
      inputRefs.current[index - 1]?.focus();
      inputRefs.current[index - 1]?.select();
    }
  }

  function handleKeyUp(e: React.KeyboardEvent<HTMLInputElement>, index: number) {
    // Auto-advance on any printable character
    if (e.key.length === 1 && !e.ctrlKey && !e.metaKey && index < CRON_FIELDS.length - 1) {
      inputRefs.current[index + 1]?.focus();
      inputRefs.current[index + 1]?.select();
    }
  }

  let preview = '';
  let isValid = false;
  try {
    preview = cronstrue.toString(value, { throwExceptionOnParseError: true });
    isValid = true;
  } catch {
    preview = 'Invalid cron expression';
  }

  return (
    <div className="space-y-2 py-0.5">
      <div className="flex items-end gap-2">
        {CRON_FIELDS.map(({ label, min, max }, i) => {
          const fieldValid = isValidCronPart(parts[i], min, max);
          return (
            <div key={label} className="flex flex-col items-center gap-1">
              <span className="text-xs text-gray-400">{label}</span>
              <input
                ref={(el) => { inputRefs.current[i] = el; }}
                autoFocus={i === 0}
                className={`w-12 rounded border px-1.5 py-1 text-xs font-mono text-center focus:outline-none focus:ring-2 focus:ring-indigo-500 ${
                  fieldValid ? 'border-gray-300' : 'border-red-400 bg-red-50 text-red-600'
                }`}
                value={parts[i]}
                onChange={(e) => setPart(i, e.target.value)}
                onFocus={(e) => e.target.select()}
                onKeyDown={(e) => handleKeyDown(e, i)}
                onKeyUp={(e) => handleKeyUp(e, i)}
              />
            </div>
          );
        })}
      </div>
      <p className={`text-xs ${isValid ? 'text-gray-400' : 'text-red-500'}`}>{preview}</p>
      <div className="flex items-center gap-2">
        <Button size="sm" variant="primary" onClick={onSave} disabled={!isValid} loading={isPending}>Save</Button>
        <Button size="sm" variant="secondary" onClick={onCancel}>Cancel</Button>
      </div>
    </div>
  );
}

export default function BoardDetailPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { data: board, isLoading, error } = useJobBoard(id);
  const { data: runs } = useScrapeRuns(id, 10);

  const updateBoard = useUpdateBoard(id);
  const deleteBoard = useDeleteBoard();
  const refreshBoard = useRefreshBoard();
  const reanalyzeBoard = useReanalyzeBoard();

  const qc = useQueryClient();
  const [awaitingRun, setAwaitingRun] = useState(false);

  // Poll every 2 s after a manual refresh until Hangfire creates the run.
  // Once a 'running' run appears the useScrapeRuns refetchInterval takes over.
  useEffect(() => {
    if (!awaitingRun) return;
    const interval = setInterval(
      () => qc.refetchQueries({ queryKey: ['scrape-runs', id, 10] }),
      2000,
    );
    return () => clearInterval(interval);
  }, [awaitingRun, id, qc]);

  useEffect(() => {
    if (awaitingRun && runs?.some((r) => isActiveRunStatus(r.status))) {
      setAwaitingRun(false);
    }
  }, [runs, awaitingRun]);

  // When board analysis completes (pending → active), an immediate scrape is auto-triggered.
  // Activate the same aggressive 2 s polling used for manual refresh so the new run
  // appears as 'running' in the list rather than only appearing once completed.
  const prevBoardStatusRef = useRef<string | undefined>(undefined);
  useEffect(() => {
    if (prevBoardStatusRef.current === 'pending' && board?.status === 'active') {
      setAwaitingRun(true);
    }
    prevBoardStatusRef.current = board?.status;
  }, [board?.status]);

  // Detect when a run transitions from 'running' → completed/failed/partial and refresh
  // the board detail (adCount, lastScrapedAt) and global unread badge.
  // This is a polling-based fallback for when the SSE runCompleted event is missed.
  const hadRunningRunRef = useRef(false);
  useEffect(() => {
    if (runs === undefined) return;
    const hasActive = runs.some((r) => isActiveRunStatus(r.status));
    if (hadRunningRunRef.current && !hasActive) {
      qc.invalidateQueries({ queryKey: ['job-boards', id] });
      qc.invalidateQueries({ queryKey: ['status'] });
    }
    hadRunningRunRef.current = hasActive;
  }, [runs, id, qc]);

  const [editingField, setEditingField] = useState<string | null>(null);
  const [draftValue, setDraftValue] = useState('');
  const fieldInputRef = useRef<HTMLInputElement>(null);

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

  function startFieldEdit(field: string, value: string) {
    setEditingField(field);
    setDraftValue(value);
  }

  function cancelFieldEdit() {
    setEditingField(null);
    setDraftValue('');
  }

  async function saveField(data: UpdateJobBoardRequest) {
    await updateBoard.mutateAsync(data);
    setEditingField(null);
    setDraftValue('');
  }

  function saveFieldFromRef(builder: (v: string) => UpdateJobBoardRequest) {
    const v = fieldInputRef.current?.value ?? '';
    saveField(builder(v));
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
          <div>
            <h2 className="font-semibold text-gray-900">Board Details</h2>
            <p className="text-xs text-gray-400 mt-0.5">
              Created {formatDate(board.createdAt)}{board.lastScrapedAt && (
                <> · Last scraped <Tooltip content={formatDate(board.lastScrapedAt)} position="top" wrapperAs="span"><span className="cursor-default">{timeAgo(board.lastScrapedAt)}</span></Tooltip></>
              )}
            </p>
          </div>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              {/* Section: Board */}
              <tr className="bg-gray-50">
                <td colSpan={3} className="px-4 py-2 text-xs font-semibold text-gray-400 uppercase tracking-wide">Board</td>
              </tr>

              {/* Name */}
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Name</td>
                {editingField === 'name' ? (
                  <>
                    <td className="px-4 py-2.5">
                      <input ref={fieldInputRef} autoFocus className="rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 w-64" defaultValue={draftValue} />
                    </td>
                    <td className="px-4 py-2.5 text-right whitespace-nowrap">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="primary" onClick={() => saveFieldFromRef((v) => ({ name: v }))} loading={updateBoard.isPending}>Save</Button>
                        <Button size="sm" variant="secondary" onClick={cancelFieldEdit}>Cancel</Button>
                      </div>
                    </td>
                  </>
                ) : (
                  <>
                    <td className="px-4 py-2.5 text-sm text-gray-900">{board.name ?? <span className="text-gray-400 italic">—</span>}</td>
                    <td className="px-4 py-2.5 text-right">
                      <button onClick={() => startFieldEdit('name', board.name ?? '')} className="text-xs text-indigo-500 hover:underline">Edit</button>
                    </td>
                  </>
                )}
              </tr>

              {/* URL */}
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48 shrink-0">URL</td>
                {editingField === 'url' ? (
                  <>
                    <td className="px-4 py-2.5">
                      <input ref={fieldInputRef} autoFocus className="rounded border border-gray-300 px-2 py-1 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500 w-full max-w-lg" defaultValue={draftValue} />
                    </td>
                    <td className="px-4 py-2.5 text-right whitespace-nowrap">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="primary" onClick={() => saveFieldFromRef((v) => ({ url: v }))} loading={updateBoard.isPending}>Save</Button>
                        <Button size="sm" variant="secondary" onClick={cancelFieldEdit}>Cancel</Button>
                      </div>
                    </td>
                  </>
                ) : (
                  <>
                    <td className="px-4 py-2.5 min-w-0 max-w-0">
                      <Tooltip content={board.url} position="bottom" wrapperAs="span" wrap tooltipClassName="max-w-xs">
                        <a href={board.url} target="_blank" rel="noopener noreferrer" className="font-mono text-xs text-indigo-600 hover:underline truncate block">{board.url}</a>
                      </Tooltip>
                    </td>
                    <td className="px-4 py-2.5 text-right whitespace-nowrap">
                      <button onClick={() => startFieldEdit('url', board.url)} className="text-xs text-indigo-500 hover:underline">Edit</button>
                    </td>
                  </>
                )}
              </tr>

              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500">Status</td>
                <td className="px-4 py-2.5" colSpan={2}><Badge status={board.status} /></td>
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500">Job ads</td>
                <td className="px-4 py-2.5" colSpan={2}>
                  <Link href={`/boards/${id}/ads`} className="text-sm text-indigo-600 hover:underline">
                    {board.adCount} ads →
                  </Link>
                </td>
              </tr>
              {/* Section: Schedule */}
              <tr className="bg-gray-50 border-t border-gray-100">
                <td colSpan={3} className="px-4 py-2 text-xs font-semibold text-gray-400 uppercase tracking-wide">Schedule</td>
              </tr>

              {/* Active toggle */}
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Active</td>
                <td className="px-4 py-2.5" colSpan={2}>
                  <button
                    role="switch"
                    aria-checked={board.status !== 'paused'}
                    onClick={handleTogglePause}
                    disabled={updateBoard.isPending}
                    className={`relative inline-flex h-5 w-9 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:opacity-50 ${board.status !== 'paused' ? 'bg-indigo-600' : 'bg-gray-200'}`}
                  >
                    <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${board.status !== 'paused' ? 'translate-x-4' : 'translate-x-0'}`} />
                  </button>
                </td>
              </tr>

              {/* Cron */}
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Cron expression</td>
                {editingField === 'cron' ? (
                  <td className="px-4 py-2.5" colSpan={2}>
                    <CronEditor
                      value={draftValue}
                      onChange={setDraftValue}
                      onSave={() => saveField({ scheduleCron: draftValue })}
                      onCancel={cancelFieldEdit}
                      isPending={updateBoard.isPending}
                    />
                  </td>
                ) : (
                  <>
                    <td className="px-4 py-2.5 flex items-center gap-2">
                      <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">{board.scheduleCron}</span>
                      <span className="text-xs text-gray-400">{cronstrue.toString(board.scheduleCron, { throwExceptionOnParseError: false })}</span>
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      <button onClick={() => startFieldEdit('cron', board.scheduleCron)} className="text-xs text-indigo-500 hover:underline">Edit</button>
                    </td>
                  </>
                )}
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>

      {/* Scraper Config */}
      {board.scraperConfig && (
        <div className={board.status === 'pending' ? 'opacity-50 pointer-events-none' : ''}>
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <h2 className="font-semibold text-gray-900">Scraper Configuration</h2>
                <p className="text-xs text-gray-400 mt-0.5">Generated {new Date(board.scraperConfig.generatedAt).toLocaleString()}</p>
              </div>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => reanalyzeBoard.mutate(id)}
                loading={reanalyzeBoard.isPending || board.status === 'pending'}
                disabled={board.status === 'pending'}
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  className="w-4 h-4 mr-1.5 inline-block"
                >
                  <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
                </svg>
                Auto-configure with AI
              </Button>
            </div>
          </CardHeader>
          <CardBody>
            <ScraperConfigView boardId={id} config={board.scraperConfig} />
          </CardBody>
        </Card>
        </div>
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
              onClick={() => { setAwaitingRun(true); refreshBoard.mutate(id); }}
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
          )}
        </CardBody>
      </Card>
    </div>
  );
}
