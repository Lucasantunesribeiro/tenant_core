using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Domain.Entities;

namespace TenantCore.Infrastructure.Auth;

public sealed class JwtTokenService(
    IOptions<JwtOptions> jwtOptions,
    IClock clock) : ITokenService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public AuthTokenBundle CreateTokenBundle(User user)
    {
        var now = clock.UtcNow;
        var accessTokenExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var refreshTokenExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(ClaimNames.TenantId, user.TenantId.ToString())
        };

        var descriptor = new JwtSecurityToken(
            _jwtOptions.Issuer,
            _jwtOptions.Audience,
            claims,
            now.UtcDateTime,
            accessTokenExpiresAt.UtcDateTime,
            credentials);

        return new AuthTokenBundle(
            new JwtSecurityTokenHandler().WriteToken(descriptor),
            GenerateRefreshToken(),
            accessTokenExpiresAt,
            refreshTokenExpiresAt);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshToken(string refreshToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(hash);
    }
}
