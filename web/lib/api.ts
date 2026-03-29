import type {
  JobBoard,
  JobAd,
  AdScoring,
  ScrapeRun,
  PagedResponse,
  CreateJobBoardRequest,
  UpdateJobBoardRequest,
  UpdateScraperConfigRequest,
  AppSettings,
  UpdateSettingsRequest,
  Application,
  ApplicationStatus,
  GeneratedResume,
  GeneratedLetter,
  ResumeOptimizationLevel,
} from '@/types';

// Use relative URLs so browser requests go to the Next.js proxy at /api/[...path]
// which forwards to the backend via API_INTERNAL_URL (no CORS issues).
const API_BASE = '';

async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  if (!res.ok) {
    let detail = `API error ${res.status}`;
    try {
      const err = await res.json();
      detail = err.detail ?? detail;
    } catch {
      // ignore parse error
    }
    throw new Error(detail);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text) return undefined as T;
  return JSON.parse(text) as T;
}

// ── Shared query-building helpers ────────────────────────────────────────────

type CommonFilterParams = {
  titles?: string[];
  excludeTitles?: string[];
  locations?: string[];
  excludeLocations?: string[];
  companies?: string[];
  excludeCompanies?: string[];
  minScore?: number;
  trashed?: boolean;
  cursor?: string;
  limit?: number;
};

function buildCommonFilterParams(q: URLSearchParams, params: CommonFilterParams): void {
  params.titles?.forEach(t => q.append('titles', t));
  params.excludeTitles?.forEach(t => q.append('excludeTitles', t));
  params.locations?.forEach(l => q.append('locations', l));
  params.excludeLocations?.forEach(l => q.append('excludeLocations', l));
  params.companies?.forEach(c => q.append('companies', c));
  params.excludeCompanies?.forEach(c => q.append('excludeCompanies', c));
  if (params.minScore !== undefined) q.set('minScore', String(params.minScore));
  if (params.trashed) q.set('trashed', 'true');
  if (params.cursor) q.set('cursor', params.cursor);
  if (params.limit) q.set('limit', String(params.limit));
}

function makeDistinctEndpoint(path: string) {
  return (q?: string) =>
    apiFetch<string[]>(`${path}${q ? `?q=${encodeURIComponent(q)}` : ''}`);
}

