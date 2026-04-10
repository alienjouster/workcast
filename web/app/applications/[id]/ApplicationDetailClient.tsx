'use client';

import { useEffect, useRef, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useApplication, useUpdateApplicationJobAdContent, useRunApplicationScoring, useCancelApplicationScoring, useResumeVersions, useDeleteResumeVersion, useGenerateResume, useUpdateGeneratedResume, useLetterVersions, useDeleteLetterVersion, useGenerateLetter, useUpdateGeneratedLetter } from '@/lib/hooks/useApplications';
import type { ResumeOptimizationLevel } from '@/types';
import { useSettings } from '@/lib/hooks/useSettings';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { CATEGORY_STYLES, scoreColorClass, ScoringSpinner, ScoringErrorBanner, ScoringRequirementsGrid } from '@/components/scoring/ScoringShared';
import { ApplicationStatusTimeline } from '@/components/applications/ApplicationStatusTimeline';
import { StatusBadge } from '@/components/applications/StatusBadge';
import { Tooltip } from '@/components/ui/Tooltip';
import { InterviewDrillTab } from '@/components/applications/InterviewDrillTab';
import { InterviewStepsTab } from '@/components/applications/InterviewStepsTab';


// ── Tabs ──────────────────────────────────────────────────────────────────────

type Tab = 'job-ad' | 'scoring' | 'resume' | 'letter' | 'interview' | 'interview-steps';

const TABS: { id: Tab; label: string }[] = [
  { id: 'job-ad',           label: 'Job Ad' },
  { id: 'scoring',          label: 'Scoring' },
  { id: 'resume',           label: 'Custom Resume' },
  { id: 'letter',           label: 'Application Letter' },
  { id: 'interview-steps',  label: 'Interview steps' },
  { id: 'interview',        label: 'Interview drill' },
];

// ── Tab content ───────────────────────────────────────────────────────────────

