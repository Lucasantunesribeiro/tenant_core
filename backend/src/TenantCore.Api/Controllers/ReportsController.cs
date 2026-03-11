using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Common.Security;
using TenantCore.Application.Reports;
using TenantCore.Application.Reports.Queries;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.TenantMember)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/reports")]
public sealed class ReportsController : ApiControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<TenantProjectDashboard>> GetDashboard(CancellationToken cancellationToken)
        => Ok(await Sender.Send(new GetTenantDashboardQuery(), cancellationToken));
}
