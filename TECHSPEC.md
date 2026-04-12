**Workcast Platform**

Technical Specification Document

|             |                                |
|-------------|--------------------------------|
| **Version** | 1.5                            |
| **Status**  | Updated — Post-Implementation  |
| **Date**    | April 12, 2026                 |
| **Author**  | Solutions Architecture         |

*CONFIDENTIAL — INTERNAL USE ONLY*

> **Changelog — v1.5 (2026-04-12)**
> Added community board sharing feature: import/export endpoints (section 6.2.1), `BoardExchangeDto` / `ScraperConfigExchangeDto` exchange DTOs, `/community-boards/` folder convention, and associated frontend UI (multi-file + multi-URL import queue, export button).

> **Changelog — v1.4 (2026-04-10)**
> Added InterviewStep entity (section 3.10), interview steps API endpoints (section 6.6.1), and Phase 12 implementation notes.
> Section renumbering: ScraperConfig JSON Schema → 3.12, Indexes → 3.13. Interview Drill Endpoints → 6.6.2.

> **Changelog — v1.3 (2026-04-03)**
> Gap analysis pass to align specification with code changes since v1.2 (2026-03-25).
> Major additions: GeneratedResume entity, GeneratedLetter entity, ApplicationStatus workflow,
> resume & letter generation AI operations and endpoints, resume template upload, per-operation
> MaxTokens configuration, Prometheus + Grafana monitoring stack, Playwright stealth hardening,
> manual job ad creation and editing, expanded bulk actions, expanded SSE event types.
> Major removals: Note field on JobAd, ApplicationId FK on AdScoring (scoring data is now
> denormalized onto Application at creation time), unread-count merged into /api/status.
> Data model corrections: AppSettings Id is INT (not UUID), AdScoring.OverallScore is FLOAT,
> Requirements schema updated, Application entity greatly expanded.

> **Changelog — v1.2 (2026-03-25)**
> Gap analysis pass to align specification with implemented code.
> Major additions: AdScoring entity, Application entity, AppSettings entity, ad management
> endpoints (pin/read/trash/note/bulk), applications API, scoring API, settings API, SSE
> real-time events, three new background jobs, updated frontend structure.

**Table of Contents**

**1. Introduction**

**1.1 Purpose**

This document defines the complete technical specification for the Workcast Platform — an AI-powered job board aggregation system. It serves as the authoritative reference for the engineering team during implementation and provides enough detail to begin development without further architectural input.

**1.2 Project Overview**

Workcast allows users to register any job board URL. The platform automatically analyzes the target website using AI, generates a scraping configuration, and then scrapes job advertisements on a configurable schedule. No manual selector configuration is required from the user.

Beyond scraping, the platform supports a full job-search workflow: AI-powered relevance scoring of ads against a user resume, application tracking with status history, AI-generated tailored resumes and application letters, and a read/trash lifecycle for managing the ad inbox.

**Core value proposition:** A user provides a URL. The system handles everything else.

**1.3 Scope**

This specification covers:

- REST API backend built with .NET 10

- AI-powered scraping engine using Claude API (Anthropic)

- AI-powered job ad scoring engine using Claude API (Anthropic)

- AI-powered resume and application letter generation using Claude API (Anthropic)

- JavaScript-rendering layer using Microsoft Playwright

- Background job processing with Hangfire

- Next.js frontend application

- Full local Docker Compose deployment with persistent external volumes

- Performance monitoring via Prometheus and Grafana

Out of scope for v1:

- User authentication and multi-tenancy

- Redis / distributed caching

- Email or webhook alerting

- Mobile application

**1.4 Definitions**

|                |                                                                                     |
|----------------|-------------------------------------------------------------------------------------|
| **Term**       | **Definition**                                                                      |
| Job Board      | A website that lists job advertisements, registered by the user via URL             |
| Scrape Run     | A single execution of the scraping pipeline for one job board                       |
| Board Analysis | The one-time AI process that generates a scraper config from a job board URL        |
| Scraper Config | A structured JSON document describing how to navigate and extract data from a board |
| Ad Scoring     | The AI process that evaluates a job ad's fit against the user's resume              |
| Application    | A user-created record tracking their application to a specific job ad               |
| AI Provider    | An abstracted service that handles all communication with an LLM API                |
| Hangfire       | An embedded .NET background job framework used for scheduling and queuing           |
| Playwright     | A .NET browser automation library used to render JavaScript-heavy pages             |
| Deduplication  | The process of preventing the same job ad from being stored more than once          |
| SSE            | Server-Sent Events — used for real-time push notifications from API to frontend     |

**2. System Architecture**

**2.1 High-Level Overview**

The platform is composed of six Docker services communicating over a private network, with data persisted to external volumes mounted on the host machine.

```
┌────────────────────────────────────────────────────────────────────┐
│                          Docker Network                            │
│                                                                    │
│  ┌──────────────┐    ┌─────────────────────────────────────────┐   │
│  │   Next.js    │───▶│             .NET 10 API                 │   │
│  │  (port 3000) │    │             (port 8080)                 │   │
│  └──────────────┘    │  ┌───────────────────────────────────┐  │   │
│       SSE ◀──────────┤  │   Hangfire Scheduler              │  │   │
│                      │  │   Playwright Engine (stealth)     │  │   │
│                      │  │   AI Extraction / Scoring Service │  │   │
│                      │  │   Resume / Letter Generation      │  │   │
│                      │  │   HangfireMetricsService          │  │   │
│                      │  └───────────────────────────────────┘  │   │
│                      └──────────────┬───────────────────────────┘   │
│                                     │                              │
│                      ┌──────────────▼───────────────────────────┐   │
│                      │           PostgreSQL 16                  │   │
│                      │            (port 5432)                   │   │
│                      └──────────────────────────────────────────┘   │
│                                                                    │
│  ┌──────────────────┐   ┌────────────────┐   ┌────────────────┐   │
│  │ postgres-exporter│──▶│  Prometheus    │──▶│    Grafana     │   │
│  │                  │   │  (port 9090)   │   │  (port 3001)   │   │
│  └──────────────────┘   └────────────────┘   └────────────────┘   │
└────────────────────────────────────────────────────────────────────┘
                              │
              External Volumes (host filesystem)
              /volumes/postgres, /volumes/prometheus, /volumes/grafana
```

**2.2 Technology Stack**

|                    |                                                |
|--------------------|------------------------------------------------|
| **Component**      | **Technology & Version**                       |
| Backend API        | .NET 10 — ASP.NET Core Web API                 |
| ORM                | Entity Framework Core 8 + Npgsql provider      |
| Database           | PostgreSQL 16                                  |
| Browser Automation | Microsoft.Playwright for .NET                  |
| Background Jobs    | Hangfire 1.8 (in-process, PostgreSQL storage)  |
| AI Provider        | Anthropic Claude API (configurable model)      |
| Frontend           | Next.js 14 (App Router)                        |
| Frontend State     | TanStack Query (React Query v5)                |
| Containerisation   | Docker + Docker Compose v2                     |
| API Documentation  | Swashbuckle / Swagger UI                       |
| Real-Time Events   | Server-Sent Events (SSE)                       |
| Metrics            | Prometheus + Grafana (provisioned dashboards)  |

**2.3 Solution Structure**

The backend follows Clean Architecture with four projects. The frontend is a separate Next.js application.

```
Workcast.sln
├── src/
│ ├── Workcast.Core/          # Domain layer — entities, enums, value objects, interfaces, events
│ ├── Workcast.Infrastructure/ # Infrastructure layer — EF Core, Playwright, Claude AI, Hangfire, SSE, Metrics
│ ├── Workcast.Jobs/          # Background jobs — board analysis, scraping, scoring, resume/letter generation, cleanup
│ └── Workcast.Api/           # Presentation layer — controllers, DTOs, mapping, Program.cs
│
├── web/                      # Next.js 14 frontend (App Router, TanStack Query, Tailwind)
│
└── docker/                   # Docker Compose, Dockerfiles, .env.example
```

**3. Data Model**

**3.1 Entity Relationship Overview**

```
AppSettings (singleton, Id = 1)

JobBoard 1 ──────< ScrapeRun
         1 ──────< JobAd
ScrapeRun 1 ──────< JobAd (optional FK, tracks which run discovered each ad)
JobAd     1 ──────< AdScoring (optional, one-per-ad, replaced on re-score)

Application (self-contained; optionally linked to JobAd via nullable FK)
Application 1 ──────< GeneratedResume (versioned)
Application 1 ──────< GeneratedLetter (versioned)
Application 1 ──────< InterviewStep (ordered, user-managed)
```

Note: `AdScoring` is no longer linked to `Application`. Scoring data is denormalized onto `Application` at creation time and can be refreshed via a dedicated scoring endpoint.

**3.2 JobBoard**

|               |                                                                         |
|---------------|-------------------------------------------------------------------------|
| **Column**    | **Type / Constraints / Notes**                                          |
| Id            | UUID — Primary Key, generated by DB                                     |
| Name          | VARCHAR(255) — Nullable, auto-populated from page title if not provided |
| Url           | VARCHAR(2048) — NOT NULL, the seed URL provided by the user             |
| ScraperConfig | JSONB — Nullable, populated after board analysis completes              |
| ScheduleCron  | VARCHAR(100) — NOT NULL, default "0 * * * *" (every hour, on the hour) |
| Status        | VARCHAR(50) — ENUM: pending \| active \| paused \| error                |
| LastScrapedAt | TIMESTAMPTZ — Nullable                                                  |
| CreatedAt     | TIMESTAMPTZ — NOT NULL, default NOW()                                   |
| UpdatedAt     | TIMESTAMPTZ — NOT NULL, updated via EF Core interceptor                 |

**3.3 JobAd**

|                   |                                                                  |
|-------------------|------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                   |
| Id                | UUID — Primary Key                                               |
| JobBoardId        | UUID — FK → JobBoard.Id, CASCADE DELETE (**nullable** for manual ads) |
| ScrapeRunId       | UUID — FK → ScrapeRun.Id, SET NULL (nullable)                    |
| ExternalId        | VARCHAR(512) — Nullable, board-specific identifier for dedup     |
| Url               | VARCHAR(2048) — NOT NULL                                         |
| Title             | VARCHAR(512) — Nullable                                          |
| Company           | VARCHAR(255) — Nullable                                          |
| Location          | VARCHAR(255) — Nullable                                          |
| SalaryRaw         | VARCHAR(255) — Nullable, stored as-is from listing page          |
| Description       | TEXT — Nullable, short snippet if visible on listing page        |
| PostedAt          | TIMESTAMPTZ — Nullable                                           |
| ScrapedAt         | TIMESTAMPTZ — NOT NULL, default NOW()                            |
| IsActive          | BOOLEAN — default TRUE, set FALSE if ad disappears               |
| IsRead            | BOOLEAN — default FALSE, set TRUE when user views the ad         |
| IsPinned          | BOOLEAN — default FALSE, manually set by user                    |
| IsTrashed         | BOOLEAN — default FALSE, soft-delete flag                        |
| IsScoringPending  | BOOLEAN — default FALSE, TRUE while AdScoringJob is running      |
| IsManual          | BOOLEAN — default FALSE, TRUE for user-created ads not from scraping |
| LastScoringError  | TEXT — Nullable, error from last failed scoring attempt          |

