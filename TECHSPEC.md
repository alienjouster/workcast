**Workcast Platform**

Technical Specification Document

|             |                                |
|-------------|--------------------------------|
| **Version** | 1.2                            |
| **Status**  | Updated — Post-Implementation  |
| **Date**    | March 25, 2026                 |
| **Author**  | Solutions Architecture         |

*CONFIDENTIAL — INTERNAL USE ONLY*

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

Beyond scraping, the platform supports a full job-search workflow: AI-powered relevance scoring of ads against a user resume, personal notes and pinning, application tracking, and a read/trash lifecycle for managing the ad inbox.

**Core value proposition:** A user provides a URL. The system handles everything else.

**1.3 Scope**

This specification covers:

- REST API backend built with .NET 10

- AI-powered scraping engine using Claude API (Anthropic)

- AI-powered job ad scoring engine using Claude API (Anthropic)

- JavaScript-rendering layer using Microsoft Playwright

- Background job processing with Hangfire

- Next.js frontend application

- Full local Docker Compose deployment with persistent external volumes

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

The platform is composed of three Docker services communicating over a private network, with data persisted to external volumes mounted on the host machine.

```
┌────────────────────────────────────────────────────────────┐
│                        Docker Network                      │
│                                                            │
│  ┌──────────────┐    ┌─────────────────────────────────┐   │
│  │   Next.js    │ ─▶│           .NET 10 API           │   │
│  │  (port 3000) │    │           (port 8080)           │   │
│  └──────────────┘    │  ┌─────────────────────────┐    │   │
│       SSE ◀──────────┤  │   Hangfire Scheduler    │    │   │
│                      │  │   Playwright Engine     │    │   │
│                      │  │   AI Extraction Service │    │   │
│                      │  │   Ad Scoring Service    │    │   │
│                      │  └─────────────────────────┘    │   │
│                      └──────────────┬──────────────────┘   │
│                                     │                      │
│                      ┌──────────────▼──────────────────┐   │
│                      │         PostgreSQL 16           │   │
│                      │          (port 5432)            │   │
│                      └─────────────────────────────────┘   │
│                                                            │
└────────────────────────────────────────────────────────────┘
                              │
              External Volume (host filesystem)
              /volumes/postgres, /volumes/playwright
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

**2.3 Solution Structure**

The backend follows Clean Architecture with four projects. The frontend is a separate Next.js application.

```
Workcast.sln
├── src/
│ ├── Workcast.Core/          # Domain layer — entities, enums, value objects, interfaces, events
│ ├── Workcast.Infrastructure/ # Infrastructure layer — EF Core, Playwright, Claude AI, Hangfire, SSE
│ ├── Workcast.Jobs/          # Background jobs — board analysis, scraping, scoring, cleanup
│ └── Workcast.Api/           # Presentation layer — controllers, DTOs, mapping, Program.cs
│
├── web/                      # Next.js 14 frontend (App Router, TanStack Query, Tailwind)
│
└── docker/                   # Docker Compose, Dockerfiles, .env.example
```

**3. Data Model**

**3.1 Entity Relationship Overview**

```
AppSettings (singleton)

JobBoard 1 ──────< ScrapeRun
         1 ──────< JobAd
