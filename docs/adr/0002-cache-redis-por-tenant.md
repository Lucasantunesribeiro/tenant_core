# ADR 0002: Cache Redis por tenant para uso e assinatura

## Status

Aceito.

## Contexto

As telas de dashboard e billing são lidas com frequência e agregam várias tabelas. Em banco
compartilhado, uma chave de cache sem tenant entregaria o resumo de um tenant a outro.

## Decisão

Cache distribuído (Redis via `IDistributedCache`) apenas para dois resumos, sempre com o tenant
na chave, obtido de `ICurrentSession.GetRequiredTenantId()`:

- `usage:{tenantId}` — `GetUsageDashboardQuery`, TTL 2 min.
- `subscription:{tenantId}` — `GetSubscriptionQuery`, TTL 5 min.

Invalidação: handlers de escrita que mudam contagens/plano removem a chave do tenant;
`UsageSnapshotJob` e `SubscriptionEnforcementJob` removem as chaves de cada tenant processado.

## Consequências

- Positivas: leituras repetidas mais rápidas; superfície de cache pequena e auditável.
  `CrossTenantAccessTests.CachedTenantResponses_ShouldBeKeyedPerTenant` usa um cache compartilhado
  e prova que cada tenant recebe o próprio resumo.
- Negativas: leitura pode ficar defasada até o TTL; todo handler novo que altera contagem precisa
  lembrar de invalidar. Hoje a invalidação ocorre antes do `SaveChanges`, então uma leitura
  concorrente pode repopular o valor antigo até o TTL expirar.

## Alternativas rejeitadas

- Sem cache: consultas agregadas repetidas a cada carregamento.
- Cache de listagens paginadas: invalidação muito mais complexa para pouco ganho.
