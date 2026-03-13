'use client';

import { useState } from 'react';
import type { ScraperConfig, UpdateScraperConfigRequest } from '@/types';
import { useUpdateScraperConfig } from '@/lib/hooks/useJobBoards';
import { Button } from '@/components/ui/Button';

interface ScraperConfigViewProps {
  boardId: string;
  config: ScraperConfig;
}

// ── Read-only helpers ────────────────────────────────────────────────────────

function SelectorRow({ label, value }: { label: string; value: string | null }) {
  if (!value) return null;
  return (
    <div className="text-sm">
      <span className="text-gray-500">{label}</span>
      <p className="font-mono text-xs bg-gray-100 px-2 py-1 rounded mt-1 break-all">{value}</p>
    </div>
  );
}

// ── Edit-mode helpers ────────────────────────────────────────────────────────

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="text-sm">
      <label className="block text-gray-500 mb-1">{label}</label>
      {children}
    </div>
  );
}

const inputCls = 'w-full rounded border border-gray-300 px-2 py-1 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500';
const inputClsPlain = 'w-full rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500';

// ── Component ────────────────────────────────────────────────────────────────

export function ScraperConfigView({ boardId, config }: ScraperConfigViewProps) {
  const [expanded, setExpanded] = useState(false);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<UpdateScraperConfigRequest | null>(null);
  const updateConfig = useUpdateScraperConfig(boardId);

  function startEdit() {
    setDraft({
      paginationType: config.paginationType,
      jobCardSelector: config.jobCardSelector,
      fieldSelectors: { ...config.fieldSelectors },
      nextPageSelector: config.nextPageSelector,
      urlParamName: config.urlParamName,
      urlParamIsOffset: config.urlParamIsOffset,
      maxPages: config.maxPages,
      requiresJs: config.requiresJs,
      suggestedDelayMs: config.suggestedDelayMs,
      analyzerNotes: config.analyzerNotes,
    });
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setDraft(null);
  }

  async function saveEdit() {
    if (!draft) return;
    await updateConfig.mutateAsync(draft);
    setEditing(false);
    setDraft(null);
  }

  function set<K extends keyof UpdateScraperConfigRequest>(key: K, value: UpdateScraperConfigRequest[K]) {
    setDraft((d) => d ? { ...d, [key]: value } : d);
  }

  function setField(key: keyof UpdateScraperConfigRequest['fieldSelectors'], value: string) {
    setDraft((d) => d ? { ...d, fieldSelectors: { ...d.fieldSelectors, [key]: value || null } } : d);
  }

  return (
    <div className="border border-gray-200 rounded-md overflow-hidden">
      {/* Header */}
      <button
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors"
      >
        <span>Scraper Configuration</span>
        <div className="flex items-center gap-3">
          <span className="text-xs text-gray-500">
            Confidence: {(config.confidenceScore * 100).toFixed(0)}%
          </span>
          <span>{expanded ? '▲' : '▼'}</span>
        </div>
      </button>

      {expanded && !editing && (
        <div className="p-4 space-y-4">
          {/* General settings */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 text-sm">
            <div>
              <span className="text-gray-500">Pagination</span>
              <p className="font-mono font-medium">{config.paginationType}</p>
            </div>
            <div>
              <span className="text-gray-500">Requires JS</span>
              <p className="font-medium">{config.requiresJs ? 'Yes' : 'No'}</p>
            </div>
            <div>
              <span className="text-gray-500">Delay (ms)</span>
              <p className="font-medium">{config.suggestedDelayMs}</p>
            </div>
            <div>
              <span className="text-gray-500">Max Pages</span>
              <p className="font-medium">{config.maxPages ?? 'Unlimited'}</p>
            </div>
          </div>

          {(config.urlParamName || config.nextPageSelector) && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Pagination</p>
              {config.urlParamName && (
                <div className="text-sm">
                  <span className="text-gray-500">URL Param</span>
                  <p className="font-mono text-xs bg-gray-100 px-2 py-1 rounded mt-1">
                    {config.urlParamName}{config.urlParamIsOffset ? ' (offset)' : ' (page number)'}
                  </p>
                </div>
              )}
              <SelectorRow label="Next Page Selector" value={config.nextPageSelector} />
            </div>
          )}

          <div className="space-y-2">
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Selectors</p>
            <SelectorRow label="Job Card" value={config.jobCardSelector} />
            <SelectorRow label="Detail URL" value={config.fieldSelectors.detailUrl} />
            <SelectorRow label="Title" value={config.fieldSelectors.title} />
            <SelectorRow label="Company" value={config.fieldSelectors.company} />
            <SelectorRow label="Location" value={config.fieldSelectors.location} />
            <SelectorRow label="Salary" value={config.fieldSelectors.salaryRaw} />
            <SelectorRow label="Posted At" value={config.fieldSelectors.postedAt} />
            <SelectorRow label="Description Snippet" value={config.fieldSelectors.descriptionSnippet} />
            <SelectorRow label="External ID" value={config.fieldSelectors.externalId} />
          </div>

          {config.analyzerNotes && (
            <div className="text-sm">
              <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1">Analyzer Notes</p>
              <p className="text-gray-700 bg-yellow-50 border border-yellow-100 rounded px-3 py-2 text-xs">
                {config.analyzerNotes}
              </p>
            </div>
          )}

          <div className="flex items-center justify-between">
            <span className="text-xs text-gray-400">
              Generated at {new Date(config.generatedAt).toLocaleString()}
            </span>
            <Button variant="secondary" size="sm" onClick={startEdit}>Edit</Button>
          </div>
        </div>
      )}

      {expanded && editing && draft && (
        <div className="p-4 space-y-4">
          {/* General */}
          <div className="grid grid-cols-2 gap-3">
            <Field label="Pagination Type">
              <select
                className={inputClsPlain}
                value={draft.paginationType}
                onChange={(e) => set('paginationType', e.target.value as UpdateScraperConfigRequest['paginationType'])}
              >
                <option value="none">none</option>
                <option value="url_param">url_param</option>
                <option value="next_button">next_button</option>
                <option value="infinite_scroll">infinite_scroll</option>
              </select>
            </Field>
            <Field label="Requires JS">
              <div className="flex items-center gap-2 mt-1">
                <input
                  type="checkbox"
                  checked={draft.requiresJs}
                  onChange={(e) => set('requiresJs', e.target.checked)}
                  className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
                <span className="text-sm text-gray-700">Required</span>
              </div>
            </Field>
            <Field label="Delay between pages (ms)">
              <input
                type="number"
                min={0}
                max={10000}
                className={inputClsPlain}
                value={draft.suggestedDelayMs}
                onChange={(e) => set('suggestedDelayMs', Number(e.target.value))}
              />
            </Field>
            <Field label="Max Pages (blank = unlimited)">
              <input
                type="number"
                min={1}
                className={inputClsPlain}
                value={draft.maxPages ?? ''}
                onChange={(e) => set('maxPages', e.target.value ? Number(e.target.value) : null)}
              />
            </Field>
          </div>

          {/* Pagination selectors */}
          <div className="space-y-2">
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Pagination Selectors</p>
            <Field label="Next Page Selector">
              <input className={inputCls} value={draft.nextPageSelector ?? ''} onChange={(e) => set('nextPageSelector', e.target.value || null)} />
            </Field>
            <Field label="URL Param Name">
              <input className={inputCls} value={draft.urlParamName ?? ''} onChange={(e) => set('urlParamName', e.target.value || null)} />
            </Field>
            <Field label="URL Param Is Offset">
              <div className="flex items-center gap-2 mt-1">
                <input
                  type="checkbox"
                  checked={draft.urlParamIsOffset}
                  onChange={(e) => set('urlParamIsOffset', e.target.checked)}
                  className="h-4 w-4 rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                />
                <span className="text-sm text-gray-700">Param is item offset, not page number</span>
              </div>
            </Field>
          </div>

          {/* Field selectors */}
          <div className="space-y-2">
            <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide">Field Selectors (relative to job card)</p>
            <Field label="Job Card Selector">
              <input className={inputCls} value={draft.jobCardSelector} onChange={(e) => set('jobCardSelector', e.target.value)} />
            </Field>
            {(
              [
                ['detailUrl', 'Detail URL'],
                ['title', 'Title'],
                ['company', 'Company'],
                ['location', 'Location'],
                ['salaryRaw', 'Salary'],
                ['postedAt', 'Posted At'],
                ['descriptionSnippet', 'Description Snippet'],
                ['externalId', 'External ID'],
              ] as const
            ).map(([key, label]) => (
              <Field key={key} label={label}>
                <input
                  className={inputCls}
                  value={draft.fieldSelectors[key] ?? ''}
                  onChange={(e) => setField(key, e.target.value)}
                />
              </Field>
            ))}
          </div>

          {/* Analyzer notes */}
          <Field label="Analyzer Notes">
            <textarea
              rows={3}
              className="w-full rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              value={draft.analyzerNotes ?? ''}
              onChange={(e) => set('analyzerNotes', e.target.value || null)}
            />
          </Field>

          {updateConfig.isError && (
            <p className="text-sm text-red-600 bg-red-50 rounded px-3 py-2">
              {(updateConfig.error as Error)?.message ?? 'Failed to save configuration'}
            </p>
          )}

          <div className="flex justify-end gap-2 pt-1">
            <Button variant="secondary" size="sm" onClick={cancelEdit}>Cancel</Button>
            <Button variant="primary" size="sm" onClick={saveEdit} loading={updateConfig.isPending}>
              Save Configuration
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
