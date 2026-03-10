# Azure Infrastructure — tenant_core

This document explains how to provision every Azure resource required to run
tenant_core in production and how to wire the CI/CD secrets so the GitHub
Actions deploy workflows work end-to-end.

The target architecture uses only free or near-free tiers:

```
GitHub Actions
  ├── deploy-backend.yml  ──builds──► ghcr.io (free)
  │                         deploys►  Azure App Service (B1/F1)
  │                                       │
  │                                       ├── Azure SQL Database Serverless (free tier)
  │                                       └── Upstash Redis (free, external)
  │
  └── deploy-frontend.yml ──builds+deploys►  Azure Static Web Apps (free)
```

---

## Prerequisites

- Azure CLI installed: https://learn.microsoft.com/cli/azure/install-azure-cli
- Logged in: `az login`
- A GitHub repository: https://github.com/Lucasantunesribeiro/tenant_core

All `az` commands below assume bash. Replace every `<PLACEHOLDER>` with your
own value before running.

---

## 1. Create a resource group

```bash
RESOURCE_GROUP="tenant-core-rg"
LOCATION="eastus"   # choose the region closest to your users

az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION"
```

---

## 2. Create Azure SQL Database (Serverless, free tier)

The free serverless tier (General Purpose, 1 vCore) has no monthly cost for
the first 12 months on a new subscription. After the free period it
auto-pauses when idle, so cost stays near zero for demo workloads.

```bash
SQL_SERVER_NAME="tenant-core-sql"        # must be globally unique
SQL_DB_NAME="tenant_core"
SQL_ADMIN_USER="sqladmin"
SQL_ADMIN_PASSWORD="<STRONG_PASSWORD>"   # min 8 chars, upper+lower+number+symbol
                                          # NEVER commit this value

# Create the logical SQL server
az sql server create \
  --name "$SQL_SERVER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --admin-user "$SQL_ADMIN_USER" \
  --admin-password "$SQL_ADMIN_PASSWORD"

# Allow Azure services (including App Service) to reach the SQL server
az sql server firewall-rule create \
  --server "$SQL_SERVER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --name "AllowAzureServices" \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create the database on the free serverless tier
az sql db create \
  --server "$SQL_SERVER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SQL_DB_NAME" \
  --edition "GeneralPurpose" \
  --family "Gen5" \
  --capacity 1 \
  --compute-model "Serverless" \
  --auto-pause-delay 60 \
  --min-capacity 0.5
```

The connection string to use in App Service environment variables:

```
Server=tcp:<SQL_SERVER_NAME>.database.windows.net,1433;Database=tenant_core;User Id=<SQL_ADMIN_USER>;Password=<SQL_ADMIN_PASSWORD>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Note: `TrustServerCertificate=False` is required for Azure SQL. The backend
Dockerfile does NOT set this — it comes from the connection string at runtime.

---

## 3. Set up Upstash Redis (free, external)

Azure Cache for Redis starts at ~$16/month. Upstash offers a free tier with
10,000 commands/day, which is sufficient for the usage and subscription cache
keys used by tenant_core.

1. Create a free account at https://upstash.com
2. Click "Create Database" → select "Redis" → choose the region closest to
   your App Service location → select "Free" plan
3. Copy the **Redis URL** from the "Details" tab. It looks like:
   `rediss://default:<PASSWORD>@<HOST>.upstash.io:6379`

The `ConnectionStrings__Redis` App Service app setting must use this URL
verbatim. StackExchange.Redis (used by the backend) accepts the `rediss://`
scheme for TLS connections.

---

## 4. Create the App Service Plan and Web App for Containers

```bash
APP_SERVICE_PLAN="tenant-core-plan"
WEBAPP_NAME="tenant-core-api"   # must be globally unique; becomes <name>.azurewebsites.net

# B1 (Basic, ~$13/month) supports custom containers. F1 (Free) does NOT support
# custom containers — use B1 for this project.
az appservice plan create \
  --name "$APP_SERVICE_PLAN" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --is-linux \
  --sku B1

# Create the web app for containers. The initial image is a placeholder;
# the CI/CD pipeline overwrites it on the first deploy.
az webapp create \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$APP_SERVICE_PLAN" \
  --deployment-container-image-name "mcr.microsoft.com/dotnet/samples:aspnetapp"
```

