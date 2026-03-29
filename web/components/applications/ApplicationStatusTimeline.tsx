'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import type { Application, ApplicationStatus, StatusHistoryEntry } from '@/types';
import {
  useUpdateApplicationStatus,
  useUpdateApplicationStatusDate,
  useUpdateApplicationScrapedAt,
} from '@/lib/hooks/useApplications';
import { Tooltip } from '@/components/ui/Tooltip';

// ── Data ──────────────────────────────────────────────────────────────────────

const CLOSED_OPTIONS: { key: ApplicationStatus; label: string; color: string }[] = [
  { key: 'ClosedNoAnswer', label: 'No Answer', color: 'text-amber-600' },
  { key: 'ClosedRejected', label: 'Rejected',  color: 'text-red-600'   },
  { key: 'ClosedHired',    label: 'Hired',     color: 'text-green-600' },
];

const CLOSED_KEYS = new Set<ApplicationStatus>(['ClosedNoAnswer', 'ClosedRejected', 'ClosedHired']);

// The 4 main steps (excluding the anchor "Job Posted")
const MAIN_STEPS: ApplicationStatus[] = ['ToApply', 'Applied', 'Interviewing'];

const STEP_LABEL: Record<string, string> = {
  ToApply: 'Preparing application', Applied: 'Applied', Interviewing: 'Interviewing', closed: 'Closed',
};

const URGENCY_TOOLTIP = 'Applications sent within 48h of posting have better chances to be considered.';

// ── Warning icon ──────────────────────────────────────────────────────────────

