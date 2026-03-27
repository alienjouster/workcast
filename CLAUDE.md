# Workcast Platform — Claude Code Bootstrap

This file is read automatically by Claude Code at the start of every session.
Read it in full before taking any action.

---

## What This Project Is

Workcast is an AI-powered job board aggregation platform. Users register a job
board URL — the system handles everything else: AI-driven page analysis, scraper
config generation, scheduled scraping, job ad extraction, job vs resume scoring, and applications.

---

## Mandatory Reading Order

Before writing a single line of code, read these files in this exact order:

1. `TECHSPEC.md` — full technical specification (architecture, data model, API, Docker)
2. `CONVENTIONS.md` — naming, patterns, approved packages, DI lifetimes

Do not proceed until all three are read. They are the source of truth.
When in doubt about any implementation decision, re-read the relevant spec section.

---

## Repo Layout

Provided in the 'TECHSPEC.md' file.

```

## Tech Stack at a Glance

| Layer | Technology |
|---|---|
| Backend | .NET 10 — ASP.NET Core Web API |
| Database | PostgreSQL 16 via EF Core 8 + Npgsql |
| Browser automation | Microsoft.Playwright (Chromium, headless) |
| Background jobs | Hangfire 1.8 (in-process, PostgreSQL storage) |
| AI provider | Anthropic Claude API Tool Use only |
| Frontend | Next.js 14 App Router, TanStack Query v5, Tailwind CSS |
| Infrastructure | Docker + Docker Compose v2, external volumes |

---

## Non-Negotiable Rules

1. **Tool Use only for AI calls** — never plain text prompts for structured data
2. **No hardcoded secrets** — all config from IConfiguration / environment variables
3. **Migrations are generated, not hand-written** — run `dotnet ef migrations add`
4. **No TODO comments** — implement it or document the decision explicitly
5. **Use coding conventions** — from `CONVENTIONS.md`