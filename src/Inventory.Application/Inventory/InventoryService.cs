using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Inventory;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Inventory;

public sealed record InventoryFilterRequest : PagedRequest
{
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? LowStockOnly { get; init; }
}

public sealed record StockMovementFilterRequest : PagedRequest
{
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public StockMovementType? MovementType { get; init; }
}

public sealed record StockReservationFilterRequest : PagedRequest
{
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public Guid? SalesOrderId { get; init; }
    public ReservationStatus? Status { get; init; }
}

public sealed record AdjustInventoryRequest(
    Guid WarehouseId,
    Guid ProductId,
    int QuantityDelta,
    string Reason,
    string? ReferenceNumber = null,
    string? Notes = null);

public sealed record ReconcileInventoryRequest(
    Guid WarehouseId,
    Guid ProductId,
    int CountedPhysicalQuantity,
    string Reason,
    string? Notes = null);

public sealed record InventoryBalanceDto(
    Guid InventoryItemId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string CategoryName,
    string UnitOfMeasure,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int InTransitQuantity,
    int ReorderPoint,
    int TargetStockLevel,
    bool IsLowStock,
    decimal MovingAverageCost,
    decimal TotalInventoryValue,
    DateTimeOffset? LastCountedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record StockMovementDto(
    Guid Id,
    Guid InventoryItemId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    StockMovementType MovementType,
    int QuantityDelta,
    int BalanceBefore,
    int BalanceAfter,
    decimal UnitCost,
    string ReferenceType,
    string ReferenceNumber,
    Guid? ReferenceId,
    string Reason,
    string? Notes,
    Guid? CreatedByUserId,
    DateTimeOffset OccurredAtUtc);

public sealed record StockReservationDto(
    Guid Id,
    Guid InventoryItemId,
    Guid WarehouseId,
    string WarehouseCode,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid SalesOrderId,
    string OrderNumber,
    int Quantity,
    ReservationStatus Status,
    string? ReleaseReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record ReconciliationResultDto(
    Guid ReconciliationId,
    Guid InventoryItemId,
    Guid WarehouseId,
    Guid ProductId,
    string ProductSku,
    int SystemQuantityBefore,
    int LedgerCalculatedQuantity,
    int CountedQuantity,
    int VarianceQuantity,
    bool WasLedgerConsistentBeforeCount,
    Guid? AdjustmentMovementId,
    DateTimeOffset PerformedAtUtc);

public sealed class AdjustInventoryRequestValidator : AbstractValidator<AdjustInventoryRequest>
{
    public AdjustInventoryRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.QuantityDelta)
            .NotEqual(0).WithMessage("Adjustment quantity delta must be non-zero.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A business reason is required for stock adjustments.")
            .MaximumLength(300);
    }
}

public sealed class ReconcileInventoryRequestValidator : AbstractValidator<ReconcileInventoryRequest>
{
    public ReconcileInventoryRequestValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CountedPhysicalQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Counted physical quantity cannot be negative.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reconciliation reason is required.")
            .MaximumLength(300);
    }
}

public interface IInventoryService
{
    Task<PagedResponse<InventoryBalanceDto>> GetInventoryBalancesAsync(InventoryFilterRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<StockMovementDto>> GetStockMovementsAsync(StockMovementFilterRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<StockReservationDto>> GetStockReservationsAsync(StockReservationFilterRequest request, CancellationToken cancellationToken = default);
    Task<InventoryBalanceDto> AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken cancellationToken = default);
    Task<ReconciliationResultDto> ReconcileInventoryAsync(ReconcileInventoryRequest request, CancellationToken cancellationToken = default);
}

public sealed class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly AdjustInventoryRequestValidator _adjustValidator = new();
    private readonly ReconcileInventoryRequestValidator _reconcileValidator = new();

