using Inventory.Domain.Catalog;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.Suppliers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.SubtotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 4);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });

        builder.HasOne<Supplier>()
            .WithMany()
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.TargetWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Receipts)
            .WithOne()
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("purchase_order_lines", t =>
        {
            t.HasCheckConstraint("ck_po_lines_ordered_positive", "ordered_quantity > 0");
            t.HasCheckConstraint("ck_po_lines_received_non_negative", "received_quantity >= 0");
            t.HasCheckConstraint("ck_po_lines_received_lte_ordered", "received_quantity <= ordered_quantity");
            t.HasCheckConstraint("ck_po_lines_unit_cost_non_negative", "unit_cost >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Ignore(x => x.RemainingQuantity);
        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
        builder.Property(x => x.LineTotal).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.TenantId, x.PurchaseOrderId, x.ProductId }).IsUnique();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ReceiptNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.ReceiptNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PurchaseOrderId });

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GoodsReceiptLineConfiguration : IEntityTypeConfiguration<GoodsReceiptLine>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptLine> builder)
    {
        builder.ToTable("goods_receipt_lines", t =>
        {
            t.HasCheckConstraint("ck_goods_receipt_lines_positive_qty", "quantity_received > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UnitCost).HasPrecision(18, 4);
    }
}

public sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("sales_orders");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerEmail).HasMaxLength(256);
        builder.Property(x => x.CustomerPhone).HasMaxLength(64);
        builder.Property(x => x.ShippingAddress).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.SubtotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.TaxRatePercent).HasPrecision(18, 4);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 4);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 4);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 4);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasIndex(x => new { x.TenantId, x.OrderNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAtUtc });

        builder.HasOne<Warehouse>()
            .WithMany()
            .HasForeignKey(x => x.FulfillmentWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SalesOrderLineConfiguration : IEntityTypeConfiguration<SalesOrderLine>
{
    public void Configure(EntityTypeBuilder<SalesOrderLine> builder)
    {
        builder.ToTable("sales_order_lines", t =>
        {
            t.HasCheckConstraint("ck_sales_order_lines_positive_qty", "quantity > 0");
            t.HasCheckConstraint("ck_sales_order_lines_price_non_negative", "unit_price >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4);
        builder.Property(x => x.LineTotal).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.TenantId, x.SalesOrderId, x.ProductId }).IsUnique();

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
