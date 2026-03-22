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
  paginationType: 'url_param' | 'next_button' | 'infinite_scroll' | 'load_more_button' | 'none';
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
  isRead: boolean;
  overallScore: number | null;
  isScoringPending: boolean;
  isTrashed: boolean;
  note: string | null;
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
  totalCount: number;
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

export interface AppSettings {
  aiModel: string;
  availableModels: string[];
  hasResume: boolean;
  resumeFileName: string | null;
  resumeUploadedAt: string | null;
}

export type ScoringCategory = 'match' | 'partial_match' | 'gap';

export interface ScoringRequirement {
  name: string;
  category: ScoringCategory;
  isOptional: boolean;
  score: number;
  notes: string | null;
}

export interface AdScoring {
  id: string;
  jobAdId: string;
  scoredAt: string;
  overallScore: number;
  summary: string;
  recommendation: string;
  requirements: ScoringRequirement[];
}

export interface UpdateSettingsRequest {
  aiModel: string;
}

export interface UpdateScraperConfigRequest {
  paginationType: 'url_param' | 'next_button' | 'infinite_scroll' | 'load_more_button' | 'none';
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
