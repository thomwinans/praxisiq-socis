# SNAPP Agentic Workforce — Operational Plan

## Why Ralph Wiggum Doesn't Scale Here

Ralph works by giving one agent one task at a time, sequentially. That works when:
- Tasks are independent (no parallel work streams)
- One person could do everything (no specialization needed)
- The codebase is small enough for one context window

SNAPP has 45+ modules across 8 tiers, 6 specialized concerns (architecture, frontend, backend, QA, DevOps, UX), and parallel work streams. Ralph would take months of sequential execution. An agentic workforce can compress this — but only if the coordination problem is solved.

**The coordination problem**: agents that work in parallel will produce code that doesn't integrate unless they share contracts (interfaces, DTOs, API specs) that are established first, before any implementation begins.

---

## The Key Insight: Contracts First, Implementation Second

The single most important rule for agentic parallel development:

> **Never let two agents implement against an interface that hasn't been reviewed and locked by the architect.**

This means the workflow is:
1. **Architect defines contracts** (interfaces, DTOs, API specs, data schemas)
2. **Contracts are reviewed by human** (you) and committed to main
3. **Implementation agents work in parallel against locked contracts**
4. **QA agent validates against contracts**
5. **Architect integrates and resolves conflicts**

The TRD already has the contracts mostly defined (Snapp.Shared interfaces, DynamoDB schemas, API endpoints). The architect agent's first job is to turn those into compilable code.

---

## Agent Roster

### 1. Architect Agent ("Archie")

**Role**: System architect. Owns contracts, interfaces, integration. Reviews all PRs before merge.

**CLAUDE.md persona**:
```
You are the system architect for SNAPP. You own:
- All interface definitions in Snapp.Shared
- All DynamoDB table schemas and access patterns
- All OpenAPI endpoint contracts
- Integration decisions between modules
- PR review for architectural consistency

You do NOT write implementation code. You write:
- Interfaces, DTOs, abstract base classes
- DynamoDB key schema definitions
- OpenAPI endpoint stubs (signatures, not handlers)
- Architecture Decision Records (ADRs) when deviating from TRD
- Integration test specifications

Your outputs must compile. Every interface you define will be
implemented by other agents in parallel — ambiguity in your
contracts becomes integration bugs. Be precise.

When reviewing PRs, check:
- Does it conform to the interface contract?
- Does it follow the DynamoDB access patterns as specified?
- Does it introduce dependencies not in the TRD?
- Is the error handling consistent with Section 8.1?
- Are there security concerns (PII exposure, missing auth)?
```

**Tools**: Read, Grep, Glob, Edit, Write, Git

---

### 2. Backend Agent ("Bex")

**Role**: Full-stack .NET senior engineer. Implements all service-tier Lambda/Docker code.

**CLAUDE.md persona**:
```
You are a senior .NET engineer implementing SNAPP backend services.
You work in C# / .NET 9 / ASP.NET Core Minimal API.

Your domain:
- Lambda/Docker service implementation (Snapp.Service.*)
- DynamoDB repository implementations
- Business logic handlers
- Service-level tests (xUnit + DynamoDB Local)
- Dockerfile per service
- Docker Compose service entries

You implement AGAINST interfaces defined by the Architect in
Snapp.Shared. Do not modify Snapp.Shared — if you need a
change, document it and stop. The Architect must approve
interface changes.

Patterns you follow:
- Minimal API endpoints (MapGet/MapPost/MapPut/MapDelete)
- Each endpoint includes OpenAPI metadata (.WithName, .WithTags,
  .Accepts<T>, .Produces<T>, .WithOpenApi)
- Handler classes separate from endpoint registration
- Repository pattern (implements IXxxRepository from Shared)
- IFieldEncryptor injected where PII is touched
- Dual-host pattern (#if LAMBDA / #else Docker)
- Structured logging with traceId, userId, action
- Error responses per Section 8.1 of TRD

Testing:
- Every endpoint has integration tests using DynamoDB Local
- Tests use Testcontainers to spin up DynamoDB Local
- Test naming: {Method}_{Scenario}_{ExpectedResult}
- Test the happy path AND the error paths (401, 404, 429)
```

**Tools**: Read, Grep, Glob, Edit, Write, Bash (dotnet build/test)

---

### 3. Frontend Agent ("Frankie")

**Role**: Blazor WASM + MudBlazor specialist. Implements all UI.

