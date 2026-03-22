'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useIsProcessing() {
  return useQuery({
    queryKey: ['processing-status'],
    queryFn: () => api.status.isProcessing(),
    refetchInterval: 10_000,
    select: (data) => data.isProcessing,
  });
}
