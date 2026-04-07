import type { JobBoard } from '@/types';

export type StatusTag = 'active' | 'inactive' | 'read' | 'unread' | 'pinned' | 'unpinned';
export type FilterFeature = 'board' | 'status' | 'title' | 'location' | 'company' | 'score';
export type PopoverView = 'menu' | 'board' | 'status' | 'title' | 'location' | 'company' | 'score';
export type TriState = 'none' | 'include' | 'exclude';

export interface FilterState {
  boardIds: string[];
  excludeBoardIds: string[];
  titles: string[];
  excludeTitles: string[];
  statuses: StatusTag[];
  locations: string[];
  excludeLocations: string[];
  companies: string[];
  excludeCompanies: string[];
  minScore: number | undefined;
  /** When false, filters are preserved but not applied to queries. */
  enabled: boolean;
}

export interface SuggestionFetchers {
  titles?: (q: string) => Promise<string[]>;
  locations?: (q: string) => Promise<string[]>;
  companies?: (q: string) => Promise<string[]>;
}

export interface FilterBarProps {
  filters: FilterState;
  onChange: (f: FilterState) => void;
  /** Which filter categories to show. Defaults to all. */
  features?: FilterFeature[];
  /** Boards list — required when 'board' feature is enabled. */
  boards?: JobBoard[];
  /** Override typeahead data sources. Defaults to job-ads distinct endpoints. */
  suggestionFetchers?: SuggestionFetchers;
}
