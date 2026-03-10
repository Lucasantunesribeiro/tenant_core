using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Application.Users.Commands;
using TenantCore.Application.Users.Queries;
using TenantCore.Domain.Enums;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.TenantMember)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/users")]
public sealed class UsersController : ApiControllerBase
{
    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItem>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] UserRole? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Sender.Send(new GetUsersQuery(search, role, page, pageSize), cancellationToken));
    }

    [Authorize(Policy = PolicyNames.AdminOnly)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var userId = await Sender.Send(
            new CreateUserCommand(request.Email, request.FullName, request.Password, request.Role, request.InvitationPending),
            cancellationToken);

        return CreatedAtAction(nameof(GetUsers), new { id = userId }, userId);
    }

    [Authorize(Policy = PolicyNames.AdminOnly)]
    [HttpPatch("{userId:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid userId, [FromBody] ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new ChangeUserRoleCommand(userId, request.Role), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string Password,
    UserRole Role,
    bool InvitationPending);

public sealed record ChangeUserRoleRequest(UserRole Role);
