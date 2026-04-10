'use client';

import { useState } from 'react';
import type { InterviewStep, InterviewStepInterviewer } from '@/types';
import {
  useInterviewSteps,
  useCreateInterviewStep,
  useUpdateInterviewStep,
  useDeleteInterviewStep,
} from '@/lib/hooks/useApplications';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';

// ── Constants ─────────────────────────────────────────────────────────────────

const COMMON_TIMEZONES = [
  'UTC', 'CET', 'CEST', 'EET', 'EEST',
  'GMT', 'BST', 'IST', 'JST', 'CST',
  'EST', 'EDT', 'PST', 'PDT', 'MST', 'MDT',
];

// ── Types ─────────────────────────────────────────────────────────────────────

interface StepFormState {
  date: string;
  time: string;
  durationMinutes: string;
  timezone: string;
  isOnSite: boolean;
  remoteCallLink: string;
  interviewers: InterviewStepInterviewer[];
  notes: string;
}

function emptyForm(): StepFormState {
  return {
    date: '',
    time: '',
    durationMinutes: '',
    timezone: 'CEST',
    isOnSite: false,
    remoteCallLink: '',
    interviewers: [],
    notes: '',
  };
}

function stepToForm(step: InterviewStep): StepFormState {
  return {
    date: step.date ?? '',
    time: step.time ?? '',
    durationMinutes: step.durationMinutes != null ? String(step.durationMinutes) : '',
    timezone: step.timezone,
    isOnSite: step.isOnSite,
    remoteCallLink: step.remoteCallLink ?? '',
    interviewers: step.interviewers.map(i => ({ name: i.name, jobFunction: i.jobFunction })),
    notes: step.notes ?? '',
  };
}

// ── Interviewer list editor ───────────────────────────────────────────────────

function InterviewerListEditor({
  interviewers,
  onChange,
}: {
  interviewers: InterviewStepInterviewer[];
  onChange: (updated: InterviewStepInterviewer[]) => void;
}) {
  function add() {
    onChange([...interviewers, { name: '', jobFunction: '' }]);
  }

  function remove(index: number) {
    onChange(interviewers.filter((_, i) => i !== index));
  }

  function update(index: number, field: keyof InterviewStepInterviewer, value: string) {
    onChange(interviewers.map((iv, i) => i === index ? { ...iv, [field]: value } : iv));
  }

  return (
    <div className="space-y-2">
      {interviewers.map((iv, i) => (
        <div key={i} className="flex gap-2 items-center">
          <input
            type="text"
            placeholder="Name"
            value={iv.name}
            onChange={e => update(i, 'name', e.target.value)}
            className="flex-1 min-w-0 text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
          <input
            type="text"
            placeholder="Title / Function"
            value={iv.jobFunction}
            onChange={e => update(i, 'jobFunction', e.target.value)}
            className="flex-1 min-w-0 text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
          <button
            type="button"
            onClick={() => remove(i)}
            className="shrink-0 text-gray-300 hover:text-red-500 transition-colors p-1 rounded"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4">
              <path fillRule="evenodd" d="M5 3.25V4H2.75a.75.75 0 0 0 0 1.5h.3l.815 8.15A1.5 1.5 0 0 0 5.357 15h5.285a1.5 1.5 0 0 0 1.493-1.35l.815-8.15h.3a.75.75 0 0 0 0-1.5H11v-.75A2.25 2.25 0 0 0 8.75 1h-1.5A2.25 2.25 0 0 0 5 3.25Zm2.25-.75a.75.75 0 0 0-.75.75V4h3v-.75a.75.75 0 0 0-.75-.75h-1.5ZM6.05 6a.75.75 0 0 1 .787.713l.275 5.5a.75.75 0 0 1-1.498.075l-.275-5.5A.75.75 0 0 1 6.05 6Zm3.9 0a.75.75 0 0 1 .712.787l-.275 5.5a.75.75 0 0 1-1.498-.075l.275-5.5a.75.75 0 0 1 .786-.711Z" clipRule="evenodd" />
            </svg>
          </button>
        </div>
      ))}
      <button
        type="button"
        onClick={add}
        className="flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800 font-medium transition-colors"
      >
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
          <path d="M8.75 3.75a.75.75 0 0 0-1.5 0v3.5h-3.5a.75.75 0 0 0 0 1.5h3.5v3.5a.75.75 0 0 0 1.5 0v-3.5h3.5a.75.75 0 0 0 0-1.5h-3.5v-3.5Z" />
        </svg>
        Add interviewer
      </button>
    </div>
  );
}

