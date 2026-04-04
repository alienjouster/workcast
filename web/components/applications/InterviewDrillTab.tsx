'use client';

import React, { useState, useRef, useEffect } from 'react';
import type { Application, InterviewQuestionCategory } from '@/types';
import { useInterviewDrillPlan, useGenerateInterviewDrill, useCancelInterviewDrill, useSaveInterviewDrillAnswer } from '@/lib/hooks/useApplications';
import { ScoringSpinner, ScoringErrorBanner } from '@/components/scoring/ScoringShared';

// ── Category config ────────────────────────────────────────────────────────────

const CATEGORY_CONFIG: Record<InterviewQuestionCategory, { label: string; badgeClass: string }> = {
  warm_up:     { label: 'Warm Up',     badgeClass: 'bg-blue-100 text-blue-700' },
  easy:        { label: 'Easy',        badgeClass: 'bg-green-100 text-green-700' },
  medium:      { label: 'Medium',      badgeClass: 'bg-amber-100 text-amber-700' },
  challenging: { label: 'Challenging', badgeClass: 'bg-red-100 text-red-700' },
};

// ── SparkleIcon (local — avoids prop-drilling from parent) ────────────────────

function SparkleIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className={className}>
      <path d="M15.5 2a.5.5 0 0 1 .463.311l.82 2.047 2.047.82a.5.5 0 0 1 0 .925l-2.047.82-.82 2.047a.5.5 0 0 1-.925 0l-.82-2.047-2.047-.82a.5.5 0 0 1 0-.925l2.047-.82.82-2.047A.5.5 0 0 1 15.5 2ZM6 6a.5.5 0 0 1 .463.311l1.18 2.95 2.95 1.18a.5.5 0 0 1 0 .925l-2.95 1.18-1.18 2.95a.5.5 0 0 1-.925 0l-1.18-2.95-2.95-1.18a.5.5 0 0 1 0-.925l2.95-1.18 1.18-2.95A.5.5 0 0 1 6 6Z" />
    </svg>
  );
}

// ── SpeechRecognition type augmentation (not in lib.dom for all envs) ─────────
declare global {
  interface Window {
    SpeechRecognition: typeof SpeechRecognition | undefined;
    webkitSpeechRecognition: typeof SpeechRecognition | undefined;
  }
}

// ── MicIcon ───────────────────────────────────────────────────────────────────
function MicIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className={className}>
      <path d="M7 4a3 3 0 0 1 6 0v6a3 3 0 1 1-6 0V4Z" />
      <path d="M5.5 9.643a.75.75 0 0 0-1.5 0V10c0 3.06 2.29 5.585 5.25 5.954V17.5h-1.5a.75.75 0 0 0 0 1.5h4.5a.75.75 0 0 0 0-1.5h-1.5v-1.546A6.001 6.001 0 0 0 16 10v-.357a.75.75 0 0 0-1.5 0V10a4.5 4.5 0 0 1-9 0v-.357Z" />
    </svg>
  );
}

// ── SpeakerIcon ───────────────────────────────────────────────────────────────
function SpeakerIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className={className}>
      <path d="M10 3.75a.75.75 0 0 0-1.264-.546L4.703 7H3.167a.75.75 0 0 0-.7.48A6.985 6.985 0 0 0 2 10c0 .93.168 1.82.468 2.52.13.313.43.48.699.48h1.536l4.033 3.796A.75.75 0 0 0 10 16.25V3.75ZM15.95 5.05a.75.75 0 0 0-1.06 1.061 5.5 5.5 0 0 1 0 7.778.75.75 0 0 0 1.06 1.06 7 7 0 0 0 0-9.899Z" />
      <path d="M13.829 7.172a.75.75 0 0 0-1.061 1.06 2.5 2.5 0 0 1 0 3.536.75.75 0 0 0 1.06 1.06 4 4 0 0 0 0-5.656Z" />
    </svg>
  );
}

// ── StopIcon ──────────────────────────────────────────────────────────────────
function StopIcon({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className={className}>
      <path d="M5.25 3A2.25 2.25 0 0 0 3 5.25v9.5A2.25 2.25 0 0 0 5.25 17h9.5A2.25 2.25 0 0 0 17 14.75v-9.5A2.25 2.25 0 0 0 14.75 3h-9.5Z" />
    </svg>
  );
}

