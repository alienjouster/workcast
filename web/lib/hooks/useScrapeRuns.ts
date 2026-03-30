'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { isActiveRunStatus } from '@/types';
import { STALE_TIMES } from '@/lib/constants';

export function useScrapeRuns(boardId: string, limit?: number) {
  return useQuery({
    queryKey: ['scrape-runs', boardId, limit],
    queryFn: () => api.boards.listRuns(boardId, limit),
    staleTime: STALE_TIMES.MEDIUM,
    // SSE handles instant updates. Polling provides two fallback tiers:
    //   3 s while a run is active  — tracks progress if SSE is delayed
    //  30 s otherwise              — catches scheduler-triggered runs that start
    //                                with no prior user action on this page
    refetchInterval: (query) =>
      query.state.data?.some((r) => isActiveRunStatus(r.status)) ? 3000 : false,
  });
}

export function useAllScrapeRuns(limit?: number) {
  return useQuery({
    queryKey: ['scrape-runs-all', limit],
    queryFn: () => api.runs.list(limit),
    staleTime: STALE_TIMES.MEDIUM,
    refetchInterval: (query) =>
      query.state.data?.some((r) => isActiveRunStatus(r.status)) ? 3000 : false,
  });
}

export function useScrapeRun(id: string) {
  return useQuery({
    queryKey: ['scrape-runs', 'detail', id],
    queryFn: () => api.runs.get(id),
    staleTime: STALE_TIMES.MEDIUM,
    refetchInterval: (query) =>
      query.state.data ? isActiveRunStatus(query.state.data.status) ? 3000 : false : false,
  });
}
