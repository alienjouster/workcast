'use client';

import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

export interface UseJobAdsParams {
  boardIds?: string[];
  excludeBoardIds?: string[];
  titles?: string[];
  excludeTitles?: string[];
  locations?: string[];
  excludeLocations?: string[];
  companies?: string[];
  excludeCompanies?: string[];
  isActive?: boolean;
  isRead?: boolean;
  isPinned?: boolean;
  minScore?: number;
  trashed?: boolean;
}

interface UseJobAdsOptions {
  // Set to false for queries whose content only changes via explicit user actions
  // (e.g. trash bin). Skips the 60 s fallback poll; SSE events still trigger
  // refetches when relevant.
  poll?: boolean;
  // Set to false to disable the query entirely (prevents the HTTP request).
  enabled?: boolean;
}

export function useJobAds(params: UseJobAdsParams = {}, { poll = true, enabled = true }: UseJobAdsOptions = {}) {
  return useInfiniteQuery({
    queryKey: ['job-ads', params],
    queryFn: ({ pageParam }) =>
      api.ads.list({ ...params, cursor: pageParam as string | undefined }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled,
    // Poll every 3 s while any ad has a scoring job in flight; otherwise 60 s
    // as a fallback for missed SSE runCompleted events.
    // Disabled for trash queries: trash content only changes via user actions,
    // which already trigger cache invalidation via mutation onSuccess handlers.
    refetchInterval: !poll ? false : (query) => {
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
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['job-ads'] });
      qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
    },
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

export function useBulkAction() {
  const qc = useQueryClient();
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['job-ads'] });
    qc.invalidateQueries({ queryKey: ['job-ads-unread-count'] });
  };
  const pin      = useMutation({ mutationFn: (ids: string[]) => api.ads.bulkPin(ids),        onSuccess: invalidate });
  const unpin    = useMutation({ mutationFn: (ids: string[]) => api.ads.bulkUnpin(ids),      onSuccess: invalidate });
  const read     = useMutation({ mutationFn: (ids: string[]) => api.ads.bulkMarkRead(ids),   onSuccess: invalidate });
  const unread   = useMutation({ mutationFn: (ids: string[]) => api.ads.bulkMarkUnread(ids), onSuccess: invalidate });
  const trash    = useMutation({ mutationFn: (ids: string[]) => api.ads.bulkTrash(ids),      onSuccess: invalidate });
  const isPending = pin.isPending || unpin.isPending || read.isPending || unread.isPending || trash.isPending;
  return { pin, unpin, read, unread, trash, isPending };
}

export function useSetNote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, note }: { id: string; note: string | null }) => api.ads.setNote(id, note),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-ads'] }),
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
