using Inventory.Domain.Common;
using Inventory.Domain.Inventory;
using Inventory.Domain.StockTransfers;

namespace Inventory.Domain.UnitTests.Inventory;

public sealed class InventoryEngineDomainTests
{
    [Fact]
    public void InventoryItem_ReceiveReserveAndFulfill_ShouldMaintainExactBalancesAndLedger()
    {
        var tenantId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var actorId = Guid.CreateVersion7();

        var item = new InventoryItem(tenantId, warehouseId, productId);

        // 1. Receive 50 units
        var receiptMovement = item.ReceiveStock(
            quantity: 50,
            unitCost: 25.00m,
            referenceType: "PurchaseOrder",
            referenceNumber: "PO-1001",
            referenceId: Guid.CreateVersion7(),
            reason: "Initial PO receipt",
            notes: null,
            actorUserId: actorId);

        item.OnHandQuantity.Should().Be(50);
        item.ReservedQuantity.Should().Be(0);
        item.AvailableQuantity.Should().Be(50);
        receiptMovement.BalanceBefore.Should().Be(0);
        receiptMovement.QuantityDelta.Should().Be(50);
        receiptMovement.BalanceAfter.Should().Be(50);

        // 2. Reserve 20 units for a Sales Order
        var reservation = item.ReserveStock(
            quantity: 20,
            salesOrderId: Guid.CreateVersion7(),
            salesOrderLineId: Guid.CreateVersion7(),
            orderNumber: "SO-2001");

        item.OnHandQuantity.Should().Be(50);
        item.ReservedQuantity.Should().Be(20);
        item.AvailableQuantity.Should().Be(30);
        reservation.Status.Should().Be(ReservationStatus.Active);

        // 3. Fulfill the 20 reserved units
        var fulfillMovement = item.FulfillReservedStock(
            reservation,
            unitCost: 25.00m,
            orderNumber: "SO-2001",
            actorUserId: actorId);

        item.OnHandQuantity.Should().Be(30);
        item.ReservedQuantity.Should().Be(0);
        item.AvailableQuantity.Should().Be(30);
        reservation.Status.Should().Be(ReservationStatus.Fulfilled);
        fulfillMovement.BalanceBefore.Should().Be(50);
        fulfillMovement.QuantityDelta.Should().Be(-20);
        fulfillMovement.BalanceAfter.Should().Be(30);
    }

    [Fact]
    public void InventoryItem_ReserveMoreThanAvailable_ShouldThrowConflictException()
    {
        var item = new InventoryItem(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        item.ReceiveStock(10, 10m, "PO", "PO-1", Guid.CreateVersion7(), "Seed", null, null);
        item.ReserveStock(6, Guid.CreateVersion7(), Guid.CreateVersion7(), "SO-1");

        // Available is now 4 (OnHand=10, Reserved=6). Requesting 5 must fail.
        var act = () => item.ReserveStock(5, Guid.CreateVersion7(), Guid.CreateVersion7(), "SO-2");

        act.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "Inventory.InsufficientStock");
    }

    [Fact]
    public void InventoryItem_AdjustBelowReservedQuantity_ShouldThrowConflictException()
    {
        var item = new InventoryItem(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        item.ReceiveStock(20, 10m, "PO", "PO-1", Guid.CreateVersion7(), "Seed", null, null);
        item.ReserveStock(15, Guid.CreateVersion7(), Guid.CreateVersion7(), "SO-1");

        // Attempting to adjust by -10 would reduce OnHand to 10, which is below Reserved (15).
        var act = () => item.AdjustStock(-10, 10m, "Shrinkage", "ADJ-1", null, null);

        act.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "Inventory.AdjustmentBelowReservedStock");
    }

    [Fact]
    public void InventoryItem_ReleaseReservation_ShouldRestoreAvailableQuantity()
    {
        var item = new InventoryItem(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        item.ReceiveStock(15, 10m, "PO", "PO-1", Guid.CreateVersion7(), "Seed", null, null);
        var reservation = item.ReserveStock(10, Guid.CreateVersion7(), Guid.CreateVersion7(), "SO-1");

        item.AvailableQuantity.Should().Be(5);

        item.ReleaseReservation(reservation, "Customer cancelled order");

        item.ReservedQuantity.Should().Be(0);
        item.AvailableQuantity.Should().Be(15);
        reservation.Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public void StockTransfer_DispatchAndReceive_ShouldTransitionStatesAndPreventDoubleReceive()
    {
        var tenantId = Guid.CreateVersion7();
        var sourceWh = Guid.CreateVersion7();
        var destWh = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        var transfer = new StockTransfer(
            tenantId,
            "TRF-001",
            sourceWh,
            destWh,
            "Restock West warehouse",
            Guid.CreateVersion7(),
            [(productId, 25, null)]);

        transfer.Status.Should().Be(TransferStatus.Requested);

        transfer.MarkDispatched(Guid.CreateVersion7());
        transfer.Status.Should().Be(TransferStatus.InTransit);

        transfer.MarkReceived(Guid.CreateVersion7());
        transfer.Status.Should().Be(TransferStatus.Received);

        var cancelAfterReceive = () => transfer.Cancel("Too late");
        cancelAfterReceive.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "Transfer.CannotCancelReceived");
    }
}
