'use client';

import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

interface UseJobAdsParams {
  boardId?: string;
  search?: string;
  isActive?: boolean;
  trashed?: boolean;
}

export function useJobAds(params: UseJobAdsParams = {}) {
  return useInfiniteQuery({
    queryKey: ['job-ads', params],
    queryFn: ({ pageParam }) =>
      api.ads.list({ ...params, trashed: params.trashed, cursor: pageParam as string | undefined }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    // Poll every 3 s while any ad has a scoring job in flight; otherwise 60 s
    // as a fallback for missed SSE runCompleted events.
    refetchInterval: (query) => {
      const hasPending = query.state.data?.pages
        .flatMap((p) => p.items)
        .some((a) => a.isScoringPending);
      return hasPending ? 3_000 : 60_000;
    },
  });
}

export function useDeleteAd() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.ads.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['job-ads'] });
      qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
    },
  });
}

export function useTrashAd() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.ads.trash(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-ads'] }),
  });
}

export function useRestoreAd() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.ads.restore(id),
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
    // Poll every 60 s as a fallback for missed SSE runCompleted events.
    refetchInterval: 60_000,
  });
}