ScrapeRun 1 ──────< JobAd (optional fk, tracks which run discovered each ad)
JobAd     1 ──────< AdScoring (optional, generated on demand)
Application (independent; optionally linked to JobAd via nullable FK)
Application 1 ──────< AdScoring (optional, same scoring model)
```

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
| JobBoardId        | UUID — FK → JobBoard.Id, CASCADE DELETE                          |
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
| Note              | TEXT — Nullable, personal note written by the user per ad        |

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

AI-generated relevance score for a single job ad or application, computed on demand against the user's uploaded resume.

|                   |                                                                          |
|-------------------|--------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                           |
| Id                | UUID — Primary Key                                                       |
| JobAdId           | UUID — FK → JobAd.Id, CASCADE DELETE (nullable if attached to Application)|
| ApplicationId     | UUID — FK → Application.Id, CASCADE DELETE (nullable if attached to ad)  |
| OverallScore      | INTEGER — 0–100, AI-computed relevance score                             |
| Recommendation    | VARCHAR(50) — e.g. "strong match", "partial match", "not recommended"    |
| Summary           | TEXT — AI-generated summary of fit                                       |
| Requirements      | JSONB — Array of { requirement, met (bool), explanation } breakdowns     |
| CreatedAt         | TIMESTAMPTZ — NOT NULL, default NOW()                                    |

**3.6 Application**

User-created record tracking their application to a job. Mirrors key JobAd fields to remain self-contained even if the source ad is later deleted or deactivated.

|                   |                                                                           |
|-------------------|---------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                            |
| Id                | UUID — Primary Key                                                        |
| JobAdId           | UUID — FK → JobAd.Id, SET NULL (nullable) — source ad                    |
| Title             | VARCHAR(512) — Nullable, mirrored from JobAd at creation time             |
| Company           | VARCHAR(255) — Nullable, mirrored from JobAd                              |
| Location          | VARCHAR(255) — Nullable, mirrored from JobAd                              |
| SalaryRaw         | VARCHAR(255) — Nullable, mirrored from JobAd                              |
| Description       | TEXT — Nullable, mirrored from JobAd                                      |
| IsTrashed         | BOOLEAN — default FALSE, soft-delete flag                                 |
| CreatedAt         | TIMESTAMPTZ — NOT NULL, default NOW()                                     |
| UpdatedAt         | TIMESTAMPTZ — NOT NULL, updated via EF Core interceptor                   |

**3.7 AppSettings**

Singleton settings record for the user's global preferences. Only one row exists in the table.

|                   |                                                                             |
|-------------------|-----------------------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                                              |
| Id                | UUID — Primary Key (singleton, single known row)                            |
| BoardAnalyzerModel| VARCHAR(100) — Claude model ID used for board analysis                      |
| ScoringModel      | VARCHAR(100) — Claude model ID used for ad scoring                          |
| HasResume         | BOOLEAN — default FALSE, TRUE when a resume has been uploaded               |
| ResumeFileName    | VARCHAR(255) — Nullable, original filename of the uploaded resume           |
| ResumeUploadedAt  | TIMESTAMPTZ — Nullable, timestamp of last successful upload                 |

**3.8 ScraperConfig JSON Schema**

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

**3.9 Indexes**

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

    // Called on demand per ad to score its relevance against the user's resume.
    // Returns a structured scoring result with overall score and per-requirement breakdown.
    Task<AdScoringResult> ScoreAdAsync(
        string adContent,
        string resumeContent,
        CancellationToken ct = default);
}
```

The active Claude models for each operation are configurable via `AppSettings.BoardAnalyzerModel` and `AppSettings.ScoringModel`, read from the database at runtime. This allows the user to switch models via the Settings UI without a code deployment.

**4.2 Structured Output via Tool Use**

To guarantee consistent response schemas, all AI operations use the Claude Tool Use API. Claude is instructed to call a specific tool with a fixed schema — it cannot return free-form JSON. The tool input is then deserialized directly into the corresponding C# record.

```
Why Tool Use instead of a JSON prompt?
A plain "return JSON" instruction yields inconsistent field names, missing required fields, and schema drift between calls. Tool Use forces Claude to populate a pre-defined schema exactly — the response is always machine-readable and deserializable without defensive patching.
```

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

The ad scorer is called on demand per ad (triggered by the user or by `AdScoringJob`). It receives the ad's text content and the user's resume text, and returns a structured relevance assessment.

```
Tool name: "save_ad_score"
{
"type": "object",
"required": ["overall_score", "recommendation", "summary", "requirements"],
"properties": {
  "overall_score":   { "type": "integer", "minimum": 0, "maximum": 100 },
  "recommendation":  { "type": "string" },
  "summary":         { "type": "string" },
  "requirements": {
    "type": "array",
    "items": {
      "type": "object",
      "required": ["requirement", "met", "explanation"],
      "properties": {
        "requirement": { "type": "string" },
        "met":         { "type": "boolean" },
        "explanation": { "type": "string" }
      }
    }
  }
}
}
```

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

7.  Cleaning is applied to listing pages sent to `AnalyzeBoardAsync` only. No cleaning is needed per ad since there are no per-ad AI calls.

Target: under 10,000 tokens per API call after cleaning.

**4.7 Self-Healing**

Since there are no per-ad AI calls, there are no per-ad confidence scores. Self-healing is triggered structurally: if `job_card_selector` matches zero elements on the first listing page of a scrape run, the system automatically enqueues a new `BoardAnalysisJob` to regenerate the `ScraperConfig`. This indicates the board's HTML structure has changed and the selectors are stale.

