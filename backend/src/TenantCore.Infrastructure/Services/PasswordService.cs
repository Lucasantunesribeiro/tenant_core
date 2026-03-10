using Microsoft.AspNetCore.Identity;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Entities;

namespace TenantCore.Infrastructure.Services;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string Hash(string password)
    {
        return _passwordHasher.HashPassword(new User(Guid.Empty, string.Empty, string.Empty, string.Empty, Domain.Enums.UserRole.User), password);
    }

    public bool Verify(string password, string passwordHash)
    {
        var verification = _passwordHasher.VerifyHashedPassword(
            new User(Guid.Empty, string.Empty, string.Empty, string.Empty, Domain.Enums.UserRole.User),
            passwordHash,
            password);

        return verification is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
