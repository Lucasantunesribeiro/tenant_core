using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TenantCore.Application.Common.Security;
using TenantCore.Infrastructure.Services;

namespace TenantCore.UnitTests;

public sealed class HttpCurrentSessionTests
{
    private static readonly Guid ClaimTenant = Guid.NewGuid();
    private static readonly Guid ForgedTenant = Guid.NewGuid();

    private static HttpCurrentSession CreateSession(HttpContext? context) =>
        new(new HttpContextAccessor { HttpContext = context });

    private static DefaultHttpContext AuthenticatedContext(string? tenantClaim)
    {
        var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
        if (tenantClaim is not null)
        {
            claims.Add(new Claim(ClaimNames.TenantId, tenantClaim));
        }

        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
    }

    [Fact]
    public void TenantId_WhenAuthenticated_ShouldComeFromClaimEvenIfItemsWereForged()
    {
        var context = AuthenticatedContext(ClaimTenant.ToString());
        context.Items[HeaderNames.TenantId] = ForgedTenant.ToString();
        context.Request.Headers[HeaderNames.TenantId] = ForgedTenant.ToString();

        CreateSession(context).TenantId.Should().Be(ClaimTenant);
    }

    [Fact]
    public void TenantId_WhenAuthenticatedWithoutTenantClaim_ShouldBeNullAndNotFallBackToHeader()
    {
        var context = AuthenticatedContext(tenantClaim: null);
        context.Items[HeaderNames.TenantId] = ForgedTenant.ToString();

        CreateSession(context).TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_WhenAnonymous_ShouldUseValueResolvedByMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Items[HeaderNames.TenantId] = ClaimTenant.ToString();

        CreateSession(context).TenantId.Should().Be(ClaimTenant);
    }

    [Fact]
    public void TenantId_WhenAnonymous_ShouldIgnoreRawHeaderNotValidatedByMiddleware()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[HeaderNames.TenantId] = ForgedTenant.ToString();

        CreateSession(context).TenantId.Should().BeNull();
    }

    [Fact]
    public void TenantId_WithoutHttpContext_ShouldBeNull()
    {
        // Jobs Quartz rodam sem HttpContext: query filters ficam fail-closed.
        CreateSession(null).TenantId.Should().BeNull();
    }
}
