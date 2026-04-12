'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useIsProcessing() {
  return useQuery({
    queryKey: ['status'],
    queryFn: () => api.status.get(),
    refetchInterval: 10_000,
    select: (data) => data.isProcessing,
  });
}

export function useUnreadCount() {
  return useQuery({
    queryKey: ['status'],
    queryFn: () => api.status.get(),
    refetchInterval: 10_000,
    select: (data) => data.unreadCount,
  });
}

export function useAiKeyError() {
  return useQuery({
    queryKey: ['status'],
    queryFn: () => api.status.get(),
    refetchInterval: 10_000,
    select: (data) => data.aiKeyError,
  });
}
