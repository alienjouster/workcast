'use client';

import { useEffect, useRef, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useApplication, useUpdateApplicationJobAdContent, useRunApplicationScoring, useCancelApplicationScoring, useLatestGeneratedResume, useGenerateResume, useUpdateGeneratedResume } from '@/lib/hooks/useApplications';
import { useSettings } from '@/lib/hooks/useSettings';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { CATEGORY_STYLES, scoreColorClass, ScoringSpinner, ScoringErrorBanner, ScoringRequirementsGrid } from '@/components/scoring/ScoringShared';
import { ApplicationStatusTimeline } from '@/components/applications/ApplicationStatusTimeline';
import type { ApplicationStatus } from '@/types';

const STATUS_BADGE: Record<ApplicationStatus, { label: string; cls: string }> = {
  ToApply:        { label: 'Preparing application', cls: 'bg-gray-100 text-gray-600'     },
  Applied:        { label: 'Applied',               cls: 'bg-blue-100 text-blue-700'     },
  Interviewing:   { label: 'Interviewing',          cls: 'bg-indigo-100 text-indigo-700' },
  ClosedNoAnswer: { label: 'Closed — No Answer',    cls: 'bg-amber-100 text-amber-700'   },
  ClosedRejected: { label: 'Closed — Rejected',     cls: 'bg-red-100 text-red-700'       },
  ClosedHired:    { label: 'Closed — Hired',        cls: 'bg-green-100 text-green-700'   },
};

function StatusBadge({ status }: { status: ApplicationStatus }) {
  const b = STATUS_BADGE[status];
  if (!b) return null;
  return (
    <span className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${b.cls}`}>
      {b.label}
    </span>
  );
}


// ── Tabs ──────────────────────────────────────────────────────────────────────

type Tab = 'job-ad' | 'scoring' | 'resume' | 'letter';

const TABS: { id: Tab; label: string }[] = [
  { id: 'job-ad',  label: 'Job Ad' },
  { id: 'scoring', label: 'Scoring' },
  { id: 'resume',  label: 'Custom Resume' },
  { id: 'letter',  label: 'Application Letter' },
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

function ScoringTab({ app }: { app: ReturnType<typeof useApplication>['data'] }) {
  const { data: settings } = useSettings();
  const runScoring = useRunApplicationScoring(app?.id ?? '');
  const cancelScoring = useCancelApplicationScoring(app?.id ?? '');

  if (!app) return null;

  const hasResume  = settings?.hasResume ?? false;
  const isRunning  = app.isScoringPending || runScoring.isPending;
  const hasScore   = app.overallScore != null;

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
          <button
            onClick={() => runScoring.mutate()}
            disabled={!hasResume || runScoring.isPending}
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
            disabled={!hasResume || runScoring.isPending}
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

function DummyTab({ label }: { label: string }) {
  return (
    <div className="bg-white rounded-lg border border-dashed border-gray-300 p-12 text-center">
      <p className="text-gray-400 text-sm font-medium">{label}</p>
      <p className="text-gray-300 text-xs mt-1">Coming soon</p>
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

function ResumeTab({ app }: { app: ReturnType<typeof useApplication>['data'] }) {
  const { data: settings } = useSettings();
  const { data: latest, isLoading: isLoadingLatest } = useLatestGeneratedResume(app?.id ?? '');
  const generate = useGenerateResume(app?.id ?? '');
  const update = useUpdateGeneratedResume(app?.id ?? '');

  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState('');

  if (!app) return null;

  const hasResume    = settings?.hasResume ?? false;
  const hasTemplate  = settings?.hasResumeTemplate ?? false;
  const hasScoring   = app.overallScore != null;
  const canGenerate  = hasResume && hasTemplate && hasScoring;
  const isGenerating = generate.isPending;

  const missingItems: string[] = [];
  if (!hasResume)   missingItems.push('resume content (Settings)');
  if (!hasTemplate) missingItems.push('resume template (Settings)');
  if (!hasScoring)  missingItems.push('scoring data (Scoring tab)');

  function startEdit() {
    setDraft(latest?.htmlContent ?? '');
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setDraft('');
  }

  function saveEdit() {
    update.mutate(draft, { onSuccess: () => setEditing(false) });
  }

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          {latest && !isGenerating && !editing && (
            <span className="text-xs text-gray-400">
              Generated {new Date(latest.generatedAt).toLocaleString()} · {latest.modelUsed}
            </span>
          )}
        </div>
        <div className="flex items-center gap-3">
          {!canGenerate && missingItems.length > 0 && !editing && (
            <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-1.5">
              Requires: {missingItems.join(', ')}
            </p>
          )}
          {editing ? (
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
          ) : (
            <>
              {latest && !isGenerating && (
                <button
                  onClick={startEdit}
                  className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
                >
                  Edit
                </button>
              )}
              <button
                onClick={() => generate.mutate()}
                disabled={!canGenerate || isGenerating}
                className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                <SparkleIcon className="w-4 h-4" />
                {isGenerating ? 'Generating…' : latest ? 'Re-generate' : 'Generate'}
              </button>
            </>
          )}
        </div>
      </div>

      {generate.error && (
        <div className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-md px-4 py-3">
          {(generate.error as Error).message}
        </div>
      )}

      {isGenerating && (
        <div className="bg-white rounded-lg border border-gray-200 p-12 flex flex-col items-center gap-4 text-center">
          <ScoringSpinner />
          <p className="text-sm text-gray-400">Generating your tailored resume…</p>
          <p className="text-xs text-gray-300">This may take up to a minute.</p>
        </div>
      )}

      {!isGenerating && isLoadingLatest && (
        <div className="bg-white rounded-lg border border-gray-200 p-8 flex justify-center">
          <ScoringSpinner />
        </div>
      )}

      {!isGenerating && !isLoadingLatest && !latest && (
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

      {!isGenerating && latest && (
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
          <ResumeIframe
            htmlContent={latest.htmlContent}
            editing={editing}
            onDraftChange={setDraft}
          />
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
      {activeTab === 'job-ad'  && <JobAdTab app={app} />}
      {activeTab === 'scoring' && <ScoringTab app={app} />}
      {activeTab === 'resume'  && <ResumeTab app={app} />}
      {activeTab === 'letter'  && <DummyTab label="Application Letter" />}
    </div>
  );
}
