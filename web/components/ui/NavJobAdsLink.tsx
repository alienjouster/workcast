'use client';

import Link from 'next/link';
import { useUnreadCount } from '@/lib/hooks/useJobAds';

export function NavJobAdsLink() {
  const { data: unreadCount } = useUnreadCount();

  return (
    <Link
      href="/ads"
      className="flex items-center gap-1.5 text-sm font-medium text-gray-700 hover:text-indigo-600 transition-colors"
    >
      Job Ads
      {unreadCount != null && unreadCount > 0 && (
        <span className="inline-flex items-center justify-center min-w-[1.25rem] h-5 px-1 rounded-full bg-red-500 text-white text-xs font-semibold leading-none">
          {unreadCount > 999 ? '999+' : unreadCount}
        </span>
      )}
    </Link>
  );
}
