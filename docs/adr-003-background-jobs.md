# ADR 003: In-process Quartz jobs for operational maintenance

## Status

Accepted

## Context

The platform needs recurring operational work:

- usage aggregation
- quota warning computation
- cleanup of expired refresh tokens and stale snapshots

These tasks should be visible in the codebase and runnable locally without introducing separate worker infrastructure too early.

## Decision

Use Quartz.NET hosted inside the API process with dedicated job classes:

- `UsageSnapshotJob`
- `SubscriptionEnforcementJob`
- `CleanupJob`

Each job is scheduled by cron configuration and instrumented with logging and OpenTelemetry activity spans.

## Consequences

### Positive

- simple local and CI setup
- jobs are easy to review in the same repository and deployment unit
- enough realism to demonstrate background processing patterns

### Negative

- web and job concerns share the same process
- at larger scale, a separate worker service would likely be preferable

## Rejected alternatives

### Hosted service timers only

Rejected because Quartz gives explicit scheduling, job identity, and better production migration options.

### Separate worker service immediately

Rejected because it would add operational weight and duplicate infrastructure for limited additional value in this project stage.
