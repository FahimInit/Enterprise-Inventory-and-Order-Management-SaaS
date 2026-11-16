using Inventory.Domain.Common;

namespace Inventory.Domain.PurchaseOrders;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    PartiallyReceived = 4,
    Received = 5,
    Cancelled = 6
}

public sealed class PurchaseOrder : TenantEntityBase
{
    private readonly List<PurchaseOrderLine> _lines = [];
    private readonly List<GoodsReceipt> _receipts = [];

    public string OrderNumber { get; private set; } = string.Empty;
    public Guid SupplierId { get; private set; }
    public Guid TargetWarehouseId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; } = PurchaseOrderStatus.Draft;
    public decimal SubtotalAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTimeOffset? ExpectedDeliveryDateUtc { get; private set; }
    public string? Notes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<PurchaseOrderLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<GoodsReceipt> Receipts => _receipts.AsReadOnly();

    private PurchaseOrder()
    {
    }

    public PurchaseOrder(
        Guid tenantId,
        string orderNumber,
        Guid supplierId,
        Guid targetWarehouseId,
        DateTimeOffset? expectedDeliveryDateUtc,
        decimal taxRatePercent,
        string? notes,
        Guid? createdByUserId,
        IEnumerable<(Guid ProductId, int OrderedQuantity, decimal UnitCost)> lines)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("PurchaseOrder.OrderNumberRequired", "Purchase order number is required.");
        }

        if (supplierId == Guid.Empty)
        {
            throw new DomainException("PurchaseOrder.SupplierRequired", "SupplierId is required.");
        }

        if (targetWarehouseId == Guid.Empty)
        {
            throw new DomainException("PurchaseOrder.WarehouseRequired", "TargetWarehouseId is required.");
        }

        if (taxRatePercent < 0m || taxRatePercent > 100m)
        {
            throw new DomainException("PurchaseOrder.InvalidTaxRate", "Tax rate percent must be between 0 and 100.");
        }

        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        SupplierId = supplierId;
        TargetWarehouseId = targetWarehouseId;
        Status = PurchaseOrderStatus.Draft;
        ExpectedDeliveryDateUtc = expectedDeliveryDateUtc;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;

        var grouped = lines
            .GroupBy(x => x.ProductId)
            .Select(g => (
                ProductId: g.Key,
                OrderedQuantity: g.Sum(x => x.OrderedQuantity),
                UnitCost: g.First().UnitCost))
            .ToList();

        if (grouped.Count == 0)
        {
            throw new DomainException("PurchaseOrder.EmptyLines", "A purchase order must contain at least one product line.");
        }

        foreach (var item in grouped)
        {
            _lines.Add(new PurchaseOrderLine(TenantId, Id, item.ProductId, item.OrderedQuantity, item.UnitCost));
        }

        RecalculateTotals(taxRatePercent);
    }

    public void Submit()
    {
        if (Status != PurchaseOrderStatus.Draft)
        {
            throw new ConflictException(
                "PurchaseOrder.InvalidStateForSubmit",
                $"Cannot submit purchase order '{OrderNumber}' when status is '{Status}'.");
        }

        if (_lines.Count == 0)
        {
            throw new DomainException("PurchaseOrder.EmptyLines", "Cannot submit an empty purchase order.");
        }

        Status = PurchaseOrderStatus.Submitted;
        SubmittedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    public void Approve(Guid? approverUserId)
    {
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted))
        {
            throw new ConflictException(
                "PurchaseOrder.InvalidStateForApprove",
                $"Cannot approve purchase order '{OrderNumber}' when status is '{Status}'.");
        }

        Status = PurchaseOrderStatus.Approved;
        ApprovedByUserId = approverUserId;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    public GoodsReceipt ReceiveGoods(
        string receiptNumber,
        string? notes,
        Guid? receivedByUserId,
        IEnumerable<(Guid PurchaseOrderLineId, int QuantityReceived)> receiptQuantities)
    {
        if (Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
        {
            throw new ConflictException(
                "PurchaseOrder.InvalidStateForReceive",
                $"Goods can only be received against 'Approved' or 'PartiallyReceived' purchase orders. Current status: '{Status}'.");
        }

        var itemsToReceive = receiptQuantities
            .Where(x => x.QuantityReceived > 0)
            .ToList();

        if (itemsToReceive.Count == 0)
        {
            throw new DomainException("PurchaseOrder.EmptyReceipt", "At least one line with a positive received quantity must be provided.");
        }

        var receiptLines = new List<GoodsReceiptLine>();
        var receiptId = Guid.CreateVersion7();

        foreach (var (lineId, qtyReceived) in itemsToReceive)
        {
            var poLine = _lines.FirstOrDefault(l => l.Id == lineId)
                ?? throw new NotFoundException(nameof(PurchaseOrderLine), lineId);

            poLine.RecordReceipt(qtyReceived);

            receiptLines.Add(new GoodsReceiptLine(
                TenantId,
                receiptId,
                poLine.Id,
                poLine.ProductId,
                qtyReceived,
                poLine.UnitCost));
        }

        var allReceived = _lines.All(l => l.ReceivedQuantity >= l.OrderedQuantity);
        Status = allReceived ? PurchaseOrderStatus.Received : PurchaseOrderStatus.PartiallyReceived;

        if (allReceived)
        {
            CompletedAtUtc = DateTimeOffset.UtcNow;
        }

        var receipt = new GoodsReceipt(
            receiptId,
            TenantId,
            Id,
            TargetWarehouseId,
            receiptNumber,
            notes,
            receivedByUserId,
            receiptLines);

        _receipts.Add(receipt);
        TouchUpdatedTimestamp();

        return receipt;
    }

    public void Cancel(string reason)
    {
        if (Status is PurchaseOrderStatus.Received or PurchaseOrderStatus.PartiallyReceived)
        {
            throw new ConflictException(
                "PurchaseOrder.CannotCancelAfterReceipt",
                $"Purchase order '{OrderNumber}' already has received inventory and cannot be cancelled.");
        }

        if (Status == PurchaseOrderStatus.Cancelled)
        {
            throw new ConflictException("PurchaseOrder.AlreadyCancelled", "Purchase order is already cancelled.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("PurchaseOrder.CancelReasonRequired", "A cancellation reason is required.");
        }

        Status = PurchaseOrderStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    private void RecalculateTotals(decimal taxRatePercent)
    {
        SubtotalAmount = decimal.Round(_lines.Sum(l => l.LineTotal), 4);
        TaxAmount = decimal.Round(SubtotalAmount * (taxRatePercent / 100m), 4);
        TotalAmount = decimal.Round(SubtotalAmount + TaxAmount, 4);
    }
}

public sealed class PurchaseOrderLine : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int OrderedQuantity { get; private set; }
    public int ReceivedQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal LineTotal { get; private set; }

    public int RemainingQuantity => OrderedQuantity - ReceivedQuantity;

    private PurchaseOrderLine()
    {
    }

    public PurchaseOrderLine(
        Guid tenantId,
        Guid purchaseOrderId,
        Guid productId,
        int orderedQuantity,
        decimal unitCost)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("PurchaseOrderLine.ProductRequired", "ProductId is required.");
        }

        if (orderedQuantity <= 0)
        {
            throw new DomainException("PurchaseOrderLine.InvalidQuantity", "Ordered quantity must be greater than zero.");
        }

        if (unitCost < 0m)
        {
            throw new DomainException("PurchaseOrderLine.NegativeCost", "Unit cost cannot be negative.");
        }

        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        ProductId = productId;
        OrderedQuantity = orderedQuantity;
        ReceivedQuantity = 0;
        UnitCost = decimal.Round(unitCost, 4);
        LineTotal = decimal.Round(orderedQuantity * UnitCost, 4);
    }

    internal void RecordReceipt(int quantityReceived)
    {
        if (quantityReceived <= 0)
        {
            throw new DomainException("PurchaseOrderLine.InvalidReceiptQuantity", "Received quantity must be greater than zero.");
        }

        if (ReceivedQuantity + quantityReceived > OrderedQuantity)
        {
            throw new ConflictException(
                "PurchaseOrderLine.OverReceiptForbidden",
                $"Cannot receive {quantityReceived} units for line '{Id}' because only {RemainingQuantity} units remain on order (Ordered: {OrderedQuantity}, Already Received: {ReceivedQuantity}).");
        }

        ReceivedQuantity += quantityReceived;
        TouchUpdatedTimestamp();
    }
}