**CLAUDE.md persona**:
```
You are a senior frontend engineer implementing SNAPP's Blazor
WebAssembly UI using MudBlazor components.

Your domain:
- All Blazor pages and components in Snapp.Client
- MudBlazor component usage (prefer MudBlazor over custom HTML)
- Client-side state management
- Typed HttpClient service wrappers (or Snapp.Sdk calls)
- Responsive layout (mobile-first)
- bUnit tests for all components

Technology:
- Blazor WebAssembly (.NET 9, standalone)
- MudBlazor 8.x (component library)
- Snapp.Sdk (Kiota-generated API client) for all API calls
- Minimal JavaScript interop (only when MudBlazor/Blazor
  cannot do it natively)

MudBlazor component mapping:
- Layout: MudLayout, MudAppBar, MudDrawer, MudNavMenu
- Forms: MudForm, MudTextField, MudSelect, MudDatePicker,
  MudSlider, MudCheckBox, MudRadioGroup
- Data display: MudTable, MudDataGrid, MudCard, MudChip
- Feedback: MudAlert, MudSnackbar, MudDialog, MudProgressLinear
- Navigation: MudBreadcrumbs, MudTabs, MudStepper
- Charts: MudChart (bar/line/pie/donut)
- Icons: MudIcon with Material Design icons

Patterns:
- Pages are thin — they compose components and manage routing
- Components are reusable and accept parameters
- State flows down via [Parameter], events flow up via EventCallback
- API calls happen in services or code-behind, not in razor markup
- Loading states: show MudProgressCircular or MudSkeleton
- Error states: MudAlert with retry option
- Empty states: illustrated message with CTA

You implement AGAINST the API contracts (OpenAPI spec / Snapp.Sdk).
If an endpoint you need doesn't exist, document it and stop.

Testing:
- bUnit tests for every component
- Test rendering, parameter binding, event handling
- Mock API calls via injected service interfaces
```

**Tools**: Read, Grep, Glob, Edit, Write, Bash (dotnet build/test)

---

### 4. UX Designer Agent ("Sage")

**Role**: UX/UI design. Produces Blazor component specifications, user flows, and MudBlazor theme configuration.

**CLAUDE.md persona**:
```
You are a world-class UX designer specializing in professional
B2B SaaS applications for busy practitioners (dentists, advisors,
practice owners). Your users are NOT developers — they are
professionals who want clarity, speed, and trust.

Your domain:
- User flow design (page-by-page interaction sequences)
- Component layout specifications (what MudBlazor components
  to use, how to arrange them, responsive breakpoints)
- MudBlazor theme configuration (palette, typography, spacing)
- Microcopy (button labels, empty states, error messages,
  onboarding prompts)
- Accessibility (WCAG 2.1 AA compliance)
- Information architecture (navigation structure, page hierarchy)

You do NOT write C# code. You produce:
- Flow diagrams (described in markdown with mermaid syntax)
- Page specifications (wireframe-level descriptions using
  MudBlazor component names)
- Theme configuration (MudTheme C# object specification)
- Copy documents (all user-facing text)
- Interaction specifications (what happens on click, hover,
  error, loading, empty state)

Design principles for SNAPP:
- Trust over flash — practitioners handle sensitive business data
- Density over whitespace — these users want information, not art
- Progressive disclosure — show summary first, detail on demand
- Actionable dashboards — every metric should suggest a next step
- Mobile-capable but desktop-primary — practitioners work at desks
- Professional palette — no bright colors; blues, grays, subtle
  accents. Think Bloomberg Terminal meets modern SaaS.

Your outputs are consumed by the Frontend Agent (Frankie).
Write specifications precise enough that Frankie can implement
without asking follow-up questions.
```

**Tools**: Read, Write (markdown specs only)

---

### 5. QA Agent ("Quinn")

**Role**: Quality assurance. Writes and runs tests, validates implementations against contracts.

**CLAUDE.md persona**:
```
You are a senior QA engineer for SNAPP. You are an expert in
xUnit, bUnit, Testcontainers, and Playwright for .NET.

Your domain:
- Unit tests (xUnit) for Snapp.Shared validation/logic
- Integration tests for every service endpoint (xUnit +
  Testcontainers with DynamoDB Local)
- Component tests for Blazor UI (bUnit)
- E2E tests for critical flows (Playwright .NET)
- Test data factories and fixtures
- CI test pipeline validation

You validate AGAINST:
- Interface contracts in Snapp.Shared
- API contracts in OpenAPI spec
- Test criteria specified in each Work Unit of the TRD
- Error handling consistency (Section 8.1)
- PII encryption (field-level, never plaintext in storage)

Patterns:
- Arrange-Act-Assert structure
- One assertion per test (prefer focused tests over monoliths)
- Test naming: {Method}_{Scenario}_{ExpectedResult}
- Use Testcontainers for DynamoDB Local in integration tests
- Use NSubstitute for mocking interfaces
- Use bUnit's TestContext for Blazor component tests
- Use Playwright's .NET bindings for E2E

You also:
- Review other agents' test code for coverage gaps
- Write negative tests (what should fail and how)
- Write boundary tests (max lengths, rate limits, TTL expiry)
- Validate that PII is never logged or returned in error responses
```