Note: The `Note` field was removed. Manual ads (`IsManual = true`) have null `JobBoardId` and null `ScrapeRunId`.

**3.4 ScrapeRun**

|              |                                                               |
|--------------|---------------------------------------------------------------|
| **Column**   | **Type / Constraints / Notes**                                |
| Id           | UUID — Primary Key                                            |
| JobBoardId   | UUID — FK → JobBoard.Id, CASCADE DELETE                       |
| TriggeredBy  | VARCHAR(50) — ENUM: scheduler \| manual                       |
| StartedAt    | TIMESTAMPTZ — NOT NULL                                        |
| FinishedAt   | TIMESTAMPTZ — Nullable                                        |
| Status       | VARCHAR(50) — ENUM: running \| completed \| failed \| partial |
| PagesScraped | INTEGER — default 0                                           |
| AdsFound     | INTEGER — default 0                                           |
| AdsNew       | INTEGER — default 0                                           |
| Errors       | JSONB — Array of error objects: { page, message, timestamp }  |

**3.5 AdScoring**

AI-generated relevance score for a single job ad, computed on demand against the user's uploaded resume. One record per job ad; re-scoring replaces the existing record.

|                   |                                                                          |
|-------------------|--------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                           |
| Id                | UUID — Primary Key                                                       |
| JobAdId           | UUID — FK → JobAd.Id, CASCADE DELETE (NOT NULL)                          |
| ScoredAt          | TIMESTAMPTZ — NOT NULL, timestamp of scoring                             |
| OverallScore      | FLOAT — 0–100, AI-computed relevance score (average of per-requirement scores) |
| Summary           | TEXT — AI-generated summary of fit                                       |
| Recommendation    | TEXT — AI-generated actionable recommendation                            |
| Requirements      | JSONB — Array of ScoringRequirement: { Name, Category, IsOptional, Score, Notes } |

Note: `AdScoring` is no longer linked to `Application`. Scoring data for an application is denormalized into the `Application` row at creation time and refreshed via a dedicated endpoint.

**3.6 Application**

Self-contained record tracking the user's application to a job. All relevant job ad and scoring data is copied at creation time so the record remains complete even if the source ad is deleted.

|                             |                                                                           |
|-----------------------------|---------------------------------------------------------------------------|
| **Column**                  | **Type / Constraints / Notes**                                            |
| Id                          | UUID — Primary Key                                                        |
| JobAdId                     | UUID — FK → JobAd.Id, SET NULL (nullable) — source ad reference           |
| CreatedAt                   | TIMESTAMPTZ — NOT NULL, default NOW()                                     |
| IsTrashed                   | BOOLEAN — default FALSE, soft-delete flag                                 |
| Status                      | VARCHAR(50) — ApplicationStatus enum (see 3.6.1)                          |
| StatusHistory               | JSONB — ordered list of { Status, AchievedAt } entries                    |
| Url                         | VARCHAR(2048) — copied from JobAd                                         |
| Title                       | VARCHAR(512) — Nullable, copied from JobAd                                |
| Company                     | VARCHAR(255) — Nullable, copied from JobAd                                |
| Location                    | VARCHAR(255) — Nullable, copied from JobAd                                |
| SalaryRaw                   | VARCHAR(255) — Nullable, copied from JobAd                                |
| Description                 | TEXT — Nullable, copied from JobAd                                        |
| PostedAt                    | TIMESTAMPTZ — Nullable, copied from JobAd                                 |
| ScrapedAt                   | TIMESTAMPTZ — NOT NULL, copied from JobAd                                 |
| ExternalId                  | VARCHAR(512) — Nullable, copied from JobAd                                |
| OverallScore                | FLOAT — Nullable, copied from AdScoring at creation (or updated later)    |
| ScoredAt                    | TIMESTAMPTZ — Nullable, timestamp of the scoring snapshot                 |
| Summary                     | TEXT — Nullable, AI scoring summary copied from AdScoring                 |
| Recommendation              | TEXT — Nullable, AI scoring recommendation copied from AdScoring          |
| Requirements                | JSONB — scoring breakdown, empty array if no scoring existed              |
| JobAdContent                | TEXT — Nullable, full plain-text content of the job ad page               |
| IsScoringPending            | BOOLEAN — TRUE while an ApplicationScoringJob is in progress              |
| LastScoringError            | TEXT — Nullable, error from last failed scoring attempt                   |
| IsResumeGenerationPending   | BOOLEAN — TRUE while an ApplicationResumeGenerationJob is in progress     |
| LastResumeGenerationError   | TEXT — Nullable, error from last failed resume generation attempt         |
| IsLetterGenerationPending   | BOOLEAN — TRUE while an ApplicationLetterGenerationJob is in progress     |
| LastLetterGenerationError   | TEXT — Nullable, error from last failed letter generation attempt         |

**3.6.1 ApplicationStatus Enum**

| **Value**        | **Meaning**                                              |
|------------------|----------------------------------------------------------|
| ToApply (0)      | Ad saved; application not yet submitted                  |
| Applied (1)      | Application submitted                                    |
| Interviewing (2) | Interview invitation received                            |
| ClosedNoAnswer (3) | Process ended with no employer response                |
| ClosedRejected (4) | Application was rejected                               |
| ClosedHired (5)  | Offer received and accepted                              |

The three `Closed*` values are mutually exclusive. Transitioning between them replaces the previous closed entry in StatusHistory. Moving backward clears later entries.

**3.7 AppSettings**

Singleton settings record. Exactly one row exists with `Id = 1`, seeded on first migration.

|                          |                                                                             |
|--------------------------|-----------------------------------------------------------------------------|
| **Column**               | **Type / Constraints / Notes**                                              |
| Id                       | INT — Primary Key, always 1 (singleton)                                     |
| BoardAnalyzerModel       | VARCHAR(100) — Claude model ID for board analysis. Default: claude-sonnet-4-5 |
| ScoringModel             | VARCHAR(100) — Claude model ID for ad scoring. Default: claude-haiku-4-5-20251001 |
| ResumeGenerationModel    | VARCHAR(100) — Claude model ID for resume generation. Default: claude-sonnet-4-6 |
| LetterGenerationModel    | VARCHAR(100) — Claude model ID for letter generation. Default: claude-sonnet-4-6 |
| BoardAnalyzerMaxTokens   | INT — Max tokens for board analysis calls. Default: 4096                    |
| ScoringMaxTokens         | INT — Max tokens for scoring calls. Default: 4096                           |
| ResumeGenerationMaxTokens| INT — Max tokens for resume generation calls. Default: 8192                 |
| LetterGenerationMaxTokens| INT — Max tokens for letter generation calls. Default: 2048                 |
| ResumeFileName           | VARCHAR(255) — Nullable, original filename of the uploaded resume           |
| ResumeContent            | BYTEA — Nullable, raw bytes of the resume file (stored in DB)               |
| ResumeContentType        | VARCHAR(100) — Nullable, MIME type (e.g. "application/pdf", "text/plain")   |
| ResumeUploadedAt         | TIMESTAMPTZ — Nullable, timestamp of last successful upload                 |
| ResumeTemplateFileName   | VARCHAR(255) — Nullable, original filename of the HTML resume template      |
| ResumeTemplateContent    | TEXT — Nullable, HTML content of the resume template                        |
| ResumeTemplateUploadedAt | TIMESTAMPTZ — Nullable, timestamp of last template upload                   |

`HasResume` and `HasResumeTemplate` are computed properties (not columns): `HasResume = ResumeContent IS NOT NULL`, `HasResumeTemplate = ResumeTemplateContent IS NOT NULL`.

Note: The resume file is stored as bytes in the database (`ResumeContent`), not on the container filesystem.

**3.8 GeneratedResume**

Versioned HTML resume generated by AI for a specific application. Each AI generation or manual WYSIWYG edit creates a new row; version numbers are sequential and never reused even after deletion.

|                   |                                                                          |
|-------------------|--------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                           |
| Id                | UUID — Primary Key                                                       |
| ApplicationId     | UUID — FK → Application.Id, CASCADE DELETE                               |
| VersionNumber     | INT — Sequential per application, starts at 1. Gaps are permanent.      |
| HtmlContent       | TEXT — Complete HTML resume document                                     |
| ModelUsed         | VARCHAR(100) — Claude model ID used for generation (or inherited for manual edits) |
| OptimizationLevel | VARCHAR(50) — Nullable, optimization level used for AI generation        |
| IsManualEdit      | BOOLEAN — TRUE if this version was created by the user via WYSIWYG editor |
| GeneratedAt       | TIMESTAMPTZ — NOT NULL, default NOW()                                    |

**3.9 GeneratedLetter**

Versioned HTML application letter generated by AI for a specific application. Same versioning semantics as `GeneratedResume`.

|                   |                                                                          |
|-------------------|--------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                           |
| Id                | UUID — Primary Key                                                       |
| ApplicationId     | UUID — FK → Application.Id, CASCADE DELETE                               |
| VersionNumber     | INT — Sequential per application, starts at 1. Gaps are permanent.      |
| HtmlContent       | TEXT — Complete HTML letter document                                     |
| ModelUsed         | VARCHAR(100) — Claude model ID used for generation                       |
| IsManualEdit      | BOOLEAN — TRUE if this version was created by the user via WYSIWYG editor |
| GeneratedAt       | TIMESTAMPTZ — NOT NULL, default NOW()                                    |

**3.10 InterviewStep**

Represents a single round in the user's interview process for an application. Steps are numbered sequentially starting at 1. Deleting a step in the middle renumbers remaining steps to keep the sequence contiguous. Multiple interviewers per step are stored as a JSONB array.

