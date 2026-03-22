'use client';

import React, { useState } from 'react';
import type { JobAd, ScoringCategory } from '@/types';
import { Button } from '@/components/ui/Button';
import { useMarkAdRead, usePinAd, useTrashAd } from '@/lib/hooks/useJobAds';
import { useAdScoring, useRunScoring } from '@/lib/hooks/useAdScoring';
import { useSettings } from '@/lib/hooks/useSettings';

// ── Helpers ──────────────────────────────────────────────────────────────────

function timeAgo(date: string): string {
  const seconds = Math.floor((Date.now() - new Date(date).getTime()) / 1000);
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
  if (seconds < 60)   return rtf.format(-seconds, 'second');
  if (seconds < 3600) return rtf.format(-Math.floor(seconds / 60), 'minute');
  if (seconds < 86400) return rtf.format(-Math.floor(seconds / 3600), 'hour');
  return rtf.format(-Math.floor(seconds / 86400), 'day');
}

// ── Scoring sub-component ────────────────────────────────────────────────────

const CATEGORY_STYLES: Record<ScoringCategory, { label: string; className: string }> = {
  match:         { label: 'Match',   className: 'bg-green-100 text-green-800' },
  partial_match: { label: 'Partial', className: 'bg-amber-100 text-amber-800' },
  gap:           { label: 'Gap',     className: 'bg-red-100   text-red-800'   },
};

function AdScoringPanel({ adId, isScoringPending }: { adId: string; isScoringPending: boolean }) {
  const { data: scoring, isLoading, isFetching } = useAdScoring(adId, isScoringPending);
  const { data: settings } = useSettings();
  const runScoring = useRunScoring();

  const hasResume = settings?.hasResume ?? false;
  const isRunning = runScoring.isPending || isFetching || isScoringPending;

  return (
    <div className="mt-4 border-t border-gray-200 pt-3">
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Ad matching score
          {scoring?.scoredAt && (
            <span className="ml-2 normal-case font-normal text-gray-400">{timeAgo(scoring.scoredAt)}</span>
          )}
        </span>
        <div className="flex items-center gap-2">
          {!hasResume && (
            <span className="text-xs text-gray-400 italic">
              Upload a resume from the{' '}
              <a href="/settings" className="text-indigo-500 hover:underline">Settings page</a>
            </span>
          )}
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
            {isLoading ? 'Loading…' : scoring ? 'Re-score' : 'Run scoring'}
          </Button>
        </div>
      </div>

      {isRunning && !scoring && (
        <p className="text-xs text-gray-400 italic">Analysing…</p>
      )}

      {scoring && (
        <div className="space-y-3">
          {/* Score + summary box */}
          <div className="flex items-start gap-4 rounded-lg border border-gray-200 bg-white px-4 py-3">
            <span className="shrink-0 text-3xl font-bold text-gray-800 leading-none pt-0.5">
              {Math.round(scoring.overallScore)}<span className="text-base font-normal text-gray-400">/100</span>
            </span>
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
          <div className="rounded-lg border border-gray-200 bg-white overflow-hidden text-xs">
            {(['match', 'partial_match', 'gap'] as ScoringCategory[]).map((cat) => {
              const items = scoring.requirements.filter(r => r.category === cat);
              const style = CATEGORY_STYLES[cat];
              return (
                <div key={cat}>
                  <div className={`px-4 py-1.5 font-semibold text-[11px] uppercase tracking-wide border-b border-gray-100 ${style.className}`}>
                    {style.label}
                  </div>
                  <div className="px-4 py-2 border-b border-gray-100 last:border-b-0">
                    {items.length === 0 ? (
                      <p className="text-gray-300 italic">N/A</p>
                    ) : (
                      <div className="grid" style={{ gridTemplateColumns: 'minmax(0, 30%) 1fr' }}>
                        {items.map((req, i) => (
                          <React.Fragment key={i}>
                            <div className={`py-1 pr-3 text-gray-700 break-words ${i > 0 ? 'border-t border-gray-100' : ''}`}>
                              {req.name}
                              {req.isOptional && <span className="ml-1 text-[10px] text-gray-400">(opt)</span>}
                            </div>
                            <div className={`py-1 text-gray-400 ${i > 0 ? 'border-t border-gray-100' : ''}`}>{req.notes ?? ''}</div>
                          </React.Fragment>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

        </div>
      )}
    </div>
  );
}

interface AdTableProps {
  ads: JobAd[];
}

export function AdTable({ ads }: AdTableProps) {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const trashAd = useTrashAd();
  const pinAd = usePinAd();
  const markRead = useMarkAdRead();

  if (ads.length === 0) return null;

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left font-medium text-gray-500 w-8"></th>
            <th className="px-4 py-3 text-left font-medium text-gray-500 w-8"></th>
            <th className="px-4 py-3 text-left font-medium text-gray-500 w-16">
              <span className="flex items-center gap-1">
                Match
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5 text-indigo-400">
                  <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
                </svg>
              </span>
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
            <React.Fragment key={ad.id}>
              <tr
                key={ad.id}
                className={`cursor-pointer ${ad.isPinned ? 'bg-amber-50 hover:bg-amber-100' : 'hover:bg-gray-50'}`}
                onClick={() => {
                  const isOpening = expandedId !== ad.id;
                  setExpandedId(isOpening ? ad.id : null);
                  if (isOpening && !ad.isRead) markRead.mutate({ id: ad.id, read: false });
                }}
              >
                <td className="px-4 py-3">
                  <button
                    title={ad.isPinned ? 'Unpin' : 'Pin to top'}
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
                </td>
                <td className="px-4 py-3">
                  <button
                    title={ad.isRead ? 'Mark as unread' : 'Mark as read'}
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
                </td>
                <td className="px-4 py-3 text-xs font-medium tabular-nums">
                  {ad.overallScore != null ? (
                    <span className={
                      ad.overallScore >= 70 ? 'text-green-600' :
                      ad.overallScore >= 40 ? 'text-amber-500' : 'text-red-500'
                    }>
                      {Math.round(ad.overallScore)}%
                    </span>
                  ) : (
                    <span className="text-gray-300">—</span>
                  )}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    {!ad.isActive && (
                      <span className="text-xs bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded">
                        Inactive
                      </span>
                    )}
                    <a
                      href={ad.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={`hover:underline ${ad.isRead ? 'font-normal text-indigo-400' : 'font-semibold text-indigo-700'}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        if (!ad.isRead) markRead.mutate({ id: ad.id, read: false });
                      }}
                    >
                      {ad.title ?? '(no title)'}
                    </a>
                  </div>
                </td>
                <td className={`px-4 py-3 ${ad.isRead ? 'text-gray-400' : 'text-gray-700'}`}>{ad.company ?? '—'}</td>
                <td className={`px-4 py-3 ${ad.isRead ? 'text-gray-400' : 'text-gray-700'}`}>{ad.location ?? '—'}</td>
                <td className="px-4 py-3 text-gray-500">
                  {new Date(ad.scrapedAt).toLocaleDateString()}
                </td>
                <td className="px-4 py-3">
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
                </td>
              </tr>
              {expandedId === ad.id && (
                <tr key={`${ad.id}-expand`}>
                  <td colSpan={8} className="px-4 py-4 bg-gray-50">
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
                    <AdScoringPanel adId={ad.id} isScoringPending={ad.isScoringPending} />
                  </td>
                </tr>
              )}
            </React.Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
}
