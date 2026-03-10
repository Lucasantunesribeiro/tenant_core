using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Common.Security;
using TenantCore.Application.Tenants.Commands;
using TenantCore.Application.Tenants.Queries;
using TenantCore.Domain.Enums;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.TenantMember)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/tenant")]
public sealed class TenantController : ApiControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<TenantProfileResponse>> GetProfile(CancellationToken cancellationToken)
    {
        return Ok(await Sender.Send(new GetTenantProfileQuery(), cancellationToken));
    }

    [Authorize(Policy = PolicyNames.AdminOnly)]
    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTenantSettingsRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(
            new UpdateTenantSettingsCommand(
                request.Name,
                request.BillingEmail,
                request.SupportEmail,
                request.TimeZone,
                request.Theme,
                request.AllowedDomains),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("subscription")]
    public async Task<ActionResult<SubscriptionResponse>> GetSubscription(CancellationToken cancellationToken)
    {
        return Ok(await Sender.Send(new GetSubscriptionQuery(), cancellationToken));
    }

    [Authorize(Policy = PolicyNames.AdminOnly)]
    [HttpPost("subscription/change-plan")]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new ChangePlanCommand(request.PlanCode), cancellationToken);
        return NoContent();
    }

    [HttpGet("usage")]
    public async Task<ActionResult<UsageDashboardResponse>> GetUsage(CancellationToken cancellationToken)
    {
        return Ok(await Sender.Send(new GetUsageDashboardQuery(), cancellationToken));
    }
}

public sealed record UpdateTenantSettingsRequest(
    string Name,
    string BillingEmail,
    string SupportEmail,
    string TimeZone,
    string Theme,
    string AllowedDomains);

public sealed record ChangePlanRequest(PlanCode PlanCode);
