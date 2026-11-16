namespace Inventory.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a specific tenant (Organization).
/// Every query and persistence operation on tenant entities must enforce tenant isolation.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; }
}
