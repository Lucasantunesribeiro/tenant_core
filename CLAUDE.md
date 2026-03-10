# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

`tenant_core` is a fullstack multi-tenant B2B SaaS monorepo demonstrating enterprise .NET + React patterns. It uses a shared SQL Server database with application-enforced tenant isolation.

## Commands

### Backend (run from `backend/`)

```bash
dotnet restore tenant_core.sln
dotnet build tenant_core.sln -c Release
dotnet test tenant_core.sln -c Release
dotnet test tenant_core.sln -c Release --filter "FullyQualifiedName~ClassName"   # single test class
dotnet run --project src/TenantCore.Api
```

### Frontend (run from `frontend/`)

```bash
npm ci
npm run dev
npm run lint
npm run build
npm test                  # vitest run (single pass)
npm run test:watch        # vitest watch
```

### Full stack

```bash
cp .env.example .env
docker compose up --build -d
```

Local endpoints: frontend `http://localhost:5173`, API `http://localhost:5000`, Swagger `http://localhost:5000/swagger`, Jaeger `http://localhost:16686`.

Demo password for all seeded accounts: `Passw0rd!`

## Backend architecture

### Project layers

- **TenantCore.Api** — controllers, middleware, program wiring (auth, CORS, rate limiting, OTEL, health checks at `/health/live` and `/health/ready`)
- **TenantCore.Application** — MediatR handlers (CQRS: `Commands/` for writes, `Queries/` for reads per feature), `ValidationBehavior` pipeline, `IAuditService`, `IPlanLimitService`, `ICurrentSession`
- **TenantCore.Domain** — aggregate roots (`Tenant`, `User`, `RefreshToken`, `Project`, `WorkTask`, `Client`, `TenantSubscription`, `TenantUsageSnapshot`, `AuditLog`), enums, base classes (`Entity` → `AuditableEntity` → `TenantOwnedEntity`)
- **TenantCore.Infrastructure** — EF Core + SQL Server, Redis cache, Quartz jobs, JWT service, password hashing, migrations, seed data

### Request pipeline order (Program.cs)

`GlobalExceptionMiddleware` → `CorrelationIdMiddleware` → Serilog request logging → `SecurityHeadersMiddleware` → CORS → Authentication → **`TenantResolutionMiddleware`** → Rate limiter → Authorization → Controllers

### Tenant isolation (critical)

Every authenticated request must carry `X-Tenant-Id`. `TenantResolutionMiddleware` validates the header and ensures it matches the `tenantId` JWT claim — mismatches return 403 `tenant_mismatch`. EF Core global query filters on all tenant-owned entities enforce the boundary at the data layer. `IgnoreQueryFilters()` is reserved exclusively for the three Quartz background jobs that operate cross-tenant.

### Authorization policies

- `TenantMember` — any authenticated user
- `ManagerOrAdmin` — `Manager` or `Admin` role
- `AdminOnly` — `Admin` role only

### Background jobs (Quartz.NET, in-process)

- `UsageSnapshotJob` — aggregates active entity counts per tenant
- `SubscriptionEnforcementJob` — computes quota health state (`Healthy` / `NearLimit` / `Exceeded`)
- `CleanupJob` — deletes expired refresh tokens and old snapshots

### Redis cache keys

- `usage:{tenantId}` — usage dashboard response
- `subscription:{tenantId}` — subscription summary response

Writes invalidate affected keys immediately; jobs also clear keys after recomputation.

### Auth lifecycle

JWT access tokens (short-lived) carry `sub`, `email`, `role`, `tenantId`, `jti`. Refresh tokens are stored as SHA-256 hashes, delivered via `HttpOnly` cookie, rotated on refresh, and revoked on logout.

## Frontend architecture

React 18 + Vite + TypeScript admin dashboard (`frontend/src/`):

- **`lib/http.ts`** — Axios instance with request interceptors (adds `Bearer` token + `X-Tenant-Id` header) and response interceptors (auto-refresh on 401 with deduplication)
- **`lib/api.ts`** — typed API client; TanStack Query hooks wrap all API methods
- **`lib/session-store.ts`** — in-memory session state
- **`features/auth/`** — login page, `ProtectedRoute`, `useSession` hook
- **`routes/app-router.tsx`** — route tree with protected and public routes
- **`features/`** — one folder per domain: `dashboard`, `projects`, `tasks`, `clients`, `users`, `billing`, `audit`, `settings`
- **`components/`** — shared layout (`app-shell.tsx`) and UI primitives (`ui/primitives.tsx`)
- **`app/providers.tsx`** — TanStack Query + React Router providers

The frontend reads tenant identity from the authenticated session and never independently asserts it.

## Testing

### Backend

- `TenantCore.UnitTests` — covers JWT service, plan limit logic, role guards, query filter isolation
- `TenantCore.IntegrationTests` — end-to-end API tests covering auth flows, tenant isolation enforcement, workspace boundaries, plan limits, and audit writes

Integration tests use a real SQL Server test database. Test factory is in `tests/TenantCore.IntegrationTests/Testing/`.

### Frontend

Vitest + Testing Library. Test files live alongside feature code (e.g., `login-page.test.tsx`, `protected-route.test.tsx`).

## Git workflow

**Após cada tarefa concluída, crie um commit e envie para o repositório remoto:**

```bash
# Adicione apenas os arquivos relevantes (nunca use git add -A cegamente)
git add <arquivos alterados>

# Mensagem de commit: título conciso (≤ 72 chars) + corpo opcional
git commit -m "$(cat <<'EOF'
tipo(escopo): descrição curta do que foi feito

Detalhes adicionais se necessário. Explique o "porquê",
não apenas o "o quê".

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"

# Push para o repositório remoto
git push origin main
```

**Repositório:** https://github.com/Lucasantunesribeiro/tenant_core

**Regras:**
- Commits atômicos: uma task = um commit (ou mais se justificado)
- Nunca commitar `.env`, segredos ou arquivos de build (`bin/`, `dist/`, `node_modules/`)
- Prefixos: `feat`, `fix`, `refactor`, `style`, `test`, `docs`, `chore`
- Sempre verificar `git status` antes de adicionar arquivos

## Key conventions

- All new tenant-owned entities must inherit `TenantOwnedEntity` (includes `TenantId`) and have a global EF Core query filter registered in `TenantCoreDbContext`.
- New `IgnoreQueryFilters()` usages require a security review — they bypass the tenant boundary.
- Application handlers access tenant context only through `ICurrentSession`, not `HttpContext`.
- Domain failures throw `AppException`; `GlobalExceptionMiddleware` maps it to ProblemDetails.
- Audit writes go through `IAuditService` in application handlers for all sensitive mutations.
- Plan limit checks go through `IPlanLimitService` before writes that count against quotas.
- New frontend API calls go in `lib/api.ts` as TanStack Query hooks; never call `http.ts` directly from components.
