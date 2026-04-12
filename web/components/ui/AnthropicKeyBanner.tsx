'use client';

import { useAiKeyError } from '@/lib/hooks/useProcessingStatus';

export function AnthropicKeyBanner() {
  const { data: aiKeyError } = useAiKeyError();

  if (!aiKeyError) return null;

  return (
    <div className="bg-red-600 text-white px-4 py-3">
      <div className="max-w-7xl mx-auto flex items-start gap-3">
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox="0 0 20 20"
          fill="currentColor"
          className="w-5 h-5 shrink-0 mt-0.5"
        >
          <path
            fillRule="evenodd"
            d="M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-8-5a.75.75 0 0 1 .75.75v4.5a.75.75 0 0 1-1.5 0v-4.5A.75.75 0 0 1 10 5Zm0 10a1 1 0 1 0 0-2 1 1 0 0 0 0 2Z"
            clipRule="evenodd"
          />
        </svg>
        <div>
          <p className="font-semibold text-sm">Anthropic API key error — AI features are unavailable</p>
          <p className="text-xs text-red-100 mt-0.5">{aiKeyError}</p>
          <p className="text-xs text-red-200 mt-1">
            Running locally: set <code className="bg-red-700 px-1 rounded">Anthropic:ApiKey</code> in{' '}
            <code className="bg-red-700 px-1 rounded">src/Workcast.Api/appsettings.Development.json</code>.
            {' '}Running Docker: set <code className="bg-red-700 px-1 rounded">ANTHROPIC_API_KEY</code> in{' '}
            <code className="bg-red-700 px-1 rounded">./docker/.env</code>.
            Restart after changing.
            {' '}No API key?{' '}
            <a
              href="https://platform.claude.com/"
              target="_blank"
              rel="noopener noreferrer"
              className="underline hover:text-white"
            >
              Get one at platform.claude.com
            </a>.
          </p>
        </div>
      </div>
    </div>
  );
}
