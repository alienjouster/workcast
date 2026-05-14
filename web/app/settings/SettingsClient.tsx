'use client';

import { useEffect, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Card, CardHeader, CardBody } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import {
  useSettings,
  useUpdateSettings,
  useUploadResume,
  useDeleteResume,
  useUploadResumeTemplate,
  useDeleteResumeTemplate,
  useGoogleDriveDisconnect,
  useUpdateGoogleDriveBasePath,
} from '@/lib/hooks/useSettings';
import { notificationsEnabled, setNotificationsEnabled } from '@/lib/hooks/useNotifications';
import { api } from '@/lib/api';

const MODEL_INFO: Record<string, string> = {
  'claude-haiku-4-5-20251001': 'Fastest & cheapest',
  'claude-sonnet-4-5':         'Balanced speed and accuracy — recommended default',
  'claude-sonnet-4-6':         'Latest Sonnet — stronger reasoning, slightly higher cost',
  'claude-opus-4-6':           'Most capable — best for complex or unusual board layouts, highest cost',
};

type ModelField = 'boardAnalyzer' | 'scoring' | 'resumeGeneration' | 'letterGeneration' | 'interviewTrainer' | 'interviewAnswerEvaluation';
type TokenField = 'boardAnalyzerTokens' | 'scoringTokens' | 'resumeGenerationTokens' | 'letterGenerationTokens' | 'interviewTrainerTokens' | 'interviewAnswerEvaluationTokens';
type EditingField = ModelField | TokenField | null;

const TOKEN_OPTIONS = [512, 1024, 2048, 4096, 8192, 16384];

const DEFAULT_MAX_TOKENS: Record<TokenField, number> = {
  boardAnalyzerTokens:              4096,
  scoringTokens:                    4096,
  resumeGenerationTokens:           8192,
  letterGenerationTokens:           2048,
  interviewTrainerTokens:           4096,
  interviewAnswerEvaluationTokens:  1024,
};

const MCP_URL = `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/mcp`;

