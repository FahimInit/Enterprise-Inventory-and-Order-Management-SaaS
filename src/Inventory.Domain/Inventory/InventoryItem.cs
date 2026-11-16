using Inventory.Domain.Common;

namespace Inventory.Domain.Inventory;

public sealed class InventoryItem : TenantEntityBase
{
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? DefaultLocationId { get; private set; }
    public int OnHandQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int InTransitQuantity { get; private set; }
    public DateTimeOffset? LastCountedAtUtc { get; private set; }

    /// <summary>
    /// Authoritative formula: Available Stock = On-Hand Stock - Reserved Stock.
    /// </summary>
    public int AvailableQuantity => OnHandQuantity - ReservedQuantity;

    private InventoryItem()
    {
    }

    public InventoryItem(
        Guid tenantId,
        Guid warehouseId,
        Guid productId,
        Guid? defaultLocationId = null)
        : base(tenantId)
    {
        if (warehouseId == Guid.Empty)
        {
            throw new DomainException("Inventory.WarehouseRequired", "WarehouseId is required.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("Inventory.ProductRequired", "ProductId is required.");
        }

        WarehouseId = warehouseId;
        ProductId = productId;
        DefaultLocationId = defaultLocationId;
        OnHandQuantity = 0;
        ReservedQuantity = 0;
        InTransitQuantity = 0;
    }

    public void AssignDefaultLocation(Guid? locationId)
    {
        DefaultLocationId = locationId;
        TouchUpdatedTimestamp();
    }

    public StockMovement ReceiveStock(
        int quantity,
        decimal unitCost,
        string referenceType,
        string referenceNumber,
        Guid referenceId,
        string reason,
        string? notes,
        Guid? actorUserId)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Inventory.InvalidReceiptQuantity", "Received stock quantity must be greater than zero.");
        }

        var balanceBefore = OnHandQuantity;
        var balanceAfter = balanceBefore + quantity;
        OnHandQuantity = balanceAfter;
        TouchUpdatedTimestamp();

