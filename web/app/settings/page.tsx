'use client';

import { useState } from 'react';
import { Card, CardHeader, CardBody } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { useSettings, useUpdateSettings } from '@/lib/hooks/useSettings';

const MODEL_INFO: Record<string, string> = {
  'claude-haiku-4-5-20251001': 'Fastest & cheapest — to avoid, usually too weak for Workcast',
  'claude-sonnet-4-5':         'Balanced speed and accuracy — recommended default',
  'claude-sonnet-4-6':         'Latest Sonnet — stronger reasoning, slightly higher cost',
  'claude-opus-4-6':           'Most capable — best for complex or unusual board layouts, highest cost',
};

export default function SettingsPage() {
  const { data: settings, isLoading } = useSettings();
  const { mutate: updateSettings, isPending } = useUpdateSettings();

  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');

  function startEdit() {
    setDraft(settings!.aiModel);
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setDraft('');
  }

  function saveEdit() {
    updateSettings(draft, { onSuccess: () => setEditing(false) });
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-gray-900">Settings</h1>

      {/* AI section */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">AI</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Model</td>
                {editing ? (
                  <>
                    <td className="px-4 py-2.5">
                      <select
                        autoFocus
                        value={draft}
                        onChange={(e) => setDraft(e.target.value)}
                        className="text-sm border border-gray-300 rounded px-2 py-1 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                      >
                        {settings!.availableModels.map((model) => (
                          <option key={model} value={model}>
                            {model}
                          </option>
                        ))}
                      </select>
                      {MODEL_INFO[draft] && (
                        <p className="mt-1 text-xs text-gray-400">{MODEL_INFO[draft]}</p>
                      )}
                    </td>
                    <td className="px-4 py-2.5 text-right whitespace-nowrap">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="primary" onClick={saveEdit} loading={isPending}>Save</Button>
                        <Button size="sm" variant="secondary" onClick={cancelEdit}>Cancel</Button>
                      </div>
                    </td>
                  </>
                ) : (
                  <>
                    <td className="px-4 py-2.5 text-sm text-gray-900">
                      {isLoading ? (
                        <span className="text-gray-400">Loading…</span>
                      ) : (
                        <span className="inline-flex items-baseline gap-2">
                          <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">{settings?.aiModel}</span>
                          {settings?.aiModel && MODEL_INFO[settings.aiModel] && (
                            <span className="text-xs text-gray-400">{MODEL_INFO[settings.aiModel]}</span>
                          )}
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-2.5 text-right">
                      {!isLoading && (
                        <button onClick={startEdit} className="text-xs text-indigo-500 hover:underline">Edit</button>
                      )}
                    </td>
                  </>
                )}
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>

      {/* Jobs section */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Jobs</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Hangfire dashboard</td>
                <td className="px-4 py-2.5 text-sm text-gray-900">Background job monitoring and queue management</td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  <a
                    href="http://localhost:8080/hangfire"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 text-xs text-indigo-500 hover:underline"
                  >
                    Open
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
                      <path d="M6.22 8.72a.75.75 0 0 0 1.06 1.06l5.22-5.22v1.69a.75.75 0 0 0 1.5 0v-3.5a.75.75 0 0 0-.75-.75h-3.5a.75.75 0 0 0 0 1.5h1.69L6.22 8.72Z"/>
                      <path d="M3.5 6.75c0-.69.56-1.25 1.25-1.25H7A.75.75 0 0 0 7 4H4.75A2.75 2.75 0 0 0 2 6.75v4.5A2.75 2.75 0 0 0 4.75 14h4.5A2.75 2.75 0 0 0 12 11.25V9a.75.75 0 0 0-1.5 0v2.25c0 .69-.56 1.25-1.25 1.25h-4.5c-.69 0-1.25-.56-1.25-1.25v-4.5Z"/>
                    </svg>
                  </a>
                </td>
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
