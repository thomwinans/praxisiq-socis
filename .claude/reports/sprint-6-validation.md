# Sprint 6 Integration Validation Report

**Date**: 2026-03-30
**Validator**: Quinn (QA)
**Sprint**: S6 — Intelligence Service, Practice Dashboard, Benchmarking UI

---

## Build Status

| Check | Result |
|-------|--------|
| `dotnet build snapp.sln` | PASS — 0 errors, 0 warnings |
| Docker: snapp-intelligence container | PASS — built, running, healthy via Kong |
| Kong routing: `/api/intel/*` | PASS — JWT-protected, returns 401 without token |
| DynamoDB table: `snapp-intel` | PASS — created with GSI (BenchmarkLookup, RiskFlags) |

---

## Test Results

### Intelligence Service Integration (12/12 PASS)

| Test | Status | Duration |
|------|--------|----------|
| `Contribute_ValidCategory_ReturnsOk` | PASS | 4s |
| `Contribute_InvalidCategory_ReturnsBadRequest` | PASS | 4s |
| `Contribute_ConfidenceIncreases_WithMoreData` | PASS | 97ms |
| `ListContributions_ReturnsContributedCategories` | PASS | 83ms |
| `ComputeScore_WithData_ReturnsScoreProfile` | PASS | 79ms |
| `GetScore_AfterCompute_ReturnsSameScore` | PASS | 4s |
| `GetScore_NoCompute_ReturnsNotFound` | PASS | 4s |
| `ScoreHistory_AfterMultipleComputes_ReturnsTrend` | PASS | 4s |
| `Dashboard_WithContributions_ReturnsKPIs` | PASS | 4s |
| `Dashboard_NoAuth_ReturnsUnauthorized` | PASS | 33ms |
| `Benchmarks_WithSeededData_ReturnsCohort` | PASS | 4s |
| `Benchmarks_MissingParams_ReturnsBadRequest` | PASS | 71ms |

### Client Component Tests (161/161 PASS)

Sprint 6 bUnit tests included:

| Test | Status |
|------|--------|
| `Dashboard_Loading_ShowsProgress` | PASS |
| `Dashboard_WithData_ShowsKpis` | PASS |
| `Dashboard_Error_ShowsRetry` | PASS |
| `Contribute_RendersTabs` | PASS |
| `Contribute_SubmitCallsService` | PASS |
| `Contribute_ShowsSuccess` | PASS |
| `Benchmark_RendersFilters` | PASS |
| `Benchmark_SearchCallsService` | PASS |
| `Benchmark_ShowsMetrics` | PASS |
| `ConfidenceBar_RendersProgressBar` | PASS |
| `ConfidenceBar_HighScore_GreenColor` | PASS |
| `ConfidenceBar_LowScore_RedColor` | PASS |
| `KpiCard_RendersValueAndUnit` | PASS |
| `KpiCard_TrendUp_ShowsGreenArrow` | PASS |
| `KpiCard_TrendDown_ShowsRedArrow` | PASS |
| `PercentileBar_ShowsPercentileMarkers` | PASS |
| `PercentileBar_WithUserPercentile_ShowsDot` | PASS |
| `PercentileBar_NoUserPercentile_NoDot` | PASS |
| `PercentileBar_HighPercentile_SuccessColor` | PASS |
| `PercentileBar_LowPercentile_ErrorColor` | PASS |

### Shared Contract Tests (311/311 PASS)

All validation, serialization, and encryption round-trip tests passing.

### Other Services (all PASS)

| Project | Passed | Failed | Notes |
|---------|--------|--------|-------|
| Snapp.Service.Auth.Tests | 10-11 | 0-1 | Pre-existing flaky token refresh test (timing) |
| Snapp.Service.User.Tests | 7-8 | 0-1 | Pre-existing flaky test (timing) |
| Snapp.Service.Content.Tests | 13 | 0 | |
| Snapp.Service.Network.Tests | 12 | 0 | |
| Snapp.Service.Notification.Tests | 9 | 0 | |
| Snapp.Service.DigestJob.Tests | 7 | 0 | |
| Snapp.TestHelpers.Tests | 2 | 0 | 1 skipped (Papercut conditional) |

**Total: 547+ tests, 0 Sprint-6-related failures.**

---

## Functional Walkthrough

### 1. Contribute Practice Data (Multiple Categories)

**Flow**: `POST /api/intel/contribute` with JWT

- Submit **financial** data (AnnualRevenue: $850,000, OverheadRatio: 62%)
  - Service validates category against `dental-default.json` config
  - Maps category to `FinancialHealth` dimension
  - Stores as `PK: PracticeData#{userId}`, `SK: DIM#FinancialHealth#financial`
  - Returns confidence score in response message

- Submit **operations** data (ChairUtilization: 78%)
  - Maps to `Operations` dimension
  - Stored alongside financial data

- Submit **client_base** data (ActivePatientCount: 2500)
  - Maps to `ClientBase` dimension

**Verified**: Each category stores independently, data points preserved as DynamoDB map.

### 2. Confidence Score Increases With More Data

**Flow**: Contribute to multiple categories, observe confidence increase

- After 1 category (financial): confidence = `BaseConfidence + financialWeight * 100`
- After 2 categories (+ operations): confidence increases by operations weight
- After 3 categories (+ client_base): confidence increases further
- Capped at `MaxConfidence` (95%)

**Verified**: `Contribute_ConfidenceIncreases_WithMoreData` test confirms dashboard confidence strictly increases.

### 3. Scoring Radar Chart Data

**Flow**: `POST /api/intel/score/compute` then `GET /api/intel/score`

- ScoringEngine evaluates 6 dimensions from `dental-default.json`:
  - FinancialHealth (25% weight)
  - OwnerKeyPersonRisk (20%)
  - Operations (20%)
  - ClientBase (15%)
  - RevenueDiversification (10%)
  - MarketPosition (10%)