|                   |                                                                                      |
|-------------------|--------------------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                                       |
| Id                | UUID — Primary Key, default gen_random_uuid()                                        |
| ApplicationId     | UUID — FK → Application.Id, CASCADE DELETE                                           |
| StepNumber        | INT — 1-based sequential number per application. Renumbered after any deletion.      |
| Date              | DATE — Nullable, calendar date of the interview                                      |
| Time              | TIME — Nullable, time of day for the interview (24-hour)                             |
| DurationMinutes   | INT — Nullable, expected duration in minutes                                         |
| Timezone          | VARCHAR(50) — NOT NULL, default "CEST" (e.g. "UTC", "CET", "EST")                   |
| IsOnSite          | BOOLEAN — NOT NULL, true = on-site, false = remote                                   |
| RemoteCallLink    | VARCHAR(2048) — Nullable, video/phone call URL for remote interviews                 |
| Interviewers      | JSONB — Array of `{ name, jobFunction }` objects (see below)                         |
| Notes             | TEXT — Nullable, free-form preparation notes                                         |
| CreatedAt         | TIMESTAMPTZ — NOT NULL, default NOW()                                                |

**InterviewStepInterviewer (JSONB object within Interviewers array)**

| **Field**    | **Type / Notes**                                    |
|--------------|-----------------------------------------------------|
| Name         | STRING — Full name of the interviewer               |
| JobFunction  | STRING — Job title or function (e.g. "Engineering Manager") |

Index: `ix_interview_steps_application_id` on `ApplicationId` (non-unique).

**3.11 InterviewDrillPlan**

One plan per application — regenerating replaces the previous plan (delete-then-insert). Questions are stored as a JSONB array on the plan row; no separate questions table exists.

|                |                                                                                    |
|----------------|------------------------------------------------------------------------------------|
| **Column**     | **Type / Constraints / Notes**                                                     |
| Id             | UUID — Primary Key, default gen_random_uuid()                                      |
| ApplicationId  | UUID — FK → Application.Id, CASCADE DELETE. Unique index (one plan per application) |
| GeneratedAt    | TIMESTAMPTZ — NOT NULL                                                             |
| ModelUsed      | VARCHAR(100) — Claude model ID used for generation                                 |
| Questions      | JSONB — Ordered array of `InterviewQuestion` objects (see below)                   |

**InterviewQuestion (JSONB object within Questions array)**

|              |                                                                                                        |
|--------------|--------------------------------------------------------------------------------------------------------|
| **Field**    | **Type / Notes**                                                                                       |
| OrderIndex   | INT — 1-based display order                                                                            |
| Text         | STRING — The question text                                                                             |
| Category     | STRING — `warm_up` \| `easy` \| `medium` \| `challenging`                                             |
| RequirementName | STRING \| null — The scoring requirement that inspired this question; null for warm-up/generic questions |
| Answer       | STRING \| null — User's answer recorded during a drill session; null if not yet answered               |
| AnsweredAt   | TIMESTAMPTZ \| null — UTC timestamp when the answer was last saved; null if not yet answered           |

Answers are patched in-place on the JSONB array via `SaveAnswerAsync` (marks the `Questions` property modified explicitly, since EF Core does not detect mutations inside value-converted columns automatically). Regenerating the drill clears all answers.

**3.12 ScraperConfig JSON Schema**

The ScraperConfig column on JobBoard stores a structured object validated against this schema. It is generated by the AI board analysis step and stored verbatim.

```
{
"pagination_type": "url_param" | "next_button" | "infinite_scroll" | "load_more_button" | "none",
"job_card_selector": "string (CSS selector matching each job card container on the listing page)",
"field_selectors": {
  "detail_url": "string | null (CSS selector within card for the <a> link; null = first <a> in card)",
  "title":       "string | null (CSS selector within card for job title text)",
  "company":     "string | null",
  "location":    "string | null",
  "salary_raw":  "string | null",
  "posted_at":   "string | null",
  "description_snippet": "string | null (short summary if visible on listing page)",
  "external_id": "string | null (CSS selector for a stable board-specific job ID; null if not present)"
},
"next_page_selector": "string | null (CSS selector for next button)",
"url_param_name": "string | null (e.g. 'page', 'offset')",
"url_param_is_offset": "boolean (true if param is item count, not page number)",
"max_pages": "integer | null (safety cap, null = unlimited)",
"requires_js": "boolean",
"suggested_delay_ms": "integer (politeness delay between requests)",
"confidence_score": "float 0.0–1.0",
"analyzer_notes": "string | null (free-text notes from AI about unusual patterns)",
"generated_at": "ISO8601 timestamp"
}
```

The `job_card_selector` selects the entire card container. All field selectors are evaluated relative to each matched card element, enabling full job ad data extraction from the listing page alone — no detail page visits are required.

**3.13 Indexes**

|                                                                   |                                       |
|-------------------------------------------------------------------|---------------------------------------|
| **Index**                                                         | **Purpose**                           |
| JobAd(JobBoardId, Url) UNIQUE                                     | Primary deduplication key             |
| JobAd(JobBoardId, ExternalId) UNIQUE WHERE ExternalId IS NOT NULL | Secondary dedup using board ID        |
| JobAd(ScrapedAt DESC)                                             | Timeline queries on the ads dashboard |
| JobAd(JobBoardId, IsActive)                                       | Filtered listing queries              |
| JobAd(IsRead, IsTrashed)                                          | Unread count and inbox queries        |
| ScrapeRun(JobBoardId, StartedAt DESC)                             | Run history per board                 |
| JobBoard(Status)                                                  | Filtering active/paused boards        |

**4. AI Layer**

**4.1 Abstraction Design**

All AI interactions are routed through a single interface. This decouples business logic from any specific LLM provider and allows future providers (OpenAI, Gemini, local models) to be added by implementing the interface and registering it in the DI container.

```
public interface IAiProvider
{
    // Called once when a job board is first registered (or re-analyzed).
    // Returns a structured config describing how to scrape the board,
    // including card-level CSS selectors for extracting all job ad fields
    // directly from the listing page — no detail page visits required.
    Task<BoardAnalysisResult> AnalyzeBoardAsync(
        string html,
        string url,
        CancellationToken ct = default);

    // Called on demand per ad or application to score its relevance against the user's resume.
    // The resume is passed as raw bytes to support PDF and JSON resume formats.
    Task<AdScoringResult> ScoreAdAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string jobPageText,
        CancellationToken ct = default);

    // Generates a tailored HTML resume from the candidate's resume, an HTML template
    // (defines visual structure), the job ad content, and the scoring analysis.
    Task<string> GenerateResumeAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string resumeTemplateHtml,
        string jobAdContent,
        string scoringSummary,
        string scoringRecommendation,
        string scoringRequirementsJson,
        ResumeOptimizationLevel optimizationLevel = ResumeOptimizationLevel.None,
        CancellationToken ct = default);

    // Generates a professional application letter (~half a page) in HTML format,
    // tailored to the job ad and highlighting matched requirements.
    Task<string> GenerateLetterAsync(
        byte[] resumeContent,
        string resumeContentType,
        string resumeFileName,
        string jobAdContent,
        string? jobTitle,
        string? company,
        string scoringSummary,
        string scoringRecommendation,
        string scoringRequirementsJson,
        CancellationToken ct = default);
}
```

The active Claude models and max-token limits for each operation are configurable via `AppSettings`, read from the database at runtime. This allows the user to switch models and tune token budgets via the Settings UI without a code deployment.

**4.2 Structured Output via Tool Use**

To guarantee consistent response schemas, all AI operations use the Claude Tool Use API. Claude is instructed to call a specific tool with a fixed schema — it cannot return free-form JSON. The tool input is then deserialized directly into the corresponding C# record.

```
Why Tool Use instead of a JSON prompt?
A plain "return JSON" instruction yields inconsistent field names, missing required fields, and schema drift between calls. Tool Use forces Claude to populate a pre-defined schema exactly — the response is always machine-readable and deserializable without defensive patching.
```

Note: `GenerateResumeAsync` and `GenerateLetterAsync` return raw HTML strings, not Tool Use structured output — they use a direct text-generation prompt with the HTML document as the expected response.

**4.3 Board Analysis — Tool Schema**

```
Tool name: "save_board_config"
{
"type": "object",
"required": ["pagination_type", "job_card_selector", "field_selectors",
"requires_js", "suggested_delay_ms", "confidence_score"],
"properties": {
"pagination_type": {
  "type": "string",
  "enum": ["url_param", "next_button", "infinite_scroll", "load_more_button", "none"]
},
"job_card_selector": { "type": "string" },
"field_selectors": {
  "type": "object",
  "required": ["title"],
  "properties": {
    "detail_url":           { "type": ["string", "null"] },
    "title":                { "type": "string" },
    "company":              { "type": ["string", "null"] },
    "location":             { "type": ["string", "null"] },
    "salary_raw":           { "type": ["string", "null"] },
    "posted_at":            { "type": ["string", "null"] },
    "description_snippet":  { "type": ["string", "null"] },
    "external_id":          { "type": ["string", "null"] }
  }
},
"next_page_selector":  { "type": ["string", "null"] },
"url_param_name":      { "type": ["string", "null"] },
"url_param_is_offset": { "type": "boolean" },
"max_pages":           { "type": ["integer", "null"], "minimum": 1 },
"requires_js":         { "type": "boolean" },
"suggested_delay_ms":  { "type": "integer", "minimum": 0, "maximum": 10000 },
"confidence_score":    { "type": "number", "minimum": 0, "maximum": 1 },
"analyzer_notes":      { "type": ["string", "null"] }
}
}
```

**4.4 Ad Scoring — Tool Schema**

The ad scorer is called on demand per ad or application. It receives the ad's text content (fetched from the job URL) and the user's resume bytes (supports PDF and JSON formats), and returns a structured relevance assessment.

```
Tool name: "save_ad_score"
{
"type": "object",
"required": ["overall_score", "summary", "recommendation", "requirements"],
"properties": {
  "overall_score":   { "type": "number", "minimum": 0, "maximum": 100 },
  "summary":         { "type": "string" },
  "recommendation":  { "type": "string" },
  "requirements": {
    "type": "array",
    "items": {
      "type": "object",
      "required": ["name", "category", "score"],
      "properties": {
        "name":        { "type": "string" },
        "category":    { "type": "string", "enum": ["match", "partial_match", "gap"] },
        "is_optional": { "type": "boolean" },
        "score":       { "type": "number", "minimum": 0, "maximum": 100 },
        "notes":       { "type": ["string", "null"] }
      }
    }
  }
}
}
```

