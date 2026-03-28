'use client';

import Link from 'next/link';
import type { Application } from '@/types';
import { Button } from '@/components/ui/Button';
import { scoreColorClass } from '@/components/scoring/ScoringShared';
import { useTrashApplication } from '@/lib/hooks/useApplications';
import { StatusBadge } from '@/components/applications/StatusBadge';

const URGENCY_TOOLTIP = 'Applications sent within 48h of posting have better chances to be considered.';

function UrgencyIcon({ urgency }: { urgency: 'orange' | 'red' }) {
  return (
    <div className="relative group inline-flex items-center">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor"
        className={`w-4 h-4 ${urgency === 'red' ? 'text-red-500' : 'text-orange-500'}`}>
        <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495zM10 5a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 10 5zm0 9a1 1 0 1 0 0-2 1 1 0 0 0 0 2z" clipRule="evenodd" />
      </svg>
      <span className="pointer-events-none absolute left-1/2 -translate-x-1/2 bottom-full mb-2 whitespace-nowrap rounded bg-gray-900 px-2.5 py-1.5 text-xs text-white opacity-0 group-hover:opacity-100 transition-opacity z-30">
        {URGENCY_TOOLTIP}
      </span>
    </div>
  );
}

function UrgencyCell({ app }: { app: Application }) {
  if (app.status !== 'ToApply') return null;
  const hoursScraped = (Date.now() - new Date(app.scrapedAt).getTime()) / 3_600_000;
  const urgency: 'orange' | 'red' | null = hoursScraped > 48 ? 'red' : hoursScraped > 12 ? 'orange' : null;
  return urgency ? <UrgencyIcon urgency={urgency} /> : null;
}

interface ApplicationTableProps {
  applications: Application[];
}

export function ApplicationTable({ applications }: ApplicationTableProps) {
  const trashApplication = useTrashApplication();

  if (applications.length === 0) return null;

  return (
    <div>
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-3 py-3 w-8" />
            <th className="px-3 py-3 text-left font-medium text-gray-500 w-36">Status</th>
            <th className="px-3 py-3 text-left font-medium text-gray-500 w-14">Score</th>
            <th className="px-3 py-3 text-left font-medium text-gray-500">Title</th>
            <th className="px-3 py-3 text-left font-medium text-gray-500 w-36">Company</th>
            <th className="px-3 py-3 text-left font-medium text-gray-500 w-24">Scraped</th>
            <th className="px-3 py-3 text-left font-medium text-gray-500 w-16">Actions</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-200">
          {applications.map((app) => (
            <tr key={app.id} className="hover:bg-gray-50">
              <td className="px-3 py-3 w-8">
                <UrgencyCell app={app} />
              </td>
              <td className="px-3 py-3">
                <StatusBadge status={app.status} />
              </td>
              <td className="px-3 py-3 text-xs font-medium tabular-nums">
                {app.overallScore != null ? (
                  <span className={scoreColorClass(app.overallScore)}>
                    {Math.round(app.overallScore)}%
                  </span>
                ) : (
                  <span className="text-gray-300">—</span>
                )}
              </td>
              <td className="px-3 py-3">
                <Link
                  href={`/applications/${app.id}`}
                  className="font-medium text-indigo-700 hover:underline"
                >
                  {app.title ?? '(no title)'}
                </Link>
              </td>
              <td className="px-3 py-3 text-gray-700">{app.company ?? '—'}</td>
              <td className="px-3 py-3 text-gray-500">
                {new Date(app.scrapedAt).toLocaleDateString()}
              </td>
              <td className="px-3 py-3">
                <Button
                  variant="ghost"
                  size="sm"
                  title="Move to trash"
                  onClick={() => trashApplication.mutate(app.id)}
                  className="text-gray-400 hover:text-red-500"
                >
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                    <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z" clipRule="evenodd" />
                  </svg>
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
