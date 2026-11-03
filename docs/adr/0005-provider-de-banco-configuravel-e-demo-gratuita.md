# ADR 0005: Provider de banco configurável e proposta de demo gratuita

## Status

Aceito para o provider de banco (implementado). Proposta para a hospedagem gratuita
(não configurada no repositório nem publicada).

## Contexto

A demo pública rodava em Azure: SPA em Static Web Apps e API em App Service. O dono não quer
mais pagar Azure. Em 2026-11-03 a API respondia `/health/live` 200, mas `/health/ready` 503
(banco/Redis inacessíveis), ou seja, a demo já não funcionava.

SQL Server não tem hospedagem gratuita fora do Azure. Há Postgres gratuito (Neon, Supabase,
Render) e hospedagem gratuita de contêiner (Render free web service). Além disso, o refresh
token vai em cookie `SameSite=Strict`: com SPA e API em domínios diferentes (como
`*.azurestaticapps.net` e `*.azurewebsites.net`) o cookie não é enviado e o refresh falha.

## Decisão

1. `Database:Provider` escolhe o banco: `SqlServer` (padrão, alvo principal) ou `PostgreSQL`
   (Npgsql). Cada provider tem as próprias migrations: SQL Server em
   `TenantCore.Infrastructure/Database/Migrations`, PostgreSQL no projeto
   `TenantCore.Infrastructure.Postgres`. O modelo do SQL Server não mudou.
2. Diferenças de provider ficam no `OnModelCreating`: concorrência otimista com `rowversion`
   no SQL Server e com a coluna de sistema `xmin` no PostgreSQL; `nvarchar(max)` só no SQL Server.
3. O SQL Dapper do dashboard é único e portável (identificadores entre aspas duplas, booleano e
   data como parâmetros, `CAST(... AS INT)` nos agregados).
4. A suíte de isolamento contra banco real roda nos dois providers (Testcontainers).
5. Redis passa a ser opcional (cache em memória sem `ConnectionStrings:Redis`) e o número de
   proxies confiáveis (`ForwardedHeaders:ForwardLimit`) é configurável.
6. Deploys para Azure ficam só com `workflow_dispatch`.

Proposta de hospedagem (ver README, "Deploy gratuito (proposta)"): API em Render (plano free,
Dockerfile existente), Postgres no Neon (free), sem Redis, e SPA no Netlify com proxy de `/api/*`
para a API, para que o cookie de refresh seja do mesmo site.

## Consequências

- Duas trilhas de migrations: toda mudança de modelo precisa de `migrations add` nos dois
  projetos (checar com `has-pending-model-changes` em cada um).
- No PostgreSQL a busca por texto (`Contains`) diferencia maiúsculas de minúsculas; no SQL
  Server, com a collation padrão, não. Os handlers baixam só o termo, não a coluna.
- Render free hiberna após inatividade; a primeira requisição pode levar cerca de 1 minuto.
  O frontend mostra uma mensagem específica quando a API não responde.
- Sem Redis, com mais de uma instância da API, o cache de resumo deixa de ser compartilhado.
- Por trás do proxy do Netlify, `ForwardLimit` precisa refletir a cadeia real de proxies; o
  valor correto não foi verificado em ambiente publicado.
