# IMPLEMENTATION_GUIDE.md — Workcast Platform
# Step-by-Step Human-Supervised Multi-Agent Implementation

This guide walks you through the full implementation. Each phase ends with
a review checkpoint before the next agent starts. Never skip a checkpoint.

---

## Prerequisites — Do This First

### 1. Install Claude Code
```bash
npm install -g @anthropic/claude-code
```

Verify:
```bash
claude --version
```

### 2. Set Up Your Anthropic API Key
```bash
export ANTHROPIC_API_KEY=sk-ant-...
```
Add this to your shell profile (~/.zshrc or ~/.bashrc) so it persists.

### 3. Create the Repository
```bash
mkdir Workcast && cd Workcast
git init
```

### 4. Copy Context Files Into the Repo Root
Place these three files in the repo root before running any agent:
- `AGENTS.md`
- `CONVENTIONS.md`
- `TECHSPEC.md` ← rename the downloaded tech spec to this

### 5. Create .gitignore
```
volumes/
.env
**/bin/
**/obj/
**/node_modules/
.next/
```

Commit the context files:
```bash
git add AGENTS.md CONVENTIONS.md TECHSPEC.md .gitignore
git commit -m "Add project context files"
```

---

## Phase A — Agent 1: Core Domain

### Goal
Establish the shared domain layer that all other agents depend on.
Entities, interfaces, enums, and model records. No external dependencies.

### Run Agent 1

```bash
git checkout -b agent/1-core

claude "Read AGENTS.md, CONVENTIONS.md, and TECHSPEC.md in full.
You are Agent 1 — Core Agent.
Implement everything defined in your scope in AGENTS.md.
Follow all conventions in CONVENTIONS.md exactly.
Do not create any files outside src/Workcast.Core/."
```

### What Agent 1 Should Produce
```
Workcast.sln
src/Workcast.Core/
├── Workcast.Core.csproj        (no external NuGet refs)
├── Entities/
│   ├── JobBoard.cs
│   ├── JobAd.cs
│   └── ScrapeRun.cs
├── Interfaces/
│   ├── IScraperEngine.cs
│   ├── IAiProvider.cs
│   └── IJobBoardAnalyzer.cs
└── Models/
    ├── BoardAnalysisResult.cs
    ├── JobAdExtractionResult.cs
    ├── PaginationType.cs
    ├── BoardStatus.cs
    ├── ScrapeRunStatus.cs
    └── TriggerSource.cs
```

### ✅ Review Checkpoint A

Before proceeding, verify each item:

**Build check:**
```bash
dotnet build src/Workcast.Core/
```
Must produce zero errors, zero warnings.

**Interface review — check each interface manually:**
- [ ] `IScraperEngine` has `RenderPageAsync(string url, CancellationToken ct)` returning `Task<string>`
- [ ] `IAiProvider` has both `AnalyzeBoardAsync` and `ExtractJobAdAsync` with correct signatures
- [ ] `IJobBoardAnalyzer` has `AnalyzeAndActivateAsync(Guid jobBoardId, CancellationToken ct)`
- [ ] All async methods accept `CancellationToken`

**Entity review:**
- [ ] `JobBoard` has: Id, Url, Name, ScraperConfig, ScheduleCron, Status, LastScrapedAt, CreatedAt, UpdatedAt
- [ ] `JobAd` has: Id, JobBoardId, ScrapeRunId, ExternalId, Url, Title, Company, Location, SalaryRaw, Description, PostedAt, ScrapedAt, RawHtml, AiConfidenceScore, IsActive
- [ ] `ScrapeRun` has: Id, JobBoardId, TriggeredBy, StartedAt, FinishedAt, Status, PagesScraped, AdsFound, AdsNew, Errors
- [ ] Entities have private setters and factory methods or constructors
- [ ] No EF Core annotations on entities

**Model review:**
- [ ] `BoardAnalysisResult` fields match the ScraperConfig JSON schema in Section 3.5 of the tech spec
- [ ] `JobAdExtractionResult` fields match the ad extraction tool schema in Section 4.4 of the tech spec
- [ ] All enums are defined (PaginationType, BoardStatus, ScrapeRunStatus, TriggerSource)

**If issues found:** Fix them before moving to Phase B. You can re-run the agent with:
```bash
claude "Fix the following issues in Workcast.Core: [describe issues]"
```

**When satisfied:**
```bash
git add -A && git commit -m "[Agent 1] Implement Core domain layer"
git checkout main && git merge agent/1-core
```

---

## Phase B — Agent 2: Infrastructure (parallel-safe with Agent 5 skeleton)

### Goal
Implement all external concerns: database, Playwright, Claude AI provider,
Hangfire scheduler registration. Depends on locked Core interfaces.

### Run Agent 2

