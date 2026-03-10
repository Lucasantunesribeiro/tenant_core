using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Auth.Commands;
using TenantCore.Application.Auth.Queries;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Common.Security;

namespace TenantCore.Api.Controllers;

[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicyNames.Auth)]
public sealed class AuthController : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginEnvelope>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await Sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresAtUtc);

        return Ok(new LoginEnvelope(
            response.AccessToken,
            response.AccessTokenExpiresAtUtc,
            response.User,
            response.TenantName,
            response.PlanCode));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshEnvelope>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[CookieNames.RefreshToken]
            ?? throw new AppException("missing_refresh_token", "Refresh token missing", 401, "No refresh token cookie was found.");

        var response = await Sender.Send(new RefreshSessionCommand(refreshToken), cancellationToken);
        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresAtUtc);

        return Ok(new RefreshEnvelope(response.AccessToken, response.AccessTokenExpiresAtUtc));
    }

    [Authorize(Policy = PolicyNames.TenantMember)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(CookieNames.RefreshToken, out var refreshToken))
        {
            await Sender.Send(new LogoutCommand(refreshToken), cancellationToken);
        }

        Response.Cookies.Delete(CookieNames.RefreshToken);
        return NoContent();
    }

    [Authorize(Policy = PolicyNames.TenantMember)]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        return Ok(await Sender.Send(new GetCurrentUserQuery(), cancellationToken));
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTimeOffset expiresAtUtc)
    {
        // Secure = true always; local dev must use HTTPS or a reverse proxy that sets the flag.
        // Never rely on Request.IsHttps — it returns false behind plain-HTTP Docker proxies.
        Response.Cookies.Append(CookieNames.RefreshToken, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc.UtcDateTime,
            IsEssential = true
        });
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginEnvelope(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    AuthenticatedUserDto User,
    string TenantName,
    TenantCore.Domain.Enums.PlanCode PlanCode);

public sealed record RefreshEnvelope(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc);
