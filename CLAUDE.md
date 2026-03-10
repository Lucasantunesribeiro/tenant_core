# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository overview

`tenant_core` is a fullstack multi-tenant B2B SaaS monorepo demonstrating enterprise .NET + React patterns. It uses a shared SQL Server database with application-enforced tenant isolation.

## Commands

### Backend (run from `backend/`)

```bash
dotnet restore tenant_core.sln
dotnet build tenant_core.sln -c Release
dotnet test tenant_core.sln -c Release
dotnet test tenant_core.sln -c Release --filter "FullyQualifiedName~ClassName"   # single test class
dotnet run --project src/TenantCore.Api
```

### Frontend (run from `frontend/`)

```bash
npm ci
npm run dev
npm run lint
npm run build
npm test                  # vitest run (single pass)
npm run test:watch        # vitest watch
```

### Full stack

```bash
cp .env.example .env
docker compose up --build -d
```

Local endpoints: frontend `http://localhost:5173`, API `http://localhost:5000`, Swagger `http://localhost:5000/swagger`, Jaeger `http://localhost:16686`.

Demo password for all seeded accounts: `Passw0rd!`

## Backend architecture

### Project layers

- **TenantCore.Api** — controllers, middleware, program wiring (auth, CORS, rate limiting, OTEL, health checks at `/health/live` and `/health/ready`)
- **TenantCore.Application** — MediatR handlers (CQRS: `Commands/` for writes, `Queries/` for reads per feature), `ValidationBehavior` pipeline, `IAuditService`, `IPlanLimitService`, `ICurrentSession`
- **TenantCore.Domain** — aggregate roots (`Tenant`, `User`, `RefreshToken`, `Project`, `WorkTask`, `Client`, `TenantSubscription`, `TenantUsageSnapshot`, `AuditLog`), enums, base classes (`Entity` → `AuditableEntity` → `TenantOwnedEntity`)
- **TenantCore.Infrastructure** — EF Core + SQL Server, Redis cache, Quartz jobs, JWT service, password hashing, migrations, seed data

### Request pipeline order (Program.cs)

`GlobalExceptionMiddleware` → `CorrelationIdMiddleware` → Serilog request logging → `SecurityHeadersMiddleware` → CORS → Authentication → **`TenantResolutionMiddleware`** → Rate limiter → Authorization → Controllers

### Tenant isolation (critical)

Every authenticated request must carry `X-Tenant-Id`. `TenantResolutionMiddleware` validates the header and ensures it matches the `tenantId` JWT claim — mismatches return 403 `tenant_mismatch`. EF Core global query filters on all tenant-owned entities enforce the boundary at the data layer. `IgnoreQueryFilters()` is reserved exclusively for the three Quartz background jobs that operate cross-tenant.

### Authorization policies

- `TenantMember` — any authenticated user
- `ManagerOrAdmin` — `Manager` or `Admin` role
- `AdminOnly` — `Admin` role only

### Background jobs (Quartz.NET, in-process)

- `UsageSnapshotJob` — aggregates active entity counts per tenant
- `SubscriptionEnforcementJob` — computes quota health state (`Healthy` / `NearLimit` / `Exceeded`)
- `CleanupJob` — deletes expired refresh tokens and old snapshots

### Redis cache keys

- `usage:{tenantId}` — usage dashboard response
- `subscription:{tenantId}` — subscription summary response

Writes invalidate affected keys immediately; jobs also clear keys after recomputation.

### Auth lifecycle

JWT access tokens (short-lived) carry `sub`, `email`, `role`, `tenantId`, `jti`. Refresh tokens are stored as SHA-256 hashes, delivered via `HttpOnly` cookie, rotated on refresh, and revoked on logout.

## Frontend architecture

React 18 + Vite + TypeScript admin dashboard (`frontend/src/`):

- **`lib/http.ts`** — Axios instance with request interceptors (adds `Bearer` token + `X-Tenant-Id` header) and response interceptors (auto-refresh on 401 with deduplication)
- **`lib/api.ts`** — typed API client; TanStack Query hooks wrap all API methods
- **`lib/session-store.ts`** — in-memory session state
- **`features/auth/`** — login page, `ProtectedRoute`, `useSession` hook
- **`routes/app-router.tsx`** — route tree with protected and public routes
- **`features/`** — one folder per domain: `dashboard`, `projects`, `tasks`, `clients`, `users`, `billing`, `audit`, `settings`
- **`components/`** — shared layout (`app-shell.tsx`) and UI primitives (`ui/primitives.tsx`)
- **`app/providers.tsx`** — TanStack Query + React Router providers