```bash
git checkout -b agent/2-infrastructure

claude "Read AGENTS.md, CONVENTIONS.md, and TECHSPEC.md in full.
You are Agent 2 — Infrastructure Agent.
The Core project is complete and its interfaces are locked — do not modify them.
Implement everything defined in your scope in AGENTS.md.
Follow all conventions in CONVENTIONS.md exactly.
Do not create any files outside src/Workcast.Infrastructure/.
After implementing all classes, run: dotnet ef migrations add InitialCreate
from the Infrastructure project directory."
```

### What Agent 2 Should Produce
```
src/Workcast.Infrastructure/
├── Workcast.Infrastructure.csproj
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/
│   │   ├── JobBoardConfiguration.cs
│   │   ├── JobAdConfiguration.cs
│   │   └── ScrapeRunConfiguration.cs
│   └── Migrations/              (EF Core generated files)
├── Scraping/
│   └── PlaywrightScraperEngine.cs
├── AI/
│   ├── ClaudeAiProvider.cs
│   ├── AiExtractionService.cs
│   └── HtmlCleaningService.cs
├── Scheduling/
│   └── HangfireJobScheduler.cs
└── DependencyInjection.cs
```

### ✅ Review Checkpoint B

**Build check:**
```bash
dotnet build src/Workcast.Infrastructure/
```
Zero errors required.

**Database review:**
- [ ] `AppDbContext` has DbSets for all three entities
- [ ] All three `IEntityTypeConfiguration` classes exist
- [ ] Table names are snake_case (job_boards, job_ads, scrape_runs)
- [ ] `ScraperConfig` mapped as JSONB column
- [ ] `Errors` on ScrapeRun mapped as JSONB column
- [ ] All indexes from Section 3.6 of the spec are present
- [ ] `UpdatedAt` interceptor exists and is registered
- [ ] Migration file exists (do NOT hand-check the SQL — just confirm it was generated)

**Playwright review:**
- [ ] Registered as Singleton
- [ ] Uses Headless Chromium
- [ ] WaitUntil NetworkIdle
- [ ] 30 second timeout
- [ ] Implements `IScraperEngine` exactly

**Claude AI provider review — this is the most critical section:**
- [ ] Uses Tool Use API (not plain text completion)
- [ ] Board analysis tool name is exactly `save_board_config`
- [ ] Ad extraction tool name is exactly `save_job_ad`
- [ ] `tool_choice` is set to force the tool call
- [ ] Tool schemas match Sections 4.3 and 4.4 of the tech spec field-by-field
- [ ] Temperature is 0
- [ ] Model is `claude-sonnet-4-5`
- [ ] Retry policy: 3 attempts, 1s/2s/4s backoff
- [ ] Response deserialization targets `tool_use` block input, not text content

**HTML Cleaning review:**
- [ ] Removes `<script>`, `<style>`, `<svg>`, comments
- [ ] Collapses whitespace
- [ ] Attempts to isolate `<main>` / `<article>` for detail pages
- [ ] Uses HtmlAgilityPack

**DI review:**
- [ ] Single `AddInfrastructure` extension method
- [ ] All services registered with correct lifetimes (see CONVENTIONS.md)
- [ ] Hangfire configured with PostgreSQL storage

**If issues found:** Fix before Phase C.

**When satisfied:**
```bash
git add -A && git commit -m "[Agent 2] Implement Infrastructure layer"
git checkout main && git merge agent/2-infrastructure
```

---

## Phase C — Agent 3: Jobs (depends on Phase B)

### Goal
Implement the Hangfire job classes that orchestrate the full scraping pipeline.

### Run Agent 3

```bash
git checkout -b agent/3-jobs

claude "Read AGENTS.md, CONVENTIONS.md, and TECHSPEC.md in full.
You are Agent 3 — Jobs Agent.
The Core and Infrastructure projects are complete and locked — do not modify them.
Implement everything defined in your scope in AGENTS.md.
Follow all conventions in CONVENTIONS.md exactly.
Do not create any files outside src/Workcast.Jobs/."
```

### What Agent 3 Should Produce
```
src/Workcast.Jobs/
├── Workcast.Jobs.csproj
├── BoardAnalysisJob.cs
└── ScrapeJobRunner.cs
```

### ✅ Review Checkpoint C

**Build check:**
```bash
dotnet build src/Workcast.Jobs/
```

**BoardAnalysisJob review:**
- [ ] Loads JobBoard from DB
- [ ] Calls IScraperEngine, HtmlCleaningService, IAiProvider in correct order
- [ ] Persists ScraperConfig and sets Status = Active on success
- [ ] Sets Status = Error on failure and logs the exception
- [ ] Registers Hangfire recurring job after success
- [ ] Enqueues an immediate first ScrapeJobRunner run

