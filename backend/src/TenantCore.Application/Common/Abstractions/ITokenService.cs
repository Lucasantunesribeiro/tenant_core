using TenantCore.Application.Common.Models;
using TenantCore.Domain.Entities;

namespace TenantCore.Application.Common.Abstractions;

public interface ITokenService
{
    AuthTokenBundle CreateTokenBundle(User user);

    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}
