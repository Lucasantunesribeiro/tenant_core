# tenant_core

`tenant_core` is a production-shaped multi-tenant B2B SaaS built to showcase market-relevant .NET and React engineering skills. It demonstrates tenant isolation, RBAC, plan enforcement, refresh token rotation, background jobs, Redis caching, structured observability, Dockerized local infrastructure, and CI workflows in a single coherent monorepo.

This is intentionally not a toy CRUD sample. The platform models how an operations-focused workspace SaaS behaves when multiple companies share the same backend safely and each company needs its own users, projects, tasks, clients, subscription state, usage tracking, and audit trail.

## What it demonstrates

- .NET 9 Web API with Controllers, MediatR, FluentValidation, ProblemDetails, Swagger, Quartz.NET, Serilog, OpenTelemetry, EF Core, SQL Server, Redis, JWT, and refresh token rotation
- React 18 + Vite + TypeScript admin dashboard with TanStack Query, React Router, role-aware routes, typed API access, protected flows, and enterprise-style UX
- Shared-database multi-tenancy enforced through `X-Tenant-Id`, JWT tenant claims, EF Core query filters, and integration tests
- Plan-based SaaS restrictions with `Free`, `Pro`, and `Business` limits enforced in application rules
- Auditability and operational visibility through audit logs, health endpoints, correlation IDs, OTEL traces, and structured logs

## Product scope

Each tenant workspace can:

- manage users and roles
- manage projects, tasks, and clients
- review billing plan and usage
- trigger simulated plan changes
- inspect audit logs
- update tenant settings

## Tech stack

### Backend

- .NET 9
- ASP.NET Core Web API
- MediatR
- FluentValidation
- EF Core + SQL Server
- Redis
- Quartz.NET
- Serilog
- OpenTelemetry
- JWT + rotating refresh tokens

### Frontend

- React 18
- TypeScript
- Vite
- Tailwind CSS
- TanStack Query
- React Router
- Axios

### DevOps

- Docker Compose
- SQL Server container
- Redis container
- OpenTelemetry Collector
- Jaeger
- GitHub Actions

## Repository tree

```text
tenant_core/
├─ backend/
│  ├─ Dockerfile
│  ├─ tenant_core.sln
│  ├─ src/
│  │  ├─ TenantCore.Api/
│  │  ├─ TenantCore.Application/
│  │  ├─ TenantCore.Domain/
│  │  └─ TenantCore.Infrastructure/
│  └─ tests/
│     ├─ TenantCore.UnitTests/
│     └─ TenantCore.IntegrationTests/
├─ frontend/
│  ├─ Dockerfile
│  ├─ nginx.conf
│  └─ src/
├─ docs/
│  ├─ architecture.md
│  ├─ tenant-isolation.md
│  ├─ security.md
│  ├─ runbook.md
│  └─ adr-00x-*.md
├─ infra/
│  └─ otel/
├─ docker-compose.yml
└─ .github/workflows/
```

## Seeded demo access

All seeded demo accounts use password `Passw0rd!`.

| Tenant | Tenant ID | Accounts |
| --- | --- | --- |
| Acme Operations | `11111111-1111-1111-1111-111111111111` | `admin@acme.test`, `manager@acme.test`, `user@acme.test` |
| Globex Advisory | `22222222-2222-2222-2222-222222222222` | `admin@globex.test` |

## Local run

### Option A: Docker Compose

1. Copy `.env.example` to `.env` if you want to override defaults.
2. Run `docker compose up --build -d`.
3. Open:
   - Frontend: `http://localhost:5173`
   - API Swagger: `http://localhost:5000/swagger`
   - API readiness: `http://localhost:5000/health/ready`
   - Jaeger: `http://localhost:16686`

### Option B: Manual development

1. Start SQL Server and Redis locally.
2. Run the API from `backend`:
   - `dotnet restore tenant_core.sln`
   - `dotnet run --project src/TenantCore.Api`
3. Run the frontend from `frontend`:
   - `npm ci`
   - `npm run dev`

The frontend defaults to `http://localhost:5000` for the API through `VITE_API_URL`.

## Quality gates

### Backend

- `dotnet build backend/tenant_core.sln -c Release`
- `dotnet test backend/tenant_core.sln -c Release`

### Frontend

- `npm run lint`
- `npm run build`
- `npm test`

### Full stack

- `docker compose build`
- `docker compose up -d`

## Key implementation choices

- Tenant resolution is mandatory for almost every request and enforced before business logic runs.
- JWT access tokens carry `sub`, `email`, `role`, and `tenantId`.
- Refresh tokens are stored as hashes, rotated on refresh, and revoked on logout.
- Plan limits are enforced in application services, not only in the UI.
- Redis caches subscription and usage dashboard responses and is invalidated on writes and scheduled recomputation.
- Quartz jobs maintain usage snapshots, quota warning state, and cleanup tasks.

## Deployment preparation

The repository includes a local container stack, CI workflows, migrations, and observability wiring so the app can move into a cloud deployment path without redesigning the codebase.

Recommended AWS target:

- ECS Fargate services for `api` and `web`
- Application Load Balancer in front of the services
- Amazon RDS for SQL Server
- ElastiCache for Redis
- OTEL collector sidecar or dedicated collector service

Useful official AWS references:

- ECS with Application Load Balancer: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/alb.html
- ECS on AWS Fargate: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/AWS_Fargate.html

## Documentation map

- [Architecture](docs/architecture.md)
- [Tenant isolation](docs/tenant-isolation.md)
- [Security](docs/security.md)
- [Runbook](docs/runbook.md)
- [ADR 001: Multi-tenancy](docs/adr-001-multi-tenancy.md)
- [ADR 002: Caching](docs/adr-002-caching.md)
- [ADR 003: Background jobs](docs/adr-003-background-jobs.md)

## Why this project is recruiter-relevant

This repository is optimized to make enterprise backend capabilities obvious during review:

- clear multi-tenant boundary enforcement
- real auth lifecycle and RBAC
- business rules beyond CRUD
- test coverage around isolation and quota failures
- operational maturity through health checks, logs, traces, caching, and jobs
- a frontend that feels like an actual admin product instead of a disconnected demo screen
