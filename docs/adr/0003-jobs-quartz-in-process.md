# ADR 0003: Jobs Quartz.NET dentro do processo da API

## Status

Aceito.

## Contexto

Há trabalho recorrente: snapshot de uso por tenant, cálculo de estado de cota e limpeza de
refresh tokens e snapshots antigos. Esses jobs percorrem todos os tenants, então rodam sem
`HttpContext` e, portanto, sem tenant na sessão.

## Decisão

Quartz.NET hospedado na API (`AddQuartzHostedService`), com `UsageSnapshotJob`,
`SubscriptionEnforcementJob` e `CleanupJob`, agendados por cron (`Jobs:*` na configuração) e
com `[DisallowConcurrentExecution]`.

Sem tenant na sessão, os query filters retornam vazio (fail-closed). Por isso os jobs usam
`IgnoreQueryFilters()` e filtram `TenantId` explicitamente em cada consulta. Esses três jobs são
os únicos usos permitidos de `IgnoreQueryFilters()`.

## Consequências

- Positivas: setup local e de CI simples; jobs revisáveis no mesmo repositório.
- Negativas: web e jobs dividem o processo; com várias instâncias da API, cada uma agenda os
  jobs (não há store clusterizado do Quartz configurado). Um job que esqueça o filtro explícito por
  `TenantId` mistura dados de tenants; `SqlServerTenantIsolationTests` cobre o `UsageSnapshotJob`.

## Alternativas rejeitadas

- Timers em `IHostedService`: sem identidade de job nem agendamento cron.
- Worker separado desde já: infraestrutura extra sem ganho no estágio atual.
