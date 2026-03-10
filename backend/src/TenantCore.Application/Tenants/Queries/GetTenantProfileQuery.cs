using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Tenants.Queries;

public sealed record GetTenantProfileQuery : IRequest<TenantProfileResponse>;

public sealed record TenantProfileResponse(
    Guid Id,
    string Name,
    string Slug,
    string BillingEmail,
    string SupportEmail,
    string TimeZone,
    string Theme,
    string AllowedDomains);

internal sealed class GetTenantProfileQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetTenantProfileQuery, TenantProfileResponse>
{
    public async Task<TenantProfileResponse> Handle(GetTenantProfileQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();
        var tenantId = currentSession.GetRequiredTenantId();

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new AppException("tenant_not_found", "Tenant not found", 404, "The tenant does not exist.");

        return new TenantProfileResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.BillingEmail,
            tenant.SupportEmail,
            tenant.TimeZone,
            tenant.Theme,
            tenant.AllowedDomains);
    }
}
