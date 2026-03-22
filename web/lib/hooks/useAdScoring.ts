'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useAdScoring(adId: string) {
  return useQuery({
    queryKey: ['ad-scoring', adId],
    queryFn: () => api.scoring.get(adId),
    // 404 when no scoring exists yet — treat as null rather than error.
    retry: false,
    throwOnError: false,
  });
}

export function useRunScoring() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (adId: string) => api.scoring.run(adId),
    // No optimistic update — result arrives via SSE scoringCompleted event.
    onSuccess: (_data, adId) => {
      // Invalidate immediately after triggering so any stale result is cleared.
      qc.removeQueries({ queryKey: ['ad-scoring', adId] });
    },
  });
}
