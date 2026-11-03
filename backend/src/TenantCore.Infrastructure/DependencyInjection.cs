using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using StackExchange.Redis;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Reports;
using TenantCore.Infrastructure.Auth;
using TenantCore.Infrastructure.Caching;
using TenantCore.Infrastructure.Clock;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Database.Repositories;
using TenantCore.Infrastructure.Database.Seed;
using TenantCore.Infrastructure.HealthChecks;
using TenantCore.Infrastructure.Jobs;
using TenantCore.Infrastructure.Services;

namespace TenantCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        DapperConfig.RegisterTypeHandlers();

        services.AddHttpContextAccessor();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<JobScheduleOptions>(configuration.GetSection(JobScheduleOptions.SectionName));

        var databaseProvider = DatabaseProviderSettings.GetProvider(configuration);
        var databaseConnection = DatabaseProviderSettings.GetConnectionString(configuration, databaseProvider);
        var redisConnection = configuration.GetConnectionString("Redis");

        services.AddDbContext<TenantCoreDbContext>(options =>
            options.UseConfiguredDatabase(databaseProvider, databaseConnection, enableRetryOnFailure: true));

        services.AddScoped<ITenantCoreDbContext>(sp => sp.GetRequiredService<TenantCoreDbContext>());
        services.AddScoped<ITenantDashboardRepository, TenantDashboardRepository>();
        services.AddScoped<ICurrentSession, HttpCurrentSession>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICacheService, DistributedCacheService>();
        services.AddScoped<DemoDataSeeder>();

        var healthChecks = services.AddHealthChecks()
            .AddDbContextCheck<TenantCoreDbContext>("database", tags: new[] { "ready" });

        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            // Sem Redis (ex.: demo gratuita com uma única instância): cache distribuído em memória.
            // Com mais de uma instância da API o cache deixa de ser compartilhado; use Redis nesse caso.
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
            healthChecks.AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });
        }

        services.AddQuartz(q =>
        {
            var jobOptions = configuration.GetSection(JobScheduleOptions.SectionName).Get<JobScheduleOptions>() ?? new JobScheduleOptions();

            var usageJobKey = new JobKey(nameof(UsageSnapshotJob));
            q.AddJob<UsageSnapshotJob>(opts => opts.WithIdentity(usageJobKey));
            q.AddTrigger(opts => opts.ForJob(usageJobKey).WithIdentity($"{nameof(UsageSnapshotJob)}-trigger").WithCronSchedule(jobOptions.UsageSnapshotCron));

            var enforcementJobKey = new JobKey(nameof(SubscriptionEnforcementJob));
            q.AddJob<SubscriptionEnforcementJob>(opts => opts.WithIdentity(enforcementJobKey));
            q.AddTrigger(opts => opts.ForJob(enforcementJobKey).WithIdentity($"{nameof(SubscriptionEnforcementJob)}-trigger").WithCronSchedule(jobOptions.SubscriptionEnforcementCron));

            var cleanupJobKey = new JobKey(nameof(CleanupJob));
            q.AddJob<CleanupJob>(opts => opts.WithIdentity(cleanupJobKey));
            q.AddTrigger(opts => opts.ForJob(cleanupJobKey).WithIdentity($"{nameof(CleanupJob)}-trigger").WithCronSchedule(jobOptions.CleanupCron));
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }
}
