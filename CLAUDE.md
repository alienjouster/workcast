# Workcast Platform — Claude Code Bootstrap

This file is read automatically by Claude Code at the start of every session.
Read it in full before taking any action.

---

## What This Project Is

Workcast is an AI-powered job board aggregation platform. Users register a job
board URL — the system handles everything else: AI-driven page analysis, scraper
config generation, scheduled scraping, and job ad extraction.

---

## Mandatory Reading Order

Before writing a single line of code, read these files in this exact order:

1. `TECHSPEC.md` — full technical specification (architecture, data model, API, Docker)
2. `AGENTS.md` — your agent scope, file ownership, and hard constraints
3. `CONVENTIONS.md` — naming, patterns, approved packages, DI lifetimes

Do not proceed until all three are read. They are the source of truth.
When in doubt about any implementation decision, re-read the relevant spec section.

---

## Repo Layout

```
Workcast/                        ← repo root
├── CLAUDE.md                    ← you are here
├── TECHSPEC.md                  ← full technical specification
├── AGENTS.md                    ← agent boundaries and ownership
├── CONVENTIONS.md               ← coding standards
├── IMPLEMENTATION_GUIDE.md      ← step-by-step build guide (human reference)
├── Workcast.sln                 ← created by Agent 4
├── src/
│   ├── Workcast.Core/           ← Agent 1
│   ├── Workcast.Infrastructure/ ← Agent 2
│   ├── Workcast.Jobs/           ← Agent 3
│   └── Workcast.Api/            ← Agent 4
├── web/                         ← Agent 5 (Next.js)
├── docker/                      ← Agent 4
│   ├── docker-compose.yml
│   ├── .env.example
│   ├── api.Dockerfile
│   └── web.Dockerfile
└── volumes/                     ← gitignored, local only
    ├── postgres/
    └── playwright/
```

---

## Your Agent Identity

AGENTS.md defines five agents. You are one of them. When you are invoked, your
prompt will tell you which agent you are. Look up your agent in AGENTS.md and
operate strictly within that scope.

**The most important rule: never create or modify files outside your designated scope.**

---

## Tech Stack at a Glance

| Layer | Technology |
|---|---|
| Backend | .NET 8 — ASP.NET Core Web API |
| Database | PostgreSQL 16 via EF Core 8 + Npgsql |
| Browser automation | Microsoft.Playwright (Chromium, headless) |
| Background jobs | Hangfire 1.8 (in-process, PostgreSQL storage) |
| AI provider | Anthropic Claude API — claude-sonnet-4-5, Tool Use only |
| Frontend | Next.js 14 App Router, TanStack Query v5, Tailwind CSS |
| Infrastructure | Docker + Docker Compose v2, external volumes |

---

## Non-Negotiable Rules

1. **Tool Use only for AI calls** — never plain text prompts for structured data
2. **Core interfaces are locked** once Agent 1 completes — all other agents implement against them, never modify them
3. **No files outside your scope** — if you find yourself creating a file in another agent's directory, stop
4. **No hardcoded secrets** — all config from IConfiguration / environment variables
5. **Migrations are generated, not hand-written** — run `dotnet ef migrations add`
6. **No TODO comments** — implement it or document the decision explicitly