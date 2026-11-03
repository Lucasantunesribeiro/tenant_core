# ADR 0001: Multi-tenancy em banco compartilhado com header + claim

## Status

Aceito.

## Contexto

O produto é um SaaS B2B em que várias empresas (tenants) usam o mesmo backend. Um banco por
cliente traria custo e orquestração (migrations, provisionamento, conexões) desproporcionais para
o tamanho do projeto. A fronteira de tenant precisa ser explícita no código e testável.

## Decisão

Um único SQL Server. Toda linha tenant-owned guarda `TenantId` (classe base `TenantOwnedEntity`).
O isolamento é aplicado em camadas:

- `TenantResolutionMiddleware`: exige `X-Tenant-Id` válido em toda rota (exceto `/health`,
  `/swagger`, `/openapi`, `/favicon` e `OPTIONS`); com usuário autenticado, o header precisa ser
  igual ao claim `tenantId` do JWT, senão `403 tenant_mismatch`.
- `ICurrentSession` (`HttpCurrentSession`): única fonte de tenant para os handlers.
- Query filters globais do EF Core em todas as entidades tenant-owned.
- Barreira de escrita no `SaveChanges` (ver ADR 0004).

## Consequências

- Positivas: desenvolvimento local e CI simples; relatórios e jobs operacionais diretos; fronteira
  visível no middleware e nas consultas.
- Negativas: todo `IgnoreQueryFilters()` vira ponto sensível de segurança; índices precisam começar
  por `TenantId`; SQL bruto (Dapper) não passa pelos filtros e precisa filtrar `TenantId`
  manualmente; administração cross-tenant exige caminho próprio.

## Alternativas rejeitadas

- Banco por tenant: complexidade operacional e custo.
- Tenant só pelo JWT (sem header): o header torna o contexto explícito no frontend, nos logs e
  nos testes. O header é obrigatório, mas não decide o tenant de usuário autenticado: o claim
  decide e o header precisa concordar.
