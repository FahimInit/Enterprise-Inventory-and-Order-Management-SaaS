using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Common;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Warehouses;

public sealed record CreateWarehouseRequest(
    string Code,
    string Name,
    string? AddressLine1,
    string? City,
    string? StateOrProvince,
    string? PostalCode,
    string? Country,
    Guid? ManagerUserId = null,
    bool IsDefault = false,
    bool IsActive = true);

public sealed record UpdateWarehouseRequest(
    string Code,
    string Name,
    string? AddressLine1,
    string? City,
    string? StateOrProvince,
    string? PostalCode,
    string? Country,
    Guid? ManagerUserId,
    bool IsDefault,
    bool IsActive);

public sealed record CreateWarehouseLocationRequest(
    string Code,
    string Name,
    string Zone = "GENERAL",
    bool IsPickable = true,
    bool IsReceivable = true,
    bool IsActive = true);

public sealed record WarehouseLocationDto(
    Guid Id,
    Guid WarehouseId,
    string Code,
    string Name,
    string Zone,
    bool IsPickable,
    bool IsReceivable,
    bool IsActive);

public sealed record WarehouseDto(
    Guid Id,
    string Code,
    string Name,
    string? AddressLine1,
    string? City,
    string? StateOrProvince,
    string? PostalCode,
    string? Country,
    Guid? ManagerUserId,
    string? ManagerName,
    bool IsDefault,
    bool IsActive,
    int LocationCount,
    DateTimeOffset CreatedAtUtc);

public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
    }
}

public sealed class CreateWarehouseLocationRequestValidator : AbstractValidator<CreateWarehouseLocationRequest>
{
    public CreateWarehouseLocationRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Zone).NotEmpty().MaximumLength(64);
    }
}

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default);
    Task<WarehouseDto> GetWarehouseByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default);
    Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<WarehouseDto> UpdateWarehouseAsync(Guid warehouseId, UpdateWarehouseRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WarehouseLocationDto>> GetLocationsAsync(Guid warehouseId, CancellationToken cancellationToken = default);
    Task<WarehouseLocationDto> CreateLocationAsync(Guid warehouseId, CreateWarehouseLocationRequest request, CancellationToken cancellationToken = default);
}

