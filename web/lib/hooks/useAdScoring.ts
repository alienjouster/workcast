'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { InfiniteData } from '@tanstack/react-query';
import type { PagedResponse, JobAd } from '@/types';
import { api } from '@/lib/api';

export function useAdScoring(adId: string, isScoringPending = false) {
  return useQuery({
    queryKey: ['ad-scoring', adId],
    // Return null on 404 so the query stays in success state (not error state).
    // Error state prevents refetchInterval and invalidateQueries from re-fetching.
    queryFn: async () => {
      try {
        return await api.scoring.get(adId);
      } catch {
        return null;
      }
    },
    // Poll every 3 s while a scoring job is in flight so results appear as soon
    // as the job finishes, without relying solely on the SSE event.
    refetchInterval: isScoringPending ? 3_000 : false,
  });
}

export function useRunScoring() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (adId: string) => api.scoring.run(adId),
    onSuccess: (_data, adId) => {
      // Optimistically mark the ad as scoring-pending in all cached job-ads pages
      // so the button stays disabled immediately — before the next refetch arrives.
      qc.setQueriesData<InfiniteData<PagedResponse<JobAd>>>(
        { queryKey: ['job-ads'] },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            pages: old.pages.map((page) => ({
              ...page,
              items: page.items.map((ad) =>
                ad.id === adId ? { ...ad, isScoringPending: true } : ad
              ),
            })),
          };
        }
      );
      // Clear the previous result immediately so the table disappears while the new job runs.
      qc.setQueryData(['ad-scoring', adId], null);
    },
  });
}
