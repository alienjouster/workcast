'use client';

import React, { useState, useMemo, useCallback } from 'react';
import type { JobAd } from '@/types';
import { Button } from '@/components/ui/Button';
import { useDeleteAd, useRestoreAd, useBulkTrashAction } from '@/lib/hooks/useJobAds';

interface TrashTableProps {
  ads: JobAd[];
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
  const bulk = useBulkTrashAction();

  const run = (fn: (ids: string[]) => Promise<unknown>) => {
    fn(selectedIds).then(() => { setOpen(false); onDone(); });
  };

  const actions = [
    {
      label: 'Restore',
      fn: () => run((ids) => bulk.restore.mutateAsync(ids)),
      icon: (
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
          <path fillRule="evenodd" d="M7.793 2.232a.75.75 0 0 1-.025 1.06L3.622 7.25h10.003a5.375 5.375 0 0 1 0 10.75H10.75a.75.75 0 0 1 0-1.5h2.875a3.875 3.875 0 0 0 0-7.75H3.622l4.146 3.957a.75.75 0 0 1-1.036 1.085l-5.5-5.25a.75.75 0 0 1 0-1.085l5.5-5.25a.75.75 0 0 1 1.06.025Z" clipRule="evenodd" />
        </svg>
      ),
    },
    {
      label: 'Delete permanently',
      fn: () => {
        if (confirm(`Permanently delete ${selectedIds.length} job ad${selectedIds.length > 1 ? 's' : ''}? They will be re-scraped on the next run.`)) {
          run((ids) => bulk.del.mutateAsync(ids));
        }
      },
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
          <div className="absolute left-0 z-20 mt-1 w-52 rounded-md border border-gray-200 bg-white shadow-lg py-1 text-sm">
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

// ── TrashTable ────────────────────────────────────────────────────────────────

export function TrashTable({ ads }: TrashTableProps) {
  const restoreAd = useRestoreAd();
  const deleteAd = useDeleteAd();

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

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

  return (
    <div>
      {/* Info card */}
      <div className="flex items-start gap-3 mb-4 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5 shrink-0 mt-0.5 text-blue-500">
          <path fillRule="evenodd" d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-7-4a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM9 9a.75.75 0 0 0 0 1.5h.253a.25.25 0 0 1 .244.304l-.459 2.066A1.75 1.75 0 0 0 10.747 15H11a.75.75 0 0 0 0-1.5h-.253a.25.25 0 0 1-.244-.304l.459-2.066A1.75 1.75 0 0 0 9.253 9H9Z" clipRule="evenodd" />
        </svg>
        <span>Items in the trash bin are automatically deleted after <strong>30 days</strong>. Inactive ads are also removed on the same schedule.</span>
      </div>

      {ads.length === 0 ? (
        <div className="text-center py-12 text-gray-400 text-sm">The trash bin is empty.</div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          {/* Bulk action bar */}
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

          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="w-10 px-4 py-3">
                  <input
                    type="checkbox"
                    checked={allSelected}
                    onChange={toggleAll}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                </th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Company</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Location</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Scraped</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Actions</th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {ads.map((ad) => (
                <tr key={ad.id} className={`hover:bg-gray-50 ${selectedIds.has(ad.id) ? 'bg-indigo-50' : ''}`}>
                  <td className="px-4 py-3">
                    <input
                      type="checkbox"
                      checked={selectedIds.has(ad.id)}
                      onChange={() => toggleOne(ad.id)}
                      className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                    />
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    <a
                      href={ad.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="hover:underline text-indigo-400"
                    >
                      {ad.title ?? '(no title)'}
                    </a>
                  </td>
                  <td className="px-4 py-3 text-gray-400">{ad.company ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-400">{ad.location ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-400">
                    {new Date(ad.scrapedAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-3 flex items-center gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => restoreAd.mutate(ad.id)}
                      loading={restoreAd.isPending}
                    >
                      Restore
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        if (confirm('Permanently delete this job ad? It will be re-scraped on the next run.')) {
                          deleteAd.mutate(ad.id);
                        }
                      }}
                      className="text-red-500 hover:text-red-700"
                    >
                      Delete
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
