using System.Linq.Expressions;
using System.Text.Json;
using Inventory.Application.Common;
using Inventory.Domain.Auditing;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Identity;
using Inventory.Domain.Inventory;
using Inventory.Domain.Organizations;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.StockTransfers;
using Inventory.Domain.Suppliers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Inventory.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantContext _tenantContext;

    public Guid? CurrentTenantId => _tenantContext.TenantId;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationRole> OrganizationRoles => Set<OrganizationRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<InventoryReconciliation> InventoryReconciliations => Set<InventoryReconciliation>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction is not null)
        {
            return Database.CurrentTransaction;
        }

        return await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(Guid WarehouseId, Guid ProductId), InventoryItem>> GetOrCreateAndLockInventoryItemsAsync(
        Guid tenantId,
        IEnumerable<(Guid WarehouseId, Guid ProductId)> targets,
        CancellationToken cancellationToken = default)
    {
        var sortedTargets = targets
            .Distinct()
            .OrderBy(t => t.WarehouseId)
            .ThenBy(t => t.ProductId)
            .ToList();

        var result = new Dictionary<(Guid WarehouseId, Guid ProductId), InventoryItem>(sortedTargets.Count);

        foreach (var (warehouseId, productId) in sortedTargets)
        {
            if (Database.IsRelational())
            {
                var newId = Guid.CreateVersion7();
                var now = DateTimeOffset.UtcNow;

                await Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO inventory_items (
                        id,
                        tenant_id,
                        warehouse_id,
                        product_id,
                        on_hand_quantity,
                        reserved_quantity,
                        in_transit_quantity,
                        created_at_utc
                    )
                    VALUES (
                        {newId},
                        {tenantId},
                        {warehouseId},
                        {productId},
                        0,
                        0,
                        0,
                        {now}
                    )
                    ON CONFLICT (tenant_id, warehouse_id, product_id) DO NOTHING;
                    """,
                    cancellationToken);

                var lockedItem = await InventoryItems
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM inventory_items
                        WHERE tenant_id = {tenantId}
                          AND warehouse_id = {warehouseId}
                          AND product_id = {productId}
                        FOR UPDATE
                        """)
                    .IgnoreQueryFilters()
                    .FirstAsync(cancellationToken);

                await Entry(lockedItem).ReloadAsync(cancellationToken);

                result[(warehouseId, productId)] = lockedItem;
            }
            else
            {
                var item = await InventoryItems
                    .FirstOrDefaultAsync(
                        i => i.TenantId == tenantId && i.WarehouseId == warehouseId && i.ProductId == productId,
                        cancellationToken);

                if (item is null)
                {
                    item = new InventoryItem(tenantId, warehouseId, productId);
                    InventoryItems.Add(item);
                    await SaveChangesAsync(cancellationToken);
                }

                result[(warehouseId, productId)] = item;
            }
        }

        return result;
    }

    public void RecordAudit(
        string action,
        string entityType,
        string entityId,
        string summary,
        object? beforeState = null,
        object? afterState = null,
        Guid? explicitTenantId = null)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var auditLog = new AuditLog(
            tenantId: explicitTenantId ?? _tenantContext.TenantId,
            actorUserId: _tenantContext.UserId,
            actorEmail: _tenantContext.UserEmail,
            action: action,
            entityType: entityType,
            entityId: entityId,
            summary: summary,
            beforeStateJson: beforeState is null ? null : JsonSerializer.Serialize(beforeState, jsonOptions),
            afterStateJson: afterState is null ? null : JsonSerializer.Serialize(afterState, jsonOptions),
            correlationId: _tenantContext.CorrelationId,
            ipAddress: _tenantContext.IpAddress);

        AuditLogs.Add(auditLog);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(CreateTenantFilterExpression(entityType.ClrType));
            }
        }
    }

    private LambdaExpression CreateTenantFilterExpression(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var tenantProperty = Expression.Property(parameter, nameof(ITenantEntity.TenantId));
        var currentTenantProperty = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        var nullableTenantProperty = Expression.Convert(tenantProperty, typeof(Guid?));
        var equality = Expression.Equal(nullableTenantProperty, currentTenantProperty);

        return Expression.Lambda(equality, parameter);
    }
}
