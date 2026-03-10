using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Users.Queries;

public sealed record GetUsersQuery(
    string? Search,
    UserRole? Role,
    int Page = 1,
    int PageSize = 10) : IRequest<PagedResult<UserListItem>>;

public sealed record UserListItem(
    Guid Id,
    string Email,
    string FullName,
    UserRole Role,
    bool InvitationPending,
    DateTimeOffset? LastLoginAtUtc);

internal sealed class GetUsersQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetUsersQuery, PagedResult<UserListItem>>
{
    public async Task<PagedResult<UserListItem>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email.Contains(term) || x.FullName.Contains(term));
        }

        if (request.Role.HasValue)
        {
            query = query.Where(x => x.Role == request.Role.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.FullName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new UserListItem(
                x.Id,
                x.Email,
                x.FullName,
                x.Role,
                x.InvitationPending,
                x.LastLoginAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItem>(items, request.Page, request.PageSize, totalCount);
    }
}
