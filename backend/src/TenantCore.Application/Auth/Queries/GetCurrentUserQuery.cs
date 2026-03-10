using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Auth.Queries;

public sealed record GetCurrentUserQuery : IRequest<CurrentUserResponse>;

public sealed record CurrentUserResponse(
    Guid Id,
    Guid TenantId,
    string Email,
    string FullName,
    UserRole Role);

internal sealed class GetCurrentUserQueryHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession) : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse>
{
    public async Task<CurrentUserResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentSession.GetRequiredUserId();

        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new AppException("user_not_found", "User not found", 404, "The current user does not exist.");

        return new CurrentUserResponse(user.Id, user.TenantId, user.Email, user.FullName, user.Role);
    }
}
