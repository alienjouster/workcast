'use client';

import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

interface UseJobAdsParams {
  boardId?: string;
  search?: string;
  isActive?: boolean;
}

export function useJobAds(params: UseJobAdsParams = {}) {
  return useInfiniteQuery({
    queryKey: ['job-ads', params],
    queryFn: ({ pageParam }) =>
      api.ads.list({ ...params, cursor: pageParam as string | undefined }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

export function useDeleteAd() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.ads.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-ads'] }),
  });
}
