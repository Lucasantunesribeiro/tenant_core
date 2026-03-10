namespace TenantCore.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "tenant_core";

    public string Audience { get; init; } = "tenant_core.web";

    public string SigningKey { get; init; } = "super-secret-signing-key-change-me";

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 14;
}
