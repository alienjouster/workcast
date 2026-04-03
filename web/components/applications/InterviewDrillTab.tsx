'use client';

import { useState } from 'react';
import type { Application, InterviewQuestionCategory } from '@/types';
import { useInterviewDrillPlan, useGenerateInterviewDrill, useCancelInterviewDrill } from '@/lib/hooks/useApplications';
import { ScoringSpinner, ScoringErrorBanner } from '@/components/scoring/ScoringShared';

// ── Category config ────────────────────────────────────────────────────────────

const CATEGORY_CONFIG: Record<InterviewQuestionCategory, { label: string; badgeClass: string }> = {
  warm_up:     { label: 'Warm Up',     badgeClass: 'bg-blue-100 text-blue-700' },
  easy:        { label: 'Easy',        badgeClass: 'bg-green-100 text-green-700' },
  medium:      { label: 'Medium',      badgeClass: 'bg-amber-100 text-amber-700' },
  challenging: { label: 'Challenging', badgeClass: 'bg-red-100 text-red-700' },
};

// ── SparkleIcon (local — avoids prop-drilling from parent) ────────────────────

function SparkleIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className={className}>
      <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
    </svg>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

export function InterviewDrillTab({ app }: { app: Application }) {
  const { data: plan, isLoading: isPlanLoading } = useInterviewDrillPlan(app.id);
  const generate = useGenerateInterviewDrill(app.id);
  const cancel = useCancelInterviewDrill(app.id);

  // Drill mode state
  const [drillActive, setDrillActive] = useState(false);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [questionsRevealed, setQuestionsRevealed] = useState(false);

  const isGenerating = app.isInterviewDrillPending || generate.isPending;
  const hasResume = true; // validation happens server-side; the UI simply shows the button
  const hasScoring = app.overallScore != null;

  function startDrill() {
    setCurrentIndex(0);
    setDrillActive(true);
  }

  function exitDrill() {
    setDrillActive(false);
  }

  // ── Pending ──────────────────────────────────────────────────────────────────
  if (isGenerating) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 flex flex-col items-center gap-4">
        <ScoringSpinner />
        <p className="text-sm text-gray-400">Generating interview questions…</p>
        <p className="text-xs text-gray-300">This may take up to a minute.</p>
        {app.isInterviewDrillPending && !generate.isPending && (
          <button
            onClick={() => cancel.mutate()}
            disabled={cancel.isPending}
            className="text-xs text-gray-400 hover:text-gray-600 underline underline-offset-2 disabled:opacity-50"
          >
            {cancel.isPending ? 'Cancelling…' : 'Cancel'}
          </button>
        )}
      </div>
    );
  }

  // ── Error banner ─────────────────────────────────────────────────────────────
  const errorBanner = app.lastInterviewDrillError
    ? <ScoringErrorBanner error={app.lastInterviewDrillError} />
    : null;

  // ── No plan yet ──────────────────────────────────────────────────────────────
  if (!plan && !isPlanLoading) {
    const missingItems: string[] = [];
    if (!hasScoring) missingItems.push('scoring data (Scoring tab)');

    return (
      <div className="space-y-3">
        {errorBanner}
        <div className="bg-white rounded-lg border border-gray-200 p-8 flex flex-col items-center gap-4 text-center">
          <div className="w-12 h-12 rounded-full bg-indigo-50 flex items-center justify-center">
            <SparkleIcon className="w-6 h-6 text-indigo-400" />
          </div>
          <div>
            <p className="text-sm font-medium text-gray-700">No interview drill plan yet</p>
            <p className="text-xs text-gray-400 mt-1 max-w-sm leading-relaxed">
              Generate 15–20 tailored questions to help you prepare for this interview — warm-up, easy, medium, and challenging questions based on your scoring analysis.
            </p>
          </div>
          {missingItems.length > 0 && (
            <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-2">
              Requires: {missingItems.join(', ')}
            </p>
          )}
          <button
            onClick={() => generate.mutate()}
            disabled={!hasScoring || generate.isPending}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <SparkleIcon className="w-4 h-4" />
            Generate Interview Drill
          </button>
        </div>
      </div>
    );
  }

  if (!plan) return null;

  const questions = [...plan.questions].sort((a, b) => a.orderIndex - b.orderIndex);
  const total = questions.length;

  // ── Drill active ─────────────────────────────────────────────────────────────
  if (drillActive) {
    const q = questions[currentIndex];
    const category = q.category as InterviewQuestionCategory;
    const cfg = CATEGORY_CONFIG[category] ?? CATEGORY_CONFIG.warm_up;
    const progressPct = Math.round(((currentIndex + 1) / total) * 100);

    return (
      <div className="space-y-6">
        {/* Progress bar */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <span className="text-xs text-gray-400">Question {currentIndex + 1} of {total}</span>
            <button
              onClick={exitDrill}
              className="text-xs text-gray-400 hover:text-gray-600 underline underline-offset-2"
            >
              Exit Drill
            </button>
          </div>
          <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-2 bg-indigo-500 rounded-full transition-all duration-300"
              style={{ width: `${progressPct}%` }}
            />
          </div>
        </div>

        {/* Question card */}
        <div className="bg-white rounded-lg border border-gray-200 p-8">
          <div className="flex items-center gap-2 mb-6">
            <span className={`inline-block text-xs font-semibold px-2.5 py-1 rounded-full ${cfg.badgeClass}`}>
              {cfg.label}
            </span>
            {q.requirementName && (
              <span className="text-xs text-gray-400 truncate max-w-xs" title={q.requirementName}>
                {q.requirementName}
              </span>
            )}
          </div>
          <p className="text-lg font-medium text-gray-900 leading-relaxed">{q.text}</p>
        </div>

        {/* Navigation */}
        <div className="flex items-center justify-between">
          <button
            onClick={() => setCurrentIndex((i) => Math.max(0, i - 1))}
            disabled={currentIndex === 0}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            aria-label="Previous question"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
              <path fillRule="evenodd" d="M11.78 5.22a.75.75 0 0 1 0 1.06L8.06 10l3.72 3.72a.75.75 0 1 1-1.06 1.06l-4.25-4.25a.75.75 0 0 1 0-1.06l4.25-4.25a.75.75 0 0 1 1.06 0Z" clipRule="evenodd" />
            </svg>
            Previous
          </button>

          {currentIndex < total - 1 ? (
            <button
              onClick={() => setCurrentIndex((i) => Math.min(total - 1, i + 1))}
              className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 transition-colors"
              aria-label="Next question"
            >
              Next
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                <path fillRule="evenodd" d="M8.22 5.22a.75.75 0 0 1 1.06 0l4.25 4.25a.75.75 0 0 1 0 1.06l-4.25 4.25a.75.75 0 1 1-1.06-1.06L11.94 10 8.22 6.28a.75.75 0 0 1 0-1.06Z" clipRule="evenodd" />
              </svg>
            </button>
          ) : (
            <button
              onClick={exitDrill}
              className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-green-600 text-white text-sm font-medium hover:bg-green-700 transition-colors"
            >
              Finish
            </button>
          )}
        </div>
      </div>
    );
  }

  // ── Plan overview ─────────────────────────────────────────────────────────────
  const categoryCounts = (Object.keys(CATEGORY_CONFIG) as InterviewQuestionCategory[]).map((cat) => ({
    cat,
    count: questions.filter((q) => q.category === cat).length,
    cfg: CATEGORY_CONFIG[cat],
  })).filter((c) => c.count > 0);

  return (
    <div className="space-y-4">
      {errorBanner}

      {/* Plan summary card */}
      <div className="bg-white rounded-lg border border-gray-200 px-6 py-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <p className="text-sm font-semibold text-gray-900">{total} questions ready</p>
            <p className="text-xs text-gray-400 mt-0.5">
              Generated {new Date(plan.generatedAt).toLocaleDateString()} · {plan.modelUsed}
            </p>
          </div>
          <button
            onClick={() => { generate.mutate(); setDrillActive(false); }}
            disabled={!hasScoring || generate.isPending}
            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md border border-gray-200 bg-white text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <SparkleIcon className="w-3.5 h-3.5" />
            Re-generate
          </button>
        </div>

        {/* Category breakdown bar */}
        <div className="flex rounded-full overflow-hidden h-3 mb-3" aria-label="Question categories">
          {categoryCounts.map(({ cat, count, cfg }) => (
            <div
              key={cat}
              className={cfg.badgeClass.replace('text-', 'bg-').split(' ')[0]}
              style={{ width: `${Math.round((count / total) * 100)}%` }}
              title={`${cfg.label}: ${count}`}
            />
          ))}
        </div>

        {/* Legend */}
        <div className="flex flex-wrap gap-3">
          {categoryCounts.map(({ cat, count, cfg }) => (
            <span key={cat} className="inline-flex items-center gap-1.5 text-xs text-gray-500">
              <span className={`inline-block w-2.5 h-2.5 rounded-sm ${cfg.badgeClass.split(' ')[0]}`} />
              {cfg.label} ({count})
            </span>
          ))}
        </div>
      </div>

      {/* Start button */}
      <div className="flex justify-center">
        <button
          onClick={startDrill}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-md bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 transition-colors shadow-sm"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5">
            <path d="M6.3 2.84A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.27l9.344-5.891a1.5 1.5 0 0 0 0-2.538L6.3 2.841Z" />
          </svg>
          Start Interview Drill
        </button>
      </div>

      {/* Question list preview */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="px-4 py-2.5 border-b border-gray-100 flex items-center justify-between">
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Questions</p>
          <button
            onClick={() => setQuestionsRevealed((v) => !v)}
            className="text-xs text-indigo-500 hover:text-indigo-700 font-medium"
          >
            {questionsRevealed ? 'Hide questions' : 'Reveal questions'}
          </button>
        </div>
        {questionsRevealed && (
          <ol className="divide-y divide-gray-100">
            {questions.map((q) => {
              const category = q.category as InterviewQuestionCategory;
              const cfg = CATEGORY_CONFIG[category] ?? CATEGORY_CONFIG.warm_up;
              return (
                <li key={q.orderIndex} className="flex items-start gap-3 px-4 py-3">
                  <span className="shrink-0 text-xs text-gray-300 w-5 text-right mt-0.5">{q.orderIndex}.</span>
                  <span className={`shrink-0 text-[10px] font-semibold px-1.5 py-0.5 rounded-full mt-0.5 ${cfg.badgeClass}`}>
                    {cfg.label}
                  </span>
                  <span className="text-sm text-gray-700 leading-snug">{q.text}</span>
                </li>
              );
            })}
          </ol>
        )}
      </div>
    </div>
  );
}
