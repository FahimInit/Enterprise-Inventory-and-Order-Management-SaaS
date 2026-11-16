using Inventory.Application.Catalog;
using Inventory.Application.Identity;
using Inventory.Application.Inventory;
using Inventory.Application.PurchaseOrders;
using Inventory.Application.SalesOrders;
using Inventory.Application.StockTransfers;
using Inventory.Domain.Identity;

namespace Inventory.Application.UnitTests.Common;

public sealed class ValidatorsAndPermissionsTests
{
    [Fact]
    public void RegisterRequestValidator_WithWeakPassword_ShouldFailValidation()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest(
            "user@example.com",
            "Test User",
            "weakpassword");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterRequest.Password));
    }

    [Fact]
    public void CreateProductRequestValidator_WhenTargetBelowReorderPoint_ShouldFailValidation()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest(
            Guid.CreateVersion7(),
            "SKU-001",
            "Industrial Pump",
            null,
            null,
            "EA",
            StandardCost: 100m,
            UnitPrice: 150m,
            ReorderPoint: 50,
            TargetStockLevel: 20);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateProductRequest.TargetStockLevel));
    }

    [Fact]
    public void AdjustInventoryRequestValidator_WithZeroDelta_ShouldFailValidation()
    {
        var validator = new AdjustInventoryRequestValidator();
        var request = new AdjustInventoryRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            QuantityDelta: 0,
            Reason: "Cycle count");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateStockTransferRequestValidator_WithSameSourceAndDestination_ShouldFailValidation()
    {
        var validator = new CreateStockTransferRequestValidator();
        var warehouseId = Guid.CreateVersion7();

        var request = new CreateStockTransferRequest(
            warehouseId,
            warehouseId,
            "Invalid transfer",
            [new CreateStockTransferLineRequest(Guid.CreateVersion7(), 10)]);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateStockTransferRequest.DestinationWarehouseId));
    }

    [Fact]
    public void CreateSalesOrderRequestValidator_WithEmptyLines_ShouldFailValidation()
    {
        var validator = new CreateSalesOrderRequestValidator();
        var request = new CreateSalesOrderRequest(
            "Acme Buyer",
            "buyer@acme.com",
            null,
            null,
            Guid.CreateVersion7(),
            TaxRatePercent: 8m,
            DiscountAmount: 0m,
            Notes: null,
            ConfirmImmediately: true,
            Lines: []);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSalesOrderRequest.Lines));
    }

    [Fact]
    public void PermissionsMatrix_ShouldEnforceClearRoleSeparation()
    {
        var roles = Permissions.GetDefaultRoleDefinitions();

        roles[Permissions.SystemRoleNames.Owner].Permissions
            .Should().BeEquivalentTo(Permissions.All);

        roles[Permissions.SystemRoleNames.Auditor].Permissions
            .Should().Contain(Permissions.AuditLogs.Read)
            .And.NotContain(Permissions.Inventory.Adjust)
            .And.NotContain(Permissions.PurchaseOrders.Approve)
            .And.NotContain(Permissions.SalesOrders.Confirm);

        roles[Permissions.SystemRoleNames.InventoryClerk].Permissions
            .Should().Contain(Permissions.Inventory.Adjust)
            .And.Contain(Permissions.PurchaseOrders.Receive)
            .And.NotContain(Permissions.PurchaseOrders.Approve)
            .And.NotContain(Permissions.Organization.MembersManage);

        roles[Permissions.SystemRoleNames.SalesRepresentative].Permissions
            .Should().Contain(Permissions.SalesOrders.Write)
            .And.Contain(Permissions.SalesOrders.Confirm)
            .And.NotContain(Permissions.PurchaseOrders.Approve)
            .And.NotContain(Permissions.Inventory.Adjust);
    }
}