---

## 5. Configure App Service environment variables

These app settings override the values in appsettings.Production.json. The
double-underscore `__` notation maps to the colon `:` hierarchy in .NET
configuration, so `ConnectionStrings__SqlServer` is equivalent to
`ConnectionStrings:SqlServer`.

```bash
WEBAPP_NAME="tenant-core-api"
RESOURCE_GROUP="tenant-core-rg"

# Construct values before running — never type secrets directly into history
SQL_CONN="Server=tcp:<SQL_SERVER_NAME>.database.windows.net,1433;Database=tenant_core;User Id=<SQL_ADMIN_USER>;Password=<SQL_ADMIN_PASSWORD>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
REDIS_CONN="rediss://default:<UPSTASH_PASSWORD>@<UPSTASH_HOST>.upstash.io:6379"
JWT_KEY="<RANDOM_BASE64_STRING_AT_LEAST_32_CHARS>"   # generate: openssl rand -base64 48
SWA_URL="https://<YOUR_SWA_SUBDOMAIN>.azurestaticapps.net"

az webapp config appsettings set \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    WEBSITES_PORT="8080" \
    ConnectionStrings__SqlServer="$SQL_CONN" \
    ConnectionStrings__Redis="$REDIS_CONN" \
    Jwt__SigningKey="$JWT_KEY" \
    Cors__AllowedOrigins__0="$SWA_URL"
```

**JWT signing key requirement**: Program.cs validates that `Jwt__SigningKey`
is at least 32 characters and will throw `InvalidOperationException` at
startup if this is not met. Generate a key with:

```bash
openssl rand -base64 48
```

**WEBSITES_PORT=8080**: The Dockerfile sets `EXPOSE 8080` and
`ASPNETCORE_URLS=http://+:8080`. Azure App Service by default proxies to
port 80. Setting `WEBSITES_PORT=8080` tells the platform to forward traffic
to port 8080 instead.

---

## 6. Configure App Service to authenticate with ghcr.io

The deploy workflow pushes the image to GitHub Container Registry (ghcr.io).
The App Service needs credentials to pull it.

```bash
GITHUB_USERNAME="<YOUR_GITHUB_USERNAME>"
# Create a GitHub PAT with read:packages scope at https://github.com/settings/tokens
GHCR_PAT="<GITHUB_PAT_WITH_READ_PACKAGES>"

az webapp config appsettings set \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    DOCKER_REGISTRY_SERVER_URL="https://ghcr.io" \
    DOCKER_REGISTRY_SERVER_USERNAME="$GITHUB_USERNAME" \
    DOCKER_REGISTRY_SERVER_PASSWORD="$GHCR_PAT"
```

If your GitHub repository is public, the ghcr.io image is publicly readable
and you can skip the `DOCKER_REGISTRY_SERVER_USERNAME/PASSWORD` settings.

---

## 7. Download the App Service publish profile

The deploy workflow authenticates with the `AZURE_WEBAPP_PUBLISH_PROFILE`
secret. Download it from the Azure Portal:

1. Open the App Service resource in the Azure Portal
2. Click "Download publish profile" in the Overview blade
3. Open the downloaded `.PublishSettings` file — copy its entire XML content
4. In your GitHub repository: Settings → Secrets and variables → Actions →
   New repository secret → Name: `AZURE_WEBAPP_PUBLISH_PROFILE` → paste XML

Alternatively, with the CLI:

```bash
az webapp deployment list-publishing-profiles \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --xml
```

Copy the output and store it as the `AZURE_WEBAPP_PUBLISH_PROFILE` secret.

---

## 8. Create Azure Static Web Apps

```bash
SWA_NAME="tenant-core-web"
GITHUB_REPO="https://github.com/Lucasantunesribeiro/tenant_core"
GITHUB_TOKEN="<GITHUB_PAT_WITH_REPO_SCOPE>"

az staticwebapp create \
  --name "$SWA_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "eastus2" \
  --source "$GITHUB_REPO" \
  --branch "master" \
  --app-location "frontend" \
  --output-location "dist" \
  --login-with-github
```

