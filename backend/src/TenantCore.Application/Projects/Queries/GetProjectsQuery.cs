using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Projects.Queries;

public sealed record GetProjectsQuery(
    string? Search,
    ProjectStatus? Status,
    int Page = 1,
    int PageSize = 10) : IRequest<PagedResult<ProjectListItem>>;

public sealed record ProjectListItem(
    Guid Id,
    string Name,
    string Code,
    ProjectStatus Status,
    Guid? ClientId,
    string? ClientName,
    Guid? OwnerUserId,
    string? OwnerName,
    DateOnly? DueDate,
    int TaskCount,
    DateTimeOffset UpdatedAtUtc);

internal sealed class GetProjectsQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetProjectsQuery, PagedResult<ProjectListItem>>
{
    public async Task<PagedResult<ProjectListItem>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();

        var query = dbContext.Projects.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.Contains(term) || x.Code.Contains(term) || x.Description.Contains(term));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        var pageSize = Math.Min(request.PageSize, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Name)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProjectListItem(
                x.Id,
                x.Name,
                x.Code,
                x.Status,
                x.ClientId,
                x.ClientId == null
                    ? null
                    : dbContext.Clients.Where(c => c.Id == x.ClientId).Select(c => c.Name).FirstOrDefault(),
                x.OwnerUserId,
                x.OwnerUserId == null
                    ? null
                    : dbContext.Users.Where(u => u.Id == x.OwnerUserId).Select(u => u.FullName).FirstOrDefault(),
                x.DueDate,
                dbContext.Tasks.Count(t => t.ProjectId == x.Id),
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProjectListItem>(items, request.Page, pageSize, totalCount);
    }
}