The frontend reads tenant identity from the authenticated session and never independently asserts it.

## Testing

### Backend

- `TenantCore.UnitTests` — covers JWT service, plan limit logic, role guards, query filter isolation
- `TenantCore.IntegrationTests` — end-to-end API tests covering auth flows, tenant isolation enforcement, workspace boundaries, plan limits, and audit writes

Integration tests use a real SQL Server test database. Test factory is in `tests/TenantCore.IntegrationTests/Testing/`.

### Frontend

Vitest + Testing Library. Test files live alongside feature code (e.g., `login-page.test.tsx`, `protected-route.test.tsx`).

## Documentação técnica (`docs/`)

Consulte estes arquivos antes de fazer mudanças na área correspondente:

| Arquivo | Conteúdo | Quando consultar |
|---|---|---|
| `docs/architecture.md` | Visão geral do sistema, lifecycle de requests, camadas, observabilidade, topologia AWS recomendada | Antes de alterar pipeline, camadas ou planejar deploy |
| `docs/tenant-isolation.md` | Pontos de enforcement (middleware, ICurrentSession, query filters), índices, failure modes, cobertura de testes | Antes de qualquer mudança em entidades tenant-owned, `IgnoreQueryFilters()` ou middleware |
| `docs/security.md` | Auth lifecycle (JWT + refresh token), policies RBAC, rate limiting, security headers, CORS, auditabilidade, checklist de produção | Antes de implementar ou alterar qualquer fluxo de autenticação/autorização |
| `docs/runbook.md` | Startup local e Docker, demo flow para recrutadores, comandos úteis (migrations, build, test), incidents comuns, reset de ambiente | Para rodar o projeto, demo, troubleshooting ou reset |
| `docs/adr-001-multi-tenancy.md` | Decisão: shared-database com header + claim enforcement; alternativas rejeitadas | Antes de propor mudança no modelo de tenancy |
| `docs/adr-002-caching.md` | Decisão: Redis para `usage:{id}` e `subscription:{id}`; invalidação em writes e jobs | Antes de adicionar ou alterar estratégias de cache |
| `docs/adr-003-background-jobs.md` | Decisão: Quartz.NET in-process; alternativas rejeitadas | Antes de adicionar ou refatorar jobs |

## Agentes disponíveis (Claude Code)

Use o `Agent` tool com o `subagent_type` adequado para cada situação. Sempre prefira o agente especializado ao invés do general-purpose.

| Agente | Quando usar neste projeto |
|---|---|
| `Explore` | Busca rápida de arquivos, padrões, keywords no codebase |
| `Plan` | Antes de implementar features complexas — planejar arquitetura e trade-offs |
| `backend-architect` | Novos endpoints, handlers MediatR, entidades de domínio, migrations EF Core |
| `lucas-frontend-engineer` | Novos componentes React, páginas, hooks, integração com TanStack Query |
| `postgres-architect` | Modelagem de schema, migrations, índices, otimização de queries SQL Server |
| `qa-engineer` | Criar testes unitários e de integração, PRs, casos de borda |
| `code-quality-reviewer` | Após implementar qualquer feature — revisar antes de commitar |
| `security-hardening-validator` | Após implementar auth, endpoints, file uploads, qualquer surface sensível |
| `architecture-advisor` | Decisões de refatoração, novos padrões, trade-offs de design |
| `devops-deploy-architect` | Dockerfile, CI/CD GitHub Actions, health checks, configuração de produção |
| `sre-observability` | Logging (Serilog), OpenTelemetry, Jaeger, métricas, alertas |
| `tech-lead-orchestrator` | Revisão pré-deploy, decisões cross-cutting (auth flow, API design) |
| `dx-docs-writer` | Após features novas — atualizar README, runbook, ADRs em `docs/` |
| `llm-integration-architect` | Se adicionar IA/LLM ao projeto |
| `claude-code-guide` | Dúvidas sobre Claude Code CLI, hooks, MCP servers, keybindings |