// ── Step form (create / edit inline) ─────────────────────────────────────────

function StepForm({
  title,
  form,
  onChange,
  onSubmit,
  onCancel,
  isPending,
}: {
  title: string;
  form: StepFormState;
  onChange: (updated: StepFormState) => void;
  onSubmit: () => void;
  onCancel: () => void;
  isPending: boolean;
}) {
  function set<K extends keyof StepFormState>(key: K, value: StepFormState[K]) {
    onChange({ ...form, [key]: value });
  }

  return (
    <div className="bg-white rounded-lg border border-indigo-200 p-5 space-y-4">
      <p className="text-sm font-semibold text-gray-800">{title}</p>

      {/* Date / Time / Duration / Timezone row */}
      <div className="grid grid-cols-4 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Date</label>
          <input
            type="date"
            value={form.date}
            onChange={e => set('date', e.target.value)}
            className="w-full text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Time</label>
          <input
            type="text"
            inputMode="numeric"
            placeholder="HH:MM"
            maxLength={5}
            value={form.time}
            onChange={e => {
              const digits = e.target.value.replace(/\D/g, '').slice(0, 4);
              const formatted = digits.length > 2 ? `${digits.slice(0, 2)}:${digits.slice(2)}` : digits;
              set('time', formatted);
            }}
            className="w-full text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Duration (min)</label>
          <input
            type="number"
            min={1}
            placeholder="e.g. 60"
            value={form.durationMinutes}
            onChange={e => set('durationMinutes', e.target.value)}
            className="w-full text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Timezone</label>
          <input
            type="text"
            list="tz-list"
            value={form.timezone}
            onChange={e => set('timezone', e.target.value)}
            className="w-full text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
          <datalist id="tz-list">
            {COMMON_TIMEZONES.map(tz => <option key={tz} value={tz} />)}
          </datalist>
        </div>
      </div>

      {/* On-site / Remote toggle */}
      <div>
        <label className="block text-xs font-medium text-gray-500 mb-1.5">Format</label>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => set('isOnSite', true)}
            className={`px-3 py-1.5 text-xs font-medium rounded-md border transition-colors ${
              form.isOnSite
                ? 'border-indigo-500 bg-indigo-50 text-indigo-700'
                : 'border-gray-200 text-gray-600 hover:border-gray-300'
            }`}
          >
            On-site
          </button>
          <button
            type="button"
            onClick={() => set('isOnSite', false)}
            className={`px-3 py-1.5 text-xs font-medium rounded-md border transition-colors ${
              !form.isOnSite
                ? 'border-indigo-500 bg-indigo-50 text-indigo-700'
                : 'border-gray-200 text-gray-600 hover:border-gray-300'
            }`}
          >
            Remote
          </button>
        </div>
      </div>

      {/* Remote call link */}
      {!form.isOnSite && (
        <div>
          <label className="block text-xs font-medium text-gray-500 mb-1">Call link</label>
          <input
            type="url"
            placeholder="https://meet.google.com/..."
            value={form.remoteCallLink}
            onChange={e => set('remoteCallLink', e.target.value)}
            className="w-full text-sm border border-gray-200 rounded-md px-2.5 py-1.5 focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
        </div>
      )}

      {/* Interviewers */}
      <div>
        <label className="block text-xs font-medium text-gray-500 mb-1.5">Interviewers</label>
        <InterviewerListEditor
          interviewers={form.interviewers}
          onChange={updated => set('interviewers', updated)}
        />
      </div>

      {/* Notes */}
      <div>
        <label className="block text-xs font-medium text-gray-500 mb-1">Notes</label>
        <textarea
          rows={4}
          placeholder="Preparation notes, topics to cover, follow-up actions…"
          value={form.notes}
          onChange={e => set('notes', e.target.value)}
          className="w-full text-sm border border-gray-200 rounded-md px-2.5 py-2 focus:outline-none focus:ring-1 focus:ring-indigo-400 resize-y"
        />
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-2 pt-1">
        <button
          type="button"
          onClick={onCancel}
          disabled={isPending}
          className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:text-gray-800 border border-gray-200 rounded-md disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          type="button"
          onClick={onSubmit}
          disabled={isPending}
          className="px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-md disabled:opacity-50"
        >
          {isPending ? 'Saving…' : 'Save'}
        </button>
      </div>
    </div>
  );
}

// ── Step card (read view) ─────────────────────────────────────────────────────

