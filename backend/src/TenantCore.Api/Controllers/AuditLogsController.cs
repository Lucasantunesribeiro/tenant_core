using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.AuditLogs.Queries;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.AdminOnly)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/audit-logs")]
public sealed class AuditLogsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogItem>>> GetAuditLogs(
        [FromQuery] string? action,
        [FromQuery] string? entityType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Sender.Send(new GetAuditLogsQuery(action, entityType, page, pageSize), cancellationToken));
    }
}
