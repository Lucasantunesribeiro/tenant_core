# ADR 001: Shared-database multi-tenancy with header plus claim enforcement

## Status

Accepted

## Context

The product must demonstrate real multi-tenancy for a B2B SaaS without the operational overhead of one database per customer. It also needs a tenant boundary that is visible to reviewers and easy to test.

## Decision

Use a shared SQL Server database where every tenant-owned row stores `TenantId`, and enforce tenancy through:

- mandatory `X-Tenant-Id` header resolution
- JWT `tenantId` claim comparison for authenticated requests
- EF Core global query filters for tenant-owned entities

## Consequences

### Positive

- simpler local development and CI
- realistic SaaS architecture for many mid-market products
- explicit, testable tenant boundary in middleware and queries
- easier reporting and operational jobs compared with one-database-per-tenant

### Negative

- mistakes around `IgnoreQueryFilters()` become security-sensitive
- tenant-aware indexing is mandatory for performance
- cross-tenant administration needs deliberate design instead of ad hoc queries

## Rejected alternatives

### Database per tenant

Rejected because it adds orchestration complexity, cost, and onboarding overhead that would distract from demonstrating the core enterprise application concerns in this repository.

### Tenant derived only from JWT

Rejected because the frontend and API both need an explicit tenant context for testing, debugging, and operational clarity. The header makes the boundary obvious and independently verifiable.
