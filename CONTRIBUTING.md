# Contributing to SNAPP

## Prerequisites

- **Docker Desktop** (with Docker Compose v2)
- **.NET 9 SDK** — [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **AWS CLI v2** — used by DynamoDB Local init scripts
- **Git**

## Quick Start

```bash
# 1. Start everything and run all tests in one command
scripts/start-and-test.sh

# 2. Or set up the local environment manually
scripts/setup-local.sh
```

## Running Tests

### All tests (with infrastructure validation)
```bash
scripts/test-all.sh
```

### All tests including E2E
```bash
scripts/test-all.sh --e2e
```

### Start Docker, run tests, tear down
```bash
scripts/start-and-test.sh --teardown
```

### Infrastructure validation only
```bash
scripts/validate-all.sh
```

### Unit tests only
```bash
dotnet test test/Snapp.Shared.Tests/
dotnet test test/Snapp.TestHelpers.Tests/
```

### Specific service tests
```bash
dotnet test test/Snapp.Service.Auth.Tests/
dotnet test test/Snapp.Service.User.Tests/
```

### Blazor component tests
```bash
dotnet test test/Snapp.Client.Tests/
```

## Test Reports

Test reports are generated automatically in `.claude/reports/` with the naming convention `test-report_{timestamp}.md`. Each report includes pass/fail counts by suite and category.

## Local Environment

### Starting
```bash
scripts/setup-local.sh
```
This will:
1. Generate dev encryption and JWT signing keys
2. Start all Docker Compose services
3. Create DynamoDB Local tables
4. Create MinIO buckets
5. Verify Kong API Gateway routes

### Stopping
```bash
docker compose -f src/Snapp.Infrastructure/Docker/docker-compose.yml down
```

### Full reset (removes data volumes)
```bash
docker compose -f src/Snapp.Infrastructure/Docker/docker-compose.yml down -v
```

## Port Map

| Service | Port | URL |
|---------|------|-----|
| DynamoDB Local | 8042 | http://localhost:8042 |
| Kong Proxy | 8000 | http://localhost:8000 |
| Kong Admin API | 8001 | http://localhost:8001 |
| MinIO API | 9000 | http://localhost:9000 |
| MinIO Console | 9001 | http://localhost:9001 |
| Papercut SMTP | 1025 | `smtp://localhost:1025` |
| Papercut Web UI | 8025 | http://localhost:8025 |
| Swagger UI | 8090 | http://localhost:8090 |

## Useful Links

- **Caught emails:** http://localhost:8025 — Papercut catches all outbound email (magic link tokens, notifications)
- **API docs:** http://localhost:8090 — Swagger UI with all service OpenAPI specs
- **MinIO console:** http://localhost:9001 — Browse uploaded files (login: `minioadmin` / `minioadmin`)
- **Kong routes:** `curl http://localhost:8001/routes` — List all configured API gateway routes

## API Gateway Routes

All API traffic goes through Kong at `http://localhost:8000`:

| Route | Backend Service |
|-------|----------------|
| `/api/auth/*` | Auth service |
| `/api/users/*` | User service |
| `/api/networks/*` | Network service |
| `/api/content/*` | Content service |
| `/api/intel/*` | Intelligence service |
| `/api/tx/*` | Transaction service |
| `/api/notif/*` | Notification service |
| `/api/linkedin/*` | LinkedIn service |

## Project Structure

```
snapp/
├── src/
│   ├── Snapp.Shared/              # Contracts (DO NOT modify without approval)
│   ├── Snapp.Client/              # Blazor WASM + MudBlazor
│   ├── Snapp.Service.Auth/        # Magic link authentication
│   ├── Snapp.Service.User/        # Profile management
│   ├── Snapp.Service.Network/     # Networks + membership
│   ├── Snapp.Service.Content/     # Feed + discussions
│   ├── Snapp.Service.Intelligence/# Scoring, benchmarks
│   ├── Snapp.Service.Transaction/ # Referrals, deals
│   ├── Snapp.Service.Notification/# Notifications + digest
│   ├── Snapp.Service.LinkedIn/    # LinkedIn integration
│   ├── Snapp.Sdk/                 # Kiota-generated SDK
│   └── Snapp.Infrastructure/     # Docker, Kong, Pulumi, scripts
├── test/                          # xUnit test projects (mirror src/)
├── scripts/                       # Top-level orchestration scripts
└── snapp.sln
```
