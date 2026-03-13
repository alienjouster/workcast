'use client';

import React, { useState } from 'react';
import type { JobAd } from '@/types';
import { Button } from '@/components/ui/Button';
import { useDeleteAd } from '@/lib/hooks/useJobAds';

interface AdTableProps {
  ads: JobAd[];
}

function confidenceColor(score: number) {
  if (score >= 0.8) return 'text-green-600';
  if (score >= 0.5) return 'text-yellow-600';
  return 'text-red-600';
}

export function AdTable({ ads }: AdTableProps) {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const deleteAd = useDeleteAd();

  if (ads.length === 0) return null;

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
            <th className="px-4 py-3 text-left font-medium text-gray-500">Company</th>
            <th className="px-4 py-3 text-left font-medium text-gray-500">Location</th>
            <th className="px-4 py-3 text-left font-medium text-gray-500">Scraped</th>
            <th className="px-4 py-3 text-left font-medium text-gray-500">Score</th>
            <th className="px-4 py-3 text-left font-medium text-gray-500">Actions</th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-200">
          {ads.map((ad) => (
            <React.Fragment key={ad.id}>
              <tr
                key={ad.id}
                className="hover:bg-gray-50 cursor-pointer"
                onClick={() => setExpandedId(expandedId === ad.id ? null : ad.id)}
              >
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    {!ad.isActive && (
                      <span className="text-xs bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded">
                        Inactive
                      </span>
                    )}
                    <a
                      href={ad.url}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-indigo-600 hover:underline font-medium"
                      onClick={(e) => e.stopPropagation()}
                    >
                      {ad.title ?? '(no title)'}
                    </a>
                  </div>
                </td>
                <td className="px-4 py-3 text-gray-600">{ad.company ?? '—'}</td>
                <td className="px-4 py-3 text-gray-600">{ad.location ?? '—'}</td>
                <td className="px-4 py-3 text-gray-500">
                  {new Date(ad.scrapedAt).toLocaleDateString()}
                </td>
                <td className={`px-4 py-3 font-medium ${confidenceColor(ad.aiConfidenceScore)}`}>
                  {(ad.aiConfidenceScore * 100).toFixed(0)}%
                </td>
                <td className="px-4 py-3">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={(e) => {
                      e.stopPropagation();
                      if (confirm('Delete this job ad?')) {
                        deleteAd.mutate(ad.id);
                      }
                    }}
                    className="text-red-500 hover:text-red-700"
                  >
                    Delete
                  </Button>
                </td>
              </tr>
              {expandedId === ad.id && (
                <tr key={`${ad.id}-expand`}>
                  <td colSpan={6} className="px-4 py-4 bg-gray-50">
                    {ad.description ? (
                      <p className="text-sm text-gray-700 whitespace-pre-wrap max-h-48 overflow-y-auto">
                        {ad.description}
                      </p>
                    ) : (
                      <p className="text-sm text-gray-400 italic">No description available.</p>
                    )}
                    <div className="flex gap-4 mt-2 text-xs text-gray-400">
                      {ad.salaryRaw && <span>Salary: {ad.salaryRaw}</span>}
                      {ad.postedAt && (
                        <span>Posted: {new Date(ad.postedAt).toLocaleDateString()}</span>
                      )}
                    </div>
                  </td>
                </tr>
              )}
            </React.Fragment>
          ))}
        </tbody>
      </table>
    </div>
  );
}