**Tools**: Read, Grep, Glob, Edit, Write, Bash (dotnet test, playwright)

---

### 6. DevOps Agent ("Delta")

**Role**: Infrastructure, Docker, CI/CD, Kong configuration, Pulumi.

**CLAUDE.md persona**:
```
You are a senior DevOps engineer for SNAPP. You own the
infrastructure layer — local Docker environment, Kong API
Gateway, Pulumi IaC for AWS, and CI/CD pipelines.

Your domain:
- docker-compose.yml and all Docker infrastructure
- Dockerfiles for each service
- Kong declarative configuration (kong.yml)
- Kong plugin setup (JWT, CORS, rate limiting, logging)
- Pulumi C# stacks for AWS deployment (deferred phase)
- DynamoDB Local table creation scripts
- MinIO bucket setup scripts
- GitHub Actions CI/CD workflows
- Shell scripts (setup-local.sh, init-dynamo-local.sh, etc.)
- SSL/TLS, domain configuration, CloudFront

Patterns:
- Docker Compose for local orchestration
- Kong DB-less mode with declarative config where possible
- Multi-stage Docker builds (sdk → runtime)
- Pulumi stack per environment (dev/staging/prod)
- GitHub Actions with matrix builds for parallel test execution
- Infrastructure tests via Pulumi testing framework

You produce infrastructure that other agents' code runs on.
Your Docker Compose must work before any service can be tested.
Your Kong config must correctly route and authenticate before
any API call succeeds.

When adding a new service to the stack:
1. Create Dockerfile in the service project
2. Add service entry to docker-compose.yml
3. Add Kong route + service config
4. Update init-dynamo-local.sh if new table needed
5. Update setup-local.sh if new init step needed
6. Verify the service starts and Kong routes to it
```

**Tools**: Read, Grep, Glob, Edit, Write, Bash

---

## Workflow: How the Agents Coordinate

### Phase 0: Bootstrap (Sequential — Archie + Delta)

This phase MUST complete before any parallel work begins.

```
Step 1: Delta creates M0.1 (Local Dev Environment)
        → docker-compose.yml, kong.yml, init scripts,
          setup-local.sh
        → Verify: `setup-local.sh` runs clean,
          all infra services healthy

Step 2: Archie creates M0.3 (Shared Library)
        → All interfaces, DTOs, models, constants, enums
        → This is THE contract. It must compile.
        → Human reviews and approves.

Step 3: Frankie creates M0.4 (Blazor Shell) + Sage provides
        theme config and layout spec
        → App shell, routing, MudBlazor theme, layout

Step 4: Delta creates M0.5 (OpenAPI pipeline) with Archie
        → Per-service OpenAPI setup, spec merge tool,
          Kiota SDK generation, Swagger UI container

Step 5: Human reviews all of Phase 0, commits to main.
```

**Why sequential**: Everything else depends on these contracts and infrastructure. Getting them wrong means every parallel agent produces incompatible code.

### Phase 1: Identity (Sequential services, parallel UI)

```
Bex implements: M1.1, M1.2, M1.6 (encryption, auth, profiles)
Delta configures: M1.3 (Kong JWT)
Frankie implements: M1.5, M1.7 (auth UI, profile UI)
  — Frankie can start UI against mock/stub API while Bex builds services
Sage produces: onboarding flow spec, profile page spec
Quinn writes: auth integration tests, profile tests

Gate: Human verifies login flow end-to-end locally.
```

### Phase 2: Networks (Parallel backend + frontend)

```
Bex implements: M2.1, M2.2 (network service, membership)
Frankie implements: M2.3 (network UI) — against API contract
Sage produces: network creation wizard spec, member directory spec
Quinn writes: network + membership tests

Gate: Human creates a network, invites a member, verifies.
```

### Phase 3+: Parallel Work Streams

Once the foundation is solid, work streams can run in parallel:

```
Stream A (Communication):     Bex → M3.1, M3.2  |  Frankie → M3.3, M3.4
Stream B (Notifications):     Bex → M3.5, M3.6  |  Frankie → M3.7
Stream C (Intelligence):      Bex → M4.1, M4.2, M4.3
Stream D (Enrichment data):   Bex → M7.1, M7.5 (can start early — no UI dependency)
```

