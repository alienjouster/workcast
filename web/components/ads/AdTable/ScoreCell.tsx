'use client';

import type { JobAd } from '@/types';
import { useRunScoring } from '@/lib/hooks/useAdScoring';
import { useSettings } from '@/lib/hooks/useSettings';
import { ScoringSpinner, scoreColorClass } from '@/components/scoring/ScoringShared';
import { Tooltip } from '@/components/ui/Tooltip';

export function ScoreCell({ ad }: { ad: JobAd }) {
  const runScoring = useRunScoring();
  const { data: settings } = useSettings();
  const hasResume = settings?.hasResume ?? false;

  if (ad.isScoringPending || runScoring.isPending) {
    return <ScoringSpinner />;
  }

  if (ad.overallScore != null) {
    return (
      <span className={`text-xs font-medium tabular-nums ${scoreColorClass(ad.overallScore)}`}>
        {Math.round(ad.overallScore)}%
      </span>
    );
  }

  return (
    <Tooltip content="✨ Run scoring analysis" position="top" wrapperAs="span">
      <button
        disabled={!hasResume}
        onClick={(e) => { e.stopPropagation(); runScoring.mutate(ad.id); }}
        className="text-gray-300 hover:text-indigo-400 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
          <path d="M3.75 13.5a8.25 8.25 0 1 1 16.5 0" />
          <path d="M12 13.5 9.5 8.5" />
          <circle cx="12" cy="13.5" r="1" fill="currentColor" stroke="none" />
        </svg>
      </button>
    </Tooltip>
  );
}
