'use client';

import React, { useState, useMemo, useCallback, useRef } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import type { JobAd } from '@/types';
import { Button } from '@/components/ui/Button';
import { useMarkAdRead, usePinAd, useTrashAd } from '@/lib/hooks/useJobAds';
import { NewJobAdModal } from '@/components/ads/NewJobAdModal';
import { Tooltip } from '@/components/ui/Tooltip';
import { ApplyCell } from './ApplyCell';
import { ScoreCell } from './ScoreCell';
import { AdScoringPanel } from './AdScoringPanel';
import { BulkActionMenu } from './BulkActionMenu';

// ── Flat row type for virtualiser ─────────────────────────────────────────────

type FlatRow = { type: 'ad'; ad: JobAd } | { type: 'expand'; ad: JobAd };

interface AdTableProps {
  ads: JobAd[];
}

export function AdTable({ ads }: AdTableProps) {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [editAdId, setEditAdId] = useState<string | null>(null);
  const parentRef = useRef<HTMLDivElement>(null);
  const trashAd = useTrashAd();
  const pinAd = usePinAd();
  const markRead = useMarkAdRead();

  const editAd = useMemo(
    () => (editAdId ? ads.find((a) => a.id === editAdId) ?? null : null),
    [editAdId, ads]
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
      {editAd && (
        <NewJobAdModal ad={editAd} onClose={() => setEditAdId(null)} />
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
            <col style={{ width: '8rem' }} />
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
                        <Tooltip content="Edit" position="top" wrapperAs="span">
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              setEditAdId(ad.id);
                            }}
                            className="text-gray-300 hover:text-indigo-500 transition-colors"
                          >
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                              <path d="M5.433 13.917l1.262-3.155A4 4 0 0 1 7.58 9.42l6.92-6.918a2.121 2.121 0 0 1 3 3l-6.92 6.918c-.383.383-.84.685-1.343.886l-3.154 1.262a.5.5 0 0 1-.65-.65Z" />
                              <path d="M3.5 5.75c0-.69.56-1.25 1.25-1.25H10A.75.75 0 0 0 10 3H4.75A2.75 2.75 0 0 0 2 5.75v9.5A2.75 2.75 0 0 0 4.75 18h9.5A2.75 2.75 0 0 0 17 15.25V10a.75.75 0 0 0-1.5 0v5.25c0 .69-.56 1.25-1.25 1.25h-9.5c-.69 0-1.25-.56-1.25-1.25v-9.5Z" />
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
