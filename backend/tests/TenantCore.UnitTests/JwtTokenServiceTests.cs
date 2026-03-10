using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;
using TenantCore.Infrastructure.Auth;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateTokenBundle_ShouldEmitExpectedClaimsAndRefreshTokenHash()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero));
        var service = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                Issuer = "tenant_core.tests",
                Audience = "tenant_core.tests.web",
                SigningKey = "test-signing-key-with-sufficient-length-12345",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7,
            }),
            clock);

        var tenantId = Guid.NewGuid();
        var user = new User(tenantId, "admin@acme.test", "Ava Stone", "hash", UserRole.Admin);

        var bundle = service.CreateTokenBundle(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(bundle.AccessToken);
        var secondBundle = service.CreateTokenBundle(user);
        var secondJwt = new JwtSecurityTokenHandler().ReadJwtToken(secondBundle.AccessToken);

        bundle.AccessToken.Should().NotBeNullOrWhiteSpace();
        bundle.RefreshToken.Should().NotBeNullOrWhiteSpace();
        bundle.AccessTokenExpiresAtUtc.Should().Be(clock.UtcNow.AddMinutes(15));
        bundle.RefreshTokenExpiresAtUtc.Should().Be(clock.UtcNow.AddDays(7));
        bundle.AccessToken.Should().NotBe(secondBundle.AccessToken);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(x => x.Type == JwtRegisteredClaimNames.Jti);
        jwt.Claims.Should().Contain(x => x.Type == "email" && x.Value == user.Email);
        jwt.Claims.Should().Contain(x => x.Type == ClaimTypes.Role && x.Value == user.Role.ToString());
        jwt.Claims.Should().Contain(x => x.Type == "tenantId" && x.Value == tenantId.ToString());
        jwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value
            .Should().NotBe(secondJwt.Claims.Single(x => x.Type == JwtRegisteredClaimNames.Jti).Value);

        var firstHash = service.HashRefreshToken(bundle.RefreshToken);
        var secondHash = service.HashRefreshToken(bundle.RefreshToken);

        firstHash.Should().Be(secondHash);
        firstHash.Should().NotBe(bundle.RefreshToken);
    }
}
