using Inventory.Domain.Auditing;
using Inventory.Domain.Catalog;
using Inventory.Domain.Identity;
using Inventory.Domain.Inventory;
using Inventory.Domain.Organizations;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.StockTransfers;
using Inventory.Domain.Suppliers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Application.Common;

public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<OrganizationRole> OrganizationRoles { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<OrganizationMember> OrganizationMembers { get; }
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<SupplierProduct> SupplierProducts { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseLocation> WarehouseLocations { get; }
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<StockMovement> StockMovements { get; }
    DbSet<StockReservation> StockReservations { get; }
    DbSet<InventoryReconciliation> InventoryReconciliations { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockTransferLine> StockTransferLines { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderLine> SalesOrderLines { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }

    DatabaseFacade Database { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<(Guid WarehouseId, Guid ProductId), InventoryItem>> GetOrCreateAndLockInventoryItemsAsync(
        Guid tenantId,
        IEnumerable<(Guid WarehouseId, Guid ProductId)> targets,
        CancellationToken cancellationToken = default);

    void RecordAudit(
        string action,
        string entityType,
        string entityId,
        string summary,
        object? beforeState = null,
        object? afterState = null,
        Guid? explicitTenantId = null);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
