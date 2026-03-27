'use client';

import { useRef, useState } from 'react';
import { Card, CardHeader, CardBody } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { useSettings, useUpdateSettings, useUploadResume, useDeleteResume } from '@/lib/hooks/useSettings';

const MODEL_INFO: Record<string, string> = {
  'claude-haiku-4-5-20251001': 'Fastest & cheapest',
  'claude-sonnet-4-5':         'Balanced speed and accuracy — recommended default',
  'claude-sonnet-4-6':         'Latest Sonnet — stronger reasoning, slightly higher cost',
  'claude-opus-4-6':           'Most capable — best for complex or unusual board layouts, highest cost',
};

type EditingField = 'boardAnalyzer' | 'scoring' | null;

export function SettingsClient() {
  const { data: settings, isLoading } = useSettings();
  const { mutate: updateSettings, isPending } = useUpdateSettings();
  const { mutate: uploadResume, isPending: isUploading, error: uploadError } = useUploadResume();
  const { mutate: deleteResume, isPending: isDeleting } = useDeleteResume();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [editingField, setEditingField] = useState<EditingField>(null);
  const [draft, setDraft] = useState('');

  function startEdit(field: EditingField) {
    setDraft(field === 'boardAnalyzer' ? settings!.boardAnalyzerModel : settings!.scoringModel);
    setEditingField(field);
  }

  function cancelEdit() {
    setEditingField(null);
    setDraft('');
  }

  function saveEdit() {
    const boardAnalyzerModel = editingField === 'boardAnalyzer' ? draft : settings!.boardAnalyzerModel;
    const scoringModel = editingField === 'scoring' ? draft : settings!.scoringModel;
    updateSettings({ boardAnalyzerModel, scoringModel }, { onSuccess: () => setEditingField(null) });
  }

  function renderModelCell(field: EditingField, currentValue: string | undefined) {
    if (editingField === field) {
      return (
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
      );
    }

    return (
      <>
        <td className="px-4 py-2.5 text-sm text-gray-900">
          {isLoading ? (
            <span className="text-gray-400">Loading…</span>
          ) : (
            <span className="inline-flex items-baseline gap-2">
              <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">{currentValue}</span>
              {currentValue && MODEL_INFO[currentValue] && (
                <span className="text-xs text-gray-400">{MODEL_INFO[currentValue]}</span>
              )}
            </span>
          )}
        </td>
        <td className="px-4 py-2.5 text-right">
          {!isLoading && editingField === null && (
            <button onClick={() => startEdit(field)} className="text-xs text-indigo-500 hover:underline">Edit</button>
          )}
        </td>
      </>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-gray-900">Settings</h1>

      {/* Resume section */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Resume</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48 align-top pt-3">File</td>
                <td className="px-4 py-2.5">
                  {isLoading ? (
                    <span className="text-gray-400">Loading…</span>
                  ) : settings?.hasResume ? (
                    <div className="space-y-0.5">
                      <p className="text-sm font-medium text-gray-900">{settings.resumeFileName}</p>
                      {settings.resumeUploadedAt && (
                        <p className="text-xs text-gray-400">
                          Uploaded {new Date(settings.resumeUploadedAt).toLocaleString()}
                        </p>
                      )}
                    </div>
                  ) : (
                    <div>
                      <p className="text-sm text-gray-500 italic">No resume uploaded.</p>
                      <p className="text-xs text-gray-400 mt-0.5">
                        Structured formats are recommended — JSON with clearly labelled sections gives the best scoring accuracy.
                      </p>
                    </div>
                  )}
                  {uploadError && (
                    <p className="text-xs text-red-600 mt-1">{(uploadError as Error).message}</p>
                  )}
                </td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap align-top pt-3">
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".pdf,.txt,.json,application/pdf,text/plain,application/json"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) uploadResume(file);
                      e.target.value = '';
                    }}
                  />
                  <div className="flex items-center justify-end gap-2">
                    <Button
                      size="sm"
                      variant="secondary"
                      onClick={() => fileInputRef.current?.click()}
                      loading={isUploading}
                    >
                      {settings?.hasResume ? 'Replace' : 'Upload'}
                    </Button>
                    {settings?.hasResume && (
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => deleteResume()}
                        loading={isDeleting}
                        className="text-red-500 hover:text-red-700"
                      >
                        Remove
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>

      {/* AI section */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">AI</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Board analyzer model</td>
                {renderModelCell('boardAnalyzer', settings?.boardAnalyzerModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Scoring model</td>
                {renderModelCell('scoring', settings?.scoringModel)}
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
