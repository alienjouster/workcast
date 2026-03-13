**Workcast Platform**

Technical Specification Document

|             |                                |
|-------------|--------------------------------|
| **Version** | 1.1                            |
| **Status**  | Draft — For Engineering Review |
| **Date**    | March 13, 2026                 |
| **Author**  | Solutions Architecture         |

*CONFIDENTIAL — INTERNAL USE ONLY*

**Table of Contents**

**1. Introduction**

**1.1 Purpose**

This document defines the complete technical specification for the Workcast Platform — an AI-powered job board aggregation system. It serves as the authoritative reference for the engineering team during implementation and provides enough detail to begin development without further architectural input.

**1.2 Project Overview**

Workcast allows users to register any job board URL. The platform automatically analyzes the target website using AI, generates a scraping configuration, and then scrapes job advertisements on a configurable schedule. No manual selector configuration is required from the user.

**Core value proposition:** A user provides a URL. The system handles everything else.

**1.3 Scope**

This specification covers:

- REST API backend built with .NET 8

- AI-powered scraping engine using Claude API (Anthropic)

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
| AI Provider    | An abstracted service that handles all communication with an LLM API                |
| Hangfire       | An embedded .NET background job framework used for scheduling and queuing           |
| Playwright     | A .NET browser automation library used to render JavaScript-heavy pages             |
| Deduplication  | The process of preventing the same job ad from being stored more than once          |

**2. System Architecture**

**2.1 High-Level Overview**

The platform is composed of three Docker services communicating over a private network, with data persisted to external volumes mounted on the host machine.