Note: The requirements schema changed from `{ requirement, met: bool, explanation }` to `{ name, category, is_optional, score, notes }`. The `overall_score` is a FLOAT (average of per-requirement scores), not an integer.

**4.5 Job Ad Extraction — Deterministic CSS Extraction (No AI)**

Job ad data is extracted entirely from the listing page using the `field_selectors` map generated during board analysis. For each element matching `job_card_selector`, the scraper evaluates each selector relative to the card element and reads the text content of the matched node. No AI call is made per ad during a scrape run.

This approach means:
- Zero AI calls per scrape run (after initial board analysis)
- Detail pages are never visited; the user opens the ad URL directly in their browser
- If a selector stops matching (board redesigned), self-healing triggers a new board analysis

**4.6 HTML Pre-Processing (Cost Optimisation)**

Raw Playwright HTML can exceed 500KB (roughly 125,000 tokens). Before sending HTML to Claude, the system applies a cleaning pipeline to reduce token consumption by 5–10x:

1.  Remove all \<script\> tags and their content

2.  Remove all \<style\> tags and their content

3.  Remove all \<svg\> elements

4.  Remove HTML comments

5.  Collapse whitespace (multiple spaces, blank lines)

6.  Strip non-visible attributes (data-\*, aria-\* where not essential)

7.  Cleaning is applied to listing pages sent to `AnalyzeBoardAsync` only. No cleaning is needed per ad since there are no per-ad AI calls during scraping.

Target: under 10,000 tokens per API call after cleaning.

**4.7 Self-Healing**

Since there are no per-ad AI calls, there are no per-ad confidence scores. Self-healing is triggered structurally: if `job_card_selector` matches zero elements on the first listing page of a scrape run, the system automatically enqueues a new `BoardAnalysisJob` to regenerate the `ScraperConfig`. This indicates the board's HTML structure has changed and the selectors are stale.

The `JobBoard` status is set to `error` if the re-analysis also fails.

**4.8 Claude API Configuration**

|               |                                                                        |
|---------------|------------------------------------------------------------------------|
| **Parameter** | **Value**                                                              |
| Model         | Configurable per operation via AppSettings (board analyzer / scorer / resume / letter) |
| Max Tokens    | Configurable per operation via AppSettings (default: 4096 for analysis/scoring, 8192 for resume, 2048 for letter) |
| Temperature   | 0 (deterministic structured output)                                    |
| tool_choice   | { "type": "tool", "name": "\<tool_name\>" } (force tool call, analysis and scoring only) |
| Timeout       | 30 seconds per call                                                    |
| Retry Policy  | 3 attempts with exponential backoff (1s, 2s, 4s)                      |

**5. Scraping Pipeline**

**5.1 Board Registration Flow (one-time)**

```
POST /api/job-boards { "url": "https://example-jobs.com" }
│
├─ Validate URL reachable (HTTP HEAD)
├─ Create JobBoard record (status: "pending")
├─ Enqueue BoardAnalysisJob (Hangfire fire-and-forget)
└─ Return 202 Accepted + board ID
BoardAnalysisJob:
├─ Playwright renders the URL (full JS execution, wait for networkidle)
├─ Extract & clean HTML
├─ Send to ClaudeAiProvider.AnalyzeBoardAsync()
├─ Deserialize tool_use response → BoardAnalysisResult
├─ Persist ScraperConfig to JobBoard.ScraperConfig
├─ Set JobBoard.Status = "active"
├─ Register Hangfire recurring job (cron from ScraperConfig or default)
└─ Trigger immediate first ScrapeRun
```

**5.2 Recurring Scrape Run Flow**

```
ScrapeJobRunner.ExecuteAsync(jobBoardId):
│
├─ Load JobBoard + ScraperConfig
├─ Create ScrapeRun record (status: "running")
│
├─ LISTING LOOP:
│ ├─ Playwright renders current page URL (with stealth headers)
│ ├─ Find all elements matching job_card_selector
│ ├─ If 0 cards found on page 1 → trigger self-heal (re-analysis), stop run
│ ├─ For each card element:
│ │ ├─ Extract detail URL (field_selectors.detail_url or first <a> in card)
│ │ ├─ Normalize URL (strip query string, lowercase)
│ │ ├─ Check dedup (normalized URL lookup; ExternalId if field_selectors.external_id set)
│ │ ├─ Skip if already exists
│ │ ├─ Extract fields from card: title, company, location, salary_raw,
│ │ │   posted_at, description_snippet, external_id
│ │ └─ Persist JobAd
│ │
│ └─ PAGINATION:
│   ├─ url_param → increment param, build next URL
│   ├─ next_button → find selector, check disabled, get href
│   ├─ load_more_button → click selector, wait for new cards to load
│   ├─ infinite_scroll → single page only (not supported)
│   └─ none → stop
│
├─ Update ScrapeRun (status: "completed", counts)
├─ Update JobBoard.LastScrapedAt
├─ Apply stale detection (ads not seen this run → IsActive = false)
└─ Broadcast SSE event: runCompleted
```

No AI calls are made during a scrape run. All extraction is deterministic CSS-based logic using the selectors generated during board analysis. The `suggested_delay_ms` applies between listing pages, not between ads.

**5.3 Deduplication Strategy**

Deduplication is applied before rendering the detail page to avoid unnecessary Playwright calls:

- **Primary:** Exact URL match — JobAd.Url = detail link href (normalised, query strings stripped for comparison)

- **Secondary:** ExternalId match — if `field_selectors.external_id` is set in the `ScraperConfig`, the extracted value is also used as a unique key per board

- **Stale detection:** Ads whose normalized URL was not seen in the current scrape run are marked `IsActive = false`

**5.4 Playwright Configuration**

|                      |                                                                |
|----------------------|----------------------------------------------------------------|
| **Setting**          | **Value / Notes**                                              |
| Browser              | Chromium (lightest, fastest startup)                           |
| Launch mode          | Headless                                                       |
| WaitUntil            | networkidle (ensures JS frameworks have settled)               |
| Timeout              | 30 seconds per page load                                       |
| User agent           | Realistic desktop UA string (stealth mode)                     |
| Viewport             | 1280 × 800 (prevents mobile layouts)                           |
| Concurrency          | Single browser instance, sequential page processing            |
| Stealth headers      | Extra-UA, Accept-Language, platform hints set to pass bot checks |
| shm_size             | 256 MB (set in docker-compose to prevent Chromium OOM crashes) |

Note: The Playwright browser binary is bundled in the `playwright/dotnet` base image and does not require a separate volume mount. The `shm_size: '256mb'` setting on the API Docker service prevents Chromium shared-memory crashes.

**6. REST API Specification**

**6.1 Base URL & Conventions**

- Base URL: http://localhost:8080/api (local Docker)

- All requests/responses: application/json

- Timestamps: ISO 8601 UTC

- IDs: UUID v4 strings

- Errors: RFC 7807 Problem Details format

- Pagination: cursor-based via ?cursor= and ?limit= (default 50, max 200)

**6.2 Job Boards Endpoints**

|                                     |                                                                                                                                                          |
|-------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                        | **Description**                                                                                                                                          |
| POST /api/job-boards                | Register a new job board. Accepts { url, name? (optional), schedule_cron? }. Returns 202 Accepted with the created board. Triggers async board analysis. |
| GET /api/job-boards                 | List all job boards. Returns array with status, last_scraped_at, ad counts.                                                                              |
| GET /api/job-boards/{id}            | Get a single board with full scraper_config.                                                                                                             |
| PATCH /api/job-boards/{id}          | Update name, url, schedule_cron, or status (active/paused). Re-registers Hangfire job if cron changes.                                                   |
| DELETE /api/job-boards/{id}         | Delete board and cancel Hangfire job. Cascade deletes ads and runs.                                                                                      |
| POST /api/job-boards/{id}/refresh   | Trigger an immediate scrape run (fire-and-forget Hangfire enqueue). Returns 202.                                                                         |
| POST /api/job-boards/{id}/reanalyze | Trigger a new board analysis to regenerate scraper_config. Returns 202.                                                                                  |
| GET /api/job-boards/{id}/export     | Export the board's scraper configuration as a portable `BoardExchangeDto` JSON file. Returns 400 if the board has no scraper config yet.                 |
| POST /api/job-boards/import         | Import a board from a `BoardExchangeDto`. Creates the board as Active, registers the recurring Hangfire job, and enqueues an immediate first scrape. Returns 201. |

**6.2.1 Community Board Sharing**

Workcast supports sharing scraper configurations between users via a `/community-boards/` folder in the repository. Each file is a `BoardExchangeDto` JSON document that can be committed to the repo and shared via GitHub pull request.

**Export flow**

1. User clicks **Export config** on the board detail page (only shown when a scraper config exists).
2. Frontend calls `GET /api/job-boards/{id}/export`.
3. Backend maps the `JobBoard` entity to a `BoardExchangeDto`, sets `Content-Disposition: attachment`, and returns the JSON. User-specific fields (`id`, `status`, `lastScrapedAt`, `createdAt`, `updatedAt`, `adCount`) are excluded.
4. Browser downloads the file as `{board-name}.json`.

**Import flow**

1. User clicks **+ Add Board** → **Import Boards** tab.
2. User selects one or more local `.json` files and/or pastes one or more raw GitHub URLs (one per line) and clicks **Load**.
3. Frontend parses/fetches all sources in parallel and displays a queue. Each entry shows name, URL, parse status, and a remove button.
4. User reviews the queue and clicks **Import Boards**.
5. Frontend calls `POST /api/job-boards/import` for every valid entry in parallel (`Promise.allSettled`).
6. Backend validates the request, creates the `JobBoard` via `JobBoard.Create()`, calls `board.Activate(scraperConfig)`, persists, registers the recurring Hangfire scrape job, and enqueues an immediate first scrape (`TriggerSource.Manual`). Returns `201 Created`.
7. After all requests settle, the `job-boards` TanStack Query cache is invalidated once. The form closes only if every entry succeeded; otherwise it stays open showing per-item errors.

**`BoardExchangeDto` shape**

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | string | Currently `"1"`. Import rejects unknown versions with 400. |
| `name` | string | Board display name. |
| `url` | string | Canonical seed URL. |
| `scheduleCron` | string | Suggested scrape schedule. Importer uses it as-is; user can change it afterwards. |
| `scraperConfig` | `ScraperConfigExchangeDto` | Full scraper configuration (see below). |