export function SettingsClient() {
  const queryClient = useQueryClient();
  const { data: settings, isLoading } = useSettings();
  const { mutate: updateSettings, isPending } = useUpdateSettings();
  const { mutate: uploadResume, mutateAsync: uploadResumeAsync, isPending: isUploading, error: uploadError } = useUploadResume();
  const { mutate: deleteResume, isPending: isDeleting } = useDeleteResume();
  const { mutate: uploadTemplate, isPending: isUploadingTemplate, error: uploadTemplateError } = useUploadResumeTemplate();
  const { mutate: deleteTemplate, isPending: isDeletingTemplate } = useDeleteResumeTemplate();
  const { mutate: disconnectDrive, isPending: isDisconnecting } = useGoogleDriveDisconnect();
  const { mutate: updateBasePath, isPending: isUpdatingBasePath } = useUpdateGoogleDriveBasePath();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const templateInputRef = useRef<HTMLInputElement>(null);

  const [driveBasePathDraft, setDriveBasePathDraft] = useState('');
  const [editingBasePath, setEditingBasePath] = useState(false);

  const [editingField, setEditingField] = useState<EditingField>(null);
  const [draft, setDraft] = useState('');
  const [tokenDraft, setTokenDraft] = useState(0);

  const [editingResume, setEditingResume] = useState(false);
  const [resumeDraft, setResumeDraft] = useState('');
  const [resumeJsonError, setResumeJsonError] = useState<string | null>(null);
  const [isFetchingResume, setIsFetchingResume] = useState(false);

  const [copied, setCopied] = useState(false);

  const [notifEnabled, setNotifEnabled] = useState(false);
  const [notifPermission, setNotifPermission] = useState<NotificationPermission>('default');

  useEffect(() => {
    setNotifEnabled(notificationsEnabled());
    if (typeof Notification !== 'undefined') {
      setNotifPermission(Notification.permission);
    }
  }, []);

  async function toggleNotifications() {
    if (notifEnabled) {
      setNotificationsEnabled(false);
      setNotifEnabled(false);
    } else {
      const permission = await Notification.requestPermission();
      setNotifPermission(permission);
      if (permission === 'granted') {
        setNotificationsEnabled(true);
        setNotifEnabled(true);
      }
    }
  }

  function copyMcpUrl() {
    navigator.clipboard.writeText(MCP_URL).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  function startEdit(field: ModelField) {
    const value =
      field === 'boardAnalyzer'             ? settings!.boardAnalyzerModel :
      field === 'scoring'                   ? settings!.scoringModel :
      field === 'resumeGeneration'          ? settings!.resumeGenerationModel :
      field === 'letterGeneration'          ? settings!.letterGenerationModel :
      field === 'interviewTrainer'          ? settings!.interviewTrainerModel :
                                              settings!.interviewAnswerEvaluationModel;
    setDraft(value);
    setEditingField(field);
  }

  function startTokenEdit(field: TokenField) {
    const value =
      field === 'boardAnalyzerTokens'             ? settings!.boardAnalyzerMaxTokens :
      field === 'scoringTokens'                   ? settings!.scoringMaxTokens :
      field === 'resumeGenerationTokens'          ? settings!.resumeGenerationMaxTokens :
      field === 'letterGenerationTokens'          ? settings!.letterGenerationMaxTokens :
      field === 'interviewTrainerTokens'          ? settings!.interviewTrainerMaxTokens :
                                                    settings!.interviewAnswerEvaluationMaxTokens;
    setTokenDraft(value);
    setEditingField(field);
  }

  function cancelEdit() {
    setEditingField(null);
    setDraft('');
    setTokenDraft(0);
  }

  function saveEdit() {
    const boardAnalyzerModel             = editingField === 'boardAnalyzer'            ? draft : settings!.boardAnalyzerModel;
    const scoringModel                   = editingField === 'scoring'                  ? draft : settings!.scoringModel;
    const resumeGenerationModel          = editingField === 'resumeGeneration'         ? draft : settings!.resumeGenerationModel;
    const letterGenerationModel          = editingField === 'letterGeneration'         ? draft : settings!.letterGenerationModel;
    const interviewTrainerModel          = editingField === 'interviewTrainer'         ? draft : settings!.interviewTrainerModel;
    const interviewAnswerEvaluationModel = editingField === 'interviewAnswerEvaluation' ? draft : settings!.interviewAnswerEvaluationModel;
    const boardAnalyzerMaxTokens             = editingField === 'boardAnalyzerTokens'            ? tokenDraft : settings!.boardAnalyzerMaxTokens;
    const scoringMaxTokens                   = editingField === 'scoringTokens'                  ? tokenDraft : settings!.scoringMaxTokens;
    const resumeGenerationMaxTokens          = editingField === 'resumeGenerationTokens'         ? tokenDraft : settings!.resumeGenerationMaxTokens;
    const letterGenerationMaxTokens          = editingField === 'letterGenerationTokens'         ? tokenDraft : settings!.letterGenerationMaxTokens;
    const interviewTrainerMaxTokens          = editingField === 'interviewTrainerTokens'         ? tokenDraft : settings!.interviewTrainerMaxTokens;
    const interviewAnswerEvaluationMaxTokens = editingField === 'interviewAnswerEvaluationTokens' ? tokenDraft : settings!.interviewAnswerEvaluationMaxTokens;
    updateSettings(
      { boardAnalyzerModel, scoringModel, resumeGenerationModel, letterGenerationModel, interviewTrainerModel, interviewAnswerEvaluationModel,
        boardAnalyzerMaxTokens, scoringMaxTokens, resumeGenerationMaxTokens, letterGenerationMaxTokens, interviewTrainerMaxTokens, interviewAnswerEvaluationMaxTokens },
      { onSuccess: () => cancelEdit() }
    );
  }

  function renderModelCell(field: ModelField, currentValue: string | undefined) {
    if (editingField === field) {
      return (
        <>
          <td className="px-4 py-2.5">
            <select
              autoFocus
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="text-sm border border-gray-300 rounded px-2 py-1 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              {settings!.availableModels.map((model) => (
                <option key={model} value={model}>
                  {model}
                </option>
              ))}
            </select>
            {MODEL_INFO[draft] && (
              <p className="mt-1 text-xs text-gray-400">{MODEL_INFO[draft]}</p>
            )}
          </td>
          <td className="px-4 py-2.5 text-right whitespace-nowrap">
            <div className="flex items-center justify-end gap-2">
              <Button size="sm" variant="primary" onClick={saveEdit} loading={isPending}>Save</Button>
              <Button size="sm" variant="secondary" onClick={cancelEdit}>Cancel</Button>
            </div>
          </td>
        </>
      );
    }

    return (
      <>
        <td className="px-4 py-2.5 text-sm text-gray-900">
          {isLoading ? (
            <span className="text-gray-400">Loading…</span>
          ) : (
            <span className="inline-flex items-baseline gap-2">
              <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">{currentValue}</span>
              {currentValue && MODEL_INFO[currentValue] && (
                <span className="text-xs text-gray-400">{MODEL_INFO[currentValue]}</span>
              )}
            </span>
          )}
        </td>
        <td className="px-4 py-2.5 text-right">
          {!isLoading && editingField === null && (
            <button onClick={() => startEdit(field)} className="text-xs text-indigo-500 hover:underline">Edit</button>
          )}
        </td>
      </>
    );
  }

  function renderTokenCell(field: TokenField, currentValue: number | undefined) {
    if (editingField === field) {
      return (
        <>
          <td className="px-4 py-2.5">
            <select
              autoFocus
              value={tokenDraft}
              onChange={(e) => setTokenDraft(Number(e.target.value))}
              className="text-sm border border-gray-300 rounded px-2 py-1 bg-white text-gray-900 focus:outline-none focus:ring-2 focus:ring-indigo-500"
            >
              {TOKEN_OPTIONS.map((v) => (
                <option key={v} value={v}>{v.toLocaleString()} tokens</option>
              ))}
            </select>
          </td>
          <td className="px-4 py-2.5 text-right whitespace-nowrap">
            <div className="flex items-center justify-end gap-2">
              <Button size="sm" variant="primary" onClick={saveEdit} loading={isPending}>Save</Button>
              <Button size="sm" variant="secondary" onClick={cancelEdit}>Cancel</Button>
            </div>
          </td>
        </>
      );
    }

    return (
      <>
        <td className="px-4 py-2.5 text-sm text-gray-900">
          {isLoading ? (
            <span className="text-gray-400">Loading…</span>
          ) : (
            <span className="inline-flex items-baseline gap-2">
              <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">{currentValue?.toLocaleString()}</span>
              {currentValue === DEFAULT_MAX_TOKENS[field] && (
                <span className="text-xs text-gray-400">default</span>
              )}
            </span>
          )}
        </td>
        <td className="px-4 py-2.5 text-right whitespace-nowrap">
          {!isLoading && editingField === null && (
            <div className="flex items-center justify-end gap-3">
              <button onClick={() => startTokenEdit(field)} className="text-xs text-indigo-500 hover:underline">Edit</button>
              {currentValue !== DEFAULT_MAX_TOKENS[field] && (
                <button
                  onClick={() => {
                    const def = DEFAULT_MAX_TOKENS[field];
                    const boardAnalyzerMaxTokens             = field === 'boardAnalyzerTokens'            ? def : settings!.boardAnalyzerMaxTokens;
                    const scoringMaxTokens                   = field === 'scoringTokens'                  ? def : settings!.scoringMaxTokens;
                    const resumeGenerationMaxTokens          = field === 'resumeGenerationTokens'         ? def : settings!.resumeGenerationMaxTokens;
                    const letterGenerationMaxTokens          = field === 'letterGenerationTokens'         ? def : settings!.letterGenerationMaxTokens;
                    const interviewTrainerMaxTokens          = field === 'interviewTrainerTokens'         ? def : settings!.interviewTrainerMaxTokens;
                    const interviewAnswerEvaluationMaxTokens = field === 'interviewAnswerEvaluationTokens' ? def : settings!.interviewAnswerEvaluationMaxTokens;
                    updateSettings({
                      boardAnalyzerModel: settings!.boardAnalyzerModel,
                      scoringModel: settings!.scoringModel,
                      resumeGenerationModel: settings!.resumeGenerationModel,
                      letterGenerationModel: settings!.letterGenerationModel,
                      interviewTrainerModel: settings!.interviewTrainerModel,
                      interviewAnswerEvaluationModel: settings!.interviewAnswerEvaluationModel,
                      boardAnalyzerMaxTokens,
                      scoringMaxTokens,
                      resumeGenerationMaxTokens,
                      letterGenerationMaxTokens,
                      interviewTrainerMaxTokens,
                      interviewAnswerEvaluationMaxTokens,
                    });
                  }}
                  className="text-xs text-gray-400 hover:text-gray-600 hover:underline"
                >
                  Restore default
                </button>
              )}
            </div>
          )}
        </td>
      </>
    );
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold text-gray-900">Settings</h1>

      {/* Resume */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Resume</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              {/* Content row */}
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Content</td>
                <td className="px-4 py-2.5">
                  <p className="text-xs text-gray-400 mb-1.5">
                    JSON format works best. The AI uses this as the source of truth for all resume and cover letter generation. Freely editable — add, remove, or reword any section but try to keep the structure and naming that is matching the Resume HTML template. {' '}
                    <a
                      href="/master-resume-sample.json"
                      download="master-resume-sample.json"
                      className="text-indigo-500 hover:underline"
                    >
                      Download sample
                    </a>
                  </p>
                  {isLoading ? (
                    <span className="text-gray-400">Loading…</span>
                  ) : settings?.hasResume ? (
                    <span className="inline-flex items-center gap-2">
                      <span className="text-sm text-gray-900">{settings.resumeFileName}</span>
                      {settings.resumeUploadedAt && (
                        <span className="text-xs text-gray-400">
                          uploaded {new Date(settings.resumeUploadedAt).toLocaleString()}
                        </span>
                      )}
                      {!editingResume && !settings.resumeFileName?.toLowerCase().endsWith('.pdf') && (
                        <button
                          className="text-gray-400 hover:text-indigo-600 transition-colors disabled:opacity-40"
                          disabled={isFetchingResume}
                          onClick={async () => {
                            setIsFetchingResume(true);
                            try {
                              const content = await api.settings.getResumeContent();
                              setResumeDraft(content);
                              setResumeJsonError(null);
                              setEditingResume(true);
                            } finally {
                              setIsFetchingResume(false);
                            }
                          }}
                        >
                          {isFetchingResume ? (
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" className="w-3.5 h-3.5 animate-spin">
                              <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2.5" strokeOpacity="0.2" />
                              <path d="M12 3a9 9 0 0 1 9 9" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" />
                            </svg>
                          ) : (
                            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className="w-3.5 h-3.5">
                              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                            </svg>
                          )}
                        </button>
                      )}
                    </span>
                  ) : (
                    <span className="text-sm text-gray-400 italic">No file uploaded</span>
                  )}
                  {uploadError && (
                    <p className="text-xs text-red-600 mt-1">{(uploadError as Error).message}</p>
                  )}
                </td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept=".pdf,.txt,.json,application/pdf,text/plain,application/json"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) uploadResume(file);
                      e.target.value = '';
                    }}
                  />
                  <div className="flex items-center justify-end gap-2">
                    {!editingResume && (
                      <>
                        <Button size="sm" variant="secondary" onClick={() => fileInputRef.current?.click()} loading={isUploading}>
                          {settings?.hasResume ? 'Replace' : 'Upload'}
                        </Button>
                        {settings?.hasResume && (
                          <Button size="sm" variant="ghost" onClick={() => deleteResume()} loading={isDeleting} className="text-red-500 hover:text-red-700">
                            Remove
                          </Button>
                        )}
                      </>
                    )}
                  </div>
                </td>
              </tr>
              {editingResume && (
                <tr className="border-t border-gray-100 bg-gray-50">
                  <td colSpan={3} className="px-4 py-3">
                    <textarea
                      className="w-full h-96 font-mono text-xs border border-gray-200 rounded p-2 focus:outline-none focus:ring-2 focus:ring-indigo-400 resize-y"
                      value={resumeDraft}
                      onChange={(e) => {
                        setResumeDraft(e.target.value);
                        setResumeJsonError(null);
                      }}
                      spellCheck={false}
                    />
                    {resumeJsonError && (
                      <p className="text-xs text-red-600 mt-1">{resumeJsonError}</p>
                    )}
                    <div className="flex justify-end gap-2 mt-2">
                      <Button size="sm" variant="ghost" onClick={() => { setEditingResume(false); setResumeJsonError(null); }}>
                        Cancel
                      </Button>
                      <Button size="sm" variant="primary" loading={isUploading}
                        onClick={async () => {
                          const isJson = settings?.resumeFileName?.toLowerCase().endsWith('.json');
                          if (isJson) {
                            try { JSON.parse(resumeDraft); } catch {
                              setResumeJsonError('Invalid JSON — fix the syntax before saving.');
                              return;
                            }
                          }
                          const mimeType = isJson ? 'application/json' : 'text/plain';
                          const file = new File([resumeDraft], settings!.resumeFileName!, { type: mimeType });
                          await uploadResumeAsync(file);
                          setEditingResume(false);
                          setResumeJsonError(null);
                        }}>
                        Save
                      </Button>
                    </div>
                  </td>
                </tr>
              )}
              {/* Template row */}
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Template</td>
                <td className="px-4 py-2.5">
                  <p className="text-xs text-gray-400 mb-1.5">
                    HTML format. Best results when sections mirror the JSON structure — the AI fills in the tags from your resume content.{' '}
                    <a
                      href="/resume-template-sample.html"
                      download="resume-template-sample.html"
                      className="text-indigo-500 hover:underline"
                    >
                      Download sample
                    </a>
                  </p>
                  {isLoading ? (
                    <span className="text-gray-400">Loading…</span>
                  ) : settings?.hasResumeTemplate ? (
                    <span className="inline-flex items-baseline gap-2">
                      <span className="text-sm text-gray-900">{settings.resumeTemplateFileName}</span>
                      {settings.resumeTemplateUploadedAt && (
                        <span className="text-xs text-gray-400">
                          uploaded {new Date(settings.resumeTemplateUploadedAt).toLocaleString()}
                        </span>
                      )}
                    </span>
                  ) : (
                    <span className="text-sm text-gray-400 italic">No file uploaded</span>
                  )}
                  {uploadTemplateError && (
                    <p className="text-xs text-red-600 mt-1">{(uploadTemplateError as Error).message}</p>
                  )}
                </td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  <input
                    ref={templateInputRef}
                    type="file"
                    accept=".html,.htm,text/html"
                    className="hidden"
                    onChange={(e) => {
                      const file = e.target.files?.[0];
                      if (file) uploadTemplate(file);
                      e.target.value = '';
                    }}
                  />
                  <div className="flex items-center justify-end gap-2">
                    <Button size="sm" variant="secondary" onClick={() => templateInputRef.current?.click()} loading={isUploadingTemplate}>
                      {settings?.hasResumeTemplate ? 'Replace' : 'Upload'}
                    </Button>
                    {settings?.hasResumeTemplate && (
                      <Button size="sm" variant="ghost" onClick={() => deleteTemplate()} loading={isDeletingTemplate} className="text-red-500 hover:text-red-700">
                        Remove
                      </Button>
                    )}
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>

      {/* AI section */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">AI</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Board analyzer model</td>
                {renderModelCell('boardAnalyzer', settings?.boardAnalyzerModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-400 w-48 pl-8">Max tokens</td>
                {renderTokenCell('boardAnalyzerTokens', settings?.boardAnalyzerMaxTokens)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Scoring model</td>
                {renderModelCell('scoring', settings?.scoringModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-400 w-48 pl-8">Max tokens</td>
                {renderTokenCell('scoringTokens', settings?.scoringMaxTokens)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Resume generation model</td>
                {renderModelCell('resumeGeneration', settings?.resumeGenerationModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-400 w-48 pl-8">Max tokens</td>
                {renderTokenCell('resumeGenerationTokens', settings?.resumeGenerationMaxTokens)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Letter generation model</td>
                {renderModelCell('letterGeneration', settings?.letterGenerationModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-400 w-48 pl-8">Max tokens</td>
                {renderTokenCell('letterGenerationTokens', settings?.letterGenerationMaxTokens)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Interview trainer model</td>
                {renderModelCell('interviewTrainer', settings?.interviewTrainerModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-400 w-48 pl-8">Max tokens</td>
                {renderTokenCell('interviewTrainerTokens', settings?.interviewTrainerMaxTokens)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Answer evaluation model</td>
                {renderModelCell('interviewAnswerEvaluation', settings?.interviewAnswerEvaluationModel)}
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-400 w-48 pl-8">Max tokens</td>
                {renderTokenCell('interviewAnswerEvaluationTokens', settings?.interviewAnswerEvaluationMaxTokens)}
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>

      {/* MCP Server */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">MCP Server</h2>
        </CardHeader>
        <CardBody>
          <p className="text-sm text-gray-500 mb-4">
            Connect Claude Code or Claude Desktop to your Workcast data and ask natural-language questions about your job search — <em>"Which unread ads have a score above 70?"</em>, <em>"Summarise my application pipeline"</em>, or <em>"Trigger a scrape on my Stack Overflow board"</em>. Claude reads your boards, ads, applications, and scoring results in real time and can act on them directly.
          </p>
          <div className="flex items-center bg-gray-100 rounded border border-gray-200">
            <code className="flex-1 text-xs font-mono px-3 py-2 text-gray-800 select-all">
              {MCP_URL}
            </code>
            <div className="relative shrink-0">
              <button
                onClick={copyMcpUrl}
                title="Copy URL"
                className="flex items-center justify-center px-2.5 py-2 text-gray-400 hover:text-gray-700 hover:bg-gray-200 transition-colors border-l border-gray-200 rounded-r"
              >
                {copied ? (
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5 text-green-500">
                    <path fillRule="evenodd" d="M12.416 3.376a.75.75 0 0 1 .208 1.04l-5 7.5a.75.75 0 0 1-1.154.114l-3-3a.75.75 0 0 1 1.06-1.06l2.353 2.353 4.493-6.74a.75.75 0 0 1 1.04-.207Z" clipRule="evenodd" />
                  </svg>
                ) : (
                  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
                    <path d="M3.5 2A1.5 1.5 0 0 0 2 3.5v9A1.5 1.5 0 0 0 3.5 14h6a1.5 1.5 0 0 0 1.5-1.5v-7A1.5 1.5 0 0 0 9.5 4H9V3.5A1.5 1.5 0 0 0 7.5 2h-4Z" />
                    <path d="M6.5 6A1.5 1.5 0 0 0 5 7.5v5A1.5 1.5 0 0 0 6.5 14H12a1.5 1.5 0 0 0 1.5-1.5v-5A1.5 1.5 0 0 0 12 6H6.5Z" />
                  </svg>
                )}
              </button>
              {copied && (
                <div className="absolute bottom-full right-0 mb-1.5 px-2 py-1 rounded bg-gray-800 text-white text-xs whitespace-nowrap pointer-events-none">
                  Copied to clipboard
                  <span className="absolute top-full right-3 border-4 border-transparent border-t-gray-800" />
                </div>
              )}
            </div>
          </div>
          <p className="mt-2 text-xs text-gray-400">
            Use this URL when adding Workcast as an MCP server in Claude Code (<code className="bg-gray-100 px-1 rounded">.mcp.json</code>) or Claude Desktop (<code className="bg-gray-100 px-1 rounded">claude_desktop_config.json</code>).
          </p>
        </CardBody>
      </Card>

      {/* Notifications */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Notifications</h2>
        </CardHeader>
        <CardBody>
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-sm font-medium text-gray-800">Enable desktop notifications</p>
              <p className="text-sm text-gray-500 mt-0.5">Get notified when new job ads are scraped, even when the tab is not focused.</p>
            </div>
            <button
              onClick={toggleNotifications}
              disabled={notifPermission === 'denied'}
              title={notifPermission === 'denied' ? 'Notifications blocked by browser' : undefined}
              className={`relative shrink-0 inline-flex h-6 w-11 items-center rounded-full transition-colors focus:outline-none ${
                notifEnabled ? 'bg-indigo-600' : 'bg-gray-200'
              } ${notifPermission === 'denied' ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer'}`}
            >
              <span
                className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
                  notifEnabled ? 'translate-x-6' : 'translate-x-1'
                }`}
              />
            </button>
          </div>
          {notifPermission === 'denied' && (
            <p className="mt-3 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded px-3 py-2">
              Notifications are blocked by your browser. Allow them in your browser&apos;s site settings to enable this feature.
            </p>
          )}
        </CardBody>
      </Card>

      {/* Google Drive */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Google Drive</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Connection</td>
                <td className="px-4 py-2.5">
                  {isLoading ? (
                    <span className="text-gray-400">Loading…</span>
                  ) : settings?.isGoogleDriveConnected ? (
                    <span className="inline-flex items-center gap-1.5 text-sm text-green-700">
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" className="w-4 h-4">
                        <path fillRule="evenodd" d="M2.25 12c0-5.385 4.365-9.75 9.75-9.75s9.75 4.365 9.75 9.75-4.365 9.75-9.75 9.75S2.25 17.385 2.25 12zm13.36-1.814a.75.75 0 10-1.22-.872l-3.236 4.53L9.53 12.22a.75.75 0 00-1.06 1.06l2.25 2.25a.75.75 0 001.14-.094l3.75-5.25z" clipRule="evenodd" />
                      </svg>
                      Connected
                    </span>
                  ) : (
                    <span className="text-sm text-gray-400 italic">Not connected</span>
                  )}
                </td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  {!isLoading && (settings?.isGoogleDriveConnected ? (
                    <Button size="sm" variant="ghost" loading={isDisconnecting}
                      className="text-red-500 hover:text-red-700"
                      onClick={() => disconnectDrive()}>
                      Disconnect
                    </Button>
                  ) : (
                    <Button size="sm" variant="secondary"
                      onClick={async () => {
                        const { url } = await api.googleDrive.getAuthUrl();
                        const w = 600, h = 700;
                        window.open(
                          url,
                          'google-drive-auth',
                          `width=${w},height=${h},left=${Math.round(window.screenX + (window.outerWidth - w) / 2)},top=${Math.round(window.screenY + (window.outerHeight - h) / 2)},resizable=yes,scrollbars=yes`
                        );
                        const handler = (e: MessageEvent) => {
                          if (e.origin !== window.location.origin) return;
                          if (e.data?.type === 'google-drive-connected')
                            queryClient.invalidateQueries({ queryKey: ['settings'] });
                          window.removeEventListener('message', handler);
                        };
                        window.addEventListener('message', handler);
                      }}>
                      Connect
                    </Button>
                  ))}
                </td>
              </tr>
              <tr className="border-t border-gray-100 hover:bg-gray-50">
                <td className="px-4 py-2.5 text-sm text-gray-500 w-48">Path</td>
                <td className="px-4 py-2.5">
                  {isLoading ? (
                    <span className="text-gray-400">Loading…</span>
                  ) : editingBasePath ? (
                    <input
                      autoFocus
                      type="text"
                      value={driveBasePathDraft}
                      onChange={(e) => setDriveBasePathDraft(e.target.value)}
                      className="text-sm border border-gray-300 rounded px-2 py-1 w-64 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                      placeholder="jobs"
                    />
                  ) : (
                    <span className="font-mono text-xs bg-gray-100 px-2 py-1 rounded">
                      {settings?.googleDriveBasePath ?? 'jobs'}
                    </span>
                  )}
                </td>
                <td className="px-4 py-2.5 text-right whitespace-nowrap">
                  {!isLoading && (editingBasePath ? (
                    <div className="flex items-center justify-end gap-2">
                      <Button size="sm" variant="primary" loading={isUpdatingBasePath}
                        onClick={() => updateBasePath(driveBasePathDraft || 'jobs', { onSuccess: () => setEditingBasePath(false) })}>
                        Save
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setEditingBasePath(false)}>Cancel</Button>
                    </div>
                  ) : (
                    <button
                      onClick={() => { setDriveBasePathDraft(settings?.googleDriveBasePath ?? 'jobs'); setEditingBasePath(true); }}
                      className="text-xs text-indigo-500 hover:underline">
                      Edit
                    </button>
                  ))}
                </td>
              </tr>
            </tbody>
          </table>
        </CardBody>
      </Card>

      {/* Debug & Tools section */}
      <Card>
        <CardHeader>
          <h2 className="font-semibold text-gray-900">Debug &amp; Tools</h2>
        </CardHeader>
        <CardBody className="p-0">
          <table className="min-w-full text-sm">
            <tbody>
              {[
                {
                  label: 'Grafana',
                  description: 'Backend metrics dashboards — HTTP, jobs, .NET process, PostgreSQL',
                  href: process.env.NEXT_PUBLIC_GRAFANA_URL || 'http://localhost:3001',
                },
                {
                  label: 'Prometheus',
                  description: 'Raw metrics explorer and scrape target status',
                  href: process.env.NEXT_PUBLIC_PROMETHEUS_URL || 'http://localhost:9090',
                },
                {
                  label: 'Swagger',
                  description: 'Interactive REST API documentation and request tester',
                  href: `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/swagger`,
                },
                {
                  label: 'Hangfire',
                  description: 'Background job queue monitoring and management',
                  href: `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/hangfire`,
                },
              ].map(({ label, description, href }) => (
                <tr key={label} className="border-t border-gray-100 hover:bg-gray-50">
                  <td className="px-4 py-2.5 text-sm text-gray-500 w-48">{label}</td>
                  <td className="px-4 py-2.5 text-sm text-gray-900">{description}</td>
                  <td className="px-4 py-2.5 text-right whitespace-nowrap">
                    {href ? (
                      <a
                        href={href}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1.5 text-xs text-indigo-500 hover:underline"
                      >
                        Open
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16" fill="currentColor" className="w-3.5 h-3.5">
                          <path d="M6.22 8.72a.75.75 0 0 0 1.06 1.06l5.22-5.22v1.69a.75.75 0 0 0 1.5 0v-3.5a.75.75 0 0 0-.75-.75h-3.5a.75.75 0 0 0 0 1.5h1.69L6.22 8.72Z"/>
                          <path d="M3.5 6.75c0-.69.56-1.25 1.25-1.25H7A.75.75 0 0 0 7 4H4.75A2.75 2.75 0 0 0 2 6.75v4.5A2.75 2.75 0 0 0 4.75 14h4.5A2.75 2.75 0 0 0 12 11.25V9a.75.75 0 0 0-1.5 0v2.25c0 .69-.56 1.25-1.25 1.25h-4.5c-.69 0-1.25-.56-1.25-1.25v-4.5Z"/>
                        </svg>
                      </a>
                    ) : (
                      <span className="text-xs text-gray-300">Not configured</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CardBody>
      </Card>
    </div>
  );
}