```
┌────────────────────────────────────────────────────────────┐
│                        Docker Network                      │
│                                                            │
│  ┌──────────────┐    ┌─────────────────────────────────┐   │
│  │   Next.js    │ ─▶│           .NET 8 API            │   │
│  │  (port 3000) │    │           (port 8080)           │   │
│  └──────────────┘    │  ┌─────────────────────────┐    │   │
│                      │  │   Hangfire Scheduler    │    │   │
│                      │  │   Playwright Engine     │    │   │
│                      │  │   AI Extraction Service │    │   │
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
| Backend API        | .NET 8 — ASP.NET Core Web API                  |
| ORM                | Entity Framework Core 8 + Npgsql provider      |
| Database           | PostgreSQL 16                                  |
| Browser Automation | Microsoft.Playwright for .NET                  |
| Background Jobs    | Hangfire 1.8 (in-process, PostgreSQL storage)  |
| AI Provider        | Anthropic Claude API (claude-sonnet-4-5 model) |
| Frontend           | Next.js 14 (App Router)                        |
| Frontend State     | TanStack Query (React Query v5)                |
| Containerisation   | Docker + Docker Compose v2                     |
| API Documentation  | Scalar / OpenAPI 3                             |

**2.3 Solution Structure (.NET)**

The backend follows Clean Architecture principles with clear separation of concerns.

```
Workcast.sln
├── src/
│ ├── Workcast.Api/ # Entry point, controllers, middleware
│ │ ├── Controllers/
│ │ │ ├── JobBoardsController.cs
│ │ │ └── JobAdsController.cs
│ │ ├── Program.cs
│ │ └── appsettings.json
│ │
│ ├── Workcast.Core/ # Domain entities, interfaces
│ │ ├── Entities/
│ │ │ ├── JobBoard.cs
│ │ │ ├── JobAd.cs
│ │ │ └── ScrapeRun.cs
│ │ ├── Interfaces/
│ │ │ ├── IScraperEngine.cs
│ │ │ ├── IAiProvider.cs
│ │ │ └── IJobBoardAnalyzer.cs
│ │ └── Models/
│ │ ├── BoardAnalysisResult.cs
│ │ └── JobAdExtractionResult.cs
│ │
│ ├── Workcast.Infrastructure/ # All external concerns
│ │ ├── Persistence/
│ │ │ ├── AppDbContext.cs
│ │ │ └── Migrations/
│ │ ├── Scraping/
│ │ │ └── PlaywrightScraperEngine.cs
│ │ ├── AI/
│ │ │ ├── IAiProvider.cs
│ │ │ ├── ClaudeAiProvider.cs
│ │ │ └── AiExtractionService.cs
│ │ └── Scheduling/
│ │ └── HangfireJobScheduler.cs
│ │
│ └── Workcast.Jobs/ # Hangfire job implementations
│ ├── ScrapeJobRunner.cs
│ └── BoardAnalysisJob.cs
│
└── docker/
├── docker-compose.yml
├── .env.example
├── api.Dockerfile
└── web.Dockerfile
```

**3. Data Model**

**3.1 Entity Relationship Overview**

```
JobBoard 1 ──────&lt; ScrapeRun
1 ──────&lt; JobAd
ScrapeRun 1 ──────&lt; JobAd (optional fk, tracks which run discovered each ad)
```

**3.2 JobBoard**

|               |                                                                         |
|---------------|-------------------------------------------------------------------------|
| **Column**    | **Type / Constraints / Notes**                                          |
| Id            | UUID — Primary Key, generated by DB                                     |
| Name          | VARCHAR(255) — Nullable, auto-populated from page title if not provided |
| Url           | VARCHAR(2048) — NOT NULL, the seed URL provided by the user             |
| ScraperConfig | JSONB — Nullable, populated after board analysis completes              |
| ScheduleCron  | VARCHAR(100) — NOT NULL, default "0  0  \* \* \*" (every day at 00:00)  |
| Status        | VARCHAR(50) — ENUM: pending \| active \| paused \| error                |
| LastScrapedAt | TIMESTAMPTZ — Nullable                                                  |
| CreatedAt     | TIMESTAMPTZ — NOT NULL, default NOW()                                   |
| UpdatedAt     | TIMESTAMPTZ — NOT NULL, updated via EF Core interceptor                 |

**3.3 JobAd**

|                   |                                                              |
|-------------------|--------------------------------------------------------------|
| **Column**        | **Type / Constraints / Notes**                               |
| Id                | UUID — Primary Key                                           |
| JobBoardId        | UUID — FK → JobBoard.Id, CASCADE DELETE                      |
| ScrapeRunId       | UUID — FK → ScrapeRun.Id, SET NULL (nullable)                |
| ExternalId        | VARCHAR(512) — Nullable, board-specific identifier for dedup |
| Url               | VARCHAR(2048) — NOT NULL                                     |
| Title             | VARCHAR(512) — Nullable                                      |
| Company           | VARCHAR(255) — Nullable                                      |
| Location          | VARCHAR(255) — Nullable                                      |
| SalaryRaw         | VARCHAR(255) — Nullable, stored as-is from listing page      |
| Description       | TEXT — Nullable, short snippet if visible on listing page    |
| PostedAt          | TIMESTAMPTZ — Nullable                                       |
| ScrapedAt         | TIMESTAMPTZ — NOT NULL, default NOW()                        |
| IsActive          | BOOLEAN — default TRUE, set FALSE if ad disappears           |

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

**3.5 ScraperConfig JSON Schema**

The ScraperConfig column on JobBoard stores a structured object validated against this schema. It is generated by the AI board analysis step and stored verbatim.

```
{
"pagination_type": "url_param" | "next_button" | "infinite_scroll" | "none",
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

The `job_card_selector` replaces the former `job_links_selector`. Instead of selecting only the `<a>` link, it selects the entire card container. All field selectors are evaluated relative to each matched card element, enabling full job ad data extraction from the listing page alone — no detail page visits are required.

**3.6 Indexes**

|                                                                   |                                       |
|-------------------------------------------------------------------|---------------------------------------|
| **Index**                                                         | **Purpose**                           |
| JobAd(JobBoardId, Url) UNIQUE                                     | Primary deduplication key             |
| JobAd(JobBoardId, ExternalId) UNIQUE WHERE ExternalId IS NOT NULL | Secondary dedup using board ID        |
| JobAd(ScrapedAt DESC)                                             | Timeline queries on the ads dashboard |
| JobAd(JobBoardId, IsActive)                                       | Filtered listing queries              |
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
Task&lt;BoardAnalysisResult&gt; AnalyzeBoardAsync(
string html,
string url,
CancellationToken ct = default);
}
```

**4.2 Structured Output via Tool Use**

To guarantee consistent response schemas, both AI operations use the Claude Tool Use API. Claude is instructed to call a specific tool with a fixed schema — it cannot return free-form JSON. The tool input is then deserialized directly into the corresponding C# record.

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
  "enum": ["url_param", "next_button", "infinite_scroll", "none"]
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

**4.4 Job Ad Extraction — Deterministic CSS Extraction (No AI)**

Job ad data is extracted entirely from the listing page using the `field_selectors` map generated during board analysis. For each element matching `job_card_selector`, the scraper evaluates each selector relative to the card element and reads the text content of the matched node. No AI call is made per ad.

This approach means:
- Zero AI calls per scrape run (after initial board analysis)
- Detail pages are never visited; the user opens the ad URL directly in their browser
- If a selector stops matching (board redesigned), self-healing triggers a new board analysis

The `JobAdExtractionResult` model and the `ExtractJobAdAsync` method on `IAiProvider` are removed in this architecture.

**4.5 HTML Pre-Processing (Cost Optimisation)**

Raw Playwright HTML can exceed 500KB (roughly 125,000 tokens). Before sending HTML to Claude, the system applies a cleaning pipeline to reduce token consumption by 5–10x:

1.  Remove all \<script\> tags and their content

2.  Remove all \<style\> tags and their content

3.  Remove all \<svg\> elements

4.  Remove HTML comments

5.  Collapse whitespace (multiple spaces, blank lines)

6.  Strip non-visible attributes (data-\*, aria-\* where not essential)

7.  Cleaning is applied to listing pages sent to `AnalyzeBoardAsync` only. No cleaning is needed per ad since there are no per-ad AI calls.

Target: under 10,000 tokens per API call after cleaning.

**4.6 Self-Healing**

Since there are no per-ad AI calls, there are no per-ad confidence scores. Self-healing is instead triggered structurally: if `job_card_selector` matches zero elements on the first listing page of a scrape run, the system automatically enqueues a new `BoardAnalysisJob` to regenerate the `ScraperConfig`. This indicates the board's HTML structure has changed and the selectors are stale.

The `JobBoard` status is set to `error` if the re-analysis also fails.

**4.7 Claude API Configuration**

|               |                                                               |
|---------------|---------------------------------------------------------------|
| **Parameter** | **Value**                                                     |
| Model         | claude-sonnet-4-5 (balance of quality and cost)               |
| Max Tokens    | 1,024 (tool use responses are compact)                        |
| Temperature   | 0 (deterministic structured output)                           |
| tool_choice   | { "type": "tool", "name": "\<tool_name\>" } (force tool call) |
| Timeout       | 30 seconds per call                                           |
| Retry Policy  | 3 attempts with exponential backoff (1s, 2s, 4s)              |

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
├─ Extract &amp; clean HTML
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
│ └─ PAGINATION: (unchanged)
│ ├─ url_param → increment param, build next URL
│ ├─ next_button → find selector, check disabled, get href
│ ├─ infinite_scroll → single page only (not supported)
│ └─ none → stop
│
├─ Update ScrapeRun (status: "completed", counts)
├─ Update JobBoard.LastScrapedAt
└─ Apply stale detection (ads not seen this run → IsActive = false)
```

