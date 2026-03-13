'use client';

import { useState } from 'react';
import type { ScraperConfig } from '@/types';

interface ScraperConfigViewProps {
  config: ScraperConfig;
}

export function ScraperConfigView({ config }: ScraperConfigViewProps) {
  const [expanded, setExpanded] = useState(false);

  return (
    <div className="border border-gray-200 rounded-md overflow-hidden">
      <button
        onClick={() => setExpanded((v) => !v)}
        className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors"
      >
        <span>Scraper Configuration</span>
        <div className="flex items-center gap-3">
          <span className="text-xs text-gray-500">
            Confidence: {(config.confidenceScore * 100).toFixed(0)}%
          </span>
          <span>{expanded ? '▲' : '▼'}</span>
        </div>
      </button>
      {expanded && (
        <div className="p-4 space-y-3">
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <span className="text-gray-500">Pagination Type</span>
              <p className="font-mono font-medium">{config.paginationType}</p>
            </div>
            <div>
              <span className="text-gray-500">Requires JS</span>
              <p className="font-medium">{config.requiresJs ? 'Yes' : 'No'}</p>
            </div>
            <div>
              <span className="text-gray-500">Delay (ms)</span>
              <p className="font-medium">{config.suggestedDelayMs}</p>
            </div>
            <div>
              <span className="text-gray-500">Max Pages</span>
              <p className="font-medium">{config.maxPages ?? 'Unlimited'}</p>
            </div>
          </div>
          <div className="text-sm">
            <span className="text-gray-500">Job Links Selector</span>
            <p className="font-mono text-xs bg-gray-100 px-2 py-1 rounded mt-1">
              {config.jobLinksSelector}
            </p>
          </div>
          {config.nextPageSelector && (
            <div className="text-sm">
              <span className="text-gray-500">Next Page Selector</span>
              <p className="font-mono text-xs bg-gray-100 px-2 py-1 rounded mt-1">
                {config.nextPageSelector}
              </p>
            </div>
          )}
          {config.analyzerNotes && (
            <div className="text-sm">
              <span className="text-gray-500">Analyzer Notes</span>
              <p className="text-gray-700 mt-1">{config.analyzerNotes}</p>
            </div>
          )}
          <div className="text-xs text-gray-400">
            Generated at {new Date(config.generatedAt).toLocaleString()}
          </div>
        </div>
      )}
    </div>
  );
}
