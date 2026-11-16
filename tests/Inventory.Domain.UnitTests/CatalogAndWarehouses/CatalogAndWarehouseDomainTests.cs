using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Suppliers;

namespace Inventory.Domain.UnitTests.CatalogAndWarehouses;

public sealed class CatalogAndWarehouseDomainTests
{
    [Fact]
    public void Product_UpdateMovingAverageCost_ShouldComputeWeightedAverageCorrectly()
    {
        var tenantId = Guid.CreateVersion7();
        var categoryId = Guid.CreateVersion7();

        var product = new Product(
            tenantId,
            categoryId,
            "sku-100",
            "Industrial Sensor",
            "High precision sensor",
            "123456789",
            "EA",
            standardCost: 100.00m,
            unitPrice: 150.00m,
            reorderPoint: 10,
            targetStockLevel: 50);

        product.Sku.Should().Be("SKU-100");
        product.MovingAverageCost.Should().Be(100.00m);

        // Existing on-hand: 10 units @ $100 = $1,000
        // Incoming receipt: 10 units @ $120 = $1,200
        // Total: 20 units @ $2,200 -> $110.00 moving average cost
        product.UpdateMovingAverageCost(
            currentOnHandBeforeReceipt: 10,
            receivedQuantity: 10,
            receiptUnitCost: 120.00m);

        product.MovingAverageCost.Should().Be(110.0000m);
    }

    [Fact]
    public void Product_WhenTargetStockBelowReorderPoint_ShouldThrowDomainException()
    {
        var act = () => new Product(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "SKU-200",
            "Valve",
            null,
            null,
            "EA",
            standardCost: 10m,
            unitPrice: 20m,
            reorderPoint: 25,
            targetStockLevel: 10);

        act.Should().Throw<DomainException>()
            .Where(ex => ex.ErrorCode == "Product.InvalidTargetStockLevel");
    }

    [Fact]
    public void Product_WhenArchived_EnsureCanBeOrdered_ShouldThrowDomainException()
    {
        var product = new Product(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "SKU-300",
            "Legacy Part",
            null,
            null,
            "EA",
            standardCost: 10m,
            unitPrice: 20m,
            reorderPoint: 5,
            targetStockLevel: 10,
            status: ProductStatus.Archived);

        var act = () => product.EnsureCanBeOrdered();

        act.Should().Throw<DomainException>()
            .Where(ex => ex.ErrorCode == "Product.Archived");
    }

    [Fact]
    public void SupplierProduct_WithInvalidMinimumOrderQuantity_ShouldThrowDomainException()
    {
        var act = () => new SupplierProduct(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "SUP-SKU-1",
            agreedUnitCost: 15m,
            minimumOrderQuantity: 0);

        act.Should().Throw<DomainException>()
            .Where(ex => ex.ErrorCode == "SupplierProduct.InvalidMoq");
    }
}
