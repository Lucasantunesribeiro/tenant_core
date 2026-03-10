using FluentAssertions;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;
using TenantCore.UnitTests.Common;

namespace TenantCore.UnitTests;

public sealed class RoleGuardTests
{
    [Fact]
    public void EnsureManagerOrAdmin_ShouldRejectRegularUser()
    {
        var currentSession = new TestCurrentSession
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Role = UserRole.User,
        };

        var action = () => CurrentSessionExtensions.EnsureManagerOrAdmin(currentSession);

        var exception = action.Should().Throw<AppException>();
        exception.Which.StatusCode.Should().Be(403);
        exception.Which.Message.Should().Contain("Manager or Admin");
    }

    [Fact]
    public void EnsureAdmin_ShouldAllowAdmin()
    {
        var currentSession = new TestCurrentSession
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Role = UserRole.Admin,
        };

        var action = () => CurrentSessionExtensions.EnsureAdmin(currentSession);

        action.Should().NotThrow();
    }
}
