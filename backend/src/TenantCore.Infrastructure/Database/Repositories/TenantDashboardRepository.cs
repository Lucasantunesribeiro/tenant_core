using System.Data;
using Dapper;
using Microsoft.Extensions.Configuration;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Reports;

namespace TenantCore.Infrastructure.Database.Repositories;

internal sealed class TenantDashboardRepository(IConfiguration configuration) : ITenantDashboardRepository
{
    // SQL portável entre SQL Server e PostgreSQL: identificadores entre aspas duplas
    // (QUOTED_IDENTIFIER é ON por padrão no SqlClient), booleano e data como parâmetros
    // e agregados convertidos para INT (no PostgreSQL COUNT/SUM devolvem bigint).
    private const string Sql = """
        SELECT
            t."Id"        AS TenantId,
            t."Name"      AS TenantName,
            t."Slug"      AS TenantSlug
        FROM "Tenants" t
        WHERE t."Id" = @TenantId AND t."IsActive" = @IsActive;

        SELECT
            p."Id"           AS ProjectId,
            p."Name"         AS ProjectName,
            p."Code"         AS ProjectCode,
            p."Status"       AS ProjectStatus,
            p."StartDate"    AS StartDate,
            p."DueDate"      AS DueDate,
            c."Name"         AS ClientName,
            u."FullName"     AS OwnerFullName,
            CAST(COUNT(tk."Id") AS INT)                                                                   AS TotalTasks,
            CAST(SUM(CASE WHEN tk."Status" = 'Backlog'    THEN 1 ELSE 0 END) AS INT)                    AS BacklogTasks,
            CAST(SUM(CASE WHEN tk."Status" = 'InProgress' THEN 1 ELSE 0 END) AS INT)                    AS InProgressTasks,
            CAST(SUM(CASE WHEN tk."Status" = 'Blocked'    THEN 1 ELSE 0 END) AS INT)                    AS BlockedTasks,
            CAST(SUM(CASE WHEN tk."Status" = 'Done'       THEN 1 ELSE 0 END) AS INT)                    AS DoneTasks,
            CAST(SUM(CASE WHEN tk."DueDate" < @Today
                          AND tk."Status" <> 'Done'       THEN 1 ELSE 0 END) AS INT)                    AS OverdueTasks
        FROM "Projects" p
        LEFT JOIN "Clients" c ON c."Id" = p."ClientId"     AND c."TenantId"  = @TenantId
        LEFT JOIN "Users" u   ON u."Id" = p."OwnerUserId"  AND u."TenantId"  = @TenantId
        LEFT JOIN "Tasks" tk  ON tk."ProjectId" = p."Id"   AND tk."TenantId" = @TenantId
        WHERE p."TenantId" = @TenantId
        GROUP BY
            p."Id", p."Name", p."Code", p."Status", p."StartDate", p."DueDate",
            c."Name", u."FullName"
        ORDER BY
            p."Status", p."Name";
        """;

    public async Task<TenantProjectDashboard> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using var conn = DatabaseProviderSettings.CreateConnection(configuration);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        using var grid = await conn.QueryMultipleAsync(Sql, new { TenantId = tenantId, IsActive = true, Today = today });

        var tenantRow = await grid.ReadSingleOrDefaultAsync<TenantRow>();

        if (tenantRow is null)
        {
            throw new AppException(
                "tenant_not_found",
                "Tenant not found.",
                404,
                "Tenant not found.");
        }

        var rows = await grid.ReadAsync<ProjectDashboardRow>();

        return new TenantProjectDashboard(
            tenantRow.TenantId,
            tenantRow.TenantName,
            tenantRow.TenantSlug,
            rows.ToList());
    }

    private sealed record TenantRow(Guid TenantId, string TenantName, string TenantSlug);
}
