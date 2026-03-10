using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Tenants.Commands;

public sealed record UpdateTenantSettingsCommand(
    string Name,
    string BillingEmail,
    string SupportEmail,
    string TimeZone,
    string Theme,
    string AllowedDomains) : IRequest;

internal sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BillingEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.SupportEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Theme).NotEmpty().MaximumLength(40);
    }
}

internal sealed class UpdateTenantSettingsCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<UpdateTenantSettingsCommand>
{
    public async Task Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAdmin();
        var tenantId = currentSession.GetRequiredTenantId();

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new AppException("tenant_not_found", "Tenant not found", 404, "The tenant does not exist.");

        tenant.UpdateSettings(
            request.Name.Trim(),
            request.BillingEmail.Trim().ToLowerInvariant(),
            request.SupportEmail.Trim().ToLowerInvariant(),
            request.TimeZone.Trim(),
            request.Theme.Trim(),
            request.AllowedDomains.Trim(),
            clock.UtcNow);

        await auditService.WriteAsync("tenant.settings_updated", "Tenant", tenant.Id.ToString(),
            new { request.Name, request.BillingEmail, request.SupportEmail, request.TimeZone, request.Theme }, cancellationToken);
        await cacheService.RemoveAsync($"subscription:{tenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