**`ScraperConfigExchangeDto` shape** — mirrors `ScraperConfig` exactly and is used for both import and export (round-trip safe). Contains all pagination fields (`paginationType`, `nextPageSelector`, `urlParamName`, `urlParamIsOffset`, `maxPages`), all selector fields (`jobCardSelector`, `fieldSelectors.*`), all behaviour fields (`requiresJs`, `suggestedDelayMs`), and AI metadata (`confidenceScore`, `analyzerNotes`, `generatedAt`). AI metadata is informational and not required for a successful import.

**Community folder**

The `/community-boards/` folder at the repository root holds community-contributed configs. The default URL pre-filled in the import UI points to `community-boards/example.json` in the `alienjouster/workcast` repository on GitHub so new users can verify the import flow without manually finding a file.

**6.3 Job Ads Endpoints**

|                                      |                                                                                                                        |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                         | **Description**                                                                                                        |
| POST /api/job-ads                    | Create a manual job ad (not from scraping). Accepts { url, title, company?, location? }. Returns 201.                 |
| PATCH /api/job-ads/{id}              | Update user-editable fields of a job ad: url, title, company, location. Works on both manual and scraped ads.          |
| GET /api/job-ads                     | List ads. Supports: ?board_ids= (multi), ?titles= (multi, include/exclude), ?locations= (multi), ?companies= (multi), ?is_active=, ?is_read=, ?is_pinned=, ?min_score=, ?trashed=, ?cursor=, ?limit= |
| GET /api/job-ads/{id}                | Get a single ad with full fields including scoring if available.                                                       |
| DELETE /api/job-ads/{id}             | Hard delete a single ad.                                                                                               |
| PATCH /api/job-ads/{id}/pin          | Pin the ad (IsPinned = true).                                                                                          |
| PATCH /api/job-ads/{id}/unpin        | Unpin the ad (IsPinned = false).                                                                                       |
| PATCH /api/job-ads/{id}/read         | Mark ad as read (IsRead = true).                                                                                       |
| PATCH /api/job-ads/{id}/unread       | Mark ad as unread (IsRead = false).                                                                                    |
| POST /api/job-ads/mark-all-read      | Mark all visible (non-trashed) ads as read for a given board_id or globally.                                           |
| PATCH /api/job-ads/{id}/trash        | Soft-delete the ad (IsTrashed = true).                                                                                 |
| PATCH /api/job-ads/{id}/restore      | Restore a trashed ad (IsTrashed = false).                                                                              |
| POST /api/job-ads/bulk/pin           | Bulk pin a set of ads. Accepts { ids: uuid[] }.                                                                        |
| POST /api/job-ads/bulk/unpin         | Bulk unpin a set of ads. Accepts { ids: uuid[] }.                                                                      |
| POST /api/job-ads/bulk/read          | Bulk mark ads as read. Accepts { ids: uuid[] }.                                                                        |
| POST /api/job-ads/bulk/unread        | Bulk mark ads as unread. Accepts { ids: uuid[] }.                                                                      |
| POST /api/job-ads/bulk/trash         | Bulk trash a set of ads. Accepts { ids: uuid[] }.                                                                      |
| POST /api/job-ads/bulk/restore       | Bulk restore a set of trashed ads. Accepts { ids: uuid[] }.                                                            |
| POST /api/job-ads/bulk/delete        | Bulk hard-delete a set of ads. Accepts { ids: uuid[] }.                                                                |
| GET /api/job-ads/distinct-titles     | Returns distinct title values for filter typeahead. Supports ?board_id=.                                               |
| GET /api/job-ads/distinct-locations  | Returns distinct location values for filter typeahead.                                                                 |
| GET /api/job-ads/distinct-companies  | Returns distinct company values for filter typeahead.                                                                  |

Note: The `/api/job-ads/{id}/note` endpoint was removed. The `/api/job-ads/unread-count` endpoint was merged into `/api/status`. Bulk actions are now separate endpoints per action (not a single `POST /api/job-ads/bulk` with an `action` parameter).

**6.4 Scrape Runs Endpoints**

|                               |                                                                       |
|-------------------------------|-----------------------------------------------------------------------|
| **Endpoint**                  | **Description**                                                       |
| GET /api/job-boards/{id}/runs | List scrape run history for a board (newest first). Supports ?limit=. |
| GET /api/runs/{id}            | Get details of a single run including errors array.                   |

**6.5 Ad Scoring Endpoints**

|                               |                                                                                               |
|-------------------------------|-----------------------------------------------------------------------------------------------|
| **Endpoint**                  | **Description**                                                                               |
| GET /api/ad-scoring/{adId}    | Get the existing scoring for an ad. Returns 404 if not yet scored.                            |
| POST /api/ad-scoring/{adId}   | Trigger on-demand scoring for an ad. Enqueues AdScoringJob. Returns 202.                      |

**6.6 Applications Endpoints**

|                                              |                                                                                                          |
|----------------------------------------------|----------------------------------------------------------------------------------------------------------|
| **Endpoint**                                 | **Description**                                                                                          |
| POST /api/applications                       | Create an application. Accepts { job_ad_id? } — copies fields from the source ad if provided.           |
| GET /api/applications                        | List applications. Supports: ?titles=, ?locations=, ?companies=, ?min_score=, ?trashed=, ?cursor=, ?limit= |
| GET /api/applications/{id}                   | Get a single application with all fields.                                                                |
| GET /api/applications/distinct-titles        | Distinct title values for filter typeahead.                                                              |
| GET /api/applications/distinct-locations     | Distinct location values for filter typeahead.                                                           |
| GET /api/applications/distinct-companies     | Distinct company values for filter typeahead.                                                            |
| PATCH /api/applications/{id}/trash           | Soft-delete an application.                                                                              |
| PATCH /api/applications/{id}/restore         | Restore a trashed application.                                                                           |
| DELETE /api/applications/{id}                | Hard delete an application.                                                                              |
| PATCH /api/applications/{id}/status          | Update the application status. Accepts { status, achieved_at? }.                                        |
| PATCH /api/applications/{id}/status/date     | Update only the date for an already-reached status. Accepts { status, achieved_at }.                    |
| PATCH /api/applications/{id}/posted-at       | Update the PostedAt field.                                                                               |
| PATCH /api/applications/{id}/scraped-at      | Update the ScrapedAt field.                                                                              |
| PATCH /api/applications/{id}/job-ad-content  | Store or clear the full text of the job ad page. Accepts { content: string \| null }.                   |
| POST /api/applications/{id}/scoring          | Trigger AI scoring for this application. Enqueues ApplicationScoringJob. Returns 202.                   |
| DELETE /api/applications/{id}/scoring        | Clear the stored scoring data from an application.                                                       |
| POST /api/applications/{id}/resume/generate  | Trigger AI resume generation. Enqueues ApplicationResumeGenerationJob. Returns 202.                     |
| PATCH /api/applications/{id}/resume/latest   | Save a manual edit to the latest resume version (creates a new version row). Returns 200.               |
| GET /api/applications/{id}/resume/latest     | Get the latest generated resume HTML.                                                                    |
| GET /api/applications/{id}/resume/versions   | List all resume versions for this application.                                                           |
| DELETE /api/applications/{id}/resume/versions/{versionId} | Delete a specific resume version.                                               |
| POST /api/applications/{id}/letter/generate  | Trigger AI letter generation. Enqueues ApplicationLetterGenerationJob. Returns 202.                     |
| PATCH /api/applications/{id}/letter/latest   | Save a manual edit to the latest letter version (creates a new version row). Returns 200.               |
| GET /api/applications/{id}/letter/latest     | Get the latest generated letter HTML.                                                                    |
| GET /api/applications/{id}/letter/versions   | List all letter versions for this application.                                                           |
| DELETE /api/applications/{id}/letter/versions/{versionId} | Delete a specific letter version.                                               |

**6.6.1 Interview Steps Endpoints**

|                                                          |                                                                                                                                    |
|----------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                                             | **Description**                                                                                                                    |
| GET /api/applications/{id}/interview-steps               | List all interview steps for an application, ordered by StepNumber ascending. Returns 404 if application not found.                |
| POST /api/applications/{id}/interview-steps              | Create a new step. StepNumber is assigned automatically (max + 1). Returns 201 with the created step.                              |
| PUT /api/applications/{id}/interview-steps/{stepId}      | Update all fields of an existing step. Returns 200 with the updated step. Returns 404 if step or application not found.            |
| DELETE /api/applications/{id}/interview-steps/{stepId}   | Delete a step and renumber remaining steps to keep the sequence contiguous. Returns 204. Returns 404 if not found.                 |

**6.6.2 Interview Drill Endpoints**

|                                                                        |                                                                                                                     |
|------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                                                           | **Description**                                                                                                     |
| POST /api/applications/{id}/interview-drill/generate                   | Trigger AI drill generation. Requires scoring data on the application. Enqueues InterviewDrillJob. Returns 202.     |
| DELETE /api/applications/{id}/interview-drill/generate                 | Clear a stuck `isInterviewDrillPending` flag without regenerating.                                                  |
| GET /api/applications/{id}/interview-drill                             | Get the current drill plan including all questions and any saved answers. Returns 404 if no plan exists.            |
| PUT /api/applications/{id}/interview-drill/questions/{orderIndex}/answer | Save or clear the user's answer for a single question. Body: `{ answer: string \| null }`. Returns 204.          |

**6.7 Settings Endpoints**

|                                   |                                                                                                              |
|-----------------------------------|--------------------------------------------------------------------------------------------------------------|
| **Endpoint**                      | **Description**                                                                                              |
| GET /api/settings                 | Get current user settings (models, max tokens, resume/template status).                                      |
| PATCH /api/settings               | Update settings. Accepts model IDs and/or MaxTokens per operation.                                           |
| PUT /api/settings/resume          | Upload a resume file. Stored as bytes in the database. Returns 200 on success.                               |
| DELETE /api/settings/resume       | Delete the stored resume. Sets ResumeContent = null.                                                         |
| PUT /api/settings/resume-template | Upload an HTML resume template. Stored as text in the database. Returns 200 on success.                      |
| DELETE /api/settings/resume-template | Delete the stored resume template.                                                                        |

**6.8 Server-Sent Events Endpoint**

|                 |                                                                                                                 |
|-----------------|-----------------------------------------------------------------------------------------------------------------|
| **Endpoint**    | **Description**                                                                                                 |
| GET /api/events | SSE stream. The client holds a persistent connection. The server pushes named events as domain activity occurs. |

**SSE Event Types:**