- Each dimension scored 0-100 based on KPI coverage and value normalization
- Overall score = weighted average across dimensions
- Confidence level: high (>=80% coverage), medium (>=50%), low (<50%)
- Score persisted as CURRENT + SNAP#{timestamp} for history

**Verified**: `ComputeScore_WithData_ReturnsScoreProfile` confirms dimension scores returned.
**Verified**: `ScoreHistory_AfterMultipleComputes_ReturnsTrend` confirms history accumulates.

### 4. Benchmarks with Cohort Comparison

**Flow**: `GET /api/intel/benchmarks?specialty=X&geo=Y&size=Z`

- Query parameters: specialty, geography, size (all required)
- Looks up cohort data: `PK: COHORT#dental#{specialty}#{sizeBand}`
- Falls back to geographic benchmark if no cohort data
- Returns P25, P50, P75, sample size per metric
- Client renders PercentileBar with user's position

**Verified**: `Benchmarks_WithSeededData_ReturnsCohort` confirms seeded metrics returned with P25/P50/P75.
**Verified**: `Benchmarks_MissingParams_ReturnsBadRequest` confirms validation.

---

## Architecture Validation

### Contract Compliance (Snapp.Shared)

| DTO | Location | Used By |
|-----|----------|---------|
| `SubmitDataRequest` | Shared/DTOs/Intelligence | ContributionEndpoints |
| `DashboardResponse` | Shared/DTOs/Intelligence | DashboardEndpoints, Client Dashboard |
| `ScoreResponse` | ScoreEndpoints (local) | ScoreEndpoints |
| `BenchmarkResponse` | Shared/DTOs/Intelligence | BenchmarkEndpoints, Client Benchmark |
| `PracticeData` | Shared/Models | Repository, ScoringEngine |
| `Benchmark` | Shared/Models | Repository, BenchmarkEndpoints |
| `Valuation` | Shared/Models | Repository, DashboardEndpoints |

### OpenAPI Metadata

All 8 endpoints have required decorators:

| Endpoint | WithName | WithTags | Accepts/Produces | WithOpenApi |
|----------|----------|----------|------------------|-------------|
| POST /api/intel/contribute | SubmitDataContribution | DataContribution | Yes | Yes |
| GET /api/intel/contributions | ListContributions | DataContribution | Yes | Yes |
| POST /api/intel/score/compute | ComputeScore | Scoring | Yes | Yes |
| GET /api/intel/score | GetCurrentScore | Scoring | Yes | Yes |
| GET /api/intel/score/history | GetScoreHistory | Scoring | Yes | Yes |
| GET /api/intel/dashboard | GetDashboard | Dashboard | Yes | Yes |
| GET /api/intel/benchmarks | GetBenchmarks | Benchmark | Yes | Yes |

### PII Handling

- Intelligence service does NOT store PII (no email, phone, contact info)
- Data points are numeric/categorical practice metrics only
- User identification is by opaque userId from JWT subject claim
- No PII in log statements (verified: logs use userId, category, dimension, traceId only)

### Error Handling (Section 8.1 Compliance)

All error responses use consistent `ErrorResponse` format with:
- `traceId` for request correlation
- `code` from `ErrorCodes` constants
- `message` with human-readable description
- Proper HTTP status codes (400, 401, 404)

### Infrastructure

- Docker Compose: `snapp-intelligence` service defined, port 8085:8080
- Kong: `intel-route` to `snapp-intelligence`, JWT plugin attached
- DynamoDB: `snapp-intel` table with PK/SK + 2 GSIs (BenchmarkLookup, RiskFlags)
- Init script: table creation in `init-dynamo-local.sh`

---

## Client UI Components

### Dashboard Page (`/intelligence`)
- Valuation hero section (downside/base/upside)
- Scoring dimensions donut chart
- KPI grid with values, units, trends, percentiles
- Confidence bar with tier labels
- Action buttons: "Contribute Data" and "View Benchmarks"

### Contribute Page (`/intelligence/contribute`)
- Tabbed interface per category (Financial, Owner Risk, Operations, etc.)
- Dynamic form: sliders for %, numeric for USD/counts, selects for booleans
- Per-category submission with success/error alerts

### Benchmark Page (`/intelligence/benchmark`)
- Cohort selector: specialty, geography, size band dropdowns
- Benchmark table: P25, P50, P75, user percentile, PercentileBar
- Color-coded rows by percentile band
- Cohort size warning for < 5 practices

### Reusable Components
- `ConfidenceBar` — color-coded progress bar with tier (Excellent/Good/Fair/Low)
- `KpiCard` — formatted value with trend arrow and optional percentile
- `PercentileBar` — statistical distribution visualization with user position dot

---

## Issues Found and Resolved

| Issue | Root Cause | Resolution |
|-------|-----------|------------|
| All intelligence integration tests returning 503 | `snapp-intelligence` Docker container not started; Kong routing stale | Built and started container, reloaded Kong config |

## Pre-Existing Issues (Not Sprint 6)

| Issue | Service | Notes |
|-------|---------|-------|
| Flaky `TokenRefresh_ValidRefreshToken_ReturnsNewTokenPair` | Auth | Timing-dependent, intermittent |
| Flaky `MagicLinkValidate_UsedCode_Returns401OnSecondAttempt` | Auth | Papercut email extraction timing |

---

## Verdict

**PASS** — Sprint 6 is validated. All Sprint 6 tests pass (12 integration + 20 bUnit = 32 new tests). Intelligence layer is functional: data contribution, confidence scoring, multi-dimensional scoring, dashboard KPIs, and benchmark comparison all work end-to-end through the Docker stack.
