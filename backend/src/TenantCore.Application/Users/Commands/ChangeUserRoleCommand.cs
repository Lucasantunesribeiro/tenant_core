using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Users.Commands;

public sealed record ChangeUserRoleCommand(Guid UserId, UserRole Role) : IRequest;

internal sealed class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

internal sealed class ChangeUserRoleCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService) : IRequestHandler<ChangeUserRoleCommand>
{
    public async Task Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAdmin();

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new AppException("user_not_found", "User not found", 404, "The requested user does not exist.");

        user.ChangeRole(request.Role, clock.UtcNow);

        await auditService.WriteAsync("user.role_changed", "User", user.Id.ToString(), new { request.Role }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
