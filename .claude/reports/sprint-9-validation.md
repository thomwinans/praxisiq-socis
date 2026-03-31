# Sprint 9 Validation Report
**Validator**: Quinn (QA)
**Date**: 2026-03-30
**Sprint**: S9 — Enrichment Data (Registry + Benchmarks + Listings)

## Sprint 9 Deliverables

| WU | Agent | Description | Status |
|----|-------|-------------|--------|
| S9-001 | bex | Association Benchmark Data Loader | PASS |
| S9-002 | bex | Business Listing Integration | PASS |
| S9-003 | quinn | Sprint 9 integration validation | This report |

## Build Status

- **Solution**: `snapp.sln` — **0 warnings, 0 errors**
- **All projects compile**: 19 src projects, 17 test projects

## Test Results

| Test Project | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| Snapp.Shared.Tests | 329 | 0 | 0 | 329 |
| Snapp.Client.Tests | 175 | 0 | 0 | 175 |
| Snapp.Service.Enrichment.Tests | 40 | 0 | 0 | 40 |
| Snapp.Service.Intelligence.Tests | 36 | 0 | 0 | 36 |
| Snapp.Service.Content.Tests | 13 | 0 | 0 | 13 |
| Snapp.Service.Network.Tests | 12 | 0 | 0 | 12 |
| Snapp.Service.Auth.Tests | 11 | 0 | 0 | 11 |
| Snapp.Service.Notification.Tests | 9 | 0 | 0 | 9 |
| Snapp.Service.User.Tests | 8 | 0 | 0 | 8 |
| Snapp.Service.DigestJob.Tests | 7 | 0 | 0 | 7 |
| Snapp.TestHelpers.Tests | 2 | 0 | 1 | 3 |
| **TOTAL** | **642** | **0** | **1** | **643** |

> Sprint 8 ended at 611 tests. Sprint 9 adds 32 new tests (+5.2%).

## Bug Found and Fixed During Validation

`EnrichmentRepository.CountSignalsByPrefixAsync()` used a single-page DynamoDB Scan to count items by PK prefix. When the `snapp-intel` table grew beyond one scan page (after full pipeline populates providers + markets + benchmarks + regulatory + listings), COHORT# and MKT# items could land on later pages, returning a count of 0.

**Fix**: Added scan pagination loop that accumulates `response.Count` across all pages until `LastEvaluatedKey` is empty. This corrected 2 failing integration tests:
- `BenchmarkIntegrationTests.RunAsync_LoadsBenchmarksAndRegulatoryData`
- `EnrichmentIntegrationTests.RunAsync_CreatesMarketProfileItems`

**File**: `src/Snapp.Service.Enrichment/Repositories/EnrichmentRepository.cs:213`

## S9-001: Association Benchmark Data Loader (M7.3)

### What Was Validated

| Requirement | Evidence | Status |
|---|---|---|
| P25/P50/P75 quartile benchmarks | 4 fixture files: revenue, compensation, overhead, production metrics | PASS |
| Multiple specialties | 7 dental specialties | PASS |
| Size band stratification | 3 bands (small/medium/large) for General Practice | PASS |
| Geographic levels | National + 3 state-level (AZ, TX, CA) | PASS |
| COHORT# PK design | `COHORT#dental#{specialty}#{sizeBand}` with `METRIC#{name}` SK | PASS |
| BENCH# PK design | `BENCH#dental#{state}#{level}` with `METRIC#{name}` SK | PASS |
| Scoring calibration readiness | P50 retrieval for revenue ($780K) and profit margin (28%) | PASS |
| Idempotent loading | Re-running produces same count, no duplicates | PASS |
| CMS regulatory data (M7.2) | 14 synthetic CMS records with NPI, prescribing, demographics | PASS |
| Regulatory signal storage | `SIGNAL#{npi}/REGULATORY#{ulid}` items | PASS |

### Benchmark Data Summary

| Fixture File | Records | Key Metrics |
|---|---|---|
| revenue-quartiles.json | 14 | GP small P50 = $780K, large P50 = $2.1M |
| compensation-ranges.json | 12 | Owner P50 = $250K, Hygienist P50 = $78K |
| overhead-ratios.json | 15 | GP small overhead P50 = 62%, profit margin = 28% |
| production-metrics.json | 13 | GP small production P50 = $700K, collection rate = 97% |

