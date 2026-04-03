'use client';

import { useEffect, useRef, useState } from 'react';
import { useCreateJobAd, useUpdateJobAd } from '@/lib/hooks/useJobAds';
import type { JobAd } from '@/types';

// Matches http:// or https:// followed by at least one dot-separated segment.
const URL_REGEX = /^https?:\/\/[^\s/$.?#][^\s]*\.[^\s]+$/i;

interface NewJobAdModalProps {
  onClose: () => void;
  /** When provided, the modal operates in edit mode for this ad. */
  ad?: JobAd;
}

export function NewJobAdModal({ onClose, ad }: NewJobAdModalProps) {
  const isEditMode = ad !== undefined;

  const [url, setUrl] = useState(ad?.url ?? '');
  const [title, setTitle] = useState(ad?.title ?? '');
  const [company, setCompany] = useState(ad?.company ?? '');
  const [location, setLocation] = useState(ad?.location ?? '');
  const [urlError, setUrlError] = useState<string | null>(null);

  const urlRef = useRef<HTMLInputElement>(null);
  const createJobAd = useCreateJobAd();
  const updateJobAd = useUpdateJobAd();
  const mutation = isEditMode ? updateJobAd : createJobAd;

  useEffect(() => {
    urlRef.current?.focus();
  }, []);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [onClose]);

  const validateUrl = (value: string): boolean => {
    if (!URL_REGEX.test(value.trim())) {
      setUrlError('Please enter a valid URL starting with http:// or https://');
      return false;
    }
    setUrlError(null);
    return true;
  };

  const isValid = url.trim().length > 0 && title.trim().length > 0 && urlError === null;

  const submit = () => {
    if (mutation.isPending) return;
    if (!validateUrl(url)) return;
    if (!isValid) return;

    const data = {
      url: url.trim(),
      title: title.trim(),
      company: company.trim() || undefined,
      location: location.trim() || undefined,
    };

    if (isEditMode) {
      updateJobAd.mutate({ id: ad.id, data }, { onSuccess: onClose });
    } else {
      createJobAd.mutate(data, { onSuccess: onClose });
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) submit();
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40"
    >
      <div className="bg-white rounded-lg shadow-xl w-full max-w-lg mx-4 flex flex-col" onKeyDown={handleKeyDown}>
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200">
          <h2 className="text-sm font-semibold text-gray-900">
            {isEditMode ? 'Edit Job Ad' : 'New Job Ad'}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 transition-colors"
            aria-label="Close"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5">
              <path d="M6.28 5.22a.75.75 0 0 0-1.06 1.06L8.94 10l-3.72 3.72a.75.75 0 1 0 1.06 1.06L10 11.06l3.72 3.72a.75.75 0 1 0 1.06-1.06L11.06 10l3.72-3.72a.75.75 0 0 0-1.06-1.06L10 8.94 6.28 5.22Z" />
            </svg>
          </button>
        </div>

        {/* Form */}
        <div className="px-5 py-4 flex flex-col gap-4">
          <div>
            <label className="block text-xs font-medium text-gray-700 mb-1">
              URL <span className="text-red-500">*</span>
            </label>
            <input
              ref={urlRef}
              type="url"
              value={url}
              onChange={(e) => {
                setUrl(e.target.value);
                if (urlError) validateUrl(e.target.value);
              }}
              onBlur={() => url.trim() && validateUrl(url)}
              placeholder="https://example.com/jobs/123"
              className={`w-full rounded-md border px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-2 focus:border-transparent ${
                urlError
                  ? 'border-red-400 focus:ring-red-400'
                  : 'border-gray-300 focus:ring-indigo-500'
              }`}
            />
            {urlError && (
              <p className="mt-1 text-xs text-red-600">{urlError}</p>
            )}
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-700 mb-1">
              Title <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Software Engineer"
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
            />
          </div>
          <div className="flex gap-3">
            <div className="flex-1">
              <label className="block text-xs font-medium text-gray-700 mb-1">Company</label>
              <input
                type="text"
                value={company}
                onChange={(e) => setCompany(e.target.value)}
                placeholder="Acme Corp"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
            <div className="flex-1">
              <label className="block text-xs font-medium text-gray-700 mb-1">Location</label>
              <input
                type="text"
                value={location}
                onChange={(e) => setLocation(e.target.value)}
                placeholder="Remote"
                className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              />
            </div>
          </div>

          {mutation.isError && (
            <p className="text-sm text-red-600 bg-red-50 rounded-md px-3 py-2">
              {(mutation.error as Error).message}
            </p>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-5 py-3 border-t border-gray-200 bg-gray-50 rounded-b-lg">
          <button
            onClick={onClose}
            className="px-3 py-1.5 text-sm text-gray-600 hover:text-gray-800 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={submit}
            disabled={!isValid || mutation.isPending}
            className="px-4 py-1.5 text-sm font-medium bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            {mutation.isPending
              ? isEditMode ? 'Saving…' : 'Adding…'
              : isEditMode ? 'Save Changes' : 'Add Job Ad'}
          </button>
        </div>
      </div>
    </div>
  );
}