// ── Detect browser SpeechRecognition support ──────────────────────────────────
function getSpeechRecognition(): (new () => SpeechRecognition) | null {
  if (typeof window === 'undefined') return null;
  return window.SpeechRecognition ?? window.webkitSpeechRecognition ?? null;
}

// ── Browser / OS detection (static, UA-based) ─────────────────────────────────
type BrowserOS = {
  isWindows: boolean;
  isMac: boolean;
  isEdge: boolean;
  isChrome: boolean;
  isSafari: boolean;
  isFirefox: boolean;
};

function detectBrowserOS(): BrowserOS {
  if (typeof window === 'undefined') return { isWindows: false, isMac: false, isEdge: false, isChrome: false, isSafari: false, isFirefox: false };
  const ua = navigator.userAgent;
  const isWindows = ua.includes('Windows');
  const isMac    = ua.includes('Macintosh') || ua.includes('Mac OS');
  const isEdge   = ua.includes('Edg/');
  const isChrome = ua.includes('Chrome/') && !ua.includes('Edg/');
  const isSafari = ua.includes('Safari/') && !ua.includes('Chrome/');
  const isFirefox = ua.includes('Firefox/');
  return { isWindows, isMac, isEdge, isChrome, isSafari, isFirefox };
}

/** Returns true if the current browser/OS combo offers high-quality TTS. */
function hasPremiumTTS(): boolean {
  const { isWindows, isMac, isEdge, isChrome, isSafari } = detectBrowserOS();
  if (isEdge && isWindows) return true;       // Microsoft Neural voices
  if ((isChrome || isSafari) && isMac) return true; // Apple system voices
  return false;
}

// ── Pick the best available TTS voice ────────────────────────────────────────
function pickVoice(): SpeechSynthesisVoice | null {
  const voices = window.speechSynthesis.getVoices();
  if (!voices.length) return null;
  // Prefer natural/online neural voices (Edge/Windows), then any en-US, then first available.
  const prefer = voices.find((v) => /natural|online/i.test(v.name) && v.lang.startsWith('en'));
  if (prefer) return prefer;
  const enUs = voices.find((v) => v.lang === 'en-US');
  if (enUs) return enUs;
  return voices[0] ?? null;
}

// ── DrillTips ─────────────────────────────────────────────────────────────────

