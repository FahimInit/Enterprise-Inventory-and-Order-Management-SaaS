using Inventory.Application.Common;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Catalog;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<PagedResponse<ProductDto>> GetProductsAsync(ProductFilterRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateProductAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken = default);
}

public sealed class CatalogService : ICatalogService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly CreateCategoryRequestValidator _createCategoryValidator = new();
    private readonly UpdateCategoryRequestValidator _updateCategoryValidator = new();
    private readonly CreateProductRequestValidator _createProductValidator = new();
    private readonly UpdateProductRequestValidator _updateProductValidator = new();

    public CatalogService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var categories = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var productCounts = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .GroupBy(p => p.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var categoryNameById = categories.ToDictionary(c => c.Id, c => c.Name);

        return categories
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.ParentCategoryId,
                c.ParentCategoryId.HasValue && categoryNameById.TryGetValue(c.ParentCategoryId.Value, out var parentName)
                    ? parentName
                    : null,
                c.IsActive,
                productCounts.GetValueOrDefault(c.Id, 0),
                c.CreatedAtUtc))
            .ToList();
    }

    public async Task<CategoryDto> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createCategoryValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? Organization.NormalizeSlug(request.Name)
            : Organization.NormalizeSlug(request.Slug);

        var duplicateExists = await _dbContext.Categories
            .AnyAsync(
                c => c.TenantId == tenantId &&
                     (c.Slug == slug || c.Name.ToLower() == request.Name.Trim().ToLower()),
                cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException("Category.AlreadyExists", $"A category with name '{request.Name.Trim()}' or slug '{slug}' already exists.");
        }

        string? parentName = null;
        if (request.ParentCategoryId.HasValue)
        {
            var parent = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.ParentCategoryId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);

            parentName = parent.Name;
        }

        var category = new Category(
            tenantId,
            request.Name,
            slug,
            request.Description,
            request.ParentCategoryId);

        _dbContext.Categories.Add(category);

        _dbContext.RecordAudit(
            action: "Category.Created",
            entityType: nameof(Category),
            entityId: category.Id.ToString(),
            summary: $"Created product category '{category.Name}' ({category.Slug}).",
            afterState: new { category.Id, category.Name, category.Slug, category.ParentCategoryId });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.ParentCategoryId,
            parentName,
            category.IsActive,
            ProductCount: 0,
            category.CreatedAtUtc);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(
        Guid categoryId,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateCategoryValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == categoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), categoryId);

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? Organization.NormalizeSlug(request.Name)
            : Organization.NormalizeSlug(request.Slug);

        var duplicateExists = await _dbContext.Categories
            .AnyAsync(
                c => c.TenantId == tenantId &&
                     c.Id != categoryId &&
                     (c.Slug == slug || c.Name.ToLower() == request.Name.Trim().ToLower()),
                cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException("Category.AlreadyExists", $"Another category with name '{request.Name.Trim()}' or slug '{slug}' already exists.");
        }

        string? parentName = null;
        if (request.ParentCategoryId.HasValue)
        {
            var parent = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.ParentCategoryId.Value, cancellationToken)
                ?? throw new NotFoundException(nameof(Category), request.ParentCategoryId.Value);

            parentName = parent.Name;
        }

        category.Update(
            request.Name,
            slug,
            request.Description,
            request.ParentCategoryId,
            request.IsActive);

        _dbContext.RecordAudit(
            action: "Category.Updated",
            entityType: nameof(Category),
            entityId: category.Id.ToString(),
            summary: $"Updated product category '{category.Name}'.",
            afterState: new { category.Id, category.Name, category.Slug, category.IsActive });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var productCount = await _dbContext.Products
            .CountAsync(p => p.TenantId == tenantId && p.CategoryId == categoryId, cancellationToken);

        return new CategoryDto(
            category.Id,
            category.Name,
            category.Slug,
            category.Description,
            category.ParentCategoryId,
            parentName,
            category.IsActive,
            productCount,
            category.CreatedAtUtc);
    }

    public async Task<PagedResponse<ProductDto>> GetProductsAsync(
        ProductFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from p in _dbContext.Products.AsNoTracking()
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    where p.TenantId == tenantId
                    select new { Product = p, CategoryName = c.Name };

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Product.CategoryId == request.CategoryId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Product.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Product.Sku.ToLower().Contains(searchTerm) ||
                x.Product.Name.ToLower().Contains(searchTerm) ||
                (x.Product.Barcode != null && x.Product.Barcode.ToLower().Contains(searchTerm)));
        }

        var isAscending = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        query = (request.SortBy?.ToLowerInvariant()) switch
        {
            "sku" => isAscending ? query.OrderBy(x => x.Product.Sku) : query.OrderByDescending(x => x.Product.Sku),
            "name" => isAscending ? query.OrderBy(x => x.Product.Name) : query.OrderByDescending(x => x.Product.Name),
            "price" => isAscending ? query.OrderBy(x => x.Product.UnitPrice) : query.OrderByDescending(x => x.Product.UnitPrice),
            _ => isAscending ? query.OrderBy(x => x.Product.CreatedAtUtc) : query.OrderByDescending(x => x.Product.CreatedAtUtc)
        };

        var projected = query.Select(x => new ProductDto(
            x.Product.Id,
            x.Product.CategoryId,
            x.CategoryName,
            x.Product.Sku,
            x.Product.Name,
            x.Product.Description,
            x.Product.Barcode,
            x.Product.UnitOfMeasure,
            x.Product.StandardCost,
            x.Product.MovingAverageCost,
            x.Product.UnitPrice,
            x.Product.ReorderPoint,
            x.Product.TargetStockLevel,
            x.Product.Status,
            x.Product.AttributesJson,
            x.Product.CreatedAtUtc,
            x.Product.UpdatedAtUtc));

        return await PagedResponse<ProductDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }

    public async Task<ProductDto> GetProductByIdAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var item = await (
            from p in _dbContext.Products.AsNoTracking()
            join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
            where p.TenantId == tenantId && p.Id == productId
            select new ProductDto(
                p.Id,
                p.CategoryId,
                c.Name,
                p.Sku,
                p.Name,
                p.Description,
                p.Barcode,
                p.UnitOfMeasure,
                p.StandardCost,
                p.MovingAverageCost,
                p.UnitPrice,
                p.ReorderPoint,
                p.TargetStockLevel,
                p.Status,
                p.AttributesJson,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Product), productId);

        return item;
    }

    public async Task<ProductDto> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createProductValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        var normalizedSku = Product.NormalizeSku(request.Sku);
        var skuExists = await _dbContext.Products
            .AnyAsync(p => p.TenantId == tenantId && p.Sku == normalizedSku, cancellationToken);

        if (skuExists)
        {
            throw new ConflictException("Product.SkuAlreadyExists", $"A product with SKU '{normalizedSku}' already exists in this organization.");
        }

        var product = new Product(
            tenantId,
            request.CategoryId,
            normalizedSku,
            request.Name,
            request.Description,
            request.Barcode,
            request.UnitOfMeasure,
            request.StandardCost,
            request.UnitPrice,
            request.ReorderPoint,
            request.TargetStockLevel,
            request.Status,
            request.AttributesJson);

        _dbContext.Products.Add(product);

        _dbContext.RecordAudit(
            action: "Product.Created",
            entityType: nameof(Product),
            entityId: product.Id.ToString(),
            summary: $"Created product '{product.Sku}' ({product.Name}).",
            afterState: new
            {
                product.Id,
                product.Sku,
                product.Name,
                product.StandardCost,
                product.UnitPrice,
                product.ReorderPoint
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.CategoryId,
            category.Name,
            product.Sku,
            product.Name,
            product.Description,
            product.Barcode,
            product.UnitOfMeasure,
            product.StandardCost,
            product.MovingAverageCost,
            product.UnitPrice,
            product.ReorderPoint,
            product.TargetStockLevel,
            product.Status,
            product.AttributesJson,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }

    public async Task<ProductDto> UpdateProductAsync(
        Guid productId,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateProductValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == productId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), productId);

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        var normalizedSku = Product.NormalizeSku(request.Sku);
        var skuConflict = await _dbContext.Products
            .AnyAsync(p => p.TenantId == tenantId && p.Id != productId && p.Sku == normalizedSku, cancellationToken);

        if (skuConflict)
        {
            throw new ConflictException("Product.SkuAlreadyExists", $"Another product with SKU '{normalizedSku}' already exists.");
        }

        var before = new
        {
            product.Sku,
            product.Name,
            product.StandardCost,
            product.UnitPrice,
            product.ReorderPoint,
            product.Status
        };

        product.UpdateDetails(
            request.CategoryId,
            normalizedSku,
            request.Name,
            request.Description,
            request.Barcode,
            request.UnitOfMeasure,
            request.StandardCost,
            request.UnitPrice,
            request.ReorderPoint,
            request.TargetStockLevel,
            request.Status,
            request.AttributesJson);

        _dbContext.RecordAudit(
            action: "Product.Updated",
            entityType: nameof(Product),
            entityId: product.Id.ToString(),
            summary: $"Updated product '{product.Sku}' ({product.Name}).",
            beforeState: before,
            afterState: new
            {
                product.Sku,
                product.Name,
                product.StandardCost,
                product.UnitPrice,
                product.ReorderPoint,
                product.Status
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProductDto(
            product.Id,
            product.CategoryId,
            category.Name,
            product.Sku,
            product.Name,
            product.Description,
            product.Barcode,
            product.UnitOfMeasure,
            product.StandardCost,
            product.MovingAverageCost,
            product.UnitPrice,
            product.ReorderPoint,
            product.TargetStockLevel,
            product.Status,
            product.AttributesJson,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);
    }
}
