using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using StackExchange.Redis;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Infrastructure.Auth;
using TenantCore.Infrastructure.Caching;
using TenantCore.Infrastructure.Clock;
using TenantCore.Infrastructure.Database;
using TenantCore.Infrastructure.Database.Seed;
using TenantCore.Infrastructure.HealthChecks;
using TenantCore.Infrastructure.Jobs;
using TenantCore.Infrastructure.Services;

namespace TenantCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<JobScheduleOptions>(configuration.GetSection(JobScheduleOptions.SectionName));

        var sqlConnection = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("Connection string 'SqlServer' is missing.");
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Connection string 'Redis' is missing.");

        services.AddDbContext<TenantCoreDbContext>(options =>
            options.UseNpgsql(sqlConnection, pg =>
            {
                pg.MigrationsAssembly(typeof(TenantCoreDbContext).Assembly.FullName);
                pg.EnableRetryOnFailure();
            }));

        services.AddScoped<ITenantCoreDbContext>(sp => sp.GetRequiredService<TenantCoreDbContext>());
        services.AddScoped<ICurrentSession, HttpCurrentSession>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<ICacheService, DistributedCacheService>();
        services.AddScoped<DemoDataSeeder>();

        services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddHealthChecks()
            .AddDbContextCheck<TenantCoreDbContext>("postgres", tags: new[] { "ready" })
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

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
