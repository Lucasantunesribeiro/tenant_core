using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Common.Security;
using TenantCore.Domain.Enums;

namespace TenantCore.Infrastructure.Services;

public sealed class HttpCurrentSession(IHttpContextAccessor httpContextAccessor) : ICurrentSession
{
    public Guid? UserId
    {
        get
        {
            var raw = HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      HttpContext?.User.FindFirstValue(ClaimTypes.Name) ??
                      HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(raw, out var parsed) ? parsed : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            var itemValue = HttpContext?.Items[HeaderNames.TenantId]?.ToString();
            if (Guid.TryParse(itemValue, out var parsedFromItem))
            {
                return parsedFromItem;
            }

            var claimValue = HttpContext?.User.FindFirstValue(ClaimNames.TenantId);
            return Guid.TryParse(claimValue, out var parsedFromClaim) ? parsedFromClaim : null;
        }
    }

    public string? Email =>
        HttpContext?.User.FindFirstValue(ClaimTypes.Email) ??
        HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Email);

    public UserRole? Role
    {
        get
        {
            var role = HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed) ? parsed : null;
        }
    }

    public string CorrelationId =>
        HttpContext?.Items[HeaderNames.CorrelationId]?.ToString() ??
        HttpContext?.TraceIdentifier ??
        Guid.NewGuid().ToString("N");

    public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated == true;

    private HttpContext? HttpContext => httpContextAccessor.HttpContext;

    public void EnsureAuthenticated()
    {
        if (!IsAuthenticated || UserId is null)
        {
            throw new AppException("unauthorized", "Unauthorized", 401, "Authentication is required for this operation.");
        }
    }

    public void EnsureTenantAvailable()
    {
        if (TenantId is null)
        {
            throw new AppException("tenant_header_required", "Tenant required", 400, "The X-Tenant-Id header is required.");
        }
    }
}
