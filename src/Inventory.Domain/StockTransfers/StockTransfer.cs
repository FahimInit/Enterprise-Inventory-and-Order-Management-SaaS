using Inventory.Domain.Common;

namespace Inventory.Domain.StockTransfers;

public enum TransferStatus
{
    Draft = 1,
    Requested = 2,
    InTransit = 3,
    Received = 4,
    Cancelled = 5
}

public sealed class StockTransfer : TenantEntityBase
{
    private readonly List<StockTransferLine> _lines = [];

    public string TransferNumber { get; private set; } = string.Empty;
    public Guid SourceWarehouseId { get; private set; }
    public Guid DestinationWarehouseId { get; private set; }
    public TransferStatus Status { get; private set; } = TransferStatus.Requested;
    public string? Notes { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public Guid? DispatchedByUserId { get; private set; }
    public Guid? ReceivedByUserId { get; private set; }
    public DateTimeOffset? DispatchedAtUtc { get; private set; }
    public DateTimeOffset? ReceivedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<StockTransferLine> Lines => _lines.AsReadOnly();

    private StockTransfer()
    {
    }

    public StockTransfer(
        Guid tenantId,
        string transferNumber,
        Guid sourceWarehouseId,
        Guid destinationWarehouseId,
        string? notes,
        Guid? requestedByUserId,
        IEnumerable<(Guid ProductId, int Quantity, string? LineNotes)> lines)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(transferNumber))
        {
            throw new DomainException("Transfer.NumberRequired", "Transfer number is required.");
        }

        if (sourceWarehouseId == Guid.Empty || destinationWarehouseId == Guid.Empty)
        {
            throw new DomainException("Transfer.WarehousesRequired", "Both source and destination warehouses are required.");
        }

        if (sourceWarehouseId == destinationWarehouseId)
        {
            throw new DomainException("Transfer.SameWarehouseForbidden", "Source and destination warehouses must be different.");
        }

        TransferNumber = transferNumber.Trim().ToUpperInvariant();
        SourceWarehouseId = sourceWarehouseId;
        DestinationWarehouseId = destinationWarehouseId;
        Status = TransferStatus.Requested;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        RequestedByUserId = requestedByUserId;

        var groupedLines = lines
            .GroupBy(x => x.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(x => x.Quantity), LineNotes: g.First().LineNotes))
            .ToList();

        if (groupedLines.Count == 0)
        {
            throw new DomainException("Transfer.EmptyLines", "At least one product line is required for a stock transfer.");
        }

        foreach (var line in groupedLines)
        {
            _lines.Add(new StockTransferLine(TenantId, Id, line.ProductId, line.Quantity, line.LineNotes));
        }
    }

    public void MarkDispatched(Guid? dispatchedByUserId)
    {
        if (Status is not (TransferStatus.Draft or TransferStatus.Requested))
        {
            throw new ConflictException(
                "Transfer.InvalidStateForDispatch",
                $"Cannot dispatch transfer '{TransferNumber}' because its current status is '{Status}'.");
        }

        Status = TransferStatus.InTransit;
        DispatchedByUserId = dispatchedByUserId;
        DispatchedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    public void MarkReceived(Guid? receivedByUserId)
    {
        if (Status != TransferStatus.InTransit)
        {
            throw new ConflictException(
                "Transfer.InvalidStateForReceive",
                $"Cannot receive transfer '{TransferNumber}' because its current status is '{Status}'. Only 'InTransit' transfers can be received.");
        }

        Status = TransferStatus.Received;
        ReceivedByUserId = receivedByUserId;
        ReceivedAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();
    }

    public bool Cancel(string reason)
    {
        if (Status == TransferStatus.Received)
        {
            throw new ConflictException("Transfer.CannotCancelReceived", "A completed stock transfer that has already been received cannot be cancelled.");
        }

        if (Status == TransferStatus.Cancelled)
        {
            throw new ConflictException("Transfer.AlreadyCancelled", "This stock transfer is already cancelled.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Transfer.CancelReasonRequired", "A cancellation reason is required.");
        }

        var wasInTransit = Status == TransferStatus.InTransit;
        Status = TransferStatus.Cancelled;
        CancellationReason = reason.Trim();
        CancelledAtUtc = DateTimeOffset.UtcNow;
        TouchUpdatedTimestamp();

        return wasInTransit;
    }
}

public sealed class StockTransferLine : EntityBase, ITenantEntity
{
    public Guid TenantId { get; private set; }
    public Guid StockTransferId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public string? Notes { get; private set; }

    private StockTransferLine()
    {
    }

    public StockTransferLine(
        Guid tenantId,
        Guid stockTransferId,
        Guid productId,
        int quantity,
        string? notes = null)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("TransferLine.ProductRequired", "ProductId is required.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("TransferLine.InvalidQuantity", "Transfer line quantity must be greater than zero.");
        }

        TenantId = tenantId;
        StockTransferId = stockTransferId;
        ProductId = productId;
        Quantity = quantity;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