public sealed class WarehouseService : IWarehouseService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly CreateWarehouseRequestValidator _createWarehouseValidator = new();
    private readonly UpdateWarehouseRequestValidator _updateWarehouseValidator = new();
    private readonly CreateWarehouseLocationRequestValidator _createLocationValidator = new();

    public WarehouseService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var warehouses = await (
            from w in _dbContext.Warehouses.AsNoTracking()
            join u in _dbContext.Users.AsNoTracking() on w.ManagerUserId equals u.Id into managers
            from manager in managers.DefaultIfEmpty()
            where w.TenantId == tenantId
            orderby w.IsDefault descending, w.Code
            select new WarehouseDto(
                w.Id,
                w.Code,
                w.Name,
                w.AddressLine1,
                w.City,
                w.StateOrProvince,
                w.PostalCode,
                w.Country,
                w.ManagerUserId,
                manager != null ? manager.FullName : null,
                w.IsDefault,
                w.IsActive,
                w.Locations.Count,
                w.CreatedAtUtc)).ToListAsync(cancellationToken);

        return warehouses;
    }

    public async Task<WarehouseDto> GetWarehouseByIdAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var warehouse = await (
            from w in _dbContext.Warehouses.AsNoTracking()
            join u in _dbContext.Users.AsNoTracking() on w.ManagerUserId equals u.Id into managers
            from manager in managers.DefaultIfEmpty()
            where w.TenantId == tenantId && w.Id == warehouseId
            select new WarehouseDto(
                w.Id,
                w.Code,
                w.Name,
                w.AddressLine1,
                w.City,
                w.StateOrProvince,
                w.PostalCode,
                w.Country,
                w.ManagerUserId,
                manager != null ? manager.FullName : null,
                w.IsDefault,
                w.IsActive,
                w.Locations.Count,
                w.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);

        return warehouse;
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createWarehouseValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;
        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _dbContext.Warehouses
            .AnyAsync(w => w.TenantId == tenantId && w.Code == normalizedCode, cancellationToken);

        if (exists)
        {
            throw new ConflictException("Warehouse.CodeAlreadyExists", $"A warehouse with code '{normalizedCode}' already exists.");
        }

        string? managerName = null;
        if (request.ManagerUserId.HasValue)
        {
            var managerMember = await (
                from m in _dbContext.OrganizationMembers.AsNoTracking()
                join u in _dbContext.Users.AsNoTracking() on m.UserId equals u.Id
                where m.TenantId == tenantId && m.UserId == request.ManagerUserId.Value && m.IsActive
                select u.FullName).FirstOrDefaultAsync(cancellationToken);

            if (managerMember is null)
            {
                throw new DomainException("Warehouse.InvalidManager", "The specified warehouse manager must be an active member of this organization.");
            }

            managerName = managerMember;
        }

        var warehouse = new Warehouse(
            tenantId,
            normalizedCode,
            request.Name,
            request.AddressLine1,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.Country,
            request.ManagerUserId,
            request.IsDefault,
            request.IsActive);

        // Seed a default general receiving/picking bin location automatically
        var defaultLocation = new WarehouseLocation(
            tenantId,
            warehouse.Id,
            "MAIN-01",
            "Main Floor Bin 01",
            "GENERAL");

        _dbContext.Warehouses.Add(warehouse);
        _dbContext.WarehouseLocations.Add(defaultLocation);

        _dbContext.RecordAudit(
            action: "Warehouse.Created",
            entityType: nameof(Warehouse),
            entityId: warehouse.Id.ToString(),
            summary: $"Created warehouse '{warehouse.Code}' ({warehouse.Name}).",
            afterState: new { warehouse.Id, warehouse.Code, warehouse.Name });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WarehouseDto(
            warehouse.Id,
            warehouse.Code,
            warehouse.Name,
            warehouse.AddressLine1,
            warehouse.City,
            warehouse.StateOrProvince,
            warehouse.PostalCode,
            warehouse.Country,
            warehouse.ManagerUserId,
            managerName,
            warehouse.IsDefault,
            warehouse.IsActive,
            LocationCount: 1,
            warehouse.CreatedAtUtc);
    }

    public async Task<WarehouseDto> UpdateWarehouseAsync(
        Guid warehouseId,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateWarehouseValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var conflict = await _dbContext.Warehouses
            .AnyAsync(w => w.TenantId == tenantId && w.Id != warehouseId && w.Code == normalizedCode, cancellationToken);

        if (conflict)
        {
            throw new ConflictException("Warehouse.CodeAlreadyExists", $"Another warehouse with code '{normalizedCode}' already exists.");
        }

        warehouse.Update(
            normalizedCode,
            request.Name,
            request.AddressLine1,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.Country,
            request.ManagerUserId,
            request.IsDefault,
            request.IsActive);

        _dbContext.RecordAudit(
            action: "Warehouse.Updated",
            entityType: nameof(Warehouse),
            entityId: warehouse.Id.ToString(),
            summary: $"Updated warehouse '{warehouse.Code}' ({warehouse.Name}).",
            afterState: new { warehouse.Id, warehouse.Code, warehouse.Name, warehouse.IsActive });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetWarehouseByIdAsync(warehouseId, cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseLocationDto>> GetLocationsAsync(
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var warehouseExists = await _dbContext.Warehouses
            .AnyAsync(w => w.TenantId == tenantId && w.Id == warehouseId, cancellationToken);

        if (!warehouseExists)
        {
            throw new NotFoundException(nameof(Warehouse), warehouseId);
        }

        return await _dbContext.WarehouseLocations
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.WarehouseId == warehouseId)
            .OrderBy(l => l.Zone)
            .ThenBy(l => l.Code)
            .Select(l => new WarehouseLocationDto(
                l.Id,
                l.WarehouseId,
                l.Code,
                l.Name,
                l.Zone,
                l.IsPickable,
                l.IsReceivable,
                l.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseLocationDto> CreateLocationAsync(
        Guid warehouseId,
        CreateWarehouseLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createLocationValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var warehouseExists = await _dbContext.Warehouses
            .AnyAsync(w => w.TenantId == tenantId && w.Id == warehouseId, cancellationToken);

        if (!warehouseExists)
        {
            throw new NotFoundException(nameof(Warehouse), warehouseId);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.WarehouseLocations
            .AnyAsync(
                l => l.TenantId == tenantId && l.WarehouseId == warehouseId && l.Code == normalizedCode,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException("WarehouseLocation.CodeAlreadyExists", $"Location code '{normalizedCode}' already exists in this warehouse.");
        }

        var location = new WarehouseLocation(
            tenantId,
            warehouseId,
            normalizedCode,
            request.Name,
            request.Zone,
            request.IsPickable,
            request.IsReceivable,
            request.IsActive);

        _dbContext.WarehouseLocations.Add(location);

        _dbContext.RecordAudit(
            action: "WarehouseLocation.Created",
            entityType: nameof(WarehouseLocation),
            entityId: location.Id.ToString(),
            summary: $"Created bin location '{location.Code}' in warehouse '{warehouseId}'.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new WarehouseLocationDto(
            location.Id,
            location.WarehouseId,
            location.Code,
            location.Name,
            location.Zone,
            location.IsPickable,
            location.IsReceivable,
            location.IsActive);
    }
}
