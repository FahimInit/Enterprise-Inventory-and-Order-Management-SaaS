using Inventory.Domain.Common;

namespace Inventory.Domain.SalesOrders;

public enum SalesOrderStatus
{
    Draft = 1,
    Pending = 2,
    Confirmed = 3,
    Processing = 4,
    Fulfilled = 5,
    Cancelled = 6
}

public sealed class SalesOrder : TenantEntityBase
{
    private readonly List<SalesOrderLine> _lines = [];

    public string OrderNumber { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public string? CustomerEmail { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? ShippingAddress { get; private set; }
    public Guid FulfillmentWarehouseId { get; private set; }
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;
    public decimal SubtotalAmount { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    public Guid? FulfilledByUserId { get; private set; }
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }
    public DateTimeOffset? FulfilledAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<SalesOrderLine> Lines => _lines.AsReadOnly();

    private SalesOrder()
    {
    }

    public SalesOrder(
        Guid tenantId,
        string orderNumber,
        string customerName,
        string? customerEmail,
        string? customerPhone,
        string? shippingAddress,
        Guid fulfillmentWarehouseId,
        decimal taxRatePercent,
        decimal discountAmount,
        string? notes,
        Guid? createdByUserId,
        IEnumerable<(Guid ProductId, int Quantity, decimal AuthoritativeUnitPrice, decimal UnitCostSnapshot)> lines,
        bool submitImmediately = true)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("SalesOrder.OrderNumberRequired", "Order number is required.");
        }

        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new DomainException("SalesOrder.CustomerNameRequired", "Customer name is required.");
        }

        if (fulfillmentWarehouseId == Guid.Empty)
        {
            throw new DomainException("SalesOrder.WarehouseRequired", "Fulfillment warehouse is required.");
        }

        if (taxRatePercent < 0m || taxRatePercent > 100m)
        {
            throw new DomainException("SalesOrder.InvalidTaxRate", "Tax rate percent must be between 0 and 100.");
        }

        if (discountAmount < 0m)
        {
            throw new DomainException("SalesOrder.NegativeDiscount", "Discount amount cannot be negative.");
        }

        OrderNumber = orderNumber.Trim().ToUpperInvariant();
        CustomerName = customerName.Trim();
        CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim();
        CustomerPhone = string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone.Trim();
        ShippingAddress = string.IsNullOrWhiteSpace(shippingAddress) ? null : shippingAddress.Trim();
        FulfillmentWarehouseId = fulfillmentWarehouseId;
        Status = submitImmediately ? SalesOrderStatus.Pending : SalesOrderStatus.Draft;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedByUserId = createdByUserId;

        var groupedLines = lines
            .GroupBy(x => x.ProductId)
            .Select(g => (
                ProductId: g.Key,
                Quantity: g.Sum(x => x.Quantity),
                UnitPrice: g.First().AuthoritativeUnitPrice,
                UnitCostSnapshot: g.First().UnitCostSnapshot))
            .ToList();

        if (groupedLines.Count == 0)
        {
            throw new DomainException("SalesOrder.EmptyLines", "A sales order must contain at least one order line.");
        }

        foreach (var line in groupedLines)
        {
            _lines.Add(new SalesOrderLine(
                TenantId,
                Id,
                line.ProductId,
                line.Quantity,
                line.UnitPrice,
                line.UnitCostSnapshot));
        }

