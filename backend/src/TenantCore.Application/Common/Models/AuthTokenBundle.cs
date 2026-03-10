namespace TenantCore.Application.Common.Models;

public sealed record AuthTokenBundle(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc);
