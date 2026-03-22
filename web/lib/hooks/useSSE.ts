'use client';

import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';

interface SseEvent {
  type: 'boardStatusChanged' | 'runStarted' | 'runCompleted' | 'unreadCountChanged' | 'scoringCompleted';
  boardId?: string;
  runId?: string;
  adId?: string;
  status?: string;
  adsNew?: number;
  unreadCount?: number;
}

export function useSSE() {
  const qc = useQueryClient();

  useEffect(() => {
    const es = new EventSource('/api/events');

    es.onopen = () => {
      // Resync all data when the SSE connection (re)establishes — guards against missed
      // events during a brief disconnection.
      qc.invalidateQueries({ queryKey: ['job-boards'] });
      qc.invalidateQueries({ queryKey: ['scrape-runs'] });
      qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
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

        case 'runStarted':
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          break;

        case 'runCompleted':
          qc.invalidateQueries({ queryKey: ['scrape-runs', event.boardId] });
          qc.invalidateQueries({ queryKey: ['scrape-runs', 'detail', event.runId] });
          qc.invalidateQueries({ queryKey: ['job-boards', event.boardId] });
          qc.invalidateQueries({ queryKey: ['job-boards'] });
          qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
          qc.invalidateQueries({ queryKey: ['job-ads'] });
          break;

        case 'unreadCountChanged':
          qc.setQueryData(['job-ads-unread-count'], event.unreadCount);
          break;

        case 'scoringCompleted':
          // Use refetchQueries (not invalidate) to force a fetch even if the query
          // is in error state (e.g. a 404 from a poll before the result was ready).
          qc.refetchQueries({ queryKey: ['ad-scoring', event.adId] });
          // Refresh the ads list so isScoringPending clears and overallScore updates.
          qc.invalidateQueries({ queryKey: ['job-ads'] });
          break;
      }
    };

    return () => es.close();
  }, [qc]);
}