## MCPs disponíveis

### Documentação e pesquisa
| MCP | Uso neste projeto |
|---|---|
| `mcp__context7__query-docs` | Buscar docs atualizadas de EF Core, ASP.NET, React, TanStack Query, Tailwind |
| `mcp__Ref__ref_read_url` / `ref_search_documentation` | Ler docs de qualquer biblioteca por URL |
| `mcp__awslabs-docs__search_documentation` | Documentação AWS quando usar serviços cloud |
| `mcp__exa__web_search_exa` | Pesquisa web para soluções técnicas e exemplos |
| `mcp__firecrawl-mcp__firecrawl_scrape` | Scraping de páginas de docs externas |

### UI / Design
| MCP | Uso neste projeto |
|---|---|
| `mcp__figma__get_design_context` | Importar designs Figma para replicar no frontend |
| `mcp__shadcn-ui__get_component` | Buscar componentes shadcn/ui compatíveis com Tailwind |
| `mcp__magic-mcp__21st_magic_component_builder` | Gerar componentes UI complexos com prompt |
| `mcp__magicuidesign__searchRegistryItems` | Buscar animações e componentes premium |
| `mcp__stitch__generate_screen_from_text` | Gerar mockups de telas para novas páginas |

### Browser / QA
| MCP | Uso neste projeto |
|---|---|
| `mcp__playwright__browser_navigate` | Testes E2E e smoke tests da aplicação rodando localmente |
| `mcp__chrome-devtools__take_screenshot` | Capturar screenshots para validar UI |
| `mcp__chrome-devtools__lighthouse_audit` | Auditoria de performance/acessibilidade do frontend |

### AWS / Infra
| MCP | Uso neste projeto |
|---|---|
| `mcp__awslabs-iam__*` | Gerenciar IAM quando fazer deploy na AWS |
| `mcp__awslabs-dynamodb__dynamodb_data_modeling` | Se migrar para DynamoDB |
| `mcp__awslabs-api__call_aws` | Executar comandos AWS diretamente |
| `mcp__netlify__*` | Deploy do frontend na Netlify |
| `mcp__supabase__*` | Se migrar banco para Supabase |

### Produtividade
| MCP | Uso neste projeto |
|---|---|
| `mcp__sequential-thinking__sequentialthinking` | Problemas complexos que precisam de raciocínio passo-a-passo |
| `mcp__notebooklm-mcp__notebook_create` | Criar notebook de pesquisa para ADRs complexos |

## Git workflow

**Após cada tarefa concluída, crie um commit e envie para o repositório remoto:**

```bash
# Adicione apenas os arquivos relevantes (nunca use git add -A cegamente)
git add <arquivos alterados>

# Mensagem de commit: título conciso (≤ 72 chars) + corpo opcional
git commit -m "$(cat <<'EOF'
tipo(escopo): descrição curta do que foi feito

Detalhes adicionais se necessário. Explique o "porquê",
não apenas o "o quê".

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>
EOF
)"

# Push para o repositório remoto
git push origin main
```

**Repositório:** https://github.com/Lucasantunesribeiro/tenant_core

**Regras:**
- Commits atômicos: uma task = um commit (ou mais se justificado)
- Nunca commitar `.env`, segredos ou arquivos de build (`bin/`, `dist/`, `node_modules/`)
- Prefixos: `feat`, `fix`, `refactor`, `style`, `test`, `docs`, `chore`
- Sempre verificar `git status` antes de adicionar arquivos

## Key conventions

- All new tenant-owned entities must inherit `TenantOwnedEntity` (includes `TenantId`) and have a global EF Core query filter registered in `TenantCoreDbContext`.
- New `IgnoreQueryFilters()` usages require a security review — they bypass the tenant boundary.
- Application handlers access tenant context only through `ICurrentSession`, not `HttpContext`.
- Domain failures throw `AppException`; `GlobalExceptionMiddleware` maps it to ProblemDetails.
- Audit writes go through `IAuditService` in application handlers for all sensitive mutations.
- Plan limit checks go through `IPlanLimitService` before writes that count against quotas.
- New frontend API calls go in `lib/api.ts` as TanStack Query hooks; never call `http.ts` directly from components.
