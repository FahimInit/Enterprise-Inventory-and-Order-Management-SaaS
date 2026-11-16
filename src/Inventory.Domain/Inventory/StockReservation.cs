using Inventory.Domain.Common;

namespace Inventory.Domain.Inventory;

public enum ReservationStatus
{
    Active = 1,
    Fulfilled = 2,
    Released = 3
}

public sealed class StockReservation : TenantEntityBase
{
    public Guid InventoryItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid SalesOrderLineId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; } = ReservationStatus.Active;
    public string? ReleaseReason { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    private StockReservation()
    {
    }

    public StockReservation(
        Guid tenantId,
        Guid inventoryItemId,
        Guid warehouseId,
        Guid productId,
        Guid salesOrderId,
        Guid salesOrderLineId,
        string orderNumber,
        int quantity)
        : base(tenantId)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Reservation.InvalidQuantity", "Reserved quantity must be greater than zero.");
        }

        InventoryItemId = inventoryItemId;
        WarehouseId = warehouseId;
        ProductId = productId;
        SalesOrderId = salesOrderId;
        SalesOrderLineId = salesOrderLineId;
        OrderNumber = orderNumber.Trim();
        Quantity = quantity;
        Status = ReservationStatus.Active;
    }

    internal void MarkReleased(string reason)
    {
        if (Status != ReservationStatus.Active)
        {
            throw new DomainException("Reservation.NotActive", $"Cannot release reservation in '{Status}' status.");
        }

        Status = ReservationStatus.Released;
        ReleaseReason = string.IsNullOrWhiteSpace(reason) ? "Order cancelled" : reason.Trim();
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    internal void MarkFulfilled()
    {
        if (Status != ReservationStatus.Active)
        {
            throw new DomainException("Reservation.NotActive", $"Cannot fulfill reservation in '{Status}' status.");
        }

        Status = ReservationStatus.Fulfilled;
        ResolvedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }
}

public sealed class InventoryReconciliation : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public int SystemQuantityBefore { get; private set; }
    public int LedgerCalculatedQuantity { get; private set; }
    public int CountedQuantity { get; private set; }
    public int VarianceQuantity { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? PerformedByUserId { get; private set; }
    public DateTimeOffset PerformedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private InventoryReconciliation()
    {
    }

    public InventoryReconciliation(
        Guid tenantId,
        Guid inventoryItemId,
        Guid warehouseId,
        Guid productId,
        int systemQuantityBefore,
        int ledgerCalculatedQuantity,
        int countedQuantity,
        string reason,
        string? notes,
        Guid? performedByUserId)
    {
        TenantId = tenantId;
        InventoryItemId = inventoryItemId;
        WarehouseId = warehouseId;
        ProductId = productId;
        SystemQuantityBefore = systemQuantityBefore;
        LedgerCalculatedQuantity = ledgerCalculatedQuantity;
        CountedQuantity = countedQuantity;
        VarianceQuantity = countedQuantity - systemQuantityBefore;
        Reason = reason.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        PerformedByUserId = performedByUserId;
        PerformedAtUtc = DateTimeOffset.UtcNow;
    }
}
