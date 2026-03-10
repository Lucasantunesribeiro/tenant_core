# Runbook

## Purpose

This runbook is for local development, smoke validation, and common operational troubleshooting.

## Standard startup

### Docker Compose

1. Optional: copy `.env.example` to `.env` and adjust secrets for local use.
2. Run `docker compose up --build -d`.
3. Validate:
   - `http://localhost:5000/health/live`
   - `http://localhost:5000/health/ready`
   - `http://localhost:5173/healthz`
   - `http://localhost:16686`

### Manual

1. Ensure SQL Server and Redis are reachable.
2. Start the API:
   - `dotnet run --project backend/src/TenantCore.Api`
3. Start the frontend:
   - `cd frontend`
   - `npm ci`
   - `npm run dev`

## Demo flow

Recommended recruiter demo:

1. Sign in as `admin@acme.test` with tenant `11111111-1111-1111-1111-111111111111`.
2. Show dashboard usage, billing state, and audit history.
3. Create a project, task, client, and user to demonstrate write flows.
4. Change the tenant plan and show billing state updates.
5. Switch to another seeded tenant and show the isolated workspace.
6. Trigger a tenant mismatch manually in Swagger or an API client to show the guardrail.

## Useful commands

### Build and test

- `dotnet build backend/tenant_core.sln -c Release`
- `dotnet test backend/tenant_core.sln -c Release`
- `cd frontend && npm run lint`
- `cd frontend && npm run build`
- `cd frontend && npm test`

### Database

- add migration:
  `dotnet ef migrations add <Name> --project backend/src/TenantCore.Infrastructure/TenantCore.Infrastructure.csproj --startup-project backend/src/TenantCore.Api/TenantCore.Api.csproj --output-dir Database/Migrations`
- update database:
  `dotnet ef database update --project backend/src/TenantCore.Infrastructure/TenantCore.Infrastructure.csproj --startup-project backend/src/TenantCore.Api/TenantCore.Api.csproj`

## Common incidents

### API is down but SQL Server and Redis are up

Check:

- `docker compose logs api`
- connection strings
- signing key configuration
- whether migrations failed during startup

### `/health/ready` is failing

This usually means one of:

- SQL Server not reachable
- Redis not reachable
- startup migration still running

Check:

- `docker compose ps`
- `docker compose logs sqlserver`
- `docker compose logs redis`
- `docker compose logs api`

### Login fails with tenant mismatch

Check:

- the `X-Tenant-Id` header value
- the tenant tied to the authenticated user
- whether the frontend session belongs to a different seeded tenant

### Refresh flow fails

Check:

- whether the refresh token cookie exists
- whether the cookie expired
- whether logout already revoked the token
- whether the tenant header matches the token record

### Dashboard data looks stale

Expected behavior:

- usage and subscription summaries are cached for short periods

Ways to force refresh:

- execute a write that invalidates the key
- wait for the short cache TTL
- restart the stack for a full local reset

## Resetting the environment

Full reset:

1. `docker compose down -v`
2. `docker compose up --build -d`

This recreates SQL Server, Redis, and reseeds the demo data through application startup.

## Observability

### Logs

- API logs: `docker compose logs api`
- SQL Server logs: `docker compose logs sqlserver`
- Redis logs: `docker compose logs redis`
- OTEL collector logs: `docker compose logs otel-collector`

### Traces

Open Jaeger at `http://localhost:16686` and inspect traces for:

- login
- workspace CRUD
- health checks
- Quartz jobs

## Production preparation notes

- replace development secrets
- move secrets into a managed secret store
- terminate TLS in front of the API
- externalize SQL Server backups and restore policy
- add alerting on health endpoints, auth failures, and quota-exceeded states
