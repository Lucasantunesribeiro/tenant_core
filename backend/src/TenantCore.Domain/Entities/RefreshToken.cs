using TenantCore.Domain.Common;

namespace TenantCore.Domain.Entities;

public sealed class RefreshToken : TenantOwnedEntity
{
    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAtUtc,
        string createdByIp,
        string userAgent)
    {
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
        UserAgent = userAgent;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public string? ReplacedByTokenHash { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public DateTimeOffset? LastUsedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public string CreatedByIp { get; private set; } = string.Empty;

    public string UserAgent { get; private set; } = string.Empty;

    public bool IsExpired(DateTimeOffset now) => ExpiresAtUtc <= now;

    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && !IsExpired(now);

    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAtUtc = now;
        Touch(now);
    }

    public void Rotate(string replacementTokenHash, DateTimeOffset now)
    {
        ReplacedByTokenHash = replacementTokenHash;
        RevokedAtUtc = now;
        Touch(now);
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAtUtc = now;
        Touch(now);
    }
}
