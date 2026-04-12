'use client';

import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/Button';
import { useCreateBoard } from '@/lib/hooks/useJobBoards';
import { api } from '@/lib/api';
import type { BoardExchangeDto } from '@/types';

interface AddBoardFormProps {
  onClose: () => void;
}

type Mode = 'register' | 'import';

export function AddBoardForm({ onClose }: AddBoardFormProps) {
  const [mode, setMode] = useState<Mode>('register');

  return (
    <div>
      {/* Mode tabs */}
      <div className="flex gap-1 mb-5 bg-gray-100 rounded-lg p-1">
        <button
          type="button"
          onClick={() => setMode('register')}
          className={`flex-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
            mode === 'register'
              ? 'bg-white text-gray-900 shadow-sm'
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          Register new URL
        </button>
        <button
          type="button"
          onClick={() => setMode('import')}
          className={`flex-1 rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
            mode === 'import'
              ? 'bg-white text-gray-900 shadow-sm'
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          Import config
        </button>
      </div>

      {mode === 'register' ? (
        <RegisterForm onClose={onClose} />
      ) : (
        <ImportForm onClose={onClose} />
      )}
    </div>
  );
}

function RegisterForm({ onClose }: { onClose: () => void }) {
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

// ── Types ──────────────────────────────────────────────────────────────────��──

type QueueItem = {
  key: string;
  source: string;
  dto: BoardExchangeDto | null;
  parseError: string | null;
  importError: string | null;
  imported: boolean;
};

let _nextKey = 0;
function nextKey() { return String(++_nextKey); }

// ── Helpers ───────────────────────────────────────────────────────────────────

function parseDto(json: string): BoardExchangeDto {
  const parsed = JSON.parse(json) as BoardExchangeDto;
  if (!parsed.url) throw new Error('Missing required field: url');
  if (!parsed.scraperConfig) throw new Error('Missing required field: scraperConfig');
  if (!parsed.scraperConfig.jobCardSelector) throw new Error('Missing required field: scraperConfig.jobCardSelector');
  if (!parsed.scraperConfig.paginationType) throw new Error('Missing required field: scraperConfig.paginationType');
  return parsed;
}

function makeItem(source: string, json: string): QueueItem {
  try {
    return { key: nextKey(), source, dto: parseDto(json), parseError: null, importError: null, imported: false };
  } catch (err) {
    return { key: nextKey(), source, dto: null, parseError: err instanceof Error ? err.message : 'Invalid JSON', importError: null, imported: false };
  }
}

// ── Component ─────────────────────────────────────────────────────────────────

function ImportForm({ onClose }: { onClose: () => void }) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [urlInput, setUrlInput] = useState('https://raw.githubusercontent.com/alienjouster/workcast/master/community-boards/example.json');
  const [queue, setQueue] = useState<QueueItem[]>([]);
  const [isFetching, setIsFetching] = useState(false);
  const [isImporting, setIsImporting] = useState(false);
  const qc = useQueryClient();

  // ── File loading ─────────────────────────────────────────────────────────

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    if (!files.length) return;
    // Reset the input so the same files can be re-selected if removed
    e.target.value = '';

    let pending = files.length;
    const items: QueueItem[] = [];

    files.forEach((file) => {
      const reader = new FileReader();
      reader.onload = (ev) => {
        items.push(makeItem(file.name, ev.target?.result as string));
        if (--pending === 0) {
          setQueue((prev) => [...prev, ...items]);
        }
      };
      reader.readAsText(file);
    });
  }

  // ── URL loading ───────────────────────────────────────────────────────────

  async function handleLoadUrls() {
    const urls = urlInput.split('\n').map((u) => u.trim()).filter(Boolean);
    if (!urls.length) return;
    setIsFetching(true);
    setUrlInput('');

    const results = await Promise.allSettled(
      urls.map(async (url) => {
        const res = await fetch(url);
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return { url, text: await res.text() };
      }),
    );

    const items: QueueItem[] = results.map((r, i) => {
      if (r.status === 'fulfilled') return makeItem(urls[i], r.value.text);
      return { key: nextKey(), source: urls[i], dto: null, parseError: r.reason instanceof Error ? r.reason.message : 'Failed to fetch', importError: null, imported: false };
    });

    setQueue((prev) => [...prev, ...items]);
    setIsFetching(false);
  }

  // ── Queue management ──────────────────────────────────────────────────────

  function removeItem(key: string) {
    setQueue((prev) => prev.filter((item) => item.key !== key));
  }

  // ── Import ────────────────────────────────────────────────────────────────

  async function handleImport() {
    const valid = queue.filter((item) => item.dto && !item.imported);
    if (!valid.length) return;
    setIsImporting(true);

    const results = await Promise.allSettled(
      valid.map((item) => api.boards.import(item.dto!)),
    );

    setQueue((prev) =>
      prev.map((item) => {
        const idx = valid.findIndex((v) => v.key === item.key);
        if (idx === -1) return item;
        const result = results[idx];
        if (result.status === 'fulfilled') return { ...item, imported: true, importError: null };
        return { ...item, importError: result.reason instanceof Error ? result.reason.message : 'Import failed' };
      }),
    );

    await qc.invalidateQueries({ queryKey: ['job-boards'] });
    setIsImporting(false);

    // Close only if every valid item imported successfully
    if (results.every((r) => r.status === 'fulfilled')) onClose();
  }

  // ── Derived state ─────────────────────────────────────────────────────────

  const validCount = queue.filter((item) => item.dto && !item.imported).length;
  const hasAny = queue.length > 0;

  // ── Render ────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-4">
      {/* Info note */}
      <p className="text-sm text-gray-500">
        Ready-to-use scraper configurations for common job boards are available in the{' '}
        <code className="bg-gray-100 px-1 rounded text-xs">/community-boards</code> folder of the repository.
      </p>

      {/* File input */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Load from files
        </label>
        <input
          ref={fileInputRef}
          type="file"
          accept=".json,application/json"
          multiple
          onChange={handleFileChange}
          className="block w-full text-sm text-gray-500 file:mr-3 file:py-1.5 file:px-3 file:rounded file:border-0 file:text-sm file:font-medium file:bg-indigo-50 file:text-indigo-700 hover:file:bg-indigo-100"
        />
      </div>

      {/* Divider */}
      <div className="flex items-center gap-3">
        <div className="flex-1 border-t border-gray-200" />
        <span className="text-xs text-gray-400 uppercase tracking-wide">or</span>
        <div className="flex-1 border-t border-gray-200" />
      </div>

      {/* URL textarea */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-1">
          Load from URLs{' '}
          <span className="text-gray-400 font-normal">(one per line, e.g. GitHub raw URLs)</span>
        </label>
        <textarea
          value={urlInput}
          onChange={(e) => setUrlInput(e.target.value)}
          placeholder={'https://raw.githubusercontent.com/alienjouster/workcast/master/community-boards/indeed-fr.json\nhttps://raw.githubusercontent.com/alienjouster/workcast/master/community-boards/stackoverflow-jobs.json'}
          rows={3}
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500 resize-none"
        />
        <div className="flex justify-end mt-1.5">
          <Button
            variant="secondary"
            size="sm"
            type="button"
            loading={isFetching}
            disabled={!urlInput.trim()}
            onClick={handleLoadUrls}
          >
            Load
          </Button>
        </div>
      </div>

      {/* Queue */}
      {hasAny && (
        <div className="space-y-2">
          <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">
            Queue — {validCount} ready{queue.length > validCount ? `, ${queue.length - validCount} with errors` : ''}
          </p>
          <ul className="space-y-1.5 max-h-48 overflow-y-auto pr-1">
            {queue.map((item) => (
              <li
                key={item.key}
                className={`flex items-start gap-2 rounded-md border px-3 py-2 text-sm ${
                  item.imported
                    ? 'border-green-200 bg-green-50'
                    : item.parseError || item.importError
                    ? 'border-red-200 bg-red-50'
                    : 'border-indigo-200 bg-indigo-50'
                }`}
              >
                {/* Status icon */}
                <span className="mt-0.5 shrink-0">
                  {item.imported ? (
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4 text-green-600"><path fillRule="evenodd" d="M12.416 3.376a.75.75 0 0 1 .208 1.04l-5 7.5a.75.75 0 0 1-1.154.114l-3-3a.75.75 0 0 1 1.06-1.06l2.353 2.353 4.493-6.74a.75.75 0 0 1 1.04-.207Z" clipRule="evenodd" /></svg>
                  ) : item.parseError || item.importError ? (
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4 text-red-500"><path fillRule="evenodd" d="M8 15A7 7 0 1 0 8 1a7 7 0 0 0 0 14ZM8 5a.75.75 0 0 1 .75.75v2.5a.75.75 0 0 1-1.5 0v-2.5A.75.75 0 0 1 8 5Zm0 7a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z" clipRule="evenodd" /></svg>
                  ) : (
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4 text-indigo-400"><path d="M8 9.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3Z" /><path fillRule="evenodd" d="M1.38 8.28a4.942 4.942 0 0 1 0-.563C1.47 4.185 4.25 2 8 2s6.53 2.185 6.62 5.718a4.942 4.942 0 0 1 0 .563C14.53 11.815 11.75 14 8 14s-6.53-2.185-6.62-5.718ZM11 8a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" clipRule="evenodd" /></svg>
                  )}
                </span>

                {/* Content */}
                <div className="min-w-0 flex-1">
                  {item.dto ? (
                    <>
                      <p className="font-medium text-gray-900 truncate">{item.dto.name}</p>
                      <p className="font-mono text-xs text-gray-500 truncate">{item.dto.url}</p>
                      {item.importError && (
                        <p className="text-xs text-red-600 mt-0.5">{item.importError}</p>
                      )}
                    </>
                  ) : (
                    <>
                      <p className="font-mono text-xs text-gray-500 truncate">{item.source}</p>
                      <p className="text-xs text-red-600 mt-0.5">{item.parseError}</p>
                    </>
                  )}
                </div>

                {/* Remove */}
                {!item.imported && (
                  <button
                    type="button"
                    onClick={() => removeItem(item.key)}
                    className="shrink-0 text-gray-400 hover:text-gray-600"
                    aria-label="Remove"
                  >
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4"><path d="M5.28 4.22a.75.75 0 0 0-1.06 1.06L6.94 8l-2.72 2.72a.75.75 0 1 0 1.06 1.06L8 9.06l2.72 2.72a.75.75 0 1 0 1.06-1.06L9.06 8l2.72-2.72a.75.75 0 0 0-1.06-1.06L8 6.94 5.28 4.22Z" /></svg>
                  </button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="flex justify-end gap-3 pt-2">
        <Button variant="secondary" type="button" onClick={onClose}>
          Cancel
        </Button>
        <Button
          variant="primary"
          type="button"
          onClick={handleImport}
          disabled={validCount === 0}
          loading={isImporting}
        >
          {validCount > 1 ? `Import ${validCount} Boards` : 'Import Boards'}
        </Button>
      </div>
    </div>
  );
}
