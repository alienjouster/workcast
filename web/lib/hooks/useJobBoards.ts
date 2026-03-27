'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { STALE_TIMES } from '@/lib/constants';
import type { CreateJobBoardRequest, UpdateJobBoardRequest, UpdateScraperConfigRequest } from '@/types';

export function useJobBoards() {
  return useQuery({
    queryKey: ['job-boards'],
    queryFn: () => api.boards.list(),
    staleTime: STALE_TIMES.MEDIUM,
    // SSE handles instant updates; slow poll catches boards whose status changed
    // while this component was not the active SSE recipient.
    refetchInterval: (query) => {
      const boards = query.state.data;
      return boards?.some((b) => b.status === 'pending' || b.hasActiveRun) ? 3_000 : false;
    },
  });
}

export function useJobBoard(id: string) {
  return useQuery({
    queryKey: ['job-boards', id],
    queryFn: () => api.boards.get(id),
    staleTime: STALE_TIMES.MEDIUM,
    // SSE handles instant updates; poll every 3 s as a fallback while board
    // analysis is in progress in case the boardStatusChanged event is delayed.
    refetchInterval: (query) =>
      query.state.data?.status === 'pending' ? 3_000 : false,
  });
}

export function useCreateBoard() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateJobBoardRequest) => api.boards.create(data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-boards'] }),
  });
}

export function useUpdateBoard(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateJobBoardRequest) => api.boards.update(id, data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['job-boards'] });
      qc.invalidateQueries({ queryKey: ['job-boards', id] });
    },
  });
}

export function useDeleteBoard() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.boards.delete(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-boards'] }),
  });
}

export function useRefreshBoard() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.boards.refresh(id),
    onSuccess: (_data, id) => {
      qc.invalidateQueries({ queryKey: ['job-boards', id] });
      qc.invalidateQueries({ queryKey: ['scrape-runs', id] });
    },
  });
}

export function useUpdateScraperConfig(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateScraperConfigRequest) => api.boards.updateScraperConfig(id, data),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['job-boards', id] }),
  });
}

export function useReanalyzeBoard() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.boards.reanalyze(id),
    onSuccess: (_data, id) => qc.invalidateQueries({ queryKey: ['job-boards', id] }),
  });
}
