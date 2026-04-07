'use client';

import { useState } from 'react';
import { SCORE_FILTER_PRESETS } from '@/lib/constants';

interface ScorePickerProps {
  value: number | undefined;
  onChange: (v: number | undefined) => void;
}

export function ScorePicker({ value, onChange }: ScorePickerProps) {
  const [local, setLocal] = useState(value ?? 70);

  return (
    <div className="p-4 w-60">
      <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide mb-3">Minimum match score</p>
      <div className="flex gap-2 mb-4">
        {SCORE_FILTER_PRESETS.map(preset => (
          <button
            key={preset}
            onClick={() => { setLocal(preset); onChange(preset); }}
            className={`flex-1 py-1.5 rounded-md text-sm font-medium border transition-colors ${
              value === preset
                ? 'bg-indigo-600 text-white border-indigo-600'
                : 'border-gray-300 text-gray-700 hover:bg-gray-50'
            }`}
          >
            ≥{preset}%
          </button>
        ))}
      </div>
      <div className="space-y-1.5">
        <div className="flex justify-between text-xs text-gray-500">
          <span>0%</span>
          <span className="font-semibold text-indigo-700">≥ {local}%</span>
          <span>100%</span>
        </div>
        <input
          type="range"
          min={0}
          max={100}
          step={5}
          value={local}
          onChange={e => setLocal(Number(e.target.value))}
          onMouseUp={() => onChange(local)}
          onTouchEnd={() => onChange(local)}
          className="w-full accent-indigo-600"
        />
      </div>
      {value !== undefined && (
        <button
          onClick={() => onChange(undefined)}
          className="mt-3 w-full text-xs text-gray-400 hover:text-red-500 text-center transition-colors"
        >
          Clear score filter
        </button>
      )}
    </div>
  );
}
