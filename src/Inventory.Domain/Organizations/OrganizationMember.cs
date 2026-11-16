using Inventory.Domain.Common;

namespace Inventory.Domain.Organizations;

public sealed class OrganizationMember : TenantEntityBase
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsOwner { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset JoinedAtUtc { get; private set; } = DateTimeOffset.UtcNow;
    public Guid? InvitedByUserId { get; private set; }

    public OrganizationRole? Role { get; private set; }

    private OrganizationMember()
    {
    }

    public OrganizationMember(
        Guid tenantId,
        Guid userId,
        Guid roleId,
        bool isOwner = false,
        Guid? invitedByUserId = null)
        : base(tenantId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Member.UserIdRequired", "UserId is required.");
        }

        if (roleId == Guid.Empty)
        {
            throw new DomainException("Member.RoleIdRequired", "RoleId is required.");
        }

        UserId = userId;
        RoleId = roleId;
        IsOwner = isOwner;
        IsActive = true;
        InvitedByUserId = invitedByUserId;
    }

    public void ChangeRole(Guid newRoleId, bool isOwnerRole = false)
    {
        if (newRoleId == Guid.Empty)
        {
            throw new DomainException("Member.RoleIdRequired", "New RoleId is required.");
        }

        RoleId = newRoleId;
        IsOwner = isOwnerRole;
        TouchUpdatedTimestamp();
    }

    public void Deactivate()
    {
        if (IsOwner)
        {
            throw new DomainException("Member.CannotDeactivateOwner", "The primary organization owner cannot be deactivated without transferring ownership.");
        }

        IsActive = false;
        TouchUpdatedTimestamp();
    }

    public void Activate()
    {
        IsActive = true;
        TouchUpdatedTimestamp();
    }
}
