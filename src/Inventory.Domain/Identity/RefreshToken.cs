using Inventory.Domain.Common;

namespace Inventory.Domain.Identity;

public sealed class RefreshToken : EntityBase
{
    public Guid UserId { get; private set; }
    public Guid? ActiveTenantId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool IsUsed { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? RevokedReason { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string? CreatedByIp { get; private set; }

    private RefreshToken()
    {
    }

    public RefreshToken(
        Guid userId,
        Guid? activeTenantId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset expiresAtUtc,
        string? createdByIp)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("RefreshToken.UserIdRequired", "UserId is required.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainException("RefreshToken.HashRequired", "Token hash is required.");
        }

        UserId = userId;
        ActiveTenantId = activeTenantId;
        TokenHash = tokenHash;
        FamilyId = familyId == Guid.Empty ? Guid.CreateVersion7() : familyId;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
    }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && !IsUsed && !IsExpired(nowUtc);

    public void MarkAsRotated(string newTokenHash)
    {
        IsUsed = true;
        ReplacedByTokenHash = newTokenHash;
        TouchUpdatedTimestamp();
    }

    public void Revoke(string reason)
    {
        IsRevoked = true;
        RevokedReason = reason;
        TouchUpdatedTimestamp();
    }
}
