'use client';

import type { JobAd } from '@/types';
import { Button } from '@/components/ui/Button';
import { useDeleteAd, useRestoreAd } from '@/lib/hooks/useJobAds';

interface TrashTableProps {
  ads: JobAd[];
}

export function TrashTable({ ads }: TrashTableProps) {
  const restoreAd = useRestoreAd();
  const deleteAd = useDeleteAd();

  return (
    <div>
      {/* Info card */}
      <div className="flex items-start gap-3 mb-4 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5 shrink-0 mt-0.5 text-amber-500">
          <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495ZM10 5a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 10 5Zm0 9a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z" clipRule="evenodd" />
        </svg>
        <span>Items in the trash bin are automatically deleted after <strong>30 days</strong>. Inactive ads are also removed on the same schedule.</span>
      </div>

      {ads.length === 0 ? (
        <div className="text-center py-12 text-gray-400 text-sm">The trash bin is empty.</div>
      ) : (
        <div className="bg-white rounded-lg border border-gray-200 shadow-sm overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Company</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Location</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Scraped</th>
                <th className="px-4 py-3 text-left font-medium text-gray-500">Actions</th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {ads.map((ad) => (
                <tr key={ad.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3 text-gray-500">
                    <a
                      href={ad.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="hover:underline text-indigo-400"
                    >
                      {ad.title ?? '(no title)'}
                    </a>
                  </td>
                  <td className="px-4 py-3 text-gray-400">{ad.company ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-400">{ad.location ?? '—'}</td>
                  <td className="px-4 py-3 text-gray-400">
                    {new Date(ad.scrapedAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-3 flex items-center gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => restoreAd.mutate(ad.id)}
                      loading={restoreAd.isPending}
                    >
                      Restore
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        if (confirm('Permanently delete this job ad? It will be re-scraped on the next run.')) {
                          deleteAd.mutate(ad.id);
                        }
                      }}
                      className="text-red-500 hover:text-red-700"
                    >
                      Delete
                    </Button>
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
