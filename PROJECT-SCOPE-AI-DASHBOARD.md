# PROJECT SCOPE: AI Personal Dashboard
**Showcase Project — Portfolio / GitHub**
**Last Updated: April 2026**

---

## Project Goal

Build a publicly viewable, fully deployed AI-powered personal dashboard that demonstrates:
- AWS Lambda + Step Functions architecture
- AWS Bedrock AI integration (Claude Haiku — summarization)
- .NET 8 / C# backend
- AWS CDK infrastructure as code
- Clean frontend deployment (Netlify)
- Real-world data integration via public APIs
- DynamoDB table design with multiple entity types
- Production-grade security and cost protection

This project exists to showcase technical skills to employers and clients.
It should be impressive to read AND have a live demo link.

---

## What It Does (User Perspective)

A personal dashboard that aggregates data from public sources and uses
AWS Bedrock AI to generate a daily intelligent summary / briefing.

**Think:** A "morning briefing" dashboard. You open it and see:
- Current weather for Maryland
- Top Hacker News stories (broad tech)
- Top Dev.to articles (web dev specific)
- GitHub public repo activity
- Recent Steam gaming activity
- Mock maintenance reminders with status indicators
- A Bedrock AI-generated "daily briefing" that summarizes everything

The AI summary is the centerpiece — Bedrock reads all the data once per day
and generates something like:

*"Overcast in Maryland today. Three stories on HN worth a look, plus a solid
CSS Grid deep dive on Dev.to. You pushed commits to dobelweb and ai-dashboard
yesterday. Last played: Return of the Living Dead. Oil change coming up in 14
days. Here's what matters today..."*

---

## Data Sources

| Source | API | What It Shows | Auth |
|---|---|---|---|
| Weather | OpenWeatherMap (free) | Current conditions + 3 day forecast | API key |
| Tech News | Hacker News API (free) | Top 5 broad tech stories | None — no key needed |
| Web Dev News | Dev.to API (free) | Top 5 web dev articles | None — no key needed |
| GitHub | GitHub REST API (public) | Recent commits, repo activity | None — public repos |
| Gaming | Steam API (free) | Recently played games + playtime | Steam API key |
| Reminders | DynamoDB (seeded mock data) | Upcoming maintenance + appointments | Internal only — read only |

**Why these sources:**
- All free, no paid tiers needed
- Hacker News + Dev.to covers broad tech AND web dev specifically
- Neither HN nor Dev.to require any auth — zero keys to expose
- GitHub public API works without auth for public repos
- Steam adds personality — shows who you actually are
- Reminders demonstrates database design and CRUD architecture safely
  using mock data that exposes nothing personal

All external API keys stored in **AWS Parameter Store** — never in code,
never in env files committed to GitHub.

---

## Architecture

```
EventBridge (Scheduled — runs daily at 7am EST)
        │
        ▼
AWS Step Functions
        │
        ├── Lambda 1: Data Fetcher
        │   - Calls OpenWeatherMap API
        │   - Calls Hacker News API (no key)
        │   - Calls Dev.to API (no key)
        │   - Calls GitHub API (public repos)
        │   - Calls Steam API (recently played)
        │   - Saves raw JSON to S3
        │
        ├── Lambda 2: Bedrock Summarizer
        │   - Reads raw data from S3
        │   - Sends to AWS Bedrock (Claude Haiku)
        │   - Generates daily briefing text
        │   - Saves summary + raw data to DynamoDB (dashboard-summaries)
        │   - Sets TTL of 48 hours on DynamoDB record
        │
        └── Lambda 3: API Reader (READ ONLY)
            - Reads latest summary from dashboard-summaries
            - Reads upcoming reminders from dashboard-reminders
            - Returns combined payload to frontend
            - Never calls Bedrock directly
            - Never calls external APIs directly
            - CORS enabled for Netlify frontend

API Gateway (REST — rate limited per IP)
        │
        ▼
Frontend (Netlify — static HTML + Tailwind)
        │
        - Fetches cached data from API Gateway on load
        - Displays all widgets including reminders
        - Displays AI briefing prominently
        - Cache-Control: max-age=3600 (browser caches 1 hour)
        - Dark theme, terminal green accent
```

