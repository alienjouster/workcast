'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { CreateJobBoardRequest, UpdateJobBoardRequest, UpdateScraperConfigRequest } from '@/types';

export function useJobBoards() {
  return useQuery({
    queryKey: ['job-boards'],
    queryFn: () => api.boards.list(),
    refetchInterval: (query) => {
      const boards = query.state.data;
      if (boards?.some((b) => b.status === 'pending')) return 3000;
      return 30_000;
    },
  });
}

export function useJobBoard(id: string) {
  return useQuery({
    queryKey: ['job-boards', id],
    queryFn: () => api.boards.get(id),
    refetchInterval: (query) =>
      query.state.data?.status === 'pending' ? 3000 : false,
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
