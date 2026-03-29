'use client';

import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import type { UseJobAdsParams } from '@/lib/hooks/useJobAds';

type SseEvent =
  | { type: 'boardStatusChanged'; boardId: string }
  | { type: 'runEnqueued'; boardId: string; runId: string }
  | { type: 'runStarted'; boardId: string; runId: string }
  | { type: 'runStatusChanged'; boardId: string; runId: string; status: string }
  | { type: 'runCompleted'; boardId: string; runId: string }
  | { type: 'unreadCountChanged'; unreadCount: number }
  | { type: 'scoringCompleted'; adId: string }
  | { type: 'applicationScoringCompleted'; applicationId: string }
  | { type: 'applicationResumeGenerationCompleted'; applicationId: string };

export function useSSE() {
  const qc = useQueryClient();

  useEffect(() => {
    const es = new EventSource('/api/events');

    es.onopen = () => {
      // Resync all data when the SSE connection (re)establishes — guards against missed
      // events during a brief disconnection.
      qc.invalidateQueries({ queryKey: ['job-boards'] });
      qc.invalidateQueries({ queryKey: ['scrape-runs'] });
      qc.invalidateQueries({ queryKey: ['scrape-runs-all'] });
      qc.invalidateQueries({ queryKey: ['status'] });
    };

    es.onmessage = (e: MessageEvent) => {
      const event = JSON.parse(e.data as string) as SseEvent;

      switch (event.type) {
        case 'boardStatusChanged':
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          // Also refresh runs: analysis completion immediately enqueues a scrape job,
          // so the runs list should update at the same time the board status changes.
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          break;

        case 'runEnqueued':
          // A new run record has been created — refresh runs list and board (hasActiveRun).
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          qc.invalidateQueries({ queryKey: ['scrape-runs-all'] });
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          break;

        case 'runStarted':
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          qc.invalidateQueries({ queryKey: ['scrape-runs-all'] });
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          break;

        case 'runStatusChanged':
          // Intermediate Hangfire state change (scheduled, awaiting, enqueued retry, deleted).
          // Only refresh runs — job-ads and board metadata are not affected.
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          qc.invalidateQueries({ queryKey: ['scrape-runs-all'] });
          qc.invalidateQueries({ queryKey: ['scrape-runs', 'detail', event.runId] });
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          break;

        case 'runCompleted':
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          qc.invalidateQueries({ queryKey: ['scrape-runs-all'] });
          qc.invalidateQueries({ queryKey: ['scrape-runs', 'detail', event.runId] });
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          qc.invalidateQueries({ queryKey: ['status'] });
          // Scraped ads never land in the trash bin, so only refresh non-trashed queries.
          qc.invalidateQueries({
            predicate: (query) =>
              query.queryKey[0] === 'job-ads' &&
              !(query.queryKey[1] as UseJobAdsParams | undefined)?.trashed,
          });
          break;

        case 'unreadCountChanged':
          qc.setQueryData<{ isProcessing: boolean; unreadCount: number }>(
            ['status'],
            (old) => old ? { ...old, unreadCount: event.unreadCount } : old,
          );
          break;

        case 'scoringCompleted':
          // Use refetchQueries (not invalidate) to force a fetch even if the query
          // is in error state (e.g. a 404 from a poll before the result was ready).
          qc.refetchQueries({ queryKey: ['ad-scoring', event.adId] });
          // Refresh the ads list so isScoringPending clears and overallScore updates.
          // Scoring is only triggered from the main list, not from the trash bin.
          qc.invalidateQueries({
            predicate: (query) =>
              query.queryKey[0] === 'job-ads' &&
              !(query.queryKey[1] as UseJobAdsParams | undefined)?.trashed,
          });
          break;

        case 'applicationScoringCompleted':
          // Refresh the individual application so the scoring snapshot, pending flag,
          // and any error message are all up to date.
          qc.invalidateQueries({ queryKey: ['applications', event.applicationId] });
          // Also refresh the applications list so overallScore column updates.
          qc.invalidateQueries({
            predicate: (query) =>
              query.queryKey[0] === 'applications' &&
              typeof query.queryKey[1] !== 'string',
          });
          break;

        case 'applicationResumeGenerationCompleted':
          // Refresh the application so isResumeGenerationPending clears and any error shows.
          qc.invalidateQueries({ queryKey: ['applications', event.applicationId] });
          // Refresh the generated resume so the new HTML appears immediately.
          qc.invalidateQueries({ queryKey: ['generated-resume', event.applicationId] });
          break;
      }
    };

    return () => es.close();
  }, [qc]);
}
