export type BoardStatus = 'pending' | 'active' | 'paused' | 'error';
export type RunStatus =
  | 'enqueued'
  | 'scheduled'
  | 'awaiting'
  | 'processing'
  | 'completed'
  | 'failed'
  | 'partial'
  | 'deleted';

/** Returns true for statuses that represent an in-flight (not yet terminal) run. */
export function isActiveRunStatus(status: RunStatus | string): boolean {
  return status === 'enqueued' || status === 'scheduled' || status === 'awaiting' || status === 'processing';
}
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
  hasActiveRun: boolean;
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
  lastScoringError: string | null;
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
  boardName?: string | null;
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
  boardAnalyzerModel: string;
  scoringModel: string;
  resumeGenerationModel: string;
  letterGenerationModel: string;
  boardAnalyzerMaxTokens: number;
  scoringMaxTokens: number;
  resumeGenerationMaxTokens: number;
  letterGenerationMaxTokens: number;
  availableModels: string[];
  hasResume: boolean;
  resumeFileName: string | null;
  resumeUploadedAt: string | null;
  hasResumeTemplate: boolean;
  resumeTemplateFileName: string | null;
  resumeTemplateUploadedAt: string | null;
}

export type ResumeOptimizationLevel = 'None' | 'Light' | 'Medium' | 'Heavy';

export interface GeneratedResume {
  id: string;
  applicationId: string;
  versionNumber: number;
  htmlContent: string;
  modelUsed: string;
  generatedAt: string;
  optimizationLevel: ResumeOptimizationLevel | null;
  isManualEdit: boolean;
}

export interface GeneratedLetter {
  id: string;
  applicationId: string;
  versionNumber: number;
  htmlContent: string;
  modelUsed: string;
  generatedAt: string;
  isManualEdit: boolean;
}

export type ApplicationStatus =
  | 'ToApply'
  | 'Applied'
  | 'Interviewing'
  | 'ClosedNoAnswer'
  | 'ClosedRejected'
  | 'ClosedHired';

export interface StatusHistoryEntry {
  status: ApplicationStatus;
  achievedAt: string;
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

export interface Application {
  id: string;
  jobAdId: string | null;
  createdAt: string;
  isTrashed: boolean;
  url: string;
  title: string | null;
  company: string | null;
  location: string | null;
  salaryRaw: string | null;
  description: string | null;
  postedAt: string | null;
  scrapedAt: string;
  externalId: string | null;
  overallScore: number | null;
  scoredAt: string | null;
  summary: string | null;
  recommendation: string | null;
  requirements: ScoringRequirement[];
  jobAdContent: string | null;
  isScoringPending: boolean;
  lastScoringError: string | null;
  isResumeGenerationPending: boolean;
  lastResumeGenerationError: string | null;
  isLetterGenerationPending: boolean;
  lastLetterGenerationError: string | null;
  status: ApplicationStatus;
  statusHistory: StatusHistoryEntry[];
}

export interface UpdateSettingsRequest {
  boardAnalyzerModel: string;
  scoringModel: string;
  resumeGenerationModel: string;
  letterGenerationModel: string;
  boardAnalyzerMaxTokens: number;
  scoringMaxTokens: number;
  resumeGenerationMaxTokens: number;
  letterGenerationMaxTokens: number;
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
