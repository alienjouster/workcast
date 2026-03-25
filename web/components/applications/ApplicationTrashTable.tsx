'use client';

import Link from 'next/link';
import { useState } from 'react';
import type { Application } from '@/types';
import { Button } from '@/components/ui/Button';
import { useRestoreApplication, useDeleteApplication } from '@/lib/hooks/useApplications';

interface ApplicationTrashTableProps {
  applications: Application[];
}

export function ApplicationTrashTable({ applications }: ApplicationTrashTableProps) {
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const restoreApplication = useRestoreApplication();
  const deleteApplication = useDeleteApplication();

  if (applications.length === 0) return null;

  return (
    <>
      {confirmDeleteId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl p-6 max-w-sm w-full mx-4">
            <h2 className="text-base font-semibold text-gray-900 mb-2">Delete application?</h2>
            <p className="text-sm text-gray-500 mb-6">This action cannot be undone.</p>
            <div className="flex justify-end gap-3">
              <Button variant="secondary" size="sm" onClick={() => setConfirmDeleteId(null)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                size="sm"
                loading={deleteApplication.isPending}
                onClick={() =>
                  deleteApplication.mutate(confirmDeleteId, {
                    onSuccess: () => setConfirmDeleteId(null),
                  })
                }
              >
                Delete
              </Button>
            </div>
          </div>
        </div>
      )}

      <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 mb-4 text-sm text-amber-800">
        Trashed applications are not auto-deleted. Permanently delete them when no longer needed.
      </div>

      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Title</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Company</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Location</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Applied</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Actions</th>
            </tr>
          </thead>
          <tbody className="bg-white divide-y divide-gray-200">
            {applications.map((app) => (
              <tr key={app.id} className="hover:bg-gray-50">
                <td className="px-4 py-3">
                  <Link
                    href={`/applications/${app.id}`}
                    className="text-indigo-600 hover:underline"
                  >
                    {app.title ?? '(no title)'}
                  </Link>
                </td>
                <td className="px-4 py-3 text-gray-500">{app.company ?? '—'}</td>
                <td className="px-4 py-3 text-gray-500">{app.location ?? '—'}</td>
                <td className="px-4 py-3 text-gray-500">
                  {new Date(app.createdAt).toLocaleDateString()}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => restoreApplication.mutate(app.id)}
                      loading={restoreApplication.isPending}
                    >
                      Restore
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setConfirmDeleteId(app.id)}
                      className="text-red-500 hover:text-red-700"
                    >
                      Delete
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
