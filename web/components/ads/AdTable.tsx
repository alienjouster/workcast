'use client';

import React, { useState, useMemo, useCallback, useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import { useRouter } from 'next/navigation';
import type { JobAd } from '@/types';
import { Button } from '@/components/ui/Button';
import { useMarkAdRead, usePinAd, useTrashAd, useBulkAction } from '@/lib/hooks/useJobAds';
import { useAdScoring, useRunScoring } from '@/lib/hooks/useAdScoring';
import { useSettings } from '@/lib/hooks/useSettings';
import { useCreateApplication } from '@/lib/hooks/useApplications';
import { NoteModal } from '@/components/ads/NoteModal';
import { CATEGORY_STYLES, ScoringSpinner, ScoringErrorBanner, ScoringRequirementsGrid, scoreColorClass } from '@/components/scoring/ScoringShared';
import { Tooltip } from '@/components/ui/Tooltip';

// ── Helpers ──────────────────────────────────────────────────────────────────

function timeAgo(date: string): string {
  const seconds = Math.floor((Date.now() - new Date(date).getTime()) / 1000);
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
  if (seconds < 60)   return rtf.format(-seconds, 'second');
  if (seconds < 3600) return rtf.format(-Math.floor(seconds / 60), 'minute');
  if (seconds < 86400) return rtf.format(-Math.floor(seconds / 3600), 'hour');
  return rtf.format(-Math.floor(seconds / 86400), 'day');
}

// ── Apply column cell ─────────────────────────────────────────────────────────

function ApplyCell({ adId }: { adId: string }) {
  const router = useRouter();
  const createApplication = useCreateApplication();

  return (
    <Tooltip content="Apply to this job" position="top" wrapperAs="span">
    <button
      disabled={createApplication.isPending}
      onClick={(e) => {
        e.stopPropagation();
        createApplication.mutateAsync(adId).then((application) => {
          router.push(`/applications/${application.id}`);
        });
      }}
      className="text-gray-300 hover:text-indigo-400 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
    >
      {createApplication.isPending ? (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" className="w-5 h-5 text-indigo-400 animate-spin">
          <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="3" strokeOpacity="0.2" />
          <path d="M12 3a9 9 0 0 1 9 9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
        </svg>
      ) : (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <path d="M14 2v6h6" />
          <path d="M8 13h8m0 0-3-3m3 3-3 3" />
        </svg>
      )}
    </button>
    </Tooltip>
  );
}

// ── Score column cell ─────────────────────────────────────────────────────────

function ScoreCell({ ad }: { ad: JobAd }) {
  const runScoring = useRunScoring();
  const { data: settings } = useSettings();
  const hasResume = settings?.hasResume ?? false;

  if (ad.isScoringPending || runScoring.isPending) {
    return <ScoringSpinner />;
  }

  if (ad.overallScore != null) {
    return (
      <span className={`text-xs font-medium tabular-nums ${scoreColorClass(ad.overallScore)}`}>
        {Math.round(ad.overallScore)}%
      </span>
    );
  }

  return (
    <Tooltip content="✨ Run scoring analysis" position="top" wrapperAs="span">
    <button
      disabled={!hasResume}
      onClick={(e) => { e.stopPropagation(); runScoring.mutate(ad.id); }}
      className="text-gray-300 hover:text-indigo-400 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
    >
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
        <path d="M3.75 13.5a8.25 8.25 0 1 1 16.5 0" />
        <path d="M12 13.5 9.5 8.5" />
        <circle cx="12" cy="13.5" r="1" fill="currentColor" stroke="none" />
      </svg>
    </button>
    </Tooltip>
  );
}

// ── Scoring sub-component ────────────────────────────────────────────────────

function AdScoringPanel({ adId, isScoringPending, lastScoringError }: { adId: string; isScoringPending: boolean; lastScoringError: string | null }) {
  const { data: scoring, isLoading, isFetching } = useAdScoring(adId, isScoringPending);
  const { data: settings } = useSettings();
  const runScoring = useRunScoring();

  const hasResume = settings?.hasResume ?? false;
  const isRunning = runScoring.isPending || isFetching || isScoringPending;

  if (!scoring && !isRunning && !lastScoringError) return null;

  return (
    <div className="mt-4 border-t border-gray-200 pt-3">
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Ad matching score
          {scoring?.scoredAt && (
            <span className="ml-2 normal-case font-normal text-gray-400">{timeAgo(scoring.scoredAt)}</span>
          )}
        </span>
        {!scoring && !hasResume && (
          <span className="text-xs text-gray-400 italic">
            Upload a resume from the{' '}
            <a href="/settings" className="text-indigo-500 hover:underline">Settings page</a>
          </span>
        )}
      </div>

      {isRunning && !scoring && (
        <p className="text-xs text-gray-400 italic">Analysing…</p>
      )}

      {!isRunning && !scoring && lastScoringError && (
        <ScoringErrorBanner error={lastScoringError} />
      )}

      {scoring && (
        <div className="space-y-3">
          {/* Score + summary box */}
          <div className="flex items-stretch gap-4 rounded-lg border border-gray-200 bg-white px-4 py-3">
            <div className="shrink-0 flex flex-col items-center justify-between pt-0.5">
              <span className="text-3xl font-bold text-gray-800 leading-none">
                {Math.round(scoring.overallScore)}<span className="text-base font-normal text-gray-400">/100</span>
              </span>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => runScoring.mutate(adId)}
                loading={isRunning}
                disabled={isRunning || !hasResume}
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5 mr-1">
                  <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
                </svg>
                Re-score
              </Button>
            </div>
            <div className="flex-1 space-y-2">
              {scoring.recommendation && (
                <div>
                  <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-0.5">Recommendation</p>
                  <p className="text-sm text-gray-800 leading-relaxed">{scoring.recommendation}</p>
                </div>
              )}
              {scoring.recommendation && scoring.summary && (
                <hr className="border-gray-100" />
              )}
              {scoring.summary && (
                <p className="text-xs text-gray-500 leading-relaxed">{scoring.summary}</p>
              )}
            </div>
          </div>

          {/* Requirements grouped by category */}
          <ScoringRequirementsGrid requirements={scoring.requirements} />
        </div>
      )}
    </div>
  );
}

