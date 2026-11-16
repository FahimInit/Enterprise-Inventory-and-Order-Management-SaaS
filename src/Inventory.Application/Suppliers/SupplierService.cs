using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Suppliers;

public sealed record SupplierFilterRequest : PagedRequest
{
    public bool? IsActive { get; init; }
}

public sealed record CreateSupplierRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Country,
    string PaymentTerms = "Net 30",
    int LeadTimeDays = 7,
    bool IsActive = true,
    string? Notes = null);

public sealed record UpdateSupplierRequest(
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Country,
    string PaymentTerms,
    int LeadTimeDays,
    bool IsActive,
    string? Notes);

public sealed record LinkSupplierProductRequest(
    Guid ProductId,
    string? SupplierSku,
    decimal AgreedUnitCost,
    int MinimumOrderQuantity = 1,
    int LeadTimeDays = 7,
    bool IsPreferred = false);

public sealed record SupplierProductDto(
    Guid Id,
    Guid SupplierId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string? SupplierSku,
    decimal AgreedUnitCost,
    int MinimumOrderQuantity,
    int LeadTimeDays,
    bool IsPreferred);

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? Country,
    string PaymentTerms,
    int LeadTimeDays,
    bool IsActive,
    string? Notes,
    int LinkedProductCount,
    DateTimeOffset CreatedAtUtc);

public sealed record SupplierDetailsDto(
    SupplierDto Supplier,
    IReadOnlyList<SupplierProductDto> Products);

public sealed class CreateSupplierRequestValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class LinkSupplierProductRequestValidator : AbstractValidator<LinkSupplierProductRequest>
{
    public LinkSupplierProductRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.AgreedUnitCost).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.MinimumOrderQuantity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.LeadTimeDays).GreaterThanOrEqualTo(0);
    }
}

public interface ISupplierService
{
    Task<PagedResponse<SupplierDto>> GetSuppliersAsync(SupplierFilterRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDetailsDto> GetSupplierByIdAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<SupplierDto> CreateSupplierAsync(CreateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierDto> UpdateSupplierAsync(Guid supplierId, UpdateSupplierRequest request, CancellationToken cancellationToken = default);
    Task<SupplierProductDto> LinkSupplierProductAsync(Guid supplierId, LinkSupplierProductRequest request, CancellationToken cancellationToken = default);
}

public sealed class SupplierService : ISupplierService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly CreateSupplierRequestValidator _createValidator = new();
    private readonly UpdateSupplierRequestValidator _updateValidator = new();
    private readonly LinkSupplierProductRequestValidator _linkValidator = new();

    public SupplierService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResponse<SupplierDto>> GetSuppliersAsync(
        SupplierFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = _dbContext.Suppliers
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        if (request.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Code.ToLower().Contains(term) ||
                s.Name.ToLower().Contains(term) ||
                (s.ContactName != null && s.ContactName.ToLower().Contains(term)));
        }

        query = query.OrderBy(s => s.Name);

        var projected = query.Select(s => new SupplierDto(
            s.Id,
            s.Code,
            s.Name,
            s.ContactName,
            s.Email,
            s.Phone,
            s.AddressLine1,
            s.City,
            s.Country,
            s.PaymentTerms,
            s.LeadTimeDays,
            s.IsActive,
            s.Notes,
            s.Products.Count,
            s.CreatedAtUtc));

        return await PagedResponse<SupplierDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }

