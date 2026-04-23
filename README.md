# AI Personal Dashboard

A serverless morning briefing dashboard that aggregates personal data feeds and uses Claude AI to reason across all of them — surfacing what's relevant, connecting cross-source insights, and generating a daily dev challenge.

Built entirely in the .NET ecosystem: three AWS Lambda functions in C#, infrastructure as code with AWS CDK, and a Blazor WebAssembly frontend.

**[Live Demo](#)** · **[Portfolio](https://dobelweb.dev)**

> Open `?mock` to see all features without hitting the live API.

---

## What It Does

Every morning a Step Functions pipeline fetches data from six sources, passes everything to Claude simultaneously, and stores the result. The Blazor frontend reads it on demand from a cached API.

**AI Widgets — Claude Haiku 4.5 via AWS Bedrock**

- **Daily Briefing** — Claude reasons across all data sources at once: flags HN/Dev.to articles relevant to your stack, generates cross-source insights (e.g. a trending article that relates to your recent commits, or a reminder made more urgent by weather), and ranks today's priority actions. The dashboard already shows the raw data — Claude's job is to connect the dots it can't.
- **Daily Dev Challenge** — randomized interview-style question across C#, Blazor, AWS, SQL, and async patterns. Reveal the answer when ready.
- **Cult Vault: B-Horror Pick** — TMDB's discover API surfaces real low-popularity cult films (50+ votes, sorted by popularity ascending). Claude picks one from the verified list and explains in one sentence why it's worth watching. Real data + AI curation eliminates hallucinated film titles.

**Live Data Feeds**

| Widget | Source |
|---|---|
| Hacker News | HN Algolia API — top stories |
| Dev.to | Dev.to API — trending articles with tags |
| GitHub | GitHub API — yesterday's commits across repos |
| Steam | Steam API — recently played with library icons |
| Weather | OpenWeather API — current + 3-day forecast |
| Task Queue | DynamoDB — reminders sorted by urgency, overdue flagged |

---

## Architecture

```
EventBridge (daily schedule)
        │
        ▼
┌───────────────────┐     ┌──────────────────────┐
│  DataFetcher      │────▶│  S3  (raw JSON, 7d)  │
│  Lambda           │     └──────────┬───────────┘
│                   │                │
│  · OpenWeather    │                ▼
│  · Hacker News    │     ┌──────────────────────┐     ┌────────────────┐
│  · Dev.to         │     │  Summarizer Lambda   │◀───▶│  AWS Bedrock   │
│  · GitHub         │     │                      │     │  Claude Haiku  │
│  · Steam          │     │  · Daily briefing    │     └────────────────┘
│  · TMDB           │     │  · Dev challenge     │
└───────────────────┘     │  · B-horror pick     │
                          └──────────┬───────────┘
                                     │
                          ┌──────────▼───────────┐
                          │  DynamoDB            │
                          │  summaries (48h TTL) │
                          │  reminders           │
                          └──────────┬───────────┘
                                     │
                          ┌──────────▼───────────┐
                          │  ApiReader Lambda    │
                          └──────────┬───────────┘
                                     │
                          ┌──────────▼───────────┐
                          │  API Gateway         │
                          │  1h cache · rate     │
                          │  limited · CORS      │
                          └──────────┬───────────┘
                                     │
                          ┌──────────▼───────────┐
                          │  Blazor WASM         │
                          │  Netlify             │
                          └──────────────────────┘
```

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (.NET 8), Tailwind CSS |
| Backend | AWS Lambda (.NET 8) × 3 |
| Orchestration | AWS Step Functions |
| AI | AWS Bedrock — Claude Haiku 4.5 (cross-region inference) |
| Storage | DynamoDB (summaries + reminders), S3 (raw data) |
| API | AWS API Gateway (REST, 1h cache, rate limited) |
| Secrets | AWS SSM Parameter Store (SecureString) |
| IaC | AWS CDK (C#) |
| Scheduling | Amazon EventBridge |
| Hosting | Netlify (frontend) |

---

## Project Structure

```
ai-dashboard/
├── frontend-blazor/
│   ├── Components/              # One .razor component per widget
│   │   ├── AiBriefing.razor
│   │   ├── WeatherWidget.razor
│   │   ├── HackerNewsWidget.razor
│   │   ├── DevToWidget.razor
│   │   ├── GitHubWidget.razor
│   │   ├── SteamWidget.razor
│   │   ├── BHorrorWidget.razor
│   │   ├── RemindersWidget.razor
│   │   └── QuizWidget.razor
│   ├── Pages/Home.razor         # Data fetching, layout, loading/error state
│   ├── Helpers/MarkdownHelper.cs
│   └── wwwroot/
│       └── mock/mock-data.json  # Demo data for ?mock mode
├── backend/
│   ├── AiDashboard.sln
│   ├── src/
│   │   ├── Dashboard.Shared/    # Models + helpers shared across all Lambdas
│   │   ├── Dashboard.DataFetcher/
│   │   ├── Dashboard.Summarizer/
│   │   └── Dashboard.ApiReader/
│   └── cdk/                     # Full AWS infrastructure as CDK C#
└── mock/mock-data.json
```

---

## Local Development

**Frontend only (no AWS needed)**
```bash
cd frontend-blazor
dotnet watch --urls http://localhost:5050
```
Open `http://localhost:5050?mock` — serves from `wwwroot/mock/mock-data.json`.

**Full stack**

Prerequisites: .NET 8 SDK, AWS CLI configured, CDK CLI, CDK bootstrapped in your account.

Store secrets in SSM:
```bash
aws ssm put-parameter --name "/dashboard/openweather/api-key" --value "YOUR_KEY" --type SecureString
aws ssm put-parameter --name "/dashboard/openweather/lat"     --value "YOUR_LAT" --type String
aws ssm put-parameter --name "/dashboard/openweather/lon"     --value "YOUR_LON" --type String
aws ssm put-parameter --name "/dashboard/steam/api-key"       --value "YOUR_KEY" --type SecureString
aws ssm put-parameter --name "/dashboard/steam/user-id"       --value "YOUR_ID"  --type String
aws ssm put-parameter --name "/dashboard/tmdb/api-key"        --value "YOUR_KEY" --type SecureString
```

Build and deploy:
```bash
cd backend/src
dotnet publish Dashboard.DataFetcher/Dashboard.DataFetcher.csproj -c Release -r linux-x64 --self-contained false
dotnet publish Dashboard.Summarizer/Dashboard.Summarizer.csproj   -c Release -r linux-x64 --self-contained false
dotnet publish Dashboard.ApiReader/Dashboard.ApiReader.csproj     -c Release -r linux-x64 --self-contained false

cd ../cdk
cdk deploy --require-approval never
```

Trigger the pipeline manually:
```bash
aws stepfunctions start-execution \
  --state-machine-arn arn:aws:states:us-east-1:ACCOUNT_ID:stateMachine:dashboard-daily-pipeline \
  --input '{}'
```

---

## Design Decisions

**Three Lambdas instead of one**
Each step has a different timeout profile and failure mode. DataFetcher wraps every external API call in `SafeFetch` — any single source failing (HN rate limit, Steam outage) returns null rather than failing the whole pipeline. Summarizer has a separate timeout budget for Bedrock calls. Separating them makes failures debuggable and retries cheap.

**Blazor WASM over React/Vue/plain JS**
The entire stack is .NET. Blazor lets the frontend reference `Dashboard.Shared` directly — the same `DashboardRecord`, `Reminder`, and `SteamGame` types used by the Lambdas, with no duplicated type definitions, no JSON property name guessing, and proper null safety at compile time.

**TMDB + Claude for B-Horror instead of pure prompting**
Claude hallucinates film titles when asked to recommend obscure movies. TMDB's discover API with `sort_by=popularity.asc` and `vote_count.gte=50` surfaces real low-popularity films. Claude then picks from that verified list — real data for existence, AI for curation.

**API Gateway 1-hour cache**
The pipeline runs once daily. There's no value in hitting DynamoDB on every browser refresh. Caching at the Gateway layer means the Lambda only runs when the cache is cold.

**No streaming from Bedrock**
The pipeline runs async once a day and stores a snapshot. The frontend reads a static result — no streaming needed, and it keeps the API stateless and cacheable.

---

## Security

- Bedrock invoked **once per day** by scheduled pipeline only — no public trigger
- API Gateway: 60 req/min rate limit, 10 burst limit
- Lambda reserved concurrency capped at 5
- DynamoDB TTL: summaries expire after 48 hours automatically
- API responses cached 1 hour (`Cache-Control: max-age=3600`)
- CloudWatch spend alarm at $5/month → SNS notification
- Zero secrets in code — all in AWS SSM Parameter Store as SecureString

---

## Cost

Running continuously, this costs approximately **$0–$2/month**:

| Service | Estimated cost |
|---|---|
| Bedrock (Claude Haiku, 3 calls/day) | ~$0.30/month |
| Lambda (3 functions, once daily) | Free tier |
| DynamoDB (on-demand, low traffic) | Free tier |
| API Gateway | Free tier covers personal use |
| S3 | Negligible |
| EventBridge | Free |

---

## Roadmap / Personal Fork

This repo is intentionally demo-safe — no personal credentials, no private calendar data, no write-back to external services. A private fork extends it with:

**Google Calendar integration**
Reads today and tomorrow's events into the briefing context. Claude can then reason about your actual schedule alongside reminders and GitHub activity: *"Heavy meeting day ahead, two overdue reminders, and no commits yet this week — block time this afternoon before it's gone."*

**Calendar write-back**
Add events directly from the dashboard via a small form component → `POST /events` endpoint → Lambda writes to Google Calendar API using a stored OAuth refresh token.

**AWS cost and usage widget**
Pulls daily spend from Cost Explorer. Claude flags anomalies correlated with recent deployments: *"Lambda invocations spiked 3x after Thursday's push — worth checking CloudWatch logs."*

**Fitness and sleep data**
Garmin or Oura integration. Sleep hours alongside GitHub commit volume makes productivity patterns uncomfortable to ignore.

**Targeted Reddit feeds**
r/dotnet, r/csharp, r/aws as a community signal layer — what developers are actually debating vs. what Dev.to is publishing.
