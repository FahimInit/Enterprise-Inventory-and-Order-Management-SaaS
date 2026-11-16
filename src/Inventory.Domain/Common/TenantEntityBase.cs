namespace Inventory.Domain.Common;

public abstract class TenantEntityBase : EntityBase, ITenantEntity
{
    public Guid TenantId { get; protected set; }

    /// <summary>
    /// Mapped to PostgreSQL system column xmin for optimistic concurrency protection.
    /// </summary>
    public uint Version { get; private set; }

    protected TenantEntityBase()
    {
    }

    protected TenantEntityBase(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Tenant.Required", "A valid TenantId is required for all tenant-owned records.");
        }

        TenantId = tenantId;
    }
}
