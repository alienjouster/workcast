// ── Stale times (ms) ──────────────────────────────────────────────────────────

export const STALE_TIMES = {
  SHORT:  10_000,
  MEDIUM: 30_000,
  LONG:   60_000,
} as const;

// ── Scoring thresholds ────────────────────────────────────────────────────────

export const SCORE_GOOD = 70;
export const SCORE_FAIR = 40;

// ── UI constants ───────────────────────────────────────────────────────────────

export const BADGE_OVERFLOW = 999;
export const SCORE_FILTER_PRESETS = [70, 80, 90] as const;
