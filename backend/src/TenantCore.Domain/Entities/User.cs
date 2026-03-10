using TenantCore.Domain.Common;
using TenantCore.Domain.Enums;

namespace TenantCore.Domain.Entities;

public sealed class User : TenantOwnedEntity
{
    private User()
    {
    }

    public User(
        Guid tenantId,
        string email,
        string fullName,
        string passwordHash,
        UserRole role,
        bool invitationPending = false)
    {
        TenantId = tenantId;
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        InvitationPending = invitationPending;
        IsActive = true;
    }

    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public bool InvitationPending { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public void UpdateProfile(string fullName, DateTimeOffset now)
    {
        FullName = fullName;
        Touch(now);
    }

    public void ChangeRole(UserRole role, DateTimeOffset now)
    {
        Role = role;
        Touch(now);
    }

    public void MarkLogin(DateTimeOffset now)
    {
        InvitationPending = false;
        LastLoginAtUtc = now;
        Touch(now);
    }

    public void UpdatePasswordHash(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        Touch(now);
    }
}
