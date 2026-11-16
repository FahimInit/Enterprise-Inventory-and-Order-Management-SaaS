using Inventory.Domain.Common;

namespace Inventory.Domain.Catalog;

public enum ProductStatus
{
    Draft = 1,
    Active = 2,
    Archived = 3
}

public sealed class Product : TenantEntityBase
{
    public Guid CategoryId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Barcode { get; private set; }
    public string UnitOfMeasure { get; private set; } = "EA";
    public decimal StandardCost { get; private set; }
    public decimal MovingAverageCost { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int ReorderPoint { get; private set; }
    public int TargetStockLevel { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Active;
    public string? AttributesJson { get; private set; }

    public Category? Category { get; private set; }

    private Product()
    {
    }

    public Product(
        Guid tenantId,
        Guid categoryId,
        string sku,
        string name,
        string? description,
        string? barcode,
        string unitOfMeasure,
        decimal standardCost,
        decimal unitPrice,
        int reorderPoint,
        int targetStockLevel,
        ProductStatus status = ProductStatus.Active,
        string? attributesJson = null)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("Product.SkuRequired", "Product SKU is required.");
        }

        Sku = NormalizeSku(sku);
        MovingAverageCost = decimal.Round(standardCost, 4);

        UpdateDetails(
            categoryId,
            sku,
            name,
            description,
            barcode,
            unitOfMeasure,
            standardCost,
            unitPrice,
            reorderPoint,
            targetStockLevel,
            status,
            attributesJson);
    }

    public void UpdateDetails(
        Guid categoryId,
        string sku,
        string name,
        string? description,
        string? barcode,
        string unitOfMeasure,
        decimal standardCost,
        decimal unitPrice,
        int reorderPoint,
        int targetStockLevel,
        ProductStatus status,
        string? attributesJson = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new DomainException("Product.CategoryRequired", "Product category is required.");
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("Product.SkuRequired", "Product SKU is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product.NameRequired", "Product name is required.");
        }

        if (standardCost < 0m)
        {
            throw new DomainException("Product.NegativeCost", "Product standard cost cannot be negative.");
        }

        if (unitPrice < 0m)
        {
            throw new DomainException("Product.NegativePrice", "Product unit selling price cannot be negative.");
        }

        if (reorderPoint < 0)
        {
            throw new DomainException("Product.NegativeReorderPoint", "Reorder point cannot be negative.");
        }

        if (targetStockLevel < reorderPoint)
        {
            throw new DomainException("Product.InvalidTargetStockLevel", "Target stock level must be greater than or equal to the reorder point.");
        }

        CategoryId = categoryId;
        Sku = NormalizeSku(sku);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Barcode = string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
        UnitOfMeasure = string.IsNullOrWhiteSpace(unitOfMeasure) ? "EA" : unitOfMeasure.Trim().ToUpperInvariant();
        StandardCost = decimal.Round(standardCost, 4);
        if (MovingAverageCost == 0m && StandardCost > 0m)
        {
            MovingAverageCost = StandardCost;
        }

        UnitPrice = decimal.Round(unitPrice, 4);
        ReorderPoint = reorderPoint;
        TargetStockLevel = targetStockLevel;
        Status = status;
        AttributesJson = string.IsNullOrWhiteSpace(attributesJson) ? null : attributesJson.Trim();
        TouchUpdatedTimestamp();
    }

    public void UpdateMovingAverageCost(int currentOnHandBeforeReceipt, int receivedQuantity, decimal receiptUnitCost)
    {
        if (receivedQuantity <= 0)
        {
            throw new DomainException("Product.InvalidReceivedQuantity", "Received quantity must be greater than zero when updating moving average cost.");
        }

        if (receiptUnitCost < 0m)
        {
            throw new DomainException("Product.NegativeReceiptCost", "Receipt unit cost cannot be negative.");
        }

        var safeExistingQuantity = Math.Max(0, currentOnHandBeforeReceipt);
        var totalUnitsAfter = safeExistingQuantity + receivedQuantity;

        if (totalUnitsAfter <= 0)
        {
            MovingAverageCost = decimal.Round(receiptUnitCost, 4);
        }
        else
        {
            var currentBasis = safeExistingQuantity * MovingAverageCost;
            var incomingBasis = receivedQuantity * receiptUnitCost;
            MovingAverageCost = decimal.Round((currentBasis + incomingBasis) / totalUnitsAfter, 4);
        }

        TouchUpdatedTimestamp();
    }

    public void EnsureCanBeOrdered()
    {
        if (Status == ProductStatus.Archived)
        {
            throw new DomainException("Product.Archived", $"Product '{Sku}' ({Name}) is archived and cannot be added to new orders.");
        }
    }

    public static string NormalizeSku(string sku) =>
        sku.Trim().ToUpperInvariant();
}