function JobAdContentSection({ appId, content }: { appId: string; content: string | null }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const { mutate: saveContent, isPending } = useUpdateApplicationJobAdContent(appId);

  function startEdit() {
    setDraft(content ?? '');
    setEditing(true);
  }

  function cancel() {
    setEditing(false);
  }

  function save() {
    saveContent(draft || null, { onSuccess: () => setEditing(false) });
  }

  return (
    <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
      <div className="px-6 py-3 border-b border-gray-100 flex items-center justify-between">
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Job Ad Detail</p>
        {!editing ? (
          <button onClick={startEdit} className="text-xs text-indigo-600 hover:text-indigo-800 font-medium">
            Edit
          </button>
        ) : (
          <div className="flex items-center gap-3">
            <button onClick={save} disabled={isPending} className="text-xs text-indigo-600 hover:text-indigo-800 font-medium disabled:opacity-50">
              {isPending ? 'Saving…' : 'Save'}
            </button>
            <button onClick={cancel} disabled={isPending} className="text-xs text-gray-500 hover:text-gray-700 font-medium disabled:opacity-50">
              Cancel
            </button>
          </div>
        )}
      </div>
      {content !== null || editing ? (
        <ResumeIframe
          htmlContent={content ?? ''}
          editing={editing}
          onDraftChange={setDraft}
        />
      ) : (
        <div className="px-6 py-4">
          <div className="flex items-start gap-3 text-sm text-amber-700 bg-amber-50 rounded-md p-4">
            <span className="shrink-0 text-amber-500 mt-0.5">⚠</span>
            <div>
              <p className="font-medium">Job ad not accessible</p>
              <p className="text-xs text-amber-600 mt-0.5">
                The page could not be fetched or returned insufficient content. You can paste the job ad text manually using the Edit button.
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function JobAdTab({ app }: { app: ReturnType<typeof useApplication>['data'] }) {
  if (!app) return null;

  return (
    <div className="space-y-6">
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="px-6 py-4 border-b border-gray-100">
          <h2 className="text-base font-semibold text-gray-900">
            {app.title ?? '(no title)'}
          </h2>
          {app.company && <p className="text-sm text-gray-500 mt-0.5">{app.company}</p>}
        </div>
        <div className="px-6 py-4 grid grid-cols-2 gap-4 text-sm">
          <Field label="URL">
            <a
              href={app.url}
              target="_blank"
              rel="noopener noreferrer"
              className="text-indigo-600 hover:underline break-all"
            >
              {app.url}
            </a>
          </Field>
          {app.location && <Field label="Location">{app.location}</Field>}
          {app.salaryRaw && <Field label="Salary">{app.salaryRaw}</Field>}
          {app.postedAt && (
            <Field label="Posted">{new Date(app.postedAt).toLocaleDateString()}</Field>
          )}
          <Field label="Applied on">{new Date(app.createdAt).toLocaleDateString()}</Field>
          {app.externalId && <Field label="External ID">{app.externalId}</Field>}
        </div>
        {app.description && (
          <div className="px-6 py-4 border-t border-gray-100">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-2">Description</p>
            <p className="text-sm text-gray-700 whitespace-pre-wrap">{app.description}</p>
          </div>
        )}
      </div>

      <JobAdContentSection appId={app.id} content={app.jobAdContent} />
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-0.5">{label}</p>
      <div className="text-gray-800">{children}</div>
    </div>
  );
}

// ── AI sparkle icon ───────────────────────────────────────────────────────────

function SparkleIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className={className}>
      <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
    </svg>
  );
}

// ── Scoring tab ────────────────────────────────────────────────────────────────

function ScoringTab({ app, onNavigateToJobAd }: { app: ReturnType<typeof useApplication>['data']; onNavigateToJobAd: () => void }) {
  const { data: settings } = useSettings();
  const runScoring = useRunApplicationScoring(app?.id ?? '');
  const cancelScoring = useCancelApplicationScoring(app?.id ?? '');

  if (!app) return null;

  const hasResume       = settings?.hasResume ?? false;
  const hasJobAdContent = !!app.jobAdContent;
  const isRunning       = app.isScoringPending || runScoring.isPending;
  const hasScore        = app.overallScore != null;

  // ── Pending spinner ────────────────────────────────────────────────────────
  if (isRunning) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 flex flex-col items-center gap-4">
        <ScoringSpinner />
        <p className="text-sm text-gray-400">Scoring in progress…</p>
        {app.isScoringPending && !runScoring.isPending && (
          <button
            onClick={() => cancelScoring.mutate()}
            disabled={cancelScoring.isPending}
            className="text-xs text-gray-400 hover:text-gray-600 underline underline-offset-2 disabled:opacity-50"
          >
            {cancelScoring.isPending ? 'Cancelling…' : 'Cancel'}
          </button>
        )}
      </div>
    );
  }

  // ── Error banner (shown above results or above the "no score" box) ─────────
  const errorBanner = app.lastScoringError
    ? <ScoringErrorBanner error={app.lastScoringError} />
    : null;

  // ── No score yet ───────────────────────────────────────────────────────────
  if (!hasScore) {
    return (
      <div className="space-y-3">
        {errorBanner}
        <div className="bg-white rounded-lg border border-gray-200 p-8 flex flex-col items-center gap-4 text-center">
          <div className="w-12 h-12 rounded-full bg-indigo-50 flex items-center justify-center">
            <SparkleIcon className="w-6 h-6 text-indigo-400" />
          </div>
          <div>
            <p className="text-sm font-medium text-gray-700">No scoring data yet</p>
            <p className="text-xs text-gray-400 mt-1">
              AI scoring compares your resume against this job ad and produces a match score,
              requirement breakdown, and recommendation.
            </p>
          </div>
          {!hasResume && (
            <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-2">
              Upload a resume from the{' '}
              <a href="/settings" className="underline hover:text-amber-800">Settings page</a>
              {' '}to enable scoring.
            </p>
          )}
          {!hasJobAdContent && (
            <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-2">
              Job Ad Detail is missing. Open the{' '}
              <button
                onClick={onNavigateToJobAd}
                className="underline hover:text-amber-800 font-medium"
              >Job Ad tab</button>
              {' '}and fetch the content before scoring.
            </p>
          )}
          <button
            onClick={() => runScoring.mutate()}
            disabled={!hasResume || !hasJobAdContent || runScoring.isPending}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <SparkleIcon className="w-4 h-4" />
            Score now
          </button>
        </div>
      </div>
    );
  }

  // ── Score exists ───────────────────────────────────────────────────────────
  return (
    <div className="space-y-4">
      {errorBanner}

      {/* Score + summary + re-score button */}
      <div className="bg-white rounded-lg border border-gray-200 px-6 py-5 flex items-start gap-6">
        <div className="shrink-0 flex flex-col items-center gap-2 pt-0.5">
          <div className="text-center">
            <span className={`text-4xl font-bold ${scoreColorClass(app.overallScore!)}`}>
              {Math.round(app.overallScore!)}
            </span>
            <span className="text-lg font-normal text-gray-400">/100</span>
            {app.scoredAt && (
              <p className="text-xs text-gray-400 mt-0.5">
                {new Date(app.scoredAt).toLocaleDateString()}
              </p>
            )}
          </div>
          <button
            onClick={() => runScoring.mutate()}
            disabled={!hasResume || !hasJobAdContent || runScoring.isPending}
            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md border border-gray-200 bg-white text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <SparkleIcon className="w-3.5 h-3.5" />
            Re-score
          </button>
        </div>
        <div className="flex-1 space-y-3">
          {app.recommendation && (
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-1">Recommendation</p>
              <p className="text-sm text-gray-800 leading-relaxed">{app.recommendation}</p>
            </div>
          )}
          {app.recommendation && app.summary && <hr className="border-gray-100" />}
          {app.summary && (
            <p className="text-xs text-gray-500 leading-relaxed">{app.summary}</p>
          )}
        </div>
      </div>

      {/* Requirements */}
      {app.requirements.length > 0 && (
        <ScoringRequirementsGrid requirements={app.requirements} />
      )}
    </div>
  );
}