| **Event Name**                          | **Key Payload Fields**                          | **When Fired**                                              |
|-----------------------------------------|-------------------------------------------------|-------------------------------------------------------------|
| boardStatusChanged                      | { boardId, status }                             | When a board's status changes (pending → active, etc.)      |
| runEnqueued                             | { boardId, runId }                              | When a ScrapeRun is enqueued in Hangfire                    |
| runStarted                              | { boardId, runId }                              | When ScrapeJobRunner begins execution                       |
| runStatusChanged                        | { boardId, runId, status }                      | When a run transitions state during execution               |
| runCompleted                            | { boardId, runId, adsNew }                      | When a ScrapeJobRunner finishes (success or partial)        |
| unreadCountChanged                      | { unreadCount }                                 | When the unread ad count changes                            |
| scoringCompleted                        | { adId }                                        | When AdScoringJob finishes for a job ad                     |
| applicationScoringCompleted             | { applicationId }                               | When ApplicationScoringJob finishes                         |
| applicationResumeGenerationCompleted    | { applicationId }                               | When ApplicationResumeGenerationJob finishes                |
| applicationLetterGenerationCompleted    | { applicationId }                               | When ApplicationLetterGenerationJob finishes                |

**6.9 Status Endpoint**

|                    |                                                                                                              |
|--------------------|--------------------------------------------------------------------------------------------------------------|
| **Endpoint**       | **Description**                                                                                              |
| GET /api/status    | Returns { is_processing: bool, unread_count: int } — is_processing is TRUE if any Hangfire jobs are currently enqueued/running. |

Note: `unread_count` was merged into `/api/status` (previously a separate `/api/job-ads/unread-count` endpoint).

**6.10 Error Response Format**

```
{
"type": "https://jobscraper.local/errors/board-analysis-failed",
"title": "Board Analysis Failed",
"status": 422,
"detail": "Claude was unable to identify job listing links on this page.",
"instance": "/api/job-boards/3f2a1b..."
}
```

**7. Scheduling & Background Jobs**

**7.1 Hangfire Setup**

Hangfire runs in-process within the .NET API container. It uses PostgreSQL as its storage backend (the same database instance). No additional infrastructure is required.

|                   |                                                           |
|-------------------|-----------------------------------------------------------|
| **Configuration** | **Value**                                                 |
| Storage           | Hangfire.PostgreSql — same DB, separate schema "hangfire" |
| Dashboard URL     | http://localhost:8080/hangfire (no auth for local dev)    |
| Worker count      | 2 concurrent workers (sufficient for sequential scraping) |
| Job retention     | Succeeded jobs: 24h. Failed jobs: 7 days.                 |
| Priority queues   | Separate queues for scoring vs scraping                   |

**7.2 Job Types**

|                                   |                 |                                                                                                               |
|-----------------------------------|-----------------|---------------------------------------------------------------------------------------------------------------|
| **Job Class**                     | **Type**        | **Description**                                                                                               |
| BoardAnalysisJob                  | Fire-and-forget | Runs once on board registration. Analyzes the board and stores ScraperConfig.                                 |
| ScrapeJobRunner                   | Recurring       | Runs on board's cron schedule. Executes full scrape pipeline.                                                 |
| ScrapeJobRunner                   | Fire-and-forget | Triggered by manual refresh endpoint. Same logic, different trigger source.                                   |
| AdScoringJob                      | Fire-and-forget | Triggered on-demand per job ad. Calls AI scorer → stores AdScoring record, updates IsScoringPending.          |
| ApplicationScoringJob             | Fire-and-forget | Triggered on-demand per application. Fetches job page content, calls AI scorer → updates Application scoring fields. |
| ApplicationResumeGenerationJob    | Fire-and-forget | Triggered on-demand per application. Calls AI resume generator → creates new GeneratedResume version.         |
| ApplicationLetterGenerationJob    | Fire-and-forget | Triggered on-demand per application. Calls AI letter generator → creates new GeneratedLetter version.         |
| AdCleanupJob                      | Recurring       | Runs daily. Hard-deletes ads that have been in IsTrashed=true state for more than 30 days.                    |
| StaleRunCleanupJob                | Recurring       | Runs periodically. Removes old ScrapeRun records beyond the configured retention window.                      |

**7.3 Application Scoring Flow**

`ApplicationScoringJob` diverges from `AdScoringJob` in that it uses the stored `JobAdContent` on the Application (full page text fetched at creation time) rather than re-scraping the job URL. If `JobAdContent` is null or too short, it attempts to fetch the page via Playwright as a fallback.

**7.4 Schedule Management**

```
// On board creation (after analysis):
RecurringJob.AddOrUpdate<ScrapeJobRunner>(
    recurringJobId: $"scrape-{board.Id}",
    methodCall: x => x.ExecuteAsync(board.Id, CancellationToken.None),
    cronExpression: board.ScheduleCron,
    options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
);
// On schedule update (PATCH /api/job-boards/{id}):
RecurringJob.AddOrUpdate<ScrapeJobRunner>( // same ID = update
    recurringJobId: $"scrape-{board.Id}",
    ...
    cronExpression: updatedBoard.ScheduleCron
);
// On board pause or delete:
RecurringJob.RemoveIfExists($"scrape-{board.Id}");
// On manual refresh:
BackgroundJob.Enqueue<ScrapeJobRunner>(
    x => x.ExecuteAsync(board.Id, CancellationToken.None)
);
// On ad scoring request:
BackgroundJob.Enqueue<AdScoringJob>(
    x => x.ExecuteAsync(adId, CancellationToken.None)
);
// On application scoring request:
BackgroundJob.Enqueue<ApplicationScoringJob>(
    x => x.ExecuteAsync(applicationId, CancellationToken.None)
);
```

Note: Recurring job registrations are re-applied on API startup to ensure Hangfire's stored method signature stays in sync with the current code.

**7.5 Default Schedule**

Every new job board is assigned a default cron of "0 \* \* \* \*" (every hour, on the hour). The user can override this per board via the PATCH endpoint or the frontend UI.

**8. Docker & Infrastructure**

**8.1 docker-compose.yml**

```
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - ../volumes/postgres:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 5s
      retries: 5
    restart: unless-stopped

  api:
    build:
      context: ..
      dockerfile: docker/api.Dockerfile
    shm_size: '256mb'
    environment:
      ConnectionStrings__Default: "Host=db;Database=...;Username=...;Password=..."
      Anthropic__ApiKey: ${ANTHROPIC_API_KEY}
      ASPNETCORE_URLS: http://+:8080
    ports:
      - "8080:8080"
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

  web:
    build:
      context: ../web
      dockerfile: ../docker/web.Dockerfile
      args:
        NEXT_PUBLIC_API_URL: http://localhost:8080
        NEXT_PUBLIC_PROMETHEUS_URL: http://localhost:9090
        NEXT_PUBLIC_GRAFANA_URL: http://localhost:3001
    environment:
      NEXT_PUBLIC_API_URL: http://localhost:8080
      NEXT_PUBLIC_PROMETHEUS_URL: http://localhost:9090
      NEXT_PUBLIC_GRAFANA_URL: http://localhost:3001
      API_INTERNAL_URL: http://api:8080
    ports:
      - "3000:3000"
    depends_on:
      - api
    restart: unless-stopped

  postgres-exporter:
    image: prometheuscommunity/postgres-exporter:latest
    environment:
      DATA_SOURCE_NAME: "postgresql://${POSTGRES_USER}:${POSTGRES_PASSWORD}@db:5432/${POSTGRES_DB}?sslmode=disable"
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ../volumes/prometheus:/prometheus
    ports:
      - "9090:9090"
    depends_on:
      - api
      - postgres-exporter
    restart: unless-stopped

  grafana:
    image: grafana/grafana:latest
    volumes:
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - ../volumes/grafana:/var/lib/grafana
    ports:
      - "3001:3000"
    environment:
      GF_SECURITY_ADMIN_PASSWORD: admin
      GF_USERS_ALLOW_SIGN_UP: "false"
    depends_on:
      - prometheus
    restart: unless-stopped
```

**8.2 .env.example**

```
# Copy to .env and fill in values. Never commit .env to source control.
COMPOSE_PROJECT_NAME=workcast
POSTGRES_DB=jobscraper
POSTGRES_USER=jobscraper
POSTGRES_PASSWORD=changeme
ANTHROPIC_API_KEY=sk-ant-...
```

Note: `ANTHROPIC_MODEL` is no longer in `.env`. All model selection is done at runtime via the Settings UI and stored in the database.

**8.3 API Dockerfile**

The API image uses the official `playwright/dotnet` base image which bundles Playwright browser binaries and all required system dependencies. This eliminates the need to install system packages or run `playwright install` at build time.

```
FROM mcr.microsoft.com/playwright/dotnet:v1.x.x-noble AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Workcast.Api/Workcast.Api.csproj \
    -c Release -o /app/publish

FROM mcr.microsoft.com/playwright/dotnet:v1.x.x-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Workcast.Api.dll"]
```

> **Important:** Do not use `apt-get` to install browser system dependencies manually, and do not use `Microsoft.Playwright.CLI` (`playwright install`). The `playwright/dotnet` base image handles both. Never bind-mount over `/ms-playwright` in a way that would shadow the image's own Playwright installation.

**8.4 External Volume Strategy**

|                        |                          |                                                                  |
|------------------------|--------------------------|------------------------------------------------------------------|
| **Volume (host path)** | **Container mount**      | **Contents**                                                     |
| ./volumes/postgres     | /var/lib/postgresql/data | All PostgreSQL data files — boards, ads, runs, Hangfire state    |
| ./volumes/prometheus   | /prometheus              | Prometheus time-series data                                      |
| ./volumes/grafana      | /var/lib/grafana         | Grafana dashboards and configuration state                       |

Note: The Playwright browser binary volume mount was removed. Browsers are now bundled in the `playwright/dotnet` base image and do not require a host volume.

```
Important: volumes/ directory
The volumes/ directory must be added to .gitignore. It should only be committed if the team explicitly shares a pre-seeded database for development purposes, which is not recommended.
```

**8.5 EF Core Migrations on Startup**

The API applies pending EF Core migrations automatically on startup using the following pattern in Program.cs:

```
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.MigrateAsync();
```

This ensures the schema is always up to date when the container starts, including on first run against a fresh PostgreSQL volume.

**8.6 Performance Monitoring**

Prometheus scrapes metrics from two sources:

- **API metrics** (`/metrics` endpoint on port 8080) — exposed via the `prometheus-net.AspNetCore` package. Includes HTTP request durations, Hangfire queue depths (via `HangfireMetricsService`), and standard .NET runtime metrics.
- **PostgreSQL metrics** — exported by `postgres-exporter` and scraped by Prometheus.

