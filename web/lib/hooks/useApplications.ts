'use client';

import { useInfiniteQuery, useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

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

export function useApplications(params: ApplicationsFilter = {}) {
  return useInfiniteQuery({
    queryKey: ['applications', params],
    queryFn: ({ pageParam }) =>
      api.applications.list({ ...params, cursor: pageParam as string | undefined }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    staleTime: 30_000,
  });
}

export function useApplication(id: string) {
  return useQuery({
    queryKey: ['applications', id],
    queryFn: () => api.applications.get(id),
    staleTime: 60_000,
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