No AI calls are made during a scrape run. All extraction is deterministic CSS-based logic using the selectors generated during board analysis. The `suggested_delay_ms` applies between listing pages, not between ads.

**5.3 Deduplication Strategy**

Deduplication is applied before rendering the detail page to avoid unnecessary Playwright and Claude API calls:

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
| PATCH /api/job-boards/{id}          | Update name, schedule_cron, or status (active/paused). Re-registers Hangfire job if cron changes.                                                        |
| DELETE /api/job-boards/{id}         | Soft delete board, cancels Hangfire job. Cascade deletes ads and runs.                                                                                   |
| POST /api/job-boards/{id}/refresh   | Trigger an immediate scrape run (fire-and-forget Hangfire enqueue). Returns 202.                                                                         |
| POST /api/job-boards/{id}/reanalyze | Trigger a new board analysis to regenerate scraper_config. Returns 202.                                                                                  |

**6.3 Job Ads Endpoints**

|                          |                                                                                      |
|--------------------------|--------------------------------------------------------------------------------------|
| **Endpoint**             | **Description**                                                                      |
| GET /api/job-ads         | List ads. Supports: ?board_id=, ?search= (full-text), ?is_active=, ?cursor=, ?limit= |
| GET /api/job-ads/{id}    | Get a single ad with full description and raw_html.                                  |
| DELETE /api/job-ads/{id} | Hard delete a single ad.                                                             |

