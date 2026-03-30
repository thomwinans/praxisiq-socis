# SNAPP — Project-Wide Instructions

## What This Project Is

SNAPP (Social Networking Application for PraxisIQ) is a web application that combines LinkedIn-like social networking with private "praxisiq networks" (guilds) for owner-operated specialty practices. See SNAPP-TRD.md for the full technical requirements document.

## Technology Stack

- **UI**: Blazor WebAssembly (.NET 9) with MudBlazor component library
- **Backend**: .NET 9 Minimal API services in Docker containers
- **API Gateway**: Kong (local dev), AWS API Gateway (prod)
- **Database**: DynamoDB (DynamoDB Local for dev)
- **Object Storage**: MinIO (local dev), S3 (prod)
- **Email**: Papercut SMTP (local dev), SES (prod)
- **PII Encryption**: AES-256-GCM envelope encryption (local key file for dev, KMS for prod)
- **IaC**: Pulumi (C#)
- **SDK**: Auto-generated via Microsoft Kiota from OpenAPI specs
- **External**: SurveyIQ for question/survey engine

## Solution Structure

```
snapp/
├── src/
│   ├── Snapp.Shared/           # Contracts: DTOs, models, interfaces, constants
│   ├── Snapp.Client/           # Blazor WASM + MudBlazor
│   ├── Snapp.Service.Auth/     # Magic link auth
│   ├── Snapp.Service.User/     # Profile management
│   ├── Snapp.Service.Network/  # Networks + membership
│   ├── Snapp.Service.Content/  # Feed + discussions
│   ├── Snapp.Service.Intelligence/  # Scoring, benchmarks, valuation
│   ├── Snapp.Service.Transaction/   # Referrals, reputation, deals
│   ├── Snapp.Service.Notification/  # Notifications + digest
│   ├── Snapp.Service.LinkedIn/      # LinkedIn integration
│   ├── Snapp.Sdk/              # Kiota-generated C# SDK
│   └── Snapp.Infrastructure/   # Pulumi, Docker, Kong config
├── test/                       # Mirror of src/ with .Tests suffix
└── snapp.sln
```

## Critical Rules

1. **Snapp.Shared is the contract.** Do NOT modify it without architect approval. All services implement against its interfaces.
2. **MudBlazor for all UI.** Prefer MudBlazor components over raw HTML. No custom CSS unless MudBlazor truly cannot do it.
3. **OpenAPI metadata on every endpoint.** Every MapGet/MapPost must include .WithName(), .WithTags(), .Accepts<T>(), .Produces<T>(), .WithOpenApi().
4. **PII is always encrypted.** Email, phone, contact info go through IFieldEncryptor before storage. Never log PII. Never return PII in error responses.
5. **Local-first.** All code must work against Docker local infrastructure (DynamoDB Local, Kong, MinIO, Papercut). No AWS dependencies during development.
6. **Dual-host pattern.** Services run in Docker (dev) and Lambda (prod). Use `#if LAMBDA` conditional compilation in Program.cs.
7. **Test everything.** xUnit for services, bUnit for Blazor, Testcontainers for DynamoDB integration tests.