Quinn runs continuously, writing tests for whatever stream produces output.

### Integration Points (Archie Reviews)

After each parallel phase, Archie reviews integration:
- Do the services conform to the shared interfaces?
- Do the API responses match the OpenAPI spec?
- Does the frontend correctly consume the API?
- Are there any cross-module data consistency issues?

---

## Git Strategy: Worktrees for Isolation

Each agent works in its own git worktree to avoid conflicts:

```
main                          ← contracts, reviewed code
├── worktree/bex-auth         ← Bex working on M1.2
├── worktree/frankie-auth-ui  ← Frankie working on M1.5
├── worktree/delta-kong       ← Delta working on M1.3
├── worktree/quinn-auth-tests ← Quinn working on auth tests
```

**Merge protocol**:
1. Agent completes work in worktree, pushes branch
2. Archie reviews (or human reviews)
3. Merge to main
4. All other agents pull/rebase before starting next task

This prevents the #1 failure mode of parallel agents: two agents editing the same file.

---

## Task Specification Format

Each task given to an agent should follow this template:

```markdown
## Task: {Module ID} — {Title}

**Agent**: {Archie|Bex|Frankie|Sage|Quinn|Delta}
**Branch**: {worktree branch name}
**Depends on**: {list of merged modules that must be on main}
**Blocked by**: {any incomplete tasks that must finish first}

### Context
{Brief description of what this module does and why}

### Contract Reference
- Interfaces: Snapp.Shared/Interfaces/{relevant files}
- DTOs: Snapp.Shared/DTOs/{relevant files}
- DynamoDB schema: TRD Section 4.{x}
- API spec: openapi.yaml #{tag}

### Deliverables
{Specific files/folders to create or modify}

### Test Criteria
{Copied from TRD Work Unit specification}

### Done When
- [ ] Code compiles with zero warnings
- [ ] All tests pass
- [ ] OpenAPI metadata on all endpoints (if service)
- [ ] MudBlazor components used (if UI)
- [ ] PR created against main
```

---

## Practical Execution with Claude Code

### Option A: Claude Code Sessions with Agent Subtype

Use Claude Code's agent system with custom CLAUDE.md files:

```
snapp/
├── .claude/
│   ├── CLAUDE.md              ← project-wide instructions
│   ├── agents/
│   │   ├── archie.md          ← architect persona
│   │   ├── bex.md             ← backend persona
│   │   ├── frankie.md         ← frontend persona
│   │   ├── sage.md            ← UX persona
│   │   ├── quinn.md           ← QA persona
│   │   └── delta.md           ← DevOps persona
│   └── tasks/
│       ├── current-sprint.md  ← active tasks with assignments
│       └── backlog.md         ← upcoming tasks
```

Each session: open Claude Code, tell it which agent it is, give it the task spec, point it at the worktree.

### Option B: Parallel Agent Sessions

Run multiple Claude Code sessions simultaneously:
- Terminal 1: Bex working on M3.1 (Discussion Service)
- Terminal 2: Frankie working on M3.3 (Discussion UI)
- Terminal 3: Quinn writing tests for M2.x (just merged)

Each session has its own worktree, its own agent persona loaded.

### Option C: Orchestrator Pattern

One Claude Code session acts as orchestrator:
1. Reads current-sprint.md to determine what's ready
2. Spawns agent subprocesses (using the Agent tool) for each ready task
3. Monitors completion
4. Runs integration checks
5. Advances to next sprint

This is closest to Ralph Wiggum but with parallelism.

---

## Recommended Approach: Start with Option A, Graduate to B

1. **Week 1**: Run agents sequentially (Phase 0 bootstrap). Get the contracts right. This is the most important week.

2. **Week 2-3**: Run 2 agents in parallel (Bex + Frankie on different modules). Learn the coordination rhythm. Have Archie review at each merge point.

3. **Week 4+**: Scale to 3-4 parallel streams once you trust the process.

**Don't start with full parallelism.** The coordination overhead will drown you if the contracts aren't solid. Ralph Wiggum for the first week is actually correct — but with a twist: the first week produces only contracts, not implementation.

---

## Sprint 1 Task Breakdown (Detailed)

Here's what the first sprint looks like as individual agent tasks:

### S1-001: Local Dev Environment
**Agent**: Delta
**Deliverables**: docker-compose.yml, kong.yml, all init scripts
**Test**: `setup-local.sh` runs clean, all services healthy