**ScrapeJobRunner review:**
- [ ] Creates ScrapeRun record at start
- [ ] Dedup check happens BEFORE detail page render (URL normalisation present)
- [ ] Calls IAiProvider.ExtractJobAdAsync for new ads only
- [ ] Respects suggested_delay_ms between requests
- [ ] Handles all four pagination types: url_param, next_button, infinite_scroll, none
- [ ] Updates ScrapeRun counts (AdsFound, AdsNew) correctly
- [ ] Updates JobBoard.LastScrapedAt
- [ ] Self-heal trigger present (confidence check at end of run)
- [ ] TriggeredBy parameter correctly stored on ScrapeRun

**When satisfied:**
```bash
git add -A && git commit -m "[Agent 3] Implement Jobs layer"
git checkout main && git merge agent/3-jobs
```

---

## Phase D — Agent 4: API & Docker (depends on Phase C)

### Goal
Wire everything together: controllers, middleware, Program.cs, docker-compose,
Dockerfiles. The entire backend becomes runnable after this phase.

### Run Agent 4

```bash
git checkout -b agent/4-api

claude "Read AGENTS.md, CONVENTIONS.md, and TECHSPEC.md in full.
You are Agent 4 — API Agent.
Core, Infrastructure, and Jobs projects are complete and locked.
Implement everything defined in your scope in AGENTS.md.
Follow all conventions in CONVENTIONS.md exactly.
Create the Workcast.sln solution file and add all project references.
Create docker-compose.yml and api.Dockerfile as specified.
Create a placeholder web.Dockerfile (FROM node:20-alpine, no content yet)."
```

### What Agent 4 Should Produce
```
Workcast.sln                   (updated with all projects)
src/Workcast.Api/
docker/
├── docker-compose.yml
├── .env.example
├── api.Dockerfile
└── web.Dockerfile               (placeholder)
```

### ✅ Review Checkpoint D

**Full solution build:**
```bash
dotnet build Workcast.sln
```
All four projects must build cleanly.

**Controllers review:**
- [ ] All 12 endpoints from Section 6 are implemented
- [ ] POST /api/job-boards returns 202 and enqueues BoardAnalysisJob
- [ ] POST /api/job-boards/{id}/refresh enqueues ScrapeJobRunner (manual trigger)
- [ ] POST /api/job-boards/{id}/reanalyze enqueues BoardAnalysisJob
- [ ] GET endpoints return correct DTOs (not raw entities)
- [ ] PATCH /api/job-boards/{id} updates Hangfire recurring job if cron changes
- [ ] DELETE /api/job-boards/{id} removes Hangfire recurring job
- [ ] All errors use Problem Details format

**Program.cs review:**
- [ ] `AddInfrastructure()` called
- [ ] Hangfire server registered with 2 workers
- [ ] Hangfire dashboard at /hangfire
- [ ] EF Core `MigrateAsync()` called on startup
- [ ] Scalar at /scalar
- [ ] CORS allows localhost:3000

**Docker review:**
- [ ] docker-compose.yml has exactly 3 services: db, api, web
- [ ] No Redis service
- [ ] `./volumes/postgres` mounted to PostgreSQL data directory
- [ ] `./volumes/playwright` mounted to `/ms-playwright` in api container
- [ ] `PLAYWRIGHT_BROWSERS_PATH=/ms-playwright` set in api environment
- [ ] db healthcheck present, api depends_on db with condition: service_healthy
- [ ] .env.example has POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD, ANTHROPIC_API_KEY
- [ ] api.Dockerfile installs Playwright system dependencies via apt-get

**Smoke test — start the stack:**
```bash
cd docker
cp .env.example .env
# Edit .env — fill in ANTHROPIC_API_KEY and set a DB password
docker compose up --build
```

Expected on first start:
- PostgreSQL starts and passes healthcheck
- API starts, runs migrations, logs "Now listening on http://+:8080"
- Visit http://localhost:8080/scalar — OpenAPI docs load
- Visit http://localhost:8080/hangfire — Hangfire dashboard loads
- Web container starts (placeholder — no frontend yet)

**If smoke test passes:**
```bash
git add -A && git commit -m "[Agent 4] Implement API layer and Docker setup"
git checkout main && git merge agent/4-api
```

---

## Phase E — Agent 5: Frontend (depends on Phase D)

### Goal
Build the complete Next.js frontend. The API is now running locally so the
frontend can be developed and tested against real endpoints.

### Run Agent 5

```bash
git checkout -b agent/5-frontend

claude "Read AGENTS.md, CONVENTIONS.md, and TECHSPEC.md in full.
You are Agent 5 — Frontend Agent.
The REST API is running at http://localhost:8080.
The API contract is fully described in Section 6 of TECHSPEC.md.
Implement the complete Next.js application defined in your scope in AGENTS.md.
Follow all conventions in CONVENTIONS.md exactly.
Work entirely within the web/ directory.
Update docker/web.Dockerfile to serve the Next.js app on port 3000."
```

