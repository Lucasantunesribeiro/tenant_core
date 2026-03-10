using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public sealed record AuthenticatedUserDto(
    Guid Id,
    Guid TenantId,
    string Email,
    string FullName,
    UserRole Role);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    AuthenticatedUserDto User,
    string TenantName,
    PlanCode PlanCode);

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

internal sealed class LoginCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IPasswordService passwordService,
    ITokenService tokenService,
    IClock clock,
    IAuditService auditService) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentSession.GetRequiredTenantId();

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == tenantId && x.IsActive, cancellationToken)
            ?? throw new AppException("tenant_not_found", "Tenant not found", 404, "The provided tenant does not exist.");

        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant() && x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("invalid_credentials", "Invalid credentials", 401, "The provided credentials are invalid.");

        if (!passwordService.Verify(request.Password, user.PasswordHash))
        {
            throw new AppException("invalid_credentials", "Invalid credentials", 401, "The provided credentials are invalid.");
        }

        var tokenBundle = tokenService.CreateTokenBundle(user);
        var refreshToken = new RefreshToken(
            tenantId,
            user.Id,
            tokenService.HashRefreshToken(tokenBundle.RefreshToken),
            tokenBundle.RefreshTokenExpiresAtUtc,
            string.Empty,
            string.Empty);

        user.MarkLogin(clock.UtcNow);

        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await auditService.WriteAsync("auth.login", "User", user.Id.ToString(), new { user.Email }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var subscription = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .SingleAsync(x => x.TenantId == tenantId, cancellationToken);

        return new LoginResponse(
            tokenBundle.AccessToken,
            tokenBundle.AccessTokenExpiresAtUtc,
            tokenBundle.RefreshToken,
            tokenBundle.RefreshTokenExpiresAtUtc,
            new AuthenticatedUserDto(user.Id, user.TenantId, user.Email, user.FullName, user.Role),
            tenant.Name,
            subscription.PlanCode);
    }
}
