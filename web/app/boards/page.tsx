'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { AddBoardForm } from '@/components/boards/AddBoardForm';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { Card, CardBody } from '@/components/ui/Card';

function timeAgo(iso: string) {
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 60) return 'just now';
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;
  return `${Math.floor(months / 12)}y ago`;
}

export default function BoardsPage() {
  const router = useRouter();
  const [showForm, setShowForm] = useState(false);
  const { data: boards, isLoading, error } = useJobBoards();

  if (isLoading) return <LoadingSpinner />;
  if (error) {
    return (
      <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
        Failed to load boards: {error.message}
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Job Boards</h1>
          <p className="text-sm text-gray-500 mt-1">
            {boards?.length ?? 0} board{boards?.length !== 1 ? 's' : ''} registered
          </p>
        </div>
        <Button variant="primary" onClick={() => setShowForm((v) => !v)}>
          {showForm ? 'Cancel' : '+ Add Board'}
        </Button>
      </div>

      {showForm && (
        <Card className="mb-6">
          <CardBody>
            <h2 className="text-base font-semibold text-gray-900 mb-4">Register New Board</h2>
            <AddBoardForm onClose={() => setShowForm(false)} />
          </CardBody>
        </Card>
      )}

      {!boards || boards.length === 0 ? (
        <EmptyState
          title="No boards registered"
          description="Register a job board URL and let Workcast handle the rest — AI-driven analysis, scraping, and job ad extraction."
          action={
            <Button variant="primary" onClick={() => setShowForm(true)}>
              + Add Your First Board
            </Button>
          }
        />
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Name</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Status</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Ads</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Last scraped</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Schedule</th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {boards.map((board) => (
                <tr
                  key={board.id}
                  className="hover:bg-gray-50 cursor-pointer"
                  onClick={() => router.push(`/boards/${board.id}`)}
                >
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{board.name ?? board.url}</div>
                    {board.name && (
                      <div className="text-xs text-gray-400 truncate max-w-xs" title={board.url}>{board.url}</div>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <Badge status={board.status} />
                  </td>
                  <td className="px-4 py-3 text-gray-700">
                    {board.adCount}
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {board.hasActiveRun ? (
                      <span className="inline-flex items-center gap-1.5 text-indigo-600">
                        <svg className="w-3.5 h-3.5 animate-spin" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                          <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                          <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
                        </svg>
                        Running
                      </span>
                    ) : board.lastScrapedAt ? (
                      <span title={new Date(board.lastScrapedAt).toLocaleString()}>{timeAgo(board.lastScrapedAt)}</span>
                    ) : (
                      <span className="italic">Never</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <span className="font-mono text-xs bg-gray-100 px-2 py-0.5 rounded">{board.scheduleCron}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
