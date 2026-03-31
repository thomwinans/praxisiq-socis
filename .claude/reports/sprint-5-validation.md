# Sprint 5 Validation Report

**Date:** 2026-03-30
**Validator:** Quinn (QA)
**Sprint:** S5 — Notifications & Digest
**Work Units:** S5-001 (Notification Service), S5-002 (Digest Job), S5-003 (Notification UI)

## Summary

Sprint 5 **PASSES** validation. All tests pass. One bug found and fixed during validation (empty ImmediateTypes list causing DynamoDB write failure).

## Test Results

| Test Project | Passed | Failed | Skipped | Duration |
|---|---|---|---|---|
| Snapp.Shared.Tests | 311 | 0 | 0 | 140ms |
| Snapp.TestHelpers.Tests | 2 | 0 | 1 | 132ms |
| Snapp.Client.Tests | 116 | 0 | 0 | 849ms |
| Snapp.Service.Auth.Tests | 11 | 0 | 0 | 1s |
| Snapp.Service.User.Tests | 8 | 0 | 0 | 1s |
| Snapp.Service.Network.Tests | 12 | 0 | 0 | 1s |
| Snapp.Service.Content.Tests | 13 | 0 | 0 | 2s |
| Snapp.Service.Notification.Tests | 9 | 0 | 0 | 736ms |
| Snapp.Service.DigestJob.Tests | 7 | 0 | 0 | 1s |
| **TOTAL** | **489** | **0** | **1** | - |

## Bug Found & Fixed

**Issue:** `SavePreferences_ChangesDigestTime_MovesQueueBucket` test failing with `AmazonDynamoDBException: Supplied AttributeValue is empty, must contain exactly one of the supported datatypes`.

**Root Cause:** In `NotificationRepository.SavePreferencesAsync()`, when `ImmediateTypes` was empty, the code created an `AttributeValue` with `L = []` but DynamoDB Local rejected it because the empty list didn't properly signal its type. The AWS SDK requires `IsLSet = true` to be set explicitly for empty lists.

**Fix:** `NotificationRepository.cs:210` — Replaced conditional empty-list handling with `new() { L = immediateTypesJson, IsLSet = true }` which works for both empty and non-empty lists.

**Impact:** This bug would have caused a 500 error on the second `PUT /api/notif/preferences` call when `ImmediateTypes` was empty or omitted. The first call (initial save) would succeed because the preferences row didn't exist yet, so the `oldPrefs is not null` guard prevented the queue-write code path. The second call would fail when trying to update the digest queue bucket.

## S5-001: Notification Service Validation

### Endpoints Verified
- `GET /api/notif` — Returns user notifications in reverse chronological order with UnreadCount
- `POST /api/notif/{notifId}/read` — Marks single notification as read
- `POST /api/notif/read-all` — Batch marks all unread as read, UnreadCount drops to 0
- `GET /api/notif/preferences` — Returns defaults (DigestTime="07:00", Timezone="America/New_York") when no prefs set
- `PUT /api/notif/preferences` — Persists and retrieves correctly; validates HH:mm format (rejects "25:00" with 400)

### Security
- Unauthenticated requests return 401
- JWT-based user isolation (each user sees only their own notifications)

### DynamoDB Integration
- Digest queue bucket entries created/moved when DigestTime changes
- Notification items stored with correct PK/SK pattern (NOTIF#{userId} / EVENT#{timestamp}#{notifId})

## S5-002: Digest Job Validation

### Test Coverage (7 tests)
- Undigested notifications grouped correctly by category (Referrals, Discussions, Network, Intelligence)
- Email sent to Papercut with subject containing "PraxisIQ Daily Digest"
- Email body contains all category headers and notification titles
- Notifications marked as `IsDigested=true` after successful send
- No email sent when zero undigested notifications
- Digest record (DIGEST#{userId} / SENT#{date}) created with count and categories
- No-users-for-hour completes without error
- User with no email is skipped gracefully (notifications NOT marked as digested)

### GroupByCategory Unit Test
- 7 notifications across all types correctly grouped into 4 categories

## S5-003: Notification UI Validation

### NotificationBell (5 bUnit tests)
- Renders MudIconButton
- Shows badge with unread count; hides when count is 0
- Click invokes callback
- RefreshAsync updates count dynamically

### NotificationDrawer (5 bUnit tests)
- Shows "No notifications yet" empty state
- Lists notifications with correct titles
- Includes "Digest Preferences" link to `/notifications/preferences`
- "Mark All Read" button triggers callback and service call
- Mark All Read button renders

### Preferences Page (7 bUnit tests)
- Form renders with "Notification Preferences", "Daily Digest", "Immediate Notifications" sections
- MudTimePicker for digest time
- Switches for immediate notification types (Referral Received, Mentioned in Discussion, Application Decision, Valuation Changed)
- Loads existing preferences from backend
- Save button calls service
- MudAutocomplete for timezone selection

## Contract Compliance

| Requirement | Status |
|---|---|
| OpenAPI metadata on all endpoints | PASS — WithName, WithTags, Produces, WithOpenApi on all 5 endpoints |
| PII encrypted at rest | PASS — Email decrypted only when sending; AES-256-GCM via IFieldEncryptor |
| Error format per Section 8.1 | PASS — ErrorResponse with ErrorDetail (Code, Message, TraceId) |
| Dual-host pattern | PASS — Program.cs has Docker + Lambda conditional compilation |
| MudBlazor UI | PASS — MudIconButton, MudBadge, MudTimePicker, MudAutocomplete, MudSwitch used |
| DynamoDB key schema | PASS — NOTIF#, DQUEUE#, DIGEST# prefixes match TRD spec |

## Notes

- Auth test `MagicLinkValidate_UsedCode_Returns401OnSecondAttempt` is intermittently flaky (known race condition from S4.1-003). Passed on this run.
- E2E tests (Playwright) not run — not part of Sprint 5 scope.
- SDK tests and LinkedIn tests have no test methods (empty projects, expected).
