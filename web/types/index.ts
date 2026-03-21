export type BoardStatus = 'pending' | 'active' | 'paused' | 'error';
export type RunStatus = 'running' | 'completed' | 'failed' | 'partial';
export type TriggeredBy = 'scheduler' | 'manual';

export interface FieldSelectorMap {
  detailUrl: string | null;
  title: string | null;
  company: string | null;
  location: string | null;
  salaryRaw: string | null;
  postedAt: string | null;
  descriptionSnippet: string | null;
  externalId: string | null;
}

export interface ScraperConfig {
  paginationType: 'url_param' | 'next_button' | 'infinite_scroll' | 'none';
  jobCardSelector: string;
  fieldSelectors: FieldSelectorMap;
  nextPageSelector: string | null;
  urlParamName: string | null;
  urlParamIsOffset: boolean;
  maxPages: number | null;
  requiresJs: boolean;
  suggestedDelayMs: number;
  confidenceScore: number;
  analyzerNotes: string | null;
  generatedAt: string;
}

export interface JobBoard {
  id: string;
  name: string | null;
  url: string;
  status: BoardStatus;
  scheduleCron: string;
  lastScrapedAt: string | null;
  createdAt: string;
  updatedAt: string;
  adCount: number;
  scraperConfig: ScraperConfig | null;
}

export interface JobAd {
  id: string;
  jobBoardId: string;
  scrapeRunId: string | null;
  externalId: string | null;
  url: string;
  title: string | null;
  company: string | null;
  location: string | null;
  salaryRaw: string | null;
  description: string | null;
  postedAt: string | null;
  scrapedAt: string;
  isActive: boolean;
  isPinned: boolean;
}

export interface ScrapeRunError {
  page: string;
  message: string;
  timestamp: string;
}

export interface ScrapeRun {
  id: string;
  jobBoardId: string;
  triggeredBy: TriggeredBy;
  startedAt: string;
  finishedAt: string | null;
  status: RunStatus;
  pagesScraped: number;
  adsFound: number;
  adsNew: number;
  errors: ScrapeRunError[];
}

export interface PagedResponse<T> {
  items: T[];
  nextCursor: string | null;
  count: number;
}

export interface CreateJobBoardRequest {
  url: string;
  name?: string;
  scheduleCron?: string;
}

export interface UpdateJobBoardRequest {
  url?: string;
  name?: string;
  scheduleCron?: string;
  status?: 'active' | 'paused';
}

export interface UpdateScraperConfigRequest {
  paginationType: 'url_param' | 'next_button' | 'infinite_scroll' | 'none';
  jobCardSelector: string;
  fieldSelectors: {
    detailUrl: string | null;
    title: string | null;
    company: string | null;
    location: string | null;
    salaryRaw: string | null;
    postedAt: string | null;
    descriptionSnippet: string | null;
    externalId: string | null;
  };
  nextPageSelector: string | null;
  urlParamName: string | null;
  urlParamIsOffset: boolean;
  maxPages: number | null;
  requiresJs: boolean;
  suggestedDelayMs: number;
  analyzerNotes: string | null;
}
