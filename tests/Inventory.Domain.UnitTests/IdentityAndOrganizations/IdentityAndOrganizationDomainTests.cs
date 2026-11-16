using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Inventory.Domain.Organizations;

namespace Inventory.Domain.UnitTests.IdentityAndOrganizations;

public sealed class IdentityAndOrganizationDomainTests
{
    [Fact]
    public void ApplicationUser_AfterMaxFailedAttempts_ShouldLockAccount()
    {
        var user = new ApplicationUser("alice@acme.com", "Alice Smith", "hashed_pw");
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < ApplicationUser.MaxFailedLoginAttempts; i++)
        {
            user.RecordLoginFailure(now);
        }

        user.IsLockedOut(now).Should().BeTrue();
        var act = () => user.EnsureCanAuthenticate(now);
        act.Should().Throw<ForbiddenAccessException>()
            .Where(ex => ex.ErrorCode == "Auth.AccountLocked");
    }

    [Fact]
    public void RefreshToken_WhenMarkedRotated_ShouldNotBeActive()
    {
        var now = DateTimeOffset.UtcNow;
        var token = new RefreshToken(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "hash_1",
            Guid.CreateVersion7(),
            now.AddDays(7),
            "127.0.0.1");

        token.IsActive(now).Should().BeTrue();

        token.MarkAsRotated("hash_2");

        token.IsUsed.Should().BeTrue();
        token.ReplacedByTokenHash.Should().Be("hash_2");
        token.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void OrganizationRole_WithInvalidPermission_ShouldThrowDomainException()
    {
        var tenantId = Guid.CreateVersion7();

        var act = () => new OrganizationRole(
            tenantId,
            "Custom Role",
            "Test role",
            isSystemRole: false,
            ["catalog:read", "invalid:permission"]);

        act.Should().Throw<DomainException>()
            .Where(ex => ex.ErrorCode == "Role.UnknownPermission");
    }

    [Fact]
    public void OrganizationMember_DeactivatingPrimaryOwner_ShouldThrowDomainException()
    {
        var member = new OrganizationMember(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            isOwner: true);

        var act = () => member.Deactivate();

        act.Should().Throw<DomainException>()
            .Where(ex => ex.ErrorCode == "Member.CannotDeactivateOwner");
    }
}
