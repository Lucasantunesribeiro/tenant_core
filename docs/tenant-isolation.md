# Isolamento entre tenants

Modelo: banco SQL Server compartilhado, isolamento aplicado pela aplicação
(ver [ADR 0001](adr/0001-multi-tenancy-banco-compartilhado.md) e
[ADR 0004](adr/0004-barreira-de-escrita-e-tenant-pelo-claim.md)).

## Como o tenant é resolvido

| Requisição | Fonte do tenant | Papel do header `X-Tenant-Id` |
| --- | --- | --- |
| Autenticada (JWT válido) | claim `tenantId` do JWT | obrigatório e precisa ser igual ao claim; senão `403 tenant_mismatch` |
| Anônima (`/api/auth/login`, `/api/auth/refresh`) | header validado pelo middleware (`HttpContext.Items`) | escolhe o tenant onde as credenciais / o refresh token são procurados |
| Sem `HttpContext` (jobs Quartz, seed) | nenhum (`null`) | — |

Não há resolução por subdomínio. O header nunca sobrescreve o claim: `HttpCurrentSession`
ignora `Items` quando o usuário está autenticado.

## Pontos de aplicação

1. `TenantResolutionMiddleware` (depois de `UseAuthentication`): exige GUID válido em
   `X-Tenant-Id` (`400 tenant_header_required`) e compara com o claim (`403 tenant_mismatch`).
   Rotas excluídas: `/health`, `/swagger`, `/openapi`, `/favicon` e `OPTIONS`.
2. `ICurrentSession`: handlers só obtêm tenant por `GetRequiredTenantId()`, nunca por `HttpContext`.
3. Query filters do EF (`CurrentTenantId != Guid.Empty && x.TenantId == CurrentTenantId`) em
   `User`, `RefreshToken`, `TenantSubscription`, `TenantUsageSnapshot`, `Client`, `Project`,
   `WorkTask` e `AuditLog`. Sem tenant, os filtros retornam vazio (fail-closed). Um teste
   unitário falha se surgir entidade `ITenantOwnedEntity` sem filtro.
4. Barreira de escrita no `SaveChanges`: carimba `TenantId` vazio, rejeita escrita em outro tenant
   e torna `TenantId` imutável (`TenantIsolationViolationException`).
5. Referências recebidas no corpo (`ProjectId`, `ClientId`, `OwnerUserId`, `AssigneeUserId`) são
   validadas por consultas filtradas e respondem 404 quando são de outro tenant.
6. Recurso por id de outro tenant responde 404 (mesma resposta de id inexistente), nunca 403/200.
7. SQL bruto (`TenantDashboardRepository`, Dapper) não passa pelos filtros: cada tabela é filtrada
   por `@TenantId` no próprio SQL, inclusive nos `JOIN`s.
8. Cache: chaves `usage:{tenantId}` e `subscription:{tenantId}`.

## `IgnoreQueryFilters()`

Usado apenas em `UsageSnapshotJob`, `SubscriptionEnforcementJob` e `CleanupJob`, que filtram
`TenantId` explicitamente e não devolvem dados a usuários. Qualquer uso novo exige revisão de
segurança.

## Índices por tenant

- `Users(TenantId, Email)` único; `Projects(TenantId, Code)` único;
  `TenantSubscriptions(TenantId)` único.
- `Tasks(TenantId, ProjectId, Status)`, `Tasks(TenantId, AssigneeUserId)`,
  `AuditLogs(TenantId, OccurredAtUtc)`, `RefreshTokens(TenantId, UserId, ExpiresAtUtc)`.

## Respostas de erro

| Situação | Status | `type` |
| --- | --- | --- |
| Header ausente ou inválido | 400 | `tenant_header_required` |
| Header diferente do claim, ou JWT sem claim de tenant | 403 | `tenant_mismatch` |
| Id de outro tenant (projeto, tarefa, cliente, usuário) | 404 | `*_not_found` |
| Login com e-mail de outro tenant | 401 | `invalid_credentials` |
| Refresh token de outro tenant | 401 | `invalid_refresh_token` |

## Testes

- `TenantCore.UnitTests`: `TenantIsolationQueryFilterTests`, `TenantWriteGuardTests`,
  `HttpCurrentSessionTests`, `TenantResolutionMiddlewareTests`.
- `TenantCore.IntegrationTests/CrossTenantAccessTests` (API em memória, EF InMemory): tenant A
  não lê, altera nem apaga dados do tenant B; header forjado não troca tenant; `tenantId` no corpo
  é ignorado; cache separado por tenant.
- `TenantCore.IntegrationTests/SqlServer` (Testcontainers, SQL Server 2022 real, migrations
  aplicadas): dashboard Dapper, filtros traduzidos para T-SQL, barreira de escrita, índices únicos
  por tenant e `UsageSnapshotJob`.

## Limitações conhecidas

- A barreira de escrita não detecta FK cruzado (id de outro tenant numa entidade do tenant
  correto); isso depende da validação nos handlers.
- O FK `Tasks.ProjectId` tem `ON DELETE CASCADE` sem incluir `TenantId`: dados inconsistentes
  gravados antes desta correção ainda seriam apagados em cascata pelo outro tenant.
- Não há Row-Level Security no SQL Server: o isolamento é só da aplicação.
