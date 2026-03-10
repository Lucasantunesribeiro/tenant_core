using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TenantCore.Application.Common.Behaviors;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Services;

namespace TenantCore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly));
        services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly, includeInternalTypes: true);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPlanLimitService, PlanLimitService>();

        return services;
    }
}