function DrillTips({ autoSpeak, onAutoSpeakChange }: { autoSpeak: boolean; onAutoSpeakChange: (v: boolean) => void }) {
  const { isWindows, isChrome, isFirefox } = detectBrowserOS();
  const premiumTTS = hasPremiumTTS();

  const warnings: { icon: string; text: React.ReactNode }[] = [];

  // Chrome on Windows — robotic TTS, suggest Edge
  if (isChrome && isWindows) {
    warnings.push({
      icon: '🔊',
      text: (
        <>
          <span className="font-medium">Chrome on Windows uses a lower-quality voice</span> for text-to-speech.
          Switch to <span className="font-medium">Microsoft Edge</span> to get natural-sounding Microsoft Neural voices.
        </>
      ),
    });
  }

  // Firefox — no speech-to-text
  if (isFirefox) {
    warnings.push({
      icon: '🎤',
      text: (
        <>
          <span className="font-medium">Firefox does not support voice recording.</span>{' '}
          You can still type your answers. Switch to Chrome or Edge to enable the microphone button.
        </>
      ),
    });
  }

  // Unknown or limited browser/OS — generic advisory
  if (warnings.length === 0 && !premiumTTS) {
    warnings.push({
      icon: '💡',
      text: 'Voice quality and microphone support depend on your browser. Chrome and Edge on desktop offer the best experience.',
    });
  }

  const ttsSubtext = premiumTTS
    ? 'High-quality voice detected — enabled by default.'
    : 'Voice quality may vary on your current browser.';

  return (
    <div className="w-1/2 mx-auto rounded-lg border border-gray-200 bg-white overflow-hidden">
      {/* Warning panel */}
      {warnings.length > 0 && (
        <div className="border-b border-amber-200 bg-amber-50 px-4 py-3 space-y-2">
          {warnings.map((w, i) => (
            <p key={i} className="text-xs text-amber-800 leading-relaxed flex gap-2">
              <span>{w.icon}</span>
              <span>{w.text}</span>
            </p>
          ))}
        </div>
      )}

      {/* TTS auto-play toggle */}
      <label className="flex items-center justify-between gap-3 px-4 py-3 cursor-pointer select-none">
        <div className="flex items-center gap-2.5">
          <SpeakerIcon className="w-4 h-4 text-gray-400 shrink-0" />
          <div>
            <p className="text-sm font-medium text-gray-700">Read questions aloud (text-to-speech)</p>
            <p className="text-xs text-gray-400 mt-0.5">{ttsSubtext}</p>
          </div>
        </div>
        <button
          role="switch"
          aria-checked={autoSpeak}
          onClick={() => onAutoSpeakChange(!autoSpeak)}
          className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 ${autoSpeak ? 'bg-indigo-600' : 'bg-gray-200'}`}
        >
          <span
            className={`inline-block h-4 w-4 rounded-full bg-white shadow transition-transform ${autoSpeak ? 'translate-x-4' : 'translate-x-0.5'}`}
          />
        </button>
      </label>
    </div>
  );
}

// ── Main component ─────────────────────────────────────────────────────────────

export function InterviewDrillTab({ app }: { app: Application }) {
  const { data: plan, isLoading: isPlanLoading } = useInterviewDrillPlan(app.id);
  const generate = useGenerateInterviewDrill(app.id);
  const cancel = useCancelInterviewDrill(app.id);
  const saveAnswer = useSaveInterviewDrillAnswer(app.id);

  // Drill mode state
  const [drillActive, setDrillActive] = useState(false);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [questionsRevealed, setQuestionsRevealed] = useState(false);

  // TTS auto-play toggle — on by default only when premium voices are available
  const [autoSpeak, setAutoSpeak] = useState(() => hasPremiumTTS());

  // Answer + voice state
  const [draftAnswer, setDraftAnswer] = useState('');
  const [isRecording, setIsRecording] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const recognitionRef = useRef<SpeechRecognition | null>(null);
  const speechSupported = typeof window !== 'undefined' && getSpeechRecognition() !== null;

  const isGenerating = app.isInterviewDrillPending || generate.isPending;
  const hasResume = true; // validation happens server-side; the UI simply shows the button
  const hasScoring = app.overallScore != null;

  // Load the saved answer and optionally auto-read the question when index changes.
  useEffect(() => {
    if (!drillActive || !plan) return;
    const sorted = [...plan.questions].sort((a, b) => a.orderIndex - b.orderIndex);
    setDraftAnswer(sorted[currentIndex]?.answer ?? '');
    stopRecording();
    stopSpeaking();
    if (autoSpeak && sorted[currentIndex]) {
      speakQuestion(sorted[currentIndex].text);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentIndex, drillActive]);

  function startDrill() {
    setCurrentIndex(0);
    setDrillActive(true);
  }

  function exitDrill() {
    stopRecording();
    stopSpeaking();
    setDrillActive(false);
  }

  function persistAnswer(orderIndex: number, text: string) {
    saveAnswer.mutate({ orderIndex, answer: text.trim() || null });
  }

  // ── Speech-to-text (mic) ──────────────────────────────────────────────────
  function startRecording() {
    const SR = getSpeechRecognition();
    if (!SR) return;
    const recognition = new SR();
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = 'en-US';

    const base = draftAnswer;
    recognition.onresult = (e) => {
      let interim = '';
      let final = '';
      for (let i = e.resultIndex; i < e.results.length; i++) {
        const t = e.results[i][0].transcript;
        if (e.results[i].isFinal) final += t;
        else interim += t;
      }
      setDraftAnswer(base + (base && (final || interim) ? ' ' : '') + final + interim);
    };
    recognition.onerror = () => setIsRecording(false);
    recognition.onend = () => setIsRecording(false);

    recognitionRef.current = recognition;
    recognition.start();
    setIsRecording(true);
  }

  function stopRecording() {
    recognitionRef.current?.stop();
    recognitionRef.current = null;
    setIsRecording(false);
  }

  // ── Text-to-speech (speaker) ──────────────────────────────────────────────
  function speakQuestion(text: string) {
    if (isSpeaking) { stopSpeaking(); return; }
    const utterance = new SpeechSynthesisUtterance(text);
    // Wait for voices to load if needed (Chrome loads them async).
    const trySpeak = () => {
      const voice = pickVoice();
      if (voice) utterance.voice = voice;
      utterance.rate = 0.95;
      utterance.onend = () => setIsSpeaking(false);
      utterance.onerror = () => setIsSpeaking(false);
      window.speechSynthesis.speak(utterance);
      setIsSpeaking(true);
    };
    if (window.speechSynthesis.getVoices().length > 0) {
      trySpeak();
    } else {
      window.speechSynthesis.addEventListener('voiceschanged', trySpeak, { once: true });
    }
  }

  function stopSpeaking() {
    window.speechSynthesis.cancel();
    setIsSpeaking(false);
  }

  // ── Pending ──────────────────────────────────────────────────────────────────
  if (isGenerating) {
    return (
      <div className="bg-white rounded-lg border border-gray-200 p-8 flex flex-col items-center gap-4">
        <ScoringSpinner />
        <p className="text-sm text-gray-400">Generating interview questions…</p>
        <p className="text-xs text-gray-300">This may take up to a minute.</p>
        {app.isInterviewDrillPending && !generate.isPending && (
          <button
            onClick={() => cancel.mutate()}
            disabled={cancel.isPending}
            className="text-xs text-gray-400 hover:text-gray-600 underline underline-offset-2 disabled:opacity-50"
          >
            {cancel.isPending ? 'Cancelling…' : 'Cancel'}
          </button>
        )}
      </div>
    );
  }

  // ── Error banner ─────────────────────────────────────────────────────────────
  const errorBanner = app.lastInterviewDrillError
    ? <ScoringErrorBanner error={app.lastInterviewDrillError} />
    : null;

  // ── No plan yet ──────────────────────────────────────────────────────────────
  if (!plan && !isPlanLoading) {
    const missingItems: string[] = [];
    if (!hasScoring) missingItems.push('scoring data (Scoring tab)');

    return (
      <div className="space-y-3">
        {errorBanner}
        <div className="bg-white rounded-lg border border-gray-200 p-8 flex flex-col items-center gap-4 text-center">
          <div className="w-12 h-12 rounded-full bg-indigo-50 flex items-center justify-center">
            <SparkleIcon className="w-6 h-6 text-indigo-400" />
          </div>
          <div>
            <p className="text-sm font-medium text-gray-700">No interview drill plan yet</p>
            <p className="text-xs text-gray-400 mt-1 max-w-sm leading-relaxed">
              Generate 15–20 tailored questions to help you prepare for this interview — warm-up, easy, medium, and challenging questions based on your scoring analysis.
            </p>
          </div>
          {missingItems.length > 0 && (
            <p className="text-xs text-amber-600 bg-amber-50 border border-amber-200 rounded-md px-3 py-2">
              Requires: {missingItems.join(', ')}
            </p>
          )}
          <button
            onClick={() => generate.mutate()}
            disabled={!hasScoring || generate.isPending}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <SparkleIcon className="w-4 h-4" />
            Generate Interview Drill
          </button>
        </div>
      </div>
    );
  }

  if (!plan) return null;

  const questions = [...plan.questions].sort((a, b) => a.orderIndex - b.orderIndex);
  const total = questions.length;

  // ── Drill active ─────────────────────────────────────────────────────────────
  if (drillActive) {
    const q = questions[currentIndex];
    const category = q.category as InterviewQuestionCategory;
    const cfg = CATEGORY_CONFIG[category] ?? CATEGORY_CONFIG.warm_up;
    const progressPct = Math.round(((currentIndex + 1) / total) * 100);

    function navigateTo(nextIndex: number) {
      persistAnswer(q.orderIndex, draftAnswer);
      setCurrentIndex(nextIndex);
    }

    return (
      <div className="space-y-6">
        {/* Progress bar */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <span className="text-xs text-gray-400">Question {currentIndex + 1} of {total}</span>
            <button
              onClick={() => { persistAnswer(q.orderIndex, draftAnswer); exitDrill(); }}
              className="text-xs text-gray-400 hover:text-gray-600 underline underline-offset-2"
            >
              Exit Drill
            </button>
          </div>
          <div className="w-full h-2 bg-gray-100 rounded-full overflow-hidden">
            <div
              className="h-2 bg-indigo-500 rounded-full transition-all duration-300"
              style={{ width: `${progressPct}%` }}
            />
          </div>
        </div>

        {/* Question card */}
        <div className="bg-white rounded-lg border border-gray-200 p-8">
          <div className="flex items-center justify-between mb-6">
            <div className="flex items-center gap-2">
              <span className={`inline-block text-xs font-semibold px-2.5 py-1 rounded-full ${cfg.badgeClass}`}>
                {cfg.label}
              </span>
              {q.requirementName && (
                <span className="text-xs text-gray-400 truncate max-w-xs" title={q.requirementName}>
                  {q.requirementName}
                </span>
              )}
            </div>
            {/* Read question aloud */}
            <button
              onClick={() => speakQuestion(q.text)}
              title={isSpeaking ? 'Stop reading' : 'Read question aloud'}
              className={`p-1.5 rounded-md transition-colors ${isSpeaking ? 'text-indigo-600 bg-indigo-50 hover:bg-indigo-100' : 'text-gray-400 hover:text-indigo-500 hover:bg-indigo-50'}`}
            >
              {isSpeaking ? <StopIcon className="w-4 h-4" /> : <SpeakerIcon className="w-4 h-4" />}
            </button>
          </div>
          <p className="text-lg font-medium text-gray-900 leading-relaxed">{q.text}</p>
        </div>

        {/* Answer area */}
        <div className="bg-white rounded-lg border border-gray-200 p-5 space-y-3">
          <div className="flex items-center justify-between">
            <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Your answer</p>
            {speechSupported && (
              <button
                onClick={isRecording ? stopRecording : startRecording}
                title={isRecording ? 'Stop recording' : 'Record answer (speech to text)'}
                className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-medium transition-colors ${
                  isRecording
                    ? 'bg-red-50 text-red-600 hover:bg-red-100 border border-red-200'
                    : 'bg-gray-50 text-gray-600 hover:bg-indigo-50 hover:text-indigo-600 border border-gray-200'
                }`}
              >
                {isRecording ? (
                  <>
                    <span className="inline-block w-2 h-2 rounded-full bg-red-500 animate-pulse" />
                    <StopIcon className="w-3.5 h-3.5" />
                    Stop
                  </>
                ) : (
                  <>
                    <MicIcon className="w-3.5 h-3.5" />
                    Record
                  </>
                )}
              </button>
            )}
          </div>
          <textarea
            value={draftAnswer}
            onChange={(e) => setDraftAnswer(e.target.value)}
            placeholder="Type your answer here, or use the Record button to speak it…"
            rows={5}
            className="w-full resize-none rounded-md border border-gray-200 px-3 py-2.5 text-sm text-gray-800 placeholder-gray-300 focus:outline-none focus:ring-2 focus:ring-indigo-400 focus:border-transparent"
          />
          {saveAnswer.isPending && (
            <p className="text-xs text-gray-400">Saving…</p>
          )}
        </div>

        {/* Navigation */}
        <div className="flex items-center justify-between">
          <button
            onClick={() => navigateTo(Math.max(0, currentIndex - 1))}
            disabled={currentIndex === 0}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
            aria-label="Previous question"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
              <path fillRule="evenodd" d="M11.78 5.22a.75.75 0 0 1 0 1.06L8.06 10l3.72 3.72a.75.75 0 1 1-1.06 1.06l-4.25-4.25a.75.75 0 0 1 0-1.06l4.25-4.25a.75.75 0 0 1 1.06 0Z" clipRule="evenodd" />
            </svg>
            Previous
          </button>

          {currentIndex < total - 1 ? (
            <button
              onClick={() => navigateTo(Math.min(total - 1, currentIndex + 1))}
              className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-indigo-600 text-white text-sm font-medium hover:bg-indigo-700 transition-colors"
              aria-label="Next question"
            >
              Next
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-4 h-4">
                <path fillRule="evenodd" d="M8.22 5.22a.75.75 0 0 1 1.06 0l4.25 4.25a.75.75 0 0 1 0 1.06l-4.25 4.25a.75.75 0 1 1-1.06-1.06L11.94 10 8.22 6.28a.75.75 0 0 1 0-1.06Z" clipRule="evenodd" />
              </svg>
            </button>
          ) : (
            <button
              onClick={() => { persistAnswer(q.orderIndex, draftAnswer); exitDrill(); }}
              className="inline-flex items-center gap-1.5 px-4 py-2 rounded-md bg-green-600 text-white text-sm font-medium hover:bg-green-700 transition-colors"
            >
              Finish
            </button>
          )}
        </div>
      </div>
    );
  }

  // ── Plan overview ─────────────────────────────────────────────────────────────
  const categoryCounts = (Object.keys(CATEGORY_CONFIG) as InterviewQuestionCategory[]).map((cat) => ({
    cat,
    count: questions.filter((q) => q.category === cat).length,
    cfg: CATEGORY_CONFIG[cat],
  })).filter((c) => c.count > 0);

  return (
    <div className="space-y-4">
      {errorBanner}

      {/* Plan summary card */}
      <div className="bg-white rounded-lg border border-gray-200 px-6 py-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <p className="text-sm font-semibold text-gray-900">{total} questions ready</p>
            <p className="text-xs text-gray-400 mt-0.5">
              Generated {new Date(plan.generatedAt).toLocaleDateString()} · {plan.modelUsed}
            </p>
          </div>
          <button
            onClick={() => { generate.mutate(); setDrillActive(false); }}
            disabled={!hasScoring || generate.isPending}
            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-md border border-gray-200 bg-white text-xs font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            <SparkleIcon className="w-3.5 h-3.5" />
            Re-generate
          </button>
        </div>

        {/* Category breakdown bar */}
        <div className="flex rounded-full overflow-hidden h-3 mb-3" aria-label="Question categories">
          {categoryCounts.map(({ cat, count, cfg }) => (
            <div
              key={cat}
              className={cfg.badgeClass.replace('text-', 'bg-').split(' ')[0]}
              style={{ width: `${Math.round((count / total) * 100)}%` }}
              title={`${cfg.label}: ${count}`}
            />
          ))}
        </div>

        {/* Legend */}
        <div className="flex flex-wrap gap-3">
          {categoryCounts.map(({ cat, count, cfg }) => (
            <span key={cat} className="inline-flex items-center gap-1.5 text-xs text-gray-500">
              <span className={`inline-block w-2.5 h-2.5 rounded-sm ${cfg.badgeClass.split(' ')[0]}`} />
              {cfg.label} ({count})
            </span>
          ))}
        </div>
      </div>

      {/* Tips + TTS toggle */}
      <DrillTips autoSpeak={autoSpeak} onAutoSpeakChange={setAutoSpeak} />

      {/* Start button */}
      <div className="flex justify-center">
        <button
          onClick={startDrill}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-md bg-indigo-600 text-white text-sm font-semibold hover:bg-indigo-700 transition-colors shadow-sm"
        >
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-5 h-5">
            <path d="M6.3 2.84A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.27l9.344-5.891a1.5 1.5 0 0 0 0-2.538L6.3 2.841Z" />
          </svg>
          Start Interview Drill
        </button>
      </div>

      {/* Question list preview */}
      <div className="bg-white rounded-lg border border-gray-200 overflow-hidden">
        <div className="px-4 py-2.5 border-b border-gray-100 flex items-center justify-between">
          <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Questions</p>
          <button
            onClick={() => setQuestionsRevealed((v) => !v)}
            className="text-xs text-indigo-500 hover:text-indigo-700 font-medium"
          >
            {questionsRevealed ? 'Hide questions' : 'Reveal questions'}
          </button>
        </div>
        {questionsRevealed && (
          <ol className="divide-y divide-gray-100">
            {questions.map((q) => {
              const category = q.category as InterviewQuestionCategory;
              const cfg = CATEGORY_CONFIG[category] ?? CATEGORY_CONFIG.warm_up;
              return (
                <li key={q.orderIndex} className="flex items-start gap-3 px-4 py-3">
                  <span className="shrink-0 text-xs text-gray-300 w-5 text-right mt-0.5">{q.orderIndex}.</span>
                  <span className={`shrink-0 text-[10px] font-semibold px-1.5 py-0.5 rounded-full mt-0.5 ${cfg.badgeClass}`}>
                    {cfg.label}
                  </span>
                  <span className="text-sm text-gray-700 leading-snug">{q.text}</span>
                </li>
              );
            })}
          </ol>
        )}
      </div>
    </div>
  );
}
