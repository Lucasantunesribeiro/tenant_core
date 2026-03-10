using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Email,
    string FullName,
    string Password,
    UserRole Role,
    bool InvitationPending) : IRequest<Guid>;

internal sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

internal sealed class CreateUserCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IPasswordService passwordService,
    IPlanLimitService planLimitService,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAdmin();
        await planLimitService.EnsureUserSlotAvailableAsync(cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Email == email, cancellationToken);

        if (exists)
        {
            throw new AppException("user_conflict", "User already exists", 409, "A user with this email already exists.");
        }

        var user = new User(
            currentSession.GetRequiredTenantId(),
            email,
            request.FullName.Trim(),
            passwordService.Hash(request.Password),
            request.Role,
            request.InvitationPending);

        await dbContext.Users.AddAsync(user, cancellationToken);
        await auditService.WriteAsync("user.created", "User", user.Id.ToString(), new { user.Email, request.Role }, cancellationToken);
        await cacheService.RemoveAsync($"usage:{user.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
