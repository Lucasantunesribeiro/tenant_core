# ADR 002: Redis caching for usage and subscription summaries

## Status

Accepted

## Context

Dashboard and billing views are read frequently, derive from multiple tables, and are good candidates for short-lived caching. The platform also already depends on Redis for production-shaped infrastructure.

## Decision

Use Redis distributed cache for tenant-scoped summary reads:

- `usage:{tenantId}` with a short TTL
- `subscription:{tenantId}` with a short TTL

Invalidate cache entries:

- immediately after writes that affect tenant usage or plan state
- after Quartz jobs recompute usage or quota enforcement state

## Consequences

### Positive

- faster repeated dashboard and billing reads
- demonstrates realistic cache invalidation patterns
- keeps the cached surface area small and understandable

### Negative

- cached reads can be stale for a short period
- mutation handlers must remember to invalidate related keys

## Rejected alternatives

### No cache

Rejected because it under-represents operational maturity for a SaaS dashboard with repeated summary queries.

### Cache all list endpoints

Rejected because list filtering and pagination would increase invalidation complexity for limited practical gain in this portfolio project.