function WarningIcon({ urgency }: { urgency: 'orange' | 'red' }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor"
      className={`w-5 h-5 ${urgency === 'red' ? 'text-red-500' : 'text-orange-500'}`}>
      <path fillRule="evenodd" d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495zM10 5a.75.75 0 0 1 .75.75v3.5a.75.75 0 0 1-1.5 0v-3.5A.75.75 0 0 1 10 5zm0 9a1 1 0 1 0 0-2 1 1 0 0 0 0 2z" clipRule="evenodd" />
    </svg>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────

function fmt(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}
function toInput(iso: string) { return iso.slice(0, 10); }
function fromInput(v: string) { return new Date(v).toISOString(); }
function entry(history: StatusHistoryEntry[], s: ApplicationStatus) {
  return history.find(e => e.status === s) ?? null;
}
function activeClosed(history: StatusHistoryEntry[]): ApplicationStatus | null {
  return CLOSED_OPTIONS.find(o => entry(history, o.key))?.key ?? null;
}

// ── Inline date editor ────────────────────────────────────────────────────────

function DateCell({ e: ent, onSave, isSaving, placeholder = '—' }: {
  e: StatusHistoryEntry | null;
  onSave: (iso: string) => void;
  isSaving: boolean;
  placeholder?: string;
}) {
  const [editing, setEditing] = useState(false);
  const [draft,   setDraft]   = useState('');

  if (editing) return (
    <div className="flex flex-col items-center gap-1">
      <input
        type="date" value={draft} autoFocus
        onChange={ev => setDraft(ev.target.value)}
        className="text-[11px] border border-gray-300 rounded px-1 py-0.5 w-28 text-center focus:outline-none focus:ring-1 focus:ring-indigo-400"
      />
      <div className="flex gap-2">
        <button
          onClick={() => { onSave(fromInput(draft)); setEditing(false); }}
          disabled={isSaving || !draft}
          className="text-[11px] text-indigo-600 font-medium disabled:opacity-40"
        >
          {isSaving ? '…' : 'Save'}
        </button>
        <button onClick={() => setEditing(false)} className="text-[11px] text-gray-400">Cancel</button>
      </div>
    </div>
  );

  if (!ent && placeholder === '—') return <span className="text-[11px] text-gray-300 select-none">—</span>;

  if (!ent) return (
    <button
      onClick={() => { setDraft(''); setEditing(true); }}
      className="text-[11px] text-indigo-400 hover:text-indigo-600 hover:underline transition-colors"
    >
      {placeholder}
    </button>
  );

  return (
    <Tooltip content="Click to edit date" position="top" wrapperAs="span">
      <button
        onClick={() => { setDraft(toInput(ent.achievedAt)); setEditing(true); }}
        className="text-[11px] text-gray-400 hover:text-indigo-500 hover:underline transition-colors"
      >
        {fmt(ent.achievedAt)}
      </button>
    </Tooltip>
  );
}

// ── Closed dropdown ───────────────────────────────────────────────────────────

function ClosedDropdown({ onSelect, onClose }: {
  onSelect: (status: ApplicationStatus) => void;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handler(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [onClose]);

  return (
    <div
      ref={ref}
      className="absolute top-full mt-2 left-1/2 -translate-x-1/2 z-20 bg-white border border-gray-200 rounded-lg shadow-lg py-1 min-w-[130px]"
    >
      {CLOSED_OPTIONS.map(opt => (
        <button
          key={opt.key}
          onClick={() => { onSelect(opt.key); onClose(); }}
          className={`w-full text-left px-3 py-1.5 text-xs font-medium hover:bg-gray-50 transition-colors ${opt.color}`}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}

// ── Single step node ──────────────────────────────────────────────────────────

function StepNode({
  label, sublabel, dateEntry, isCurrent, isReached, isAnchor,
  onClick, onSaveDate, isSavingDate, dotColorCls, datePlaceholder, urgency, hideDate, children,
}: {
  label: string;
  sublabel?: string;
  dateEntry: StatusHistoryEntry | null;
  isCurrent: boolean;
  isReached: boolean;
  isAnchor: boolean;
  onClick?: () => void;
  onSaveDate: (iso: string) => void;
  isSavingDate: boolean;
  dotColorCls?: string;
  datePlaceholder?: string;
  urgency?: 'orange' | 'red';
  hideDate?: boolean;
  children?: React.ReactNode;
}) {
  let dotCls: string;
  let labelCls: string;

  if (isAnchor) {
    dotCls   = 'w-4 h-4 bg-gray-200 border-2 border-gray-300';
    labelCls = 'text-gray-400';
  } else if (urgency === 'orange') {
    dotCls   = '';
    labelCls = 'text-orange-700 font-semibold';
  } else if (urgency === 'red') {
    dotCls   = '';
    labelCls = 'text-red-700 font-semibold';
  } else if (isCurrent) {
    dotCls   = `w-5 h-5 border-2 ring-[3px] ring-offset-1 ring-indigo-200 ${dotColorCls ?? 'bg-indigo-600 border-indigo-600'}`;
    labelCls = 'text-indigo-700 font-semibold';
  } else if (isReached) {
    dotCls   = 'w-5 h-5 bg-indigo-400 border-2 border-indigo-400';
    labelCls = 'text-indigo-600 font-medium';
  } else {
    dotCls   = 'w-5 h-5 bg-white border-2 border-gray-300';
    labelCls = 'text-gray-400 font-medium';
  }

  // Dot or warning icon, optionally wrapped in a tooltip
  const dotEl = urgency
    ? <WarningIcon urgency={urgency} />
    : <div className={`rounded-full flex-shrink-0 transition-all duration-200 ${dotCls} ${!isAnchor ? 'hover:scale-110' : ''}`} />;

  const dotWithTooltip = urgency ? (
    <Tooltip content={URGENCY_TOOLTIP} position="top" className="flex items-center justify-center">
      {dotEl}
    </Tooltip>
  ) : dotEl;

  return (
    <div className="relative flex flex-col items-center gap-1 select-none">
      {isAnchor || !onClick
        ? dotWithTooltip
        : <button onClick={onClick} className="focus:outline-none focus:ring-2 focus:ring-indigo-300 focus:ring-offset-2 rounded-full">{dotWithTooltip}</button>
      }
      <span className={`text-xs text-center leading-tight whitespace-nowrap ${labelCls}`}>{label}</span>
      {sublabel && (
        <span className="text-[11px] text-gray-400 text-center leading-tight">{sublabel}</span>
      )}
      {!hideDate && <DateCell e={dateEntry} onSave={onSaveDate} isSaving={isSavingDate} placeholder={datePlaceholder} />}
      {children}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export function ApplicationStatusTimeline({ app }: { app: Application }) {
  const updateStatus    = useUpdateApplicationStatus(app.id);
  const updateDate      = useUpdateApplicationStatusDate(app.id);
  const updateScrapedAt = useUpdateApplicationScrapedAt(app.id);
  const [dropdownOpen, setDropdownOpen] = useState(false);

  const history       = app.statusHistory;
  const currentStatus = app.status;

  const isSavingDate  = updateDate.isPending;
  const activeClosedKey = activeClosed(history);

  const toApplyUrgency = useMemo<'orange' | 'red' | undefined>(() => {
    if (currentStatus !== 'ToApply') return undefined;
    const hoursScraped = (Date.now() - new Date(app.scrapedAt).getTime()) / 3_600_000;
    return hoursScraped > 48 ? 'red' : hoursScraped > 12 ? 'orange' : undefined;
  }, [app.scrapedAt, currentStatus]);
  const isCurrentClosed = CLOSED_KEYS.has(currentStatus);

  // The "Closed" step is reached if any closed option has a history entry
  const closedReached  = activeClosedKey !== null;
  const closedCurrent  = isCurrentClosed;

  // Dot color for the closed node (reflects which closed sub-status is active)
  const closedDotColor = currentStatus === 'ClosedNoAnswer' ? 'bg-amber-500 border-amber-500'
    : currentStatus === 'ClosedRejected'                    ? 'bg-red-500 border-red-500'
    : currentStatus === 'ClosedHired'                       ? 'bg-green-500 border-green-500'
    : undefined;

  // Label/sub-label for the Closed node
  const closedSublabel = activeClosedKey
    ? CLOSED_OPTIONS.find(o => o.key === activeClosedKey)?.label
    : undefined;

  function select(status: ApplicationStatus) {
    if (status === currentStatus) return;
    updateStatus.mutate({ status });
  }

  function saveDate(status: ApplicationStatus, iso: string) {
    updateDate.mutate({ status, achievedAt: iso });
  }

  // How far the active track should extend (0–4 intervals for 5 nodes incl. anchor)
  const totalIntervals = 4; // anchor + 3 main + 1 closed = 5 nodes, 4 gaps
  const reachedInterval = closedCurrent ? 4
    : currentStatus === 'Interviewing'  ? 3
    : currentStatus === 'Applied'       ? 2
    : currentStatus === 'ToApply'       ? 1
    : 0;

  const activeWidthPct = (reachedInterval / totalIntervals) * 100;

  return (
    <div className="bg-white rounded-lg border border-gray-200 px-6 py-5">

      {/* Timeline */}
      <div className="relative grid grid-cols-5 justify-items-center w-full">

        {/* Grey track — from center of col 1 (10%) to center of col 5 (10% from right) */}
        <div className="absolute top-[9px] left-[10%] right-[10%] h-0.5 bg-gray-200" aria-hidden />

        {/* Coloured active track — each of 4 intervals = 20% of total width */}
        {reachedInterval > 0 && (
          <div
            className="absolute top-[9px] left-[10%] h-0.5 bg-indigo-400 transition-all duration-300"
            style={{ width: `${reachedInterval * 20}%` }}
            aria-hidden
          />
        )}

        {/* ── Job Scraped anchor ── */}
        <StepNode
          label="Job Scraped"
          dateEntry={{ status: 'ToApply', achievedAt: app.scrapedAt }}
          isCurrent={false} isReached isAnchor
          onSaveDate={(iso) => updateScrapedAt.mutate(iso)}
          isSavingDate={updateScrapedAt.isPending}
        />

        {/* ── Main steps ── */}
        {MAIN_STEPS.map(key => {
          const ent        = entry(history, key);
          const isReached  = ent !== null;
          const isCurrent  = currentStatus === key;
          return (
            <StepNode
              key={key}
              label={STEP_LABEL[key]}
              dateEntry={ent}
              isCurrent={isCurrent}
              isReached={isReached}
              isAnchor={false}
              onClick={() => select(key)}
              onSaveDate={(iso) => saveDate(key, iso)}
              isSavingDate={isSavingDate && updateDate.variables?.status === key}
              urgency={key === 'ToApply' ? toApplyUrgency : undefined}
              hideDate={key === 'ToApply'}
            />
          );
        })}

        {/* ── Closed step (with dropdown) ── */}
        <StepNode
          label="Closed"
          sublabel={closedSublabel}
          dateEntry={activeClosedKey ? entry(history, activeClosedKey) : null}
          isCurrent={closedCurrent}
          isReached={closedReached}
          isAnchor={false}
          dotColorCls={closedDotColor}
          onClick={() => setDropdownOpen(o => !o)}
          onSaveDate={(iso) => activeClosedKey && saveDate(activeClosedKey, iso)}
          isSavingDate={isSavingDate && CLOSED_KEYS.has(updateDate.variables?.status as ApplicationStatus)}
        >
          {dropdownOpen && (
            <ClosedDropdown
              onSelect={(s) => select(s)}
              onClose={() => setDropdownOpen(false)}
            />
          )}
        </StepNode>

      </div>

    </div>
  );
}