The `--login-with-github` flag opens a browser for OAuth. The CLI will
automatically create the deploy token and store it as a GitHub Actions secret
named `AZURE_STATIC_WEB_APPS_API_TOKEN_<RANDOM>` in your repository.

Copy the deployment token for the next step:

```bash
az staticwebapp secrets list \
  --name "$SWA_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "properties.apiKey" \
  --output tsv
```

Store this value as the GitHub Actions secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.

---

## 9. Configure GitHub Actions secrets and variables

Navigate to: GitHub repository → Settings → Secrets and variables → Actions

### Repository secrets (encrypted, never visible after saving)

| Secret name                         | Value                                              |
|-------------------------------------|----------------------------------------------------|
| `AZURE_WEBAPP_PUBLISH_PROFILE`      | XML content of the App Service publish profile     |
| `AZURE_STATIC_WEB_APPS_API_TOKEN`   | SWA deployment token (from step 8)                 |

### Repository variables (non-secret, visible in logs)

| Variable name        | Example value                                                |
|----------------------|--------------------------------------------------------------|
| `AZURE_WEBAPP_NAME`  | `tenant-core-api`                                            |
| `AZURE_WEBAPP_URL`   | `https://tenant-core-api.azurewebsites.net`                  |
| `VITE_API_URL`       | `https://tenant-core-api.azurewebsites.net`                  |

`VITE_API_URL` must NOT have a trailing slash. It is injected into the Vite
build at compile time and baked into the JS bundle.

---

## 10. First deploy and database seed

The first time the backend starts in production it calls
`InitializeDatabaseAsync()` (see `ApplicationBuilderExtensions.cs`), which
runs any pending EF Core migrations and seeds demo data if the Users table is
empty. This is automatic — no manual migration step is needed.

To verify the first deploy is healthy:

```bash
curl -fsS https://tenant-core-api.azurewebsites.net/health/live
curl -fsS https://tenant-core-api.azurewebsites.net/health/ready
```

`/health/live` returns 200 as soon as the process is running.
`/health/ready` returns 200 only when both SQL Server and Redis health checks
pass (tagged `ready` in DependencyInjection.cs).

Demo credentials after seed:

| Role    | Email                       | Password   |
|---------|-----------------------------|------------|
| Admin   | alice@acme-corp.com         | Passw0rd!  |
| Manager | bob@acme-corp.com           | Passw0rd!  |
| Member  | charlie@acme-corp.com       | Passw0rd!  |

---

## 11. CORS configuration

The backend reads `Cors:AllowedOrigins` from configuration. The App Service
app setting `Cors__AllowedOrigins__0` must be set to the exact Static Web App
URL (including `https://`, without trailing slash).

If you add a custom domain to the Static Web App, add it as a second allowed
origin:

```bash
az webapp config appsettings set \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    Cors__AllowedOrigins__1="https://yourdomain.com"
```

---

## 12. Rollback procedure

Azure App Service on the Basic tier does not support deployment slots, so
there is no built-in blue-green rollback. To roll back manually:

1. Identify the previous image SHA from the GitHub Actions run history or
   from the ghcr.io package page.
2. Update the App Service container image:

```bash
PREVIOUS_SHA="sha-<40-char-sha>"
IMAGE="ghcr.io/<OWNER>/tenant-core-api:${PREVIOUS_SHA}"

az webapp config container set \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --container-image-name "$IMAGE"

# Restart to pull the image immediately
az webapp restart \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP"
```

3. Verify health checks pass after restart.
4. If the issue was a bad database migration, restore from the automatic Azure
   SQL backup (point-in-time restore, available for up to 7 days on the free
   tier).

---

## Cost summary (as of early 2026)

| Resource                     | Tier           | Monthly cost         |
|------------------------------|----------------|----------------------|
| Azure App Service            | B1 Linux       | ~$13 USD             |
| Azure SQL Database           | Serverless Gen5 1vCore, free tier | $0 (first 12 months), then ~$5-10 auto-paused |
| Azure Static Web Apps        | Free           | $0                   |
| GitHub Container Registry    | Public repo    | $0                   |
| Upstash Redis                | Free tier      | $0                   |
| **Total (first year)**       |                | **~$13/month**       |

To reduce below $13/month, scale the App Service down to F1 (Free) — but F1
does NOT support custom containers. The minimum tier for container deployment
is B1.
