'use client';

import React from 'react';
import { BADGE_OVERFLOW } from '@/lib/constants';

export interface TabDef {
  key: string;
  label: string;
  /** Total (unfiltered) count shown in the badge. Omit or pass 0 to hide. */
  count?: number;
  /** Optional icon rendered before the label. */
  icon?: React.ReactNode;
}

interface TabToggleProps {
  tabs: TabDef[];
  activeKey: string;
  onChange: (key: string) => void;
}

/** Shared tab bar used by the Ads and Applications pages. */
export function TabToggle({ tabs, activeKey, onChange }: TabToggleProps) {
  return (
    <div className="flex items-center gap-1 mb-4 border-b border-gray-200">
      {tabs.map((tab) => (
        <button
          key={tab.key}
          onClick={() => onChange(tab.key)}
          className={`flex items-center gap-1.5 px-4 py-2 text-sm font-medium border-b-2 -mb-px transition-colors ${
            activeKey === tab.key
              ? 'border-indigo-600 text-indigo-600'
              : 'border-transparent text-gray-500 hover:text-gray-700'
          }`}
        >
          {tab.icon}
          {tab.label}
          {tab.count !== undefined && tab.count > 0 && (
            <span className="inline-flex items-center justify-center min-w-[1.25rem] h-4 px-1 rounded-full bg-gray-200 text-gray-600 text-xs font-medium">
              {tab.count > BADGE_OVERFLOW ? `${BADGE_OVERFLOW}+` : tab.count}
            </span>
          )}
        </button>
      ))}
    </div>
  );
}

/** Trash bin icon — shared between the Ads and Applications tab toggles. */
export const TrashTabIcon = (
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
    <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z" clipRule="evenodd" />
  </svg>
);
