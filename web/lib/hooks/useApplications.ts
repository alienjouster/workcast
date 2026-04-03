'use client';

import { useInfiniteQuery, useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import type { InfiniteData } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { STALE_TIMES } from '@/lib/constants';
import type { Application, ApplicationStatus, InterviewDrillPlan, PagedResponse, ResumeOptimizationLevel } from '@/types';

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

export function useLatestGeneratedResume(id: string) {
  return useQuery({
    queryKey: ['generated-resume', id],
    queryFn: () => api.applications.getLatestResume(id),
    staleTime: STALE_TIMES.LONG,
    retry: false,
  });
}

export function useGenerateResume(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (optimizationLevel: ResumeOptimizationLevel = 'None') =>
      api.applications.generateResume(id, optimizationLevel),
    onSuccess: () => {
      // Optimistically mark pending so the UI shows the spinner immediately,
      // without waiting for the SSE event.
      queryClient.setQueryData<import('@/types').Application>(
        ['applications', id],
        (old) => old ? { ...old, isResumeGenerationPending: true } : old,
      );
    },
  });
}

export function useUpdateGeneratedResume(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (htmlContent: string) => api.applications.updateLatestResume(id, htmlContent),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['resume-versions', id] });
      queryClient.invalidateQueries({ queryKey: ['generated-resume', id] });
    },
  });
}

export function useResumeVersions(id: string) {
  return useQuery({
    queryKey: ['resume-versions', id],
    queryFn: () => api.applications.listResumeVersions(id),
    staleTime: STALE_TIMES.LONG,
    enabled: !!id,
  });
}

export function useDeleteResumeVersion(applicationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (versionId: string) => api.applications.deleteResumeVersion(applicationId, versionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['resume-versions', applicationId] });
      queryClient.invalidateQueries({ queryKey: ['generated-resume', applicationId] });
    },
  });
}

export function useLatestGeneratedLetter(id: string) {
  return useQuery({
    queryKey: ['generated-letter', id],
    queryFn: () => api.applications.getLatestLetter(id),
    staleTime: STALE_TIMES.LONG,
    retry: false,
  });
}

export function useGenerateLetter(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.applications.generateLetter(id),
    onSuccess: () => {
      queryClient.setQueryData<import('@/types').Application>(
        ['applications', id],
        (old) => old ? { ...old, isLetterGenerationPending: true } : old,
      );
    },
  });
}

export function useUpdateGeneratedLetter(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (htmlContent: string) => api.applications.updateLatestLetter(id, htmlContent),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['letter-versions', id] });
      queryClient.invalidateQueries({ queryKey: ['generated-letter', id] });
    },
  });
}

export function useLetterVersions(id: string) {
  return useQuery({
    queryKey: ['letter-versions', id],
    queryFn: () => api.applications.listLetterVersions(id),
    staleTime: STALE_TIMES.LONG,
    enabled: !!id,
  });
}

export function useDeleteLetterVersion(applicationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (versionId: string) => api.applications.deleteLetterVersion(applicationId, versionId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['letter-versions', applicationId] });
      queryClient.invalidateQueries({ queryKey: ['generated-letter', applicationId] });
    },
  });
}

export function useUpdateApplicationPostedAt(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (postedAt: string | null) => api.applications.updatePostedAt(id, postedAt),
    onSuccess: (updated) => {
      queryClient.setQueryData(['applications', id], updated);
    },
  });
}

export function useUpdateApplicationScrapedAt(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (scrapedAt: string) => api.applications.updateScrapedAt(id, scrapedAt),
    onSuccess: (updated) => {
      queryClient.setQueryData(['applications', id], updated);
    },
  });
}

export function useUpdateApplicationStatus(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ status, achievedAt }: { status: ApplicationStatus; achievedAt?: string }) =>
      api.applications.updateStatus(id, status, achievedAt),
    onSuccess: (updated) => {
      queryClient.setQueryData(['applications', id], updated);
      queryClient.setQueriesData<InfiniteData<PagedResponse<Application>>>(
        { queryKey: ['applications'], predicate: (q) => typeof q.queryKey[1] !== 'string' },
        (old) => {
          if (!old) return old;
          return {
            ...old,
            pages: old.pages.map((page) => ({
              ...page,
              items: page.items.map((item) => item.id === id ? updated : item),
            })),
          };
        },
      );
    },
  });
}

export function useUpdateApplicationStatusDate(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ status, achievedAt }: { status: ApplicationStatus; achievedAt: string }) =>
      api.applications.updateStatusDate(id, status, achievedAt),
    onSuccess: (updated) => {
      queryClient.setQueryData(['applications', id], updated);
    },
  });
}

export function useInterviewDrillPlan(id: string) {
  return useQuery({
    queryKey: ['interview-drill', id],
    queryFn: () => api.applications.getInterviewDrill(id),
    staleTime: STALE_TIMES.LONG,
    retry: false,
  });
}

export function useGenerateInterviewDrill(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.applications.generateInterviewDrill(id),
    onSuccess: () => {
      queryClient.setQueryData<Application>(
        ['applications', id],
        (old) => old ? { ...old, isInterviewDrillPending: true, lastInterviewDrillError: null } : old,
      );
    },
  });
}

export function useCancelInterviewDrill(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.applications.cancelInterviewDrill(id),
    onSuccess: () => {
      queryClient.setQueryData<Application>(
        ['applications', id],
        (old) => old ? { ...old, isInterviewDrillPending: false } : old,
      );
    },
  });
}
