using Inventory.Application.Common;
using Inventory.Domain.Catalog;

namespace Inventory.Application.Catalog;

public sealed record CreateCategoryRequest(
    string Name,
    string? Slug = null,
    string? Description = null,
    Guid? ParentCategoryId = null);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Slug,
    string? Description,
    Guid? ParentCategoryId,
    bool IsActive);

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentCategoryId,
    string? ParentCategoryName,
    bool IsActive,
    int ProductCount,
    DateTimeOffset CreatedAtUtc);

public sealed record ProductFilterRequest : PagedRequest
{
    public Guid? CategoryId { get; init; }
    public ProductStatus? Status { get; init; }
}

public sealed record CreateProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Description,
    string? Barcode,
    string UnitOfMeasure,
    decimal StandardCost,
    decimal UnitPrice,
    int ReorderPoint,
    int TargetStockLevel,
    ProductStatus Status = ProductStatus.Active,
    string? AttributesJson = null);

public sealed record UpdateProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string? Description,
    string? Barcode,
    string UnitOfMeasure,
    decimal StandardCost,
    decimal UnitPrice,
    int ReorderPoint,
    int TargetStockLevel,
    ProductStatus Status,
    string? AttributesJson = null);

public sealed record ProductDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Sku,
    string Name,
    string? Description,
    string? Barcode,
    string UnitOfMeasure,
    decimal StandardCost,
    decimal MovingAverageCost,
    decimal UnitPrice,
    int ReorderPoint,
    int TargetStockLevel,
    ProductStatus Status,
    string? AttributesJson,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
