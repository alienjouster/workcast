'use client';

import React from 'react';
import type { TriState } from './types';

export const TriStateCheckRow = React.memo(function TriStateCheckRow({
  label,
  state,
  onCycle,
}: {
  label: string;
  state: TriState;
  onCycle: () => void;
}) {
  const isInclude = state === 'include';
  const isExclude = state === 'exclude';
  const textClass = isInclude
    ? 'text-indigo-700 font-medium'
    : isExclude
    ? 'text-rose-700 font-medium'
    : 'text-gray-700';
  const boxClass = isInclude
    ? 'bg-indigo-600 border-indigo-600'
    : isExclude
    ? 'bg-rose-500 border-rose-500'
    : 'border-gray-300';

  return (
    <button
      onClick={onCycle}
      className={`flex items-center gap-2 w-full text-left px-2 py-1.5 rounded text-sm hover:bg-gray-50 ${textClass}`}
    >
      <span className={`w-4 h-4 rounded border flex items-center justify-center shrink-0 ${boxClass}`}>
        {isInclude && (
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 12 12" fill="none" className="w-3 h-3">
            <path d="M1 6l3.5 3.5L11 2" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        )}
        {isExclude && (
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 12 12" fill="none" className="w-3 h-3">
            <path d="M2 6h8" stroke="white" strokeWidth={2} strokeLinecap="round" />
          </svg>
        )}
      </span>
      {label}
    </button>
  );
});
