'use client';

import { useApplicationStats } from '@/lib/hooks/useApplications';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { EmptyState } from '@/components/ui/EmptyState';
import { Card, CardBody } from '@/components/ui/Card';
import { STATUS_BADGE } from '@/components/applications/StatusBadge';
import type { ApplicationStatus } from '@/types';

const STATUS_ORDER: ApplicationStatus[] = [
  'ToApply',
  'Applied',
  'Interviewing',
  'ClosedNoAnswer',
  'ClosedRejected',
  'ClosedHired',
];

function fmt(value: number | null, suffix = '', decimals = 1): string {
  return value == null ? '—' : `${value.toFixed(decimals)}${suffix}`;
}

function parseMonth(ym: string): string {
  const [year, month] = ym.split('-');
  const d = new Date(Number(year), Number(month) - 1, 1);
  return d.toLocaleDateString('en', { month: 'short', year: '2-digit' });
}

interface MetricCardProps {
  label: string;
  value: string;
  sub?: string;
}

function MetricCard({ label, value, sub }: MetricCardProps) {
  return (
    <Card>
      <CardBody className="py-4">
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{label}</p>
        <p className="mt-1 text-2xl font-bold text-gray-900">{value}</p>
        {sub && <p className="mt-0.5 text-xs text-gray-400">{sub}</p>}
      </CardBody>
    </Card>
  );
}

interface FunnelCardProps {
  label: string;
  count: number;
  total: number;
  color: string;
}

function FunnelCard({ label, count, total, color }: FunnelCardProps) {
  const pct = total > 0 ? Math.round((count / total) * 100) : 0;
  return (
    <Card className="flex-1 min-w-0">
      <CardBody className="py-4">
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide truncate">{label}</p>
        <p className="mt-1 text-3xl font-bold text-gray-900">{count}</p>
        <div className="mt-2 h-1.5 rounded-full bg-gray-100 overflow-hidden">
          <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
        </div>
        <p className="mt-1 text-xs text-gray-400">{pct}% of total</p>
      </CardBody>
    </Card>
  );
}

export function ApplicationStatsTab() {
  const { data, isLoading, error } = useApplicationStats();

  if (isLoading) return <LoadingSpinner />;
  if (error) {
    return (
      <div className="text-red-600 text-sm bg-red-50 rounded-md p-4">
        {(error as Error).message}
      </div>
    );
  }
  if (!data || data.totalApplications === 0) {
    return (
      <EmptyState
        title="No data yet"
        description="Start tracking applications to see your stats."
      />
    );
  }

  const statusMap = new Map(data.applicationsPerStatus.map((s) => [s.status, s.count]));
  const maxMonth = Math.max(...data.applicationsPerMonth.map((m) => m.count), 1);

  return (
    <div className="space-y-6">

      {/* Funnel */}
      <section>
        <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Funnel</h2>
        <div className="flex gap-3 flex-wrap sm:flex-nowrap">
          <FunnelCard label="All"          count={data.totalApplications} total={data.totalApplications} color="bg-gray-400" />
          <FunnelCard label="Submitted"   count={data.totalSubmitted}    total={data.totalApplications} color="bg-blue-500" />
          <FunnelCard label="Interviewed" count={data.totalInterviewed}  total={data.totalApplications} color="bg-indigo-500" />
          <FunnelCard label="Hired"       count={data.totalHired}        total={data.totalApplications} color="bg-green-500" />
        </div>
      </section>

      {/* Metrics */}
      <section>
        <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Metrics</h2>
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
          <MetricCard
            label="Interview hit rate"
            value={fmt(data.interviewHitRatio, '%')}
            sub={data.totalSubmitted > 0 ? `${data.totalInterviewed} of ${data.totalSubmitted} submitted` : undefined}
          />
          <MetricCard
            label="Avg. days to apply"
            value={fmt(data.averageDaysToApply, ' d')}
            sub="from scraped to submitted"
          />
          <MetricCard
            label="Avg. days to interview"
            value={fmt(data.averageDaysToInterview, ' d')}
            sub="from submitted to interview"
          />
          <MetricCard
            label="Avg. interview steps"
            value={fmt(data.averageInterviewSteps)}
            sub="among apps with steps"
          />
          <MetricCard
            label="Avg. score"
            value={fmt(data.averageScore, '', 1)}
            sub="/ 100, all scored apps"
          />
          <MetricCard
            label="Avg. score (interviewed)"
            value={fmt(data.averageScoreInterviewed, '', 1)}
            sub="/ 100, interviewed apps"
          />
        </div>
      </section>

      {/* Bottom row: distribution + trend */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">

        {/* Status distribution */}
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">By status</h2>
          <Card>
            <CardBody className="py-3 space-y-2">
              {STATUS_ORDER.map((status) => {
                const count = statusMap.get(status) ?? 0;
                const pct = data.totalApplications > 0
                  ? Math.round((count / data.totalApplications) * 100)
                  : 0;
                const badge = STATUS_BADGE[status];
                return (
                  <div key={status} className="flex items-center gap-3 text-sm">
                    <span className="w-28 shrink-0 text-xs font-medium text-gray-600 truncate">
                      {badge.label}
                    </span>
                    <div className="flex-1 h-2 rounded-full bg-gray-100 overflow-hidden">
                      <div
                        className={`h-full rounded-full ${badge.cls.replace('text-', 'bg-').replace('100', '400')}`}
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                    <span className="w-6 text-right text-xs text-gray-500 tabular-nums">{count}</span>
                  </div>
                );
              })}
            </CardBody>
          </Card>
        </section>

        {/* Monthly trend */}
        <section>
          <h2 className="text-sm font-semibold text-gray-500 uppercase tracking-wide mb-3">Last 6 months</h2>
          <Card>
            <CardBody className="py-4">
              {maxMonth === 0 ? (
                <p className="text-sm text-gray-400 text-center py-4">No activity in the last 6 months.</p>
              ) : (
                <div className="flex items-end justify-around gap-2 h-24">
                  {data.applicationsPerMonth.map((m) => {
                    const heightPct = Math.round((m.count / maxMonth) * 100);
                    return (
                      <div key={m.month} className="flex flex-col items-center gap-1 flex-1">
                        <span className="text-xs font-medium text-gray-500 tabular-nums">
                          {m.count > 0 ? m.count : ''}
                        </span>
                        <div className="w-full flex items-end" style={{ height: '64px' }}>
                          <div
                            className="w-full rounded-t bg-indigo-400"
                            style={{ height: `${Math.max(heightPct, m.count > 0 ? 4 : 0)}%` }}
                          />
                        </div>
                        <span className="text-xs text-gray-400 whitespace-nowrap">
                          {parseMonth(m.month)}
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}
            </CardBody>
          </Card>
        </section>

      </div>
    </div>
  );
}