Grafana is pre-provisioned with a `workcast.json` dashboard covering Hangfire job throughput, HTTP latency percentiles, and database connection pool usage. Default credentials: `admin / admin`.

**9. Frontend — Next.js Application**

**9.1 Structure**

```
web/
├── app/
│ ├── layout.tsx           # Root layout, navigation (Boards, Job Ads, Applications, Settings)
│ ├── page.tsx             # Redirect to /boards
│ ├── providers.tsx        # TanStack Query provider, theme
│ ├── boards/
│ │ ├── page.tsx           # Board list + add board form; recent runs tab
│ │ └── [id]/
│ │   ├── page.tsx         # Board detail: config, run history, edit schedule
│ │   └── ads/page.tsx     # Ads for this board with active/inactive filter
│ ├── ads/
│ │ ├── page.tsx           # Route
│ │ └── AdsClient.tsx      # Global ad search/browse with FilterBar; Trash Bin tab
│ ├── applications/
│ │ ├── page.tsx           # Application tracking list with filtering; Trash tab
│ │ ├── ApplicationsClient.tsx # Applications list client component
│ │ └── [id]/page.tsx      # Application detail view
│ ├── runs/[id]/page.tsx   # Scrape run detail + error log
│ ├── settings/
│ │ ├── page.tsx           # Route
│ │ └── SettingsClient.tsx # Settings: model selector, MaxTokens, resume upload, template upload
│ └── api/
│   ├── [...path]/route.ts # Proxy to backend API (API_INTERNAL_URL)
│   └── events/route.ts    # SSE relay to backend /api/events
├── components/
│ ├── boards/
│ │ ├── AddBoardForm.tsx          # URL, name, cron registration form
│ │ └── ScraperConfigView.tsx     # Editable JSON view of AI-generated config
│ ├── ads/
│ │ ├── AdTable.tsx               # Paginated ad table; expandable scoring panel; bulk actions; virtual scrolling
│ │ ├── FilterBar.tsx             # Reusable filter UI with tri-state include/exclude logic
│ │ ├── TrashTable.tsx            # Trash bin table with bulk restore/delete actions
│ │ └── NewJobAdModal.tsx         # Modal for creating or editing a job ad
│ ├── applications/
│ │ ├── ApplicationTable.tsx          # Application list with filtering
│ │ ├── ApplicationTrashTable.tsx     # Trashed applications
│ │ ├── ApplicationStatusTimeline.tsx # Visual status workflow timeline with date editing
│ │ └── StatusBadge.tsx               # Colored badge for ApplicationStatus values
│ └── ui/
│   ├── Badge.tsx           # Status badges (board status, run status)
│   ├── Button.tsx          # Unified button with variants and loading states
│   ├── Card.tsx            # Container with CardHeader/CardBody
│   ├── EmptyState.tsx      # Empty placeholder with optional action
│   ├── LoadingSpinner.tsx  # Centered spinner
��   ├── NavJobAdsLink.tsx   # Nav link with live unread count badge
│   ├── RichTextEditor.tsx  # WYSIWYG editor for generated HTML resumes and letters
│   ├── SSEProvider.tsx     # Global SSE listener; triggers query invalidations
│   ├── Tooltip.tsx         # Unified styled tooltip component (replaces native title= and inline group-hover patterns)
│   └── WorkcastLogo.tsx    # Logo/branding with Hangfire processing indicator
├── lib/
│ ├── api.ts               # Typed API client (fetch wrapper, all resources)
│ └── hooks/
│   ├── useJobBoards.ts
│   ├── useJobAds.ts
│   ├── useScrapeRuns.ts
│   ├── useAdScoring.ts
│   ├── useSettings.ts
│   ├── useApplications.ts
│   ├── useFilterState.ts        # Filter persistence via localStorage
│   ├── useSSE.ts                # SSE connection hook
│   └── useProcessingStatus.ts  # Polls /api/status for Hangfire queue state and unread count
└── types/
    └── index.ts            # TypeScript interfaces matching all API DTOs
```

Note: `NoteModal.tsx` was removed (the Note feature was removed from JobAd). `NewJobAdModal.tsx` replaces it and handles both manual ad creation and editing.

**9.2 Key Pages**

|                              |                                                                                                                                                                                                                       |
|------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Page**                     | **Functionality**                                                                                                                                                                                                     |
| Boards List /boards          | Lists all boards with status badge, last scraped time, ad count. "Add Board" button opens inline form (URL, name, cron). Status polling for pending boards. Secondary tab shows recent scrape runs.                    |
| Board Detail /boards/\[id\]  | Displays name, URL, status, cron schedule (editable), scraper_config (collapsible, editable JSON view). Buttons: Scrape Now, Re-analyze, Pause/Resume, Delete. Recent run history table.                              |
| Board Ads /boards/\[id\]/ads | Paginated ad table for this board. Active/inactive filter. Mark All Read button. Columns: title, company, location, salary_raw, scraped_at, link.                                                                     |
| Global Ads /ads              | Same as above but across all boards. Adds full FilterBar (board, status, title, location, company, score). Trash Bin secondary tab. Expandable rows show scoring panel. Manual ad creation and editing. Bulk selection in Trash Bin. |
| Applications /applications   | Lists tracked applications with filtering (title, location, company, score). Status chips and urgency indicators. Create application from ad. Trash Bin secondary tab.                                                |
| Application Detail /applications/\[id\] | Full application record: status timeline, job ad tab (full page content), scoring tab, resume generation (AI + WYSIWYG editor, versioned sidebar), letter generation (AI + WYSIWYG editor, versioned sidebar). |
| Run Detail /runs/\[id\]      | Shows run metadata, pages scraped, ads found/new, and error log (if any) with page URL and error message.                                                                                                             |
| Settings /settings           | Configure model IDs and MaxTokens per operation. Upload/delete resume file and HTML resume template. Sample file download links.                                                                                       |
| Hangfire Dashboard /hangfire | Exposed directly from the API container. Linked from the admin nav.                                                                                                                                                   |
| Grafana /grafana (port 3001) | Pre-provisioned performance dashboard for Hangfire metrics, HTTP latency, and PostgreSQL stats.                                                                                                                        |

**9.3 Real-Time Updates**

The frontend uses a combination of SSE push and client-side polling:

- **SSE (primary):** A persistent `GET /api/events` connection is maintained by `SSEProvider`. On receiving a named event, the provider invalidates the relevant TanStack Query cache keys, triggering automatic refetches.

  | **Event**                               | **Queries Invalidated**                     |
  |-----------------------------------------|---------------------------------------------|
  | boardStatusChanged                      | boards                                      |
  | runEnqueued / runStarted / runStatusChanged / runCompleted | boards, scrapeRuns, jobAds, status |
  | unreadCountChanged                      | status                                      |
  | scoringCompleted                        | adScoring for the specific adId             |
  | applicationScoringCompleted             | application for the specific applicationId  |
  | applicationResumeGenerationCompleted    | application for the specific applicationId  |
  | applicationLetterGenerationCompleted    | application for the specific applicationId  |

- **Polling (fallback):** Used for boards still in `pending` status (every 3 seconds) and for active runs showing `running` status (every 5 seconds), in case an SSE event is missed.

- **Processing indicator:** `useProcessingStatus` polls `GET /api/status` every 5 seconds to show a global activity indicator while Hangfire jobs are running, and to keep the unread count badge up to date.

**9.4 Filter System**

The `FilterBar` component is shared between the Global Ads page and the Applications page. It provides:

- **Multi-value filters** with tri-state logic: include (show only matching) / exclude (hide matching) / off
- **Typeahead inputs** for title, location, and company backed by `/api/job-ads/distinct-*` endpoints
- **Score slider** for minimum relevance score filtering
- **Status chips** for IsActive, IsRead, IsPinned
- **Enable/disable toggle** to temporarily suspend all active filters without clearing them
- Filter state is persisted to `localStorage` via `useFilterState` hook

**10. Claude API Cost Analysis**

**10.1 Call Volume Model**

Assumptions: 20 job boards, each refreshed hourly. All extraction is deterministic after board analysis — no AI calls are made during scrape runs. Ad scoring, resume generation, and letter generation are on-demand only.

|                       |                 |                                                         |
|-----------------------|-----------------|---------------------------------------------------------|
| **Phase**             | **Calls / Day** | **Breakdown**                                           |
| Board setup (one-off) | 20 total        | 1 analysis call per board                               |
| Recurring scrapes     | 0               | No AI per scrape run — CSS extraction only              |
| Ad scoring            | User-driven     | 1 call per ad scored (triggered manually)               |
| Resume generation     | User-driven     | 1 call per generation (triggered per application)       |
| Letter generation     | User-driven     | 1 call per generation (triggered per application)       |
| Self-heal (rare)      | ~1–2 total      | Only when a board redesigns its listing page structure  |

**10.2 Cost Estimate**

Based on claude-sonnet-4-5 pricing (~\$3 per million input tokens) and an estimated average of 2,000 tokens per board analysis call after HTML pre-processing:

|                       |                            |
|-----------------------|----------------------------|
| **Phase**             | **Estimated Cost**         |
| Board setup (20)      | ~\$0.12 total (negligible) |
| Steady state          | ~\$0.00/day                |
| Monthly steady state  | ~\$0.12 total (one-off)    |

Ad scoring, resume, and letter generation costs are negligible per call (compact inputs) and fully user-controlled.

```
Key architectural decision: listing-page-only extraction
By generating field-level CSS selectors during board analysis, all subsequent
scrape runs require zero AI calls. Claude is only invoked once per board (on
registration or re-analysis), and optionally per ad/application when the user
requests scoring, resume generation, or letter generation.
This reduces operational AI cost to near-zero regardless of scrape frequency or ad volume.
```

**11. Non-Functional Requirements**

**11.1 Performance**

- API response time: \< 200ms for read endpoints (list, get)

- Board analysis: async — user receives 202 immediately, analysis completes within 30–60 seconds

- Scrape run: no hard SLA, but should complete within the scheduling interval (default 1 hour)

- Ad scoring: async — user triggers via UI, result available within 10–30 seconds

- Resume/letter generation: async — result available within 15–60 seconds depending on model and token budget

**11.2 Reliability**

- Hangfire retries failed jobs up to 3 times with exponential backoff

- Playwright page load failures are caught, logged to ScrapeRun.Errors, and do not abort the entire run

- Claude API call failures retry 3 times before marking the operation as failed

- A single ad extraction failure does not stop the processing of remaining ads in a run

- SSE client reconnects automatically on connection drop (standard EventSource behavior)

