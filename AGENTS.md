# AGENTS.md — Workcast Platform
# Multi-Agent Boundary Definitions

This file defines agent ownership, boundaries, and rules of engagement only.
It contains no implementation details — those live exclusively in TECHSPEC.md.

When spec and agent instructions appear to conflict, TECHSPEC.md always wins.
If the spec is updated, this file does not need to change.

---

## Agent Roster

| Agent | Name                 | Primary Responsibility                            |
|-------|----------------------|---------------------------------------------------|
| 1     | Core Agent           | Domain entities, interfaces, model records        |
| 2     | Infrastructure Agent | EF Core, Playwright, AI provider, Hangfire setup  |
| 3     | Jobs Agent           | Hangfire job implementations                      |
| 4     | API Agent            | Controllers, middleware, Program.cs, Docker       |
| 5     | Frontend Agent       | Next.js application, API client, all pages        |

---

## Agent 1 — Core Agent

### Prerequisite
None. This is the first agent to run.

### Owns
- `src/Workcast.Core/` — entire project, all files

### Must Not Touch
- Any directory or file outside `src/Workcast.Core/`

### Scope Reference
Implement everything described under the following sections of TECHSPEC.md:
- Section 2.3 — Solution Structure (Core project only)
- Section 3 — Data Model (entities and enums)
- Section 4.1 — AI Layer abstraction (interfaces and model records only)
- Section 5.4 — Scraping pipeline (IScraperEngine interface only)

---

## Agent 2 — Infrastructure Agent

### Prerequisite
**Agent 1 must be reviewed and approved before this agent starts.**
`src/Workcast.Core/` is locked from this point — treat it as read-only.

### Owns
- `src/Workcast.Infrastructure/` — entire project, all files

### Must Not Touch
- `src/Workcast.Core/` — read only, never modify
- `src/Workcast.Api/` — does not exist yet
- `src/Workcast.Jobs/` — does not exist yet

### Scope Reference
Implement everything described under the following sections of TECHSPEC.md:
- Section 2.3 — Solution Structure (Infrastructure project only)
- Section 3 — Data Model (EF Core configuration, migrations, indexes)
- Section 4 — AI Layer (ClaudeAiProvider, AiExtractionService, HtmlCleaningService)
- Section 5.4 — Playwright configuration
- Section 7.1 — Hangfire setup and storage configuration
- Section 8.3 — api.Dockerfile (infrastructure dependencies only)

---

## Agent 3 — Jobs Agent

### Prerequisite
**Agent 2 must be reviewed and approved before this agent starts.**
`src/Workcast.Core/` and `src/Workcast.Infrastructure/` are locked — read only.

### Owns
- `src/Workcast.Jobs/` — entire project, all files

### Must Not Touch
- Any directory or file outside `src/Workcast.Jobs/`

### Scope Reference
Implement everything described under the following sections of TECHSPEC.md:
- Section 2.3 — Solution Structure (Jobs project only)
- Section 5.1 — Board registration flow (BoardAnalysisJob)
- Section 5.2 — Recurring scrape run flow (ScrapeJobRunner)
- Section 5.3 — Deduplication strategy
- Section 7.2 — Job types and behaviour
- Section 4.6 — Self-healing trigger logic

---

## Agent 4 — API Agent

### Prerequisite
**Agent 3 must be reviewed and approved before this agent starts.**
All `src/` projects except `src/Workcast.Api/` are locked — read only.

### Owns
- `src/Workcast.Api/` — entire project, all files
- `docker/` — all Docker and Compose files
- `Workcast.sln` — solution file, add all project references

### Must Not Touch
- Any `src/` project other than `src/Workcast.Api/`
- The `.env` file — only `.env.example` is committed

### Scope Reference
Implement everything described under the following sections of TECHSPEC.md:
- Section 2.3 — Solution Structure (Api project only)
- Section 6 — REST API Specification (all endpoints, DTOs, error format)
- Section 7.3 — Schedule management (Hangfire registration from controllers)
- Section 8.1 — docker-compose.yml
- Section 8.2 — .env.example
- Section 8.3 — api.Dockerfile
- Section 8.5 — EF Core migrations on startup

---

## Agent 5 — Frontend Agent

### Prerequisite
**Agent 4 must be reviewed and approved before this agent starts.**
The REST API is running locally and can be used for testing.

### Owns
- `web/` — entire Next.js application, all files
- `docker/web.Dockerfile` — update to serve the Next.js app (do not touch any other Docker file)

### Must Not Touch
- Any directory outside `web/`
- Any Docker file other than `docker/web.Dockerfile`
- Any file in `src/`

### Scope Reference
Implement everything described under the following sections of TECHSPEC.md:
- Section 9 — Frontend application (all pages, components, hooks, API client)
- Section 6 — REST API Specification (as the consumer contract — do not modify the API)

---

## Cross-Agent Rules (All Agents)

1. **TECHSPEC.md is the single source of truth** — when in doubt, re-read the relevant section
2. **Read TECHSPEC.md, then CONVENTIONS.md before writing any line of code**
3. **Never create or modify files outside your designated scope**
4. **Core interfaces are locked after Agent 1 — all agents implement against them, never modify them**
5. **Do not introduce packages or dependencies not listed in CONVENTIONS.md without an explicit justification comment**
6. **Every public method must have XML doc comments**
7. **No TODO comments — either implement it or document the decision explicitly**
8. **All configuration values come from IConfiguration — never hardcoded**
9. **If this file and TECHSPEC.md appear to conflict, TECHSPEC.md wins — flag the discrepancy with a comment**