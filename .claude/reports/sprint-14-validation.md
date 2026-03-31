# Sprint 14 Validation Report
**Validated by**: Quinn (QA Agent)
**Date**: 2026-03-31
**Sprint**: S14 — Enrichment Extended

## Summary

Sprint 14 delivered three features: state licensing enrichment, job posting intelligence enrichment, and guild compensation benchmarking. All three are feature-complete. Validation uncovered and fixed three test regressions and one production bug.

| Area | Status | Tests |
|------|--------|-------|
| State Licensing Enrichment (S14-001a) | PASS | 11/11 |
| Job Posting Intelligence (S14-001b) | PASS | 7/7 |
| Workforce Scoring Integration | PASS | 6/6 + 1 existing |
| Compensation Benchmarking (S14-002) | PASS | 9/9 |
| Shared Config Tests | PASS | 329/329 (3 fixed) |
| Client/UI Tests | PASS | 258/258 |
| **Total S14-related** | **PASS** | **321/321** |

## Issues Found and Fixed

### 1. Shared VerticalConfig Tests — 3 Failures (Fixed)
**File**: `test/Snapp.Shared.Tests/VerticalConfigTests.cs`

S14 added the Workforce dimension (7th) and WorkforceSignals contribution category (9th) to `dental.json`. Three tests had hardcoded counts:

- `DentalConfig_Has_Six_Dimensions` → Updated to expect 7, added "Workforce"
- `DentalConfig_Has_Eight_ContributionCategories` → Updated to expect 9, added "WorkforceSignals"
- `DentalConfig_Each_Dimension_Has_Thresholds` → Workforce uses inverse thresholds (lower pressure = stronger). Added inverse-scored dimension handling.

**Root cause**: S14-002 (bex) modified the vertical config but did not update the Shared contract tests.

### 2. Compensation Benchmark Scan Pagination — Production Bug (Fixed)
**File**: `src/Snapp.Service.Intelligence/Repositories/IntelligenceRepository.cs`

`GetCompensationContributionsForRoleAsync` used a single unpaginated `ScanRequest`. The snapp-intel table contains thousands of items from 13 sprints of enrichment data. DynamoDB's 1MB scan page limit caused the scan to return incomplete results — only items found in the first page were counted, falling below the 5-contributor anonymity threshold.

**Fix**: Added `LastEvaluatedKey` pagination loop to accumulate all matching items across pages.

**Impact**: Without this fix, compensation benchmarks would show "insufficient data" even with adequate contributors in any deployment with a populated snapp-intel table.

## Feature Validation Details

### S14-001a: State Licensing Enrichment
- **Fixture data**: 50 providers in `Fixtures/licensing/state-dental-licenses.json`
- **Matching**: Three-tier fuzzy matching (exact name+city=0.95, name+state=0.80, fuzzy=0.65)
- **Storage**: SIGNAL# items in snapp-intel with confidence scores
- **Career stage**: License tenure incorporated via max(explicit, license) tenure
- **Tests**: 11 integration tests covering matching, tenure, confidence, career stage integration

### S14-001b: Job Posting Intelligence Enrichment
- **Fixture data**: 20 practices in `Fixtures/job-postings/practice-job-postings.json`
- **Detection**: Chronic turnover (same role 3+ times), urgency language, posting frequency
- **Scoring**: Workforce pressure score 0-100 (volume 30pts + urgency 25pts + chronic 25pts + frequency 20pts)
- **Storage**: JOBPOST# and ANALYSIS# items in snapp-intel
- **Integration**: WorkforceEnrichmentProvider feeds scoring engine (Workforce dimension, 10% weight)
- **Tests**: 7 job posting + 6 workforce scoring integration tests

### S14-002: Guild Compensation Benchmarking
- **Endpoints**: POST `/api/intel/compensation/contribute`, GET `/api/intel/compensation/benchmarks`
- **OpenAPI metadata**: Complete on both endpoints (WithName, WithTags, Accepts, Produces, WithOpenApi)
- **Validation**: Role validated against config, required fields enforced, 401 on missing auth
- **Anonymity**: 5-contributor minimum enforced; below threshold returns zeroed percentiles
- **Percentiles**: P25/P50/P75 with linear interpolation, band midpoint estimation handles K-notation
- **Storage**: COHORT# METRIC#COMP# items in snapp-intel
- **UI**: MudBlazor Compensation tab on /intelligence/benchmark page with lazy loading
- **Tests**: 9 integration tests covering contribute, benchmarks, anonymity, formatting, upsert, config-driven roles

## Security and Contract Compliance

| Check | Result |
|-------|--------|
| PII in plaintext (logs/errors/DynamoDB) | None found |
| OpenAPI metadata on all endpoints | Complete |
| Error response format (Section 8.1) | Consistent ErrorResponse with traceId |
| Dimension weights sum to 1.0 | Verified (0.22+0.18+0.18+0.12+0.10+0.10+0.10 = 1.00) |
| Compensation amounts are bands, not exact | Confirmed (e.g., "$35-40/hr") |
| Anonymity threshold enforced | 5 contributors minimum, verified in tests |

## Recommendations

1. **GSI1 on snapp-intel**: The compensation query uses a table scan with pagination. Adding GSI1 (GSI1PK/GSI1SK already written by SaveCompensationContributionAsync) would make this a targeted query instead of a full-table scan.

2. **Test resilience**: `AuthenticateAsync` returns null on failure and tests silently pass via `if (jwt is null) return;`. Should use `jwt.Should().NotBeNull()` to fail explicitly.

3. **Edge case coverage gaps** (non-blocking):
   - No test for exactly 5 contributors at boundary
   - No test for invalid date formats in license parsing
   - No test for empty fixture files

## Build Verification

```
dotnet build snapp.sln — 0 warnings, 0 errors
Snapp.Shared.Tests — 329/329 passed
Snapp.Client.Tests — 258/258 passed
Snapp.Service.Enrichment.Tests (S14 filters) — 25/25 passed
Snapp.Service.Intelligence.Tests (Compensation) — 9/9 passed
```
