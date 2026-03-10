using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;

namespace TenantCore.Application.Auth.Commands;

public sealed record RefreshSessionCommand(string RefreshToken) : IRequest<RefreshSessionResponse>;

public sealed record RefreshSessionResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);

internal sealed class RefreshSessionCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    ITokenService tokenService,
    IClock clock) : IRequestHandler<RefreshSessionCommand, RefreshSessionResponse>
{
    public async Task<RefreshSessionResponse> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentSession.GetRequiredTenantId();
        var hashedToken = tokenService.HashRefreshToken(request.RefreshToken);

        var existingToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(x => x.TokenHash == hashedToken && x.TenantId == tenantId, cancellationToken)
            ?? throw new AppException("invalid_refresh_token", "Invalid refresh token", 401, "The refresh token is invalid or expired.");

        if (!existingToken.IsActive(clock.UtcNow))
        {
            throw new AppException("invalid_refresh_token", "Invalid refresh token", 401, "The refresh token is invalid or expired.");
        }

        var user = await dbContext.Users
            .SingleAsync(x => x.Id == existingToken.UserId, cancellationToken);

        var bundle = tokenService.CreateTokenBundle(user);
        var newTokenHash = tokenService.HashRefreshToken(bundle.RefreshToken);
        existingToken.Rotate(newTokenHash, clock.UtcNow);

        await dbContext.RefreshTokens.AddAsync(
            new RefreshToken(
                tenantId,
                user.Id,
                newTokenHash,
                bundle.RefreshTokenExpiresAtUtc,
                string.Empty,
                string.Empty),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshSessionResponse(
            bundle.AccessToken,
            bundle.AccessTokenExpiresAtUtc,
            bundle.RefreshToken,
            bundle.RefreshTokenExpiresAtUtc);
    }
}
