'use client';

import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';

const LS_KEY = 'workcast.notificationsEnabled';

export function notificationsEnabled(): boolean {
  if (typeof window === 'undefined') return false;
  return localStorage.getItem(LS_KEY) === 'true';
}

export function setNotificationsEnabled(value: boolean) {
  localStorage.setItem(LS_KEY, String(value));
}

export function useNotifications() {
  const qc = useQueryClient();
  const prevCount = useRef<number | null>(null);

  useEffect(() => {
    // Seed the ref from current cache state so the first data arrival doesn't
    // spuriously fire a notification — we only care about increases from this point.
    const current = qc.getQueryData<{ unreadCount: number }>(['status']);
    prevCount.current = current?.unreadCount ?? null;

    const unsubscribe = qc.getQueryCache().subscribe((event) => {
      if (event.type !== 'updated' || event.query.queryKey[0] !== 'status') return;

      const data = event.query.state.data as { unreadCount: number } | undefined;
      const newCount = data?.unreadCount ?? 0;
      const prev = prevCount.current ?? 0;

      if (newCount > prev && notificationsEnabled() && Notification.permission === 'granted') {
        const diff = newCount - prev;
        new Notification('Workcast — New job ads', {
          tag: 'workcast-new-ads',
          body: `${diff} new job ad${diff > 1 ? 's' : ''} found. ${newCount} unread total.`,
          icon: '/favicon.ico',
        });
      }

      prevCount.current = newCount;
    });

    return unsubscribe;
  }, [qc]);
}