// ── Bulk action menu ──────────────────────────────────────────────────────────

const BulkActionMenu = React.memo(function BulkActionMenu({
  selectedIds,
  onDone,
  disabled,
}: {
  selectedIds: string[];
  onDone: () => void;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const bulk = useBulkAction();

  const run = (fn: (ids: string[]) => Promise<unknown>) => {
    fn(selectedIds).then(() => { setOpen(false); onDone(); });
  };

  const actions = [
    {
      label: 'Pin',
      fn: () => run((ids) => bulk.pin.mutateAsync(ids)),
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
          <path d="M6.32 2.577a49.255 49.255 0 0 1 11.36 0c1.497.174 2.57 1.46 2.57 2.93V21a.75.75 0 0 1-1.085.67L12 18.089l-7.165 3.583A.75.75 0 0 1 3.75 21V5.507c0-1.47 1.073-2.756 2.57-2.93Z" />
        </svg>
      ),
    },
    {
      label: 'Unpin',
      fn: () => run((ids) => bulk.unpin.mutateAsync(ids)),
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} className="w-4 h-4">
          <path strokeLinecap="round" strokeLinejoin="round" d="M17.593 3.322c1.1.128 1.907 1.077 1.907 2.185V21L12 17.25 4.5 21V5.507c0-1.108.806-2.057 1.907-2.185a48.507 48.507 0 0 1 11.186 0Z" />
        </svg>
      ),
    },
    {
      label: 'Mark as read',
      fn: () => run((ids) => bulk.read.mutateAsync(ids)),
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
          <path d="M19.5 22.5a3 3 0 0 0 3-3v-8.174l-6.879 4.022 3.485 1.079a.75.75 0 0 1-.452 1.43l-5.995-1.858a.75.75 0 0 0-.451 0l-5.994 1.858a.75.75 0 1 1-.452-1.43l3.485-1.08-6.879-4.02V19.5a3 3 0 0 0 3 3h15Z" />
          <path d="M1.5 9.589v-.745a3 3 0 0 1 1.578-2.641l7.5-4.039a3 3 0 0 1 2.844 0l7.5 4.039A3 3 0 0 1 22.5 8.844v.745l-9.458 5.525a1.5 1.5 0 0 1-1.584 0L1.5 9.59Z" />
        </svg>
      ),
    },
    {
      label: 'Mark as unread',
      fn: () => run((ids) => bulk.unread.mutateAsync(ids)),
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
          <path d="M1.5 8.67v8.58a3 3 0 0 0 3 3h15a3 3 0 0 0 3-3V8.67l-8.928 5.493a3 3 0 0 1-3.144 0L1.5 8.67Z" />
          <path d="M22.5 6.908V6.75a3 3 0 0 0-3-3h-15a3 3 0 0 0-3 3v.158l9.714 5.978a1.5 1.5 0 0 0 1.572 0L22.5 6.908Z" />
        </svg>
      ),
    },
    {
      label: 'Trash',
      fn: () => run((ids) => bulk.trash.mutateAsync(ids)),
      danger: true,
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
          <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z" clipRule="evenodd" />
        </svg>
      ),
    },
  ];

  return (
    <div className="relative">
      <button
        onClick={() => setOpen((v) => !v)}
        disabled={disabled || bulk.isPending}
        className="flex items-center gap-1 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
          <path d="M3 10a1.5 1.5 0 1 1 3 0 1.5 1.5 0 0 1-3 0ZM8.5 10a1.5 1.5 0 1 1 3 0 1.5 1.5 0 0 1-3 0ZM15.5 8.5a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3Z" />
        </svg>
        Actions
        {bulk.isPending && <span className="ml-1 text-xs text-gray-400">…</span>}
      </button>
      {open && (
        <>
          <div className="fixed inset-0 z-10" onClick={() => setOpen(false)} />
          <div className="absolute left-0 z-20 mt-1 w-48 rounded-md border border-gray-200 bg-white shadow-lg py-1 text-sm">
            {actions.map((a) => (
              <button
                key={a.label}
                onClick={a.fn}
                className={`flex items-center gap-2.5 w-full text-left px-4 py-2 hover:bg-gray-50 ${'danger' in a && a.danger ? 'text-red-600' : 'text-gray-700'}`}
              >
                {a.icon}
                {a.label}
              </button>
            ))}
          </div>
        </>
      )}
    </div>
  );
});

