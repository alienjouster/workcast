'use client';

import { useState } from 'react';
import type { ScraperConfig, UpdateScraperConfigRequest } from '@/types';
import { useUpdateScraperConfig } from '@/lib/hooks/useJobBoards';
import { Button } from '@/components/ui/Button';
import { Tooltip } from '@/components/ui/Tooltip';

interface ScraperConfigViewProps {
  boardId: string;
  config: ScraperConfig;
}

function buildRequest(
  config: ScraperConfig,
  overrides: Partial<UpdateScraperConfigRequest>,
): UpdateScraperConfigRequest {
  return {
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
    ...overrides,
  };
}

const monoCls = 'rounded border border-gray-300 px-2 py-1 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-indigo-500';
const plainCls = 'rounded border border-gray-300 px-2 py-1 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500';

function InfoTooltip({ text }: { text: string }) {
  return (
    <Tooltip content={text} position="right" wrapperAs="span" wrap className="inline-flex items-center ml-1.5 align-middle" tooltipClassName="max-w-56 leading-relaxed">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5 text-gray-300 group-hover:text-gray-400 cursor-default transition-colors">
        <path fillRule="evenodd" d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-7-4a1 1 0 1 1-2 0 1 1 0 0 1 2 0ZM9 9a.75.75 0 0 0 0 1.5h.253a.25.25 0 0 1 .244.304l-.459 2.066A1.75 1.75 0 0 0 10.747 15H11a.75.75 0 0 0 0-1.5h-.253a.25.25 0 0 1-.244-.304l.459-2.066A1.75 1.75 0 0 0 9.253 9H9Z" clipRule="evenodd" />
      </svg>
    </Tooltip>
  );
}

function SectionRow({ label }: { label: string }) {
  return (
    <tr className="bg-gray-50">
      <td colSpan={3} className="px-4 py-2 text-xs font-semibold text-gray-400 uppercase tracking-wide">
        {label}
      </td>
    </tr>
  );
}