- Recurring Hangfire job registrations are re-applied on every API startup to prevent signature drift

**11.3 Maintainability**

- AI provider is fully abstracted — adding a new provider requires only implementing IAiProvider and updating DI registration

- AI models and token budgets are configurable at runtime via AppSettings — no code deployment needed to switch models

- ScraperConfig (including field selectors) is stored as JSONB on JobBoard — triggering re-analysis regenerates it without any code change

- Self-healing is structural (selector yields 0 results) rather than probabilistic, making it deterministic and observable in logs

**11.4 Security (Local Dev Scope)**

- API keys stored in .env file, never committed to source control

- .env.example committed with placeholder values only

- volumes/ directory gitignored

- Hangfire dashboard has no authentication in local dev — add middleware auth before any non-local deployment

- Resume files stored as bytes in the database; access is not authenticated in v1 (single-user local dev scope)

**12. Implementation Phases**

The recommended build order minimises integration risk by establishing the data layer first, then layering capabilities incrementally.

**Phase 1 — Foundation**

- **Goal:** Working API with CRUD, database, and Docker up

- PostgreSQL + EF Core setup, AppDbContext, all entities and migrations

- JobBoardsController — POST, GET, PATCH, DELETE

- JobAdsController — GET (list + single)

- ScrapeRunsController — GET

- docker-compose.yml with db and api services

- EF Core auto-migration on startup

- Swagger/Swashbuckle documentation

**Phase 2 — Playwright Integration**

- **Goal:** Render any URL and get clean HTML back

- IScraperEngine interface + PlaywrightScraperEngine implementation (with stealth headers)

- HTML cleaning pipeline (strip scripts, styles, SVGs, collapse whitespace)

- Playwright DI registration, browser lifecycle management

- `shm_size: '256mb'` in docker-compose for the API service

- Integration test: render a known URL, assert HTML returned

**Phase 3 — AI Integration**

- **Goal:** Analyze a board URL and extract job ads with Claude

- IAiProvider interface (AnalyzeBoardAsync + ScoreAdAsync + GenerateResumeAsync + GenerateLetterAsync)

- ClaudeAiProvider — AnalyzeBoardAsync with tool use schema (includes field_selectors)

- BoardAnalysisJob implementation

- AiExtractionService — orchestrates HTML cleaning + board analysis

- C# records: BoardAnalysisResult (with FieldSelectors), ScraperConfig updated

- Integration test: analyze a real job board URL, assert field selectors returned

**Phase 4 — Scraping Pipeline**

- **Goal:** Full scrape run end-to-end with deduplication

- ScrapeJobRunner — listing-only pipeline: render listing page → CSS-extract all cards → dedup → persist

- Deterministic field extraction using field_selectors from ScraperConfig

- Deduplication logic (URL normalisation, ExternalId check)

- ScrapeRun tracking (counts, errors, status)

- Self-healing trigger: 0 cards matched on first page → enqueue BoardAnalysisJob

- Stale ad detection (IsActive flag)

**Phase 5 — Hangfire Scheduling**

- **Goal:** Automated recurring scrapes with manual override

- Hangfire setup with PostgreSQL storage

- HangfireJobScheduler — AddOrUpdate, Remove, Enqueue helpers

- Recurring job registration on board activation; re-registration on startup

- Manual refresh endpoint (POST /api/job-boards/{id}/refresh)

- Re-analyze endpoint (POST /api/job-boards/{id}/reanalyze)

- Hangfire dashboard exposed at /hangfire

**Phase 6 — Ad Lifecycle & User Features**

- **Goal:** Full ad management workflow (read, pin, trash, bulk actions, manual creation)

- JobAd fields: IsRead, IsPinned, IsTrashed, IsScoringPending, IsManual, LastScoringError

- PATCH endpoints: pin, unpin, read, unread, trash, restore

- POST /api/job-ads (manual creation), PATCH /api/job-ads/{id} (edit)

- POST /api/job-ads/bulk/* for per-action bulk operations

- Distinct-values endpoints for filter typeahead

- AdCleanupJob — 30-day auto-delete of trashed ads

**Phase 7 — AI Scoring**

- **Goal:** Per-ad and per-application relevance scoring against user resume

- AppSettings entity + SettingsController

- Resume upload/delete endpoints (stored as bytes in DB, not filesystem)

- Resume template upload/delete endpoints

- ClaudeAiProvider.ScoreAdAsync with updated tool use schema (ScoringRequirement model)

- AdScoringJob fire-and-forget background job

- ApplicationScoringJob — uses stored JobAdContent, falls back to Playwright fetch

- ScoringPipeline — shared scoring logic reused by both job types

- AdScoringController endpoints

- IsScoringPending / LastScoringError lifecycle on both JobAd and Application

**Phase 8 — Applications, Resume & Letter Generation**

- **Goal:** Full application tracking workflow with AI-generated documents

- Application entity (self-contained, denormalized scoring, status history)

- GeneratedResume and GeneratedLetter entities with sequential versioning

- ApplicationsController — CRUD, status tracking, scoring, resume/letter generation endpoints

- ClaudeAiProvider.GenerateResumeAsync and GenerateLetterAsync

- ApplicationResumeGenerationJob and ApplicationLetterGenerationJob

- SSE: expanded event types for all async job completions

- SSEProvider in frontend, NavJobAdsLink unread badge

- useSSE hook and query invalidation on all event types

**Phase 9 — Next.js Frontend**

- **Goal:** Usable UI for the complete feature set

- Typed API client (lib/api.ts)

- TanStack Query hooks for all entities

- Boards List, Board Detail, Board Ads pages

- Global Ads page with FilterBar (tri-state, typeahead, score slider), Trash Bin tab, NewJobAdModal

- Applications page with filtering, Trash Bin tab, status chips, urgency indicators

- Application Detail page: status timeline, job ad content tab, scoring tab, resume tab (WYSIWYG + versioned sidebar), letter tab (WYSIWYG + versioned sidebar)

- Settings page (model selector, MaxTokens per operation, resume + template management, sample downloads)

- Run detail page with error log

- Tooltip unified component (replaces native title= and inline group-hover patterns)

- web.Dockerfile and docker-compose service

**Phase 10 — Performance Monitoring**

- **Goal:** Observability for scrape run latency and AI cost tracking

- HangfireMetricsService — Prometheus gauges for Hangfire queue depths

- prometheus-net.AspNetCore package — HTTP metrics on /metrics endpoint

- Prometheus service in docker-compose (scrapes API and postgres-exporter)

- postgres-exporter service in docker-compose

- Grafana service in docker-compose with pre-provisioned workcast dashboard

- External volumes for Prometheus and Grafana state

**Phase 11 — Interview Drill**

- **Goal:** AI-generated interview preparation tied to each application's scoring analysis

- `InterviewDrillPlan` entity — one plan per application, questions stored as JSONB array. Unique constraint on `ApplicationId`; regenerating replaces the previous plan (delete-then-insert).

- `InterviewQuestion` JSONB object — `OrderIndex`, `Text`, `Category` (warm_up / easy / medium / challenging), `RequirementName`, `Answer`, `AnsweredAt`

- `IInterviewDrillRepository` — `GetByApplicationIdAsync`, `UpsertAsync`, `SaveAnswerAsync` (patches a single question answer in the JSONB array without replacing the full plan)

- `InterviewDrillJob` — Hangfire background job; calls Claude AI to generate 15–20 tailored questions based on application scoring data and resume

- `ApplicationsController` — four interview drill endpoints (generate, cancel, get, save answer)

- `InterviewDrillTab` frontend component — plan overview with category breakdown bar; drill mode with per-question answer textarea, speech-to-text (browser `SpeechRecognition` API), and text-to-speech (`SpeechSynthesis` API)

- TTS auto-play toggle — enabled by default on Edge/Windows (Microsoft Neural voices) and Chrome/Safari on macOS (Apple system voices); disabled by default on Chrome/Windows and Firefox

- Browser tips panel — warns about Chrome on Windows TTS quality (suggest Edge) and Firefox STT unavailability

- Answers are persisted per-question on save/navigate/exit; regenerating the drill clears all answers

**Phase 12 — Interview Steps**

- **Goal:** User-managed log of interview rounds within an application, independent of AI features

- `InterviewStep` entity — one row per interview round, ordered by `StepNumber`. Fields: date, time (24-hour), duration (minutes), timezone (default CEST), on-site/remote flag, remote call link, interviewers (JSONB array of `{ name, jobFunction }`), and free-form notes

- `InterviewStepConfiguration` EF Core configuration — maps to `interview_steps` table, JSONB for `Interviewers`, cascade delete from `Application`

- Renumbering on delete — when a step is deleted the controller reloads remaining steps ordered by `StepNumber` and reassigns 1, 2, 3, … before saving, keeping the sequence contiguous

- `ApplicationsController` — four interview steps endpoints (list, create, update, delete)

- `InterviewStepsTab` frontend component — add button always on top; steps displayed newest-first (reversed); the next upcoming step (earliest future or today date) is highlighted with an indigo card and "Next" badge; delete confirmation modal; inline edit form

**13. Open Questions & Future Considerations**

**13.1 Decisions Deferred to Engineering**

|                        |                                                                                                                                          |
|------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| **Topic**              | **Question**                                                                                                                             |
| Playwright concurrency | Single browser instance is safe but slow for many boards. Consider a browser pool if scrape run duration becomes a problem.              |
| HTML cleaning depth    | The cleaning pipeline targets 10,000 tokens. Measure actual token usage on real boards and tune accordingly.                             |
| Max pages safety cap   | ScraperConfig includes max_pages. Define a global hard cap (e.g. 100 pages) as a fallback if AI does not set one.                        |
| Stale ad window        | Current stale detection marks ads inactive after a single run where they don't appear. A multi-run window may be more robust.            |
| Resume storage         | Resume file is stored as bytes in the database. For very large resumes, consider an external volume or blob storage.                     |
| SSE reconnect backoff  | The browser's native EventSource reconnects immediately. A backoff strategy should be added before production use.                       |

**13.2 Future Enhancements (Out of v1 Scope)**

- Redis — for distributed locks to prevent concurrent scrape runs on the same board

- User authentication — JWT-based auth to support multi-tenancy

- Webhook / email alerts — notify when new ads match saved search criteria

- Full-text search — PostgreSQL tsvector on title + description, or Elasticsearch

- Proxy rotation — for boards that rate-limit or block repeated requests

- Ad deduplication across boards — detect the same ad posted on multiple boards

- Saved searches — persist filter configurations and get notified on new matches
