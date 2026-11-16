using Inventory.Domain.Catalog;
using Inventory.Domain.Inventory;
using Inventory.Domain.StockTransfers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("inventory_items", t =>
        {
            t.HasCheckConstraint("ck_inventory_items_on_hand_non_negative", "on_hand_quantity >= 0");
            t.HasCheckConstraint("ck_inventory_items_reserved_non_negative", "reserved_quantity >= 0");
            t.HasCheckConstraint("ck_inventory_items_in_transit_non_negative", "in_transit_quantity >= 0");
            t.HasCheckConstraint("ck_inventory_items_reserved_lte_on_hand", "reserved_quantity <= on_hand_quantity");
        });

        builder.HasKey(x => x.Id);

        builder.Ignore(x => x.AvailableQuantity);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.WarehouseId, x.ProductId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProductId });

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WarehouseLocation>()
            .WithMany()
            .HasForeignKey(x => x.DefaultLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", t =>
        {
            t.HasCheckConstraint("ck_stock_movements_nonzero_delta", "quantity_delta <> 0");
            t.HasCheckConstraint("ck_stock_movements_balance_math", "balance_before + quantity_delta = balance_after");
            t.HasCheckConstraint("ck_stock_movements_balance_after_non_negative", "balance_after >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.ReferenceType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.InventoryItemId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.WarehouseId, x.ProductId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.ReferenceType, x.ReferenceId });

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations", t =>
        {
            t.HasCheckConstraint("ck_stock_reservations_positive_qty", "quantity > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.ReleaseReason).HasMaxLength(300);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.SalesOrderId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.InventoryItemId, x.Status });

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class InventoryReconciliationConfiguration : IEntityTypeConfiguration<InventoryReconciliation>
{
    public void Configure(EntityTypeBuilder<InventoryReconciliation> builder)
    {
        builder.ToTable("inventory_reconciliations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.InventoryItemId, x.PerformedAtUtc });
    }
}

public sealed class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransferNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.TransferNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.SourceWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.DestinationWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.StockTransferId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class StockTransferLineConfiguration : IEntityTypeConfiguration<StockTransferLine>
{
    public void Configure(EntityTypeBuilder<StockTransferLine> builder)
    {
        builder.ToTable("stock_transfer_lines", t =>
        {
            t.HasCheckConstraint("ck_stock_transfer_lines_positive_qty", "quantity > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Notes).HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.StockTransferId, x.ProductId }).IsUnique();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
