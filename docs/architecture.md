# Architecture

## System overview

`tenant_core` is a monorepo fullstack SaaS composed of:

- `backend/src/TenantCore.Api`: ASP.NET Core Web API, controllers, middleware, Swagger, auth, health checks
- `backend/src/TenantCore.Application`: use cases, MediatR handlers, validation, plan enforcement, RBAC-aware business logic
- `backend/src/TenantCore.Domain`: aggregate roots, value-oriented entities, enums, domain rules
- `backend/src/TenantCore.Infrastructure`: EF Core, SQL Server mappings, Redis cache, Quartz jobs, password hashing, JWT generation, seed data
- `frontend`: React + Vite admin dashboard for tenant operations

The platform uses a shared SQL Server database with tenant scoping enforced in middleware, session claims, and EF Core query filters.

## Backend request lifecycle

1. `CorrelationIdMiddleware` assigns or propagates a correlation identifier.
2. `SecurityHeadersMiddleware` adds defensive response headers.
3. `Authentication` validates the JWT when present.
4. `TenantResolutionMiddleware` requires `X-Tenant-Id`, parses the tenant, and blocks header/claim mismatches.
5. ASP.NET Core rate limiting applies either the auth or API policy.
6. Controllers call MediatR commands and queries.
7. Handlers use `ICurrentSession` and `ITenantCoreDbContext` so tenant-aware rules stay inside application code.
8. `GlobalExceptionMiddleware` converts domain and validation failures into RFC 7807-style ProblemDetails responses.

## Layered design

### Domain

Core entities:

- `Tenant`
- `User`
- `RefreshToken`
- `SubscriptionPlan`
- `TenantSubscription`
- `TenantUsageSnapshot`
- `Project`
- `WorkTask`
- `Client`
- `AuditLog`

Each tenant-owned aggregate stores `TenantId`. Concurrency-sensitive aggregates include a SQL Server row version.

### Application

The application layer owns:

- login, refresh, logout, and current-user flows
- tenant settings and subscription changes
- CRUD and filtered listing for projects, tasks, clients, and users
- plan limit enforcement
- audit log writes
- cache invalidation after writes

### Infrastructure

Infrastructure wires:

- EF Core SQL Server context and mappings
- Redis distributed cache
- Quartz jobs
- ASP.NET Identity password hasher
- JWT creation and refresh token hashing
- health checks
- demo seed data and migrations

## Frontend architecture

The React app is a desktop-first admin console with:

- protected routing via `ProtectedRoute`
- a shared `AppShell`
- TanStack Query for server state
- Axios interceptors for bearer tokens, `X-Tenant-Id`, and automatic refresh on `401`
- feature pages for dashboard, projects, tasks, clients, users, billing, audit logs, and settings

The frontend never decides tenant identity by itself. It uses the tenant from the authenticated session and sends it in the `X-Tenant-Id` header for every API call.

## Data and consistency model

### Strong consistency

SQL Server remains the source of truth for:

- identity data
- subscription state
- tenant settings
- workspace entities
- audit logs
- usage snapshots

### Cached reads

Redis is used for short-lived cached reads where latency matters and stale data is acceptable for a few minutes:

- `usage:{tenantId}` for usage dashboard responses
- `subscription:{tenantId}` for subscription summary responses

Writes remove affected cache keys immediately. Quartz jobs also clear cache keys after recomputation.

## Scheduled processing

Three Quartz jobs run in-process with the API service:

- `UsageSnapshotJob`: aggregates counts for active users, projects, tasks, and clients per tenant
- `SubscriptionEnforcementJob`: computes `Healthy`, `NearLimit`, and `Exceeded` quota state
- `CleanupJob`: deletes expired refresh tokens and old usage snapshots

These jobs intentionally use `IgnoreQueryFilters()` because they are cross-tenant operational processes.

## Observability

Operational signals include:

- Serilog JSON logs
- correlation IDs
- OpenTelemetry traces and metrics
- EF Core, ASP.NET Core, HttpClient, and Quartz instrumentation
- `/health/live`
- `/health/ready`
- Jaeger and an OTEL collector in the local Docker stack

## Deployment-ready path

The codebase is intentionally shaped for a straightforward container deployment.

Recommended AWS topology:

- React frontend container behind an Application Load Balancer
- ASP.NET Core API container behind the same ALB, using path or host-based routing
- ECS Fargate tasks for `web` and `api`
- RDS SQL Server for persistent storage
- ElastiCache Redis for distributed caching
- OTEL collector plus CloudWatch or another trace backend

Why this maps cleanly:

- the application already runs as separate containers
- the API is stateless except for SQL Server and Redis
- health endpoints are available for load balancer checks
- environment-based configuration is already wired

Official AWS references used for this deployment path:

- ECS with ALB: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/alb.html
- ECS on Fargate: https://docs.aws.amazon.com/AmazonECS/latest/developerguide/AWS_Fargate.html