        return new StockMovement(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            StockMovementType.PurchaseReceipt,
            quantityDelta: quantity,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            unitCost: unitCost,
            referenceType: referenceType,
            referenceNumber: referenceNumber,
            referenceId: referenceId,
            reason: reason,
            notes: notes,
            createdByUserId: actorUserId);
    }

    public StockReservation ReserveStock(
        int quantity,
        Guid salesOrderId,
        Guid salesOrderLineId,
        string orderNumber)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Inventory.InvalidReservationQuantity", "Reservation quantity must be greater than zero.");
        }

        if (AvailableQuantity < quantity)
        {
            throw new ConflictException(
                "Inventory.InsufficientStock",
                $"Insufficient available stock for Product '{ProductId}' in Warehouse '{WarehouseId}'. Available: {AvailableQuantity}, Requested: {quantity}.");
        }

        ReservedQuantity += quantity;
        TouchUpdatedTimestamp();

        EnsureInvariants();

        return new StockReservation(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            salesOrderId,
            salesOrderLineId,
            orderNumber,
            quantity);
    }

    public void ReleaseReservation(StockReservation reservation, string reason)
    {
        if (reservation.InventoryItemId != Id)
        {
            throw new DomainException("Inventory.ReservationItemMismatch", "Reservation does not belong to this inventory item.");
        }

        reservation.MarkReleased(reason);

        if (ReservedQuantity < reservation.Quantity)
        {
            throw new ConflictException(
                "Inventory.ReservedBalanceCorrupted",
                $"Cannot release {reservation.Quantity} units because only {ReservedQuantity} units are currently marked reserved.");
        }

        ReservedQuantity -= reservation.Quantity;
        TouchUpdatedTimestamp();

        EnsureInvariants();
    }

    public StockMovement FulfillReservedStock(
        StockReservation reservation,
        decimal unitCost,
        string orderNumber,
        Guid? actorUserId)
    {
        if (reservation.InventoryItemId != Id)
        {
            throw new DomainException("Inventory.ReservationItemMismatch", "Reservation does not belong to this inventory item.");
        }

        if (ReservedQuantity < reservation.Quantity || OnHandQuantity < reservation.Quantity)
        {
            throw new ConflictException(
                "Inventory.InsufficientReservedOrOnHand",
                $"Cannot fulfill reservation of {reservation.Quantity} units. OnHand: {OnHandQuantity}, Reserved: {ReservedQuantity}.");
        }

        reservation.MarkFulfilled();

        var balanceBefore = OnHandQuantity;
        var balanceAfter = balanceBefore - reservation.Quantity;

        ReservedQuantity -= reservation.Quantity;
        OnHandQuantity = balanceAfter;
        TouchUpdatedTimestamp();

        EnsureInvariants();

        return new StockMovement(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            StockMovementType.SalesFulfillment,
            quantityDelta: -reservation.Quantity,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            unitCost: unitCost,
            referenceType: "SalesOrder",
            referenceNumber: orderNumber,
            referenceId: reservation.SalesOrderId,
            reason: $"Fulfilled sales order {orderNumber}",
            notes: null,
            createdByUserId: actorUserId);
    }

    public StockMovement AdjustStock(
        int quantityDelta,
        decimal unitCost,
        string reason,
        string? referenceNumber,
        string? notes,
        Guid? actorUserId)
    {
        if (quantityDelta == 0)
        {
            throw new DomainException("Inventory.ZeroAdjustmentForbidden", "Stock adjustment quantity delta cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Inventory.AdjustmentReasonRequired", "A valid business reason is required for stock adjustments.");
        }

        var balanceBefore = OnHandQuantity;
        var balanceAfter = balanceBefore + quantityDelta;

        if (balanceAfter < 0)
        {
            throw new ConflictException(
                "Inventory.NegativeStockForbidden",
                $"Adjustment of {quantityDelta} would result in negative on-hand stock ({balanceAfter}). Current on-hand: {balanceBefore}.");
        }

        if (balanceAfter < ReservedQuantity)
        {
            throw new ConflictException(
                "Inventory.AdjustmentBelowReservedStock",
                $"Cannot reduce on-hand stock to {balanceAfter} because {ReservedQuantity} units are currently reserved for active orders.");
        }

        OnHandQuantity = balanceAfter;
        TouchUpdatedTimestamp();

        EnsureInvariants();

        var movementType = quantityDelta > 0
            ? StockMovementType.AdjustmentIncrease
            : StockMovementType.AdjustmentDecrease;

        return new StockMovement(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            movementType,
            quantityDelta: quantityDelta,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            unitCost: unitCost,
            referenceType: "StockAdjustment",
            referenceNumber: string.IsNullOrWhiteSpace(referenceNumber) ? $"ADJ-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}" : referenceNumber,
            referenceId: null,
            reason: reason,
            notes: notes,
            createdByUserId: actorUserId);
    }

    public (InventoryReconciliation Reconciliation, StockMovement? AdjustmentMovement) ReconcilePhysicalCount(
        int countedQuantity,
        int ledgerCalculatedQuantity,
        decimal unitCost,
        string reason,
        string? notes,
        Guid? actorUserId)
    {
        if (countedQuantity < 0)
        {
            throw new DomainException("Inventory.NegativeCountForbidden", "Counted physical quantity cannot be negative.");
        }

        if (countedQuantity < ReservedQuantity)
        {
            throw new ConflictException(
                "Inventory.CountBelowReservedStock",
                $"Counted physical quantity ({countedQuantity}) is less than currently reserved quantity ({ReservedQuantity}). Release or reassign conflicting order reservations first.");
        }

        var balanceBefore = OnHandQuantity;
        var variance = countedQuantity - balanceBefore;
        LastCountedAtUtc = DateTimeOffset.UtcNow;

        var reconciliation = new InventoryReconciliation(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            systemQuantityBefore: balanceBefore,
            ledgerCalculatedQuantity: ledgerCalculatedQuantity,
            countedQuantity: countedQuantity,
            reason: reason,
            notes: notes,
            performedByUserId: actorUserId);

        StockMovement? movement = null;
        if (variance != 0)
        {
            OnHandQuantity = countedQuantity;
            EnsureInvariants();

            movement = new StockMovement(
                TenantId,
                Id,
                WarehouseId,
                ProductId,
                StockMovementType.ReconciliationAdjustment,
                quantityDelta: variance,
                balanceBefore: balanceBefore,
                balanceAfter: countedQuantity,
                unitCost: unitCost,
                referenceType: "InventoryReconciliation",
                referenceNumber: $"REC-{reconciliation.Id.ToString()[..8].ToUpperInvariant()}",
                referenceId: reconciliation.Id,
                reason: reason,
                notes: notes,
                createdByUserId: actorUserId);
        }

        TouchUpdatedTimestamp();
        return (reconciliation, movement);
    }

    public StockMovement DispatchTransferOut(
        int quantity,
        decimal unitCost,
        Guid transferId,
        string transferNumber,
        Guid? actorUserId)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Transfer.InvalidQuantity", "Transfer quantity must be greater than zero.");
        }

        if (AvailableQuantity < quantity)
        {
            throw new ConflictException(
                "Inventory.InsufficientStockForTransfer",
                $"Source warehouse has {AvailableQuantity} available units, which is insufficient to transfer {quantity} units.");
        }

        var balanceBefore = OnHandQuantity;
        var balanceAfter = balanceBefore - quantity;
        OnHandQuantity = balanceAfter;
        TouchUpdatedTimestamp();

        EnsureInvariants();

        return new StockMovement(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            StockMovementType.TransferOut,
            quantityDelta: -quantity,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            unitCost: unitCost,
            referenceType: "StockTransfer",
            referenceNumber: transferNumber,
            referenceId: transferId,
            reason: $"Dispatched outgoing transfer {transferNumber}",
            notes: null,
            createdByUserId: actorUserId);
    }

    public void AddInboundInTransit(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Transfer.InvalidQuantity", "In-transit quantity must be greater than zero.");
        }

        InTransitQuantity += quantity;
        TouchUpdatedTimestamp();
        EnsureInvariants();
    }

    public StockMovement ReceiveTransferIn(
        int quantity,
        decimal unitCost,
        Guid transferId,
        string transferNumber,
        Guid? actorUserId)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Transfer.InvalidQuantity", "Transfer receipt quantity must be greater than zero.");
        }

        if (InTransitQuantity < quantity)
        {
            throw new ConflictException(
                "Transfer.InTransitMismatch",
                $"Destination warehouse has {InTransitQuantity} units in transit, cannot receive {quantity} units.");
        }

        var balanceBefore = OnHandQuantity;
        var balanceAfter = balanceBefore + quantity;

        InTransitQuantity -= quantity;
        OnHandQuantity = balanceAfter;
        TouchUpdatedTimestamp();

        EnsureInvariants();

        return new StockMovement(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            StockMovementType.TransferIn,
            quantityDelta: quantity,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            unitCost: unitCost,
            referenceType: "StockTransfer",
            referenceNumber: transferNumber,
            referenceId: transferId,
            reason: $"Received incoming transfer {transferNumber}",
            notes: null,
            createdByUserId: actorUserId);
    }

    public void CancelInboundInTransit(int quantity)
    {
        if (InTransitQuantity < quantity)
        {
            throw new ConflictException(
                "Transfer.InTransitMismatch",
                $"Cannot cancel {quantity} in-transit units because only {InTransitQuantity} units are recorded in transit.");
        }

        InTransitQuantity -= quantity;
        TouchUpdatedTimestamp();
        EnsureInvariants();
    }

    public StockMovement RestoreCancelledTransferOut(
        int quantity,
        decimal unitCost,
        Guid transferId,
        string transferNumber,
        string cancellationReason,
        Guid? actorUserId)
    {
        var balanceBefore = OnHandQuantity;
        var balanceAfter = balanceBefore + quantity;
        OnHandQuantity = balanceAfter;
        TouchUpdatedTimestamp();

        EnsureInvariants();

        return new StockMovement(
            TenantId,
            Id,
            WarehouseId,
            ProductId,
            StockMovementType.TransferReversal,
            quantityDelta: quantity,
            balanceBefore: balanceBefore,
            balanceAfter: balanceAfter,
            unitCost: unitCost,
            referenceType: "StockTransfer",
            referenceNumber: transferNumber,
            referenceId: transferId,
            reason: $"Reversed cancelled in-transit transfer {transferNumber}: {cancellationReason}",
            notes: null,
            createdByUserId: actorUserId);
    }

    private void EnsureInvariants()
    {
        if (OnHandQuantity < 0)
        {
            throw new ConflictException("Inventory.NegativeOnHand", "On-hand stock cannot be negative.");
        }

        if (ReservedQuantity < 0)
        {
            throw new ConflictException("Inventory.NegativeReserved", "Reserved stock cannot be negative.");
        }

        if (InTransitQuantity < 0)
        {
            throw new ConflictException("Inventory.NegativeInTransit", "In-transit stock cannot be negative.");
        }

        if (ReservedQuantity > OnHandQuantity)
        {
            throw new ConflictException("Inventory.ReservedExceedsOnHand", "Reserved quantity cannot exceed on-hand quantity.");
        }
    }
}