**6.4 Scrape Runs Endpoints**

|                               |                                                                       |
|-------------------------------|-----------------------------------------------------------------------|
| **Endpoint**                  | **Description**                                                       |
| GET /api/job-boards/{id}/runs | List scrape run history for a board (newest first). Supports ?limit=. |
| GET /api/runs/{id}            | Get details of a single run including errors array.                   |

**6.5 Error Response Format**

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

|                  |                 |                                                                               |
|------------------|-----------------|-------------------------------------------------------------------------------|
| **Job Class**    | **Type**        | **Description**                                                               |
| BoardAnalysisJob | Fire-and-forget | Runs once on board registration. Analyzes the board and stores ScraperConfig. |
| ScrapeJobRunner  | Recurring       | Runs on board's cron schedule. Executes full scrape pipeline.                 |
| ScrapeJobRunner  | Fire-and-forget | Triggered by manual refresh endpoint. Same logic, different trigger source.   |

**7.3 Schedule Management**

```
// On board creation (after analysis):
RecurringJob.AddOrUpdate&lt;ScrapeJobRunner&gt;(
recurringJobId: $"scrape-{board.Id}",
methodCall: x =&gt; x.ExecuteAsync(board.Id, CancellationToken.None),
cronExpression: board.ScheduleCron,
options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
);
// On schedule update (PATCH /api/job-boards/{id}):
RecurringJob.AddOrUpdate&lt;ScrapeJobRunner&gt;( // same ID = update
recurringJobId: $"scrape-{board.Id}",
...
cronExpression: updatedBoard.ScheduleCron
);
// On board pause or delete:
RecurringJob.RemoveIfExists($"scrape-{board.Id}");
// On manual refresh:
BackgroundJob.Enqueue&lt;ScrapeJobRunner&gt;(
x =&gt; x.ExecuteAsync(board.Id, CancellationToken.None)
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
ConnectionStrings__Default: &gt;
Host=db;Database=${POSTGRES_DB};
Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
Anthropic__ApiKey: ${ANTHROPIC_API_KEY}
Anthropic__Model: claude-sonnet-4-5
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
```

**8.3 API Dockerfile**