// ── Flat row type for virtualiser ─────────────────────────────────────────────

type FlatRow = { type: 'ad'; ad: JobAd } | { type: 'expand'; ad: JobAd };

interface AdTableProps {
  ads: JobAd[];
}

export function AdTable({ ads }: AdTableProps) {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [noteAdId, setNoteAdId] = useState<string | null>(null);
  const parentRef = useRef<HTMLDivElement>(null);
  const trashAd = useTrashAd();
  const pinAd = usePinAd();
  const markRead = useMarkAdRead();

  const noteAd = useMemo(
    () => (noteAdId ? ads.find((a) => a.id === noteAdId) ?? null : null),
    [noteAdId, ads]
  );

  const allSelected = useMemo(
    () => ads.length > 0 && ads.every((a) => selectedIds.has(a.id)),
    [ads, selectedIds]
  );
  const someSelected = selectedIds.size > 0;

  const toggleAll = useCallback(() => {
    setSelectedIds((prev) =>
      ads.length > 0 && ads.every((a) => prev.has(a.id))
        ? new Set()
        : new Set(ads.map((a) => a.id))
    );
  }, [ads]);

  const toggleOne = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }, []);

  // Flat row array: one entry per main row, plus one expand entry when expanded.
  const flatRows = useMemo<FlatRow[]>(() => {
    const result: FlatRow[] = [];
    for (const ad of ads) {
      result.push({ type: 'ad', ad });
      if (expandedId === ad.id) result.push({ type: 'expand', ad });
    }
    return result;
  }, [ads, expandedId]);

  const rowVirtualizer = useVirtualizer({
    count: flatRows.length,
    getScrollElement: () => parentRef.current,
    estimateSize: (index) => (flatRows[index]?.type === 'expand' ? 200 : 48),
    overscan: 5,
  });

  if (ads.length === 0) return null;

  const virtualItems = rowVirtualizer.getVirtualItems();
  const totalSize = rowVirtualizer.getTotalSize();
  const paddingTop = virtualItems.length > 0 ? virtualItems[0].start : 0;
  const paddingBottom =
    virtualItems.length > 0 ? totalSize - (virtualItems[virtualItems.length - 1]?.end ?? 0) : 0;

  return (
    <>
      {noteAd && (
        <NoteModal
          adId={noteAd.id}
          initialNote={noteAd.note}
          adTitle={noteAd.title}
          onClose={() => setNoteAdId(null)}
        />
      )}

      {/* Bulk action bar — always visible to prevent layout shift */}
      <div className="flex items-center gap-3 px-4 py-2 bg-indigo-50 border-b border-indigo-100">
        <span className="text-sm text-indigo-700 font-medium">
          {someSelected ? `${selectedIds.size} selected` : 'Select items to apply bulk actions'}
        </span>
        <BulkActionMenu
          selectedIds={Array.from(selectedIds)}
          onDone={() => setSelectedIds(new Set())}
          disabled={!someSelected}
        />
        {someSelected && (
          <button
            onClick={() => setSelectedIds(new Set())}
            className="ml-auto text-xs text-indigo-500 hover:text-indigo-700"
          >
            Clear selection
          </button>
        )}
      </div>

      {/* Virtualised scrollable table */}
      <div
        ref={parentRef}
        className="overflow-auto"
        style={{ maxHeight: 'calc(100vh - 280px)', overflowAnchor: 'none' }}
      >
        <table className="w-full divide-y divide-gray-200 text-sm table-fixed">
          {/* col widths lock layout; title col has no width so it takes the remainder */}
          <colgroup>
            <col style={{ width: '2rem' }} />
            <col style={{ width: '2rem' }} />
            <col style={{ width: '2rem' }} />
            <col style={{ width: '3.5rem' }} />
            <col style={{ width: '2.5rem' }} />
            <col />
            <col style={{ width: '9rem' }} />
            <col style={{ width: '9rem' }} />
            <col style={{ width: '6rem' }} />
            <col style={{ width: '7rem' }} />
          </colgroup>
          <thead className="bg-gray-50 sticky top-0 z-10">
            <tr>
              <th className="px-4 py-3">
                <input
                  type="checkbox"
                  checked={allSelected}
                  onChange={toggleAll}
                  className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
              </th>
              <th className="px-4 py-3"></th>
              <th className="px-4 py-3"></th>
              <th className="px-4 py-3"></th>
              <th className="px-4 py-3"></th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Company</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Location</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Scraped</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Actions</th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {paddingTop > 0 && (
              <tr>
                <td colSpan={10} style={{ height: paddingTop }} />
              </tr>
            )}
            {virtualItems.map((virtualItem) => {
              const row = flatRows[virtualItem.index];

              if (row.type === 'ad') {
                const ad = row.ad;
                return (
                  <tr
                    key={virtualItem.key}
                    data-index={virtualItem.index}
                    className={`cursor-pointer ${ad.isPinned ? 'bg-amber-50 hover:bg-amber-100' : 'hover:bg-gray-50'}`}
                    onClick={() => {
                      const isOpening = expandedId !== ad.id;
                      setExpandedId(isOpening ? ad.id : null);
                      if (isOpening && !ad.isRead) markRead.mutate({ id: ad.id, read: false });
                    }}
                  >
                    <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        checked={selectedIds.has(ad.id)}
                        onChange={() => toggleOne(ad.id)}
                        className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                      />
                    </td>
                    <td className="px-4 py-3">
                      <Tooltip content={ad.isPinned ? 'Unpin' : 'Pin to top'} position="top" wrapperAs="span">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          pinAd.mutate({ id: ad.id, pinned: ad.isPinned });
                        }}
                        className={`transition-colors ${ad.isPinned ? 'text-slate-600 hover:text-gray-400' : 'text-gray-300 hover:text-slate-400'}`}
                      >
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
                          <path d="M6.32 2.577a49.255 49.255 0 0 1 11.36 0c1.497.174 2.57 1.46 2.57 2.93V21a.75.75 0 0 1-1.085.67L12 18.089l-7.165 3.583A.75.75 0 0 1 3.75 21V5.507c0-1.47 1.073-2.756 2.57-2.93Z" />
                        </svg>
                      </button>
                      </Tooltip>
                    </td>
                    <td className="px-4 py-3">
                      <Tooltip content={ad.isRead ? 'Mark as unread' : 'Mark as read'} position="top" wrapperAs="span">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          markRead.mutate({ id: ad.id, read: ad.isRead });
                        }}
                        className={`transition-colors ${ad.isRead ? 'text-gray-300 hover:text-slate-400' : 'text-slate-600 hover:text-gray-400'}`}
                      >
                        {ad.isRead ? (
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
                            <path d="M19.5 22.5a3 3 0 0 0 3-3v-8.174l-6.879 4.022 3.485 1.079a.75.75 0 0 1-.452 1.43l-5.995-1.858a.75.75 0 0 0-.451 0l-5.994 1.858a.75.75 0 1 1-.452-1.43l3.485-1.08-6.879-4.02V19.5a3 3 0 0 0 3 3h15Z" />
                            <path d="M1.5 9.589v-.745a3 3 0 0 1 1.578-2.641l7.5-4.039a3 3 0 0 1 2.844 0l7.5 4.039A3 3 0 0 1 22.5 8.844v.745l-9.458 5.525a1.5 1.5 0 0 1-1.584 0L1.5 9.59Z" />
                          </svg>
                        ) : (
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
                            <path d="M1.5 8.67v8.58a3 3 0 0 0 3 3h15a3 3 0 0 0 3-3V8.67l-8.928 5.493a3 3 0 0 1-3.144 0L1.5 8.67Z" />
                            <path d="M22.5 6.908V6.75a3 3 0 0 0-3-3h-15a3 3 0 0 0-3 3v.158l9.714 5.978a1.5 1.5 0 0 0 1.572 0L22.5 6.908Z" />
                          </svg>
                        )}
                      </button>
                      </Tooltip>
                    </td>
                    <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-center">
                        <ScoreCell ad={ad} />
                      </div>
                    </td>
                    <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                      <div className="flex items-center justify-center">
                        <ApplyCell adId={ad.id} />
                      </div>
                    </td>
                    <td className="px-4 py-3 overflow-hidden">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className={`w-2 h-2 rounded-full shrink-0 ${ad.isRead ? 'invisible' : 'bg-red-500'}`} />
                        {!ad.isActive && (
                          <span className="text-xs bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded shrink-0">
                            Inactive
                          </span>
                        )}
                        <a
                          href={ad.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          title={ad.title ?? undefined}
                          className={`truncate hover:underline ${ad.isRead ? 'font-normal text-indigo-400' : 'font-semibold text-indigo-700'}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            if (!ad.isRead) markRead.mutate({ id: ad.id, read: false });
                          }}
                        >
                          {ad.title ?? '(no title)'}
                        </a>
                      </div>
                    </td>
                    <td className={`px-4 py-3 overflow-hidden truncate ${ad.isRead ? 'text-gray-400' : 'text-gray-700'}`} title={ad.company ?? undefined}>{ad.company ?? '—'}</td>
                    <td className={`px-4 py-3 overflow-hidden truncate ${ad.isRead ? 'text-gray-400' : 'text-gray-700'}`} title={ad.location ?? undefined}>{ad.location ?? '—'}</td>
                    <td className="px-4 py-3 text-gray-500">
                      {new Date(ad.scrapedAt).toLocaleDateString()}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1">
                        <Tooltip content={ad.note ? 'Edit note' : 'Add note'} position="top" wrapperAs="span">
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            setNoteAdId(ad.id);
                          }}
                          className={`transition-colors ${ad.note ? 'text-indigo-500 hover:text-indigo-700' : 'text-gray-300 hover:text-gray-500'}`}
                        >
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-4 h-4">
                            <path d="M15.5 3H5a2 2 0 0 0-2 2v14c0 1.1.9 2 2 2h14a2 2 0 0 0 2-2V8.5L15.5 3Z" />
                            <path d="M15 3v6h6" />
                          </svg>
                        </button>
                        </Tooltip>
                        <Tooltip content="Move to trash" position="top" wrapperAs="span">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            trashAd.mutate(ad.id);
                          }}
                          className="text-gray-400 hover:text-red-500"
                        >
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                            <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z" clipRule="evenodd" />
                          </svg>
                        </Button>
                        </Tooltip>
                      </div>
                    </td>
                  </tr>
                );
              }

              // type === 'expand'
              const ad = row.ad;
              return (
                <tr
                  key={virtualItem.key}
                  data-index={virtualItem.index}
                  ref={rowVirtualizer.measureElement}
                >
                  <td colSpan={10} className="px-4 py-4 bg-gray-50">
                    {ad.description ? (
                      <p className="text-sm text-gray-700 whitespace-pre-wrap max-h-48 overflow-y-auto">
                        {ad.description}
                      </p>
                    ) : (
                      <p className="text-sm text-gray-400 italic">No description available.</p>
                    )}
                    <div className="flex gap-4 mt-2 text-xs text-gray-400">
                      {ad.salaryRaw && <span>Salary: {ad.salaryRaw}</span>}
                      {ad.postedAt && (
                        <span>Posted: {new Date(ad.postedAt).toLocaleDateString()}</span>
                      )}
                    </div>
                    <AdScoringPanel adId={ad.id} isScoringPending={ad.isScoringPending} lastScoringError={ad.lastScoringError} />
                  </td>
                </tr>
              );
            })}
            {paddingBottom > 0 && (
              <tr>
                <td colSpan={10} style={{ height: paddingBottom }} />
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </>
  );
}
