# BLOCKERS

## S2-007: Profile and Onboarding UI

### 1. OnboardingRequest DTO missing practice fields

**Status:** Blocked on backend update

The onboarding spec (docs/ux/onboarding-spec.md) calls for Step 2 practice fields:
- Practice name
- Role (Owner, Partner, Associate, Employee, Consultant, Other)
- Practice size (Solo, 2-5, 6-15, 16-50, 51-200, 200+)
- Years in practice (< 1, 1-3, 4-7, 8-15, 16-25, 25+)
- Practice type (Solo private, Group private, DSO/MSO, Hospital/Institutional, Other)

The current `OnboardingRequest` DTO in Snapp.Shared only supports: DisplayName, Specialty, Geography, Email, Phone, LinkedInProfileUrl. Practice data fields are collected in the UI but **not persisted** until the DTO and `POST /api/users/me/onboard` endpoint are updated.

**Resolution:** Add practice fields to `OnboardingRequest` and update `UserEndpoints.OnboardAsync` to store them (likely as a `PracticeData` item in DynamoDB).

### 2. LinkedIn OAuth not wired

**Status:** Deferred to S13

The Connect step shows a disabled "Connect LinkedIn" button. LinkedIn OAuth integration is planned for Sprint 13 (S13). The UI placeholder is in place.

### 3. City/Metro autocomplete uses static sample data

**Status:** Needs geo database or API

The city autocomplete in Step 1 uses a hardcoded sample list of 50 US cities. A proper implementation needs either a geo database lookup or a third-party API for city suggestions by state.
