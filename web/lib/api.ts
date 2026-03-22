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
      boardId?: string;
      search?: string;
      isActive?: boolean;
      cursor?: string;
      limit?: number;
    }) => {
      const q = new URLSearchParams();
      if (params.boardId) q.set('boardId', params.boardId);
      if (params.search) q.set('search', params.search);
      if (params.isActive !== undefined) q.set('isActive', String(params.isActive));
      if (params.cursor) q.set('cursor', params.cursor);
      if (params.limit) q.set('limit', String(params.limit));
      const qs = q.toString();
      return apiFetch<PagedResponse<JobAd>>(`/api/job-ads${qs ? `?${qs}` : ''}`);
    },
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
    unreadCount: () => apiFetch<number>('/api/job-ads/unread-count'),
  },
  runs: {
    get: (id: string) => apiFetch<ScrapeRun>(`/api/runs/${id}`),
  },
  status: {
    isProcessing: () => apiFetch<{ isProcessing: boolean }>('/api/status'),
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
      const formData = new FormData();
      formData.append('file', file);
      const res = await fetch('/api/settings/resume', { method: 'PUT', body: formData });
      if (!res.ok) {
        let detail = `API error ${res.status}`;
        try { const err = await res.json(); detail = err.detail ?? detail; } catch { /* ignore */ }
        throw new Error(detail);
      }
      return res.json() as Promise<AppSettings>;
    },
    deleteResume: () =>
      apiFetch<AppSettings>('/api/settings/resume', { method: 'DELETE' }),
  },
};
