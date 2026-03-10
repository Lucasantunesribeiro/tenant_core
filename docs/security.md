# Security

## Authentication and session lifecycle

The platform uses short-lived JWT access tokens plus rotating refresh tokens.

### Access tokens

JWT claims include:

- `sub`
- `email`
- `role`
- `tenantId`
- `jti`

`jti` makes token instances unique, which improves rotation semantics and traceability.

### Refresh tokens

Refresh tokens are:

- generated with cryptographically secure randomness
- stored in the database only as SHA-256 hashes
- rotated on refresh
- revoked on logout
- delivered to the browser via an HTTP-only cookie

Cookie settings:

- `HttpOnly = true`
- `SameSite = Lax`
- `Secure = Request.IsHttps`

## Authorization

Policies:

- `TenantMember`
- `ManagerOrAdmin`
- `AdminOnly`

Role expectations:

- `Admin`: full tenant administration, billing, settings, audit visibility
- `Manager`: project, task, and client management inside the tenant
- `User`: authenticated member access with narrower write permissions

## Request hardening

### Validation and error handling

- FluentValidation handles DTO and command validation
- domain and business failures return ProblemDetails payloads
- internal stack traces are not exposed to API consumers

### Rate limiting

- Auth endpoints: fixed window, `5` requests per minute per remote IP
- Authenticated API routes: sliding window, `120` requests per minute per authenticated principal or remote IP

### Security headers

The API adds:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy` disabling camera, microphone, and geolocation
- a conservative `Content-Security-Policy`

### CORS

CORS is explicitly configured for the frontend origin rather than left open globally.

## Credential handling

- Passwords use ASP.NET Core `PasswordHasher<TUser>`
- refresh tokens are hashed before persistence
- JWT signing key is environment configurable

Production note:

- rotate the signing key outside source control
- switch refresh cookies to `Secure = true` behind HTTPS
- use a secrets manager instead of static environment files

## Auditability

Sensitive actions are auditable:

- login
- logout
- user creation
- role changes
- tenant settings updates
- subscription plan changes
- project, task, and client mutations

Audit records store:

- actor user
- tenant
- action
- entity type
- entity id
- correlation id
- metadata JSON
- timestamp

## Observability as a security aid

Structured logs, traces, and correlation IDs improve incident response by making it easier to answer:

- which tenant was affected
- which user performed the action
- which request path triggered the issue
- whether a quota or authorization rule was involved

## Production hardening checklist

- replace development passwords and signing keys
- enable HTTPS termination in front of the API
- store secrets in a managed secret store
- restrict CORS origins to real frontend domains
- add WAF and IP reputation controls for public auth endpoints
- review any new admin or cross-tenant features under threat modeling
