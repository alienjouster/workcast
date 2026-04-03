'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { STALE_TIMES } from '@/lib/constants';
import type { UpdateSettingsRequest } from '@/types';

const QUERY_KEY = ['settings'] as const;

export function useSettings() {
  return useQuery({
    queryKey: QUERY_KEY,
    queryFn: () => api.settings.get(),
    staleTime: STALE_TIMES.LONG,
  });
}

export function useUpdateSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: UpdateSettingsRequest) => api.settings.update(data),
    onSuccess: (updated) => {
      queryClient.setQueryData(QUERY_KEY, updated);
    },
  });
}

export function useUploadResume() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => api.settings.uploadResume(file),
    onSuccess: (updated) => {
      queryClient.setQueryData(QUERY_KEY, updated);
    },
  });
}

export function useDeleteResume() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api.settings.deleteResume(),
    onSuccess: (updated) => {
      queryClient.setQueryData(QUERY_KEY, updated);
    },
  });
}

export function useUploadResumeTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (file: File) => api.settings.uploadResumeTemplate(file),
    onSuccess: (updated) => {
      queryClient.setQueryData(QUERY_KEY, updated);
    },
  });
}

export function useDeleteResumeTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api.settings.deleteResumeTemplate(),
    onSuccess: (updated) => {
      queryClient.setQueryData(QUERY_KEY, updated);
    },
  });
}

