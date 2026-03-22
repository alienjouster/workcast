'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useScrapeRuns(boardId: string, limit?: number) {
  return useQuery({
    queryKey: ['scrape-runs', boardId, limit],
    queryFn: () => api.boards.listRuns(boardId, limit),
    // SSE handles instant updates. Polling provides two fallback tiers:
    //   3 s while a run is active  — tracks progress if SSE is delayed
    //  30 s otherwise              — catches scheduler-triggered runs that start
    //                                with no prior user action on this page
    refetchInterval: (query) =>
      query.state.data?.some((r) => r.status === 'running') ? 3000 : 30_000,
  });
}

export function useScrapeRun(id: string) {
  return useQuery({
    queryKey: ['scrape-runs', 'detail', id],
    queryFn: () => api.runs.get(id),
    refetchInterval: (query) =>
      query.state.data?.status === 'running' ? 3000 : false,
  });
}
