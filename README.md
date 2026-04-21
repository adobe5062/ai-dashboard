# AI Morning Dashboard

A serverless personal dashboard that pulls weather, Hacker News, Dev.to, GitHub, and Steam
activity — then summarizes everything each morning using AWS Bedrock AI.

**[Live Demo](#)** · **[Portfolio](https://dobelweb.dev)**

---

## What It Does

Opens to a daily briefing like:

> *"Overcast and 64°F in Maryland. Three HN threads worth a look. Dev.to has a solid CSS Grid
> deep dive today. You pushed to dobelweb and ai-dashboard yesterday. Recently played: Return
> of the Living Dead (2.3 hrs). HVAC filter is overdue — deal with that."*

The AI summary runs once per day on a schedule. No public endpoint can trigger it.

## Architecture

```
EventBridge (daily 7am EST)
    │
    ▼
Step Functions
    ├── Lambda 1: Data Fetcher  — weather, HN, Dev.to, GitHub, Steam → S3
    ├── Lambda 2: Summarizer    — S3 → Bedrock (Claude Haiku) → DynamoDB
    └── Lambda 3: API Reader    — DynamoDB → API Gateway → Frontend
                                  (read-only, rate-limited, cached)

Frontend (Netlify static HTML + Tailwind)
```

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# / .NET 9 |
| Compute | AWS Lambda |
| Orchestration | AWS Step Functions |
| AI | AWS Bedrock (Claude Haiku) |
| Storage | AWS S3 + DynamoDB |
| API | AWS API Gateway (rate-limited) |
| Infrastructure | AWS CDK (C#) |
| Scheduling | Amazon EventBridge |
| Secrets | AWS Parameter Store |
| Frontend | HTML + Tailwind CSS |
| Hosting | Netlify |

## Cost

~$0.03/month (Bedrock: 1 call/day). Spend alert at $5/month.

## Project Structure

```
ai-dashboard/
├── frontend/index.html              — Netlify deployed
├── backend/
│   ├── AiDashboard.sln
│   ├── src/
│   │   ├── Dashboard.DataFetcher/   — Lambda 1
│   │   ├── Dashboard.Summarizer/    — Lambda 2
│   │   ├── Dashboard.ApiReader/     — Lambda 3
│   │   └── Dashboard.Shared/        — shared models
│   └── cdk/                         — AWS CDK infrastructure
├── mock/mock-data.json              — local dev mock data
└── docs/architecture.png
```

## Local Development

The frontend loads from `mock/mock-data.json` when `API_BASE_URL` is not set.
No AWS credentials needed to work on the UI.

```bash
# Frontend — just open in browser
open frontend/index.html

# Backend — build all
cd backend && dotnet build

# CDK — synthesize CloudFormation
cd backend/cdk && cdk synth
```

## Deploy

**Prerequisites:**
- AWS CLI configured (`aws configure`)
- CDK bootstrapped in your account (`cdk bootstrap`)
- SSM parameters set (see `.env.example` for parameter paths)

```bash
# Build Lambda artifacts
cd backend
dotnet publish src/Dashboard.DataFetcher -c Release -r linux-x64
dotnet publish src/Dashboard.Summarizer  -c Release -r linux-x64
dotnet publish src/Dashboard.ApiReader   -c Release -r linux-x64

# Deploy infrastructure
cd cdk && cdk deploy

# Seed reminder data (run once)
# Update DynamoDB directly or use AWS Console
```

## Security

- Bedrock called **once per day** by scheduled pipeline only — no public trigger
- API Gateway: 60 req/min rate limit, 10 burst
- Lambda reserved concurrency: 5
- DynamoDB TTL: summaries expire after 48 hours
- API responses cached 1 hour (`Cache-Control: max-age=3600`)
- Spend alert at $5/month
- Zero secrets in code — all in AWS Parameter Store

## Future Phase

Private authenticated instance with Cognito, full CRUD reminders, SNS notifications,
and more personal data sources. Same codebase, different auth config.
