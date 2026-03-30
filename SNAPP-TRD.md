# SNAPP — Social Networking Application for PraxisIQ
## Technical Requirements Document

**Version:** 2.6
**Date:** 2026-03-30
**Author:** Tommy Winans + Claude
**Status:** Draft

---

## Table of Contents

1. [Foundational Principles](#1-foundational-principles)
2. [Goals → Objectives → Strategies Matrix](#2-goals--objectives--strategies-matrix)
3. [Architecture Overview](#3-architecture-overview)
4. [Data Model & DynamoDB Design](#4-data-model--dynamodb-design)
5. [Module Breakdown](#5-module-breakdown)
6. [Dependency Graph & Build Order](#6-dependency-graph--build-order)
7. [Work Unit Specifications](#7-work-unit-specifications)
8. [Cross-Cutting Concerns](#8-cross-cutting-concerns)
9. [Future Considerations](#9-future-considerations)

---

## 1. Foundational Principles

These principles constrain every architectural and implementation decision.

| ID | Principle | Implication |
|----|-----------|-------------|
| P1 | **.NET/C# everywhere** | Blazor WebAssembly for UI, .NET 9 for services, shared class libraries for DTOs/validation, Pulumi in C# for IaC .NET 10 |
| P2 | **Minimize JavaScript** | Blazor WASM handles UI; JS interop only when no Blazor-native alternative exists (e.g., clipboard API, certain browser APIs) |
| P3 | **Clean UI/Service separation** | Blazor WASM calls only HTTP APIs. No business logic in the UI layer. Services are independently deployable Docker containers behind Kong API Gateway. |
| P4 | **Stateless services in containers** | Each service is a .NET Minimal API in its own Docker container. Local development uses Docker Compose + Kong. Production deploys to AWS Lambda via Native AOT (same code, different hosting). |
| P5 | **Multi-table DynamoDB** | Separate tables by domain (Users, Networks, Content, Intelligence, Transactions, Notifications). On-demand capacity mode. Each table has its own GSIs tuned to its access patterns. |
| P6 | **Magic-link authentication** | Email-based passwordless login. User provides email → receives a link with a globally unique code → clicking the link establishes the session. No passwords stored, ever. API tokens issued per-user for all service calls. |
| P7 | **Agent-implementable work units** | Every module must be specifiable as a self-contained unit that Claude Code can implement in a single focused session: clear inputs, outputs, interfaces, test criteria. |
| P8 | **PII encrypted at field level** | Email addresses, phone numbers, and all contact information are encrypted at the application layer before storage using AES-256-GCM with envelope encryption (AWS KMS in prod, local key file in dev). DynamoDB default encryption-at-rest is additive, not a substitute. |
| P9 | **Notification digests** | Notifications are accumulated and published as a daily digest email. Users are too busy for individual notifications. Real-time individual notifications are opt-in, not default. |
| P10 | **Local-first development, AWS phased in** | Everything runs locally first — DynamoDB Local, MinIO (S3), Papercut (SMTP), Kong (API Gateway), all service containers via Docker Compose. Zero AWS dependency during development. AWS is phased in only when ready for staging/production deployment. Every service must be fully functional against local infrastructure before any AWS resource is provisioned. |
| P11 | **Pulumi IaC (C#)** | All infrastructure defined in Pulumi using C#. Same language as the application. Type-safe, testable, refactorable. |
| P12 | **OpenAPI-first APIs** | Every service exposes an OpenAPI 3.1 spec generated from code via Swashbuckle/NSwag. A C# SDK (Snapp.Sdk) is auto-generated from the merged spec using Microsoft Kiota. SDKs for other languages (TypeScript, Python, Java) can be generated from the same spec using Kiota's multi-language support. |

---

## 2. Goals → Objectives → Strategies Matrix

### GOAL 1: Establish Professional Identity Infrastructure
*A practitioner can establish a verified, portable professional identity that is richer than a LinkedIn profile and anchored in practice reality.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O1.1 | 100% of users authenticate via magic link with <30s email delivery p95 | S1.1 | AWS SES (prod) / Papercut SMTP (dev) for email delivery, service generates token, DynamoDB Users table stores token with TTL auto-expiry |
| O1.2 | Every user has a dual profile (public discoverable + private guild-scoped) within 5 min of first login | S1.2 | Guided onboarding flow in Blazor WASM with progressive form that builds both profiles simultaneously |
| O1.3 | ≥80% of users link their LinkedIn profile within first session | S1.3 | LinkedIn OAuth 2.0 integration (OpenID Connect) as optional identity enrichment during onboarding; pull name, photo, headline |
| O1.4 | Profile completeness score visible and ≥40% achievable from public data alone | S1.4 | Background enrichment service that pulls practitioner registry, state license, and business listing data to pre-populate profile fields |
| O1.5 | All PII (email, phone, contact info) encrypted at field level with zero plaintext in storage | S1.5 | Envelope encryption: data key encrypted by KMS master key (prod) or local file key (dev). `IFieldEncryptor` interface abstracts provider. Encrypted fields stored as Base64 in DynamoDB. Decryption only at service layer, never at client. |

### GOAL 2: Enable Private Praxisiq Networks (Guilds)
*Practitioners can create, join, and govern private networks organized around shared practice domains with meaningful membership.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O2.1 | A network can be created and configured (charter, roles, membership criteria) in <10 min | S2.1 | Network creation wizard in Blazor with template system; network config stored in Networks table with governance rules as structured attributes |
| O2.2 | Network supports ≥5 configurable roles with granular permissions | S2.2 | Role-permission matrix stored per-network in Networks table; permission checks enforced at Kong plugin layer + service-level validation via token claims |
| O2.3 | Membership application workflow with <24h median review time | S2.3 | Application queue with digest notification to stewards; steward dashboard showing pending applications |
| O2.4 | Network activity dashboard shows engagement, contribution, and membership metrics updated within 5 min of events | S2.4 | Event-driven metrics: DynamoDB Streams → aggregator service → metrics stored in Networks table with time-series GSI |

### GOAL 3: Deliver Multi-Dimensional Practice Intelligence
*Members receive actionable, multi-dimensional intelligence about their practice — scored across 6 dimensions, benchmarked at national/state/county/cohort levels, with career stage classification and risk flagging. This intelligence is unavailable anywhere else and generalizes across network verticals.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O3.1 | Every member sees a practice health dashboard with ≥5 KPIs and a composite score within 1 week of joining (from public data + light contribution) | S3.1 | Public signal aggregation pipeline (practitioner registries, business listings, licensing boards, job postings, association data — sources defined per vertical) pre-populates scores; onboarding survey adds 5-10 low-friction data points; data in Intelligence table |
| O3.2 | Multi-dimensional scoring across 6 configurable dimensions (financial, provider risk, operations, client base, revenue mix, market position) — each scored 0-100 with confidence level | S3.2 | Deterministic scoring engine: per-dimension rules evaluate available signals, produce scored profiles with confidence (high/medium/low based on signal availability); dimensions configurable per network vertical |
| O3.3 | Benchmarking at 4 geographic levels: national → state → market area → peer cohort, with user percentile positioning | S3.3 | Tiered benchmark computation: national aggregates from industry associations, state-level from public regulatory datasets and supply/demand trends, county-level from census/BLS/BEA, cohort-level from guild-contributed data; each level computes P25/P50/P75 |
| O3.4 | Career stage classification for every member with risk flags and trigger signals | S3.4 | Career stage classifier using tenure, co-location signals, production volume, entity type, reputation signals; 6 stages (Training → Associate → Acquisition → Growth → Mature → Pre-Exit); risk flags: retirement risk, succession risk, overextension, key-person dependency |
| O3.5 | Valuation confidence score increases monotonically with data contribution (40% → 65% → 85% → 90%+) | S3.5 | Progressive disclosure engine: each data layer unlocked = recalculated confidence score; valuation model uses contributed + public signals + benchmark calibration |
| O3.6 | ≥70% of members who see their initial scoring profile return within 30 days | S3.6 | Multi-dimensional score as the acquisition hook: radar/spider chart visualization, "your score changed" in daily digest, scenario modeling ("what if I reduce owner production to 50%?") |
| O3.7 | Market intelligence available for any geography where a network member operates | S3.7 | Market profile computation: practitioner density, competitive landscape, consolidation pressure, demographic trends, workforce supply/demand — built from census, BLS, registry, and business listing data at county level |

### GOAL 4: Facilitate Business Transactions Within the Guild
*Members can refer, transact, and collaborate with accountability — not just warm introductions that disappear.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O4.1 | Referral can be created, tracked, and outcome-recorded in <2 min | S4.1 | Structured referral entity in Transactions table with lifecycle states (created → accepted → outcome_recorded); referral form in Blazor with specialty/geography matching |
| O4.2 | ≥50% of referrals have recorded outcomes within 90 days | S4.2 | Automated follow-up: scheduled job checks open referrals → queues outcome-recording prompts for inclusion in daily digest at 30/60/90 days |
| O4.3 | Reputation score reflects actual transaction history, not self-declaration | S4.3 | Reputation computed from: referrals made + received + outcomes, data contributions, discussion contributions, peer attestations; computed on event triggers |
| O4.4 | Deal room available for M&A/succession conversations with document security | S4.4 | Encrypted S3 bucket per deal room, access controlled by Transactions table ACL, document upload/download via pre-signed URLs, audit trail in Transactions table |

### GOAL 5: Bridge to Existing Social Networks & Collaboration Tools
*PraxisIQ is not isolated — it uses LinkedIn as the discovery/identity layer, can broadcast back to it, and can relay content to team collaboration channels via email.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O5.1 | Members can cross-post network milestones to LinkedIn in 1 click | S5.1 | LinkedIn Share API integration (`w_member_social` scope); service formats milestone as LinkedIn post; user approves and publishes |
| O5.2 | ≥30% of new members discover SNAPP through LinkedIn-shared content | S5.2 | Cross-posted content includes deep-link back to SNAPP with referral tracking; UTM parameters on all outbound LinkedIn content |
| O5.3 | LinkedIn profile data enriches SNAPP profile without manual re-entry | S5.3 | On LinkedIn OAuth, pull profile fields (name, headline, photo, current position) and pre-populate SNAPP profile; store LinkedIn URN in Users table (encrypted) |
| O5.4 | Members can relay SNAPP posts, milestones, and digest summaries to Microsoft Teams or Slack channels via email | S5.4 | Channel email relay: Teams channels and Slack channels both accept inbound email. Network stewards configure a channel email address per network. SNAPP formats content as a styled HTML email and sends via SES/SMTP to the channel address. No Teams/Slack API keys required — uses existing email infrastructure. |
| O5.5 | ≥1 external channel relay configured per active network within 30 days of network creation | S5.5 | Steward settings page: "Connect a Team Channel" or "Connect a Slack Channel" — enter the channel email address. SNAPP sends a verification email to confirm the address is valid and the channel receives it. Multiple channel addresses supported per network. |

### GOAL 6: Pluggable Communication Layer
*Real-time discussion happens through a backend that can be native, Discord, or Slack — without coupling the platform.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O6.1 | Network-scoped threaded discussions functional at launch (native backend) | S6.1 | Discussion entities in Content table (thread → posts, sorted by timestamp); Blazor components for thread list, post composition, reply chains |
| O6.2 | Discord integration available as opt-in per network within 90 days of launch | S6.2 | Discord bot service that bridges SNAPP network ↔ Discord server; webhook relay for notifications; authentication bridge linking SNAPP account ↔ Discord user |
| O6.3 | Communication backend swappable without affecting identity, intelligence, or transaction layers | S6.3 | Communication abstracted behind `ICommunicationProvider` interface; native, Discord, Slack are implementations; network config selects provider |

### GOAL 7: Deliver Notification Digests
*Users receive a single daily summary rather than a stream of individual interruptions.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O7.1 | Daily digest email delivered by 7 AM user's local time with all prior-day activity | S7.1 | Notification accumulator writes events to Notifications table; scheduled digest service queries undelivered notifications, groups by category, renders HTML email via Razor template, sends via SES |
| O7.2 | Digest open rate ≥40% (meaningful content, not spam) | S7.2 | Smart digest: skip if no meaningful activity; prioritize by relevance (referral activity > discussion mentions > general feed); personalized based on user's active networks and roles |
| O7.3 | Users can opt into real-time individual notifications for specific event types | S7.3 | Notification preferences in Users table: per-event-type toggle for immediate vs. digest-only delivery; immediate notifications sent within 60s of event |

### GOAL 8: Gamified Data Validation & Progressive Intelligence Unlocks
*The platform asks users targeted micro-questions that validate public data, confirm relationships, and fill intelligence gaps — rewarding each answer by unlocking new insights. This creates a virtuous cycle: the user answers a small question, and the platform reveals something it couldn't share before. The experience feels like a game of discovery, not a survey.*

| Obj ID | Objective (Measurable) | Strategy ID | Implementation Strategy |
|--------|------------------------|-------------|------------------------|
| O8.1 | Every user is presented with ≥1 context-aware validation question per session (embedded in dashboard, digest, or feed — not a separate survey) | S8.1 | **Powered by SurveyIQ** (github.com/thomwinans/surveyiq) — an embeddable question graph engine built on the same stack (C#/.NET, DynamoDB, Pulumi, Kong, Kiota SDK). SNAPP is a SurveyIQ tenant. A "gap survey" is dynamically assembled per user from intelligence gaps: unconfirmed public data ("We see you're at 123 Main St — is this current?"), relationship inference ("Do you know Jane Doe?"), practice data estimates ("Is your staff size closer to 5 or 10?"). SurveyIQ's adaptive scoring (Required/Optional/Exploratory roles + weight tables) prioritizes questions by information gain. Rendered inline via SurveyIQ embed tokens. |
| O8.2 | Each answered question produces a visible unlock — new data point, improved confidence score, or access to a benchmark the user couldn't see before | S8.2 | Unlock engine in SNAPP (not SurveyIQ): listens for SurveyIQ `session.completed` webhooks, maps answered questions to intelligence rewards. Confirming an address unlocks the market profile for that geography. Confirming a relationship adds a connection to the guild graph. Answering a revenue band raises valuation confidence and reveals the cohort benchmark. The unlock is shown immediately. |
| O8.3 | ≥60% of presented questions are answered (questions are relevant, fast, and rewarding) | S8.3 | SurveyIQ's adaptive scoring handles relevance: weight tables adjust question priority based on prior answers. SNAPP's gap detection service generates the survey graph — only questions where (a) the intelligence gap is large, (b) the answer is easy for the user, and (c) the unlock is high-value. Rate-limit: max 3 questions per session, max 1 per digest. |
| O8.4 | Relationship validation questions build the guild's proprietary relationship graph ("Do you know X?" / "Have you referred to Y?" / "Is Z your accountant?") | S8.4 | Relationship questions generated by SNAPP from co-location analysis, complementary specialty inference, and shared network membership — assembled into a SurveyIQ graph with Boolean question types. Confirmed relationships become edges in the guild graph. Mutual confirmations have higher confidence. SurveyIQ's variable piping personalizes questions: "{{respondentName}}, do you know {{targetName}}?" |
| O8.5 | Question completion streaks and cumulative unlocks are visible as a progression indicator | S8.5 | Gamification layer in SNAPP (fed by SurveyIQ session metadata — `DwellTimeMs`, `PresentedAt`, `AnsweredAt`): "X insights unlocked", streak counter, confidence progression bar. The reward is intelligence, not status. |
| O8.6 | Questions can be delivered through the daily digest email with one-click answers (no login required for simple confirmations) | S8.6 | Digest-embedded questions: SNAPP generates SurveyIQ embed tokens for micro-surveys (1-3 questions), renders as clickable links in the digest email. Each link bootstraps a SurveyIQ session scoped to that user (identified respondent via SNAPP userId as `ExternalId`). Webhook fires on completion, SNAPP processes the unlock. |

---

## 3. Architecture Overview

### 3.1 Development Environment (Local Linux Server)

```
┌─────────────────────────────────────────────────────────────────┐
│                   LOCAL LINUX SERVER                             │
│                   Docker Compose                                │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                 Blazor WASM Dev Server                    │   │
│  │                 (dotnet watch, port 5000)                 │   │
│  └──────────────────────┬───────────────────────────────────┘   │
│                         │ http://localhost:5000                  │
│                         ▼                                       │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Kong API Gateway (port 8000)                 │   │
│  │                                                          │   │
│  │  Routes:                                                 │   │
│  │    /api/auth/*       → snapp-auth:8080                   │   │
│  │    /api/users/*      → snapp-user:8080                   │   │
│  │    /api/networks/*   → snapp-network:8080                │   │
│  │    /api/content/*    → snapp-content:8080                │   │
│  │    /api/intel/*      → snapp-intelligence:8080           │   │
│  │    /api/tx/*         → snapp-transaction:8080            │   │
│  │    /api/notif/*      → snapp-notification:8080           │   │
│  │    /api/linkedin/*   → snapp-linkedin:8080               │   │
│  │                                                          │   │
│  │  Plugins:                                                │   │
│  │    - JWT validation (mirrors prod authorizer)            │   │
│  │    - Rate limiting                                       │   │
│  │    - CORS                                                │   │
│  │    - Request logging                                     │   │
│  └─────────────┬───────────────────────────────┬────────────┘   │
│                │                               │                │
│       ┌────────┴────────┐             ┌────────┴────────┐       │
│       │  Service         │             │  Service         │      │
│       │  Containers      │    ...      │  Containers      │      │
│       │  (.NET 9 Minimal │             │  (.NET 9 Minimal │      │
│       │   API in Docker) │             │   API in Docker) │      │
│       └────────┬────────┘             └────────┬────────┘       │
│                │                               │                │
│       ┌────────┴───────────────────────────────┴────────┐       │
│       │                DATA TIER                         │       │
│       │                                                  │       │
│       │  ┌──────────────────┐  ┌──────────────────┐     │       │
│       │  │  DynamoDB Local   │  │  MinIO (S3-      │     │       │
│       │  │  (port 8042)      │  │  compatible,     │     │       │
│       │  │                   │  │  port 9000)      │     │       │
│       │  │  Tables:          │  └──────────────────┘     │       │
│       │  │   snapp-users     │                           │       │
│       │  │   snapp-networks  │  ┌──────────────────┐     │       │
│       │  │   snapp-content   │  │  Papercut SMTP   │     │       │
│       │  │   snapp-intel     │  │  (port 1025/8025)│     │       │
│       │  │   snapp-tx        │  │  (catches all    │     │       │
│       │  │   snapp-notif     │  │   email locally)  │     │       │
│       │  └──────────────────┘  └──────────────────┘     │       │
│       │                                                  │       │
│       │  ┌──────────────────┐                            │       │
│       │  │  Kong DB          │                            │       │
│       │  │  (PostgreSQL,     │                            │       │
│       │  │   port 5432)      │                            │       │
│       │  └──────────────────┘                            │       │
│       └──────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 Production Environment (AWS)

```
┌─────────────────────────────────────────────────────────────┐
│                        CLIENT TIER                          │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              Blazor WebAssembly App                  │    │
│  │  (static files on S3 + CloudFront)                  │    │
│  └─────────────────────┬───────────────────────────────┘    │
│                        │ HTTPS                               │
└────────────────────────┼─────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                       API TIER                              │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              AWS API Gateway (HTTP API)              │    │
│  │                                                     │    │
│  │  ┌──────────────┐  Routes by path prefix:           │    │
│  │  │  Authorizer   │  /api/auth/*    → Auth Lambda    │    │
│  │  │  (Lambda)     │  /api/users/*   → User Lambda    │    │
│  │  └──────────────┘  /api/networks/* → Network Lambda │    │
│  │                    /api/content/*  → Content Lambda  │    │
│  │                    /api/intel/*    → Intel Lambda    │    │
│  │                    /api/tx/*       → Transaction Lmb │    │
│  │                    /api/notif/*    → Notification Lmb│    │
│  │                    /api/linkedin/* → LinkedIn Lambda │    │
│  └─────────────────────────────────────────────────────┘    │
└────────────────────────┬─────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                     SERVICE TIER                            │
│                                                             │
│  Each Lambda: .NET 9 Native AOT                             │
│  Same code as Docker containers — different hosting entry   │
│  point (Program.cs uses conditional compilation)            │
│                                                             │
│  ┌──────┐ ┌──────┐ ┌────────┐ ┌───────┐ ┌──────────┐      │
│  │ Auth │ │ User │ │Network │ │Content│ │Intelligence│     │
│  └──────┘ └──────┘ └────────┘ └───────┘ └───────────┘      │
│  ┌──────┐ ┌──────────┐ ┌────────┐ ┌───────────────┐       │
│  │Trans │ │Notification│ │LinkedIn│ │  Authorizer   │       │
│  └──────┘ └──────────┘ └────────┘ └───────────────┘       │
└────────────────────────┬─────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                      DATA TIER                              │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │                    DynamoDB Tables                     │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐              │  │
│  │  │snapp-    │ │snapp-    │ │snapp-    │              │  │
│  │  │users     │ │networks  │ │content   │              │  │
│  │  └──────────┘ └──────────┘ └──────────┘              │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐              │  │
│  │  │snapp-    │ │snapp-    │ │snapp-    │              │  │
│  │  │intel     │ │tx        │ │notif     │              │  │
│  │  └──────────┘ └──────────┘ └──────────┘              │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌─────────┐  ┌──────────────────┐  ┌──────────────────┐   │
│  │   S3    │  │    AWS SES       │  │   AWS KMS        │   │
│  │ (media, │  │  (email delivery)│  │ (PII encryption  │   │
│  │  docs)  │  │                  │  │  master key)     │   │
│  └─────────┘  └──────────────────┘  └──────────────────┘   │
│                                                             │
│  ┌──────────────────┐  ┌──────────────────────────────┐    │
│  │  DynamoDB Streams │  │  EventBridge (async events)  │    │
│  │  → Lambda triggers│  │  → Lambda consumers          │    │
│  └──────────────────┘  └──────────────────────────────┘    │
│                                                             │
│  ┌──────────────────┐                                      │
│  │  CloudWatch       │                                      │
│  │  (logs, metrics,  │                                      │
│  │   alarms)         │                                      │
│  └──────────────────┘                                      │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI Framework | Blazor WebAssembly (standalone) | Runs in browser, static hosting on S3/CloudFront, no server affinity, all C# |
| Local API Gateway | Kong (Docker) | Mature, plugin ecosystem for JWT/rate-limiting/CORS, declarative config, mirrors prod API Gateway behavior |
| Prod API Gateway | AWS HTTP API Gateway | Lower latency, lower cost, JWT authorizer + Lambda proxy |
| Service Hosting (dev) | Docker containers (.NET 9 Minimal API) | Each service in own container, Docker Compose orchestrates everything |
| Service Hosting (prod) | AWS Lambda with Native AOT | Same code, different entry point. Conditional `#if LAMBDA` in Program.cs |
| Database | DynamoDB — 6 separate tables, on-demand | Domain separation by table. Each service owns its table(s). On-demand = true pay-per-request. |
| PII Encryption | AES-256-GCM envelope encryption | Data key per record, encrypted by master key (KMS in prod, local file in dev). `IFieldEncryptor` interface. |
| File Storage | S3 (prod) / MinIO (dev) | Compatible APIs. Pre-signed URLs for direct upload/download. |
| Email | SES (prod) / Papercut SMTP (dev) | Papercut catches all email locally for testing — no accidental sends. |
| Auth Tokens | JWT (access, 15 min) + opaque refresh token (DynamoDB Users table) | Each user gets a token on login. Token validated by Kong (dev) or API GW authorizer (prod). |
| IaC | Pulumi (C#) | Same language as application. Type-safe. Supports both AWS and local Docker via providers. |
| Notifications | Digest-first | Events accumulate in Notifications table. Daily digest job queries, groups, renders, sends. Real-time is opt-in overlay. |

### 3.4 Dual-Hosting Pattern (Docker ↔ Lambda)

Each service project supports both hosting modes via conditional compilation:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Shared service registration (identical in both modes)
builder.Services.AddSnappAuth();
builder.Services.AddSnappDynamo(builder.Configuration);
builder.Services.AddSnappEncryption(builder.Configuration);
builder.Services.AddOpenApi();  // OpenAPI spec generation

var app = builder.Build();
app.MapOpenApi();              // serves /openapi/v1.json
app.MapAuthEndpoints();        // same endpoints regardless of host

#if LAMBDA
// When published for Lambda, uses Amazon.Lambda.AspNetCoreServer
await app.RunLambdaAsync();
#else
// When run in Docker or locally
app.Run();
#endif
```

**Dockerfile** (shared pattern for all services):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Snapp.Shared/", "Snapp.Shared/"]
COPY ["src/Snapp.Service.Auth/", "Snapp.Service.Auth/"]
RUN dotnet publish "Snapp.Service.Auth/Snapp.Service.Auth.csproj" \
    -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Snapp.Service.Auth.dll"]
```

### 3.5 Blazor WASM Architecture

```
Snapp.Client (Blazor WASM)
├── Pages/              # Routable page components
│   ├── Auth/           # Login, magic link callback
│   ├── Profile/        # View/edit profile
│   ├── Network/        # Network creation, management, directory
│   ├── Feed/           # Network-scoped feed
│   ├── Discussion/     # Threaded discussions
│   ├── Intelligence/   # Dashboard, benchmarks, valuation
│   ├── Referrals/      # Referral management
│   └── DealRoom/       # Succession/M&A workspace
├── Components/         # Reusable UI components
│   ├── Layout/         # Shell, nav, sidebar
│   ├── Forms/          # Standardized form components
│   ├── Cards/          # Profile cards, post cards, metric cards
│   └── Charts/         # Blazor-native chart components
├── Services/           # Typed HttpClient wrappers
│   ├── IAuthService.cs
│   ├── IUserService.cs
│   ├── INetworkService.cs
│   ├── IContentService.cs
│   ├── IIntelligenceService.cs
│   ├── ITransactionService.cs
│   ├── INotificationService.cs
│   └── ILinkedInService.cs
├── State/              # Client-side state management
│   ├── AuthState.cs    # Current user, tokens
│   ├── NetworkState.cs # Active network context
│   └── AppState.cs     # Global UI state
└── wwwroot/            # Static assets
```

### 3.6 Lambda Service Architecture

Each service follows this internal structure:

```
Snapp.Service.{Name}/
├── Program.cs              # Minimal API setup + DI + dual-host
├── Endpoints/              # Endpoint definitions (MapGet, MapPost, etc.)
├── Handlers/               # Business logic (called by endpoints)
├── Repositories/           # DynamoDB access (implements Shared interfaces)
└── Snapp.Service.{Name}.csproj
```

Shared across all services:

```
Snapp.Shared/
├── DTOs/                   # Request/response types
├── Models/                 # Domain entities
├── Interfaces/             # Repository + service interfaces
├── Encryption/             # IFieldEncryptor interface + implementations
├── Validation/             # Data annotation validators
├── Constants/              # Table names, key prefixes, GSI names
├── Auth/                   # JWT claims, role constants
└── Hosting/                # Shared DI registration extensions
```

Infrastructure:

```
Snapp.Infrastructure/
├── Pulumi/                 # Pulumi stacks (C#)
│   ├── Program.cs          # Pulumi entry point
│   ├── AwsStack.cs         # Production AWS resources
│   └── LocalStack.cs       # Docker Compose generation (optional)
├── Kong/                   # Kong declarative config
│   └── kong.yml            # Routes, services, plugins
├── Docker/
│   └── docker-compose.yml  # Full local environment
└── Scripts/
    └── init-dynamo-local.sh # Create tables in DynamoDB Local
```

---

## 4. Data Model & DynamoDB Design

### 4.1 Table Overview

| Table Name | Owning Service(s) | Purpose |
|------------|--------------------|---------|
| `snapp-users` | Auth, User, LinkedIn | User identity, authentication, profiles, PII (encrypted), LinkedIn tokens, notification preferences |
| `snapp-networks` | Network, Membership | Network metadata, charters, roles, membership records, applications, network metrics |
| `snapp-content` | Content, Discussion | Posts, threads, replies, reactions, feed items |
| `snapp-intel` | Intelligence, Enrichment | Practice data contributions, benchmarks, valuations, public signal cache |
| `snapp-tx` | Transaction | Referrals, reputation scores, deal rooms, deal documents, audit trails |
| `snapp-notif` | Notification | Notification events, digest state, delivery tracking, user preferences override |

All tables share:
- **Billing:** PAY_PER_REQUEST (on-demand)
- **Encryption:** AWS-managed encryption at rest (additive to field-level PII encryption)
- **TTL attribute:** `ExpiresAt` (where applicable)
- **Stream:** NEW_AND_OLD_IMAGES (for event-driven processing)

### 4.2 snapp-users Table

**PK:** `PK` (String) — `USER#<userId>`
**SK:** `SK` (String) — item type qualifier

| Item | PK | SK | Key Attributes | Notes |
|------|----|----|----------------|-------|
| Profile | `USER#<id>` | `PROFILE` | DisplayName, Specialty, Geography, ProfileCompleteness, CreatedAt | Public fields unencrypted |
| PII | `USER#<id>` | `PII` | EncryptedEmail, EncryptedPhone, EncryptedContactInfo, EncryptionKeyId | **All values AES-256-GCM encrypted** |
| Email lookup | `EMAIL#<hashedEmail>` | `USER` | UserId | Email is SHA-256 hashed for lookup; actual email in PII item |
| Auth token | `TOKEN#<code>` | `MAGIC_LINK` | HashedEmail, CreatedAt, ExpiresAt (TTL) | Single-use, 15 min TTL |
| Refresh token | `REFRESH#<tokenHash>` | `SESSION` | UserId, CreatedAt, ExpiresAt (TTL) | 30-day TTL, SHA-256 hashed |
| LinkedIn | `USER#<id>` | `LINKEDIN` | EncryptedLinkedInURN, EncryptedAccessToken, TokenExpiry | Encrypted |
| Notif prefs | `USER#<id>` | `NOTIF_PREFS` | DigestTime (default "07:00"), Timezone, ImmediateTypes[] | Controls digest delivery |
| Rate limit | `RATE#<hashedEmail>#MAGIC` | `WINDOW#<yyyyMMddHHmm>` | Count, ExpiresAt (TTL: 15 min) | Rate limiting magic link requests |

**GSI: GSI-Email**
- PK: `GSI1PK` = `EMAIL#<hashedEmail>`
- SK: `GSI1SK` = `USER`
- Projection: UserId only

**GSI: GSI-Specialty**
- PK: `GSI2PK` = `SPECIALTY#<specialty>`
- SK: `GSI2SK` = `GEO#<geography>`
- Projection: UserId, DisplayName, ProfileCompleteness

### 4.3 snapp-networks Table

**PK:** `PK` (String)
**SK:** `SK` (String)

| Item | PK | SK | Key Attributes |
|------|----|----|----------------|
| Network meta | `NET#<netId>` | `META` | Name, Description, Charter, CreatedByUserId, MemberCount, CreatedAt |
| Role definition | `NET#<netId>` | `ROLE#<roleName>` | Permissions (flags), Description |
| Membership | `NET#<netId>` | `MEMBER#<userId>` | Role, JoinedAt, Status (active/suspended/emeritus), ContributionScore |
| User's networks | `UMEM#<userId>` | `NET#<netId>` | Role, NetworkName (denormalized), JoinedAt |
| Application | `NET#<netId>` | `APP#<timestamp>#<userId>` | Status (pending/approved/denied), ApplicationText, ReviewedBy |
| Metrics snapshot | `NET#<netId>` | `METRICS#<yyyyMMdd>` | MemberCount, PostCount, ActiveMembers, ReferralCount |
| Channel relay | `NET#<netId>` | `RELAY#<channelId>` | EncryptedChannelEmail, Platform (Teams/Slack/Other), Label, IsVerified, RelayTypes[] (posts/milestones/digest), CreatedAt |

**GSI: GSI-UserNetworks**
- PK: `GSI1PK` = `UMEM#<userId>`
- SK: `GSI1SK` = `NET#<netId>`
- Purpose: List all networks a user belongs to

**GSI: GSI-PendingApps**
- PK: `GSI2PK` = `APPSTATUS#<netId>#pending`
- SK: `GSI2SK` = timestamp
- Purpose: List pending applications for steward review

### 4.4 snapp-content Table

**PK:** `PK` (String)
**SK:** `SK` (String)

| Item | PK | SK | Key Attributes |
|------|----|----|----------------|
| Post | `FEED#<netId>` | `POST#<timestamp>#<postId>` | AuthorUserId, Content, PostType, ReactionCounts |
| Post by user | `UPOST#<userId>` | `POST#<timestamp>#<postId>` | NetworkId, Content, PostType |
| Thread | `DISC#<netId>` | `THREAD#<timestamp>#<threadId>` | Title, AuthorUserId, ReplyCount, LastReplyAt |
| Reply | `THREAD#<threadId>` | `REPLY#<timestamp>#<replyId>` | AuthorUserId, Content |
| Reaction | `REACT#<postId>` | `USER#<userId>` | ReactionType (like/insightful/support) |

**GSI: GSI-UserPosts**
- PK: `GSI1PK` = `UPOST#<userId>`
- SK: `GSI1SK` = `POST#<timestamp>#<postId>`
- Purpose: User's post history across networks

### 4.5 snapp-intel Table

**PK:** `PK` (String)
**SK:** `SK` (String)

This table supports a **domain-agnostic, multi-dimensional intelligence model** informed by PraxisIQ's existing data infrastructure (71 relational tables, 6 scoring dimensions, 14 signal families, career stage classification). The data categories are not hard-coded to dentistry — each network vertical defines its own KPI taxonomy via configurable scoring dimensions.

| Item | PK | SK | Key Attributes |
|------|----|----|----------------|
| Practice data (contributed) | `PDATA#<userId>` | `DIM#<dimension>#<category>` | DataPoints (map), ConfidenceContribution, SubmittedAt, Source (self-reported/integrated/public) |
| Scoring profile | `SCORE#<userId>` | `CURRENT` | DimensionScores (map: dimension→score 0-100), OverallScore, ConfidenceLevel (high/medium/low), ComputedAt |
| Scoring history | `SCORE#<userId>` | `SNAP#<timestamp>` | Same as current, for trending |
| Career stage | `STAGE#<userId>` | `CURRENT` | Stage (enum), ConfidenceLevel, RiskFlags (list), TriggerSignals (list), ComputedAt |
| Career stage history | `STAGE#<userId>` | `SNAP#<timestamp>` | Same, for tracking transitions |
| Valuation | `VAL#<userId>` | `SNAPSHOT#<timestamp>` | Downside, Base, Upside, ConfidenceScore, Drivers (map), Multiple, EbitdaMargin |
| Valuation (current) | `VAL#<userId>` | `CURRENT` | Same as snapshot, pointer to latest |
| Benchmark (geographic) | `BENCH#<vertical>#<geo>#<geoLevel>` | `METRIC#<metricName>` | P25, P50, P75, Mean, SampleSize, ComputedAt |
| Benchmark (cohort) | `COHORT#<vertical>#<specialty>#<sizeband>` | `METRIC#<metricName>` | P25, P50, P75, Mean, SampleSize, ComputedAt |
| Market profile | `MKT#<geoId>` | `PROFILE` | ProviderDensity, PopulationServed, MedianIncome, CompetitorCount, DsoPresence, GrowthTrend |
| Market signal | `MKT#<geoId>` | `SIGNAL#<signalType>#<timestamp>` | Value, Source, Details |
| Public signal cache | `SIGNAL#<userId>` | `SRC#<source>#<key>` | Value, Confidence (0.0-1.0), FetchedAt, ExpiresAt (TTL) |
| Scoring dimension def | `DIMDEF#<vertical>` | `DIM#<dimensionName>` | DisplayName, Weight, Thresholds (map: strong/acceptable/weak), KPIs (list) |
| Risk flag | `RISK#<userId>` | `FLAG#<flagType>` | Severity (high/medium/low), Evidence (map), DetectedAt, ResolvedAt |
| Question (pending) | `QPEND#<userId>` | `Q#<questionId>` | QuestionType (confirm_data/confirm_relationship/estimate_value), Prompt, Choices, UnlockDescription, GapWeight, SourceSignalId, CreatedAt, ExpiresAt (TTL) |
| Question (answered) | `QANS#<userId>` | `Q#<questionId>` | Answer, AnsweredAt, UnlockTriggered (bool), ConfidenceDelta |
| Unlock record | `UNLOCK#<userId>` | `UNL#<timestamp>#<unlockId>` | UnlockType, Description, TriggeredByQuestionId, IntelligenceRevealed (map) |
| Progression | `PROG#<userId>` | `CURRENT` | TotalQuestionsAnswered, TotalUnlocks, CurrentStreak, LongestStreak, LastAnsweredAt |

**Scoring Dimensions** (configurable per network vertical — weights and KPIs defined in Vertical Configuration Packs):

| Dimension | Default Weight | Generalized KPI Pattern | Examples Across Verticals |
|-----------|---------------|------------------------|---------------------------|
| Financial Health | 25% | Revenue performance vs. industry benchmarks | Revenue, margins, collections efficiency, overhead ratios |
| Owner/Key-Person Risk | 20% | Key-person dependency and business transferability | Owner's share of production/billing, partner count, succession planning |
| Operations | 20% | Operational efficiency relative to capacity | Utilization rates, throughput, service mix ratios, appointment/case volume |
| Client/Customer Base | 15% | Client acquisition, retention, and satisfaction health | New clients/mo, retention rate, online reputation, client demographics |
| Revenue Diversification | 10% | Revenue source concentration risk | Source mix percentages, single-source concentration, recurring vs. transactional |
| Market Position | 10% | Competitive landscape and market opportunity | Practitioner density, competitor proximity, consolidation pressure, demographic trends |

**Career Stages** (configurable per vertical — stage definitions and signal mappings in Vertical Configuration Packs):

| Stage | Generalized Signals | Risk Flags |
|-------|---------------------|------------|
| Training/Entry | Recent credentialing, no independent activity | — |
| Junior/Associate | Co-located with established practitioner, low independent activity | — |
| Acquisition/Launch | Recent registration, growing activity volume, entity formation | — |
| Growth | Expanding staff, increasing volume, additional locations | Overextension risk |
| Mature | Stable metrics, 10-20 year tenure, established reputation | Stagnation risk |
| Pre-Exit | 20+ year tenure, declining professional development, solo practice, lease nearing expiry | Retirement risk, succession risk |

**GSI: GSI-BenchmarkLookup**
- PK: `GSI1PK` = `BENCH#<vertical>#<geo>#<geoLevel>` or `COHORT#<vertical>#<specialty>#<sizeband>`
- SK: `GSI1SK` = `METRIC#<metricName>`
- Purpose: Fast benchmark lookups at any geographic or cohort level

**GSI: GSI-RiskFlags**
- PK: `GSI2PK` = `RISK#<userId>`
- SK: `GSI2SK` = `FLAG#<flagType>`
- Purpose: Quick risk profile retrieval for a user

### 4.6 snapp-tx Table

**PK:** `PK` (String)
**SK:** `SK` (String)

| Item | PK | SK | Key Attributes |
|------|----|----|----------------|
| Referral | `REF#<refId>` | `META` | SenderUserId, ReceiverUserId, NetworkId, Specialty, Status, CreatedAt, OutcomeRecordedAt |
| Referral by sender | `UREF#<userId>#SENT` | `REF#<timestamp>#<refId>` | ReceiverUserId, Status |
| Referral by receiver | `UREF#<userId>#RECV` | `REF#<timestamp>#<refId>` | SenderUserId, Status |
| Reputation | `REP#<userId>` | `CURRENT` | OverallScore, ReferralScore, ContributionScore, AttestationScore, ComputedAt |
| Reputation history | `REP#<userId>` | `SNAP#<timestamp>` | Same as current, for trending |
| Peer attestation | `ATTEST#<targetUserId>` | `FROM#<attestorUserId>` | Domain, CompetencyArea, Text, CreatedAt |
| Deal room | `DEAL#<dealId>` | `META` | Name, CreatedByUserId, Status, CreatedAt |
| Deal participant | `DEAL#<dealId>` | `PART#<userId>` | Role (seller/buyer/advisor), AddedAt |
| Deal document | `DEAL#<dealId>` | `DOC#<timestamp>#<docId>` | Filename, S3Key, UploadedByUserId, Size |
| Deal audit | `DEAL#<dealId>` | `AUDIT#<timestamp>#<eventId>` | Action, ActorUserId, Details |

**GSI: GSI-UserReferrals**
- PK: `GSI1PK` = `UREF#<userId>#SENT` or `UREF#<userId>#RECV`
- SK: `GSI1SK` = `REF#<timestamp>#<refId>`

**GSI: GSI-OpenReferrals**
- PK: `GSI2PK` = `REFSTATUS#<status>`
- SK: `GSI2SK` = `<createdAt>#<refId>`
- Purpose: Find all open referrals for follow-up scheduling

### 4.7 snapp-notif Table

**PK:** `PK` (String)
**SK:** `SK` (String)

| Item | PK | SK | Key Attributes |
|------|----|----|----------------|
| Notification | `NOTIF#<userId>` | `EVENT#<timestamp>#<notifId>` | Type, Category, Title, Body, SourceEntityId, IsRead, IsDigested |
| Digest record | `DIGEST#<userId>` | `SENT#<yyyyMMdd>` | NotificationCount, SentAt, Categories (map of counts) |
| Digest queue | `DQUEUE#<digestHour>` | `USER#<userId>` | Timezone, PreferredTime, LastDigestSent | Partition by delivery hour for scheduled processing |

**GSI: GSI-UndigestedNotifs**
- PK: `GSI1PK` = `NOTIF#<userId>`
- SK: `GSI1SK` = `EVENT#<timestamp>#<notifId>`
- Filter: `IsDigested = false`
- Purpose: Gather all undigested notifications for daily digest

**GSI: GSI-DigestQueue**
- PK: `GSI2PK` = `DQUEUE#<digestHour>`
- SK: `GSI2SK` = `USER#<userId>`
- Purpose: Scheduled job queries users whose digest is due for the current hour

---

## 5. Module Breakdown

Modules are ordered by **dependency** (fewest dependencies first) and **priority** (foundational capabilities before advanced features).

### Tier 0: Foundation (No Dependencies)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M0.1 — Local Dev Environment** | Docker Compose: DynamoDB Local, Kong, MinIO (S3), Papercut SMTP, kong.yml config, init-dynamo-local.sh, setup-local.sh. This is the first thing built — all development runs against this. | Nothing |
| **M0.2 — Pulumi Infrastructure (deferred to AWS phase-in)** | DynamoDB tables + GSIs, S3 buckets, API Gateway, CloudFront, SES, KMS key, IAM roles, Pulumi stacks. Built when ready for staging/production. Not needed for local development. | Nothing |
| **M0.3 — Shared Library** | `Snapp.Shared`: DTOs, models, interfaces, encryption interface, constants, validation | Nothing |
| **M0.4 — Blazor Shell** | App shell: layout, routing, navigation, error handling, HttpClient configuration, auth state provider | Nothing |
| **M0.5 — OpenAPI & SDK Pipeline** | Per-service OpenAPI generation, spec merge tool, Kiota C# SDK generation, Swagger UI container, CI validation | M0.3 |

### Tier 1: Identity (Depends on Tier 0)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M1.1 — Field Encryption Library** | `IFieldEncryptor` with KMS (prod) and local file key (dev) implementations. AES-256-GCM envelope encryption. | M0.3 |
| **M1.2 — Magic Link Auth Service** | Docker container + Lambda: generate magic link, validate, issue JWT + refresh token, refresh, logout | M0.1, M0.2, M0.3, M1.1 |
| **M1.3 — Kong JWT Plugin Config** | Kong declarative config for JWT validation, route protection, CORS, rate limiting | M0.2, M1.2 |
| **M1.4 — API Gateway Authorizer** | Lambda authorizer for prod: validate JWT, extract claims | M0.1, M1.2 |
| **M1.5 — Auth UI** | Blazor pages: login (email entry), magic link callback, auth state management, protected route wrapper | M0.4, M1.2 |
| **M1.6 — User Profile Service** | Docker container + Lambda: create profile, get profile, update profile, profile completeness scoring, PII encryption | M0.1, M0.2, M0.3, M1.1, M1.2 |
| **M1.7 — Profile UI** | Blazor pages: view profile, edit profile, onboarding wizard, profile completeness indicator | M0.4, M1.5, M1.6 |

### Tier 2: Networks (Depends on Tier 1)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M2.1 — Network Service** | Docker container + Lambda: create network, update config, manage roles/permissions, network directory listing | M0.1, M0.2, M0.3, M1.2 |
| **M2.2 — Membership Service** | Endpoints within Network Service: apply to network, approve/deny, manage membership, invitation system | M2.1 |
| **M2.3 — Network UI** | Blazor pages: create network wizard, network settings, member directory, application review dashboard | M0.4, M1.5, M2.1 |

### Tier 3: Communication (Depends on Tier 2)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M3.1 — Discussion Service** | Endpoints in Content Service: create thread, post reply, list threads, list replies, edit/delete own posts | M0.1, M0.2, M0.3, M1.2, M2.1 |
| **M3.2 — Feed Service** | Endpoints in Content Service: create post, list network feed, list user posts, reactions | M0.1, M0.2, M0.3, M1.2, M2.1 |
| **M3.3 — Discussion UI** | Blazor pages: thread list, thread view with replies, compose/reply, channel selector | M0.4, M1.5, M2.3, M3.1 |
| **M3.4 — Feed UI** | Blazor pages: network feed, post composer, post cards, reactions, polls | M0.4, M1.5, M2.3, M3.2 |
| **M3.5 — Notification Service** | Docker container + Lambda: create notification event, list user notifications, mark read; digest aggregation and delivery | M0.1, M0.2, M0.3, M1.1, M1.2 |
| **M3.6 — Notification Digest Job** | Scheduled Lambda / cron Docker job: query digest queue by hour, gather undigested notifications per user, render Razor email template, send via SES/Papercut | M3.5 |
| **M3.7 — Notification UI** | Blazor component: notification bell (unread count), notification drawer, digest preferences | M0.4, M1.5, M3.5 |

### Tier 4: Intelligence (Depends on Tier 2)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M4.1 — Data Contribution Service** | Endpoints in Intelligence Service: submit practice data by dimension, validate inputs per vertical schema, store with confidence tracking | M0.1, M0.3, M1.2, M2.1 |
| **M4.2 — Scoring Engine** | Deterministic multi-dimensional scorer: evaluates 6 configurable dimensions (financial, provider risk, operations, client base, revenue mix, market position), produces composite score + confidence level + risk flags. Dimension definitions configurable per network vertical. | M4.1 |
| **M4.3 — Benchmark Engine** | 4-level geographic benchmarking (national → state → county → cohort): ingests association data + public datasets + guild-contributed data, computes P25/P50/P75 per metric at each level, positions member within cohort. Anonymity threshold ≥5. | M4.1 |
| **M4.4 — Career Stage Classifier** | Deterministic classifier using tenure, co-location, production volume, entity type, reputation signals. 6 stages (Training → Associate → Acquisition → Growth → Mature → Pre-Exit). Confidence levels. Risk flags: retirement, succession, overextension, key-person dependency. | M4.1, M4.2 |
| **M4.5 — Valuation Engine** | Three-case financial model (downside/base/upside) calibrated against benchmark data: uses scoring profile + contributed data + public signals + career stage. Driver attribution, scenario modeling. | M4.1, M4.2, M4.3, M4.4 |
| **M4.6 — Market Intelligence Service** | Market profile computation per geography: practitioner density, competitive landscape, consolidation pressure (roll-up/aggregator presence), demographic trends, workforce supply/demand. Built from census, BLS, practitioner registry, and business listing data. | M4.1, M4.3 |
| **M4.7 — Practice Dashboard UI** | Blazor pages: multi-dimensional radar/spider chart, KPI grid with trend arrows, composite score, career stage indicator, risk flags, confidence bar, contribution prompts | M0.4, M1.5, M4.1, M4.2, M4.3, M4.4 |
| **M4.8 — Benchmarking UI** | Blazor pages: geographic drill-down (national → state → county → cohort), percentile visualization at each level, peer comparison (anonymized), market intelligence map | M0.4, M1.5, M4.3, M4.6 |
| **M4.9 — Valuation UI** | Blazor pages: valuation range, three-case breakdown, driver analysis, scenario modeling, career stage context, trend over time | M0.4, M1.5, M4.5 |
| **M4.10 — Gap Detection & Survey Assembly** | Analyzes intelligence gaps per user, generates SurveyIQ survey graphs dynamically (data confirmation, relationship validation, value estimation questions). Uses SurveyIQ C# SDK to create surveys, set adaptive weights, and generate embed tokens. SNAPP owns the "what to ask" logic; SurveyIQ owns the "how to present and collect" logic. | M4.1, M4.2, M4.4, M7.1 |
| **M4.11 — Unlock Engine** | Listens for SurveyIQ `session.completed` webhooks. Maps answered questions to intelligence rewards: confirmed data raises confidence, confirmed relationships add graph edges, answered value questions reveal cohort benchmarks. Immediate feedback. | M4.10, M4.3, M4.5 |
| **M4.12 — Question UI & Progression** | Blazor components: embedded SurveyIQ widget (via embed token/iframe or SurveyIQ WASM client), inline question card in dashboard, digest-embedded question links, progression indicator, "insights unlocked" counter, streak display | M0.4, M1.5, M4.10, M4.11 |

### Tier 5: Transactions (Depends on Tiers 2 + 3)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M5.1 — Referral Service** | Endpoints in Transaction Service: create referral, update status, record outcome, matching engine | M0.1, M0.2, M0.3, M1.2, M2.1 |
| **M5.2 — Reputation Service** | Endpoints + event trigger: compute reputation from transactions + contributions + attestations, decay, anti-gaming | M5.1, M4.1 |
| **M5.3 — Referral UI** | Blazor pages: create referral form, referral inbox, outcome recording, referral history | M0.4, M1.5, M5.1 |
| **M5.4 — Reputation UI** | Blazor components: reputation badge, detail breakdown, network standing | M0.4, M1.5, M5.2 |
| **M5.5 — Deal Room Service** | Endpoints in Transaction Service: create deal room, manage participants, document metadata, audit trail | M0.1, M0.2, M0.3, M1.2, M2.1 |
| **M5.6 — Deal Room UI** | Blazor pages: deal room dashboard, document upload/download, participant management, activity log | M0.4, M1.5, M5.5 |

### Tier 6: External Integration (Depends on Tier 1)

| Module | Description | Depends On |
|--------|-------------|------------|
| **M6.1 — LinkedIn OAuth Service** | Endpoints in LinkedIn Service: OAuth 2.0 flow, token exchange, profile data retrieval, token refresh | M0.1, M0.2, M0.3, M1.1, M1.2 |
| **M6.2 — LinkedIn Share Service** | Endpoints in LinkedIn Service: format content as LinkedIn post, publish via Share API, track cross-posts | M6.1 |
| **M6.3 — LinkedIn UI** | Blazor components: "Link LinkedIn" button, cross-post toggle, LinkedIn profile preview | M0.4, M1.5, M6.1, M6.2 |
| **M6.4 — Discord Bridge Service** | Docker container + Lambda: Discord bot, webhook relay, channel mapping, auth bridge | M0.1, M0.2, M2.1 |
| **M6.5 — Channel Email Relay** | Relay SNAPP content to Teams/Slack channels via their inbound email addresses. Steward configures channel email, SNAPP sends formatted HTML email on post/milestone/digest events. Uses existing SES/SMTP. | M0.1, M0.2, M0.3, M2.1, M3.5 |

### Tier 7: Data Enrichment & Public Intelligence (Depends on Tiers 1 + 4)

This tier is substantially larger than other tiers because PraxisIQ's existing data infrastructure (see github.com/PraxisIQ-Network) encompasses 71 relational tables, 400+ source files, and 29GB of public data across federal, state, and commercial sources. SNAPP's enrichment layer exposes this intelligence through the API. The enrichment modules run as scheduled jobs and populate the snapp-intel table.

| Module | Description | Depends On |
|--------|-------------|------------|
| **M7.1 — Provider/Practitioner Registry** | Scheduled import from the authoritative registry for the vertical. For healthcare verticals this is NPI/NPPES; for financial advisory it's SEC/FINRA; for legal it's state bar records. Extracts: practitioner identity, taxonomy/specialty, practice addresses, registration dates (tenure proxy). Foundation for all practitioner-level intelligence. | M0.1, M0.3, M1.6 |
| **M7.2 — Regulatory & Claims Data** | Scheduled import of government regulatory data relevant to the vertical. Examples: Medicare prescriber data (activity proxy, patient demographics), graduation/credentialing records (career stage input), service cost data by geography. Each vertical defines which regulatory datasets apply. | M7.1 |
| **M7.3 — Association & Industry Benchmark Data** | Import national/state benchmarks from trade associations and industry bodies. Examples: practice financial surveys with quartiles, market participation rates, consolidation trends, supply/demand time series, staff compensation. These calibrate the scoring engine's thresholds per vertical. | M4.2, M4.3 |
| **M7.4 — Business Listing Integration** | Scheduled crawl of public business directories (Google Places, Yelp, industry-specific directories). Multi-pass matching to registry records (phone exact → address exact → name+location fuzzy) with confidence scoring. Yields: reputation signals (ratings, review counts), web presence, operational indicators. | M7.1 |
| **M7.5 — Geographic & Economic Data** | Import county/state-level economic context: Census ACS (demographics, income), BLS QCEW (industry employment, wages), BEA (personal income, GDP), HUD ZIP-FIPS crosswalk. Domain-agnostic — powers market intelligence profiles for any vertical. | M0.1, M0.3 |
| **M7.6 — State Licensing & Regulatory Filings** | Per-state license board and corporate filing data. Fuzzy-matched to registry records. Includes UCC filings for corporate entity detection (roll-up/consolidation signals). Feeds career stage classifier and entity structure detection. Coverage expands state-by-state over time. | M7.1 |
| **M7.7 — Job Posting & Workforce Intelligence** | Monitoring of general and industry-specific job boards for hiring signals. Extracts: posting frequency, role repetition, urgency language, compensation signals. BLS compensation baselines. Staffing agency rate cards where available. Feeds workforce pressure scoring and compensation benchmarking. | M0.1, M0.3 |
| **M7.8 — Guild-Generated Compensation Benchmarks** | Extends benchmark engine with member-contributed compensation data aggregated anonymously by role, market, and practice size. The guild's unique value — cheaper, more current, more specific than any commercial source because it comes from actual operators in the same specialty and market. | M4.1, M4.3 |
| **M7.9 — Vertical Configuration Packs** | Domain-specific scoring dimension definitions, KPI taxonomies, career stage rules, registry sources, regulatory datasets, and benchmark thresholds — packaged as configuration per vertical. Each pack defines what the scoring engine measures, which public data sources feed it, and how. New verticals are added by creating a new pack, not by writing new code. | M4.2 |

**Data Source Coverage by Signal Family (Generalized):**

| Signal Family | Public Sources (vary by vertical) | Guild-Contributed | Coverage Pattern |
|---------------|----------------------------------|-------------------|-----------------|
| Practitioner identity & credentials | Registry (NPI, SEC, state bar, etc.), licensing boards | Profile, certifications | Strong — registries exist for most regulated professions |
| Financial health | Industry association benchmarks (quartiles) | Revenue, margins, overhead | National benchmark from associations + member-specific from contributions |
| Owner/key-person risk | Registry tenure, co-location analysis, credentialing records | Owner production %, associate/partner count | Strong from registration dates + structural inference |
| Operations | Regulatory activity data (where available), utilization proxies | Capacity, throughput, client volume, retention | Partial — availability varies sharply by vertical |
| Client/patient base | Business listing reviews (Google, Yelp), regulatory demographics | New clients/mo, retention, satisfaction | Mixed — reputation signals public, volume metrics contributed |
| Revenue diversification | Regulatory participation rates (where available) | Revenue source breakdown | Vertical-dependent — healthcare has public payer data, others rely on contributions |
| Market position | Census demographics, BLS employment, BEA income, registry density | — | Strong — fully public, domain-agnostic |
| Workforce | BLS OEWS wages, job posting monitoring, association compensation surveys | Staff count, tenure, compensation | National baseline always available; local granularity from guild |
| Career stage | Registration date, credentialing records, co-location, activity proxies | Self-reported intent, advisor engagement | Strong deterministic classifier from public signals |
| Competitive landscape | Registry density, business listings, corporate filings (consolidation detection) | — | Strong — public data reveals market structure |
| Facility & real estate | County assessor (future), permit records (future) | Own vs. lease, lease terms | Mostly member-contributed |
| Digital presence | Business listings, website analysis (future) | — | Partial |
| Referral/collaboration patterns | Co-location + complementary specialty inference | Explicit referral tracking on platform | Inferred from structural data + explicit from guild activity |
| Succession/exit readiness | Career stage + risk flags composite | Prior acquirer contact, advisor engagement, time horizon | Composite of public + private signals |

---

## 6. Dependency Graph & Build Order

```
EXTERNAL ──────────────────────────────────────────────
  SurveyIQ (external service, same stack — C#/.NET, DynamoDB,
            Kong, Kiota SDK). SNAPP integrates as a tenant
            via API key + C# SDK + webhooks.
            Available from Tier 4 onward (M4.10+).

TIER 0: FOUNDATION (no dependencies) ─────────────────
  M0.1 Local Dev Environment (Docker Compose + Kong + DynamoDB Local + MinIO + Papercut)
  M0.2 Pulumi Infrastructure (deferred — AWS phase-in only)
  M0.3 Shared Library
  M0.4 Blazor Shell
  M0.5 OpenAPI & SDK Pipeline ←── M0.3
          │
TIER 1: IDENTITY ──────────────────────────────────────
  M1.1 Field Encryption Library ←── M0.3
  M1.2 Magic Link Auth Service ←── M0.1, M0.3, M1.1
  M1.3 Kong JWT Plugin Config ←── M0.1, M1.2
  M1.4 API GW Authorizer (deferred — prod only) ←── M0.2, M1.2
  M1.5 Auth UI ←── M0.4, M1.2
  M1.6 User Profile Service ←── M0.1, M0.3, M1.1, M1.2
  M1.7 Profile UI ←── M0.4, M1.5, M1.6
          │
TIER 2: NETWORKS ──────────────────────────────────────
  M2.1 Network Service ←── M0.1, M0.3, M1.2
  M2.2 Membership Service ←── M2.1
  M2.3 Network UI ←── M0.4, M1.5, M2.1
          │
TIER 3: COMMUNICATION (depends on Tier 2) ────────────
  M3.1 Discussion Service ←── M0.1, M0.3, M1.2, M2.1
  M3.2 Feed Service ←── M0.1, M0.3, M1.2, M2.1
  M3.3 Discussion UI ←── M0.4, M1.5, M2.3, M3.1
  M3.4 Feed UI ←── M0.4, M1.5, M2.3, M3.2
  M3.5 Notification Service ←── M0.1, M0.3, M1.1, M1.2
  M3.6 Notification Digest Job ←── M3.5
  M3.7 Notification UI ←── M0.4, M1.5, M3.5
          │
TIER 4: INTELLIGENCE (parallel with Tier 3) ──────────
  M4.1 Data Contribution Service ←── M0.1, M0.3, M1.2, M2.1
  M4.2 Scoring Engine ←── M4.1
  M4.3 Benchmark Engine ←── M4.1
  M4.4 Career Stage Classifier ←── M4.1, M4.2
  M4.5 Valuation Engine ←── M4.1, M4.2, M4.3, M4.4
  M4.6 Market Intelligence Service ←── M4.1, M4.3
  M4.7 Practice Dashboard UI ←── M0.4, M1.5, M4.1, M4.2, M4.3, M4.4
  M4.8 Benchmarking UI ←── M0.4, M1.5, M4.3, M4.6
  M4.9 Valuation UI ←── M0.4, M1.5, M4.5
  M4.10 Gap Detection & Survey Assembly ←── M4.1, M4.2, M4.4, M7.1, SurveyIQ
  M4.11 Unlock Engine ←── M4.10, M4.3, M4.5
  M4.12 Question UI & Progression ←── M0.4, M1.5, M4.10, M4.11
          │
TIER 5: TRANSACTIONS (depends on Tiers 2 + 3) ────────
  M5.1 Referral Service ←── M0.1, M0.3, M1.2, M2.1
  M5.2 Reputation Service ←── M5.1, M4.1
  M5.3 Referral UI ←── M0.4, M1.5, M5.1
  M5.4 Reputation UI ←── M0.4, M1.5, M5.2
  M5.5 Deal Room Service ←── M0.1, M0.3, M1.2, M2.1
  M5.6 Deal Room UI ←── M0.4, M1.5, M5.5
          │
TIER 6: EXTERNAL INTEGRATION (can start after Tier 1) ─
  M6.1 LinkedIn OAuth Service ←── M0.1, M0.3, M1.1, M1.2
  M6.2 LinkedIn Share Service ←── M6.1
  M6.3 LinkedIn UI ←── M0.4, M1.5, M6.1, M6.2
  M6.4 Discord Bridge Service ←── M0.1, M2.1
  M6.5 Channel Email Relay (Teams/Slack) ←── M0.1, M0.3, M2.1, M3.5
          │
TIER 7: DATA ENRICHMENT (depends on Tiers 1 + 4) ─────
  M7.1 Provider/Practitioner Registry ←── M0.1, M0.3, M1.6
  M7.2 Regulatory & Claims Data ←── M7.1
  M7.3 Association & Industry Benchmarks ←── M4.2, M4.3
  M7.4 Business Listing Integration ←── M7.1
  M7.5 Geographic & Economic Data ←── M0.1, M0.3
  M7.6 State Licensing & Regulatory Filings ←── M7.1
  M7.7 Job Posting & Workforce Intel ←── M0.1, M0.3
  M7.8 Guild Compensation Benchmarks ←── M4.1, M4.3
  M7.9 Vertical Configuration Packs ←── M4.2

DEFERRED (AWS phase-in) ──────────────────────────────
  M0.2 Pulumi Infrastructure
  M1.4 API Gateway Authorizer
```

### Recommended Build Sequence

All sprints run against local Docker infrastructure until the AWS phase-in sprint.

| Sprint | Modules | Outcome |
|--------|---------|---------|
| **S1** | M0.1, M0.3, M0.4, M0.5 | Local env running (DynamoDB Local + Kong + MinIO + Papercut), shared library, Blazor shell, OpenAPI pipeline. All local, no AWS. |
| **S2** | M1.1, M1.2, M1.3, M1.5 | Magic link auth working locally. PII encryption. Kong validates JWTs. Auth UI functional. |
| **S3** | M1.6, M1.7 | User profiles with encrypted PII, onboarding wizard |
| **S4** | M2.1, M2.2, M2.3 | Networks: creation, membership, governance, application workflow |
| **S5** | M3.1, M3.2, M3.3, M3.4 | Discussions and feed — the network is alive with conversation |
| **S6** | M3.5, M3.6, M3.7 | Notification service, daily digest job, notification UI |
| **S7** | M4.1, M4.2, M4.3, M4.7, M4.8 | Data contribution, scoring engine, benchmark engine, dashboard + benchmarking UI |
| **S8** | M4.4, M4.5, M4.6, M4.9 | Career stage classifier, valuation engine, market intelligence, valuation UI |
| **S9** | M7.1, M7.5, M7.9 | Provider registry, geographic/economic data, first vertical config pack — enrichment foundation |
| **S10** | M7.2, M7.3, M7.4 | Regulatory data, association benchmarks, business listings — calibrate scoring + benchmarks from public data |
| **S11** | M4.10, M4.11, M4.12 | SurveyIQ integration: gap detection, survey assembly, unlock engine, question UI — gamified data validation |
| **S12** | M5.1, M5.2, M5.3, M5.4 | Referrals and reputation — the transaction layer |
| **S13** | M6.1, M6.2, M6.3 | LinkedIn integration |
| **S14** | M5.5, M5.6 | Deal room for succession/M&A |
| **S15** | M7.6, M7.7, M7.8 | State licensing, job posting intelligence, guild compensation benchmarks |
| **S16** | M6.4, M6.5 | Discord bridge, Teams/Slack channel email relay |
| **AWS** | M0.2, M1.4 | Pulumi infrastructure deployment, API Gateway authorizer — production readiness |

---

## 7. Work Unit Specifications

Each work unit is specified for implementation by a Claude Code agent in a single session. Every WU includes: inputs, outputs, detailed specification, and test criteria.

---

### WU-0.1: Local Development Environment

**Input:** Docker, Docker Compose
**Output:** `docker-compose.yml` + supporting config that runs the full local stack — this is built FIRST, before anything else

---

**Specification:**

```
Create Snapp.Infrastructure/Docker/docker-compose.yml:

Services:
  dynamodb-local:
    image: amazon/dynamodb-local:latest
    ports: ["8042:8000"]
    command: ["-jar", "DynamoDBLocal.jar", "-sharedDb"]
    volumes: dynamodb-data:/home/dynamodblocal/data

  kong-database:
    image: postgres:16-alpine
    environment: POSTGRES_DB=kong, POSTGRES_USER=kong,
                 POSTGRES_PASSWORD=kongpass
    ports: ["5432:5432"]

  kong-migration:
    image: kong:3.6
    command: kong migrations bootstrap
    depends_on: [kong-database]

  kong:
    image: kong:3.6
    ports: ["8000:8000", "8001:8001"]
    environment:
      KONG_DATABASE: postgres
      KONG_PG_HOST: kong-database
      KONG_PROXY_LISTEN: 0.0.0.0:8000
      KONG_ADMIN_LISTEN: 0.0.0.0:8001
    depends_on: [kong-migration]

  minio:
    image: minio/minio:latest
    ports: ["9000:9000", "9001:9001"]
    command: server /data --console-address ":9001"
    environment: MINIO_ROOT_USER=minioadmin,
                 MINIO_ROOT_PASSWORD=minioadmin
    volumes: minio-data:/data

  papercut:
    image: changemakerstudiosus/papercut-smtp:latest
    ports: ["1025:25", "8025:80"]

  # Service containers (initially commented out —
  # uncommented as each service is built)
  # snapp-auth:
  #   build: ../../src/Snapp.Service.Auth
  #   ports: ["8081:8080"]
  #   depends_on: [dynamodb-local]
  #   environment: see env section below

Common environment variables for all services:
  DYNAMODB__SERVICEURL: http://dynamodb-local:8000
  DYNAMODB__REGION: us-east-1
  S3__SERVICEURL: http://minio:9000
  S3__ACCESSKEY: minioadmin
  S3__SECRETKEY: minioadmin
  ENCRYPTION__PROVIDER: LocalFile
  ENCRYPTION__LOCALKEYPATH: /keys/dev-master.key
  SMTP__HOST: papercut
  SMTP__PORT: 25
  AUTH__ISSUER: snapp-dev
  AUTH__AUDIENCE: snapp-dev
  AUTH__SIGNINGKEYPATH: /keys/jwt-signing.pem

Volumes:
  dynamodb-data:
  minio-data:

Create Snapp.Infrastructure/Docker/keys/:
  - dev-master.key: 256-bit random key for local PII encryption
  - jwt-signing.pem: RSA 2048-bit key pair for JWT signing

Create Snapp.Infrastructure/Kong/kong.yml:
  (Declarative config, loaded after Kong starts via Admin API)

  Services:
    - name: snapp-auth
      url: http://snapp-auth:8080
      routes:
        - paths: ["/api/auth"]
          strip_path: false
    - name: snapp-user
      url: http://snapp-user:8080
      routes:
        - paths: ["/api/users"]
          strip_path: false
    ... (one service entry per API service)

  Plugins (global):
    - name: cors
      config:
        origins: ["http://localhost:5000"]
        methods: [GET, POST, PUT, DELETE, OPTIONS]
        headers: [Authorization, Content-Type]
    - name: request-transformer
      config:
        add.headers: ["X-Request-ID:$(uuid)"]

  Plugins (per-route, excluding /api/auth/magic-link,
           /api/auth/validate, /api/auth/refresh):
    - name: jwt
      config:
        key_claim_name: kid
        claims_to_verify: [exp]

Create Snapp.Infrastructure/Scripts/init-dynamo-local.sh:
  - Creates all 6 tables with all GSIs in DynamoDB Local
  - Uses AWS CLI with --endpoint-url http://localhost:8042
  - Idempotent (checks if table exists before creating)

Create Snapp.Infrastructure/Scripts/init-kong.sh:
  - Waits for Kong Admin API to be ready
  - Loads kong.yml via Admin API
  - Configures JWT credential for local dev signing key

Create Snapp.Infrastructure/Scripts/init-minio.sh:
  - Creates snapp-media and snapp-client buckets

Create Snapp.Infrastructure/Scripts/setup-local.sh:
  - Orchestrator: docker-compose up -d
  - Wait for services
  - Run init-dynamo-local.sh
  - Run init-kong.sh
  - Run init-minio.sh
  - Generate dev keys if not present
  - Print "Local environment ready" with URLs

Test criteria:
- `docker-compose up -d` starts all infrastructure services
- `setup-local.sh` completes without error
- DynamoDB Local responds on port 8042
- Kong responds on port 8000
- MinIO responds on port 9000
- Papercut web UI on port 8025
- Kong routes /api/auth/* (unprotected) return 503
  (service not yet running, but route exists)
- Kong routes /api/users/* return 401 (JWT required)
```

---

### WU-0.2: Pulumi Infrastructure Stacks (Deferred to AWS Phase-In)

**Input:** AWS account credentials, domain name
**Output:** Pulumi C# project that deploys all AWS resources
**Note:** This WU is NOT needed for local development. Build it when ready to deploy to staging/production. All services must be fully functional against local Docker infrastructure first.
**Specification:**

```
Create Snapp.Infrastructure/Pulumi/ with:

Program.cs — Pulumi entry point
AwsStack.cs — defines:

DynamoDB Tables (all PAY_PER_REQUEST, all with Streams):
  1. snapp-users
     - PK: "PK" (S), SK: "SK" (S)
     - GSI-Email: GSI1PK/GSI1SK, project UserId
     - GSI-Specialty: GSI2PK/GSI2SK, project UserId, DisplayName
     - TTL: ExpiresAt
     - Stream: NEW_AND_OLD_IMAGES
  2. snapp-networks — PK/SK + GSI-UserNetworks, GSI-PendingApps
  3. snapp-content — PK/SK + GSI-UserPosts
  4. snapp-intel — PK/SK + GSI-BenchmarkLookup
  5. snapp-tx — PK/SK + GSI-UserReferrals, GSI-OpenReferrals
  6. snapp-notif — PK/SK + GSI-UndigestedNotifs, GSI-DigestQueue
  (All tables: PAY_PER_REQUEST, TTL on ExpiresAt, Streams)

S3: snapp-media (CORS for Blazor), snapp-client (static hosting)
CloudFront: distribution → snapp-client, /api/* → API Gateway
API Gateway (HTTP API): routes per service, Lambda integrations
KMS: "snapp-pii-key" symmetric key for PII encryption
SES: domain verification, configuration set
IAM: one role per Lambda, scoped to its table(s) + KMS/SES as needed
CloudWatch: log groups (30-day retention), alarms (error rate, latency)

Pulumi Config: domain, environment, sesFromAddress

Test criteria:
- pulumi preview succeeds
- pulumi up creates all resources
- DynamoDB tables accessible, API GW returns 401 unauthenticated
- KMS key usable, S3 CORS configured
```

---

### WU-0.3: Shared Library

**Input:** This TRD (data model section)
**Output:** `Snapp.Shared` class library
**Specification:**

```
Create Snapp.Shared with:

Models/:
  User.cs:
    UserId (string), DisplayName (string), Specialty (string),
    Geography (string), ProfileCompleteness (decimal 0-100),
    CreatedAt (DateTime), UpdatedAt (DateTime)

  UserPii.cs:
    UserId (string), EncryptedEmail (string),
    EncryptedPhone (string?), EncryptedContactInfo (string?),
    EncryptionKeyId (string)

  Network.cs:
    NetworkId (string), Name (string), Description (string),
    Charter (string), CreatedByUserId (string),
    MemberCount (int), CreatedAt (DateTime)

  NetworkMembership.cs:
    NetworkId (string), UserId (string), Role (string),
    Status (MembershipStatus enum: Active/Suspended/Emeritus),
    JoinedAt (DateTime), ContributionScore (decimal)

  NetworkRole.cs:
    RoleName (string), Permissions (Permission flags enum)

  Post.cs:
    PostId (string), NetworkId (string), AuthorUserId (string),
    Content (string), PostType (PostType enum: Text/Milestone/Poll),
    ReactionCounts (Dictionary<string,int>), CreatedAt (DateTime)

  Thread.cs:
    ThreadId (string), NetworkId (string), Title (string),
    AuthorUserId (string), ReplyCount (int),
    LastReplyAt (DateTime?), CreatedAt (DateTime)

  Reply.cs:
    ReplyId (string), ThreadId (string), AuthorUserId (string),
    Content (string), CreatedAt (DateTime)

  Referral.cs:
    ReferralId (string), SenderUserId (string),
    ReceiverUserId (string), NetworkId (string),
    Specialty (string), Status (ReferralStatus enum:
    Created/Accepted/Completed/Expired), Notes (string),
    CreatedAt (DateTime), OutcomeRecordedAt (DateTime?)

  Reputation.cs:
    UserId (string), OverallScore (decimal),
    ReferralScore (decimal), ContributionScore (decimal),
    AttestationScore (decimal), ComputedAt (DateTime)

  PracticeData.cs:
    UserId (string), Category (string),
    DataPoints (Dictionary<string,string>),
    ConfidenceContribution (decimal), SubmittedAt (DateTime)

  Valuation.cs:
    UserId (string), Downside (decimal), Base (decimal),
    Upside (decimal), ConfidenceScore (decimal),
    Drivers (Dictionary<string,string>),
    ComputedAt (DateTime)

  Benchmark.cs:
    Specialty (string), Geography (string), SizeBand (string),
    MetricName (string), P25 (decimal), P50 (decimal),
    P75 (decimal), SampleSize (int), ComputedAt (DateTime)

  Notification.cs:
    NotificationId (string), UserId (string),
    Type (NotificationType enum), Category (string),
    Title (string), Body (string), SourceEntityId (string?),
    IsRead (bool), IsDigested (bool), CreatedAt (DateTime)

  NotificationPreferences.cs:
    UserId (string), DigestTime (string, default "07:00"),
    Timezone (string, default "America/New_York"),
    ImmediateTypes (List<NotificationType>)

  DealRoom.cs:
    DealId (string), Name (string),
    CreatedByUserId (string), Status (DealStatus enum:
    Active/Closed/Archived), CreatedAt (DateTime)

  DealDocument.cs:
    DocumentId (string), DealId (string), Filename (string),
    S3Key (string), UploadedByUserId (string),
    Size (long), CreatedAt (DateTime)

DTOs/:
  Auth/:
    MagicLinkRequest.cs: Email [Required, EmailAddress]
    MagicLinkValidateRequest.cs: Code [Required, MinLength(32)]
    TokenResponse.cs: AccessToken, RefreshToken, ExpiresIn (int)
    RefreshRequest.cs: RefreshToken [Required]

  User/:
    CreateProfileRequest.cs: DisplayName [Required, MaxLength(100)],
      Specialty, Geography
    UpdateProfileRequest.cs: DisplayName, Specialty, Geography
      (all optional)
    ProfileResponse.cs: UserId, DisplayName, Specialty, Geography,
      ProfileCompleteness, CreatedAt
    OnboardingRequest.cs: DisplayName [Required], Specialty,
      Geography, Email [Required, EmailAddress],
      Phone (optional), LinkedInProfileUrl (optional)

  Network/:
    CreateNetworkRequest.cs: Name [Required, MaxLength(100)],
      Description [MaxLength(2000)], Charter, Template (optional)
    UpdateNetworkRequest.cs: Name, Description, Charter (optional)
    NetworkResponse.cs: NetworkId, Name, Description, Charter,
      MemberCount, CreatedAt, UserRole (if member)
    NetworkListResponse.cs: Networks (List<NetworkResponse>),
      NextToken (string?)
    ApplyRequest.cs: NetworkId [Required],
      ApplicationText [MaxLength(1000)]
    ApplicationDecisionRequest.cs: UserId [Required],
      Decision (Approved/Denied), Reason (optional)
    MemberResponse.cs: UserId, DisplayName, Role, JoinedAt,
      ContributionScore
    MemberListResponse.cs: Members (List<MemberResponse>),
      NextToken (string?)

  Content/:
    CreatePostRequest.cs: NetworkId [Required],
      Content [Required, MaxLength(5000)],
      PostType (default: Text)
    PostResponse.cs: PostId, NetworkId, AuthorUserId,
      AuthorDisplayName, Content, PostType, ReactionCounts,
      CreatedAt
    FeedResponse.cs: Posts (List<PostResponse>),
      NextToken (string?)
    CreateThreadRequest.cs: NetworkId [Required],
      Title [Required, MaxLength(200)],
      Content [Required, MaxLength(10000)]
    CreateReplyRequest.cs: ThreadId [Required],
      Content [Required, MaxLength(5000)]
    ThreadResponse.cs: ThreadId, NetworkId, Title,
      AuthorUserId, AuthorDisplayName, ReplyCount,
      LastReplyAt, CreatedAt
    ThreadListResponse.cs: Threads (List<ThreadResponse>),
      NextToken (string?)
    ReplyResponse.cs: ReplyId, ThreadId, AuthorUserId,
      AuthorDisplayName, Content, CreatedAt
    ReplyListResponse.cs: Replies (List<ReplyResponse>),
      NextToken (string?)

  Intelligence/:
    SubmitDataRequest.cs: Category [Required],
      DataPoints (Dictionary<string,string>) [Required]
    DashboardResponse.cs: KPIs (List<KpiItem>),
      ConfidenceScore (decimal), ValuationSummary (optional)
    KpiItem.cs: Name, Value, Unit, Trend (Up/Down/Flat),
      Percentile (decimal?)
    BenchmarkRequest.cs: Specialty [Required],
      Geography [Required], SizeBand [Required]
    BenchmarkResponse.cs: Metrics (List<BenchmarkMetric>),
      CohortSize (int)
    BenchmarkMetric.cs: Name, P25, P50, P75, UserValue (decimal?),
      UserPercentile (decimal?)
    ValuationResponse.cs: Downside, Base, Upside,
      ConfidenceScore, Drivers (List<ValuationDriver>),
      History (List<ValuationSnapshot>)
    ValuationDriver.cs: Name, Impact (string), Direction
    ValuationSnapshot.cs: Date, Base, ConfidenceScore

  Transaction/:
    CreateReferralRequest.cs: ReceiverUserId [Required],
      NetworkId [Required], Specialty, Notes [MaxLength(1000)]
    UpdateReferralStatusRequest.cs: Status [Required]
    RecordOutcomeRequest.cs: Outcome [Required, MaxLength(2000)],
      Success (bool)
    ReferralResponse.cs: ReferralId, SenderUserId, ReceiverUserId,
      SenderDisplayName, ReceiverDisplayName, NetworkId,
      Specialty, Status, Notes, CreatedAt, OutcomeRecordedAt
    ReferralListResponse.cs: Referrals (List<ReferralResponse>),
      NextToken (string?)
    ReputationResponse.cs: UserId, OverallScore, ReferralScore,
      ContributionScore, AttestationScore, ComputedAt

  Notification/:
    NotificationResponse.cs: NotificationId, Type, Category,
      Title, Body, IsRead, CreatedAt
    NotificationListResponse.cs:
      Notifications (List<NotificationResponse>),
      UnreadCount (int), NextToken (string?)
    UpdatePreferencesRequest.cs: DigestTime, Timezone,
      ImmediateTypes (List<NotificationType>)
    DigestPreviewResponse.cs: Categories (Dictionary<string,int>),
      TopItems (List<NotificationResponse>)

Interfaces/:
  IFieldEncryptor.cs:
    Task<string> EncryptAsync(string plaintext)
    Task<string> DecryptAsync(string ciphertext)
    Task<(string encrypted, string keyId)>
      EncryptWithKeyIdAsync(string plaintext)

  IUserRepository.cs:
    Task<User?> GetByIdAsync(string userId)
    Task<UserPii?> GetPiiAsync(string userId)
    Task<string?> GetUserIdByEmailHashAsync(string emailHash)
    Task CreateAsync(User user, UserPii pii, string emailHash)
    Task UpdateAsync(User user)
    Task UpdatePiiAsync(UserPii pii)
    Task<List<User>> SearchBySpecialtyGeoAsync(
      string specialty, string geo, string? nextToken)

  INetworkRepository.cs:
    Task<Network?> GetByIdAsync(string networkId)
    Task CreateAsync(Network network)
    Task UpdateAsync(Network network)
    Task<List<Network>> ListAsync(string? nextToken)
    Task<NetworkMembership?> GetMembershipAsync(
      string networkId, string userId)
    Task AddMemberAsync(NetworkMembership membership)
    Task UpdateMemberAsync(NetworkMembership membership)
    Task<List<NetworkMembership>> ListMembersAsync(
      string networkId, string? nextToken)
    Task<List<Network>> ListUserNetworksAsync(
      string userId, string? nextToken)

  IContentRepository.cs:
    Task CreatePostAsync(Post post)
    Task<List<Post>> ListNetworkFeedAsync(
      string networkId, string? nextToken, int limit = 25)
    Task<List<Post>> ListUserPostsAsync(
      string userId, string? nextToken, int limit = 25)
    Task CreateThreadAsync(Thread thread)
    Task<List<Thread>> ListThreadsAsync(
      string networkId, string? nextToken, int limit = 25)
    Task CreateReplyAsync(Reply reply)
    Task<List<Reply>> ListRepliesAsync(
      string threadId, string? nextToken, int limit = 50)

  IIntelligenceRepository.cs:
    Task SubmitDataAsync(PracticeData data)
    Task<List<PracticeData>> GetUserDataAsync(string userId)
    Task<Valuation?> GetCurrentValuationAsync(string userId)
    Task SaveValuationAsync(Valuation valuation)
    Task<List<Benchmark>> GetBenchmarksAsync(
      string specialty, string geography, string sizeBand)
    Task SaveBenchmarkAsync(Benchmark benchmark)

  ITransactionRepository.cs:
    Task CreateReferralAsync(Referral referral)
    Task UpdateReferralAsync(Referral referral)
    Task<Referral?> GetReferralAsync(string referralId)
    Task<List<Referral>> ListSentReferralsAsync(
      string userId, string? nextToken)
    Task<List<Referral>> ListReceivedReferralsAsync(
      string userId, string? nextToken)
    Task<Reputation?> GetReputationAsync(string userId)
    Task SaveReputationAsync(Reputation reputation)

  INotificationRepository.cs:
    Task CreateNotificationAsync(Notification notification)
    Task<List<Notification>> ListUserNotificationsAsync(
      string userId, string? nextToken, int limit = 25)
    Task MarkReadAsync(string userId, string notificationId)
    Task<List<Notification>> GetUndigestedAsync(string userId)
    Task MarkDigestedAsync(string userId,
      List<string> notificationIds)
    Task<NotificationPreferences?> GetPreferencesAsync(
      string userId)
    Task SavePreferencesAsync(NotificationPreferences prefs)
    Task<List<string>> GetUsersForDigestHourAsync(
      string digestHour)

Constants/:
  TableNames.cs:
    Users = "snapp-users"
    Networks = "snapp-networks"
    Content = "snapp-content"
    Intelligence = "snapp-intel"
    Transactions = "snapp-tx"
    Notifications = "snapp-notif"

  KeyPrefixes.cs:
    User = "USER#", Email = "EMAIL#", Token = "TOKEN#",
    Refresh = "REFRESH#", Rate = "RATE#",
    Network = "NET#", UserMembership = "UMEM#",
    AppStatus = "APPSTATUS#",
    Feed = "FEED#", UserPost = "UPOST#",
    Discussion = "DISC#", Thread = "THREAD#",
    Reaction = "REACT#",
    PracticeData = "PDATA#", Valuation = "VAL#",
    Benchmark = "BENCH#", Signal = "SIGNAL#",
    Referral = "REF#", UserReferral = "UREF#",
    RefStatus = "REFSTATUS#", Reputation = "REP#",
    Attestation = "ATTEST#",
    Deal = "DEAL#",
    Notification = "NOTIF#", Digest = "DIGEST#",
    DigestQueue = "DQUEUE#"

  Limits.cs:
    MaxPostLength = 5000
    MaxThreadTitleLength = 200
    MaxReplyLength = 5000
    MaxNetworkNameLength = 100
    MaxApplicationTextLength = 1000
    MaxFeedPageSize = 25
    MaxMemberPageSize = 50
    MagicLinkTtlMinutes = 15
    RefreshTokenTtlDays = 30
    AccessTokenTtlMinutes = 15
    MagicLinkRateLimitPerWindow = 3
    RateLimitWindowMinutes = 15

Auth/:
  ClaimTypes.cs:
    UserId = "sub", Email = "email", Roles = "roles"
  Permission.cs: (flags enum)
    ViewMembers = 1, ManageMembers = 2, CreatePost = 4,
    ModerateContent = 8, ManageNetwork = 16,
    ManageRoles = 32, ReviewApplications = 64,
    ViewIntelligence = 128, ManageReferrals = 256,
    ManageDealRooms = 512, Admin = int.MaxValue

Hosting/:
  ServiceCollectionExtensions.cs:
    AddSnappDynamo(config) — registers DynamoDB client
      with ServiceURL from config (local) or default (AWS)
    AddSnappEncryption(config) — registers IFieldEncryptor
      (KMS or LocalFile based on config)
    AddSnappAuth(config) — registers JWT validation

Enums/:
  MembershipStatus.cs: Active, Suspended, Emeritus
  PostType.cs: Text, Milestone, Poll
  ReferralStatus.cs: Created, Accepted, Completed, Expired
  DealStatus.cs: Active, Closed, Archived
  NotificationType.cs: ReferralReceived, ReferralOutcome,
    MentionInDiscussion, ApplicationReceived,
    ApplicationDecision, MilestoneAchieved,
    ValuationChanged, NewNetworkMember, DigestSummary

Test criteria:
- Project builds with zero warnings
- All DTOs have validation attributes
- All models serialize/deserialize to JSON
- Enum flags work correctly (Permission)
- Constants match TRD table/key definitions
- Validation rejects invalid inputs (empty required fields,
  exceeding max lengths)
```

---

### WU-0.4: Blazor Shell

**Input:** Snapp.Shared library
**Output:** Blazor WASM app with routing, layout, auth state, HttpClient config
**Specification:**

```
Create Snapp.Client (Blazor WASM standalone, .NET 9):

Program.cs:
- Register HttpClient with base URL from config
  (http://localhost:8000/api for dev, prod API GW URL for prod)
- Register DelegatingHandler that attaches Bearer token
- Register AuthenticationStateProvider (custom, see below)
- Register typed HttpClient services:
  IAuthService → AuthService (calls /api/auth/*)
  IUserService → UserService (calls /api/users/*)
  INetworkService → NetworkService (calls /api/networks/*)
  IContentService → ContentService (calls /api/content/*)
  IIntelligenceService → IntelligenceService (/api/intel/*)
  ITransactionService → TransactionService (/api/tx/*)
  INotificationService → NotificationService (/api/notif/*)
  ILinkedInService → LinkedInService (/api/linkedin/*)
- Register Polly retry policy (1 retry on 5xx, 2s delay)

Layout/MainLayout.razor:
- Top nav bar:
  - Left: SNAPP logo + "PraxisIQ" text
  - Center: search input (placeholder, not functional yet)
  - Right: notification bell with unread count badge,
    user avatar + dropdown (Profile, Settings, Logout)
- Left sidebar (visible when authenticated):
  - Network selector dropdown (user's networks)
  - Navigation links (context-sensitive to selected network):
    - Feed, Discussions, Members, Intelligence,
      Referrals, Settings (if steward)
  - Bottom: "Create Network" button
- Main content area with @Body
- Responsive:
  - ≥1024px: sidebar visible, content beside it
  - <1024px: sidebar in hamburger menu overlay

Pages/Auth/Login.razor:
- Email input field, "Send Magic Link" button
- On submit: call IAuthService.RequestMagicLink(email)
- Show confirmation: "Check your email for a login link"
- No indication of whether email exists

Pages/Auth/Callback.razor:
- Route: /login/callback?code={code}
- On load: call IAuthService.ValidateCode(code)
- On success: store tokens, redirect to /
- On failure: show "Link expired or invalid, try again"
  with link to /login

State/SnappAuthStateProvider.cs:
- Extends AuthenticationStateProvider
- Stores JWT + refresh token in localStorage
  (via IJSRuntime interop — one of the few JS interops)
- Parses JWT claims for AuthenticationState
- Exposes: CurrentUserId, IsAuthenticated
- On 401 from any HttpClient call: attempt token refresh
  via IAuthService.Refresh(refreshToken)
- If refresh fails: clear tokens, redirect to /login

State/NetworkState.cs:
- Tracks currently selected network (from sidebar dropdown)
- Persisted in localStorage
- Cascading parameter for all network-scoped pages
- Exposes: CurrentNetworkId, CurrentNetworkName, UserRole

Services/ (typed HttpClient wrappers):
- Each implements the interface from Snapp.Shared
- Each wraps HttpClient calls with proper error handling
- Returns deserialized DTOs
- Throws typed exceptions: UnauthorizedException,
  NotFoundException, ValidationException, ServerException

Components/Layout/ProtectedRoute.razor:
- Wraps AuthorizeView
- If not authenticated → NavigationManager.NavigateTo("/login")

Routing (App.razor):
  / → Pages/Home.razor (landing if unauth, dashboard if auth)
  /login → Pages/Auth/Login.razor
  /login/callback → Pages/Auth/Callback.razor
  /profile → Pages/Profile/MyProfile.razor [Authorize]
  /profile/{userId} → Pages/Profile/ViewProfile.razor
  /networks → Pages/Network/Directory.razor [Authorize]
  /networks/create → Pages/Network/Create.razor [Authorize]
  /networks/{netId} → Pages/Network/Home.razor [Authorize]
  /networks/{netId}/feed → Pages/Feed/NetworkFeed.razor [Auth]
  /networks/{netId}/discuss → Pages/Discussion/List.razor [Auth]
  /networks/{netId}/discuss/{threadId} →
    Pages/Discussion/View.razor [Auth]
  /networks/{netId}/members → Pages/Network/Members.razor [Auth]
  /networks/{netId}/settings →
    Pages/Network/Settings.razor [Auth]
  /intelligence → Pages/Intelligence/Dashboard.razor [Auth]
  /intelligence/benchmark →
    Pages/Intelligence/Benchmark.razor [Auth]
  /intelligence/valuation →
    Pages/Intelligence/Valuation.razor [Auth]
  /referrals → Pages/Referrals/List.razor [Auth]
  /deals → Pages/DealRoom/List.razor [Auth]
  /deals/{dealId} → Pages/DealRoom/View.razor [Auth]

  All [Authorize] pages: placeholder content with page title.
  Actual content built in later WUs.

Pages/Home.razor:
  Unauthenticated: hero section with "PraxisIQ — Where
  practitioners build real guilds" + CTA to login
  Authenticated: redirect to /networks/{firstNetworkId}/feed
  or /networks if no networks joined

wwwroot/:
  - index.html (Blazor WASM host)
  - css/app.css (minimal reset + layout styles)
  - favicon.ico

appsettings.json:
  ApiBaseUrl: "http://localhost:8000/api"

appsettings.Production.json:
  ApiBaseUrl: "https://api.{domain}"

Test criteria (bUnit + manual):
- App loads and renders landing page when unauthenticated
- /login page renders email input
- Protected routes redirect to /login when unauthenticated
- After simulated auth, sidebar renders with network selector
- Layout is responsive at 375px, 768px, 1440px
- HttpClient attaches Bearer token to requests
- 401 response triggers token refresh attempt
- Network state persists across page navigation
```

---

### WU-0.5: OpenAPI & SDK Pipeline

**Input:** Snapp.Shared library
**Output:** OpenAPI generation in each service, spec merge tool, Kiota C# SDK, Swagger UI, CI validation
**Specification:**

```
This work unit establishes the API documentation and SDK
generation infrastructure. It is applied incrementally —
the pipeline is built once, then each service WU adds
OpenAPI metadata to its endpoints as part of its own build.

1. Per-Service OpenAPI Setup (template for all services)

   Each service's Program.cs includes:
     builder.Services.AddOpenApi();
     app.MapOpenApi();  // GET /openapi/v1.json

   Each endpoint includes Minimal API metadata:
     app.MapPost("/api/auth/magic-link", HandleMagicLink)
       .WithName("RequestMagicLink")
       .WithDescription("Send a magic link login email")
       .WithTags("Authentication")
       .Accepts<MagicLinkRequest>("application/json")
       .Produces<MessageResponse>(200)
       .Produces<ErrorResponse>(429)
       .WithOpenApi();

   Naming convention for operation IDs:
     {Verb}{Resource} — e.g., RequestMagicLink,
     ValidateMagicLink, GetProfile, UpdateProfile,
     CreateNetwork, ListNetworkMembers, CreatePost,
     GetNetworkFeed, SubmitPracticeData, ComputeValuation,
     CreateReferral, RecordReferralOutcome

   Every service's /openapi/v1.json is accessible in dev
   via Kong: http://localhost:8000/api/{service}/openapi/v1.json

2. Spec Merge Tool (Snapp.Tools.SpecMerge)

   Console app in src/Snapp.Tools.SpecMerge/:
   - Reads per-service OpenAPI specs (from files or URLs)
   - Merges into single snapp-api.yaml
   - Deduplicates shared schemas (references Snapp.Shared types)
   - Adds unified security scheme:
       securitySchemes:
         bearer:
           type: http
           scheme: bearer
           bearerFormat: JWT
   - Adds server URLs:
       servers:
         - url: http://localhost:8000/api
           description: Local development (Kong)
         - url: https://api.{domain}
           description: Production
   - Outputs: snapp-api.yaml + snapp-api.json

   Run: dotnet run --project src/Snapp.Tools.SpecMerge

   The tool can also fetch specs from running containers:
     dotnet run --project src/Snapp.Tools.SpecMerge -- --live
     (hits each service's /openapi/v1.json via Kong)

3. Kiota C# SDK Generation (Snapp.Sdk)

   Generated via Kiota CLI:
     kiota generate \
       --language CSharp \
       --openapi snapp-api.yaml \
       --output src/Snapp.Sdk \
       --class-name SnappApiClient \
       --namespace-name Snapp.Sdk \
       --exclude-backward-compatible \
       --serializer Microsoft.Kiota.Serialization.Json \
       --deserializer Microsoft.Kiota.Serialization.Json

   Post-generation:
   - Snapp.Sdk.csproj references Snapp.Shared
     (Kiota models that match Shared DTOs are replaced
      with direct references via partial classes or
      Kiota's --structured-mime-types option)
   - Extension method AddSnappSdk() registered in DI:
     builder.Services.AddSnappSdk(options => {
       options.BaseUrl = config["ApiBaseUrl"];
       options.AuthProvider = sp.GetRequired<IAuthProvider>();
     });

   SDK usage pattern:
     @inject SnappApiClient Api

     // Typed, discoverable, auto-complete-friendly:
     var feed = await Api.Content
       .Networks[netId].Feed.GetAsync();
     var profile = await Api.Users.Me.GetAsync();
     await Api.Auth.MagicLink.PostAsync(
       new MagicLinkRequest { Email = email });

4. Blazor Client Integration

   Update Snapp.Client to use Snapp.Sdk instead of
   hand-written typed HttpClient services:
   - Remove manual IAuthService, IUserService, etc.
   - Replace with SnappApiClient injected via DI
   - Auth token attached via Kiota's
     BaseBearerTokenAuthenticationProvider

   The Blazor service layer becomes a thin wrapper
   over Snapp.Sdk for any UI-specific concerns
   (caching, optimistic updates, error mapping).

5. Swagger UI Container (Local Dev)

   Add to docker-compose.yml:
     swagger-ui:
       image: swaggerapi/swagger-ui:latest
       ports: ["8090:8080"]
       environment:
         SWAGGER_JSON_URL: /api/docs/snapp-api.json
       depends_on: [kong]

   Kong route to serve the merged spec:
     Static file served from a shared volume or
     a tiny file-server container.

   Accessible at: http://localhost:8090

6. CI Pipeline Integration

   In .github/workflows/ci.yml, add steps:
   a. Start services (docker-compose up)
   b. Fetch per-service OpenAPI specs
   c. Run spec merge tool
   d. Validate merged spec (spectral lint)
   e. Check for breaking changes vs. main branch
      (using oasdiff: oasdiff breaking old.yaml new.yaml)
   f. Generate C# SDK via Kiota
   g. Build Snapp.Sdk
   h. Run SDK integration tests
   i. If on main branch: publish Snapp.Sdk as NuGet package
      (version bumped automatically)

   Breaking change detection:
   - Removed endpoint → FAIL
   - Removed required response field → FAIL
   - Added required request field → FAIL
   - Changed response type → FAIL
   - New optional field → OK
   - New endpoint → OK

7. Multi-Language SDK Generation (future, on demand)

   Same spec, different Kiota target:
     kiota generate --language TypeScript \
       --openapi snapp-api.yaml \
       --output sdk/typescript/snapp-sdk \
       --class-name SnappApiClient

     kiota generate --language Python \
       --openapi snapp-api.yaml \
       --output sdk/python/snapp_sdk \
       --class-name SnappApiClient

     kiota generate --language Java \
       --openapi snapp-api.yaml \
       --output sdk/java/snapp-sdk \
       --class-name SnappApiClient

   Each published to its respective package registry
   (npm, PyPI, Maven) when needed.

Test criteria:
- Each service serves valid /openapi/v1.json
- Spec merge tool produces valid OpenAPI 3.1 document
- All endpoints present in merged spec
- All Snapp.Shared DTOs appear as schemas
- Kiota generates Snapp.Sdk without errors
- Snapp.Sdk compiles and references Snapp.Shared
- SnappApiClient can be injected and makes correct HTTP calls
- Swagger UI loads and displays all endpoints
- oasdiff detects intentional breaking change (negative test)
- Spectral lint passes with zero errors
```

---

### WU-1.1: Field Encryption Library

**Input:** Snapp.Shared (IFieldEncryptor interface)
**Output:** Two implementations: KmsFieldEncryptor + LocalFileFieldEncryptor
**Specification:**

```
Create within Snapp.Shared/Encryption/:

LocalFileFieldEncryptor.cs:
- Reads 256-bit master key from local file path (config)
- EncryptAsync(plaintext):
  1. Generate random 256-bit data key
  2. Encrypt plaintext with AES-256-GCM using data key
  3. Encrypt data key with master key (AES-256-GCM)
  4. Return Base64(encryptedDataKey + nonce + ciphertext + tag)
- DecryptAsync(ciphertext):
  1. Decode Base64
  2. Extract encryptedDataKey, nonce, ciphertext, tag
  3. Decrypt data key with master key
  4. Decrypt ciphertext with data key
  5. Return plaintext
- EncryptWithKeyIdAsync: returns (encrypted, "local")

KmsFieldEncryptor.cs:
- Uses AWS KMS client
- EncryptAsync(plaintext):
  1. Call KMS GenerateDataKey (AES-256)
  2. Encrypt plaintext with AES-256-GCM using plaintext data key
  3. Wipe plaintext data key from memory
  4. Return Base64(encryptedDataKey + nonce + ciphertext + tag)
- DecryptAsync(ciphertext):
  1. Decode Base64
  2. Extract encryptedDataKey
  3. Call KMS Decrypt to get plaintext data key
  4. Decrypt ciphertext
  5. Wipe plaintext data key from memory
  6. Return plaintext
- EncryptWithKeyIdAsync: returns (encrypted, kmsKeyArn)

ServiceCollectionExtensions (update AddSnappEncryption):
- If config Encryption:Provider == "LocalFile"
    → register LocalFileFieldEncryptor
- If config Encryption:Provider == "KMS"
    → register KmsFieldEncryptor with key ARN from config

Format of encrypted blob (binary, then Base64 encoded):
  [2 bytes: encrypted data key length]
  [N bytes: encrypted data key]
  [12 bytes: GCM nonce]
  [remaining - 16 bytes: ciphertext]
  [16 bytes: GCM auth tag]

Test criteria:
- Encrypt then decrypt round-trips correctly
- Different plaintexts produce different ciphertexts
- Tampering with ciphertext causes decryption failure
- Empty string encrypts/decrypts correctly
- Unicode text encrypts/decrypts correctly
- LocalFile and KMS implementations produce interchangeable
  format (can decrypt each other's output given same key)
- Memory: plaintext data key not retained after operation
```

---

### WU-1.2: Magic Link Auth Service

**Input:** Snapp.Shared, M1.1 encryption library, DynamoDB table
**Output:** Docker container + Lambda-ready service handling `/api/auth/*`
**Specification:**

```
Create Snapp.Service.Auth/

Program.cs:
- Registers: DynamoDB, IFieldEncryptor, SMTP/SES client, JWT
- MapAuthEndpoints()
- Dual-host pattern (#if LAMBDA)

Endpoints/AuthEndpoints.cs:

  POST /api/auth/magic-link
    Body: MagicLinkRequest { Email }
    Handler logic:
    1. Validate email format
    2. Rate limit check:
       - Hash email (SHA-256)
       - Query PK=RATE#{hash}#MAGIC, SK=WINDOW#{currentWindow}
       - If Count >= 3 → return 429
       - Else: conditional update Count + 1
         (ConditionExpression: attribute_not_exists OR Count < 3)
    3. Generate 64-char URL-safe code (RandomNumberGenerator)
    4. Hash email (SHA-256) for storage
    5. Store: PK=TOKEN#{code}, SK=MAGIC_LINK
       Attrs: HashedEmail, CreatedAt, ExpiresAt (now + 15 min)
    6. Build magic link URL: {baseUrl}/login/callback?code={code}
    7. Send email:
       - Subject: "Your PraxisIQ login link"
       - Body: link + "This link expires in 15 minutes"
       - From: configured SES/SMTP sender
    8. Return 200 { message: "If that email is registered,
       you'll receive a login link" }
       (same message whether email exists or not)

  POST /api/auth/validate
    Body: MagicLinkValidateRequest { Code }
    Handler logic:
    1. Query PK=TOKEN#{code}, SK=MAGIC_LINK
    2. If not found → 401 { error: "INVALID_OR_EXPIRED_LINK" }
    3. If ExpiresAt < now → delete token, 401
    4. Delete token (single-use)
    5. Extract HashedEmail
    6. Look up PK=EMAIL#{hashedEmail}, SK=USER in snapp-users
    7. If no user:
       a. Generate userId (ULID)
       b. Create PK=USER#{userId}, SK=PROFILE (minimal)
       c. Encrypt email using IFieldEncryptor
       d. Create PK=USER#{userId}, SK=PII
          (EncryptedEmail, EncryptionKeyId)
       e. Create PK=EMAIL#{hashedEmail}, SK=USER (UserId)
       f. Set isNewUser = true
    8. Generate JWT:
       - Claims: sub=userId, email_hash=hashedEmail
       - Signed RS256 (key from config/Secrets Manager)
       - Expiry: 15 minutes
       - Issuer + Audience from config
    9. Generate refresh token (64 chars, URL-safe random)
    10. Hash refresh token (SHA-256)
    11. Store: PK=REFRESH#{hash}, SK=SESSION in snapp-users
        Attrs: UserId, CreatedAt, ExpiresAt (now + 30 days)
    12. Return 200 TokenResponse {
        AccessToken, RefreshToken, ExpiresIn: 900,
        IsNewUser: bool }

  POST /api/auth/refresh
    Body: RefreshRequest { RefreshToken }
    Handler logic:
    1. Hash token (SHA-256)
    2. Query PK=REFRESH#{hash}, SK=SESSION
    3. If not found or expired → 401
    4. Delete old refresh token
    5. Look up user (PK=USER#{userId}, SK=PROFILE)
    6. If user not found → 401
    7. Generate new JWT + new refresh token (rotation)
    8. Store new refresh token
    9. Return 200 TokenResponse

  POST /api/auth/logout
    Body: RefreshRequest { RefreshToken }
    Handler logic:
    1. Hash token, delete PK=REFRESH#{hash}
    2. Return 200 { message: "Logged out" }
    (Idempotent — no error if token doesn't exist)

Email sending:
- IEmailSender interface
- SesEmailSender (prod): uses AWS SES v2
- SmtpEmailSender (dev): uses System.Net.Mail to Papercut
- Registered based on config Email:Provider

Dockerfile:
- Multi-stage build (sdk → aspnet runtime)
- Copies Snapp.Shared + Snapp.Service.Auth
- Exposes 8080

Docker Compose addition (uncomment snapp-auth):
  snapp-auth:
    build:
      context: ../../
      dockerfile: src/Snapp.Service.Auth/Dockerfile
    ports: ["8081:8080"]
    depends_on: [dynamodb-local, papercut]
    environment: (common env vars + auth-specific)
    volumes: ["./keys:/keys:ro"]

Test criteria (xUnit + DynamoDB Local):
- POST /api/auth/magic-link sends email to Papercut
- POST /api/auth/validate with valid code returns JWT
- JWT contains correct claims (sub, email_hash, exp, iss, aud)
- Expired code returns 401
- Used code returns 401 (single-use verified)
- Refresh token rotation: new tokens issued, old invalidated
- Logout deletes refresh token
- Rate limiting: 4th request in 15 min → 429
- New user auto-created on first login
- PII (email) is encrypted in DynamoDB — verify by reading
  raw item and confirming EncryptedEmail is not plaintext
- Consistent response regardless of email existence
```

---

### WU-1.3: Kong JWT Plugin Configuration

**Input:** JWT signing key from WU-0.2, Kong running
**Output:** Kong configured to validate JWTs on protected routes
**Specification:**

```
Update Snapp.Infrastructure/Kong/kong.yml:

Consumers:
  - username: snapp-auth-service
    jwt_secrets:
      - key: snapp-dev  (matches JWT "iss" claim)
        algorithm: RS256
        rsa_public_key: (contents of jwt-signing.pub)

Global Plugins:
  - name: cors
    config:
      origins: ["http://localhost:5000", "http://localhost:5001"]
      methods: [GET, POST, PUT, DELETE, PATCH, OPTIONS]
      headers: [Authorization, Content-Type, Accept]
      credentials: true
      max_age: 3600

  - name: request-size-limiting
    config:
      allowed_payload_size: 10  # MB

Route-level JWT plugin (applied to all routes EXCEPT):
  Unprotected routes (no JWT plugin):
    - POST /api/auth/magic-link
    - POST /api/auth/validate
    - POST /api/auth/refresh
    - GET /api/health

  Protected routes (JWT plugin enabled):
    - All /api/users/* routes
    - All /api/networks/* routes
    - All /api/content/* routes
    - All /api/intel/* routes
    - All /api/tx/* routes
    - All /api/notif/* routes
    - All /api/linkedin/* routes
    - POST /api/auth/logout

  JWT plugin config:
    key_claim_name: iss
    claims_to_verify: [exp]
    header_names: [Authorization]

Update Snapp.Infrastructure/Scripts/init-kong.sh:
  - POST consumer + JWT secret to Kong Admin API
  - Apply routes and plugins

Test criteria:
- Request to /api/users/me without token → 401
- Request to /api/users/me with valid JWT → proxied to service
- Request to /api/users/me with expired JWT → 401
- Request to /api/auth/magic-link without token → proxied (200/503)
- CORS preflight (OPTIONS) returns correct headers
```

---

### WU-1.4: API Gateway Authorizer (Production)

**Input:** JWT signing public key, Pulumi stack
**Output:** Lambda authorizer for AWS API Gateway
**Specification:**

```
Create Snapp.Service.Authorizer/

Program.cs:
- Lambda function (not Minimal API — raw Lambda handler)
- Input: APIGatewayHttpApiV2ProxyRequest (REQUEST type auth)

Handler logic:
1. Extract Authorization header → Bearer token
2. If missing → return Deny
3. Validate JWT:
   - Signature: RS256 with public key
     (cached in static field after first load from
      Secrets Manager)
   - Expiry: reject if expired
   - Issuer: must match configured issuer
   - Audience: must match configured audience
4. Extract claims: sub (userId), email_hash
5. Return Allow policy with context:
   { "userId": "...", "emailHash": "..." }
6. API Gateway caches result for min(token remaining TTL, 300s)

Exclude from authorization (configured in API Gateway, not here):
- POST /api/auth/magic-link
- POST /api/auth/validate
- POST /api/auth/refresh

Pulumi update: wire authorizer Lambda to API Gateway

Test criteria:
- Valid JWT → Allow with correct context
- Expired JWT → Deny
- Wrong signature → Deny
- Missing Authorization header → Deny
- Correct claims extracted and passed in context
```

---

### WU-1.5: Auth UI

**Input:** Blazor Shell (M0.4), Auth Service (M1.2)
**Output:** Functional login flow in Blazor
**Specification:**

```
Update Snapp.Client:

Pages/Auth/Login.razor:
- Clean centered card layout
- Email input field with validation
- "Send Login Link" button
- States:
  1. Default: email input + button
  2. Sending: button disabled, spinner
  3. Sent: "Check your email" message with
     "Didn't receive it? Send again" (respects rate limit)
  4. Error: error message + retry
- Calls AuthService.RequestMagicLinkAsync(email)

Pages/Auth/Callback.razor:
- Route: /login/callback
- Query param: code
- On init: call AuthService.ValidateCodeAsync(code)
- States:
  1. Validating: "Signing you in..." with spinner
  2. Success: store tokens, redirect
     - If IsNewUser → redirect to /profile (onboarding)
     - If existing user → redirect to / (dashboard)
  3. Failed: "This link has expired or already been used"
     + "Request a new link" button → /login

Services/AuthService.cs:
- Implements IAuthService
- RequestMagicLinkAsync(email): POST /api/auth/magic-link
- ValidateCodeAsync(code): POST /api/auth/validate
  → stores tokens in AuthState on success
- RefreshAsync(): POST /api/auth/refresh
  → updates stored tokens
- LogoutAsync(): POST /api/auth/logout
  → clears stored tokens, navigates to /

State/SnappAuthStateProvider.cs (update):
- On ValidateCodeAsync success:
  store AccessToken + RefreshToken in localStorage
- NotifyAuthenticationStateChanged()
- GetAuthenticationStateAsync(): parse JWT, build
  ClaimsPrincipal
- Token refresh: called by DelegatingHandler on 401

Handlers/AuthDelegatingHandler.cs:
- Inherits DelegatingHandler
- SendAsync: if request not to /api/auth/*,
  attach Authorization: Bearer {accessToken}
- If response is 401 and refresh token exists:
  attempt refresh, retry original request once
- If refresh fails: clear auth state, navigate to /login

Test criteria (bUnit):
- Login page renders email input
- Submitting email calls AuthService
- Callback page with valid code redirects
- Callback page with invalid code shows error
- Logout clears auth state
- DelegatingHandler attaches token
- DelegatingHandler handles 401 → refresh → retry
```

---

### WU-1.6: User Profile Service

**Input:** Snapp.Shared, encryption library, auth service
**Output:** Docker container + Lambda handling `/api/users/*`
**Specification:**

```
Create Snapp.Service.User/

Endpoints/UserEndpoints.cs:

  GET /api/users/me
    Auth: required (userId from token claims)
    Handler:
    1. Get PK=USER#{userId}, SK=PROFILE from snapp-users
    2. If not found → 404
    3. Return ProfileResponse

  GET /api/users/{userId}
    Auth: required
    Handler:
    1. Get PK=USER#{targetId}, SK=PROFILE
    2. Return ProfileResponse (public fields only)

  PUT /api/users/me
    Auth: required
    Body: UpdateProfileRequest
    Handler:
    1. Get existing profile
    2. Update fields (only non-null fields from request)
    3. Recalculate ProfileCompleteness:
       - DisplayName present: +20
       - Specialty present: +20
       - Geography present: +20
       - LinkedIn linked: +15
       - At least 1 practice data point: +15
       - Profile photo: +10
    4. Save to snapp-users
    5. Return updated ProfileResponse

  POST /api/users/me/onboard
    Auth: required
    Body: OnboardingRequest
    Handler:
    1. Update PK=USER#{userId}, SK=PROFILE with
       DisplayName, Specialty, Geography
    2. If Email provided:
       a. Encrypt email via IFieldEncryptor
       b. Update PK=USER#{userId}, SK=PII
    3. If Phone provided:
       a. Encrypt phone via IFieldEncryptor
       b. Update PII item
    4. Recalculate completeness
    5. Return ProfileResponse

  GET /api/users/search?specialty={s}&geo={g}
    Auth: required
    Handler:
    1. Query GSI-Specialty where
       GSI2PK=SPECIALTY#{s}, GSI2SK begins_with GEO#{g}
    2. Return list of ProfileResponse (public fields)

  GET /api/users/me/pii
    Auth: required (self only)
    Handler:
    1. Get PK=USER#{userId}, SK=PII
    2. Decrypt email, phone via IFieldEncryptor
    3. Return { email, phone, contactInfo }
    (Used for settings page — user can see their own PII)

Dockerfile: same pattern as Auth service

Docker Compose: add snapp-user container

Test criteria:
- GET /me returns profile for authenticated user
- PUT /me updates profile fields
- Completeness score calculated correctly
- Onboarding stores encrypted PII
- Raw DynamoDB read of PII item shows encrypted (not plaintext)
- GET /me/pii decrypts and returns correct email
- Search by specialty/geo returns matching users
- GET /{otherId} returns only public fields
- Unauthenticated requests → 401 (via Kong)
```

---

### WU-1.7: Profile UI

**Input:** Blazor Shell, Auth UI, User Profile Service
**Output:** Profile pages and onboarding wizard
**Specification:**

```
Update Snapp.Client:

Pages/Profile/Onboarding.razor:
- Route: /onboarding
- Multi-step wizard (3 steps):
  Step 1: "About You"
    - DisplayName [required]
    - Specialty dropdown (predefined list from Shared constants)
    - Geography (state/region selector)
  Step 2: "Your Practice" (optional, skip-able)
    - Practice name
    - Practice size (solo / 2-5 / 6-10 / 10+)
    - Years in practice
  Step 3: "Connect" (optional, skip-able)
    - "Link your LinkedIn" button (placeholder, wired in M6)
    - Phone number (optional)
- Progress indicator (step 1/2/3)
- "Skip" on optional steps
- On complete: POST /api/users/me/onboard → redirect to /

Pages/Profile/MyProfile.razor:
- Route: /profile
- Displays: avatar placeholder, DisplayName, Specialty,
  Geography, ProfileCompleteness bar
- "Edit Profile" button → opens edit mode inline
- Completeness prompt: "Your profile is {X}% complete.
  {suggestion to improve}"
- Shows linked accounts (LinkedIn if linked)

Pages/Profile/ViewProfile.razor:
- Route: /profile/{userId}
- Displays: public fields only
- Reputation badge (placeholder, wired in M5.4)
- Network memberships in common
- "Refer" button (placeholder, wired in M5.3)

Pages/Profile/EditProfile.razor:
- Route: /profile/edit (or inline in MyProfile)
- Form: DisplayName, Specialty, Geography
- Save → PUT /api/users/me
- Cancel → return to view

Components/Cards/ProfileCard.razor:
- Compact card: avatar, name, specialty, geography,
  reputation badge
- Reusable in member lists, search results, etc.

Components/ProfileCompleteness.razor:
- Progress bar (0-100%)
- Color: red <40, yellow <70, green ≥70
- Tooltip: "Complete your profile to unlock better
  intelligence"

Test criteria (bUnit):
- Onboarding wizard advances through steps
- Required fields prevent advancement
- Optional steps can be skipped
- Profile page displays user data
- Edit form saves and reflects changes
- ProfileCard component renders correctly
- Completeness bar reflects score
```

---

### WU-2.1: Network Service

**Input:** Snapp.Shared, Auth, snapp-networks table
**Output:** Docker container + Lambda handling `/api/networks/*`
**Specification:**

```
Create Snapp.Service.Network/

Endpoints/NetworkEndpoints.cs:

  POST /api/networks
    Auth: required
    Body: CreateNetworkRequest
    Handler:
    1. Generate networkId (ULID)
    2. Create PK=NET#{netId}, SK=META in snapp-networks
    3. Create default roles:
       - PK=NET#{netId}, SK=ROLE#steward (all permissions)
       - PK=NET#{netId}, SK=ROLE#member (view + post)
       - PK=NET#{netId}, SK=ROLE#associate (view only)
    4. Add creator as steward:
       PK=NET#{netId}, SK=MEMBER#{userId}, Role=steward
    5. Add inverse: PK=UMEM#{userId}, SK=NET#{netId}
    6. Return NetworkResponse

  GET /api/networks
    Auth: required
    Handler:
    1. Scan snapp-networks for all META items (paginated)
    2. For each, check if user is member
    3. Return NetworkListResponse

  GET /api/networks/{netId}
    Auth: required
    Handler:
    1. Get PK=NET#{netId}, SK=META
    2. Get user's membership (if any)
    3. Return NetworkResponse with UserRole

  PUT /api/networks/{netId}
    Auth: required (steward only)
    Body: UpdateNetworkRequest
    Handler:
    1. Verify caller is steward of this network
    2. Update META item
    3. Return updated NetworkResponse

  GET /api/networks/{netId}/members
    Auth: required (must be member)
    Handler:
    1. Verify caller is member
    2. Query PK=NET#{netId}, SK begins_with MEMBER#
    3. For each member, get profile from snapp-users
       (batch get)
    4. Return MemberListResponse

  GET /api/networks/mine
    Auth: required
    Handler:
    1. Query PK=UMEM#{userId}, SK begins_with NET#
    2. Return list of NetworkResponse (denormalized names)

Endpoints/MembershipEndpoints.cs:

  POST /api/networks/{netId}/apply
    Auth: required
    Body: ApplyRequest
    Handler:
    1. Verify not already a member
    2. Create PK=NET#{netId},
       SK=APP#{timestamp}#{userId}
       Status=pending, ApplicationText
    3. Also write to GSI2:
       GSI2PK=APPSTATUS#{netId}#pending,
       GSI2SK=timestamp
    4. Queue notification for network stewards
       (write to snapp-notif: ApplicationReceived)
    5. Return 202 { message: "Application submitted" }

  GET /api/networks/{netId}/applications
    Auth: required (steward/reviewer only)
    Handler:
    1. Verify caller has ReviewApplications permission
    2. Query GSI-PendingApps:
       GSI2PK=APPSTATUS#{netId}#pending
    3. For each, get applicant profile
    4. Return list of applications

  POST /api/networks/{netId}/applications/{userId}/decide
    Auth: required (steward/reviewer only)
    Body: ApplicationDecisionRequest
    Handler:
    1. Verify caller has ReviewApplications permission
    2. Update application status (approved/denied)
    3. If approved:
       a. Create MEMBER# item (role: member)
       b. Create inverse UMEM# item
       c. Increment MemberCount on META
       d. Queue notification: ApplicationDecision (approved)
    4. If denied:
       a. Queue notification: ApplicationDecision (denied)
    5. Return 200

  POST /api/networks/{netId}/invite
    Auth: required (steward only)
    Body: { UserId }
    Handler:
    1. Verify caller is steward
    2. Create MEMBER# item directly (skip application)
    3. Create inverse UMEM# item
    4. Increment MemberCount
    5. Queue notification: NewNetworkMember
    6. Return 200

Dockerfile + Docker Compose entry

Test criteria:
- Create network → creator is steward
- Default roles created automatically
- Apply → application appears in pending queue
- Approve → user becomes member, count incremented
- Deny → user not added, notification sent
- Members list shows all active members
- /mine returns only user's networks
- Non-steward cannot approve/deny
- Non-member cannot see member list
- Invite bypasses application process
```

---

### WU-2.3: Network UI

**Input:** Blazor Shell, Auth UI, Network Service
**Output:** Network pages: directory, creation wizard, settings, member directory
**Specification:**

```
Update Snapp.Client:

Pages/Network/Directory.razor:
- Route: /networks
- Grid/list of available networks
- Each shows: name, description, member count, user's status
  (member / applied / not joined)
- "Create Network" CTA button
- Search/filter (by name, placeholder for specialty filter)

Pages/Network/Create.razor:
- Route: /networks/create
- Wizard:
  Step 1: Name + Description
  Step 2: Charter (rich text, what this network is about)
  Step 3: Template selection (General, Dental, Financial
    Advisory, MSP, Custom)
- On submit: POST /api/networks
- Redirect to /networks/{newNetId}

Pages/Network/Home.razor:
- Route: /networks/{netId}
- Dashboard for selected network:
  - Member count, recent activity summary
  - Quick links: Feed, Discussions, Members
  - If steward: pending applications count + link

Pages/Network/Members.razor:
- Route: /networks/{netId}/members
- Grid of ProfileCards for all members
- Role badges (steward, member, associate)
- Search by name
- Steward actions: change role, remove member

Pages/Network/Settings.razor:
- Route: /networks/{netId}/settings
- Only visible to stewards
- Tabs: General (name/description/charter),
  Roles (manage role definitions), Applications (review queue)

Pages/Network/Apply.razor:
- Modal or page for submitting application
- Application text field
- Submit → "Application submitted, you'll be notified"

Components/Cards/NetworkCard.razor:
- Name, description preview, member count, CTA

Test criteria (bUnit):
- Directory lists networks
- Create wizard produces network
- Home page shows network dashboard
- Members page lists members with roles
- Settings page only renders for stewards
- Apply submits application
```

---

### WU-3.1: Discussion Service

**Input:** Snapp.Shared, Auth, Network Service, snapp-content table
**Output:** Discussion endpoints in Content Service container
**Specification:**

```
Create Snapp.Service.Content/ (or add to existing if built)

Endpoints/DiscussionEndpoints.cs:

  POST /api/content/networks/{netId}/threads
    Auth: required (must be network member with CreatePost perm)
    Body: CreateThreadRequest
    Handler:
    1. Verify membership + permission
    2. Generate threadId (ULID)
    3. Create PK=DISC#{netId},
       SK=THREAD#{timestamp}#{threadId}
    4. Create initial reply as first post:
       PK=THREAD#{threadId},
       SK=REPLY#{timestamp}#{replyId}
    5. Return ThreadResponse

  GET /api/content/networks/{netId}/threads
    Auth: required (must be network member)
    Handler:
    1. Verify membership
    2. Query PK=DISC#{netId}, SK begins_with THREAD#,
       ScanIndexForward=false, Limit=25
    3. Return ThreadListResponse with NextToken

  GET /api/content/threads/{threadId}
    Auth: required
    Handler:
    1. Get thread metadata
    2. Verify user is member of thread's network
    3. Return ThreadResponse

  POST /api/content/threads/{threadId}/replies
    Auth: required (must be member of thread's network)
    Body: CreateReplyRequest
    Handler:
    1. Get thread, verify membership
    2. Generate replyId (ULID)
    3. Create PK=THREAD#{threadId},
       SK=REPLY#{timestamp}#{replyId}
    4. Update thread: ReplyCount++, LastReplyAt
    5. Queue notification: MentionInDiscussion
       (if @mentions detected in content)
    6. Return ReplyResponse

  GET /api/content/threads/{threadId}/replies
    Auth: required
    Handler:
    1. Verify membership
    2. Query PK=THREAD#{threadId}, SK begins_with REPLY#,
       ScanIndexForward=true, Limit=50
    3. Enrich with author DisplayName (batch get from users)
    4. Return ReplyListResponse

  DELETE /api/content/threads/{threadId}/replies/{replyId}
    Auth: required (author or moderator only)
    Handler:
    1. Verify ownership or ModerateContent permission
    2. Soft-delete (set Content = "[removed]")
    3. Return 200

Dockerfile + Docker Compose

Test criteria:
- Create thread → appears in thread list
- Reply to thread → reply appears, count incremented
- Thread list sorted newest first
- Replies sorted oldest first (chronological reading)
- Non-member cannot access threads
- Only author or moderator can delete
- @mention triggers notification creation in snapp-notif
- Pagination works with NextToken
```

---

### WU-3.2: Feed Service

**Input:** Snapp.Shared, Auth, Network Service, snapp-content table
**Output:** Feed endpoints in Content Service container
**Specification:**

```
Add to Snapp.Service.Content/

Endpoints/FeedEndpoints.cs:

  POST /api/content/networks/{netId}/posts
    Auth: required (member with CreatePost permission)
    Body: CreatePostRequest
    Handler:
    1. Verify membership + permission
    2. Generate postId (ULID), timestamp = now
    3. Create PK=FEED#{netId},
       SK=POST#{timestamp}#{postId}
    4. Create PK=UPOST#{userId},
       SK=POST#{timestamp}#{postId}
       (user's post history, NetworkId included)
    5. Return PostResponse

  GET /api/content/networks/{netId}/feed
    Auth: required (must be member)
    Query: ?nextToken={token}&limit={25}
    Handler:
    1. Verify membership
    2. Query PK=FEED#{netId}, SK begins_with POST#,
       ScanIndexForward=false, Limit=min(limit, 25)
    3. Enrich with author DisplayName (batch get)
    4. Return FeedResponse with NextToken

  GET /api/content/users/{userId}/posts
    Auth: required
    Handler:
    1. Query GSI-UserPosts:
       GSI1PK=UPOST#{userId}, begins_with POST#
    2. Return PostResponse list

  POST /api/content/posts/{postId}/react
    Auth: required
    Body: { ReactionType: "like"|"insightful"|"support" }
    Handler:
    1. Create/update PK=REACT#{postId}, SK=USER#{userId}
    2. Update post's ReactionCounts (atomic increment)
    3. Return updated counts

  DELETE /api/content/posts/{postId}/react
    Auth: required
    Handler:
    1. Delete PK=REACT#{postId}, SK=USER#{userId}
    2. Decrement post's ReactionCounts
    3. Return updated counts

Test criteria:
- Create post → appears in network feed
- Feed sorted newest first
- User's posts appear in their post history
- Reactions increment/decrement correctly
- One reaction per user per post (upsert, not duplicate)
- Non-member cannot read feed
- Pagination works
```

---

### WU-3.3: Discussion UI

**Input:** Blazor Shell, Network UI, Discussion Service
**Output:** Discussion pages
**Specification:**

```
Update Snapp.Client:

Pages/Discussion/List.razor:
- Route: /networks/{netId}/discuss
- List of threads: title, author, reply count, last activity
- "New Thread" button (opens Create form)
- Sorted by LastReplyAt (most recently active first)
- Infinite scroll or "Load More" with NextToken

Pages/Discussion/View.razor:
- Route: /networks/{netId}/discuss/{threadId}
- Thread title + original post at top
- Replies listed chronologically below
- Reply composer at bottom (auto-focus)
- Each reply: author ProfileCard (mini), content, timestamp
- Own replies: "Delete" option
- Moderator: "Delete" on any reply

Pages/Discussion/CreateThread.razor:
- Modal or inline form
- Title input + Content textarea
- Submit → creates thread, navigates to thread view

Components/Discussion/ReplyComposer.razor:
- Textarea with submit button
- @mention autocomplete (type @ → search network members)
- Shift+Enter for newline, Enter to submit

Components/Discussion/ThreadListItem.razor:
- Title, author name, reply count badge,
  "Last reply X ago" timestamp

Test criteria (bUnit):
- Thread list renders threads
- Creating thread redirects to view
- Replies render in order
- Reply composer submits and appends new reply
- Delete removes reply content
```

---

### WU-3.4: Feed UI

**Input:** Blazor Shell, Network UI, Feed Service
**Output:** Feed pages
**Specification:**

```
Update Snapp.Client:

Pages/Feed/NetworkFeed.razor:
- Route: /networks/{netId}/feed
- Post composer at top (text area + post type selector + submit)
- Feed items below, newest first
- Infinite scroll with NextToken
- Each post: PostCard component

Components/Feed/PostComposer.razor:
- Textarea (auto-grow)
- Post type selector: Text (default), Milestone
- Character count (max 5000)
- Submit button
- "Posting to: {NetworkName}" indicator

Components/Feed/PostCard.razor:
- Author: mini ProfileCard (avatar, name, specialty)
- Timestamp ("2 hours ago", "Yesterday", etc.)
- Content (markdown rendered — use Markdig library
  for .NET markdown → HTML, rendered via Blazor
  MarkupString)
- Reaction bar: like/insightful/support buttons with counts
- User's current reaction highlighted

Test criteria (bUnit):
- Feed loads and displays posts
- Post composer creates new post
- New post appears at top of feed
- Reactions toggle on click
- Markdown content renders as HTML
- Infinite scroll loads more posts
```

---

### WU-3.5: Notification Service

**Input:** Snapp.Shared, encryption (for decrypting email), snapp-notif + snapp-users tables
**Output:** Docker container + Lambda handling `/api/notif/*`
**Specification:**

```
Create Snapp.Service.Notification/

Endpoints/NotificationEndpoints.cs:

  GET /api/notif
    Auth: required
    Query: ?nextToken={}&limit={25}
    Handler:
    1. Query PK=NOTIF#{userId}, SK begins_with EVENT#,
       ScanIndexForward=false, Limit
    2. Return NotificationListResponse with UnreadCount

  POST /api/notif/{notifId}/read
    Auth: required
    Handler:
    1. Update PK=NOTIF#{userId},
       SK=EVENT#{...}#{notifId}: IsRead=true
    2. Return 200

  POST /api/notif/read-all
    Auth: required
    Handler:
    1. Query all unread for user
    2. Batch update IsRead=true
    3. Return 200

  GET /api/notif/preferences
    Auth: required
    Handler:
    1. Get PK=USER#{userId}, SK=NOTIF_PREFS from snapp-users
    2. Return preferences (or defaults)

  PUT /api/notif/preferences
    Auth: required
    Body: UpdatePreferencesRequest
    Handler:
    1. Validate timezone (IANA format)
    2. Validate DigestTime (HH:mm format)
    3. Save to PK=USER#{userId}, SK=NOTIF_PREFS
    4. Update digest queue entry:
       PK=DQUEUE#{digestHour}, SK=USER#{userId}
       (move to new hour bucket if DigestTime changed)
    5. Return 200

Internal (not exposed via API — called by other services):

  CreateNotification(userId, type, category, title, body,
    sourceEntityId):
    1. Generate notifId (ULID)
    2. Store: PK=NOTIF#{userId},
       SK=EVENT#{timestamp}#{notifId}
       IsRead=false, IsDigested=false
    3. Check user's ImmediateTypes preference
    4. If this type is in ImmediateTypes:
       a. Get user's encrypted email from snapp-users PII
       b. Decrypt email
       c. Send immediate email via SES/SMTP
    5. Return

  This is called internally via:
  - Direct DynamoDB write from other services
    (write notification item directly to snapp-notif table)
  - OR via EventBridge event that this service consumes

Dockerfile + Docker Compose

Test criteria:
- GET /notif returns user's notifications
- Mark read works
- Mark all read works
- Preferences save and retrieve correctly
- Changing DigestTime moves user to correct queue bucket
- Notification creation stores item
- Immediate notification sends email for opted-in types
- Non-immediate notifications stored but not emailed
```

---

### WU-3.6: Notification Digest Job

**Input:** Notification Service, snapp-notif + snapp-users tables, SES/SMTP
**Output:** Scheduled Lambda (prod) / cron Docker job (dev) that sends daily digests
**Specification:**

```
Create Snapp.Service.DigestJob/

This is a standalone Lambda / console app that runs on a schedule.

Schedule: runs every hour (cron: 0 * * * *)
  Each run processes users whose digest is due for that hour.

Handler logic:
1. Determine current UTC hour
2. Map to digest queue partition:
   Query PK=DQUEUE#{currentHourUTC}, SK begins_with USER#
   (These are users who want their digest at this UTC hour)
3. For each user in the queue:
   a. Query undigested notifications:
      PK=NOTIF#{userId}, filter IsDigested=false
   b. If zero notifications → skip (no empty digests)
   c. Group notifications by Category:
      - Referrals (ReferralReceived, ReferralOutcome)
      - Discussions (MentionInDiscussion)
      - Network (ApplicationReceived, ApplicationDecision,
        NewNetworkMember)
      - Intelligence (ValuationChanged, MilestoneAchieved)
   d. Render HTML email using Razor template:
      - Header: "Your PraxisIQ Daily Digest — {date}"
      - Per category section with notification summaries
      - "View in PraxisIQ" deep links for each item
      - Footer: "Manage notification preferences"
   e. Get user's encrypted email from snapp-users
   f. Decrypt email
   g. Send via SES/SMTP
   h. Mark all included notifications as IsDigested=true
   i. Record: PK=DIGEST#{userId}, SK=SENT#{yyyyMMdd}
      with count and categories

Razor email template:
  Located in Templates/DigestEmail.cshtml
  Uses standard HTML email best practices
  (table layout, inline styles, dark mode support)
  Sections:
    - Priority items (referrals, mentions) highlighted
    - Activity summary (X new posts, Y new members)
    - Intelligence updates (valuation changes)
    - "This week in your networks" if applicable

Error handling:
- If email send fails: log error, do NOT mark as digested
  (will retry next day)
- If user has no email → log warning, skip
- Process users in parallel (Task.WhenAll, batch of 10)

Dev mode:
- Runs as console app with --now flag for testing
- Docker Compose: snapp-digest job with cron schedule
  or manual trigger

Prod mode:
- EventBridge scheduled rule → Lambda invocation

Test criteria:
- Running job with undigested notifications → email sent
- Email contains all undigested notifications grouped by category
- Notifications marked as digested after send
- No email sent when zero undigested notifications
- Digest record created in snapp-notif
- Failed email send → notifications NOT marked as digested
- Multiple users processed in parallel
```

---

### WU-3.7: Notification UI

**Input:** Blazor Shell, Notification Service
**Output:** Notification bell, drawer, and preferences page
**Specification:**

```
Update Snapp.Client:

Components/Layout/NotificationBell.razor:
- In top nav bar
- Shows unread count badge (red dot with number)
- Polls GET /api/notif?limit=1 every 60 seconds for count
  (lightweight — only need UnreadCount from response)
- Click → toggles NotificationDrawer

Components/Layout/NotificationDrawer.razor:
- Slide-out panel from right
- List of recent notifications (most recent first)
- Each item: icon (by type), title, body preview, timestamp
- Unread items have visual indicator (bold/dot)
- Click item → mark read + navigate to source
- "Mark All Read" button at top
- "View All" link → full notifications page (future)
- "Digest Preferences" link

Pages/Notification/Preferences.razor:
- Route: /notifications/preferences (or in user settings)
- Digest time selector (hour picker, default 7:00 AM)
- Timezone selector (dropdown, auto-detect from browser)
- Per-type toggles: "Notify me immediately for:"
  - Referral received
  - Mentioned in discussion
  - Application decision
  - Valuation changed
  (All default OFF — digest only)
- Save → PUT /api/notif/preferences

Test criteria (bUnit):
- Bell shows unread count
- Drawer lists notifications
- Mark read removes visual indicator
- Preferences form saves correctly
- Bell count updates after mark-all-read
```

---

### WU-4.1: Data Contribution Service

**Input:** Snapp.Shared, Auth, Network membership, snapp-intel table
**Output:** Endpoints in Intelligence Service for data submission
**Specification:**

```
Create Snapp.Service.Intelligence/

Endpoints/DataContributionEndpoints.cs:

  POST /api/intel/contribute
    Auth: required
    Body: SubmitDataRequest { Category, DataPoints }
    Handler:
    1. Validate category against the active Vertical Config Pack
       for the user's network. Categories are NOT hard-coded —
       they are defined per vertical (e.g., a healthcare vertical
       might define Revenue, StaffCount, Utilization, ClientVolume,
       RevenueMix, OwnerProduction; a financial advisory vertical
       might define AUM, ClientCount, FeeStructure, AdvisorCount).
    2. Validate data points per category schema (also from config)
    3. Calculate ConfidenceContribution per the vertical config's
       weight map (each category has a configured confidence weight)
    4. Store: PK=PDATA#{userId}, SK=CAT#{category}
       (upsert — replaces previous submission for same category)
    5. Recalculate user's total confidence score:
       base 40% (public signals) + sum of contributions
    6. Trigger benchmark recomputation if enough data
       (write event to EventBridge or DynamoDB stream)
    7. Return { category, confidenceContribution, totalConfidence }

  GET /api/intel/contributions
    Auth: required
    Handler:
    1. Query PK=PDATA#{userId}, SK begins_with CAT#
    2. Return list of contributed categories with timestamps

  GET /api/intel/dashboard
    Auth: required
    Handler:
    1. Get user's practice data contributions
    2. Get current valuation (if exists)
    3. Get relevant benchmarks for user's cohort
    4. Build KPI list from available data
    5. Return DashboardResponse { KPIs, ConfidenceScore,
       ValuationSummary }

Test criteria:
- Submit data → stored correctly
- Re-submitting same category → updates (upsert)
- Confidence score increases with contributions
- Dashboard returns KPIs from available data
- Invalid category → 400
- Invalid data points for category → 400
```

---

### WU-4.2: Benchmark Engine

**Input:** Data Contribution Service, snapp-intel table
**Output:** Benchmark computation endpoints + stream trigger
**Specification:**

```
Add to Snapp.Service.Intelligence/

Endpoints/BenchmarkEndpoints.cs:

  GET /api/intel/benchmarks
    Auth: required
    Query: ?specialty={}&geo={}&size={}
    Handler:
    1. Validate params
    2. Query PK=BENCH#{specialty}#{geo}#{size},
       SK begins_with METRIC#
    3. If user has contributed data for matching metrics,
       compute their percentile position
    4. Return BenchmarkResponse { Metrics, CohortSize }

DynamoDB Stream Trigger (or scheduled job):
  BenchmarkComputeHandler:
  Triggered when new PDATA# items are written.

  Logic:
  1. Get all contributors for the same
     specialty + geography + size band
  2. For each metric (Revenue, RecallRate, etc.):
     a. Collect all values
     b. Compute P25, P50, P75
     c. Compute sample size
  3. Store/update: PK=BENCH#{specialty}#{geo}#{size},
     SK=METRIC#{metricName}
  4. Only compute if sample size ≥ 5
     (anonymity threshold — need at least 5 to show benchmarks)

  Minimum anonymity rule:
  - Never show benchmarks with <5 contributors
  - Never show individual data points
  - Cohort must be broad enough to prevent de-identification

Test criteria:
- With 5+ contributors in same cohort → benchmarks computed
- With <5 contributors → no benchmark (returns empty)
- User's percentile correctly positioned
- Benchmark updates when new data contributed
- Anonymity threshold enforced
```

---

### WU-4.3: Valuation Engine

**Input:** Data Contribution, Benchmark Engine, snapp-intel table
**Output:** Valuation computation endpoints
**Specification:**

```
Add to Snapp.Service.Intelligence/

Endpoints/ValuationEndpoints.cs:

  GET /api/intel/valuation
    Auth: required
    Handler:
    1. Get PK=VAL#{userId}, SK=CURRENT
    2. Get valuation history:
       PK=VAL#{userId}, SK begins_with SNAPSHOT#
       (last 12 entries)
    3. Return ValuationResponse

  POST /api/intel/valuation/compute
    Auth: required
    Handler:
    1. Get all user's practice data (PK=PDATA#{userId})
    2. Get relevant benchmarks for user's cohort
    3. Compute three-case model:

       Revenue = contributed or estimated from public data
       EBITDA margin = contributed or benchmarked (P50 for cohort)
       Multiple = based on specialty, size, owner dependence

       Downside case:
         Revenue × (EBITDA_margin - 5%) × (Multiple - 0.5)
       Base case:
         Revenue × EBITDA_margin × Multiple
       Upside case:
         Revenue × (EBITDA_margin + 5%) × (Multiple + 0.5)

    4. Compute confidence score:
       Sum of weighted confidence contributions
       (see WU-4.1 for per-category weights)

    5. Identify drivers (resolved from Vertical Config Pack):
       - Key-person/owner dependency (biggest suppressor
         in most owner-operated practices)
       - Revenue source diversification
       - Staff stability and depth
       - Facility/lease position
       - Market dynamics (growth vs. saturation,
         consolidation pressure)

    6. Store as PK=VAL#{userId}, SK=CURRENT
    7. Store historical: PK=VAL#{userId},
       SK=SNAPSHOT#{timestamp}
    8. If previous valuation exists and changed significantly
       (>5%): queue notification (ValuationChanged)
    9. Return ValuationResponse

  POST /api/intel/valuation/scenario
    Auth: required
    Body: { Overrides: Dictionary<string,string> }
    Handler:
    1. Same computation as /compute but with user-provided
       overrides (e.g., "OwnerProduction": "50" to model
       reducing from 80% to 50%)
    2. Return ValuationResponse (not saved — scenario only)

Test criteria:
- Valuation computed from available data
- Three cases: downside < base < upside
- Confidence score matches data completeness
- History tracked over time
- Scenario modeling returns different values with overrides
- Notification queued on significant change
- Missing data → wider range, lower confidence
```

---

### WU-4.4: Practice Dashboard UI

**Input:** Blazor Shell, Data Contribution, Benchmark, Valuation services
**Output:** Main intelligence dashboard page
**Specification:**

```
Update Snapp.Client:

Pages/Intelligence/Dashboard.razor:
- Route: /intelligence
- Hero section: valuation range card (if computed)
  Downside — **Base** — Upside
  Confidence: {X}% (progress bar)
  "Last updated: {date}"
- KPI grid (2x3 or responsive):
  Each KPI card: value, unit, trend arrow, percentile bar
- "Contribute Data" CTA if confidence < 65%
- "View Full Benchmarks" link
- "View Valuation Details" link

Pages/Intelligence/Contribute.razor:
- Route: /intelligence/contribute
- Category selector (dropdown or tabs)
- Per-category form: dynamically rendered from the active
  Vertical Configuration Pack. Each category defines its
  input fields (band selectors, percentages, counts, sliders).
  The UI renders the form from config, not hard-coded fields.
  Examples vary by vertical — a healthcare network might ask
  about revenue bands, staff counts, and client volume; a
  financial advisory network might ask about AUM, client count,
  and fee structure.
- Submit → POST /api/intel/contribute
- Shows confidence improvement: "This will raise your
  confidence to {X}%"
- After submit: "Contribution saved. +{N}% confidence"

Components/Intelligence/KpiCard.razor:
- Value + unit (large)
- Trend indicator (up arrow green, down red, flat gray)
- Percentile bar (if benchmark available):
  "You're at the 65th percentile for your cohort"
- Mini sparkline (last 6 data points if available)

Components/Intelligence/ConfidenceBar.razor:
- Segmented bar: 0-40% (red), 40-65% (yellow),
  65-85% (blue), 85-100% (green)
- Current score marker
- Labels at each tier boundary

Test criteria (bUnit):
- Dashboard renders with available data
- KPI cards show values and trends
- Contribute form submits correctly
- Confidence bar reflects score
- CTA shows when confidence is low
```

---

### WU-4.5: Benchmarking UI

**Input:** Blazor Shell, Benchmark Engine
**Output:** Benchmarking visualization page
**Specification:**

```
Pages/Intelligence/Benchmark.razor:
- Route: /intelligence/benchmark
- Cohort selector:
  Specialty (dropdown), Geography (dropdown), Size Band (dropdown)
  "Compare against {N} peers" (sample size)
- Benchmark table:
  One row per metric
  Columns: Metric Name, Your Value, P25, P50, P75, Percentile
  Color-coded: red if below P25, yellow P25-P50,
  green above P50
- Bar chart visualization:
  Horizontal bars showing distribution with user's position marked

Components/Intelligence/PercentileBar.razor:
- Horizontal bar: P25/P50/P75 markers
- User's position as a dot/arrow on the bar
- Color gradient from red (low) to green (high)

Test criteria (bUnit):
- Cohort selector filters benchmarks
- Table renders metrics with user position
- Percentile bars render correctly
- "Not enough data" message when sample < 5
```

---

### WU-4.6: Valuation UI

**Input:** Blazor Shell, Valuation Engine
**Output:** Valuation detail and scenario modeling pages
**Specification:**

```
Pages/Intelligence/Valuation.razor:
- Route: /intelligence/valuation
- Three-case display:
  Three columns: Downside | **Base** | Upside
  Large dollar values, confidence badge
- Driver analysis:
  List of drivers with impact indicators
  "Owner dependence: HIGH impact — reducing from 80% to 60%
   could increase valuation by $X-$Y"
- Scenario modeling:
  Sliders/inputs to override key drivers
  "What if..." section
  Real-time recalculation (calls /api/intel/valuation/scenario)
  Shows delta: "This scenario → +$X base case"
- Valuation history:
  Line chart showing base case over time
  Confidence score overlay

Components/Intelligence/ValuationCard.razor:
- Compact three-case summary for dashboard embedding
- Confidence badge
- "Details →" link

Components/Intelligence/DriverList.razor:
- Per driver: name, current value, impact level
  (high/medium/low), recommendation text

Test criteria (bUnit):
- Three-case values render
- Driver list shows all identified drivers
- Scenario sliders update values
- History chart renders with data points
- "Insufficient data" shown when confidence < 40%
```

---

### WU-5.1: Referral Service

**Input:** Snapp.Shared, Auth, Network membership, snapp-tx table
**Output:** Referral endpoints in Transaction Service
**Specification:**

```
Create Snapp.Service.Transaction/

Endpoints/ReferralEndpoints.cs:

  POST /api/tx/referrals
    Auth: required
    Body: CreateReferralRequest
    Handler:
    1. Verify both sender and receiver are members of the network
    2. Generate referralId (ULID)
    3. Create PK=REF#{refId}, SK=META
    4. Create sender index: PK=UREF#{senderId}#SENT,
       SK=REF#{timestamp}#{refId}
    5. Create receiver index: PK=UREF#{receiverId}#RECV,
       SK=REF#{timestamp}#{refId}
    6. Queue notification for receiver: ReferralReceived
    7. Return ReferralResponse

  GET /api/tx/referrals/sent
    Auth: required
    Handler:
    1. Query PK=UREF#{userId}#SENT, SK begins_with REF#,
       ScanIndexForward=false
    2. Enrich with receiver display names
    3. Return ReferralListResponse

  GET /api/tx/referrals/received
    Auth: required
    Handler:
    1. Query PK=UREF#{userId}#RECV
    2. Enrich with sender display names
    3. Return ReferralListResponse

  PUT /api/tx/referrals/{refId}/status
    Auth: required (sender or receiver only)
    Body: UpdateReferralStatusRequest
    Handler:
    1. Get referral, verify participant
    2. Validate status transition
       (Created→Accepted, Accepted→Completed,
        any→Expired)
    3. Update PK=REF#{refId}, SK=META
    4. Return updated ReferralResponse

  POST /api/tx/referrals/{refId}/outcome
    Auth: required (sender or receiver)
    Body: RecordOutcomeRequest
    Handler:
    1. Get referral, verify participant
    2. Update: Outcome, Success, OutcomeRecordedAt
    3. Trigger reputation recalculation for both parties
    4. Queue notification: ReferralOutcome
    5. Return updated ReferralResponse

Dockerfile + Docker Compose

Test criteria:
- Create referral → stored with correct indexes
- Receiver gets notification
- Sent/received queries return correct referrals
- Status transitions validated (can't go backwards)
- Outcome recording triggers reputation update
- Non-participant cannot modify referral
```

---

### WU-5.2: Reputation Service

**Input:** Referral Service, Data Contribution, snapp-tx table
**Output:** Reputation computation endpoints + event trigger
**Specification:**

```
Add to Snapp.Service.Transaction/

Endpoints/ReputationEndpoints.cs:

  GET /api/tx/reputation/{userId}
    Auth: required
    Handler:
    1. Get PK=REP#{userId}, SK=CURRENT
    2. Return ReputationResponse

  GET /api/tx/reputation/{userId}/history
    Auth: required
    Handler:
    1. Query PK=REP#{userId}, SK begins_with SNAP#
    2. Return list of ReputationResponse (for trending)

Internal: ReputeComputeHandler (triggered by referral outcome
  or data contribution event):

  Logic:
  1. Get user's referral outcomes:
     - Successful referrals sent (count + recency)
     - Successful referrals received (count)
  2. Get user's data contributions from snapp-intel
  3. Get peer attestations:
     PK=ATTEST#{userId}, SK begins_with FROM#
  4. Compute scores:
     ReferralScore = f(successful_referrals, recency_weight)
     ContributionScore = f(data_categories_contributed,
       discussion_posts_count)
     AttestationScore = f(attestation_count,
       attestor_reputation_weight)
     OverallScore = weighted average
       (Referral: 40%, Contribution: 30%, Attestation: 30%)
  5. Apply decay: scores decrease 5% per month of inactivity
  6. Anti-gaming:
     - Detect reciprocal attestation rings
       (A attests B, B attests A within 7 days)
     - Flag but don't auto-reject (steward review)
  7. Save PK=REP#{userId}, SK=CURRENT
  8. Save PK=REP#{userId}, SK=SNAP#{timestamp}

  POST /api/tx/attestations
    Auth: required
    Body: { TargetUserId, Domain, CompetencyArea, Text }
    Handler:
    1. Cannot attest self
    2. Must share at least one network
    3. Create PK=ATTEST#{targetId}, SK=FROM#{attestorId}
    4. Trigger reputation recomputation for target
    5. Return 200

Test criteria:
- Reputation computed from actual transactions
- Score increases with successful referrals
- Decay applied to inactive users
- Reciprocal attestation detected and flagged
- Self-attestation rejected
- History tracked for trending
```

---

### WU-5.3: Referral UI

**Input:** Blazor Shell, Referral Service
**Specification:**

```
Pages/Referrals/List.razor:
- Route: /referrals
- Tabs: Sent | Received
- Each tab: list of referral cards
- Status badges: Created (blue), Accepted (yellow),
  Completed (green), Expired (gray)
- "New Referral" button

Pages/Referrals/Create.razor:
- Route: /referrals/create
- Form: Network selector, Receiver (search members),
  Specialty, Notes
- Submit → POST /api/tx/referrals

Components/Transaction/ReferralCard.razor:
- Sender/receiver names, specialty, status badge
- Expand: notes, outcome (if recorded)
- Actions: Accept (receiver), Record Outcome (either party)

Test criteria: renders lists, creates referrals, status updates work
```

---

### WU-5.4: Reputation UI

**Specification:**

```
Components/Transaction/ReputationBadge.razor:
- Compact badge: score (0-100) with color
- Tooltip: breakdown (referral/contribution/attestation)
- Used in ProfileCard, member lists, referral cards

Pages/Profile/ReputationDetail.razor:
- Full breakdown: three sub-scores with bars
- History chart (line graph of OverallScore over time)
- Recent attestations received
- "Request Attestation" button

Test criteria: badge renders, detail page shows breakdown
```

---

### WU-5.5: Deal Room Service

**Specification:**

```
Add to Snapp.Service.Transaction/

  POST /api/tx/deals — create deal room
  GET /api/tx/deals — list user's deal rooms
  GET /api/tx/deals/{dealId} — get deal room (participant only)
  POST /api/tx/deals/{dealId}/participants — add participant
  DELETE /api/tx/deals/{dealId}/participants/{userId} — remove
  POST /api/tx/deals/{dealId}/documents — upload
    (returns pre-signed S3 URL for direct upload)
  GET /api/tx/deals/{dealId}/documents — list documents
  GET /api/tx/deals/{dealId}/documents/{docId}/url
    — get pre-signed download URL
  GET /api/tx/deals/{dealId}/audit — audit trail

S3: separate prefix per deal (snapp-media/deals/{dealId}/)
Audit: every action logged to snapp-tx as AUDIT# items

Test criteria:
- Only participants can access deal room
- Documents upload via pre-signed URL
- Audit trail records all actions
- Removing participant revokes access immediately
```

---

### WU-5.6: Deal Room UI

**Specification:**

```
Pages/DealRoom/List.razor — user's deal rooms
Pages/DealRoom/View.razor — deal room dashboard:
  Participants list, document list, activity log
  "Add Participant" (seller/buyer/advisor roles)
  "Upload Document" → gets pre-signed URL, uploads directly
  Document list: filename, uploaded by, date, download link
  Audit log: chronological activity

Test criteria: renders deal rooms, upload/download works,
  participant management functional
```

---

### WU-6.1: LinkedIn OAuth Service

**Specification:**

```
Create Snapp.Service.LinkedIn/

  GET /api/linkedin/auth-url
    Returns LinkedIn OAuth 2.0 authorization URL
    (client_id, redirect_uri, scope: openid profile email
     w_member_social, state: CSRF token)

  POST /api/linkedin/callback
    Body: { Code, State }
    Handler:
    1. Validate CSRF state
    2. Exchange code for access token with LinkedIn
    3. Fetch profile: GET https://api.linkedin.com/v2/userinfo
    4. Encrypt LinkedIn access token via IFieldEncryptor
    5. Store in snapp-users: PK=USER#{userId}, SK=LINKEDIN
       EncryptedLinkedInURN, EncryptedAccessToken, TokenExpiry
    6. Update profile completeness (+15%)
    7. Return { linkedInName, linkedInHeadline, photoUrl }

  GET /api/linkedin/status
    Auth: required
    Returns: { isLinked: bool, linkedInName, tokenExpiry }

  POST /api/linkedin/unlink
    Auth: required
    Deletes LinkedIn item from snapp-users

Test criteria:
- OAuth URL generated with correct params
- Token exchange works (mock LinkedIn API in test)
- LinkedIn data encrypted before storage
- Profile completeness updated
- Unlink removes data
```

---

### WU-6.2: LinkedIn Share Service

**Specification:**

```
Add to Snapp.Service.LinkedIn/

  POST /api/linkedin/share
    Auth: required (must have linked LinkedIn)
    Body: { Content, NetworkId, SourceType (post/milestone) }
    Handler:
    1. Get user's LinkedIn token from snapp-users, decrypt
    2. Check token not expired (refresh if needed)
    3. Format as LinkedIn Share:
       POST https://api.linkedin.com/rest/posts
       Header: LinkedIn-Version: 202401
       Body: { author: urn, commentary: content,
         visibility: PUBLIC, distribution: MAIN_FEED }
    4. Append: "— via PraxisIQ {deeplink}"
    5. Track: store cross-post record in snapp-content
    6. Return { linkedInPostUrl }

  Rate limiting: max 25 shares per day per user
    (LinkedIn's own limit is higher, but be conservative)

Test criteria:
- Share creates LinkedIn post (mock LinkedIn API)
- Deep link included in post
- Rate limiting enforced
- Expired token → attempt refresh → share
- Unlinked user → 400
```

---

### WU-6.3: LinkedIn UI

**Specification:**

```
Components/LinkedIn/LinkButton.razor:
- "Connect LinkedIn" button (if not linked)
- "LinkedIn Connected ✓" indicator (if linked)
- Click → redirect to LinkedIn OAuth URL
- Callback page handles token exchange

Components/LinkedIn/CrossPostToggle.razor:
- Toggle in post composer: "Also share on LinkedIn"
- When enabled + post submitted → call /api/linkedin/share
- Shows LinkedIn icon when cross-posting

Pages/Profile section update:
- Show LinkedIn link status in profile
- "Unlink LinkedIn" option in settings

Test criteria: link/unlink flow works, cross-post toggle functional
```

---

### WU-6.4: Discord Bridge Service

**Specification:**

```
Create Snapp.Service.Discord/ (or add to existing)

Discord Bot (runs as long-lived Docker container, not Lambda):
  - Connects to Discord via Gateway WebSocket
  - Manages server ↔ network mappings
  - Relays:
    SNAPP → Discord: new posts, notifications, milestones
    Discord → SNAPP: messages tagged for relay

  Slash commands in Discord:
    /snapp link {networkId} — links Discord channel to network
    /snapp benchmark — shows user's benchmark summary
    /snapp valuation — links to valuation page
    /snapp profile — links to SNAPP profile

  Webhook relay:
    Discord webhook → Kong → SNAPP handler
    Creates content entries in snapp-content

  Account linking:
    POST /api/discord/link { discordUserId }
    Stores mapping in snapp-users

This is a Tier 6 module — deferred until after core platform
is functional. Design now, build later.

Test criteria:
- Bot connects to Discord test server
- Slash commands return correct data
- Messages relay bidirectionally
- Account linking works
```

---

### WU-6.5: Channel Email Relay (Teams/Slack)

**Input:** Snapp.Shared, Network Service, Notification Service, SES/SMTP
**Output:** Channel relay endpoints in Network Service + email relay logic in Notification/Content services
**Specification:**

```
This module enables SNAPP to relay content to Microsoft Teams
channels and Slack channels via their inbound email addresses.
No Teams or Slack API integration is required — both platforms
accept inbound email to channels natively.

How it works:
  - Teams: Channel settings → "Get email address" produces
    an address like {guid}@{tenant}.teams.ms
  - Slack: Install the "Email" app → each channel gets
    an address like {channel}@{workspace}.slack.com
  - SNAPP sends a formatted HTML email to that address
  - The email appears as a post in the Teams/Slack channel

1. Steward Configuration Endpoints

   Add to Snapp.Service.Network/Endpoints/RelayEndpoints.cs:

   POST /api/networks/{netId}/relays
     Auth: required (steward only)
     Body: {
       ChannelEmail: "abc@team.teams.ms",
       Platform: "Teams" | "Slack" | "Other",
       Label: "General Channel",
       RelayTypes: ["posts", "milestones", "digest"]
     }
     Handler:
     1. Verify steward permission
     2. Generate channelId (ULID)
     3. Encrypt ChannelEmail via IFieldEncryptor
     4. Store: PK=NET#{netId}, SK=RELAY#{channelId}
        IsVerified=false
     5. Send verification email to the channel address:
        Subject: "PraxisIQ Network Relay Verification"
        Body: "This channel has been connected to the
        PraxisIQ network '{NetworkName}'. If you did not
        expect this, no action is needed — no further
        emails will be sent until verified."
        + verification link:
        {baseUrl}/api/networks/{netId}/relays/{channelId}/verify?code={code}
     6. Store verification code (TOKEN# pattern, 24h TTL)
     7. Return { channelId, status: "pending_verification" }

   POST /api/networks/{netId}/relays/{channelId}/verify
     Query: ?code={code}
     Handler:
     1. Validate code (same pattern as magic link)
     2. Update RELAY item: IsVerified=true
     3. Send confirmation email to channel:
        "This channel is now connected to PraxisIQ
         network '{NetworkName}'. You will receive:
         {relayTypes list}."
     4. Return 200 / redirect to success page

   GET /api/networks/{netId}/relays
     Auth: required (steward only)
     Handler:
     1. Query PK=NET#{netId}, SK begins_with RELAY#
     2. Decrypt channel emails for display
     3. Return list of relay configs

   DELETE /api/networks/{netId}/relays/{channelId}
     Auth: required (steward only)
     Handler:
     1. Delete RELAY item
     2. Optionally send farewell email to channel:
        "This channel has been disconnected from PraxisIQ."
     3. Return 200

   PUT /api/networks/{netId}/relays/{channelId}
     Auth: required (steward only)
     Body: { RelayTypes, Label } (cannot change email —
       delete and re-add for that)
     Handler:
     1. Update RELAY item
     2. Return 200

2. Content Relay Logic

   When a relayable event occurs, the service checks for
   verified relays on the network and sends formatted email.

   Events and their relay mapping:
   - New post (if "posts" in RelayTypes):
     Subject: "[{NetworkName}] New post from {AuthorName}"
     Body: post content (rendered from markdown to HTML),
     author info, "View in PraxisIQ" deep link
     Sent: immediately on post creation

   - Milestone (if "milestones" in RelayTypes):
     Subject: "[{NetworkName}] Milestone: {MilestoneTitle}"
     Body: milestone details, author, deep link
     Sent: immediately on milestone post

   - Daily digest (if "digest" in RelayTypes):
     Subject: "[{NetworkName}] Daily Activity — {date}"
     Body: summary of day's activity (post count,
     new members, discussion highlights)
     Sent: as part of the digest job (WU-3.6),
     after user digests are sent

   Implementation in Snapp.Service.Content:
     After creating a post/milestone, check for relays:
     1. Query PK=NET#{netId}, SK begins_with RELAY#
     2. Filter: IsVerified=true AND relayType matches
     3. For each: decrypt channel email, send formatted email
     4. Fire-and-forget (don't block post creation on relay)
        Use EventBridge or DynamoDB Stream trigger

   Implementation in Snapp.Service.DigestJob:
     After sending user digests, check for digest relays:
     1. For each network with activity that day
     2. Query relay configs where "digest" in RelayTypes
     3. Send network activity summary email

3. Email Formatting

   Razor templates in Snapp.Shared/Templates/:
   - ChannelRelayPost.cshtml — single post relay
   - ChannelRelayMilestone.cshtml — milestone relay
   - ChannelRelayDigest.cshtml — daily network summary

   Design constraints for Teams/Slack email rendering:
   - Simple HTML (tables for layout, inline CSS)
   - No complex CSS (Teams strips most styles)
   - Include plain-text alternative (multipart/alternative)
   - Keep under 25KB (Slack truncates larger emails)
   - Include PraxisIQ branding + deep link
   - "Manage this relay" link for stewards

4. Network Settings UI Addition

   Add to Pages/Network/Settings.razor, new tab: "Channels"
   - List of connected channels with status badges
     (verified ✓ / pending ⏳)
   - "Add Channel" form:
     Platform selector (Teams/Slack/Other)
     Channel email input
     Label input
     Relay type checkboxes (posts/milestones/digest)
   - Per-channel: edit relay types, remove
   - Instructions panel:
     "How to find your Teams channel email:
      1. Open Teams → channel → ⋯ → Get email address
      2. Paste the address here"
     "How to find your Slack channel email:
      1. Install the Email app in Slack
      2. Open channel → ⋯ → Integrations → Send emails
      3. Paste the address here"

Test criteria:
- Steward can add a channel relay
- Verification email sent to channel address
- Verification link activates the relay
- Post creation triggers relay email to verified channels
- Unverified channels receive no relay emails
- Milestone posts trigger milestone relay
- Digest job includes channel digest for networks with
  digest relay configured
- Delete removes relay and optionally notifies channel
- Channel email is encrypted in DynamoDB
- Email format renders acceptably in Teams and Slack
  (test with actual Teams/Slack channels)
- Relay failure (email bounce) does not block post creation
- Labels and relay types can be updated
```

---

### WU-7.1: Public Signal Aggregation (Provider Registry + Business Listings)

**Specification:**

```
Create Snapp.Service.Enrichment/ (scheduled job)

Runs daily (or on-demand per user):

This service is vertical-aware — it reads the active
Vertical Configuration Pack to determine which registries,
directories, and public sources to query for a given
network's vertical.

Sources (resolved per vertical config):
  Practitioner Registry:
    - Query the authoritative registry for the vertical
      (e.g., NPPES for healthcare, SEC/FINRA for financial,
       state bar for legal, state boards for trades)
    - Extract: identity, taxonomy/specialty, practice address,
      co-located practitioners, registration date
    - Store: PK=SIGNAL#{userId}, SK=SRC#REGISTRY#{registryId}

  Business Listings:
    - Search Google Places / industry directories by
      practice name + address
    - Extract: rating, review count, hours, website, photos
    - Multi-pass matching: phone exact → address exact →
      name+location fuzzy (with confidence scoring)
    - Store: PK=SIGNAL#{userId}, SK=SRC#LISTING#{listingId}

  State Licensing / Regulatory:
    - Query state license boards (per vertical config)
    - Extract: license status, credential type, issue date
    - Store: PK=SIGNAL#{userId}, SK=SRC#LICENSE#{state}

  Job Postings:
    - Search general + industry-specific job boards
      by practice name / location
    - Extract: roles hiring, posting frequency, urgency
    - Store: PK=SIGNAL#{userId}, SK=SRC#JOBS#{source}

Each signal: cached with TTL (30 days), re-fetched on expiry.
Confidence score (0.0-1.0) attached to every signal.

Updates profile completeness (public data portion).
Pre-populates fields for onboarding.
Feeds scoring engine with public signal inputs.

Test criteria:
- Registry lookup returns practitioner data (mock API in test)
- Business listing matching produces correct confidence scores
- Signals cached with TTL
- Re-fetch on expiry
- Profile completeness updated from public signals
- Vertical config determines which sources are queried
```

---

### WU-7.2: Compensation Benchmarking

**Specification:**

```
Extends M4.3 Benchmark Engine with compensation-specific logic.

New contribution categories:
  StaffCompensation:
    - Role (vertical-defined: the roles relevant to this
      practice type, configured in Vertical Config Pack)
    - Compensation type (hourly/salary/dailyRate)
    - Amount (band, not exact: $30-35/hr, $35-40/hr, etc.)
    - Benefits included (boolean flags)

  CompensationBenchmark output:
    PK=COHORT#{vertical}#{specialty}#{size},
    SK=METRIC#COMP#{role}
    P25, P50, P75 for compensation by role

  Intelligence output to user:
    "Practices in your market with your revenue profile
     pay [role] between $X and $Y"

Same anonymity threshold (≥5 contributors).

This is the guild's unique value proposition per the
GuildThinking analysis — compensation transparency that
doesn't exist anywhere else. Every vertical has the same
information asymmetry problem with staff compensation.

Test criteria:
- Compensation data accepted and validated
- Benchmarks computed per role per cohort
- Role list resolved from vertical config (not hard-coded)
- Anonymity threshold enforced
- Results formatted as range statements
```

---

## 8. Cross-Cutting Concerns

### 8.1 Error Handling

All services return consistent error responses:

```json
{
  "error": {
    "code": "NETWORK_NOT_FOUND",
    "message": "The requested network does not exist",
    "traceId": "abc-123-def"
  }
}
```

HTTP status codes: 400 (validation), 401 (auth), 403 (forbidden), 404 (not found), 429 (rate limit), 500 (unexpected).

Blazor client: global error boundary catches unhandled exceptions, displays user-friendly message, logs to console (dev) or API (prod).

### 8.2 Observability

- **Structured logging**: every service logs JSON with `traceId`, `userId`, `action`, `duration`
- **Local dev**: logs to stdout (visible in Docker Compose logs)
- **Prod**: CloudWatch Logs with custom metrics
- **X-Ray tracing** (prod): enabled on API Gateway + Lambda
- **Kong logging plugin** (dev): request/response logging

### 8.3 Testing Strategy

| Layer | Tool | Scope |
|-------|------|-------|
| Shared library | xUnit | Model validation, serialization, encryption round-trip |
| Services | xUnit + DynamoDB Local (Docker) | Endpoint + repository integration tests |
| Blazor UI | bUnit | Component rendering, state management |
| E2E | Playwright (.NET) | Critical flows: login → create network → post → referral |
| Infrastructure | Pulumi testing | Stack produces expected resources |

### 8.4 Deployment

- **CI/CD**: GitHub Actions
- **Pipeline**: build → test (with Docker Compose for DynamoDB) → Pulumi deploy (staging) → E2E → Pulumi deploy (prod)
- **Environments**: `dev` (local Docker), `staging` (auto-deploy on PR merge), `prod` (manual approval)
- **Blazor WASM**: build → publish to S3 → CloudFront invalidation
- **Lambda**: build with Native AOT → zip → deploy via Pulumi
- **Docker images**: pushed to ECR (for staging env that runs on ECS if Lambda isn't suitable for a service)

### 8.5 OpenAPI Specification & SDK Generation

Every SNAPP service produces an OpenAPI 3.1 specification directly from its Minimal API endpoint definitions. These specs are merged into a unified API specification and used to generate type-safe client SDKs.

#### Per-Service OpenAPI Generation

Each service uses .NET 9's built-in OpenAPI support (`Microsoft.AspNetCore.OpenApi`):

```csharp
// Program.cs (every service)
builder.Services.AddOpenApi();

var app = builder.Build();
app.MapOpenApi();  // serves /openapi/v1.json

// Endpoints self-document via Minimal API metadata:
app.MapPost("/api/auth/magic-link", HandleMagicLink)
   .WithName("RequestMagicLink")
   .WithDescription("Send a magic link to the provided email address")
   .WithTags("Authentication")
   .Accepts<MagicLinkRequest>("application/json")
   .Produces<MessageResponse>(200)
   .Produces<ErrorResponse>(429)
   .WithOpenApi();
```

Every endpoint must include:
- **Operation ID** (via `.WithName()`) — becomes the SDK method name
- **Description** (via `.WithDescription()`)
- **Tags** (via `.WithTags()`) — groups endpoints in the spec
- **Request/response types** (via `.Accepts<T>()` / `.Produces<T>()`) — auto-generates schemas from Snapp.Shared DTOs
- **Error responses** — all possible error status codes documented

#### Spec Merging

A build-time tool (`Snapp.Tools.SpecMerge`) merges per-service specs into a unified `snapp-api.yaml`:

```
Input:
  Snapp.Service.Auth/openapi.json       → /api/auth/* endpoints
  Snapp.Service.User/openapi.json       → /api/users/* endpoints
  Snapp.Service.Network/openapi.json    → /api/networks/* endpoints
  Snapp.Service.Content/openapi.json    → /api/content/* endpoints
  Snapp.Service.Intelligence/openapi.json → /api/intel/* endpoints
  Snapp.Service.Transaction/openapi.json → /api/tx/* endpoints
  Snapp.Service.Notification/openapi.json → /api/notif/* endpoints
  Snapp.Service.LinkedIn/openapi.json   → /api/linkedin/* endpoints

Output:
  snapp-api.yaml (unified OpenAPI 3.1 spec)
```

The merged spec includes:
- Shared security scheme (Bearer JWT)
- Shared error response schemas (from Snapp.Shared)
- Server URLs for dev (http://localhost:8000) and prod
- All DTO schemas generated from Snapp.Shared model/DTO types

#### SDK Generation with Microsoft Kiota

**Kiota** is Microsoft's OpenAPI-based SDK generator. It produces idiomatic, type-safe clients from any OpenAPI spec.

**C# SDK (default — `Snapp.Sdk`):**
```bash
kiota generate \
  --language CSharp \
  --openapi snapp-api.yaml \
  --output src/Snapp.Sdk \
  --class-name SnappApiClient \
  --namespace-name Snapp.Sdk
```

The generated SDK:
- Produces a `SnappApiClient` class with strongly typed methods for every endpoint
- Uses `Snapp.Shared` DTOs directly (no duplicate models — Kiota configured to reference shared types)
- Handles authentication via a Kiota `IAuthenticationProvider` that attaches the Bearer token
- Supports retry, redirect, and error handling via Kiota middleware

**Blazor WASM uses the SDK instead of hand-written HttpClient wrappers:**
```csharp
// Program.cs (Blazor client)
builder.Services.AddSnappSdk(options =>
{
    options.BaseUrl = builder.Configuration["ApiBaseUrl"];
    options.AuthProvider = new BearerTokenProvider(authState);
});

// Usage in a Blazor component:
@inject SnappApiClient Api

var feed = await Api.Content.Networks[netId].Feed.GetAsync();
```

**Other language SDKs** generated from the same spec:

| Language | Kiota Flag | Output | Use Case |
|----------|-----------|--------|----------|
| C# | `--language CSharp` | `Snapp.Sdk` | Blazor WASM, MAUI, internal tools |
| TypeScript | `--language TypeScript` | `snapp-sdk-ts` | Third-party web integrations, Discord bot |
| Python | `--language Python` | `snapp-sdk-python` | Data analysis, scripting, Jupyter notebooks |
| Java | `--language Java` | `snapp-sdk-java` | Android, enterprise integrations |
| Go | `--language Go` | `snapp-sdk-go` | CLI tools, infrastructure integrations |

SDK generation is automated in CI — any endpoint change triggers regeneration and version bump.

#### Kong Dev Portal (Local)

Kong's dev portal (or a Swagger UI container) serves the merged spec locally:

```yaml
# docker-compose.yml addition
swagger-ui:
  image: swaggerapi/swagger-ui:latest
  ports: ["8090:8080"]
  environment:
    SWAGGER_JSON_URL: http://kong:8000/api/openapi
```

Developers and API consumers can explore the full API at `http://localhost:8090`.

#### Spec Validation in CI

The CI pipeline validates that:
1. Each service's OpenAPI spec is syntactically valid
2. The merged spec is valid OpenAPI 3.1
3. No breaking changes vs. the previous version (using `oasdiff`)
4. The generated C# SDK compiles against Snapp.Shared
5. SDK integration tests pass

### 8.6 Cost Estimate (Scale: 0 → 10,000 users)

| Service | Free Tier | At 10K Users (est.) |
|---------|-----------|---------------------|
| DynamoDB On-Demand (6 tables) | 25 RCU/WCU always free | ~$10-30/mo |
| Lambda | 1M requests/mo free | ~$3-10/mo |
| API Gateway | 1M calls/mo free | ~$3-5/mo |
| S3 + CloudFront | 5GB + 1M requests free | ~$5-15/mo |
| SES | 62K emails/mo free (from Lambda) | ~$1-5/mo |
| KMS | $1/key/mo + $0.03/10K requests | ~$2-5/mo |
| Secrets Manager | | ~$1/mo |
| **Total** | **~$1/mo for MVP** | **~$25-75/mo at 10K users** |

---

## 9. Future Considerations

These are explicitly **out of scope** for initial implementation but architecturally accounted for:

| Feature | Architectural Hook |
|---------|-------------------|
| Mobile app | Blazor WASM is responsive; native MAUI app can share Snapp.Shared and call same API |
| PMS integration (Dentrix, Eaglesoft) | Intelligence service already accepts structured data; PMS adapter Lambda writes to same snapp-intel table |
| Accounting integration (QuickBooks, Xero) | Same adapter pattern → snapp-intel table |
| Full-text search | DynamoDB Streams → OpenSearch Serverless |
| Real-time updates (WebSocket) | API Gateway WebSocket API → Lambda; Blazor WASM supports natively |
| Payment processing | Stripe as new service; network subscription billing |
| Admin dashboard | Separate Blazor WASM app, same Snapp.Shared, admin-scoped endpoints |
| Multi-region | DynamoDB Global Tables, CloudFront already global |
| AI/ML valuation model | SageMaker endpoint called from Intelligence Lambda |

---

## Appendix A: Technology Versions

| Technology | Version | Notes |
|------------|---------|-------|
| .NET | 9.0 | LTS, Native AOT support |
| Blazor WebAssembly | .NET 9 | Standalone hosting model |
| Pulumi | 3.x | C# (Pulumi.Aws, Pulumi.Docker packages) |
| AWS Lambda | .NET 9 custom runtime (Native AOT) | Amazon.Lambda.AspNetCoreServer.Hosting |
| DynamoDB | On-Demand | AWS SDK for .NET v3 |
| Kong | 3.6.x | Docker, declarative config |
| Docker Compose | 2.x | Local development orchestration |
| DynamoDB Local | latest | amazon/dynamodb-local Docker image |
| MinIO | latest | S3-compatible local object storage |
| Papercut SMTP | latest | Local email catch-all for dev |
| SES | v2 | AWS SDK for .NET v3 |
| KMS | | AWS SDK for .NET v3 |
| OpenAPI | Microsoft.AspNetCore.OpenApi (.NET 9 built-in) | Per-service spec generation |
| Kiota | 1.x (Microsoft.OpenApi.Kiota) | SDK generation from OpenAPI spec (C#, TypeScript, Python, Java, Go) |
| SurveyIQ | (github.com/thomwinans/surveyiq) | External embeddable question graph engine — same stack (C#/.NET, DynamoDB, Pulumi, Kong, Kiota). SNAPP integrates as a tenant via API key + C# SDK + webhooks. |
| Spectral | 6.x | OpenAPI spec linting in CI |
| oasdiff | 1.x | Breaking change detection in CI |
| JWT | System.IdentityModel.Tokens.Jwt | RS256 signing |
| Markdig | 0.37+ | Markdown → HTML rendering in Blazor |
| Polly | 8.x | HTTP retry policies |
| ULID | Ulid.Net | Sortable unique IDs (timestamp-prefixed) |
| Testing | xUnit 2.x, bUnit 1.x, Playwright .NET | |
| CI/CD | GitHub Actions | dotnet build/test/publish workflows |

## Appendix B: Solution Structure

```
snapp/
├── src/
│   ├── Snapp.Shared/                    # Shared library (DTOs, models, interfaces, encryption)
│   ├── Snapp.Client/                    # Blazor WASM
│   ├── Snapp.Service.Auth/              # Auth Lambda + Docker
│   ├── Snapp.Service.User/              # User Profile Lambda + Docker
│   ├── Snapp.Service.Network/           # Network + Membership Lambda + Docker
│   ├── Snapp.Service.Content/           # Feed + Discussion Lambda + Docker
│   ├── Snapp.Service.Intelligence/      # Data contribution + Benchmark + Valuation Lambda + Docker
│   ├── Snapp.Service.Transaction/       # Referral + Reputation + Deal Room Lambda + Docker
│   ├── Snapp.Service.Notification/      # Notification Lambda + Docker
│   ├── Snapp.Service.DigestJob/         # Scheduled digest Lambda + Docker cron
│   ├── Snapp.Service.LinkedIn/          # LinkedIn OAuth + Share Lambda + Docker
│   ├── Snapp.Service.Discord/           # Discord bot (Docker only, long-lived)
│   ├── Snapp.Service.Enrichment/        # Public signal aggregation (scheduled)
│   ├── Snapp.Service.Authorizer/        # API Gateway Authorizer Lambda (prod only)
│   ├── Snapp.Sdk/                       # Auto-generated C# SDK (Kiota output)
│   ├── Snapp.Tools.SpecMerge/           # OpenAPI spec merge tool
│   └── Snapp.Infrastructure/            # Pulumi stacks + Docker Compose + Kong config
│       ├── Pulumi/
│       ├── Kong/
│       ├── Docker/
│       └── Scripts/
├── test/
│   ├── Snapp.Shared.Tests/
│   ├── Snapp.Service.Auth.Tests/
│   ├── Snapp.Service.User.Tests/
│   ├── Snapp.Service.Network.Tests/
│   ├── Snapp.Service.Content.Tests/
│   ├── Snapp.Service.Intelligence.Tests/
│   ├── Snapp.Service.Transaction.Tests/
│   ├── Snapp.Service.Notification.Tests/
│   ├── Snapp.Service.DigestJob.Tests/
│   ├── Snapp.Service.LinkedIn.Tests/
│   ├── Snapp.Sdk.Tests/                 # SDK integration tests
│   ├── Snapp.Client.Tests/              # bUnit tests
│   └── Snapp.E2E.Tests/                # Playwright tests
├── api/
│   └── snapp-api.yaml                   # Merged OpenAPI spec (generated, checked in)
├── snapp.sln
├── Directory.Build.props                # Shared MSBuild properties
├── .github/
│   └── workflows/
│       ├── ci.yml                       # Build + test on PR
│       └── deploy.yml                   # Deploy on merge to main
└── README.md
```