The `JobBoard` status is set to `error` if the re-analysis also fails.

**4.8 Claude API Configuration**

|               |                                                                      |
|---------------|----------------------------------------------------------------------|
| **Parameter** | **Value**                                                            |
| Model         | Configurable per operation via AppSettings (board analyzer / scorer) |
| Max Tokens    | 1,024 (tool use responses are compact)                               |
| Temperature   | 0 (deterministic structured output)                                  |
| tool_choice   | { "type": "tool", "name": "\<tool_name\>" } (force tool call)       |
| Timeout       | 30 seconds per call                                                  |
| Retry Policy  | 3 attempts with exponential backoff (1s, 2s, 4s)                    |

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
│ ├─ Playwright renders current page URL
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
| User agent           | Standard Chromium UA — no spoofing                             |
| Viewport             | 1280 × 800 (prevents mobile layouts)                           |
| Concurrency          | Single browser instance, sequential page processing            |
| Browser binary cache | Mounted external Docker volume (avoids re-download on rebuild) |

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

**6.3 Job Ads Endpoints**

|                                      |                                                                                                                        |
|--------------------------------------|------------------------------------------------------------------------------------------------------------------------|
| **Endpoint**                         | **Description**                                                                                                        |
| GET /api/job-ads                     | List ads. Supports: ?board_ids= (multi), ?titles= (multi, include/exclude), ?locations= (multi), ?companies= (multi), ?is_active=, ?is_read=, ?is_pinned=, ?min_score=, ?trashed=, ?cursor=, ?limit= |
| GET /api/job-ads/{id}                | Get a single ad with full fields including scoring if available.                                                       |
| DELETE /api/job-ads/{id}             | Hard delete a single ad (typically used after trashing).                                                               |
| PATCH /api/job-ads/{id}/pin          | Pin the ad (IsPinned = true).                                                                                          |
| PATCH /api/job-ads/{id}/unpin        | Unpin the ad (IsPinned = false).                                                                                       |
| PATCH /api/job-ads/{id}/read         | Mark ad as read (IsRead = true).                                                                                       |
| PATCH /api/job-ads/{id}/unread       | Mark ad as unread (IsRead = false).                                                                                    |
| PATCH /api/job-ads/{id}/trash        | Soft-delete the ad (IsTrashed = true).                                                                                 |
| PATCH /api/job-ads/{id}/restore      | Restore a trashed ad (IsTrashed = false).                                                                              |
| PATCH /api/job-ads/{id}/note         | Set or clear a personal note. Accepts { note: string \| null }.                                                        |
| POST /api/job-ads/bulk               | Bulk action on a set of ad IDs. Accepts { ids: uuid[], action: pin\|unpin\|read\|unread\|trash }.                      |
| POST /api/job-ads/mark-all-read      | Mark all visible (non-trashed) ads as read for a given board_id or globally.                                           |
| GET /api/job-ads/unread-count        | Returns the count of unread, non-trashed ads. Used for the nav badge.                                                  |
| GET /api/job-ads/distinct-titles     | Returns distinct title values for filter typeahead. Supports ?board_id=.                                               |
| GET /api/job-ads/distinct-locations  | Returns distinct location values for filter typeahead.                                                                 |
| GET /api/job-ads/distinct-companies  | Returns distinct company values for filter typeahead.                                                                  |

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

|                                       |                                                                                                     |
|---------------------------------------|-----------------------------------------------------------------------------------------------------|
| **Endpoint**                          | **Description**                                                                                     |
| GET /api/applications                 | List applications. Supports: ?titles=, ?locations=, ?companies=, ?min_score=, ?trashed=, ?cursor=, ?limit= |
| GET /api/applications/{id}            | Get a single application with scoring if available.                                                 |
| POST /api/applications                | Create an application. Accepts { job_ad_id? } — mirrors fields from the source ad if provided.     |
| PATCH /api/applications/{id}/trash    | Soft-delete an application.                                                                         |
| PATCH /api/applications/{id}/restore  | Restore a trashed application.                                                                      |
| DELETE /api/applications/{id}         | Hard delete an application.                                                                         |
| GET /api/applications/distinct-titles     | Distinct title values for filter typeahead.                                                     |
| GET /api/applications/distinct-locations  | Distinct location values for filter typeahead.                                                  |
| GET /api/applications/distinct-companies  | Distinct company values for filter typeahead.                                                   |

