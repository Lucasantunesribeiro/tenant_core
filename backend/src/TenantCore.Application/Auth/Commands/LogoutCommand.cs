using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Auth.Commands;

public sealed record LogoutCommand(string RefreshToken) : IRequest;

internal sealed class LogoutCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    ITokenService tokenService,
    IClock clock,
    IAuditService auditService) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentSession.GetRequiredTenantId();
        var hashedToken = tokenService.HashRefreshToken(request.RefreshToken);

        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == hashedToken && x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("invalid_refresh_token", "Invalid refresh token", 401, "The refresh token is invalid.");

        refreshToken.Revoke(clock.UtcNow);

        await auditService.WriteAsync("auth.logout", "RefreshToken", refreshToken.Id.ToString(), new { refreshToken.UserId }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