export function ScraperConfigView({ boardId, config }: ScraperConfigViewProps) {
  const [editingField, setEditingField] = useState<string | null>(null);
  const [draftValue, setDraftValue] = useState('');
  const updateConfig = useUpdateScraperConfig(boardId);

  function startEdit(field: string, currentValue: string) {
    setEditingField(field);
    setDraftValue(currentValue);
  }

  function cancelEdit() {
    setEditingField(null);
    setDraftValue('');
  }

  async function saveField(request: UpdateScraperConfigRequest) {
    await updateConfig.mutateAsync(request);
    setEditingField(null);
    setDraftValue('');
  }

  // ── Row types ───────────────────────────────────────────────────────────────

  function TextRow({
    field,
    label,
    tooltip,
    value,
    mono = true,
    nullable = true,
    buildOverride,
  }: {
    field: string;
    label: string;
    tooltip: string;
    value: string | null;
    mono?: boolean;
    nullable?: boolean;
    buildOverride: (val: string | null) => Partial<UpdateScraperConfigRequest>;
  }) {
    const isEditing = editingField === field;
    return (
      <tr className="border-t border-gray-100 hover:bg-gray-50">
        <td className="px-4 py-2.5 text-sm text-gray-500 w-48 shrink-0">
          {label}<InfoTooltip text={tooltip} />
        </td>
        {isEditing ? (
          <>
            <td className="px-4 py-2.5">
              <input
                autoFocus
                className={`${mono ? monoCls : plainCls} w-full max-w-lg`}
                value={draftValue}
                onChange={(e) => setDraftValue(e.target.value)}
              />
            </td>
            <td className="px-4 py-2.5 text-right whitespace-nowrap">
              <div className="flex items-center justify-end gap-2">
                <Button size="sm" variant="primary" onClick={() => saveField(buildRequest(config, buildOverride(nullable ? draftValue || null : draftValue)))} loading={updateConfig.isPending}>
                  Save
                </Button>
                <Button size="sm" variant="secondary" onClick={cancelEdit}>Cancel</Button>
              </div>
            </td>
          </>
        ) : (
          <>
            <td className="px-4 py-2.5">
              {value
                ? <span className={mono ? 'font-mono text-xs bg-gray-100 px-2 py-1 rounded break-all' : 'text-sm text-gray-900'}>{value}</span>
                : <span className="text-sm text-gray-400 italic">—</span>
              }
            </td>
            <td className="px-4 py-2.5 text-right">
              <button onClick={() => startEdit(field, value ?? '')} className="text-xs text-indigo-500 hover:underline">
                Edit
              </button>
            </td>
          </>
        )}
      </tr>
    );
  }

  function SelectRow({
    field,
    label,
    tooltip,
    value,
    options,
    buildOverride,
  }: {
    field: string;
    label: string;
    tooltip: string;
    value: string;
    options: { value: string; label: string }[];
    buildOverride: (val: string) => Partial<UpdateScraperConfigRequest>;
  }) {
    const isEditing = editingField === field;
    return (
      <tr className="border-t border-gray-100 hover:bg-gray-50">
        <td className="px-4 py-2.5 text-sm text-gray-500 w-48">
          {label}<InfoTooltip text={tooltip} />
        </td>
        {isEditing ? (
          <>
            <td className="px-4 py-2.5">
              <select autoFocus className={plainCls} value={draftValue} onChange={(e) => setDraftValue(e.target.value)}>
                {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
              </select>
            </td>
            <td className="px-4 py-2.5 text-right whitespace-nowrap">
              <div className="flex items-center justify-end gap-2">
                <Button size="sm" variant="primary" onClick={() => saveField(buildRequest(config, buildOverride(draftValue)))} loading={updateConfig.isPending}>
                  Save
                </Button>
                <Button size="sm" variant="secondary" onClick={cancelEdit}>Cancel</Button>
              </div>
            </td>
          </>
        ) : (
          <>
            <td className="px-4 py-2.5">
              <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">{value}</span>
            </td>
            <td className="px-4 py-2.5 text-right">
              <button onClick={() => startEdit(field, value)} className="text-xs text-indigo-500 hover:underline">Edit</button>
            </td>
          </>
        )}
      </tr>
    );
  }

  function NumberRow({
    field,
    label,
    tooltip,
    value,
    nullable = false,
    buildOverride,
  }: {
    field: string;
    label: string;
    tooltip: string;
    value: number | null;
    nullable?: boolean;
    buildOverride: (val: number | null) => Partial<UpdateScraperConfigRequest>;
  }) {
    const isEditing = editingField === field;
    return (
      <tr className="border-t border-gray-100 hover:bg-gray-50">
        <td className="px-4 py-2.5 text-sm text-gray-500 w-48">
          {label}<InfoTooltip text={tooltip} />
        </td>
        {isEditing ? (
          <>
            <td className="px-4 py-2.5">
              <input type="number" autoFocus className={`${plainCls} w-32`} value={draftValue} onChange={(e) => setDraftValue(e.target.value)} />
            </td>
            <td className="px-4 py-2.5 text-right whitespace-nowrap">
              <div className="flex items-center justify-end gap-2">
                <Button size="sm" variant="primary" onClick={() => saveField(buildRequest(config, buildOverride(draftValue ? Number(draftValue) : null)))} loading={updateConfig.isPending}>
                  Save
                </Button>
                <Button size="sm" variant="secondary" onClick={cancelEdit}>Cancel</Button>
              </div>
            </td>
          </>
        ) : (
          <>
            <td className="px-4 py-2.5 text-sm text-gray-900">
              {value != null ? value : <span className="text-gray-400 italic">Unlimited</span>}
            </td>
            <td className="px-4 py-2.5 text-right">
              <button onClick={() => startEdit(field, value != null ? String(value) : '')} className="text-xs text-indigo-500 hover:underline">Edit</button>
            </td>
          </>
        )}
      </tr>
    );
  }

  function ToggleRow({
    label,
    tooltip,
    value,
    buildOverride,
  }: {
    label: string;
    tooltip: string;
    value: boolean;
    buildOverride: (val: boolean) => Partial<UpdateScraperConfigRequest>;
  }) {
    return (
      <tr className="border-t border-gray-100 hover:bg-gray-50">
        <td className="px-4 py-2.5 text-sm text-gray-500 w-48">
          {label}<InfoTooltip text={tooltip} />
        </td>
        <td className="px-4 py-2.5" colSpan={2}>
          <button
            role="switch"
            aria-checked={value}
            onClick={() => saveField(buildRequest(config, buildOverride(!value)))}
            disabled={updateConfig.isPending}
            className={`relative inline-flex h-5 w-9 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:opacity-50 ${value ? 'bg-indigo-600' : 'bg-gray-200'}`}
          >
            <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${value ? 'translate-x-4' : 'translate-x-0'}`} />
          </button>
        </td>
      </tr>
    );
  }

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      <table className="min-w-full text-sm">
        <tbody>
          <SectionRow label="General" />
          <ToggleRow
            label="Requires JS"
            tooltip="Enable if job listings are loaded dynamically by JavaScript. Necessary for React/Vue/Angular-based boards but slower than plain HTML scraping."
            value={config.requiresJs}
            buildOverride={(v) => ({ requiresJs: v })}
          />
          <NumberRow
            field="suggestedDelayMs"
            label="Delay (ms)"
            tooltip="Milliseconds to wait between page requests. Increase this value if the board rate-limits or blocks the scraper."
            value={config.suggestedDelayMs}
            buildOverride={(v) => ({ suggestedDelayMs: v ?? 0 })}
          />

          <SectionRow label="Selectors" />
          <TextRow
            field="jobCardSelector"
            label="Job card"
            tooltip="CSS selector that matches each individual job listing on the page. All other selectors below are evaluated relative to this element."
            value={config.jobCardSelector}
            nullable={false}
            buildOverride={(v) => ({ jobCardSelector: v ?? '' })}
          />
          {(
            [
              ['detailUrl',          'Detail URL',           'CSS selector for the link to the full job detail page, relative to each job card.'],
              ['title',              'Title',                'CSS selector for the job title text, relative to each job card.'],
              ['company',            'Company',              'CSS selector for the company name, relative to each job card.'],
              ['location',           'Location',             'CSS selector for the job location, relative to each job card.'],
              ['salaryRaw',          'Salary',               'CSS selector for salary or compensation information, relative to each job card.'],
              ['postedAt',           'Posted at',            'CSS selector for the job posting date, relative to each job card.'],
              ['descriptionSnippet', 'Description snippet',  'CSS selector for a short preview of the job description, relative to each job card.'],
              ['externalId',         'External ID',          'CSS selector for a unique identifier from the source board, used to detect duplicate jobs across scrape runs.'],
            ] as const
          ).map(([key, label, tooltip]) => (
            <TextRow
              key={key}
              field={`fieldSelectors.${key}`}
              label={label}
              tooltip={tooltip}
              value={config.fieldSelectors[key]}
              buildOverride={(v) => ({ fieldSelectors: { ...config.fieldSelectors, [key]: v } })}
            />
          ))}

          <SectionRow label="Pagination" />
          <SelectRow
            field="paginationType"
            label="Pagination type"
            tooltip="How the scraper moves between pages. 'none' = single page, 'url_param' = page number in URL (e.g. ?page=2), 'next_button' = follows a Next link (requires href), 'load_more_button' = clicks a button that appends items without navigating, 'infinite_scroll' = renders once after scroll."
            value={config.paginationType}
            options={[
              { value: 'none', label: 'none' },
              { value: 'url_param', label: 'url_param' },
              { value: 'next_button', label: 'next_button' },
              { value: 'load_more_button', label: 'load_more_button' },
              { value: 'infinite_scroll', label: 'infinite_scroll' },
            ]}
            buildOverride={(v) => ({ paginationType: v as UpdateScraperConfigRequest['paginationType'] })}
          />
          <NumberRow
            field="maxPages"
            label="Max pages"
            tooltip="Maximum number of pages to scrape per run (or button clicks for 'load_more_button'). Leave empty for no limit."
            value={config.maxPages}
            nullable
            buildOverride={(v) => ({ maxPages: v })}
          />
          <TextRow
            field="nextPageSelector"
            label="Next page selector"
            tooltip="CSS selector for the 'Next page' link or 'Load more' button. Used by both 'next_button' (follows the href) and 'load_more_button' (clicks the element repeatedly)."
            value={config.nextPageSelector}
            buildOverride={(v) => ({ nextPageSelector: v })}
          />
          <TextRow
            field="urlParamName"
            label="URL param name"
            tooltip="Query parameter name used for pagination, e.g. 'page' produces ?page=2, or 'start' produces ?start=20. Only used when pagination type is 'url_param'."
            value={config.urlParamName}
            buildOverride={(v) => ({ urlParamName: v })}
          />
          <ToggleRow
            label="URL param is offset"
            tooltip="Enable if the URL parameter is an item offset (e.g. ?start=20 skips 20 items) rather than a page number (e.g. ?page=2)."
            value={config.urlParamIsOffset}
            buildOverride={(v) => ({ urlParamIsOffset: v })}
          />
        </tbody>
      </table>

      {updateConfig.isError && (
        <p className="text-sm text-red-600 bg-red-50 rounded px-3 py-2">
          {(updateConfig.error as Error)?.message ?? 'Failed to save'}
        </p>
      )}

      {config.analyzerNotes && (
        <div className="border-t border-gray-100 pt-4">
          <p className="flex items-center gap-1.5 text-xs font-semibold text-gray-400 uppercase tracking-wide mb-2">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
              <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
            </svg>
            AI Analyzer Notes
          </p>
          <p className="text-sm text-gray-600 bg-yellow-50 border border-yellow-100 rounded px-3 py-2">
            {config.analyzerNotes}
          </p>
        </div>
      )}
    </div>
  );
}