**6.7 Settings Endpoints**

|                               |                                                                                                              |
|-------------------------------|--------------------------------------------------------------------------------------------------------------|
| **Endpoint**                  | **Description**                                                                                              |
| GET /api/settings             | Get current user settings (model choices, resume status).                                                    |
| PATCH /api/settings           | Update settings. Accepts { board_analyzer_model?, scoring_model? }.                                          |
| PUT /api/settings/resume      | Upload a resume file. Stored server-side; used as input to ad scoring. Returns 200 on success.               |
| DELETE /api/settings/resume   | Delete the stored resume. Sets HasResume = false.                                                            |

**6.8 Server-Sent Events Endpoint**

|                |                                                                                                                 |
|----------------|-----------------------------------------------------------------------------------------------------------------|
| **Endpoint**   | **Description**                                                                                                 |
| GET /api/events | SSE stream. The client holds a persistent connection. The server pushes named events as domain activity occurs. |

**SSE Event Types:**

| **Event Name**  | **Payload**                                | **When Fired**                                       |
|-----------------|--------------------------------------------|------------------------------------------------------|
| runCompleted    | { boardId, runId, adsFound, adsNew }       | When a ScrapeJobRunner finishes (success or partial)  |
| adsExtracted    | { boardId, count }                         | Batch of new ads persisted during a scrape run        |
| scoringComplete | { adId }                                   | When AdScoringJob finishes for an ad                  |

**6.9 Status Endpoint**

|                    |                                                                                              |
|--------------------|----------------------------------------------------------------------------------------------|
| **Endpoint**       | **Description**                                                                              |
| GET /api/status    | Returns { is_processing: bool } — TRUE if any Hangfire jobs are currently enqueued/running. |

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

**7.2 Job Types**

|                    |                 |                                                                                                     |
|--------------------|-----------------|-----------------------------------------------------------------------------------------------------|
| **Job Class**      | **Type**        | **Description**                                                                                     |
| BoardAnalysisJob   | Fire-and-forget | Runs once on board registration. Analyzes the board and stores ScraperConfig.                       |
| ScrapeJobRunner    | Recurring       | Runs on board's cron schedule. Executes full scrape pipeline.                                       |
| ScrapeJobRunner    | Fire-and-forget | Triggered by manual refresh endpoint. Same logic, different trigger source.                         |
| AdScoringJob       | Fire-and-forget | Triggered on-demand per ad or application. Calls AI scorer → stores AdScoring record.               |
| AdCleanupJob       | Recurring       | Runs daily. Hard-deletes ads that have been in IsTrashed=true state for more than 30 days.          |
| StaleRunCleanupJob | Recurring       | Runs periodically. Removes old ScrapeRun records beyond the configured retention window.            |

**7.3 Schedule Management**

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
```

**7.4 Default Schedule**

Every new job board is assigned a default cron of "0 \* \* \* \*" (every hour, on the hour). The user can override this per board via the PATCH endpoint or the frontend UI.

**8. Docker & Infrastructure**

**8.1 docker-compose.yml**

```
version: "3.9"
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - ./volumes/postgres:/var/lib/postgresql/data
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
    environment:
      ConnectionStrings__Default: >
        Host=db;Database=${POSTGRES_DB};
        Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
      Anthropic__ApiKey: ${ANTHROPIC_API_KEY}
      Anthropic__Model: ${ANTHROPIC_MODEL}
      PLAYWRIGHT_BROWSERS_PATH: /ms-playwright
      ASPNETCORE_URLS: http://+:8080
    volumes:
      - ./volumes/playwright:/ms-playwright
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
    environment:
      NEXT_PUBLIC_API_URL: http://localhost:8080
      API_INTERNAL_URL: http://api:8080
    ports:
      - "3000:3000"
    depends_on:
      - api
    restart: unless-stopped
```

**8.2 .env.example**

```
# Copy to .env and fill in values. Never commit .env to source control.
POSTGRES_DB=jobscraper
POSTGRES_USER=jobscraper
POSTGRES_PASSWORD=changeme
ANTHROPIC_API_KEY=sk-ant-...
ANTHROPIC_MODEL=claude-sonnet-4-5
```

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
# PLAYWRIGHT_BROWSERS_PATH is set via docker-compose environment.
# On first start (or after volume cleared), browsers are downloaded to the volume.
ENTRYPOINT ["dotnet", "Workcast.Api.dll"]
```

