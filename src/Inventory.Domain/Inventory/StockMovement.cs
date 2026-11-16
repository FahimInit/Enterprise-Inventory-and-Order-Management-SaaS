using Inventory.Domain.Common;

namespace Inventory.Domain.Inventory;

public enum StockMovementType
{
    PurchaseReceipt = 1,
    SalesFulfillment = 2,
    TransferOut = 3,
    TransferIn = 4,
    TransferReversal = 5,
    AdjustmentIncrease = 6,
    AdjustmentDecrease = 7,
    ReconciliationAdjustment = 8
}

public sealed class StockMovement : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public Guid ProductId { get; private set; }
    public StockMovementType MovementType { get; private set; }
    public int QuantityDelta { get; private set; }
    public int BalanceBefore { get; private set; }
    public int BalanceAfter { get; private set; }
    public decimal UnitCost { get; private set; }
    public string ReferenceType { get; private set; } = string.Empty;
    public string ReferenceNumber { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    private StockMovement()
    {
    }

    public StockMovement(
        Guid tenantId,
        Guid inventoryItemId,
        Guid warehouseId,
        Guid productId,
        StockMovementType movementType,
        int quantityDelta,
        int balanceBefore,
        int balanceAfter,
        decimal unitCost,
        string referenceType,
        string referenceNumber,
        Guid? referenceId,
        string reason,
        string? notes,
        Guid? createdByUserId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("StockMovement.TenantRequired", "TenantId is required.");
        }

        if (quantityDelta == 0)
        {
            throw new DomainException("StockMovement.ZeroDeltaForbidden", "Stock movement quantity delta cannot be zero.");
        }

        if (balanceBefore + quantityDelta != balanceAfter)
        {
            throw new DomainException(
                "StockMovement.BalanceMathMismatch",
                $"Ledger balance math invariant violated: {balanceBefore} + ({quantityDelta}) != {balanceAfter}.");
        }

        if (balanceAfter < 0)
        {
            throw new ConflictException("Inventory.NegativeStockForbidden", "Stock balance after movement cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("StockMovement.ReasonRequired", "A reason is required for every stock movement.");
        }

        TenantId = tenantId;
        InventoryItemId = inventoryItemId;
        WarehouseId = warehouseId;
        ProductId = productId;
        MovementType = movementType;
        QuantityDelta = quantityDelta;
        BalanceBefore = balanceBefore;
        BalanceAfter = balanceAfter;
        UnitCost = decimal.Round(Math.Max(0m, unitCost), 4);
        ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? "Manual" : referenceType.Trim();
        ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? "N/A" : referenceNumber.Trim();
        ReferenceId = referenceId;
        Reason = reason.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }
}