        RecalculateTotalsServerSide(taxRatePercent, discountAmount);
    }

    public void Submit()
    {
        if (Status != SalesOrderStatus.Draft)
        {
            throw new ConflictException(
                "SalesOrder.InvalidStateForSubmit",
                $"Cannot submit sales order '{OrderNumber}' when status is '{Status}'.");
        }

        Status = SalesOrderStatus.Pending;
        TouchUpdatedTimestamp();
    }

    public void Confirm(Guid? confirmedByUserId)
    {
        if (Status is not (SalesOrderStatus.Draft or SalesOrderStatus.Pending))
        {
            throw new ConflictException(
                "SalesOrder.InvalidStateForConfirm",
                $"Cannot confirm sales order '{OrderNumber}' when status is '{Status}'.");
        }

        Status = SalesOrderStatus.Confirmed;
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    public void StartProcessing()
    {
        if (Status != SalesOrderStatus.Confirmed)
        {
            throw new ConflictException(
                "SalesOrder.InvalidStateForProcessing",
                $"Cannot move sales order '{OrderNumber}' to Processing when status is '{Status}'. Order must be 'Confirmed' first.");
        }

        Status = SalesOrderStatus.Processing;
        TouchUpdatedTimestamp();
    }

    public void MarkFulfilled(Guid? fulfilledByUserId)
    {
        if (Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.Processing))
        {
            throw new ConflictException(
                "SalesOrder.InvalidStateForFulfill",
                $"Cannot fulfill sales order '{OrderNumber}' when status is '{Status}'. Order must be 'Confirmed' or 'Processing'.");
        }

        Status = SalesOrderStatus.Fulfilled;
        FulfilledByUserId = fulfilledByUserId;
        FulfilledAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    /// <summary>
    /// Cancels the sales order if not yet fulfilled.
    /// Returns true if the order held active stock reservations ('Confirmed' or 'Processing') that must be released.
    /// </summary>
    public bool Cancel(string reason)
    {
        if (Status == SalesOrderStatus.Fulfilled)
        {
            throw new ConflictException(
                "SalesOrder.CannotCancelFulfilled",
                $"Sales order '{OrderNumber}' has already been fulfilled and cannot be cancelled.");
        }

        if (Status == SalesOrderStatus.Cancelled)
        {
            throw new ConflictException("SalesOrder.AlreadyCancelled", "Sales order is already cancelled.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("SalesOrder.CancelReasonRequired", "A cancellation reason is required.");
        }

        var hadActiveReservations = Status is SalesOrderStatus.Confirmed or SalesOrderStatus.Processing;

        Status = SalesOrderStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();

        return hadActiveReservations;
    }

    private void RecalculateTotalsServerSide(decimal taxRatePercent, decimal discountAmount)
    {
        SubtotalAmount = decimal.Round(_lines.Sum(l => l.LineTotal), 4);
        DiscountAmount = decimal.Round(discountAmount, 4);

        if (DiscountAmount > SubtotalAmount)
        {
            throw new DomainException("SalesOrder.DiscountExceedsSubtotal", "Discount amount cannot exceed order subtotal.");
        }

        TaxRatePercent = decimal.Round(taxRatePercent, 4);
        var taxableAmount = SubtotalAmount - DiscountAmount;
        TaxAmount = decimal.Round(taxableAmount * (TaxRatePercent / 100m), 4);
        TotalAmount = decimal.Round(taxableAmount + TaxAmount, 4);
    }
}

public sealed class SalesOrderLine : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid SalesOrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal UnitCostSnapshot { get; private set; }
    public decimal LineTotal { get; private set; }

    private SalesOrderLine()
    {
    }

    public SalesOrderLine(
        Guid tenantId,
        Guid salesOrderId,
        Guid productId,
        int quantity,
        decimal authoritativeUnitPrice,
        decimal unitCostSnapshot)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("SalesOrderLine.ProductRequired", "ProductId is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("SalesOrderLine.InvalidQuantity", "Order line quantity must be greater than zero.");
        }

        if (authoritativeUnitPrice < 0m)
        {
            throw new DomainException("SalesOrderLine.NegativePrice", "Unit price cannot be negative.");
        }

        TenantId = tenantId;
        SalesOrderId = salesOrderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = decimal.Round(authoritativeUnitPrice, 4);
        UnitCostSnapshot = decimal.Round(Math.Max(0m, unitCostSnapshot), 4);
        LineTotal = decimal.Round(quantity * UnitPrice, 4);
    }
}
