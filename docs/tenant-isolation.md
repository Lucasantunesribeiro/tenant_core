# Tenant Isolation

## Isolation model

`tenant_core` uses a shared database, application-enforced multi-tenancy model.

The active tenant is resolved from:

- `X-Tenant-Id` request header
- `tenantId` claim inside the authenticated JWT

Both must agree for authenticated requests.

## Enforcement points

### 1. Request middleware

`TenantResolutionMiddleware` is the first hard tenant boundary.

It:

- requires a valid `X-Tenant-Id` header on almost every request
- stores the tenant in the current request context
- compares the header against the authenticated user's `tenantId` claim
- returns a ProblemDetails error on missing or mismatched tenant values

Health and Swagger routes are excluded so tooling remains usable.

### 2. Current session abstraction

Application handlers read tenant context through `ICurrentSession`. This keeps tenant access explicit in use cases and avoids hidden ambient dependencies inside handlers.

### 3. EF Core global query filters

Tenant-owned entities apply `HasQueryFilter(x => CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId)`.

This covers:

- `User`
- `RefreshToken`
- `TenantSubscription`
- `TenantUsageSnapshot`
- `Client`
- `Project`
- `WorkTask`
- `AuditLog`

Result: routine queries cannot accidentally cross tenant boundaries unless a developer explicitly opts out with `IgnoreQueryFilters()`.

### 4. Explicit cross-tenant code is limited

`IgnoreQueryFilters()` is only used in operational jobs and internal aggregation scenarios, such as:

- usage snapshot generation
- subscription enforcement
- cleanup of expired records

These code paths enumerate tenants intentionally and never return cross-tenant data to end users.

## Data model support

Every tenant-owned row includes `TenantId`, and the database model adds tenant-oriented indexes such as:

- `Users(TenantId, Email)` unique
- `Projects(TenantId, Code)` unique
- `Tasks(TenantId, ProjectId, Status)`
- `AuditLogs(TenantId, OccurredAtUtc)`

This improves both safety and query selectivity.

## Failure modes

### Missing tenant header

Returns:

- status `400`
- code `tenant_header_required`

### Header and JWT claim mismatch

Returns:

- status `403`
- code `tenant_mismatch`

### Missing tenant subscription

Returns:

- status `404`
- code `subscription_missing`

## Test coverage

Isolation is covered by:

- unit tests around query filter behavior
- integration tests proving tenant mismatch rejection
- integration tests proving one tenant cannot access another tenant's workspace data
- integration tests proving plan rules are evaluated against the current tenant only

## Operational guidance

- Never trust frontend tenant values without the backend middleware check.
- Keep tenant-aware indexes aligned with access patterns.
- Treat any new `IgnoreQueryFilters()` usage as a security review item.
- If cross-tenant administration is ever introduced, build it as a separate privileged path instead of weakening the default tenant model.
