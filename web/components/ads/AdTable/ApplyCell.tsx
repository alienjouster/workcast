'use client';

import { useRouter } from 'next/navigation';
import { useCreateApplication } from '@/lib/hooks/useApplications';
import { Tooltip } from '@/components/ui/Tooltip';

export function ApplyCell({ adId }: { adId: string }) {
  const router = useRouter();
  const createApplication = useCreateApplication();

  return (
    <Tooltip content="Apply to this job" position="top" wrapperAs="span">
      <button
        disabled={createApplication.isPending}
        onClick={(e) => {
          e.stopPropagation();
          createApplication.mutateAsync(adId).then((application) => {
            router.push(`/applications/${application.id}`);
          });
        }}
        className="text-gray-300 hover:text-indigo-400 transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
      >
        {createApplication.isPending ? (
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" className="w-5 h-5 text-indigo-400 animate-spin">
            <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="3" strokeOpacity="0.2" />
            <path d="M12 3a9 9 0 0 1 9 9" stroke="currentColor" strokeWidth="3" strokeLinecap="round" />
          </svg>
        ) : (
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="w-5 h-5">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
            <path d="M14 2v6h6" />
            <path d="M8 13h8m0 0-3-3m3 3-3 3" />
          </svg>
        )}
      </button>
    </Tooltip>
  );
}