function StepCard({
  step,
  isNext,
  onEdit,
  onDelete,
}: {
  step: InterviewStep;
  isNext: boolean;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const dateLabel = step.date
    ? new Date(step.date + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric' })
    : null;

  return (
    <div className={`rounded-lg border p-5 ${
      isNext
        ? 'bg-indigo-50 border-indigo-300'
        : 'bg-white border-gray-200'
    }`}>
      {/* Header */}
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <p className={`text-sm font-semibold ${isNext ? 'text-indigo-900' : 'text-gray-900'}`}>
              Step {step.stepNumber}
            </p>
            {isNext && (
              <span className="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-indigo-600 text-white">
                Next
              </span>
            )}
          </div>
          {(dateLabel || step.time || step.durationMinutes) && (
            <p className={`text-xs mt-0.5 ${isNext ? 'text-indigo-600' : 'text-gray-500'}`}>
              {[
                dateLabel,
                step.time ? `${step.time} ${step.timezone}` : null,
                step.durationMinutes ? `${step.durationMinutes} min` : null,
              ]
                .filter(Boolean)
                .join(' · ')}
            </p>
          )}
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <span className={`inline-block text-[11px] px-2 py-0.5 rounded-full font-medium ${
            step.isOnSite
              ? 'bg-green-50 text-green-700'
              : 'bg-blue-50 text-blue-700'
          }`}>
            {step.isOnSite ? 'On-site' : 'Remote'}
          </span>
          <button
            onClick={onEdit}
            className="text-xs text-gray-400 hover:text-indigo-600 font-medium transition-colors"
          >
            Edit
          </button>
          <button
            onClick={onDelete}
            className="text-xs text-gray-400 hover:text-red-500 font-medium transition-colors"
          >
            Delete
          </button>
        </div>
      </div>

      {/* Remote call link */}
      {!step.isOnSite && step.remoteCallLink && (
        <p className="mt-2 text-xs">
          <a
            href={step.remoteCallLink}
            target="_blank"
            rel="noopener noreferrer"
            className="text-indigo-600 hover:underline break-all"
          >
            {step.remoteCallLink}
          </a>
        </p>
      )}

      {/* Interviewers */}
      {step.interviewers.length > 0 && (
        <div className="mt-3">
          <p className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide mb-1.5">Interviewers</p>
          <div className="flex flex-wrap gap-2">
            {step.interviewers.map((iv, i) => (
              <span
                key={i}
                className="inline-flex items-center gap-1 text-xs bg-gray-100 text-gray-700 rounded-full px-2.5 py-1"
              >
                <span className="font-medium">{iv.name}</span>
                {iv.jobFunction && (
                  <span className="text-gray-400">· {iv.jobFunction}</span>
                )}
              </span>
            ))}
          </div>
        </div>
      )}

      {/* Notes */}
      {step.notes && (
        <div className="mt-3">
          <p className="text-[11px] font-semibold text-gray-400 uppercase tracking-wide mb-1">Notes</p>
          <p className="text-sm text-gray-700 whitespace-pre-wrap leading-relaxed">{step.notes}</p>
        </div>
      )}
    </div>
  );
}

// ── Main tab ──────────────────────────────────────────────────────────────────

export function InterviewStepsTab({ appId }: { appId: string }) {
  const { data: steps = [], isLoading } = useInterviewSteps(appId);
  const createStep = useCreateInterviewStep(appId);
  const updateStep = useUpdateInterviewStep(appId);
  const deleteStep = useDeleteInterviewStep(appId);

  const [showCreateForm, setShowCreateForm] = useState(false);
  const [createForm, setCreateForm] = useState<StepFormState>(emptyForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<StepFormState>(emptyForm());
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  function startEdit(step: InterviewStep) {
    setEditingId(step.id);
    setEditForm(stepToForm(step));
  }

  function cancelEdit() {
    setEditingId(null);
  }

  function submitCreate() {
    createStep.mutate(
      {
        date: createForm.date || null,
        time: createForm.time || null,
        durationMinutes: createForm.durationMinutes ? parseInt(createForm.durationMinutes, 10) : null,
        timezone: createForm.timezone,
        isOnSite: createForm.isOnSite,
        remoteCallLink: createForm.remoteCallLink || null,
        interviewers: createForm.interviewers,
        notes: createForm.notes || null,
      },
      {
        onSuccess: () => {
          setShowCreateForm(false);
          setCreateForm(emptyForm());
        },
      },
    );
  }

  function submitEdit() {
    if (!editingId) return;
    updateStep.mutate(
      {
        stepId: editingId,
        data: {
          date: editForm.date || null,
          time: editForm.time || null,
          durationMinutes: editForm.durationMinutes ? parseInt(editForm.durationMinutes, 10) : null,
          timezone: editForm.timezone,
          isOnSite: editForm.isOnSite,
          remoteCallLink: editForm.remoteCallLink || null,
          interviewers: editForm.interviewers,
          notes: editForm.notes || null,
        },
      },
      {
        onSuccess: () => setEditingId(null),
      },
    );
  }

  function confirmDelete() {
    if (!confirmDeleteId) return;
    deleteStep.mutate(confirmDeleteId, {
      onSuccess: () => setConfirmDeleteId(null),
    });
  }

  if (isLoading) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 flex justify-center">
        <LoadingSpinner />
      </div>
    );
  }

  // Steps arrive ordered by stepNumber asc from the API; reverse for display (newest on top).
  const displaySteps = [...steps].reverse();

  // The next upcoming step: earliest future date (or today), among steps that have a date.
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const nextStep = steps
    .filter(s => s.date !== null && new Date(s.date + 'T00:00:00') >= today)
    .sort((a, b) => a.date!.localeCompare(b.date!))[0] ?? null;

  return (
    <div className="space-y-4">
      {/* Create form / add button — always on top */}
      {showCreateForm ? (
        <StepForm
          title="New Interview Step"
          form={createForm}
          onChange={setCreateForm}
          onSubmit={submitCreate}
          onCancel={() => { setShowCreateForm(false); setCreateForm(emptyForm()); }}
          isPending={createStep.isPending}
        />
      ) : (
        <button
          onClick={() => setShowCreateForm(true)}
          className="w-full flex items-center justify-center gap-1.5 text-sm font-medium text-indigo-600 hover:text-indigo-800 border border-dashed border-indigo-200 hover:border-indigo-400 rounded-lg py-3 transition-colors"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-4 h-4">
            <path d="M8.75 3.75a.75.75 0 0 0-1.5 0v3.5h-3.5a.75.75 0 0 0 0 1.5h3.5v3.5a.75.75 0 0 0 1.5 0v-3.5h3.5a.75.75 0 0 0 0-1.5h-3.5v-3.5Z" />
          </svg>
          Add interview step
        </button>
      )}

      {/* Step list */}
      {displaySteps.map(step =>
        editingId === step.id ? (
          <StepForm
            key={step.id}
            title={`Edit Step ${step.stepNumber}`}
            form={editForm}
            onChange={setEditForm}
            onSubmit={submitEdit}
            onCancel={cancelEdit}
            isPending={updateStep.isPending}
          />
        ) : (
          <StepCard
            key={step.id}
            step={step}
            isNext={nextStep?.id === step.id}
            onEdit={() => startEdit(step)}
            onDelete={() => setConfirmDeleteId(step.id)}
          />
        ),
      )}

      {/* Empty state */}
      {steps.length === 0 && !showCreateForm && (
        <div className="bg-white rounded-lg border border-dashed border-gray-200 p-12 text-center">
          <div className="w-10 h-10 rounded-full bg-indigo-50 flex items-center justify-center mx-auto mb-3">
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5 text-indigo-400">
              <path fillRule="evenodd" d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm.75-11.25a.75.75 0 0 0-1.5 0v2.5h-2.5a.75.75 0 0 0 0 1.5h2.5v2.5a.75.75 0 0 0 1.5 0v-2.5h2.5a.75.75 0 0 0 0-1.5h-2.5v-2.5Z" clipRule="evenodd" />
            </svg>
          </div>
          <p className="text-sm font-medium text-gray-700">No interview steps yet</p>
          <p className="text-xs text-gray-400 mt-1">Track each round of your interview process here.</p>
        </div>
      )}

      {/* Delete confirmation modal */}
      {confirmDeleteId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
          <div className="bg-white rounded-xl shadow-xl p-6 w-full max-w-sm mx-4">
            <h3 className="text-sm font-semibold text-gray-900 mb-2">Delete interview step?</h3>
            <p className="text-xs text-gray-500 mb-4">This action cannot be undone.</p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setConfirmDeleteId(null)}
                disabled={deleteStep.isPending}
                className="px-3 py-1.5 text-xs font-medium text-gray-600 hover:text-gray-800 border border-gray-200 rounded-md disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={confirmDelete}
                disabled={deleteStep.isPending}
                className="px-3 py-1.5 text-xs font-medium text-white bg-red-600 hover:bg-red-700 rounded-md disabled:opacity-50"
              >
                {deleteStep.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