### Test Coverage (14 tests)

- Cohort item creation, revenue quartile values, state-level benchmarks
- Compensation, overhead, and production data presence
- Specialty coverage (Orthodontics, Oral Surgery)
- Idempotent loading (no duplicates on re-run)
- Regulatory signal creation, attribute mapping, multi-provider batching
- Full pipeline integration (RunAsync loads benchmarks + regulatory)
- Scoring calibration query simulation
- Multi-size-band support, complete calibration set validation

## S9-002: Business Listing Integration (M7.4)

### What Was Validated

| Requirement | Evidence | Status |
|---|---|---|
| Multi-pass matching | 3-pass pipeline: phone, address, name+city fuzzy | PASS |
| Phone exact match (confidence 1.0) | Verified with known provider match | PASS |
| Address exact match (confidence 0.8) | Verified with known provider match | PASS |
| Name+city fuzzy match (confidence 0.5-0.7) | Verified with known provider match | PASS |
| Strong online reputation flag | Rating >= 4.5 AND ReviewCount >= 100 | PASS |
| Weak reputation not flagged | Rating < 4.5 OR ReviewCount < 100 | PASS |
| No duplicate provider matches | Deduplication verified | PASS |
| No duplicate listing matches | Deduplication verified | PASS |
| LISTING# signal storage | PlaceId, Rating, ReviewCount, MatchMethod, MatchConfidence | PASS |
| Fixture data quality | 75 records across 9 states, 7 specialties | PASS |

### Matching Pipeline Results

| Pass | Method | Confidence | Coverage |
|---|---|---|---|
| 1 | Phone exact | 1.0 | 28 providers with phone numbers |
| 2 | Address exact | 0.8 | Remaining unmatched by normalized address |
| 3 | Name+city fuzzy | 0.5-0.7 | Last name / full name / specialty keyword |

### Test Coverage (18 tests)

- All three match passes with confidence scores and known providers
- Strong/weak reputation flagging
- DynamoDB signal persistence with expected attributes
- Multi-provider listing signal creation
- Full pipeline integration
- Deduplication: no provider or listing matched twice

## Full Enrichment Pipeline Integration

| Step | Description | Items Created | PK Pattern |
|---|---|---|---|
| 1 | Provider registry enrichment | 75 SIGNAL# items | `SIGNAL#{npi}` |
| 2 | Geographic and economic market data | 150+ items | `MKT#{fips}` |
| 3 | Association and industry benchmarks | 54 metrics | `COHORT#` / `BENCH#` |
| 4 | Regulatory and claims data | 14 signals | `SIGNAL#{npi}/REGULATORY#` |
| 5 | Business listing integration | ~60 signals | `SIGNAL#{npi}/LISTING#` |

### Intelligence Layer Enrichment

| Intelligence Feature | Data Source |
|---|---|
| Benchmark dashboard (P25/P50/P75 bars) | COHORT# items from Step 3 |
| Market profile cards | MKT# items from Step 2 |
| Confidence scoring | Provider signals from Step 1 |
| Reputation signals | LISTING# items from Step 5 |
| Risk indicators | REGULATORY# from Step 4 |

### Data Volume: ~350+ items loaded to snapp-intel

## Architecture Compliance

| Rule | Status |
|---|---|
| Snapp.Shared is the contract (KeyPrefixes, TableNames) | PASS |
| Local-first (DynamoDB Local on port 8042) | PASS |
| Fixture-based data sources with interface abstractions | PASS |
| Production API stubs ready (GooglePlacesClient) | PASS |
| No PII in enrichment data | PASS |
| 40 enrichment tests covering all loaders and pipeline | PASS |

## Sprint 9 Test Growth

| Sprint | Total Tests | Delta |
|---|---|---|
| S7 | 537 | -- |
| S8 | 611 | +74 |
| S9 | 643 | +32 |

## Known Limitations

1. Fuzzy matching uses string containment; production should use Levenshtein distance
2. Google Places is stub only; production requires API key and rate limiting
3. CMS data is synthetic; production requires NPPES/CMS API integration
4. State-level benchmarks cover revenue only; other metrics national-only
5. Scoring engine integration pending future sprint

## Verdict

**PASS** -- Sprint 9 enrichment pipeline fully operational. All 3 work units validated. Bug in scan pagination discovered and fixed. 643 tests pass, 0 failures.
