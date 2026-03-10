using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;

namespace TenantCore.Application.AuditLogs.Queries;

public sealed record GetAuditLogsQuery(
    string? Action,
    string? EntityType,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<AuditLogItem>>;

public sealed record AuditLogItem(
    Guid Id,
    Guid? ActorUserId,
    string Action,
    string EntityType,
    string EntityId,
    string CorrelationId,
    string MetadataJson,
    DateTimeOffset OccurredAtUtc);

internal sealed class GetAuditLogsQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogItem>>
{
    public async Task<PagedResult<AuditLogItem>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAdmin();

        var query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim().ToLowerInvariant();
            query = query.Where(x => x.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim().ToLowerInvariant();
            query = query.Where(x => x.EntityType.Contains(entityType));
        }

        var pageSize = Math.Min(request.PageSize, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogItem(
                x.Id,
                x.ActorUserId,
                x.Action,
                x.EntityType,
                x.EntityId,
                x.CorrelationId,
                x.MetadataJson,
                x.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogItem>(items, request.Page, pageSize, totalCount);
    }
}
