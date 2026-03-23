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
      <div className="flex items-start gap-3 mb-4 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5 shrink-0 mt-0.5 text-blue-500">
          <path fillRule="evenodd" d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-7-4a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM9 9a.75.75 0 0 0 0 1.5h.253a.25.25 0 0 1 .244.304l-.459 2.066A1.75 1.75 0 0 0 10.747 15H11a.75.75 0 0 0 0-1.5h-.253a.25.25 0 0 1-.244-.304l.459-2.066A1.75 1.75 0 0 0 9.253 9H9Z" clipRule="evenodd" />
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
