using System.Security.Claims;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Common.Security;

namespace TenantCore.Api.Middleware;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private static readonly string[] ExcludedPrefixes = ["/health", "/swagger", "/openapi", "/favicon"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method) ||
            ExcludedPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderNames.TenantId, out var tenantHeader) ||
            !Guid.TryParse(tenantHeader, out var tenantId))
        {
            throw new AppException("tenant_header_required", "Tenant required", 400, "A valid X-Tenant-Id header is required.");
        }

        context.Items[HeaderNames.TenantId] = tenantId.ToString();

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var claimValue = context.User.FindFirstValue(ClaimNames.TenantId);
            if (!Guid.TryParse(claimValue, out var claimedTenantId) || claimedTenantId != tenantId)
            {
                throw new AppException("tenant_mismatch", "Tenant mismatch", 403, "The tenant header does not match the authenticated user's tenant.");
            }
        }

        await next(context);
    }
}
