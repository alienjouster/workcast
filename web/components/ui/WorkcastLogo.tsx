'use client';

import { useIsProcessing } from '@/lib/hooks/useProcessingStatus';

export function WorkcastLogo() {
  const isProcessing = useIsProcessing().data ?? false;

  return (
    <div className="relative flex items-center gap-2 select-none group">
      <svg
        viewBox="0 0 24 24"
        fill="none"
        className={`w-6 h-6 transition-all duration-300 ${isProcessing ? 'drop-shadow-[0_0_5px_#818cf8]' : ''}`}
      >
        {/* Spokes */}
        <line x1="12" y1="9.5"    x2="12" y2="3.5"   stroke="#818cf8" strokeWidth="1.5" strokeLinecap="round" opacity=".5"/>
        <line x1="11.5" y1="14.5" x2="5"  y2="19.5"  stroke="#818cf8" strokeWidth="1.5" strokeLinecap="round" opacity=".5"/>
        <line x1="12.5" y1="14.5" x2="19" y2="19.5"  stroke="#818cf8" strokeWidth="1.5" strokeLinecap="round" opacity=".5"/>

        {/* Central hub */}
        <circle cx="12" cy="12" r="3.5" fill="#4f46e5">
          {isProcessing && (
            <animate attributeName="r" values="3.5;4.4;3.5" dur="1.4s" repeatCount="indefinite"/>
          )}
        </circle>

        {/* Outer nodes */}
        <circle cx="12" cy="2.5" r="2" fill="#6366f1" opacity=".7"/>
        <circle cx="4"  cy="21"  r="2" fill="#6366f1" opacity=".7"/>
        <circle cx="20" cy="21"  r="2" fill="#6366f1" opacity=".7"/>

        {/* Inward-flow particles — only rendered when processing */}
        {isProcessing && (
          <>
            <circle cx="12" cy="2.5" r="1.8" fill="#a5b4fc">
              <animateTransform attributeName="transform" type="translate"
                values="0,0; 0,9.5; 0,9.5" keyTimes="0; 0.7; 1"
                dur="1.4s" begin="0s" repeatCount="indefinite"/>
              <animate attributeName="opacity" values="0.9; 0.7; 0"
                keyTimes="0; 0.7; 1" dur="1.4s" begin="0s" repeatCount="indefinite"/>
            </circle>
            <circle cx="4" cy="21" r="1.8" fill="#a5b4fc">
              <animateTransform attributeName="transform" type="translate"
                values="0,0; 8,-9; 8,-9" keyTimes="0; 0.7; 1"
                dur="1.4s" begin="0.45s" repeatCount="indefinite"/>
              <animate attributeName="opacity" values="0.9; 0.7; 0"
                keyTimes="0; 0.7; 1" dur="1.4s" begin="0.45s" repeatCount="indefinite"/>
            </circle>
            <circle cx="20" cy="21" r="1.8" fill="#a5b4fc">
              <animateTransform attributeName="transform" type="translate"
                values="0,0; -8,-9; -8,-9" keyTimes="0; 0.7; 1"
                dur="1.4s" begin="0.9s" repeatCount="indefinite"/>
              <animate attributeName="opacity" values="0.9; 0.7; 0"
                keyTimes="0; 0.7; 1" dur="1.4s" begin="0.9s" repeatCount="indefinite"/>
            </circle>
          </>
        )}
      </svg>

      <span className="text-2xl font-extrabold text-indigo-700 tracking-tight">
        Workcast
      </span>

      {isProcessing && (
        <span className="pointer-events-none absolute left-0 top-full mt-2 whitespace-nowrap rounded bg-gray-900 px-2.5 py-1.5 text-xs text-white opacity-0 group-hover:opacity-100 transition-opacity z-20">
          Working like a dog for you...
        </span>
      )}
    </div>
  );
}
