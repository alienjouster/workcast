'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { useCreateBoard } from '@/lib/hooks/useJobBoards';

interface AddBoardFormProps {
  onClose: () => void;
}

export function AddBoardForm({ onClose }: AddBoardFormProps) {
  const [url, setUrl] = useState('');
  const [name, setName] = useState('');
  const [scheduleCron, setScheduleCron] = useState('');
  const [error, setError] = useState<string | null>(null);
  const createBoard = useCreateBoard();

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    try {
      await createBoard.mutateAsync({
        url,
        name: name.trim() || undefined,
        scheduleCron: scheduleCron.trim() || undefined,
      });
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to register board');
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Job Board URL <span className="text-red-500">*</span>
        </label>
        <input
          type="url"
          required
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          placeholder="https://example-jobs.com/careers"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Name <span className="text-gray-400 font-normal">(optional)</span>
        </label>
        <input
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Acme Corp Jobs"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Schedule (cron){' '}
          <span className="text-gray-400 font-normal">(optional, default: every hour)</span>
        </label>
        <input
          type="text"
          value={scheduleCron}
          onChange={(e) => setScheduleCron(e.target.value)}
          placeholder="0 * * * *"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>
      {error && (
        <p className="text-sm text-red-600 bg-red-50 rounded-md px-3 py-2">{error}</p>
      )}
      <div className="flex justify-end gap-3 pt-2">
        <Button variant="secondary" type="button" onClick={onClose}>
          Cancel
        </Button>
        <Button variant="primary" type="submit" loading={createBoard.isPending}>
          Register Board
        </Button>
      </div>
    </form>
  );
}
