using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Clients.Queries;

public sealed record GetClientsQuery(
    string? Search,
    ClientStatus? Status,
    int Page = 1,
    int PageSize = 10) : IRequest<PagedResult<ClientListItem>>;

public sealed record ClientListItem(
    Guid Id,
    string Name,
    string Email,
    string ContactName,
    ClientStatus Status,
    string Notes,
    DateTimeOffset UpdatedAtUtc);

internal sealed class GetClientsQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetClientsQuery, PagedResult<ClientListItem>>
{
    public async Task<PagedResult<ClientListItem>> Handle(GetClientsQuery request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();

        var query = dbContext.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Name.Contains(term) ||
                x.Email.Contains(term) ||
                x.ContactName.Contains(term));
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
            .Select(x => new ClientListItem(
                x.Id,
                x.Name,
                x.Email,
                x.ContactName,
                x.Status,
                x.Notes,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientListItem>(items, request.Page, pageSize, totalCount);
    }
}
