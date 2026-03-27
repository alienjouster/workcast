'use client';

import { useInfiniteQuery, useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { STALE_TIMES } from '@/lib/constants';

export interface ApplicationsFilter {
  titles?: string[];
  excludeTitles?: string[];
  locations?: string[];
  excludeLocations?: string[];
  companies?: string[];
  excludeCompanies?: string[];
  minScore?: number;
  trashed?: boolean;
}

export function useApplications(params: ApplicationsFilter = {}, { enabled = true }: { enabled?: boolean } = {}) {
  return useInfiniteQuery({
    queryKey: ['applications', params],
    queryFn: ({ pageParam }) =>
      api.applications.list({ ...params, cursor: pageParam as string | undefined }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    staleTime: STALE_TIMES.MEDIUM,
    enabled,
  });
}

export function useApplication(id: string) {
  return useQuery({
    queryKey: ['applications', id],
    queryFn: () => api.applications.get(id),
    staleTime: STALE_TIMES.LONG,
  });
}

export function useCreateApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (jobAdId: string) => api.applications.create(jobAdId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['applications'] });
    },
  });
}

export function useTrashApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.applications.trash(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['applications'] }),
  });
}

export function useRestoreApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.applications.restore(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['applications'] }),
  });
}

export function useDeleteApplication() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.applications.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['applications'] }),
  });
}

export function useUpdateApplicationJobAdContent(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (content: string | null) => api.applications.updateJobAdContent(id, content),
    onSuccess: (updated) => {
      queryClient.setQueryData(['applications', id], updated);
    },
  });
}

export function useCancelApplicationScoring(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.applications.cancelScoring(id),
    onSuccess: () => {
      queryClient.setQueryData<import('@/types').Application>(['applications', id], (old) =>
        old ? { ...old, isScoringPending: false } : old
      );
    },
  });
}

export function useRunApplicationScoring(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.applications.score(id),
    onSuccess: () => {
      // Optimistically mark as pending so the button disables immediately.
      queryClient.setQueryData<import('@/types').Application>(['applications', id], (old) =>
        old ? { ...old, isScoringPending: true, lastScoringError: null } : old
      );
    },
  });
}
