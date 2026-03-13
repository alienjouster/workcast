'use client';

import Link from 'next/link';
import { Badge } from '@/components/ui/Badge';
import type { JobBoard } from '@/types';

interface BoardCardProps {
  board: JobBoard;
}

function formatDate(iso: string | null) {
  if (!iso) return 'Never';
  return new Date(iso).toLocaleString();
}

export function BoardCard({ board }: BoardCardProps) {
  return (
    <Link
      href={`/boards/${board.id}`}
      className="block bg-white rounded-lg border border-gray-200 shadow-sm hover:shadow-md hover:border-indigo-300 transition-all p-5"
    >
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="font-semibold text-gray-900 truncate">
              {board.name ?? board.url}
            </h3>
            <Badge status={board.status} />
          </div>
          {board.name && (
            <p className="text-xs text-gray-500 truncate mb-2">{board.url}</p>
          )}
          <div className="flex items-center gap-4 text-xs text-gray-500">
            <span>{board.adCount} ads</span>
            <span>Last scraped: {formatDate(board.lastScrapedAt)}</span>
            <span className="font-mono">{board.scheduleCron}</span>
          </div>
        </div>
      </div>
    </Link>
  );
}
