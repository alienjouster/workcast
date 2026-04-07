'use client';

import { useAdScoring, useRunScoring } from '@/lib/hooks/useAdScoring';
import { useSettings } from '@/lib/hooks/useSettings';
import { Button } from '@/components/ui/Button';
import { ScoringErrorBanner, ScoringRequirementsGrid } from '@/components/scoring/ScoringShared';

function timeAgo(date: string): string {
  const seconds = Math.floor((Date.now() - new Date(date).getTime()) / 1000);
  const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
  if (seconds < 60)    return rtf.format(-seconds, 'second');
  if (seconds < 3600)  return rtf.format(-Math.floor(seconds / 60), 'minute');
  if (seconds < 86400) return rtf.format(-Math.floor(seconds / 3600), 'hour');
  return rtf.format(-Math.floor(seconds / 86400), 'day');
}

export function AdScoringPanel({
  adId,
  isScoringPending,
  lastScoringError,
}: {
  adId: string;
  isScoringPending: boolean;
  lastScoringError: string | null;
}) {
  const { data: scoring, isLoading: _isLoading, isFetching } = useAdScoring(adId, isScoringPending);
  const { data: settings } = useSettings();
  const runScoring = useRunScoring();

  const hasResume = settings?.hasResume ?? false;
  const isRunning = runScoring.isPending || isFetching || isScoringPending;

  if (!scoring && !isRunning && !lastScoringError) return null;

  return (
    <div className="mt-4 border-t border-gray-200 pt-3">
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
          Ad matching score
          {scoring?.scoredAt && (
            <span className="ml-2 normal-case font-normal text-gray-400">{timeAgo(scoring.scoredAt)}</span>
          )}
        </span>
        {!scoring && !hasResume && (
          <span className="text-xs text-gray-400 italic">
            Upload a resume from the{' '}
            <a href="/settings" className="text-indigo-500 hover:underline">Settings page</a>
          </span>
        )}
      </div>

      {isRunning && !scoring && (
        <p className="text-xs text-gray-400 italic">Analysing…</p>
      )}

      {!isRunning && !scoring && lastScoringError && (
        <ScoringErrorBanner error={lastScoringError} />
      )}

      {scoring && (
        <div className="space-y-3">
          {/* Score + summary box */}
          <div className="flex items-stretch gap-4 rounded-lg border border-gray-200 bg-white px-4 py-3">
            <div className="shrink-0 flex flex-col items-center justify-between pt-0.5">
              <span className="text-3xl font-bold text-gray-800 leading-none">
                {Math.round(scoring.overallScore)}<span className="text-base font-normal text-gray-400">/100</span>
              </span>
              <Button
                variant="secondary"
                size="sm"
                onClick={() => runScoring.mutate(adId)}
                loading={isRunning}
                disabled={isRunning || !hasResume}
              >
                <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5 mr-1">
                  <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
                </svg>
                Re-score
              </Button>
            </div>
            <div className="flex-1 space-y-2">
              {scoring.recommendation && (
                <div>
                  <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-0.5">Recommendation</p>
                  <p className="text-sm text-gray-800 leading-relaxed">{scoring.recommendation}</p>
                </div>
              )}
              {scoring.recommendation && scoring.summary && (
                <hr className="border-gray-100" />
              )}
              {scoring.summary && (
                <p className="text-xs text-gray-500 leading-relaxed">{scoring.summary}</p>
              )}
            </div>
          </div>

          {/* Requirements grouped by category */}
          <ScoringRequirementsGrid requirements={scoring.requirements} />
        </div>
      )}
    </div>
  );
}
