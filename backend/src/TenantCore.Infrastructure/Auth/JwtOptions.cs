namespace TenantCore.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "tenant_core";

    public string Audience { get; init; } = "tenant_core.web";

    // Must be overridden via environment variable or secrets manager — never use the default in production.
    // Minimum 32 characters; generate with: openssl rand -base64 48
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;

    public int RefreshTokenDays { get; init; } = 14;
}
