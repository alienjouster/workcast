import type { ScoringCategory, ScoringRequirement } from '@/types';

// ── Category styles ────────────────────────────────────────────────────────────

export const CATEGORY_STYLES: Record<ScoringCategory, { label: string; className: string }> = {
  match:         { label: 'Match',   className: 'bg-green-100 text-green-800' },
  partial_match: { label: 'Partial', className: 'bg-amber-100 text-amber-800' },
  gap:           { label: 'Gap',     className: 'bg-red-100   text-red-800'   },
};

// ── Score colour helper ────────────────────────────────────────────────────────

export function scoreColorClass(score: number): string {
  return score >= 70 ? 'text-green-600' : score >= 40 ? 'text-amber-500' : 'text-red-500';
}

// ── Scoring spinner ────────────────────────────────────────────────────────────

export function ScoringSpinner() {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" className="w-5 h-5 text-indigo-400 animate-spin">
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="3" strokeOpacity="0.2" />
      <path d="M12 3a9 9 0 0 1 9 9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
    </svg>
  );
}

// ── Error banner ────────────────────────────────────────────────────────────────

export function ScoringErrorBanner({ error }: { error: string }) {
  return (
    <div className="flex items-start gap-2 rounded-md bg-red-50 border border-red-200 px-4 py-3 text-xs text-red-700">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4 shrink-0 mt-0.5">
        <path fillRule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16ZM8.28 7.22a.75.75 0 0 0-1.06 1.06L8.94 10l-1.72 1.72a.75.75 0 1 0 1.06 1.06L10 11.06l1.72 1.72a.75.75 0 1 0 1.06-1.06L11.06 10l1.72-1.72a.75.75 0 0 0-1.06-1.06L10 8.94 8.28 7.22Z" clipRule="evenodd" />
      </svg>
      <span>{error}</span>
    </div>
  );
}

// ── Requirements grid ──────────────────────────────────────────────────────────

export function ScoringRequirementsGrid({ requirements }: { requirements: ScoringRequirement[] }) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white overflow-hidden text-xs">
      {(['match', 'partial_match', 'gap'] as ScoringCategory[]).map((cat) => {
        const items = requirements.filter((r) => r.category === cat);
        const style = CATEGORY_STYLES[cat];
        return (
          <div key={cat}>
            <div className={`px-4 py-1.5 font-semibold text-[11px] uppercase tracking-wide border-b border-gray-100 ${style.className}`}>
              {style.label}
            </div>
            <div className="px-4 py-2 border-b border-gray-100 last:border-b-0">
              {items.length === 0 ? (
                <p className="text-gray-300 italic">N/A</p>
              ) : (
                <div className="grid" style={{ gridTemplateColumns: 'minmax(0, 30%) 1fr' }}>
                  {items.map((req, i) => (
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
  );
}
