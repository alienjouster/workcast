import type { BoardStatus, RunStatus } from '@/types';

const STATUS_CLASSES: Record<string, string> = {
  pending: 'bg-yellow-100 text-yellow-800',
  active: 'bg-green-100 text-green-800',
  paused: 'bg-gray-100 text-gray-700',
  error: 'bg-red-100 text-red-800',
  running: 'bg-blue-100 text-blue-800',
  completed: 'bg-green-100 text-green-800',
  failed: 'bg-red-100 text-red-800',
  partial: 'bg-yellow-100 text-yellow-800',
};

interface BadgeProps {
  status: BoardStatus | RunStatus | string;
  label?: string;
}

export function Badge({ status, label }: BadgeProps) {
  const classes = STATUS_CLASSES[status] ?? 'bg-gray-100 text-gray-700';
  return (
    <span
      className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${classes}`}
    >
      {label ?? status}
    </span>
  );
}