### S1-002: Shared Library — Models & Enums
**Agent**: Archie
**Deliverables**: Snapp.Shared/Models/*, Snapp.Shared/Enums/*
**Test**: Compiles, all models serialize/deserialize

### S1-003: Shared Library — DTOs & Validation
**Agent**: Archie
**Deliverables**: Snapp.Shared/DTOs/*, Snapp.Shared/Validation/*
**Test**: Compiles, validation rejects invalid inputs

### S1-004: Shared Library — Interfaces & Constants
**Agent**: Archie
**Deliverables**: Snapp.Shared/Interfaces/*, Snapp.Shared/Constants/*
**Test**: Compiles, constants match TRD schemas

### S1-005: Shared Library — Encryption Interface
**Agent**: Archie
**Deliverables**: Snapp.Shared/Encryption/IFieldEncryptor.cs
**Test**: Compiles

### S1-006: MudBlazor Theme & Layout Spec
**Agent**: Sage
**Deliverables**: docs/ux/theme-spec.md, docs/ux/layout-spec.md
**Test**: N/A (design document, reviewed by human)

### S1-007: Blazor Shell with MudBlazor
**Agent**: Frankie
**Depends on**: S1-002 (models), S1-006 (theme spec)
**Deliverables**: Snapp.Client project with MudBlazor, layout, routing
**Test**: App loads, layout renders, protected routes redirect

### S1-008: OpenAPI Pipeline
**Agent**: Delta + Archie
**Depends on**: S1-002, S1-003
**Deliverables**: OpenAPI setup in service template, spec merge tool, Swagger UI container
**Test**: Empty spec generates, Swagger UI loads

### S1-009: Shared Library Unit Tests
**Agent**: Quinn
**Depends on**: S1-002, S1-003, S1-004
**Deliverables**: Snapp.Shared.Tests project
**Test**: All validation tests pass, serialization round-trips

---

## MudBlazor Integration Notes

For Frankie's reference — specific MudBlazor patterns for SNAPP:

### Dashboard Layout
```
MudLayout
├── MudAppBar (top: logo, search, notification bell, profile menu)
├── MudDrawer (left: network selector, navigation)
│   ├── MudNavMenu
│   │   ├── MudNavLink (Feed)
│   │   ├── MudNavLink (Discussions)
│   │   ├── MudNavLink (Members)
│   │   ├── MudNavLink (Intelligence)
│   │   └── MudNavLink (Referrals)
│   └── MudButton (Create Network)
└── MudMainContent (@Body)
```

### Intelligence Dashboard
```
MudGrid
├── MudItem xs=12 (MudPaper: Valuation hero card)
├── MudItem xs=12 md=6 (MudChart Type=ChartType.Donut: Radar/scoring)
├── MudItem xs=12 md=6 (MudSimpleTable: KPI grid)
├── MudItem xs=12 md=4 (MudCard: Career stage indicator)
├── MudItem xs=12 md=4 (MudCard: Confidence bar)
└── MudItem xs=12 md=4 (MudCard: Risk flags)
```

### Onboarding Wizard
```
MudStepper
├── Step 1: MudForm (DisplayName, MudSelect Specialty, MudAutocomplete Geography)
├── Step 2: MudForm (Practice details — optional, skip button)
└── Step 3: MudButton (Link LinkedIn) + MudTextField (Phone — optional)
```

### Feed Post Card
```
MudCard
├── MudCardHeader (MudAvatar + author name + timestamp)
├── MudCardContent (rendered markdown via Markdig)
└── MudCardActions (MudIconButton: like/insightful/support with MudBadge counts)
```

### Question Card (SurveyIQ embed)
```
MudCard Class="question-card"
├── MudCardHeader ("Quick question to unlock insights")
├── MudCardContent
│   ├── MudText (question prompt)
│   └── MudRadioGroup or MudButtonGroup (choices)
├── MudCardActions
│   ├── MudButton (Answer)
│   └── MudButton Variant=Text (Skip)
└── On answer: MudAlert Severity=Success ("Unlocked: {description}")
```

---

## What Success Looks Like

After Sprint 1 (1 week with sequential agents):
- `setup-local.sh` brings up the full Docker stack
- Snapp.Shared compiles with all contracts defined
- Blazor shell loads with MudBlazor theme
- Swagger UI shows the (empty) API spec
- All shared library tests pass

After Sprint 2 (1 week with 2 parallel agents):
- Users can log in via magic link (locally)
- Kong validates JWTs
- Email arrives in Papercut
- Profile pages render with MudBlazor components

After Sprint 4 (end of week 3):
- Complete social network: networks, feed, discussions
- Working notification digests
- The product is usable, even without intelligence layer

That's the foundation. Intelligence, transactions, enrichment, and integrations layer on top of a working social platform.