export const api = {
  boards: {
    list: () => apiFetch<JobBoard[]>('/api/job-boards'),
    get: (id: string) => apiFetch<JobBoard>(`/api/job-boards/${id}`),
    create: (data: CreateJobBoardRequest) =>
      apiFetch<JobBoard>('/api/job-boards', {
        method: 'POST',
        body: JSON.stringify(data),
      }),
    update: (id: string, data: UpdateJobBoardRequest) =>
      apiFetch<JobBoard>(`/api/job-boards/${id}`, {
        method: 'PATCH',
        body: JSON.stringify(data),
      }),
    delete: (id: string) =>
      apiFetch<void>(`/api/job-boards/${id}`, { method: 'DELETE' }),
    refresh: (id: string) =>
      apiFetch<void>(`/api/job-boards/${id}/refresh`, { method: 'POST' }),
    reanalyze: (id: string) =>
      apiFetch<void>(`/api/job-boards/${id}/reanalyze`, { method: 'POST' }),
    updateScraperConfig: (id: string, data: UpdateScraperConfigRequest) =>
      apiFetch<JobBoard>(`/api/job-boards/${id}/scraper-config`, {
        method: 'PUT',
        body: JSON.stringify(data),
      }),
    listRuns: (id: string, limit?: number) =>
      apiFetch<ScrapeRun[]>(`/api/job-boards/${id}/runs${limit ? `?limit=${limit}` : ''}`),
  },
  ads: {
    list: (params: {
      boardIds?: string[];
      excludeBoardIds?: string[];
      isActive?: boolean;
      isRead?: boolean;
      isPinned?: boolean;
    } & CommonFilterParams) => {
      const q = new URLSearchParams();
      params.boardIds?.forEach(id => q.append('boardIds', id));
      params.excludeBoardIds?.forEach(id => q.append('excludeBoardIds', id));
      if (params.isActive !== undefined) q.set('isActive', String(params.isActive));
      if (params.isRead !== undefined) q.set('isRead', String(params.isRead));
      if (params.isPinned !== undefined) q.set('isPinned', String(params.isPinned));
      buildCommonFilterParams(q, params);
      const qs = q.toString();
      return apiFetch<PagedResponse<JobAd>>(`/api/job-ads${qs ? `?${qs}` : ''}`);
    },
    distinctTitles: makeDistinctEndpoint('/api/job-ads/distinct-titles'),
    distinctLocations: makeDistinctEndpoint('/api/job-ads/distinct-locations'),
    distinctCompanies: makeDistinctEndpoint('/api/job-ads/distinct-companies'),
    get: (id: string) => apiFetch<JobAd>(`/api/job-ads/${id}`),
    delete: (id: string) =>
      apiFetch<void>(`/api/job-ads/${id}`, { method: 'DELETE' }),
    pin: (id: string) =>
      apiFetch<JobAd>(`/api/job-ads/${id}/pin`, { method: 'PATCH' }),
    unpin: (id: string) =>
      apiFetch<JobAd>(`/api/job-ads/${id}/unpin`, { method: 'PATCH' }),
    markRead: (id: string) =>
      apiFetch<JobAd>(`/api/job-ads/${id}/read`, { method: 'PATCH' }),
    markUnread: (id: string) =>
      apiFetch<JobAd>(`/api/job-ads/${id}/unread`, { method: 'PATCH' }),
    markAllRead: (boardId?: string) =>
      apiFetch<void>(`/api/job-ads/mark-all-read${boardId ? `?boardId=${boardId}` : ''}`, { method: 'POST' }),
    trash: (id: string) =>
      apiFetch<JobAd>(`/api/job-ads/${id}/trash`, { method: 'PATCH' }),
    restore: (id: string) =>
      apiFetch<JobAd>(`/api/job-ads/${id}/restore`, { method: 'PATCH' }),
    bulkPin: (ids: string[]) =>
      apiFetch<void>('/api/job-ads/bulk/pin', { method: 'POST', body: JSON.stringify({ ids }) }),
    bulkUnpin: (ids: string[]) =>
      apiFetch<void>('/api/job-ads/bulk/unpin', { method: 'POST', body: JSON.stringify({ ids }) }),
    bulkMarkRead: (ids: string[]) =>
      apiFetch<void>('/api/job-ads/bulk/read', { method: 'POST', body: JSON.stringify({ ids }) }),
    bulkMarkUnread: (ids: string[]) =>
      apiFetch<void>('/api/job-ads/bulk/unread', { method: 'POST', body: JSON.stringify({ ids }) }),
    bulkTrash: (ids: string[]) =>
      apiFetch<void>('/api/job-ads/bulk/trash', { method: 'POST', body: JSON.stringify({ ids }) }),
    setNote: (id: string, note: string | null) =>
      apiFetch<import('@/types').JobAd>(`/api/job-ads/${id}/note`, {
        method: 'PATCH',
        body: JSON.stringify({ note }),
      }),
  },
  runs: {
    list: (limit?: number) =>
      apiFetch<ScrapeRun[]>(`/api/runs${limit ? `?limit=${limit}` : ''}`),
    get: (id: string) => apiFetch<ScrapeRun>(`/api/runs/${id}`),
  },
  status: {
    get: () => apiFetch<{ isProcessing: boolean; unreadCount: number }>('/api/status'),
  },
  scoring: {
    get: (adId: string) => apiFetch<AdScoring>(`/api/job-ads/${adId}/scoring`),
    run: (adId: string) =>
      apiFetch<void>(`/api/job-ads/${adId}/scoring`, { method: 'POST' }),
  },
  settings: {
    get: () => apiFetch<AppSettings>('/api/settings'),
    update: (data: UpdateSettingsRequest) =>
      apiFetch<AppSettings>('/api/settings', {
        method: 'PATCH',
        body: JSON.stringify(data),
      }),
    uploadResume: async (file: File): Promise<AppSettings> => {
      const buffer = await file.arrayBuffer();
      const bytes = new Uint8Array(buffer);
      let binary = '';
      for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
      const contentBase64 = btoa(binary);
      return apiFetch<AppSettings>('/api/settings/resume', {
        method: 'PUT',
        body: JSON.stringify({ fileName: file.name, contentBase64, contentType: file.type }),
      });
    },
    deleteResume: () =>
      apiFetch<AppSettings>('/api/settings/resume', { method: 'DELETE' }),
    uploadResumeTemplate: async (file: File): Promise<AppSettings> => {
      const buffer = await file.arrayBuffer();
      const bytes = new Uint8Array(buffer);
      let binary = '';
      for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
      const contentBase64 = btoa(binary);
      return apiFetch<AppSettings>('/api/settings/resume-template', {
        method: 'PUT',
        body: JSON.stringify({ fileName: file.name, contentBase64 }),
      });
    },
    deleteResumeTemplate: () =>
      apiFetch<AppSettings>('/api/settings/resume-template', { method: 'DELETE' }),
  },
  applications: {
    create: (jobAdId: string) =>
      apiFetch<Application>('/api/applications', {
        method: 'POST',
        body: JSON.stringify({ jobAdId }),
      }),
    list: (params: CommonFilterParams) => {
      const q = new URLSearchParams();
      buildCommonFilterParams(q, params);
      const qs = q.toString();
      return apiFetch<PagedResponse<Application>>(`/api/applications${qs ? `?${qs}` : ''}`);
    },
    get: (id: string) => apiFetch<Application>(`/api/applications/${id}`),
    trash: (id: string) =>
      apiFetch<Application>(`/api/applications/${id}/trash`, { method: 'PATCH' }),
    restore: (id: string) =>
      apiFetch<Application>(`/api/applications/${id}/restore`, { method: 'PATCH' }),
    delete: (id: string) =>
      apiFetch<void>(`/api/applications/${id}`, { method: 'DELETE' }),
    updateJobAdContent: (id: string, content: string | null) =>
      apiFetch<Application>(`/api/applications/${id}/job-ad-content`, {
        method: 'PATCH',
        body: JSON.stringify({ content }),
      }),
    score: (id: string) =>
      apiFetch<void>(`/api/applications/${id}/scoring`, { method: 'POST' }),
    cancelScoring: (id: string) =>
      apiFetch<void>(`/api/applications/${id}/scoring`, { method: 'DELETE' }),
    generateResume: (id: string, optimizationLevel: ResumeOptimizationLevel = 'None') =>
      apiFetch<void>(`/api/applications/${id}/resume/generate`, {
        method: 'POST',
        body: JSON.stringify({ optimizationLevel }),
      }),
    getLatestResume: (id: string) =>
      apiFetch<GeneratedResume>(`/api/applications/${id}/resume/latest`),
    updateLatestResume: (id: string, htmlContent: string) =>
      apiFetch<GeneratedResume>(`/api/applications/${id}/resume/latest`, {
        method: 'PATCH',
        body: JSON.stringify({ htmlContent }),
      }),
    listResumeVersions: (id: string) =>
      apiFetch<GeneratedResume[]>(`/api/applications/${id}/resume/versions`),
    deleteResumeVersion: (id: string, versionId: string) =>
      apiFetch<void>(`/api/applications/${id}/resume/versions/${versionId}`, { method: 'DELETE' }),
    generateLetter: (id: string) =>
      apiFetch<void>(`/api/applications/${id}/letter/generate`, { method: 'POST' }),
    getLatestLetter: (id: string) =>
      apiFetch<GeneratedLetter>(`/api/applications/${id}/letter/latest`),
    updateLatestLetter: (id: string, htmlContent: string) =>
      apiFetch<GeneratedLetter>(`/api/applications/${id}/letter/latest`, {
        method: 'PATCH',
        body: JSON.stringify({ htmlContent }),
      }),
    distinctTitles: makeDistinctEndpoint('/api/applications/distinct-titles'),
    distinctLocations: makeDistinctEndpoint('/api/applications/distinct-locations'),
    distinctCompanies: makeDistinctEndpoint('/api/applications/distinct-companies'),
    updatePostedAt: (id: string, postedAt: string | null) =>
      apiFetch<Application>(`/api/applications/${id}/posted-at`, {
        method: 'PATCH',
        body: JSON.stringify({ postedAt }),
      }),
    updateScrapedAt: (id: string, scrapedAt: string) =>
      apiFetch<Application>(`/api/applications/${id}/scraped-at`, {
        method: 'PATCH',
        body: JSON.stringify({ scrapedAt }),
      }),
    updateStatus: (id: string, status: ApplicationStatus, achievedAt?: string) =>
      apiFetch<Application>(`/api/applications/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({ status, achievedAt: achievedAt ?? null }),
      }),
    updateStatusDate: (id: string, status: ApplicationStatus, achievedAt: string) =>
      apiFetch<Application>(`/api/applications/${id}/status/date`, {
        method: 'PATCH',
        body: JSON.stringify({ status, achievedAt }),
      }),
  },
};