public sealed class GoodsReceipt : EntityBase, ITenantEntity
{
    private readonly List<GoodsReceiptLine> _lines = [];

    public Guid TenantId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public Guid WarehouseId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }
    public DateTimeOffset ReceivedAtUtc { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<GoodsReceiptLine> Lines => _lines.AsReadOnly();

    private GoodsReceipt()
    {
    }

    public GoodsReceipt(
        Guid id,
        Guid tenantId,
        Guid purchaseOrderId,
        Guid warehouseId,
        string receiptNumber,
        string? notes,
        Guid? receivedByUserId,
        IEnumerable<GoodsReceiptLine> lines)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        WarehouseId = warehouseId;
        ReceiptNumber = receiptNumber.Trim().ToUpperInvariant();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ReceivedByUserId = receivedByUserId;
        ReceivedAtUtc = DateTimeOffset.UtcNow;
        _lines.AddRange(lines);
    }
}

public sealed class GoodsReceiptLine : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid GoodsReceiptId { get; private set; }
    public Guid PurchaseOrderLineId { get; private set; }
    public Guid ProductId { get; private set; }
    public int QuantityReceived { get; private set; }
    public decimal UnitCost { get; private set; }

    private GoodsReceiptLine()
    {
    }

    public GoodsReceiptLine(
        Guid tenantId,
        Guid goodsReceiptId,
        Guid purchaseOrderLineId,
        Guid productId,
        int quantityReceived,
        decimal unitCost)
    {
        TenantId = tenantId;
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderLineId = purchaseOrderLineId;
        ProductId = productId;
        QuantityReceived = quantityReceived;
        UnitCost = decimal.Round(unitCost, 4);
    }
}