**Key security principle: Bedrock is NEVER called by the frontend or
API Gateway. It is only called once per day by the scheduled pipeline.**

---

## Security Architecture

Public-facing endpoints present a real risk for AI cost abuse. The
following layers protect against spam, scraping, and runaway charges:

### Layer 1 — Cached Pipeline (Primary Protection)
Bedrock runs **once per day** via EventBridge. The frontend reads a
cached DynamoDB result. No public endpoint can trigger Bedrock.

### Layer 2 — API Gateway Throttling
```
Rate limit: 60 requests/minute per IP
Burst limit: 10 requests
```
Configured in API Gateway — blocks spam attempts automatically.

### Layer 3 — Lambda Concurrency Cap
```
Reserved concurrency: 5
```
Only 5 Lambda executions run concurrently max. Prevents runaway costs
even if throttling is somehow bypassed.

### Layer 4 — DynamoDB TTL
Daily summary records expire after 48 hours automatically.
No runaway storage accumulation.

### Layer 5 — Cache-Control Headers
API Gateway returns `Cache-Control: max-age=3600` on responses.
Most visitors never hit Lambda — browser serves from cache.

### Layer 6 — AWS Spend Alert
CloudWatch billing alert at **$5/month**.
Email fires before any meaningful cost accumulates.

### No Manual Refresh Button
Deliberately omitted. A public refresh endpoint would be a direct
attack surface for triggering Bedrock calls. The daily schedule is
cleaner architecturally and more impressive as a design decision.

