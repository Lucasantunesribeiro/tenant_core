using Microsoft.AspNetCore.Identity;
using TenantCore.Application.Common.Abstractions;

namespace TenantCore.Infrastructure.Services;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<string> _passwordHasher = new();

    public string Hash(string password)
    {
        return _passwordHasher.HashPassword(string.Empty, password);
    }

    public bool Verify(string password, string passwordHash)
    {
        var verification = _passwordHasher.VerifyHashedPassword(string.Empty, passwordHash, password);
        return verification is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