> **Important:** Do not use `apt-get` to install browser system dependencies manually, and do not use `Microsoft.Playwright.CLI` (`playwright install`). The `playwright/dotnet` base image handles both. Never bind-mount over `/ms-playwright` in a way that would shadow the image's own Playwright installation.

**8.4 External Volume Strategy**

|                        |                          |                                                                  |
|------------------------|--------------------------|------------------------------------------------------------------|
| **Volume (host path)** | **Container mount**      | **Contents**                                                     |
| ./volumes/postgres     | /var/lib/postgresql/data | All PostgreSQL data files — boards, ads, runs, Hangfire state    |
| ./volumes/playwright   | /ms-playwright           | Chromium browser binaries (~150MB) — survives container rebuilds |

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
│ ├── ads/page.tsx         # Global ad search/browse with FilterBar; Trash Bin tab
│ ├── applications/
│ │ ├── page.tsx           # Application tracking list with filtering; Trash tab
│ │ └── [id]/page.tsx      # Application detail view
│ ├── runs/[id]/page.tsx   # Scrape run detail + error log
│ ├── settings/page.tsx    # User settings: model selector, resume upload
│ └── api/
│   ├── [...path]/route.ts # Proxy to backend API (API_INTERNAL_URL)
│   └── events/route.ts    # SSE relay to backend /api/events
├── components/
│ ├── boards/
│ │ ├── AddBoardForm.tsx   # URL, name, cron registration form
│ │ └── ScraperConfigView.tsx # Read-only JSON view of AI-generated config
│ ├── ads/
│ │ ├── AdTable.tsx        # Paginated ad table; expandable scoring panel; bulk actions
│ │ ├── FilterBar.tsx      # Reusable filter UI with tri-state include/exclude logic
│ │ ├── TrashTable.tsx     # Trash bin table with restore/delete actions
│ │ └── NoteModal.tsx      # Modal for editing per-ad notes
│ ├── applications/
│ │ ├── ApplicationTable.tsx       # Application list with filtering
│ │ └── ApplicationTrashTable.tsx  # Trashed applications
│ └── ui/
│   ├── Badge.tsx           # Status badges (board status, run status)
│   ├── Button.tsx          # Unified button with variants and loading states
│   ├── Card.tsx            # Container with CardHeader/CardBody
│   ├── EmptyState.tsx      # Empty placeholder with optional action
│   ├── LoadingSpinner.tsx  # Centered spinner
│   ├── NavJobAdsLink.tsx   # Nav link with live unread count badge
│   ├── SSEProvider.tsx     # Global SSE listener; triggers query invalidations
│   └── WorkcastLogo.tsx    # Logo/branding
├── lib/
│ ├── api.ts               # Typed API client (fetch wrapper, all resources)
│ └── hooks/
│   ├── useJobBoards.ts
│   ├── useJobAds.ts
│   ├── useScrapeRuns.ts
│   ├── useAdScoring.ts
│   ├── useSettings.ts
│   ├── useApplications.ts
│   ├── useFilterState.ts   # Filter persistence via localStorage
│   ├── useSSE.ts           # SSE connection hook
│   └── useProcessingStatus.ts  # Polls /api/status for Hangfire queue state
└── types/
    └── index.ts            # TypeScript interfaces matching all API DTOs
