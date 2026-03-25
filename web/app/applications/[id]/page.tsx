'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { useApplication } from '@/lib/hooks/useApplications';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import type { ScoringCategory, ScoringRequirement } from '@/types';

// ── Tabs ──────────────────────────────────────────────────────────────────────

type Tab = 'job-ad' | 'scoring' | 'resume' | 'letter' | 'stages';

const TABS: { id: Tab; label: string }[] = [
  { id: 'job-ad',  label: 'Job Ad' },
  { id: 'scoring', label: 'Scoring' },
  { id: 'resume',  label: 'Custom Resume' },
  { id: 'letter',  label: 'Application Letter' },
  { id: 'stages',  label: 'Stages' },
];

// ── Scoring helpers ───────────────────────────────────────────────────────────

const CATEGORY_STYLES: Record<ScoringCategory, { label: string; className: string }> = {
  match:         { label: 'Match',   className: 'bg-green-100 text-green-800' },
  partial_match: { label: 'Partial', className: 'bg-amber-100 text-amber-800' },
  gap:           { label: 'Gap',     className: 'bg-red-100   text-red-800'   },
};

// ── Tab content ───────────────────────────────────────────────────────────────

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

function ScoringTab({ app }: { app: ReturnType<typeof useApplication>['data'] }) {
  if (!app) return null;

  if (app.overallScore == null) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 text-center">
        <p className="text-gray-400 text-sm">No scoring data was available when this application was created.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Score + summary */}
      <div className="bg-white rounded-lg border border-gray-200 px-6 py-5 flex items-start gap-6">
        <div className="shrink-0 text-center">
          <span className={`text-4xl font-bold ${
            app.overallScore >= 70 ? 'text-green-600' :
            app.overallScore >= 40 ? 'text-amber-500' : 'text-red-500'
          }`}>
            {Math.round(app.overallScore)}
          </span>
          <span className="text-lg font-normal text-gray-400">/100</span>
          {app.scoredAt && (
            <p className="text-xs text-gray-400 mt-1">
              Scored {new Date(app.scoredAt).toLocaleDateString()}
            </p>
          )}
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
        <div className="bg-white rounded-lg border border-gray-200 overflow-hidden text-sm">
          {(['match', 'partial_match', 'gap'] as ScoringCategory[]).map((cat) => {
            const items = app.requirements.filter((r: ScoringRequirement) => r.category === cat);
            const style = CATEGORY_STYLES[cat];
            return (
              <div key={cat}>
                <div className={`px-4 py-1.5 font-semibold text-[11px] uppercase tracking-wide border-b border-gray-100 ${style.className}`}>
                  {style.label}
                </div>
                <div className="px-4 py-2 border-b border-gray-100 last:border-b-0">
                  {items.length === 0 ? (
                    <p className="text-gray-300 italic text-xs">N/A</p>
                  ) : (
                    <div className="grid text-xs" style={{ gridTemplateColumns: 'minmax(0, 30%) 1fr' }}>
                      {items.map((req: ScoringRequirement, i: number) => (
                        <div key={i} className="contents">
                          <div className={`py-1 pr-3 text-gray-700 break-words ${i > 0 ? 'border-t border-gray-100' : ''}`}>
                            {req.name}
                            {req.isOptional && <span className="ml-1 text-[10px] text-gray-400">(opt)</span>}
                          </div>
                          <div className={`py-1 text-gray-400 ${i > 0 ? 'border-t border-gray-100' : ''}`}>
                            {req.notes ?? ''}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
        </div>
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

// ── Page ──────────────────────────────────────────────────────────────────────

export default function ApplicationDetailPage() {
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
        <h1 className="text-2xl font-bold text-gray-900 mt-2">
          {app.title ?? '(no title)'}
        </h1>
        {app.company && (
          <p className="text-sm text-gray-500 mt-0.5">{app.company}</p>
        )}
        {app.isTrashed && (
          <span className="inline-block mt-2 text-xs bg-red-100 text-red-700 px-2 py-0.5 rounded-full">
            In trash
          </span>
        )}
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
      {activeTab === 'resume'  && <DummyTab label="Custom Resume" />}
      {activeTab === 'letter'  && <DummyTab label="Application Letter" />}
      {activeTab === 'stages'  && <DummyTab label="Stages" />}
    </div>
  );
}
