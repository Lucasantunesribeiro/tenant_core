using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TenantCore.Api.Middleware;
using TenantCore.Application;
using TenantCore.Application.Common.Security;
using TenantCore.Infrastructure;
using TenantCore.Infrastructure.Auth;
using TenantCore.Infrastructure.Database.Seed;
using TenantCore.Infrastructure.Observability;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ??
                         ["http://localhost:5173"];
    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

    builder.Services.AddProblemDetails();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("frontend", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

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
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = signingKey,
                NameClaimType = "sub",
                RoleClaimType = ClaimTypes.Role
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
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        options.AddPolicy(RateLimitPolicyNames.Api, httpContext =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: httpContext.User.FindFirstValue("sub") ??
                              httpContext.Connection.RemoteIpAddress?.ToString() ??
                              "anonymous",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 4,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
    });

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "tenant_core API",
            Version = "v1",
            Description = "Enterprise multi-tenant SaaS backend for tenant_core."
        });

        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = JwtBearerDefaults.AuthenticationScheme
            }
        };

        options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            [securityScheme] = Array.Empty<string>()
        });
    });

    var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("tenant_core.api"))
        .WithTracing(tracing =>
        {
            tracing
                .AddSource(TenantCoreTelemetry.ActivitySource.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation();

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation();

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                metrics.AddOtlpExporter((options, _) =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
        });

    var app = builder.Build();

    await app.Services.InitializeDatabaseAsync();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseCors("frontend");
    app.UseAuthentication();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous();
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    }).AllowAnonymous();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program
{
}