### What Agent 5 Should Produce
```
web/                             (complete Next.js app)
docker/web.Dockerfile            (updated, not placeholder)
```

### ✅ Review Checkpoint E

**Frontend build:**
```bash
cd web && npm install && npm run build
```
Zero TypeScript errors, zero build errors.

**Pages review — visit each in browser at http://localhost:3000:**
- [ ] `/boards` — board list loads, "Add Board" form visible, accepts a URL
- [ ] Submitting a board URL triggers POST and shows the board in "pending" state
- [ ] Board status polls and updates to "active" without page refresh
- [ ] `/boards/[id]` — shows config (collapsible JSON), schedule, run history
- [ ] Manual Refresh button triggers POST and a new run appears
- [ ] `/boards/[id]/ads` — paginated ad table loads
- [ ] `/ads` — global ad browser with board filter
- [ ] `/runs/[id]` — run detail with error log if errors exist
- [ ] `/hangfire` link in nav opens the Hangfire dashboard

**Types review:**
- [ ] `types/index.ts` has TypeScript interfaces for JobBoard, JobAd, ScrapeRun
- [ ] Interfaces match the API response DTOs (no missing fields)

**Query hooks review:**
- [ ] All hooks use TanStack Query (no raw `useEffect` fetching)
- [ ] Polling implemented for pending boards (3s) and running scrape runs (5s)
- [ ] Mutations (create, update, delete) invalidate relevant query caches

**Full stack smoke test:**
```bash
cd docker && docker compose up --build
```
- [ ] Add a real job board URL (e.g. https://jobs.lever.co/anthropic or any public job board)
- [ ] Board appears as pending, transitions to active within ~60 seconds
- [ ] First scrape run appears in run history
- [ ] Job ads appear in the ads tab
- [ ] Manual refresh triggers a new run

**When satisfied:**
```bash
git add -A && git commit -m "[Agent 5] Implement Next.js frontend"
git checkout main && git merge agent/5-frontend
```

---

## Phase F — Final Integration Review

### Full End-to-End Test

With the stack running (`docker compose up`):

1. **Add a job board** — paste a real job board URL, submit
2. **Watch board analysis** — status should go pending → active within ~60s
3. **Check scraper config** — open board detail, verify JSON config was generated
4. **Check first run** — run history should show one completed run
5. **Check ads** — ads tab should show scraped results with titles, companies
6. **Check confidence scores** — any very low scores? (indicates HTML cleaning issue)
7. **Manual refresh** — trigger a second run, verify dedup (AdsNew should be 0 or very small)
8. **Edit schedule** — change cron to `*/5 * * * *` (every 5 min), verify Hangfire job updates
9. **Pause board** — status changes, Hangfire job removed
10. **Resume board** — Hangfire job re-registered

### Hangfire Dashboard Checks
- Visit http://localhost:8080/hangfire
- Succeeded jobs should show BoardAnalysisJob and ScrapeJobRunner runs
- No failed jobs (if there are, check the error for API key or network issues)

### What to Do if Something Is Broken

For targeted fixes, run Claude Code with the specific file context:
```bash
claude "The ScrapeJobRunner is not correctly handling next_button pagination.
Here is the current implementation: [paste code]
Here is the expected behaviour from the tech spec: [paste Section 5.2]
Fix only ScrapeJobRunner.cs."
```

---

## Quick Reference — Agent Commands

| Phase | Branch | Command |
|---|---|---|
| A | agent/1-core | `claude "...You are Agent 1..."` |
| B | agent/2-infrastructure | `claude "...You are Agent 2..."` |
| C | agent/3-jobs | `claude "...You are Agent 3..."` |
| D | agent/4-api | `claude "...You are Agent 4..."` |
| E | agent/5-frontend | `claude "...You are Agent 5..."` |

Always run from the repo root so Claude Code has access to all context files.

---

## Tips for Working With Claude Code

- **Keep the prompt short** — the detailed instructions are in AGENTS.md. 
  Claude Code will read those files itself. Don't repeat them in the prompt.
- **One agent at a time** — don't run two agents simultaneously on overlapping
  files even if they're on different branches.
- **Review before merge** — the checkpoints above are non-negotiable. An agent
  that gets the interfaces wrong will corrupt every downstream agent.
- **Use targeted re-runs for fixes** — if one file is wrong, don't re-run the
  entire agent. Point Claude Code at the specific problem.
- **Commit frequently** — each checkpoint is a git commit. If an agent goes
  wrong you can always revert to the last clean checkpoint.