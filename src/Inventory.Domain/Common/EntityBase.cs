namespace Inventory.Domain.Common;

public abstract class EntityBase
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAtUtc { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void TouchUpdatedTimestamp()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