```
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Workcast.Api/Workcast.Api.csproj \
-c Release -o /app/publish
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# Playwright system dependencies
RUN apt-get update &amp;&amp; apt-get install -y \
libnss3 libnspr4 libatk1.0-0 libatk-bridge2.0-0 \
libcups2 libdrm2 libxkbcommon0 libxcomposite1 \
libxdamage1 libxfixes3 libxrandr2 libgbm1 libasound2 \
&amp;&amp; rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
# Install Playwright browsers — uses PLAYWRIGHT_BROWSERS_PATH env var
# Browsers are stored in the external volume, so this only runs
# on first start or if the volume is empty.
RUN dotnet tool install --global Microsoft.Playwright.CLI \
&amp;&amp; ~/.dotnet/tools/playwright install chromium
ENTRYPOINT ["dotnet", "Workcast.Api.dll"]
```

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
var db = scope.ServiceProvider.GetRequiredService&lt;AppDbContext&gt;();
await db.Database.MigrateAsync();
```

This ensures the schema is always up to date when the container starts, including on first run against a fresh PostgreSQL volume.

**9. Frontend — Next.js Application**

**9.1 Structure**

```
web/
├── app/
│ ├── layout.tsx # Root layout, navigation
│ ├── page.tsx # Redirect to /boards
│ ├── boards/
│ │ ├── page.tsx # Board list + add board form
│ │ └── [id]/
│ │ ├── page.tsx # Board detail: config, run history
│ │ └── ads/page.tsx # Ads for this board
│ ├── ads/page.tsx # Global ad search/browse
│ └── runs/[id]/page.tsx # Scrape run detail + error log
├── components/
│ ├── boards/ # Board-specific components
│ ├── ads/ # Ad card, ad table
│ └── ui/ # Shared design system components
├── lib/
│ ├── api.ts # Typed API client (fetch wrapper)
│ └── hooks/ # TanStack Query hooks
└── types/ # TypeScript interfaces matching API DTOs
```

**9.2 Key Pages**

|                              |                                                                                                                                                                                    |
|------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Page**                     | **Functionality**                                                                                                                                                                  |
| Boards List /boards          | List all boards with status badge, last scraped time, ad count. "Add Board" button opens inline form (just a URL field). Status polling for boards in "pending" state.             |
| Board Detail /boards/\[id\]  | Displays name, URL, status, cron schedule (editable), scraper_config (collapsible JSON view). Buttons: Manual Refresh, Re-analyze, Pause/Resume, Delete. Recent run history table. |
| Board Ads /boards/\[id\]/ads | Paginated ad table for this board. Columns: title, company, location, salary_raw, scraped_at, link. The link opens the original job ad URL in a new browser tab. |
| Global Ads /ads              | Same as above but across all boards. Adds a board filter dropdown.                                                                                                |
| Run Detail /runs/\[id\]      | Shows run metadata, pages scraped, ads found/new, and error log (if any) with page URL and error message.                                                                          |
| Hangfire Dashboard /hangfire | Exposed directly from the API container. Links to it from the admin nav.                                                                                                           |

**9.3 Polling & Real-Time Updates**

Since there is no WebSocket or SSE implementation in v1, the frontend uses client-side polling for live feedback:

- Board status polling: every 3 seconds while any board is in "pending" state

- Active run polling: every 5 seconds while a run shows status "running"

- TanStack Query refetchInterval handles both cases cleanly

**10. Claude API Cost Analysis**

**10.1 Call Volume Model**

Assumptions: 20 job boards, each refreshed hourly. All extraction is deterministic after board analysis — no AI calls are made during scrape runs.

|                       |                 |                                                         |
|-----------------------|-----------------|---------------------------------------------------------|
| **Phase**             | **Calls / Day** | **Breakdown**                                           |
| Board setup (one-off) | 20 total        | 1 analysis call per board                               |
| Recurring scrapes     | 0               | No AI per scrape run — CSS extraction only              |
| Self-heal (rare)      | ~1–2 total      | Only when a board redesigns its listing page structure  |

**10.2 Cost Estimate**

Based on Claude claude-sonnet-4-5 pricing (~\$3 per million input tokens) and an estimated average of 2,000 tokens per call after HTML pre-processing:

|                       |                            |
|-----------------------|----------------------------|
| **Phase**             | **Estimated Cost**         |
| Board setup (20)      | ~\$0.12 total (negligible) |
| Steady state          | ~\$0.00/day                |
| Monthly steady state  | ~\$0.12 total (one-off)    |

```
Key architectural decision: listing-page-only extraction
By generating field-level CSS selectors during board analysis, all subsequent
scrape runs require zero AI calls. Claude is only invoked once per board (on
registration or re-analysis). This reduces operational AI cost to near-zero
regardless of scrape frequency or ad volume.
```

**11. Non-Functional Requirements**

**11.1 Performance**

- API response time: \< 200ms for read endpoints (list, get)

- Board analysis: async — user receives 202 immediately, analysis completes within 30–60 seconds

- Scrape run: no hard SLA, but should complete within the scheduling interval (default 1 hour)

**11.2 Reliability**

- Hangfire retries failed jobs up to 3 times with exponential backoff

- Playwright page load failures are caught, logged to ScrapeRun.Errors, and do not abort the entire run

- Claude API call failures retry 3 times before logging the ad as an error

- A single ad extraction failure does not stop the processing of remaining ads in a run

**11.3 Maintainability**

- AI provider is fully abstracted — adding a new provider requires only implementing IAiProvider and updating DI registration

- ScraperConfig (including field selectors) is stored as JSONB on JobBoard — triggering re-analysis regenerates it without any code change

- Self-healing is structural (selector yields 0 results) rather than probabilistic (confidence scores), making it deterministic and observable in logs

**11.4 Security (Local Dev Scope)**

- API keys stored in .env file, never committed to source control

- .env.example committed with placeholder values only

- volumes/ directory gitignored

- Hangfire dashboard has no authentication in local dev — add middleware auth before any non-local deployment

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

- OpenAPI/Scalar documentation

**Phase 2 — Playwright Integration**

- **Goal:** Render any URL and get clean HTML back

- IScraperEngine interface + PlaywrightScraperEngine implementation

- HTML cleaning pipeline (strip scripts, styles, SVGs, collapse whitespace)

- Playwright DI registration, browser lifecycle management

- Volume mount for browser binary cache

- Integration test: render a known URL, assert HTML returned

**Phase 3 — AI Integration**

- **Goal:** Analyze a board URL and extract job ads with Claude

- IAiProvider interface (AnalyzeBoardAsync only)

- ClaudeAiProvider — AnalyzeBoardAsync with updated tool use schema (includes field_selectors)

- BoardAnalysisJob implementation

- AiExtractionService — orchestrates HTML cleaning + board analysis

- C# records: BoardAnalysisResult (with FieldSelectors), ScraperConfig updated

- Integration test: analyze a real job board URL, assert field selectors returned

**Phase 4 — Scraping Pipeline**

- **Goal:** Full scrape run end-to-end with deduplication

- ScrapeJobRunner — listing-only pipeline: render listing page → CSS-extract all cards → dedup → persist

- Deterministic field extraction using field_selectors from ScraperConfig (AngleSharp CSS queries)

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

**Phase 6 — Next.js Frontend**

- **Goal:** Usable UI for the complete feature set

- Typed API client (lib/api.ts)

- TanStack Query hooks for all entities

- Boards List page with Add Board form

- Board Detail page with schedule editor and run history

- Ads browser (per-board and global)

- Run detail page with error log

- Status polling for pending boards and active runs

- web.Dockerfile and docker-compose service

**13. Open Questions & Future Considerations**

**13.1 Decisions Deferred to Engineering**

|                        |                                                                                                                                          |
|------------------------|------------------------------------------------------------------------------------------------------------------------------------------|
| **Topic**              | **Question**                                                                                                                             |
| Playwright concurrency | Single browser instance is safe but slow for many boards. Consider a browser pool if scrape run duration becomes a problem.              |
| HTML cleaning depth    | The cleaning pipeline targets 10,000 tokens. Measure actual token usage on real boards in Phase 3 and tune accordingly.                  |
| Confidence threshold   | Self-heal triggers below 0.5 confidence on \>50% of ads. Validate this threshold against real extractions.                               |
| Max pages safety cap   | ScraperConfig includes max_pages. Define a global hard cap (e.g. 100 pages) as a fallback if AI does not set one.                        |
| Stale ad window        | "3 consecutive runs" for stale detection may be too aggressive or too lenient depending on board update frequency. Make it configurable. |

**13.2 Future Enhancements (Out of v1 Scope)**

- Redis — for distributed locks to prevent concurrent scrape runs on the same board

- User authentication — JWT-based auth to support multi-tenancy

- Webhook / email alerts — notify when new ads match saved search criteria

- Full-text search — PostgreSQL tsvector on title + description, or Elasticsearch

- Proxy rotation — for boards that rate-limit or block repeated requests

- Metrics / observability — Prometheus + Grafana for scrape run latency and AI cost tracking

- Ad deduplication across boards — detect the same ad posted on multiple boards
