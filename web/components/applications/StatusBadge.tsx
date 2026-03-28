import type { ApplicationStatus } from '@/types';

export const STATUS_BADGE: Record<ApplicationStatus, { label: string; cls: string }> = {
  ToApply:        { label: 'Preparing',    cls: 'bg-gray-100 text-gray-600'     },
  Applied:        { label: 'Applied',      cls: 'bg-blue-100 text-blue-700'     },
  Interviewing:   { label: 'Interviewing', cls: 'bg-indigo-100 text-indigo-700' },
  ClosedNoAnswer: { label: 'No answer',    cls: 'bg-amber-100 text-amber-700'   },
  ClosedRejected: { label: 'Rejected',     cls: 'bg-red-100 text-red-700'       },
  ClosedHired:    { label: 'Hired',        cls: 'bg-green-100 text-green-700'   },
};

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  const b = STATUS_BADGE[status];
  if (!b) return null;
  return (
    <span className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${b.cls}`}>
      {b.label}
    </span>
  );
}
