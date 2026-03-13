'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useScrapeRuns(boardId: string, limit?: number) {
  return useQuery({
    queryKey: ['scrape-runs', boardId, limit],
    queryFn: () => api.boards.listRuns(boardId, limit),
    refetchInterval: (query) => {
      const runs = query.state.data;
      if (runs?.some((r) => r.status === 'running')) return 5000;
      return false;
    },
  });
}

export function useScrapeRun(id: string) {
  return useQuery({
    queryKey: ['scrape-runs', 'detail', id],
    queryFn: () => api.runs.get(id),
    refetchInterval: (query) =>
      query.state.data?.status === 'running' ? 5000 : false,
  });
}