    public async Task<SupplierDetailsDto> GetSupplierByIdAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == supplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), supplierId);

        var products = await (
            from sp in _dbContext.SupplierProducts.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on sp.ProductId equals p.Id
            where sp.TenantId == tenantId && sp.SupplierId == supplierId
            orderby sp.IsPreferred descending, p.Sku
            select new SupplierProductDto(
                sp.Id,
                sp.SupplierId,
                sp.ProductId,
                p.Sku,
                p.Name,
                sp.SupplierSku,
                sp.AgreedUnitCost,
                sp.MinimumOrderQuantity,
                sp.LeadTimeDays,
                sp.IsPreferred)).ToListAsync(cancellationToken);

        var dto = new SupplierDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.AddressLine1,
            supplier.City,
            supplier.Country,
            supplier.PaymentTerms,
            supplier.LeadTimeDays,
            supplier.IsActive,
            supplier.Notes,
            products.Count,
            supplier.CreatedAtUtc);

        return new SupplierDetailsDto(dto, products);
    }

    public async Task<SupplierDto> CreateSupplierAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _dbContext.Suppliers
            .AnyAsync(s => s.TenantId == tenantId && s.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new ConflictException("Supplier.CodeAlreadyExists", $"A supplier with code '{normalizedCode}' already exists.");
        }

        var supplier = new Supplier(
            tenantId,
            normalizedCode,
            request.Name,
            request.ContactName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.Country,
            request.PaymentTerms,
            request.LeadTimeDays,
            request.IsActive,
            request.Notes);

        _dbContext.Suppliers.Add(supplier);

        _dbContext.RecordAudit(
            action: "Supplier.Created",
            entityType: nameof(Supplier),
            entityId: supplier.Id.ToString(),
            summary: $"Created supplier '{supplier.Code}' ({supplier.Name}).",
            afterState: new { supplier.Id, supplier.Code, supplier.Name });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.AddressLine1,
            supplier.City,
            supplier.Country,
            supplier.PaymentTerms,
            supplier.LeadTimeDays,
            supplier.IsActive,
            supplier.Notes,
            LinkedProductCount: 0,
            supplier.CreatedAtUtc);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(
        Guid supplierId,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var supplier = await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == supplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), supplierId);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var conflict = await _dbContext.Suppliers
            .AnyAsync(s => s.TenantId == tenantId && s.Id != supplierId && s.Code == normalizedCode, cancellationToken);

        if (conflict)
        {
            throw new ConflictException("Supplier.CodeAlreadyExists", $"Another supplier with code '{normalizedCode}' already exists.");
        }

        supplier.Update(
            normalizedCode,
            request.Name,
            request.ContactName,
            request.Email,
            request.Phone,
            request.AddressLine1,
            request.City,
            request.Country,
            request.PaymentTerms,
            request.LeadTimeDays,
            request.IsActive,
            request.Notes);

        _dbContext.RecordAudit(
            action: "Supplier.Updated",
            entityType: nameof(Supplier),
            entityId: supplier.Id.ToString(),
            summary: $"Updated supplier '{supplier.Code}' ({supplier.Name}).",
            afterState: new { supplier.Id, supplier.Code, supplier.Name, supplier.IsActive });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var count = await _dbContext.SupplierProducts
            .CountAsync(sp => sp.TenantId == tenantId && sp.SupplierId == supplierId, cancellationToken);

        return new SupplierDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.AddressLine1,
            supplier.City,
            supplier.Country,
            supplier.PaymentTerms,
            supplier.LeadTimeDays,
            supplier.IsActive,
            supplier.Notes,
            count,
            supplier.CreatedAtUtc);
    }

    public async Task<SupplierProductDto> LinkSupplierProductAsync(
        Guid supplierId,
        LinkSupplierProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await _linkValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var supplierExists = await _dbContext.Suppliers
            .AnyAsync(s => s.TenantId == tenantId && s.Id == supplierId, cancellationToken);
        if (!supplierExists)
        {
            throw new NotFoundException(nameof(Supplier), supplierId);
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var existingLink = await _dbContext.SupplierProducts
            .FirstOrDefaultAsync(
                sp => sp.TenantId == tenantId && sp.SupplierId == supplierId && sp.ProductId == request.ProductId,
                cancellationToken);

        if (existingLink is not null)
        {
            existingLink.UpdateTerms(
                request.SupplierSku,
                request.AgreedUnitCost,
                request.MinimumOrderQuantity,
                request.LeadTimeDays,
                request.IsPreferred);
        }
        else
        {
            existingLink = new SupplierProduct(
                tenantId,
                supplierId,
                request.ProductId,
                request.SupplierSku,
                request.AgreedUnitCost,
                request.MinimumOrderQuantity,
                request.LeadTimeDays,
                request.IsPreferred);

            _dbContext.SupplierProducts.Add(existingLink);
        }

        _dbContext.RecordAudit(
            action: "SupplierProduct.Linked",
            entityType: nameof(SupplierProduct),
            entityId: existingLink.Id.ToString(),
            summary: $"Linked product '{product.Sku}' to supplier '{supplierId}' at cost {existingLink.AgreedUnitCost}.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SupplierProductDto(
            existingLink.Id,
            existingLink.SupplierId,
            existingLink.ProductId,
            product.Sku,
            product.Name,
            existingLink.SupplierSku,
            existingLink.AgreedUnitCost,
            existingLink.MinimumOrderQuantity,
            existingLink.LeadTimeDays,
            existingLink.IsPreferred);
    }
}
