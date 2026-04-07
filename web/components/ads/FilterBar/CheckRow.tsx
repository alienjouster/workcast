'use client';

import React from 'react';

export const CheckRow = React.memo(function CheckRow({
  label,
  checked,
  onToggle,
}: {
  label: string;
  checked: boolean;
  onToggle: () => void;
}) {
  return (
    <button
      onClick={onToggle}
      className={`flex items-center gap-2 w-full text-left px-2 py-1.5 rounded text-sm hover:bg-gray-50 ${checked ? 'text-indigo-700 font-medium' : 'text-gray-700'}`}
    >
      <span className={`w-4 h-4 rounded border flex items-center justify-center shrink-0 ${checked ? 'bg-indigo-600 border-indigo-600' : 'border-gray-300'}`}>
        {checked && (
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 12 12" fill="none" className="w-3 h-3">
            <path d="M1 6l3.5 3.5L11 2" stroke="white" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" />
          </svg>
        )}
      </span>
      {label}
    </button>
  );
});