```

**9.2 Key Pages**

|                              |                                                                                                                                                                                                                       |
|------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Page**                     | **Functionality**                                                                                                                                                                                                     |
| Boards List /boards          | Lists all boards with status badge, last scraped time, ad count. "Add Board" button opens inline form (URL, name, cron). Status polling for pending boards. Secondary tab shows recent scrape runs.                    |
| Board Detail /boards/\[id\]  | Displays name, URL, status, cron schedule (editable), scraper_config (collapsible JSON view). Buttons: Manual Refresh, Re-analyze, Pause/Resume, Delete. Recent run history table.                                    |
| Board Ads /boards/\[id\]/ads | Paginated ad table for this board. Active/inactive filter. Mark All Read button. Columns: title, company, location, salary_raw, scraped_at, link. The link opens the original job ad URL in a new browser tab.        |
| Global Ads /ads              | Same as above but across all boards. Adds full FilterBar (board, status, title, location, company, score). Trash Bin secondary tab. Expandable rows show scoring panel.                                               |
| Applications /applications   | Lists tracked applications with filtering (title, location, company, score). Create application from ad. Trash Bin secondary tab.                                                                                     |
| Run Detail /runs/\[id\]      | Shows run metadata, pages scraped, ads found/new, and error log (if any) with page URL and error message.                                                                                                             |
| Settings /settings           | Configure boardAnalyzerModel and scoringModel (Claude model IDs). Upload / delete resume file used for scoring.                                                                                                       |
| Hangfire Dashboard /hangfire | Exposed directly from the API container. Linked from the admin nav.                                                                                                                                                   |

**9.3 Real-Time Updates**

The frontend uses a combination of SSE push and client-side polling:

- **SSE (primary):** A persistent `GET /api/events` connection is maintained by `SSEProvider`. On receiving a named event, the provider invalidates the relevant TanStack Query cache keys, triggering automatic refetches.

  | **Event**       | **Queries Invalidated**              |
  |-----------------|--------------------------------------|
  | runCompleted    | boards, scrapeRuns, jobAds, unreadCount |
  | adsExtracted    | jobAds, unreadCount                  |
  | scoringComplete | adScoring for the specific adId      |

- **Polling (fallback):** Used for boards still in `pending` status (every 3 seconds) and for active runs showing `running` status (every 5 seconds), in case an SSE event is missed.

- **Processing indicator:** `useProcessingStatus` polls `GET /api/status` every 5 seconds to show a global activity indicator while Hangfire jobs are running.

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

Assumptions: 20 job boards, each refreshed hourly. All extraction is deterministic after board analysis — no AI calls are made during scrape runs. Ad scoring is on-demand only.

|                       |                 |                                                         |
|-----------------------|-----------------|---------------------------------------------------------|
| **Phase**             | **Calls / Day** | **Breakdown**                                           |
| Board setup (one-off) | 20 total        | 1 analysis call per board                               |
| Recurring scrapes     | 0               | No AI per scrape run — CSS extraction only              |
| Ad scoring            | User-driven     | 1 call per ad scored (triggered manually)               |
| Self-heal (rare)      | ~1–2 total      | Only when a board redesigns its listing page structure  |

**10.2 Cost Estimate**

Based on claude-sonnet-4-5 pricing (~\$3 per million input tokens) and an estimated average of 2,000 tokens per board analysis call after HTML pre-processing:

|                       |                            |
|-----------------------|----------------------------|
| **Phase**             | **Estimated Cost**         |
| Board setup (20)      | ~\$0.12 total (negligible) |
| Steady state          | ~\$0.00/day                |
| Monthly steady state  | ~\$0.12 total (one-off)    |

Ad scoring cost is negligible per call (compact ad text input) and fully user-controlled.

```
Key architectural decision: listing-page-only extraction
By generating field-level CSS selectors during board analysis, all subsequent
scrape runs require zero AI calls. Claude is only invoked once per board (on
registration or re-analysis), and optionally per ad when the user requests scoring.
This reduces operational AI cost to near-zero regardless of scrape frequency or ad volume.
```

**11. Non-Functional Requirements**

**11.1 Performance**

- API response time: \< 200ms for read endpoints (list, get)

- Board analysis: async — user receives 202 immediately, analysis completes within 30–60 seconds

- Scrape run: no hard SLA, but should complete within the scheduling interval (default 1 hour)

- Ad scoring: async — user triggers via UI, result available within 10–30 seconds

**11.2 Reliability**

- Hangfire retries failed jobs up to 3 times with exponential backoff

- Playwright page load failures are caught, logged to ScrapeRun.Errors, and do not abort the entire run

- Claude API call failures retry 3 times before marking the operation as failed

- A single ad extraction failure does not stop the processing of remaining ads in a run

- SSE client reconnects automatically on connection drop (standard EventSource behavior)

**11.3 Maintainability**

- AI provider is fully abstracted — adding a new provider requires only implementing IAiProvider and updating DI registration

- AI models are configurable at runtime via AppSettings — no code deployment needed to switch models

- ScraperConfig (including field selectors) is stored as JSONB on JobBoard — triggering re-analysis regenerates it without any code change

- Self-healing is structural (selector yields 0 results) rather than probabilistic, making it deterministic and observable in logs

**11.4 Security (Local Dev Scope)**

- API keys stored in .env file, never committed to source control

- .env.example committed with placeholder values only

- volumes/ directory gitignored

- Hangfire dashboard has no authentication in local dev — add middleware auth before any non-local deployment

- Resume files stored server-side; access is not authenticated in v1 (single-user local dev scope)

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

- IScraperEngine interface + PlaywrightScraperEngine implementation

- HTML cleaning pipeline (strip scripts, styles, SVGs, collapse whitespace)

- Playwright DI registration, browser lifecycle management

- Volume mount for browser binary cache

- Integration test: render a known URL, assert HTML returned

**Phase 3 — AI Integration**

- **Goal:** Analyze a board URL and extract job ads with Claude

- IAiProvider interface (AnalyzeBoardAsync + ScoreAdAsync)

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

- Recurring job registration on board activation

- Manual refresh endpoint (POST /api/job-boards/{id}/refresh)

- Re-analyze endpoint (POST /api/job-boards/{id}/reanalyze)

- Hangfire dashboard exposed at /hangfire

**Phase 6 — Ad Lifecycle & User Features**

- **Goal:** Full ad management workflow (read, pin, trash, note, bulk actions)

- JobAd fields: IsRead, IsPinned, IsTrashed, IsScoringPending, Note

- PATCH endpoints: pin, unpin, read, unread, trash, restore, note

- POST /api/job-ads/bulk for bulk actions

- GET /api/job-ads/unread-count for nav badge

- Distinct-values endpoints for filter typeahead

- AdCleanupJob — 30-day auto-delete of trashed ads

**Phase 7 — AI Scoring**

- **Goal:** Per-ad relevance scoring against user resume

- AppSettings entity + SettingsController

- Resume upload/delete endpoints

- ClaudeAiProvider.ScoreAdAsync with tool use schema

- AdScoringJob fire-and-forget background job

- AdScoringController endpoints

- IsScoringPending flag lifecycle

**Phase 8 — Applications & SSE**

- **Goal:** Application tracking and real-time UI updates

- Application entity + ApplicationsController

- SSE: EventBroadcaster, IEventBroadcaster interface, EventsController

- SSEProvider in frontend, NavJobAdsLink unread badge

- useSSE hook and query invalidation on events

**Phase 9 — Next.js Frontend**

- **Goal:** Usable UI for the complete feature set

- Typed API client (lib/api.ts)

- TanStack Query hooks for all entities

- Boards List, Board Detail, Board Ads pages

- Global Ads page with FilterBar (tri-state, typeahead, score slider) and Trash Bin tab

- Applications page with filtering and Trash Bin tab

- Settings page (model selector, resume management)

- Run detail page with error log

- SSE integration and real-time cache invalidation

- web.Dockerfile and docker-compose service

**13. Open Questions & Future Considerations**

**13.1 Decisions Deferred to Engineering**

|                        |                                                                                                                                          |
|------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| **Topic**              | **Question**                                                                                                                             |
| Playwright concurrency | Single browser instance is safe but slow for many boards. Consider a browser pool if scrape run duration becomes a problem.              |
| HTML cleaning depth    | The cleaning pipeline targets 10,000 tokens. Measure actual token usage on real boards and tune accordingly.                             |
| Max pages safety cap   | ScraperConfig includes max_pages. Define a global hard cap (e.g. 100 pages) as a fallback if AI does not set one.                        |
| Stale ad window        | Current stale detection marks ads inactive after a single run where they don't appear. A multi-run window may be more robust.            |
| Resume storage         | Resume file is stored on the container filesystem. For durability across container rebuilds, consider an external volume mount.           |
| SSE reconnect backoff  | The browser's native EventSource reconnects immediately. A backoff strategy should be added before production use.                       |

**13.2 Future Enhancements (Out of v1 Scope)**

- Redis — for distributed locks to prevent concurrent scrape runs on the same board

- User authentication — JWT-based auth to support multi-tenancy

- Webhook / email alerts — notify when new ads match saved search criteria

- Full-text search — PostgreSQL tsvector on title + description, or Elasticsearch

- Proxy rotation — for boards that rate-limit or block repeated requests

- Metrics / observability — Prometheus + Grafana for scrape run latency and AI cost tracking

- Ad deduplication across boards — detect the same ad posted on multiple boards

- Saved searches — persist filter configurations and get notified on new matches
