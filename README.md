# Workcast

Workcast is an AI-powered job board aggregation platform. Users register a job board URL — the system handles everything else: AI-driven page analysis, scraper config generation, scheduled scraping, job ad extraction, job vs resume matching score. Workcast then helps you with managing the applications: generates ATS-friendly, job-tailord resume, application letter, and interview questions drill. 

---

## Requirements

| Tool | Minimum version | Purpose |
|---|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 4.x (Compose v2) | Runs all services |
| [Anthropic API key](https://console.anthropic.com/) | — | AI analysis, scoring, generation |

[.NET SDK](https://dotnet.microsoft.com/download) and [Node.js](https://nodejs.org/) are also needed if you want to develop or run migrations outside of Docker.

---

## Quick Start

### 1. Clone the repository

```bash
git clone https://github.com/alienjouster/workcast.git
cd workcast
```

### 2. Configure environment variables

Linux:
```bash
cp docker/.env.example docker/.env
```

Windows:
```bash
copy "docker\.env.example" "docker\.env"
```

Open `docker/.env` and fill in your values:

```env
COMPOSE_PROJECT_NAME=workcast
POSTGRES_DB=jobscraper
POSTGRES_USER=jobscraper
POSTGRES_PASSWORD=changeme           # change this
ANTHROPIC_API_KEY=sk-ant-...         # your Anthropic API key (required)
```

The `ANTHROPIC_API_KEY` is required for board analysis, ad scoring, resume and letter generation. Get one at [platform.claude.com](https://platform.claude.com/) and buy some credit.

### 3. Start the stack

```bash
cd docker
docker compose up --build
```

The first build takes a few minutes. Then open the app on http://localhost:3000(http://localhost:3000)

## 4. Import Community Board Configurations (or create your own)

Pre-built scraper configurations for common job boards are available in the [`community-boards/`](community-boards/) folder. See [community-boards/README.md](community-boards/README.md) for import instructions and contribution guidelines.

---

---

## Services

The stack runs six Docker containers:

| Container | Image | Port |
|---|---|---|
| `workcast-db` | PostgreSQL 16 | 5432 |
| `workcast-api` | .NET 10 ASP.NET Core | 8080 |
| `workcast-web` | Next.js 14 | 3000 |
| `workcast-postgres-exporter` | postgres-exporter | — |
| `workcast-prometheus` | Prometheus | 9090 |
| `workcast-grafana` | Grafana | 3001 |

Data is persisted to `volumes/` at the repo root (created automatically on first run):

```
volumes/
  postgres/    ← database files
  prometheus/  ← metrics storage
  grafana/     ← dashboard state
```

---

## Local Development (without Docker)

### Backend

```bash
# Restore packages
dotnet restore

# Apply database migrations (requires a running PostgreSQL instance)
cd src/Workcast.Api
dotnet ef database update

# Run the API
dotnet run --project src/Workcast.Api
```

Set the connection string and API key via environment variables or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=jobscraper;Username=jobscraper;Password=changeme"
  },
  "Anthropic": {
    "ApiKey": "sk-ant-...",
    "Model": "claude-sonnet-4-5"
  }
}
```

### Frontend

```bash
cd web
npm install
npm run dev
```

The frontend expects the API at `http://localhost:8080` by default. Override with:

```bash
NEXT_PUBLIC_API_URL=http://localhost:8080 npm run dev
```

---

## Project Structure

```
Workcast.sln
├── src/
│   ├── Workcast.Core/           # Domain layer — entities, enums, interfaces
│   ├── Workcast.Infrastructure/ # EF Core, Playwright, Claude AI, Hangfire, SSE
│   ├── Workcast.Jobs/           # Background jobs — scraping, analysis, scoring
│   └── Workcast.Api/            # Controllers, DTOs, mapping, Program.cs
│
├── web/                         # Next.js 14 frontend
├── docker/                      # Docker Compose, Dockerfiles, .env.example
├── community-boards/            # Shareable scraper config JSON files
└── volumes/                     # Runtime data (git-ignored)
```

---
