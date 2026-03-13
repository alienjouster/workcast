'use client';

import { useState } from 'react';
import { useJobBoards } from '@/lib/hooks/useJobBoards';
import { BoardCard } from '@/components/boards/BoardCard';
import { AddBoardForm } from '@/components/boards/AddBoardForm';
import { Button } from '@/components/ui/Button';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { Card, CardBody } from '@/components/ui/Card';

export default function BoardsPage() {
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
        <div className="grid gap-4 sm:grid-cols-1 lg:grid-cols-2">
          {boards.map((board) => (
            <BoardCard key={board.id} board={board} />
          ))}
        </div>
      )}
    </div>
  );
}
