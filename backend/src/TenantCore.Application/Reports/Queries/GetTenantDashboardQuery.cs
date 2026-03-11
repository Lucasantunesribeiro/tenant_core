using MediatR;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Reports.Queries;

public sealed record GetTenantDashboardQuery : IRequest<TenantProjectDashboard>;

internal sealed class GetTenantDashboardQueryHandler(
    ITenantDashboardRepository dashboardRepository,
    ICurrentSession currentSession) : IRequestHandler<GetTenantDashboardQuery, TenantProjectDashboard>
{
    public async Task<TenantProjectDashboard> Handle(GetTenantDashboardQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();

        var tenantId = currentSession.GetRequiredTenantId();

        var result = await dashboardRepository.GetAsync(tenantId, cancellationToken);

        if (result.TenantName is null)
        {
            throw new AppException(
                "tenant_not_found",
                "Tenant not found.",
                404,
                "Tenant not found.");
        }

        return result;
    }
}
