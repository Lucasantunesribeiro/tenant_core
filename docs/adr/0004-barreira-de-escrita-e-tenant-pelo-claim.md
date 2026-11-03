# ADR 0004: Tenant pelo claim do JWT e barreira de escrita no SaveChanges

## Status

Aceito (2026-11).

## Contexto

A auditoria de isolamento encontrou três lacunas:

1. `HttpCurrentSession.TenantId` lia primeiro `HttpContext.Items` (preenchido com o header
   `X-Tenant-Id`) e só depois o claim. A proteção contra header forjado dependia apenas do
   middleware rodar antes de qualquer acesso a dados.
2. Query filters do EF protegem leitura, não escrita. Nada impedia um handler de inserir,
   alterar ou apagar linha de outro tenant, nem de trocar o `TenantId` de uma entidade.
3. `UpdateTaskCommand` e `UpdateProjectCommand` aceitavam `ProjectId`, `AssigneeUserId`,
   `ClientId` e `OwnerUserId` do corpo sem validar o tenant. O FK aceitava ids de outro tenant:
   uma tarefa da Acme podia ser movida para um projeto da Globex e seria apagada em cascata quando
   a Globex apagasse o projeto.

## Decisão

- Usuário autenticado: `HttpCurrentSession.TenantId` vem SOMENTE do claim `tenantId`. Sem claim
  válido, o tenant é nulo (os filtros retornam vazio). O valor de `Items` só vale para requisições
  anônimas (login e refresh), e só quando o middleware o validou.
- `TenantCoreDbContext.SaveChanges/SaveChangesAsync` aplica uma barreira:
  - `Added`: `TenantId` vazio recebe o tenant da sessão; `TenantId` de outro tenant é rejeitado.
  - `Modified`: `TenantId` é imutável; só se altera linha do tenant da sessão.
  - `Deleted`: só se apaga linha do tenant da sessão.
  - Sem tenant na sessão (jobs e seed): valem só a imutabilidade e a proibição de `TenantId`
    vazio, porque esses fluxos são cross-tenant de propósito.
  - Violação lança `TenantIsolationViolationException`, que o `GlobalExceptionMiddleware`
    converte em 500 genérico.
- Referências recebidas no corpo são validadas por consultas filtradas por tenant, e respondem
  404 quando o id é de outro tenant (mesma resposta de id inexistente, sem confirmar existência).

## Consequências

- Positivas: um header forjado não troca o tenant mesmo se o middleware for removido ou
  reordenado; um bug num handler futuro falha alto em vez de vazar escrita.
- Negativas: a barreira não detecta FK cruzado (um `ProjectId` de outro tenant numa entidade do
  tenant correto). Esse caso continua dependendo da validação no handler. Os testes de regressão
  ficam em `CrossTenantAccessTests`.
- Testes que semeiam dados de vários tenants precisam de um contexto sem tenant, igual ao seed.

## Alternativas rejeitadas

- Interceptor `ISaveChangesInterceptor` separado: mesmo efeito com mais registro de DI; o override
  no próprio contexto garante que nenhuma configuração esqueça a barreira.
- Row-Level Security do SQL Server: defesa forte, mas exige `SESSION_CONTEXT` por conexão e
  mudança de migrations. Fica como evolução possível.
