using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Tasks.Queries;

public sealed record GetTasksQuery(
    string? Search,
    Guid? ProjectId,
    Guid? AssigneeUserId,
    WorkTaskStatus? Status,
    int Page = 1,
    int PageSize = 10) : IRequest<PagedResult<TaskListItem>>;

public sealed record TaskListItem(
    Guid Id,
    Guid ProjectId,
    string Title,
    string Description,
    WorkTaskStatus Status,
    TaskPriority Priority,
    string ProjectName,
    Guid? AssigneeUserId,
    string? AssigneeName,
    DateOnly? DueDate,
    DateTimeOffset UpdatedAtUtc);

internal sealed class GetTasksQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetTasksQuery, PagedResult<TaskListItem>>
{
    public async Task<PagedResult<TaskListItem>> Handle(GetTasksQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();

        var query = dbContext.Tasks.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Title.Contains(term) || x.Description.Contains(term));
        }

        if (request.ProjectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == request.ProjectId.Value);
        }

        if (request.AssigneeUserId.HasValue)
        {
            query = query.Where(x => x.AssigneeUserId == request.AssigneeUserId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        var pageSize = Math.Min(request.PageSize, 100);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.DueDate)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new TaskListItem(
                x.Id,
                x.ProjectId,
                x.Title,
                x.Description,
                x.Status,
                x.Priority,
                dbContext.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.Name).First(),
                x.AssigneeUserId,
                x.AssigneeUserId == null
                    ? null
                    : dbContext.Users.Where(u => u.Id == x.AssigneeUserId).Select(u => u.FullName).FirstOrDefault(),
                x.DueDate,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskListItem>(items, request.Page, pageSize, totalCount);
    }
}
