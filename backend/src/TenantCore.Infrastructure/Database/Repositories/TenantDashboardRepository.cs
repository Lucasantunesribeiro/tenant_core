using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Reports;

namespace TenantCore.Infrastructure.Database.Repositories;

internal sealed class TenantDashboardRepository(IConfiguration configuration) : ITenantDashboardRepository
{
    private const string Sql = """
        SELECT
            Id        AS TenantId,
            Name      AS TenantName,
            Slug      AS TenantSlug
        FROM Tenants
        WHERE Id = @TenantId AND IsActive = 1;

        SELECT
            p.Id           AS ProjectId,
            p.Name         AS ProjectName,
            p.Code         AS ProjectCode,
            p.Status       AS ProjectStatus,
            p.StartDate    AS StartDate,
            p.DueDate      AS DueDate,
            c.Name         AS ClientName,
            u.FullName     AS OwnerFullName,
            COUNT(tk.Id)                                                                           AS TotalTasks,
            SUM(CASE WHEN tk.Status = 'Backlog'     THEN 1 ELSE 0 END)                            AS BacklogTasks,
            SUM(CASE WHEN tk.Status = 'InProgress'  THEN 1 ELSE 0 END)                            AS InProgressTasks,
            SUM(CASE WHEN tk.Status = 'Blocked'     THEN 1 ELSE 0 END)                            AS BlockedTasks,
            SUM(CASE WHEN tk.Status = 'Done'        THEN 1 ELSE 0 END)                            AS DoneTasks,
            SUM(CASE WHEN tk.DueDate < CAST(GETUTCDATE() AS DATE)
                     AND tk.Status <> 'Done'        THEN 1 ELSE 0 END)                            AS OverdueTasks
        FROM Projects p
        LEFT JOIN Clients c  ON c.Id  = p.ClientId    AND c.TenantId  = @TenantId
        LEFT JOIN Users u    ON u.Id  = p.OwnerUserId AND u.TenantId  = @TenantId
        LEFT JOIN [Tasks] tk ON tk.ProjectId = p.Id   AND tk.TenantId = @TenantId
        WHERE p.TenantId = @TenantId
        GROUP BY
            p.Id, p.Name, p.Code, p.Status, p.StartDate, p.DueDate,
            c.Name, u.FullName
        ORDER BY
            p.Status, p.Name;
        """;

    public async Task<TenantProjectDashboard> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServer' is missing.");

        await using var conn = new SqlConnection(connectionString);

        using var grid = await conn.QueryMultipleAsync(Sql, new { TenantId = tenantId });

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