    public InventoryService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResponse<InventoryBalanceDto>> GetInventoryBalancesAsync(
        InventoryFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from i in _dbContext.InventoryItems.AsNoTracking()
                    join w in _dbContext.Warehouses.AsNoTracking() on i.WarehouseId equals w.Id
                    join p in _dbContext.Products.AsNoTracking() on i.ProductId equals p.Id
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    where i.TenantId == tenantId
                    select new
                    {
                        Item = i,
                        WarehouseCode = w.Code,
                        WarehouseName = w.Name,
                        Product = p,
                        CategoryName = c.Name
                    };

        if (request.WarehouseId.HasValue && request.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Item.WarehouseId == request.WarehouseId.Value);
        }

        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Item.ProductId == request.ProductId.Value);
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Product.CategoryId == request.CategoryId.Value);
        }

        if (request.LowStockOnly == true)
        {
            query = query.Where(x => (x.Item.OnHandQuantity - x.Item.ReservedQuantity) <= x.Product.ReorderPoint);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Product.Sku.ToLower().Contains(term) ||
                x.Product.Name.ToLower().Contains(term) ||
                x.WarehouseCode.ToLower().Contains(term) ||
                x.WarehouseName.ToLower().Contains(term));
        }

        query = query.OrderBy(x => x.WarehouseCode).ThenBy(x => x.Product.Sku);

        var projected = query.Select(x => new InventoryBalanceDto(
            x.Item.Id,
            x.Item.WarehouseId,
            x.WarehouseCode,
            x.WarehouseName,
            x.Item.ProductId,
            x.Product.Sku,
            x.Product.Name,
            x.CategoryName,
            x.Product.UnitOfMeasure,
            x.Item.OnHandQuantity,
            x.Item.ReservedQuantity,
            x.Item.OnHandQuantity - x.Item.ReservedQuantity,
            x.Item.InTransitQuantity,
            x.Product.ReorderPoint,
            x.Product.TargetStockLevel,
            (x.Item.OnHandQuantity - x.Item.ReservedQuantity) <= x.Product.ReorderPoint,
            x.Product.MovingAverageCost,
            decimal.Round(x.Item.OnHandQuantity * x.Product.MovingAverageCost, 4),
            x.Item.LastCountedAtUtc,
            x.Item.UpdatedAtUtc));

        return await PagedResponse<InventoryBalanceDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }

    public async Task<PagedResponse<StockMovementDto>> GetStockMovementsAsync(
        StockMovementFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from m in _dbContext.StockMovements.AsNoTracking()
                    join w in _dbContext.Warehouses.AsNoTracking() on m.WarehouseId equals w.Id
                    join p in _dbContext.Products.AsNoTracking() on m.ProductId equals p.Id
                    where m.TenantId == tenantId
                    select new
                    {
                        Movement = m,
                        WarehouseCode = w.Code,
                        WarehouseName = w.Name,
                        ProductSku = p.Sku,
                        ProductName = p.Name
                    };

        if (request.WarehouseId.HasValue && request.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Movement.WarehouseId == request.WarehouseId.Value);
        }

        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Movement.ProductId == request.ProductId.Value);
        }

        if (request.MovementType.HasValue)
        {
            query = query.Where(x => x.Movement.MovementType == request.MovementType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.ProductSku.ToLower().Contains(term) ||
                x.ProductName.ToLower().Contains(term) ||
                x.Movement.ReferenceNumber.ToLower().Contains(term) ||
                x.Movement.Reason.ToLower().Contains(term));
        }

        query = query.OrderByDescending(x => x.Movement.OccurredAtUtc);

        var projected = query.Select(x => new StockMovementDto(
            x.Movement.Id,
            x.Movement.InventoryItemId,
            x.Movement.WarehouseId,
            x.WarehouseCode,
            x.WarehouseName,
            x.Movement.ProductId,
            x.ProductSku,
            x.ProductName,
            x.Movement.MovementType,
            x.Movement.QuantityDelta,
            x.Movement.BalanceBefore,
            x.Movement.BalanceAfter,
            x.Movement.UnitCost,
            x.Movement.ReferenceType,
            x.Movement.ReferenceNumber,
            x.Movement.ReferenceId,
            x.Movement.Reason,
            x.Movement.Notes,
            x.Movement.CreatedByUserId,
            x.Movement.OccurredAtUtc));

        return await PagedResponse<StockMovementDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }

    public async Task<PagedResponse<StockReservationDto>> GetStockReservationsAsync(
        StockReservationFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from r in _dbContext.StockReservations.AsNoTracking()
                    join w in _dbContext.Warehouses.AsNoTracking() on r.WarehouseId equals w.Id
                    join p in _dbContext.Products.AsNoTracking() on r.ProductId equals p.Id
                    where r.TenantId == tenantId
                    select new
                    {
                        Reservation = r,
                        WarehouseCode = w.Code,
                        ProductSku = p.Sku,
                        ProductName = p.Name
                    };

        if (request.WarehouseId.HasValue && request.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Reservation.WarehouseId == request.WarehouseId.Value);
        }

        if (request.ProductId.HasValue && request.ProductId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Reservation.ProductId == request.ProductId.Value);
        }

        if (request.SalesOrderId.HasValue && request.SalesOrderId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Reservation.SalesOrderId == request.SalesOrderId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Reservation.Status == request.Status.Value);
        }

        query = query.OrderByDescending(x => x.Reservation.CreatedAtUtc);

        var projected = query.Select(x => new StockReservationDto(
            x.Reservation.Id,
            x.Reservation.InventoryItemId,
            x.Reservation.WarehouseId,
            x.WarehouseCode,
            x.Reservation.ProductId,
            x.ProductSku,
            x.ProductName,
            x.Reservation.SalesOrderId,
            x.Reservation.OrderNumber,
            x.Reservation.Quantity,
            x.Reservation.Status,
            x.Reservation.ReleaseReason,
            x.Reservation.CreatedAtUtc,
            x.Reservation.ResolvedAtUtc));

        return await PagedResponse<StockReservationDto>.CreateAsync(
            projected,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }

    public async Task<InventoryBalanceDto> AdjustInventoryAsync(
        AdjustInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await _adjustValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var warehouse = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstAsync(c => c.TenantId == tenantId && c.Id == product.CategoryId, cancellationToken);

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            [(request.WarehouseId, request.ProductId)],
            cancellationToken);

        var item = lockedItems[(request.WarehouseId, request.ProductId)];
        var balanceBefore = item.OnHandQuantity;

        var movement = item.AdjustStock(
            request.QuantityDelta,
            product.MovingAverageCost,
            request.Reason,
            request.ReferenceNumber,
            request.Notes,
            _tenantContext.UserId);

        _dbContext.StockMovements.Add(movement);

        _dbContext.RecordAudit(
            action: "Inventory.Adjusted",
            entityType: nameof(InventoryItem),
            entityId: item.Id.ToString(),
            summary: $"Adjusted stock for '{product.Sku}' in warehouse '{warehouse.Code}' by {request.QuantityDelta:+#;-#;0} ({balanceBefore} -> {item.OnHandQuantity}). Reason: {request.Reason}",
            beforeState: new { OnHandQuantity = balanceBefore },
            afterState: new { item.OnHandQuantity, item.ReservedQuantity, item.AvailableQuantity, MovementId = movement.Id });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new InventoryBalanceDto(
            item.Id,
            item.WarehouseId,
            warehouse.Code,
            warehouse.Name,
            item.ProductId,
            product.Sku,
            product.Name,
            category.Name,
            product.UnitOfMeasure,
            item.OnHandQuantity,
            item.ReservedQuantity,
            item.AvailableQuantity,
            item.InTransitQuantity,
            product.ReorderPoint,
            product.TargetStockLevel,
            item.AvailableQuantity <= product.ReorderPoint,
            product.MovingAverageCost,
            decimal.Round(item.OnHandQuantity * product.MovingAverageCost, 4),
            item.LastCountedAtUtc,
            item.UpdatedAtUtc);
    }

    public async Task<ReconciliationResultDto> ReconcileInventoryAsync(
        ReconcileInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await _reconcileValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var warehouseExists = await _dbContext.Warehouses
            .AnyAsync(w => w.TenantId == tenantId && w.Id == request.WarehouseId, cancellationToken);
        if (!warehouseExists)
        {
            throw new NotFoundException(nameof(Warehouse), request.WarehouseId);
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            [(request.WarehouseId, request.ProductId)],
            cancellationToken);

        var item = lockedItems[(request.WarehouseId, request.ProductId)];

        var ledgerSum = await _dbContext.StockMovements
            .Where(m => m.TenantId == tenantId && m.InventoryItemId == item.Id)
            .SumAsync(m => (int?)m.QuantityDelta, cancellationToken) ?? 0;

        var wasLedgerConsistent = item.OnHandQuantity == ledgerSum;

        var (reconciliation, movement) = item.ReconcilePhysicalCount(
            request.CountedPhysicalQuantity,
            ledgerSum,
            product.MovingAverageCost,
            request.Reason,
            request.Notes,
            _tenantContext.UserId);

        _dbContext.InventoryReconciliations.Add(reconciliation);

        if (movement is not null)
        {
            _dbContext.StockMovements.Add(movement);
        }

        _dbContext.RecordAudit(
            action: "Inventory.Reconciled",
            entityType: nameof(InventoryItem),
            entityId: item.Id.ToString(),
            summary: $"Reconciled '{product.Sku}': SystemBefore={reconciliation.SystemQuantityBefore}, LedgerSum={ledgerSum}, Counted={reconciliation.CountedQuantity}, Variance={reconciliation.VarianceQuantity}.",
            beforeState: new { OnHandQuantity = reconciliation.SystemQuantityBefore, LedgerSum = ledgerSum },
            afterState: new { item.OnHandQuantity, reconciliation.VarianceQuantity, ReconciliationId = reconciliation.Id });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReconciliationResultDto(
            reconciliation.Id,
            item.Id,
            item.WarehouseId,
            item.ProductId,
            product.Sku,
            reconciliation.SystemQuantityBefore,
            reconciliation.LedgerCalculatedQuantity,
            reconciliation.CountedQuantity,
            reconciliation.VarianceQuantity,
            wasLedgerConsistent,
            movement?.Id,
            reconciliation.PerformedAtUtc);
    }
}
