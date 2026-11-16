using Inventory.Domain.Common;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;

namespace Inventory.Domain.UnitTests.Orders;

public sealed class PurchaseAndSalesOrderDomainTests
{
    [Fact]
    public void PurchaseOrder_PartialAndFullReceiving_ShouldTransitionStatusesAndPreventOverReceipt()
    {
        var tenantId = Guid.CreateVersion7();
        var supplierId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        var po = new PurchaseOrder(
            tenantId,
            "PO-5001",
            supplierId,
            warehouseId,
            expectedDeliveryDateUtc: DateTimeOffset.UtcNow.AddDays(5),
            taxRatePercent: 10m,
            notes: "Restock order",
            createdByUserId: Guid.CreateVersion7(),
            lines: [(productId, OrderedQuantity: 20, UnitCost: 50.00m)]);

        po.Status.Should().Be(PurchaseOrderStatus.Draft);
        po.SubtotalAmount.Should().Be(1000.00m);
        po.TaxAmount.Should().Be(100.00m);
        po.TotalAmount.Should().Be(1100.00m);

        po.Submit();
        po.Status.Should().Be(PurchaseOrderStatus.Submitted);

        po.Approve(Guid.CreateVersion7());
        po.Status.Should().Be(PurchaseOrderStatus.Approved);

        var lineId = po.Lines.First().Id;

        // 1. Receive partial quantity (12 of 20)
        var firstReceipt = po.ReceiveGoods(
            "GRN-001",
            "Partial shipment",
            Guid.CreateVersion7(),
            [(lineId, QuantityReceived: 12)]);

        po.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);
        po.Lines.First().ReceivedQuantity.Should().Be(12);
        po.Lines.First().RemainingQuantity.Should().Be(8);
        firstReceipt.Lines.Should().ContainSingle();

        // 2. Attempting to cancel after partial receipt must fail
        var cancelAct = () => po.Cancel("Changed mind");
        cancelAct.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "PurchaseOrder.CannotCancelAfterReceipt");

        // 3. Attempting to over-receive (10 when only 8 remain) must fail
        var overReceiveAct = () => po.ReceiveGoods(
            "GRN-002",
            "Over shipment",
            Guid.CreateVersion7(),
            [(lineId, QuantityReceived: 10)]);
        overReceiveAct.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "PurchaseOrderLine.OverReceiptForbidden");

        // 4. Receive remaining 8 units -> transitions to Received
        po.ReceiveGoods(
            "GRN-002",
            "Final shipment",
            Guid.CreateVersion7(),
            [(lineId, QuantityReceived: 8)]);

        po.Status.Should().Be(PurchaseOrderStatus.Received);
        po.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void SalesOrder_ServerSideTotalsAndStateLifecycle_ShouldEnforceBusinessRules()
    {
        var tenantId = Guid.CreateVersion7();
        var warehouseId = Guid.CreateVersion7();
        var productA = Guid.CreateVersion7();
        var productB = Guid.CreateVersion7();

        var order = new SalesOrder(
            tenantId,
            "SO-9001",
            "Global Manufacturing Corp",
            "orders@globalmfg.com",
            "+1-555-0100",
            "742 Industrial Pkwy",
            warehouseId,
            taxRatePercent: 8.5m,
            discountAmount: 50.00m,
            notes: "Priority order",
            createdByUserId: Guid.CreateVersion7(),
            lines:
            [
                (productA, Quantity: 4, AuthoritativeUnitPrice: 100.00m, UnitCostSnapshot: 60.00m),
                (productB, Quantity: 2, AuthoritativeUnitPrice: 75.00m, UnitCostSnapshot: 40.00m)
            ]);

        // Subtotal = (4 * 100) + (2 * 75) = 550.00
        // Taxable = 550 - 50 = 500.00
        // Tax @ 8.5% = 42.50
        // Total = 542.50
        order.SubtotalAmount.Should().Be(550.00m);
        order.DiscountAmount.Should().Be(50.00m);
        order.TaxAmount.Should().Be(42.50m);
        order.TotalAmount.Should().Be(542.50m);
        order.Status.Should().Be(SalesOrderStatus.Pending);

        order.Confirm(Guid.CreateVersion7());
        order.Status.Should().Be(SalesOrderStatus.Confirmed);

        order.StartProcessing();
        order.Status.Should().Be(SalesOrderStatus.Processing);

        order.MarkFulfilled(Guid.CreateVersion7());
        order.Status.Should().Be(SalesOrderStatus.Fulfilled);

        // Fulfilled orders cannot be cancelled
        var cancelFulfilledAct = () => order.Cancel("Customer requested cancellation after shipment");
        cancelFulfilledAct.Should().Throw<ConflictException>()
            .Where(ex => ex.ErrorCode == "SalesOrder.CannotCancelFulfilled");
    }
}
