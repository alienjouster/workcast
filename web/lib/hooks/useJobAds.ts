'use client';

import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
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

export function usePinAd() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, pinned }: { id: string; pinned: boolean }) =>
      pinned ? api.ads.unpin(id) : api.ads.pin(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-ads'] }),
  });
}

export function useMarkAdRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, read }: { id: string; read: boolean }) =>
      read ? api.ads.markUnread(id) : api.ads.markRead(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['job-ads'] });
      qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
    },
  });
}

export function useMarkAllRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (boardId?: string) => api.ads.markAllRead(boardId),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['job-ads'] });
      qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
    },
  });
}

export function useUnreadCount() {
  return useQuery({
    queryKey: ['job-ads-unread-count'],
    queryFn: () => api.ads.unreadCount(),
    refetchInterval: 60_000,
  });
}