### Reminders Are Read-Only Mock Data
The reminders table is seeded once at deploy time with generic mock data.
No public endpoint allows creating, editing, or deleting reminders.
Zero personal data is exposed.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 8 |
| Compute | AWS Lambda |
| Orchestration | AWS Step Functions |
| AI | AWS Bedrock (Claude Haiku) |
| Storage | AWS S3 (raw data), DynamoDB (summaries + reminders) |
| API | AWS API Gateway (rate limited) |
| Infrastructure | AWS CDK (C#) |
| Scheduling | Amazon EventBridge |
| Monitoring / Alerts | AWS CloudWatch |
| Secrets | AWS Parameter Store |
| Frontend | HTML + Tailwind CSS |
| Hosting | Netlify |

---

## Frontend Layout

**Single page dashboard**

```
┌─────────────────────────────────────────────────┐
│  Good Morning, Adam.          Tuesday Apr 2026  │
├─────────────────────────────────────────────────┤
│                                                 │
│  ┌─────────────────────────────────────────┐   │
│  │  AI DAILY BRIEFING              BEDROCK │   │
│  │                                         │   │
│  │  Overcast in Maryland. Three HN stories │   │
│  │  worth reading, plus a solid CSS Grid   │   │
│  │  deep dive on Dev.to...                 │   │
│  └─────────────────────────────────────────┘   │
│                                                 │
│  ┌─────────────┐  ┌───────────────────────┐    │
│  │   WEATHER   │  │     HACKER NEWS       │    │
│  │   64°F      │  │  1. Story title...    │    │
│  │   Overcast  │  │  2. Story title...    │    │
│  │   3-day...  │  │  3. Story title...    │    │
│  └─────────────┘  └───────────────────────┘    │
│                                                 │
│  ┌─────────────┐  ┌───────────────────────┐    │
│  │   DEV.TO    │  │       STEAM           │    │
│  │  1. Article │  │  Return of the        │    │
│  │  2. Article │  │  Living Dead          │    │
│  │  3. Article │  │  2.3 hrs recently     │    │
│  └─────────────┘  └───────────────────────┘    │
│                                                 │
│  ┌─────────────┐  ┌───────────────────────┐    │
│  │   GITHUB    │  │  UPCOMING REMINDERS   │    │
│  │   2 repos   │  │  ⚠ HVAC filter — 3d  │    │
│  │   active    │  │  ✓ Oil change — 14d   │    │
│  │   yesterday │  │  ✓ Registration — Aug │    │
│  └─────────────┘  └───────────────────────┘    │
│                                                 │
│  Last updated: today 7:02am · Powered by        │
│  AWS Bedrock · View on GitHub                   │
└─────────────────────────────────────────────────┘
```

---

## Bedrock Prompt Design

Lambda 2 sends this prompt to Claude Haiku:

```
You are a personal morning briefing assistant for a software developer
named Adam. Be direct, dry, and slightly sardonic in tone. No fluff.
Keep it under 120 words.

Summarize the following into a morning briefing:

WEATHER (Maryland): {weatherSummary}
TOP HACKER NEWS: {topHNStories}
TOP DEV.TO ARTICLES: {topDevToArticles}
GITHUB ACTIVITY: {repoActivity}
RECENTLY PLAYED (Steam): {steamGames}
UPCOMING REMINDERS: {upcomingReminders}

Write the briefing now. Start directly — no greeting,
no "Here is your briefing".
```

---

## DynamoDB Schema

### Table 1: dashboard-summaries
**Partition key:** `date` (string — "2026-04-21")

```json
{
  "date": "2026-04-21",
  "generatedAt": "2026-04-21T07:02:14Z",
  "aiSummary": "Overcast and 64°F in Maryland...",
  "weather": { ... },
  "hackerNews": [ ... ],
  "devTo": [ ... ],
  "github": { ... },
  "steam": { ... },
  "ttl": 1713744134
}
```

TTL auto-deletes records after 48 hours.

### Table 2: dashboard-reminders
**Partition key:** `id` (string)

```json
{
  "id": "rem_001",
  "title": "Vehicle oil change",
  "category": "vehicle",
  "dueDate": "2026-05-05",
  "recurring": "every 6 months",
  "status": "upcoming"
}
```

Seeded once at deploy time. Read-only from public API.
No TTL — reminders persist until manually updated via AWS console.

---

## Mock Reminders (seeded at deploy)

```json
[
  {
    "id": "rem_001",
    "title": "Vehicle oil change",
    "category": "vehicle",
    "dueDate": "2026-05-05",
    "recurring": "every 6 months",
    "status": "upcoming"
  },
  {
    "id": "rem_002",
    "title": "HVAC filter replacement",
    "category": "home",
    "dueDate": "2026-04-18",
    "recurring": "every 3 months",
    "status": "overdue"
  },
  {
    "id": "rem_003",
    "title": "Annual checkup",
    "category": "health",
    "dueDate": "2026-05-15",
    "recurring": "annually",
    "status": "upcoming"
  },
  {
    "id": "rem_004",
    "title": "Renew vehicle registration",
    "category": "vehicle",
    "dueDate": "2026-08-01",
    "recurring": "annually",
    "status": "upcoming"
  },
  {
    "id": "rem_005",
    "title": "Refrigerator water filter",
    "category": "home",
    "dueDate": "2026-06-10",
    "recurring": "every 6 months",
    "status": "upcoming"
  }
]
```

Generic enough to expose nothing personal. Overdue HVAC filter makes
it feel lived-in and real.

---

## Folder Structure

```
ai-dashboard/
│
├── README.md
│
├── frontend/
│   └── index.html                       ← Netlify deployed
│
├── backend/
│   ├── AiDashboard.sln
│   ├── src/
│   │   ├── Dashboard.DataFetcher/       ← Lambda 1
│   │   │   ├── Function.cs
│   │   │   └── Services/
│   │   │       ├── WeatherService.cs
│   │   │       ├── HackerNewsService.cs
│   │   │       ├── DevToService.cs
│   │   │       ├── GitHubService.cs
│   │   │       └── SteamService.cs
│   │   │
│   │   ├── Dashboard.Summarizer/        ← Lambda 2
│   │   │   ├── Function.cs
│   │   │   └── Services/
│   │   │       └── BedrockService.cs
│   │   │
│   │   ├── Dashboard.ApiReader/         ← Lambda 3 (READ ONLY)
│   │   │   ├── Function.cs
│   │   │   └── Services/
│   │   │       ├── SummaryService.cs
│   │   │       └── ReminderService.cs
│   │   │
│   │   └── Dashboard.Shared/
│   │       └── Models/
│   │           ├── WeatherData.cs
│   │           ├── HackerNewsItem.cs
│   │           ├── DevToArticle.cs
│   │           ├── GitHubActivity.cs
│   │           ├── SteamActivity.cs
│   │           ├── Reminder.cs
│   │           └── DashboardRecord.cs
│   │
│   └── cdk/
│       └── Dashboard.Stack/
│           ├── DashboardStack.cs        ← infra + seeder + rate limiting
│           └── StackConfig.cs
│
├── docs/
│   └── architecture.png
│
├── mock/
│   └── mock-data.json
│
└── .env.example
```

---

## Environment Variables (.env.example)

```bash
# AWS
AWS_REGION=us-east-1
AWS_PROFILE=your-profile

# Weather
OPENWEATHER_API_KEY=your_key_here
OPENWEATHER_LAT=39.1376
OPENWEATHER_LON=-76.0698

# GitHub (no key needed for public repos)
GITHUB_USERNAME=adobe5062

# Steam
STEAM_API_KEY=your_key_here
STEAM_USER_ID=your_steam_id_here

# Hacker News — no key needed
# Dev.to — no key needed

# Bedrock
BEDROCK_MODEL_ID=anthropic.claude-haiku-20240307-v1:0
BEDROCK_REGION=us-east-1

# Storage
DYNAMODB_SUMMARIES_TABLE=dashboard-summaries
DYNAMODB_REMINDERS_TABLE=dashboard-reminders
S3_BUCKET_NAME=dashboard-raw-data

# API Gateway
API_BASE_URL=https://your-api-id.execute-api.us-east-1.amazonaws.com/prod
```

---

## Mock Data (mock/mock-data.json)

```json
{
  "date": "2026-04-21",
  "generatedAt": "2026-04-21T07:02:14Z",
  "aiSummary": "Overcast and 64°F in Maryland. Three HN threads worth a look. Dev.to has a solid CSS Grid deep dive today. You pushed to dobelweb and ai-dashboard yesterday. Recently played: Return of the Living Dead (2.3 hrs). HVAC filter is overdue — deal with that.",
  "weather": {
    "temp": 64,
    "condition": "Overcast",
    "humidity": 72,
    "forecast": [
      { "day": "Wed", "high": 68, "low": 55, "condition": "Partly Cloudy" },
      { "day": "Thu", "high": 71, "low": 58, "condition": "Sunny" },
      { "day": "Fri", "high": 65, "low": 52, "condition": "Rain" }
    ]
  },
  "hackerNews": [
    { "title": "Distributed systems are hard, and that's OK", "url": "https://news.ycombinator.com", "points": 342 },
    { "title": ".NET 9 Preview 4 is now available", "url": "https://news.ycombinator.com", "points": 289 },
    { "title": "Show HN: HTTP server in Rust, 200 lines", "url": "https://news.ycombinator.com", "points": 201 }
  ],
  "devTo": [
    { "title": "CSS Grid: Everything you need to know", "url": "https://dev.to", "tags": ["css", "webdev"] },
    { "title": "Building accessible forms in 2026", "url": "https://dev.to", "tags": ["accessibility", "html"] },
    { "title": "Astro vs Next.js: an honest comparison", "url": "https://dev.to", "tags": ["astro", "nextjs"] }
  ],
  "github": {
    "reposActiveYesterday": 2,
    "recentCommits": [
      { "repo": "dobelweb", "message": "feat: complete astro migration", "time": "yesterday" },
      { "repo": "ai-dashboard", "message": "chore: initial project scaffold", "time": "yesterday" }
    ]
  },
  "steam": {
    "recentlyPlayed": [
      { "name": "Return of the Living Dead", "hoursRecent": 2.3, "hoursTotal": 14.7 }
    ]
  },
  "reminders": [
    { "id": "rem_001", "title": "Vehicle oil change", "category": "vehicle", "dueDate": "2026-05-05", "daysUntilDue": 14, "status": "upcoming" },
    { "id": "rem_002", "title": "HVAC filter replacement", "category": "home", "dueDate": "2026-04-18", "daysUntilDue": -3, "status": "overdue" },
    { "id": "rem_003", "title": "Annual checkup", "category": "health", "dueDate": "2026-05-15", "daysUntilDue": 24, "status": "upcoming" },
    { "id": "rem_004", "title": "Renew vehicle registration", "category": "vehicle", "dueDate": "2026-08-01", "daysUntilDue": 102, "status": "upcoming" },
    { "id": "rem_005", "title": "Refrigerator water filter", "category": "home", "dueDate": "2026-06-10", "daysUntilDue": 50, "status": "upcoming" }
  ]
}
```

---

## AWS Cost Estimate (Monthly)

| Service | Usage | Est. Cost |
|---|---|---|
| Lambda | ~10 invocations/day | ~$0.00 (free tier) |
| Step Functions | ~1 execution/day | ~$0.00 (free tier) |
| Bedrock (Claude Haiku) | 1 call/day = ~30/month | ~$0.03 |
| DynamoDB | 2 tables, minimal reads/writes | ~$0.00 (free tier) |
| S3 | tiny JSON files | ~$0.00 (free tier) |
| API Gateway | ~1000 calls/month | ~$0.00 (free tier) |
| CloudWatch | spend alert | ~$0.00 |
| **Total** | | **~$0.03/month** |

Spend alert set at $5/month. Absolute worst case is still pennies.

---

## Build Order

1. **Mock data** — write `mock/mock-data.json` with all 6 data sources
2. **Frontend** — build full dashboard with all widgets using hardcoded mock data
3. **Lambda 1** — Data Fetcher (weather + HN + Dev.to + GitHub + Steam)
4. **Lambda 2** — Bedrock Summarizer (updated prompt includes Dev.to + reminders)
5. **Lambda 3** — API Reader (reads summary + reminders from DynamoDB)
6. **CDK Stack** — all infra including both DynamoDB tables, seeder, rate limiting, spend alert
7. **Connect frontend** to real API Gateway endpoint
8. **Deploy frontend** to Netlify
9. **Verify** scheduled pipeline runs and updates dashboard
10. **Write README** with architecture diagram and live demo link

---

## Future Phase — Private Personal Instance

The public demo uses seeded mock data with read-only endpoints. The
architecture is intentionally designed to support a private authenticated
deployment with full CRUD functionality.

**Phase 2 additions (future):**
- AWS Cognito authentication — lock the dashboard behind login
- Full CRUD Lambda for reminders — add, edit, complete, delete
- Real personal reminders — actual appointments, maintenance schedules
- SNS/SES email notifications — alert when something is due within 7 days
- More personal data sources — calendar integration, health metrics etc
- Private deployment on a separate AWS account or home server

Same codebase, different config and auth layer. Worth mentioning in the
README as intentional design — shows forward thinking.

---

## Success Criteria

- [ ] Live demo URL works and shows real data
- [ ] All 6 widgets render correctly (weather, HN, Dev.to, GitHub, Steam, reminders)
- [ ] Bedrock AI summary updates daily automatically
- [ ] Reminders show correct overdue/upcoming status
- [ ] No public endpoint can trigger Bedrock directly
- [ ] API Gateway rate limiting configured and tested
- [ ] AWS spend alert configured at $5/month
- [ ] GitHub repo is public with clean readable code
- [ ] Mock mode works without any AWS credentials
- [ ] README has architecture diagram and live demo link
- [ ] Zero hardcoded secrets anywhere in codebase
- [ ] CDK deploys cleanly with documented commands

---

## Portfolio Card (for dobelweb.dev)

**Title:** AI Morning Dashboard
**Tags:** AWS Bedrock / Lambda / Step Functions / .NET 8 / CDK / DynamoDB
**Description:** Serverless daily briefing dashboard. Pulls weather, Hacker News,
Dev.to, GitHub, and Steam activity — summarized each morning by AWS Bedrock AI.
Includes a mock maintenance reminder system. Rate-limited and cost-protected
for public deployment.
**Links:** Live Demo | GitHub

---

*This scope is the source of truth for this project.
Update it as decisions change during development.*
