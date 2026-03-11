using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using TenantCore.Api.Controllers;
using TenantCore.Api.Middleware;
using TenantCore.Application;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Reports;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Infrastructure.Auth;
using TenantCore.Infrastructure.Clock;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Database.Seed;
using TenantCore.Infrastructure.Services;

namespace TenantCore.IntegrationTests.Testing;

internal sealed class TenantCoreApiTestHost : IAsyncDisposable
{
    public static readonly Guid AcmeTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid GlobexTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplication _app;

    private TenantCoreApiTestHost(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public static async Task<TenantCoreApiTestHost> CreateAsync()
    {
        var databaseName = $"tenant-core-tests-{Guid.NewGuid():N}";

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApplication();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        builder.Services.AddDbContext<TenantCoreDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddScoped<ITenantCoreDbContext>(sp => sp.GetRequiredService<TenantCoreDbContext>());
        builder.Services.AddScoped<ICurrentSession, HttpCurrentSession>();
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddSingleton<IPasswordService, PasswordService>();
        builder.Services.AddSingleton<ITokenService>(_ =>
            new JwtTokenService(
                Microsoft.Extensions.Options.Options.Create(new JwtOptions
                {
                    Issuer = "tenant_core.tests",
                    Audience = "tenant_core.tests.web",
                    SigningKey = "tenant-core-tests-signing-key-1234567890",
                    AccessTokenMinutes = 15,
                    RefreshTokenDays = 14,
                }),
                new SystemClock()));
        builder.Services.AddSingleton<ICacheService, TestCacheService>();
        builder.Services.AddScoped<DemoDataSeeder>();

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("tenant-core-tests-signing-key-1234567890"));

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = "tenant_core.tests",
                    ValidAudience = "tenant_core.tests.web",
                    IssuerSigningKey = signingKey,
                    NameClaimType = "sub",
                    RoleClaimType = ClaimTypes.Role,
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(PolicyNames.TenantMember, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole("Admin"));
            options.AddPolicy(PolicyNames.ManagerOrAdmin, policy => policy.RequireRole("Admin", "Manager"));
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicyNames.Auth, httpContext =>
                RateLimitPartition.GetNoLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"));
            options.AddPolicy(RateLimitPolicyNames.Api, httpContext =>
                RateLimitPartition.GetNoLimiter(httpContext.User.FindFirstValue("sub") ?? "anonymous"));
        });

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TenantCoreDbContext>("sqlserver", tags: new[] { "ready" })
            .AddCheck("redis", () => HealthCheckResult.Healthy(), tags: new[] { "ready" });

        builder.Services.AddScoped<ITenantDashboardRepository, NullTenantDashboardRepository>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TenantCoreDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            await seeder.SeedAsync();
        }

        app.UseMiddleware<GlobalExceptionMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseRateLimiter();
        app.UseAuthorization();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        }).AllowAnonymous();
        app.MapControllers();

        await app.StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://localhost");

        return new TenantCoreApiTestHost(app, client);
    }

    public async Task<LoginEnvelope> LoginAsync(string email, string tenantId, string password = "Passw0rd!")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password,
            }),
        };
        request.Headers.Add(HeaderNames.TenantId, tenantId);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<LoginEnvelope>(JsonOptions);
        return envelope ?? throw new InvalidOperationException("Login response payload was null.");
    }

    public async Task<string> LoginAndGetRefreshCookieAsync(string email, string tenantId, string password = "Passw0rd!")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password,
            }),
        };
        request.Headers.Add(HeaderNames.TenantId, tenantId);

        var response = await Client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return ExtractRefreshCookie(response);
    }

    public HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method,
        string uri,
        LoginEnvelope session,
        object? body = null,
        string? tenantId = null,
        string? refreshCookie = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.Add(HeaderNames.TenantId, tenantId ?? session.User.TenantId.ToString());

        if (!string.IsNullOrWhiteSpace(refreshCookie))
        {
            request.Headers.Add("Cookie", refreshCookie);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        var cookieHeader = response.Headers.GetValues("Set-Cookie").Single();
        return cookieHeader.Split(';', 2)[0];
    }

    public static async Task<ProblemDetailsResponse> ReadProblemAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions);
        return payload ?? throw new InvalidOperationException("Problem details payload was null.");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}

internal sealed class NullTenantDashboardRepository : ITenantDashboardRepository
{
    public Task<TenantProjectDashboard> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(new TenantProjectDashboard(tenantId, string.Empty, string.Empty, []));
}

internal sealed class TestCacheService : ICacheService
{
    private readonly Dictionary<string, object> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
        {
            return Task.FromResult<T?>(typed);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        _store[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}

internal sealed record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    string TraceId,
    Dictionary<string, string[]>? Errors);
