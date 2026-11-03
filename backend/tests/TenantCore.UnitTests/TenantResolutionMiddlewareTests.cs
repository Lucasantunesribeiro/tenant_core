using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using TenantCore.Api.Middleware;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Application.Common.Security;

namespace TenantCore.UnitTests;

public sealed class TenantResolutionMiddlewareTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static DefaultHttpContext CreateContext(string path, string? header, string? tenantClaim = null, bool authenticated = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;

        if (header is not null)
        {
            context.Request.Headers[HeaderNames.TenantId] = header;
        }

        if (authenticated)
        {
            var claims = new List<Claim> { new("sub", Guid.NewGuid().ToString()) };
            if (tenantClaim is not null)
            {
                claims.Add(new Claim(ClaimNames.TenantId, tenantClaim));
            }

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        }

        return context;
    }

    private static async Task<(bool nextCalled, AppException? error)> RunAsync(HttpContext context)
    {
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        try
        {
            await middleware.InvokeAsync(context);
            return (nextCalled, null);
        }
        catch (AppException ex)
        {
            return (nextCalled, ex);
        }
    }

    [Fact]
    public async Task AuthenticatedRequest_WithForgedHeader_ShouldReturn403AndNotCallNext()
    {
        var context = CreateContext("/api/projects", TenantB.ToString(), TenantA.ToString(), authenticated: true);

        var (nextCalled, error) = await RunAsync(context);

        nextCalled.Should().BeFalse();
        error.Should().NotBeNull();
        error!.StatusCode.Should().Be(403);
        error.Type.Should().Be("tenant_mismatch");
    }

    [Fact]
    public async Task AuthenticatedRequest_WithoutTenantClaim_ShouldReturn403()
    {
        var context = CreateContext("/api/projects", TenantA.ToString(), tenantClaim: null, authenticated: true);

        var (nextCalled, error) = await RunAsync(context);

        nextCalled.Should().BeFalse();
        error!.Type.Should().Be("tenant_mismatch");
    }

    [Fact]
    public async Task AuthenticatedRequest_WithMatchingHeader_ShouldCallNext()
    {
        var context = CreateContext("/api/projects", TenantA.ToString().ToUpperInvariant(), TenantA.ToString(), authenticated: true);

        var (nextCalled, error) = await RunAsync(context);

        error.Should().BeNull();
        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Request_WithMissingOrInvalidHeader_ShouldReturn400(string? header)
    {
        var context = CreateContext("/api/auth/login", header);

        var (nextCalled, error) = await RunAsync(context);

        nextCalled.Should().BeFalse();
        error!.StatusCode.Should().Be(400);
        error.Type.Should().Be("tenant_header_required");
    }

    [Fact]
    public async Task AnonymousRequest_WithValidHeader_ShouldStoreTenantForLogin()
    {
        var context = CreateContext("/api/auth/login", TenantA.ToString());

        var (nextCalled, _) = await RunAsync(context);

        nextCalled.Should().BeTrue();
        context.Items[HeaderNames.TenantId].Should().Be(TenantA.ToString());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/swagger/index.html")]
    public async Task ExcludedPaths_ShouldSkipTenantResolution(string path)
    {
        var context = CreateContext(path, header: null);

        var (nextCalled, error) = await RunAsync(context);

        error.Should().BeNull();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PathThatOnlyStartsWithExcludedPrefix_ShouldStillRequireTenant()
    {
        var context = CreateContext("/healthy-but-not-excluded", header: null);

        var (_, error) = await RunAsync(context);

        error!.StatusCode.Should().Be(400);
    }
}
