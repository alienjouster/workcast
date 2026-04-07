'use client';

import { Tooltip } from '@/components/ui/Tooltip';

export function Chip({
  label,
  variant = 'include',
  onToggleVariant,
  onRemove,
}: {
  label: string;
  variant?: 'include' | 'exclude';
  onToggleVariant?: () => void;
  onRemove: () => void;
}) {
  const isExclude = variant === 'exclude';
  const colors = isExclude ? 'bg-rose-100 text-rose-800' : 'bg-indigo-100 text-indigo-800';
  const toggleHover = isExclude ? 'hover:bg-rose-200' : 'hover:bg-indigo-200';
  const removeHover = isExclude ? 'hover:bg-rose-200' : 'hover:bg-indigo-200';

  return (
    <span className={`inline-flex items-center rounded-full pr-1.5 py-1 text-xs font-medium ${colors}`}>
      {onToggleVariant ? (
        <Tooltip content={isExclude ? 'Switch to include' : 'Switch to exclude'} position="top" wrapperAs="span">
          <button
            onClick={onToggleVariant}
            className={`pl-2 pr-1.5 py-0.5 font-bold rounded-full transition-colors ${toggleHover}`}
            aria-label={isExclude ? 'Switch to include' : 'Switch to exclude'}
          >
            {isExclude ? '≠' : '='}
          </button>
        </Tooltip>
      ) : (
        <span className="pl-3" />
      )}
      <span className={onToggleVariant ? '' : 'pl-3'}>{label}</span>
      <button
        onClick={onRemove}
        className={`rounded-full p-0.5 ml-1 transition-colors ${removeHover}`}
        aria-label="Remove filter"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3 h-3">
          <path d="M5.28 4.22a.75.75 0 0 0-1.06 1.06L6.94 8l-2.72 2.72a.75.75 0 1 0 1.06 1.06L8 9.06l2.72 2.72a.75.75 0 1 0 1.06-1.06L9.06 8l2.72-2.72a.75.75 0 0 0-1.06-1.06L8 6.94 5.28 4.22Z" />
        </svg>
      </button>
    </span>
  );
}