// ── Application Letter tab ────────────────────────────────────────────────────

function LetterTab({ app }: { app: ReturnType<typeof useApplication>['data'] }) {
  const { data: settings } = useSettings();
  const { data: versions = [], isLoading: isLoadingVersions } = useLetterVersions(app?.id ?? '');
  const generate = useGenerateLetter(app?.id ?? '');
  const update = useUpdateGeneratedLetter(app?.id ?? '');
  const deleteVersion = useDeleteLetterVersion(app?.id ?? '');

  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [selectedVersionId, setSelectedVersionId] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [versionsExpanded, setVersionsExpanded] = useState(false);

  if (!app) return null;

  // Sorted descending by version number (latest first)
  const sortedVersions = [...versions].sort((a, b) => b.versionNumber - a.versionNumber);

  // The currently viewed version: explicit selection or the latest one
  const selectedVersion = selectedVersionId
    ? (sortedVersions.find(v => v.id === selectedVersionId) ?? sortedVersions[0] ?? null)
    : (sortedVersions[0] ?? null);

  const hasResume   = settings?.hasResume ?? false;
  const hasScoring  = app.overallScore != null;
  const canGenerate = hasResume && hasScoring;
  const isGenerating = app.isLetterGenerationPending || generate.isPending;

  const missingItems: string[] = [];
  if (!hasResume)  missingItems.push('resume content (Settings)');
  if (!hasScoring) missingItems.push('scoring data (Scoring tab)');

  function startEdit() {
    setDraft(selectedVersion?.htmlContent ?? '');
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setDraft('');
  }

  function saveEdit() {
    if (draft === selectedVersion?.htmlContent) {
      setEditing(false);
      setDraft('');
      return;
    }
    update.mutate(draft, {
      onSuccess: () => {
        setEditing(false);
        setDraft('');
        setSelectedVersionId(null); // auto-select the newly created version
      },
    });
  }

  const confirmDeleteVersion = sortedVersions.find(v => v.id === confirmDeleteId);

  return (
    <div className="space-y-4">
      {/* Generate bar */}
      {!editing && (
        <div className="bg-white rounded-lg border border-gray-200 p-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              {!canGenerate && missingItems.length > 0 && (
                <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-1.5">
                  Requires: {missingItems.join(', ')}
                </p>
              )}
            </div>
            <button
              onClick={() => { generate.mutate(); setSelectedVersionId(null); }}
              disabled={!canGenerate || isGenerating}
              className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <SparkleIcon className="w-4 h-4" />
              {isGenerating ? 'Generating…' : sortedVersions.length > 0 ? 'Re-generate' : 'Generate'}
            </button>
          </div>
        </div>
      )}

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div>
          {selectedVersion && !isGenerating && !editing && (
            <span className="text-xs text-gray-400">
              {selectedVersion.isManualEdit ? 'Edited' : 'Generated'} {new Date(selectedVersion.generatedAt).toLocaleString()} · {selectedVersion.modelUsed}
            </span>
          )}
        </div>
        <div className="flex items-center gap-3">
          {editing && (
            <>
              <button
                onClick={saveEdit}
                disabled={update.isPending}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium disabled:opacity-50"
              >
                {update.isPending ? 'Saving…' : 'Save'}
              </button>
              <button
                onClick={cancelEdit}
                disabled={update.isPending}
                className="text-xs text-gray-500 hover:text-gray-700 font-medium disabled:opacity-50"
              >
                Cancel
              </button>
            </>
          )}
          {!editing && selectedVersion && !isGenerating && (
            <>
              <button
                onClick={startEdit}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Edit
              </button>
              <button
                onClick={() => {
                  const blob = new Blob([selectedVersion.htmlContent], { type: 'text/html' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = 'application-letter.html';
                  a.click();
                  URL.revokeObjectURL(url);
                }}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Download
              </button>
              <button
                onClick={() => {
                  const win = window.open('', '_blank');
                  if (!win) return;
                  win.document.write(selectedVersion.htmlContent);
                  win.document.close();
                  win.focus();
                  win.print();
                }}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Print
              </button>
              {sortedVersions.length > 0 && (
                <button
                  onClick={() => setVersionsExpanded(v => !v)}
                  className="flex items-center gap-1 text-xs text-gray-400 hover:text-gray-600 transition-colors"
                >
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 16 16"
                    fill="currentColor"
                    className={`w-3.5 h-3.5 transition-transform duration-200 ${versionsExpanded ? 'rotate-180' : ''}`}
                  >
                    <path fillRule="evenodd" d="M4.22 6.22a.75.75 0 0 1 1.06 0L8 8.94l2.72-2.72a.75.75 0 1 1 1.06 1.06l-3.25 3.25a.75.75 0 0 1-1.06 0L4.22 7.28a.75.75 0 0 1 0-1.06Z" clipRule="evenodd" />
                  </svg>
                  {sortedVersions.length} version{sortedVersions.length !== 1 ? 's' : ''}
                </button>
              )}
            </>
          )}
        </div>
      </div>

      {app.lastLetterGenerationError && !isGenerating && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md px-4 py-3">
          {app.lastLetterGenerationError}
        </div>
      )}

      {isGenerating && (
        <div className="bg-white rounded-lg border border-gray-200 p-12 flex flex-col items-center gap-4 text-center">
          <ScoringSpinner />
          <p className="text-sm text-gray-400">Generating your application letter…</p>
          <p className="text-xs text-gray-300">This usually takes under a minute.</p>
        </div>
      )}

      {!isGenerating && isLoadingVersions && (
        <div className="bg-white rounded-lg border border-gray-200 p-8 flex justify-center">
          <ScoringSpinner />
        </div>
      )}

      {!isGenerating && !isLoadingVersions && sortedVersions.length === 0 && (
        <div className="bg-white rounded-lg border border-dashed border-gray-200 p-12 text-center">
          <div className="w-12 h-12 rounded-full bg-indigo-50 flex items-center justify-center mx-auto mb-3">
            <SparkleIcon className="w-6 h-6 text-indigo-400" />
          </div>
          <p className="text-sm font-medium text-gray-700">No letter generated yet</p>
          <p className="text-xs text-gray-400 mt-1 max-w-sm mx-auto leading-relaxed text-center">
            Generates a concise, professional cover letter (~half a page) tailored to this job ad,
            highlighting your strongest matching qualifications from the scoring analysis.
          </p>
        </div>
      )}

      {!isGenerating && sortedVersions.length > 0 && selectedVersion && (
        <div className="flex gap-3 items-start">
          {/* Letter preview */}
          <div className="flex-1 min-w-0 bg-white rounded-lg border border-gray-200 overflow-hidden">
            <ResumeIframe
              htmlContent={selectedVersion.htmlContent}
              editing={editing}
              onDraftChange={setDraft}
            />
          </div>

          {/* Version list sidebar — right side, only when expanded */}
          {versionsExpanded && (
            <div className="w-44 flex-shrink-0 relative" style={{ maxHeight: '900px' }}>
              <div className="bg-white rounded-lg border border-gray-200 overflow-y-auto divide-y divide-gray-100" style={{ maxHeight: '900px' }}>
                {sortedVersions.map((v) => {
                  const isSelected = v.id === selectedVersion.id;
                  return (
                    <div
                      key={v.id}
                      onClick={() => { if (!editing) setSelectedVersionId(v.id); }}
                      className={`group relative flex flex-col gap-0.5 px-3 py-2.5 cursor-pointer transition-colors ${
                        isSelected ? 'bg-indigo-50' : 'hover:bg-gray-50'
                      } ${editing ? 'cursor-default opacity-60' : ''}`}
                    >
                      <div className="flex items-center justify-between gap-1">
                        <span className={`text-xs font-semibold ${isSelected ? 'text-indigo-700' : 'text-gray-700'}`}>
                          v{v.versionNumber}
                        </span>
                        {!editing && (
                          <Tooltip content="Delete this version" position="top" wrapperAs="span">
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(v.id); }}
                            className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-300 hover:text-red-500 p-0.5 rounded"
                          >
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
                              <path fillRule="evenodd" d="M5 3.25V4H2.75a.75.75 0 0 0 0 1.5h.3l.815 8.15A1.5 1.5 0 0 0 5.357 15h5.285a1.5 1.5 0 0 0 1.493-1.35l.815-8.15h.3a.75.75 0 0 0 0-1.5H11v-.75A2.25 2.25 0 0 0 8.75 1h-1.5A2.25 2.25 0 0 0 5 3.25Zm2.25-.75a.75.75 0 0 0-.75.75V4h3v-.75a.75.75 0 0 0-.75-.75h-1.5ZM6.05 6a.75.75 0 0 1 .787.713l.275 5.5a.75.75 0 0 1-1.498.075l-.275-5.5A.75.75 0 0 1 6.05 6Zm3.9 0a.75.75 0 0 1 .712.787l-.275 5.5a.75.75 0 0 1-1.498-.075l.275-5.5a.75.75 0 0 1 .786-.711Z" clipRule="evenodd" />
                            </svg>
                          </button>
                          </Tooltip>
                        )}
                      </div>
                      <p className="text-[10px] text-gray-400 leading-tight">
                        {new Date(v.generatedAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                      </p>
                      <div className="mt-0.5 flex flex-wrap gap-1">
                        {v.isManualEdit && (
                          <span className="inline-block text-[10px] px-1.5 py-0.5 rounded bg-gray-100 text-gray-500">
                            Edited
                          </span>
                        )}
                      </div>
                      <p className="text-[10px] text-gray-300 truncate leading-tight mt-0.5" title={v.modelUsed}>
                        {v.modelUsed}
                      </p>
                    </div>
                  );
                })}
              </div>
              {/* Bottom fade — only visible when the list overflows */}
              <div className="pointer-events-none absolute bottom-0 left-0 right-0 h-10 rounded-b-lg bg-gradient-to-t from-white to-transparent" />
            </div>
          )}
        </div>
      )}

      {/* Delete confirmation modal */}
      {confirmDeleteId && confirmDeleteVersion && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm mx-4">
            <h3 className="text-sm font-semibold text-gray-900 mb-2">Delete v{confirmDeleteVersion.versionNumber}?</h3>
            <p className="text-xs text-gray-500 mb-4">
              This will permanently delete version {confirmDeleteVersion.versionNumber}
              {confirmDeleteVersion.isManualEdit ? ' (manual edit)' : ''}.
              This action cannot be undone.
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setConfirmDeleteId(null)}
                disabled={deleteVersion.isPending}
                className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:text-gray-800 border border-gray-200 rounded-md disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  deleteVersion.mutate(confirmDeleteId, {
                    onSuccess: () => {
                      setConfirmDeleteId(null);
                      setSelectedVersionId(null);
                    },
                  });
                }}
                disabled={deleteVersion.isPending}
                className="px-3 py-1.5 text-xs font-medium text-white bg-red-600 hover:bg-red-700 rounded-md disabled:opacity-50"
              >
                {deleteVersion.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Editable resume iframe ────────────────────────────────────────────────────

function ResumeIframe({
  htmlContent,
  editing,
  onDraftChange,
}: {
  htmlContent: string;
  editing: boolean;
  onDraftChange: (html: string) => void;
}) {
  const iframeRef = useRef<HTMLIFrameElement>(null);

  // When switching into edit mode: inject the content, enable designMode, and
  // listen for input events on the iframe document (they don't bubble to the
  // iframe element itself, so onInput on <iframe> would never fire).
  useEffect(() => {
    const doc = iframeRef.current?.contentDocument;
    if (!doc) return;

    if (editing) {
      doc.open();
      doc.write(htmlContent);
      doc.close();
      doc.designMode = 'on';

      const handleInput = () => onDraftChange(doc.documentElement.outerHTML);
      doc.addEventListener('input', handleInput);
      return () => doc.removeEventListener('input', handleInput);
    } else {
      doc.designMode = 'off';
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing]);

  return (
    <iframe
      ref={iframeRef}
      srcDoc={editing ? undefined : htmlContent}
      sandbox="allow-same-origin"
      className="w-full border-0"
      style={{ minHeight: '900px' }}
      title="Generated resume"
    />
  );
}

// ── Custom Resume tab ──────────────────────────────────────────────────────────

const OPTIMIZATION_OPTIONS: { value: ResumeOptimizationLevel; label: string; description: string }[] = [
  { value: 'None',   label: 'None',   description: 'Strict — only information from your resume, word for word.' },
  { value: 'Light',  label: 'Light',  description: 'Synonyms only — words may be replaced to better match the job ad.' },
  { value: 'Medium', label: 'Medium', description: 'Rewording — experiences may be rephrased to align with the job ad.' },
  { value: 'Heavy',  label: 'Heavy',  description: 'Gap-filling — similar skills may be added to cover partial gaps.' },
];

const OPTIMIZATION_BADGE: Record<string, string> = {
  None:   'bg-gray-100 text-gray-600',
  Light:  'bg-blue-50 text-blue-600',
  Medium: 'bg-amber-50 text-amber-600',
  Heavy:  'bg-orange-50 text-orange-600',
};

function ResumeTab({ app }: { app: ReturnType<typeof useApplication>['data'] }) {
  const { data: settings } = useSettings();
  const { data: versions = [], isLoading: isLoadingVersions } = useResumeVersions(app?.id ?? '');
  const generate = useGenerateResume(app?.id ?? '');
  const update = useUpdateGeneratedResume(app?.id ?? '');
  const deleteVersion = useDeleteResumeVersion(app?.id ?? '');

  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');
  const [optimizationLevel, setOptimizationLevel] = useState<ResumeOptimizationLevel>('None');
  const [selectedVersionId, setSelectedVersionId] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [versionsExpanded, setVersionsExpanded] = useState(false);

  if (!app) return null;

  // Sorted descending by version number (latest first)
  const sortedVersions = [...versions].sort((a, b) => b.versionNumber - a.versionNumber);

  // The currently viewed version: explicit selection or the latest one
  const selectedVersion = selectedVersionId
    ? (sortedVersions.find(v => v.id === selectedVersionId) ?? sortedVersions[0] ?? null)
    : (sortedVersions[0] ?? null);

  const highlightsVisible = !(selectedVersion?.htmlContent.includes('<!--mark-->') ?? false);

  function toggleHighlights() {
    if (!selectedVersion) return;
    const newHtml = highlightsVisible
      ? selectedVersion.htmlContent.replace(/<mark>/g, '<!--mark-->').replace(/<\/mark>/g, '<!--/mark-->')
      : selectedVersion.htmlContent.replace(/<!--mark-->/g, '<mark>').replace(/<!--\/mark-->/g, '</mark>');
    update.mutate(newHtml, { onSuccess: () => setSelectedVersionId(null) });
  }

  const hasResume    = settings?.hasResume ?? false;
  const hasTemplate  = settings?.hasResumeTemplate ?? false;
  const hasScoring   = app.overallScore != null;
  const canGenerate  = hasResume && hasTemplate && hasScoring;
  const isGenerating = app.isResumeGenerationPending || generate.isPending;

  const missingItems: string[] = [];
  if (!hasResume)   missingItems.push('resume content (Settings)');
  if (!hasTemplate) missingItems.push('resume template (Settings)');
  if (!hasScoring)  missingItems.push('scoring data (Scoring tab)');

  function startEdit() {
    setDraft(selectedVersion?.htmlContent ?? '');
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setDraft('');
  }

  function saveEdit() {
    if (draft === selectedVersion?.htmlContent) {
      setEditing(false);
      setDraft('');
      return;
    }
    update.mutate(draft, {
      onSuccess: () => {
        setEditing(false);
        setDraft('');
        setSelectedVersionId(null); // auto-select the newly created version
      },
    });
  }

  const confirmDeleteVersion = sortedVersions.find(v => v.id === confirmDeleteId);

  return (
    <div className="space-y-4">
      {/* Optimization level selector */}
      <div className={`bg-white rounded-lg border border-gray-200 p-4 ${editing ? 'opacity-50 pointer-events-none select-none' : ''}`}>
        <p className="text-xs font-medium text-gray-500 mb-3">Optimization level</p>
        <div className="grid grid-cols-4 gap-2">
          {OPTIMIZATION_OPTIONS.map(({ value, label, description }) => (
            <button
              key={value}
              onClick={() => setOptimizationLevel(value)}
              disabled={isGenerating || editing}
              className={`flex flex-col items-start gap-1 rounded-md border px-3 py-2.5 text-left transition-colors disabled:opacity-40 disabled:cursor-not-allowed ${
                optimizationLevel === value
                  ? 'border-indigo-500 bg-indigo-50 text-indigo-700'
                  : 'border-gray-200 hover:border-gray-300 text-gray-600'
              }`}
            >
              <span className="text-xs font-semibold">{label}</span>
              <span className="text-[11px] leading-tight text-gray-400">{description}</span>
            </button>
          ))}
        </div>
        <div className="flex items-center justify-between mt-3 pt-3 border-t border-gray-100">
          <div className="flex items-center gap-3">
            <span className="text-xs text-gray-500">Highlight optimization</span>
            <button
              role="switch"
              aria-checked={highlightsVisible}
              onClick={toggleHighlights}
              disabled={update.isPending || !selectedVersion || editing}
              className={`relative inline-flex h-5 w-9 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:ring-offset-2 disabled:opacity-50 ${highlightsVisible ? 'bg-indigo-600' : 'bg-gray-200'}`}
            >
              <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${highlightsVisible ? 'translate-x-4' : 'translate-x-0'}`} />
            </button>
          </div>
          <div className="flex items-center gap-3">
            {!canGenerate && missingItems.length > 0 && (
              <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-1.5">
                Requires: {missingItems.join(', ')}
              </p>
            )}
            <button
              onClick={() => { generate.mutate(optimizationLevel); setSelectedVersionId(null); }}
              disabled={!canGenerate || isGenerating || editing}
              className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
            >
              <SparkleIcon className="w-4 h-4" />
              {isGenerating ? 'Generating…' : sortedVersions.length > 0 ? 'Re-generate' : 'Generate'}
            </button>
          </div>
        </div>
      </div>

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {selectedVersion && !isGenerating && !editing && (
            <span className="text-xs text-gray-400">
              {selectedVersion.isManualEdit ? 'Edited' : 'Generated'} {new Date(selectedVersion.generatedAt).toLocaleString()} · {selectedVersion.modelUsed}
            </span>
          )}
        </div>
        <div className="flex items-center gap-3">
          {editing && (
            <>
              <button
                onClick={saveEdit}
                disabled={update.isPending}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium disabled:opacity-50"
              >
                {update.isPending ? 'Saving…' : 'Save'}
              </button>
              <button
                onClick={cancelEdit}
                disabled={update.isPending}
                className="text-xs text-gray-500 hover:text-gray-700 font-medium disabled:opacity-50"
              >
                Cancel
              </button>
            </>
          )}
          {!editing && selectedVersion && !isGenerating && (
            <>
              <button
                onClick={startEdit}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Edit
              </button>
              <button
                onClick={() => {
                  const blob = new Blob([selectedVersion.htmlContent], { type: 'text/html' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = 'resume.html';
                  a.click();
                  URL.revokeObjectURL(url);
                }}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Download
              </button>
              <button
                onClick={() => {
                  const win = window.open('', '_blank');
                  if (!win) return;
                  win.document.write(selectedVersion.htmlContent);
                  win.document.close();
                  win.focus();
                  win.print();
                }}
                className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
              >
                Print
              </button>
              {sortedVersions.length > 0 && (
                <button
                  onClick={() => setVersionsExpanded(v => !v)}
                  className="flex items-center gap-1 text-xs text-gray-400 hover:text-gray-600 transition-colors"
                >
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 16 16"
                    fill="currentColor"
                    className={`w-3.5 h-3.5 transition-transform duration-200 ${versionsExpanded ? 'rotate-180' : ''}`}
                  >
                    <path fillRule="evenodd" d="M4.22 6.22a.75.75 0 0 1 1.06 0L8 8.94l2.72-2.72a.75.75 0 1 1 1.06 1.06l-3.25 3.25a.75.75 0 0 1-1.06 0L4.22 7.28a.75.75 0 0 1 0-1.06Z" clipRule="evenodd" />
                  </svg>
                  {sortedVersions.length} version{sortedVersions.length !== 1 ? 's' : ''}
                </button>
              )}
            </>
          )}
        </div>
      </div>

      {app.lastResumeGenerationError && !isGenerating && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md px-4 py-3">
          {app.lastResumeGenerationError}
        </div>
      )}

      {isGenerating && (
        <div className="bg-white rounded-lg border border-gray-200 p-12 flex flex-col items-center gap-4 text-center">
          <ScoringSpinner />
          <p className="text-sm text-gray-400">Generating your tailored resume…</p>
          <p className="text-xs text-gray-300">This may take up to a minute.</p>
        </div>
      )}

      {!isGenerating && isLoadingVersions && (
        <div className="bg-white rounded-lg border border-gray-200 p-8 flex justify-center">
          <ScoringSpinner />
        </div>
      )}

      {!isGenerating && !isLoadingVersions && sortedVersions.length === 0 && (
        <div className="bg-white rounded-lg border border-dashed border-gray-200 p-12 text-center">
          <div className="w-12 h-12 rounded-full bg-indigo-50 flex items-center justify-center mx-auto mb-3">
            <SparkleIcon className="w-6 h-6 text-indigo-400" />
          </div>
          <p className="text-sm font-medium text-gray-700">No resume generated yet</p>
          <p className="text-xs text-gray-400 mt-1 max-w-sm mx-auto leading-relaxed text-center">
            Combines your resume content, this job ad, and the scoring analysis to produce an ATS-friendly tailored version using your HTML template — maximising keyword alignment with the job requirements without inventing any skills or experience.
          </p>
        </div>
      )}

      {!isGenerating && sortedVersions.length > 0 && selectedVersion && (
        <div className="flex gap-3 items-start">
          {/* Resume preview */}
          <div className="flex-1 min-w-0 bg-white rounded-lg border border-gray-200 overflow-hidden">
            <ResumeIframe
              htmlContent={selectedVersion.htmlContent}
              editing={editing}
              onDraftChange={setDraft}
            />
          </div>

          {/* Version list sidebar */}
          {versionsExpanded && (
            <div className="w-44 flex-shrink-0 relative" style={{ maxHeight: '900px' }}>
              <div className="bg-white rounded-lg border border-gray-200 overflow-y-auto divide-y divide-gray-100" style={{ maxHeight: '900px' }}>
                {sortedVersions.map((v) => {
                    const isSelected = v.id === selectedVersion.id;
                    return (
                      <div
                        key={v.id}
                        onClick={() => { if (!editing) setSelectedVersionId(v.id); }}
                        className={`group relative flex flex-col gap-0.5 px-3 py-2.5 cursor-pointer transition-colors ${
                          isSelected
                            ? 'bg-indigo-50'
                            : 'hover:bg-gray-50'
                        } ${editing ? 'cursor-default opacity-60' : ''}`}
                      >
                        <div className="flex items-center justify-between gap-1">
                          <span className={`text-xs font-semibold ${isSelected ? 'text-indigo-700' : 'text-gray-700'}`}>
                            v{v.versionNumber}
                          </span>
                          {!editing && (
                            <Tooltip content="Delete this version" position="top" wrapperAs="span">
                            <button
                              onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(v.id); }}
                              className="opacity-0 group-hover:opacity-100 transition-opacity text-gray-300 hover:text-red-500 p-0.5 rounded"
                            >
                              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
                                <path fillRule="evenodd" d="M5 3.25V4H2.75a.75.75 0 0 0 0 1.5h.3l.815 8.15A1.5 1.5 0 0 0 5.357 15h5.285a1.5 1.5 0 0 0 1.493-1.35l.815-8.15h.3a.75.75 0 0 0 0-1.5H11v-.75A2.25 2.25 0 0 0 8.75 1h-1.5A2.25 2.25 0 0 0 5 3.25Zm2.25-.75a.75.75 0 0 0-.75.75V4h3v-.75a.75.75 0 0 0-.75-.75h-1.5ZM6.05 6a.75.75 0 0 1 .787.713l.275 5.5a.75.75 0 0 1-1.498.075l-.275-5.5A.75.75 0 0 1 6.05 6Zm3.9 0a.75.75 0 0 1 .712.787l-.275 5.5a.75.75 0 0 1-1.498-.075l.275-5.5a.75.75 0 0 1 .786-.711Z" clipRule="evenodd" />
                              </svg>
                            </button>
                            </Tooltip>
                          )}
                        </div>
                        <p className="text-[10px] text-gray-400 leading-tight">
                          {new Date(v.generatedAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                        </p>
                        <div className="mt-0.5 flex flex-wrap gap-1">
                          {v.optimizationLevel && (
                            <span className={`inline-block text-[10px] px-1.5 py-0.5 rounded ${OPTIMIZATION_BADGE[v.optimizationLevel] ?? 'bg-gray-100 text-gray-500'}`}>
                              {v.optimizationLevel}
                            </span>
                          )}
                          {v.isManualEdit && (
                            <span className="inline-block text-[10px] px-1.5 py-0.5 rounded bg-gray-100 text-gray-500">
                              Edited
                            </span>
                          )}
                        </div>
                        <p className="text-[10px] text-gray-300 truncate leading-tight mt-0.5" title={v.modelUsed}>
                          {v.modelUsed}
                        </p>
                      </div>
                    );
                  })}
                </div>
              {/* Bottom fade — only visible when the list overflows */}
              <div className="pointer-events-none absolute bottom-0 left-0 right-0 h-10 rounded-b-lg bg-gradient-to-t from-white to-transparent" />
            </div>
          )}
        </div>
      )}

      {/* Delete confirmation modal */}
      {confirmDeleteId && confirmDeleteVersion && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm mx-4">
            <h3 className="text-sm font-semibold text-gray-900 mb-2">Delete v{confirmDeleteVersion.versionNumber}?</h3>
            <p className="text-xs text-gray-500 mb-4">
              This will permanently delete version {confirmDeleteVersion.versionNumber}
              {confirmDeleteVersion.isManualEdit ? ' (manual edit)' : confirmDeleteVersion.optimizationLevel ? ` (${confirmDeleteVersion.optimizationLevel})` : ''}.
              This action cannot be undone.
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setConfirmDeleteId(null)}
                disabled={deleteVersion.isPending}
                className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:text-gray-800 border border-gray-200 rounded-md disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={() => {
                  deleteVersion.mutate(confirmDeleteId, {
                    onSuccess: () => {
                      setConfirmDeleteId(null);
                      setSelectedVersionId(null);
                    },
                  });
                }}
                disabled={deleteVersion.isPending}
                className="px-3 py-1.5 text-xs font-medium text-white bg-red-600 hover:bg-red-700 rounded-md disabled:opacity-50"
              >
                {deleteVersion.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function ApplicationDetailClient() {
  const { id } = useParams<{ id: string }>();
  const [activeTab, setActiveTab] = useState<Tab>('job-ad');
  const { data: app, isLoading, error } = useApplication(id);

  if (isLoading) return <LoadingSpinner />;

  if (error || !app) {
    return (
      <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
        Application not found.
      </div>
    );
  }

  return (
    <div>
      {/* Header */}
      <div className="mb-6">
        <Link href="/applications" className="text-sm text-indigo-500 hover:text-indigo-700 hover:underline">
          ← Applications
        </Link>
        <div className="flex items-center gap-3 mt-2">
          <h1 className="text-2xl font-bold text-gray-900">
            {app.title ?? '(no title)'}
          </h1>
          <StatusBadge status={app.status} />
        </div>
        {app.company && (
          <p className="text-sm text-gray-500 mt-0.5">{app.company}</p>
        )}
        {app.isTrashed && (
          <span className="inline-block mt-2 text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full">
            In trash
          </span>
        )}
      </div>

      {/* Status timeline — always visible */}
      <div className="mb-6">
        <ApplicationStatusTimeline app={app} />
      </div>

      {/* Tab bar */}
      <div className="flex items-center gap-1 mb-6 border-b border-gray-200">
        {TABS.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
              activeTab === tab.id
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {/* Tab content */}
      {activeTab === 'job-ad'    && <JobAdTab app={app} />}
      {activeTab === 'scoring'   && <ScoringTab app={app} onNavigateToJobAd={() => setActiveTab('job-ad')} />}
      {activeTab === 'resume'    && <ResumeTab app={app} />}
      {activeTab === 'letter'    && <LetterTab app={app} />}
      {activeTab === 'interview' && <InterviewDrillTab app={app} />}
      {activeTab === 'interview-steps' && <InterviewStepsTab appId={app.id} />}
    </div>
  );
}
