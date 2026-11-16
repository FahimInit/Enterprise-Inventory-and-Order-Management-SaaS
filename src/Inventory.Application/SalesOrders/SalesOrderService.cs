using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.Inventory;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.SalesOrders;

public sealed record SalesOrderFilterRequest : PagedRequest
{
    public Guid? WarehouseId { get; init; }
    public SalesOrderStatus? Status { get; init; }
}

public sealed record CreateSalesOrderLineRequest(
    Guid ProductId,
    int Quantity);

public sealed record CreateSalesOrderRequest(
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? ShippingAddress,
    Guid FulfillmentWarehouseId,
    decimal? TaxRatePercent,
    decimal DiscountAmount,
    string? Notes,
    bool ConfirmImmediately,
    IReadOnlyList<CreateSalesOrderLineRequest> Lines);

public sealed record CancelSalesOrderRequest(
    string Reason);

public sealed record SalesOrderLineDto(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record SalesOrderDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? ShippingAddress,
    Guid FulfillmentWarehouseId,
    string FulfillmentWarehouseCode,
    string FulfillmentWarehouseName,
    SalesOrderStatus Status,
    decimal SubtotalAmount,
    decimal TaxRatePercent,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? Notes,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset? FulfilledAtUtc,
    DateTimeOffset? CancelledAtUtc,
    IReadOnlyList<SalesOrderLineDto> Lines);

public sealed class CreateSalesOrderRequestValidator : AbstractValidator<CreateSalesOrderRequest>
{
    public CreateSalesOrderRequestValidator()
    {
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(200);

        RuleFor(x => x.CustomerEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));

        RuleFor(x => x.FulfillmentWarehouseId)
            .NotEmpty().WithMessage("Fulfillment warehouse is required.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0m);

        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty().WithMessage("At least one sales order line is required.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Line quantity must be greater than zero.");
        });
    }
}

public interface ISalesOrderService
{
    Task<PagedResponse<SalesOrderDto>> GetSalesOrdersAsync(SalesOrderFilterRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> GetSalesOrderByIdAsync(Guid salesOrderId, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> CreateSalesOrderAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> ConfirmSalesOrderAsync(Guid salesOrderId, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> StartProcessingSalesOrderAsync(Guid salesOrderId, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> FulfillSalesOrderAsync(Guid salesOrderId, CancellationToken cancellationToken = default);
    Task<SalesOrderDto> CancelSalesOrderAsync(Guid salesOrderId, CancelSalesOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed class SalesOrderService : ISalesOrderService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly CreateSalesOrderRequestValidator _createValidator = new();

    public SalesOrderService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResponse<SalesOrderDto>> GetSalesOrdersAsync(
        SalesOrderFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from so in _dbContext.SalesOrders.AsNoTracking()
                    join w in _dbContext.Warehouses.AsNoTracking() on so.FulfillmentWarehouseId equals w.Id
                    where so.TenantId == tenantId
                    select new
                    {
                        Order = so,
                        WarehouseCode = w.Code,
                        WarehouseName = w.Name
                    };

        if (request.WarehouseId.HasValue)
        {
            query = query.Where(x => x.Order.FulfillmentWarehouseId == request.WarehouseId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Order.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Order.OrderNumber.ToLower().Contains(term) ||
                x.Order.CustomerName.ToLower().Contains(term) ||
                (x.Order.CustomerEmail != null && x.Order.CustomerEmail.ToLower().Contains(term)));
        }

        query = query.OrderByDescending(x => x.Order.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, PagedRequest.MaxPageSize);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var headers = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orderIds = headers.Select(h => h.Order.Id).ToList();

        var lines = await (
            from l in _dbContext.SalesOrderLines.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on l.ProductId equals p.Id
            where l.TenantId == tenantId && orderIds.Contains(l.SalesOrderId)
            orderby p.Sku
            select new
            {
                l.SalesOrderId,
                Dto = new SalesOrderLineDto(
                    l.Id,
                    l.ProductId,
                    p.Sku,
                    p.Name,
                    p.UnitOfMeasure,
                    l.Quantity,
                    l.UnitPrice,
                    l.LineTotal)
            }).ToListAsync(cancellationToken);

        var linesLookup = lines.ToLookup(x => x.SalesOrderId, x => x.Dto);

        var items = headers
            .Select(h => new SalesOrderDto(
                h.Order.Id,
                h.Order.OrderNumber,
                h.Order.CustomerName,
                h.Order.CustomerEmail,
                h.Order.CustomerPhone,
                h.Order.ShippingAddress,
                h.Order.FulfillmentWarehouseId,
                h.WarehouseCode,
                h.WarehouseName,
                h.Order.Status,
                h.Order.SubtotalAmount,
                h.Order.TaxRatePercent,
                h.Order.TaxAmount,
                h.Order.DiscountAmount,
                h.Order.TotalAmount,
                h.Order.Notes,
                h.Order.CancellationReason,
                h.Order.CreatedAtUtc,
                h.Order.ConfirmedAtUtc,
                h.Order.FulfilledAtUtc,
                h.Order.CancelledAtUtc,
                linesLookup[h.Order.Id].ToList()))
            .ToList();

        return new PagedResponse<SalesOrderDto>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            pageNumber > 1,
            pageNumber < totalPages);
    }

    public async Task<SalesOrderDto> GetSalesOrderByIdAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var header = await (
            from so in _dbContext.SalesOrders.AsNoTracking()
            join w in _dbContext.Warehouses.AsNoTracking() on so.FulfillmentWarehouseId equals w.Id
            where so.TenantId == tenantId && so.Id == salesOrderId
            select new
            {
                Order = so,
                WarehouseCode = w.Code,
                WarehouseName = w.Name
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);

        var lines = await (
            from l in _dbContext.SalesOrderLines.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on l.ProductId equals p.Id
            where l.TenantId == tenantId && l.SalesOrderId == salesOrderId
            orderby p.Sku
            select new SalesOrderLineDto(
                l.Id,
                l.ProductId,
                p.Sku,
                p.Name,
                p.UnitOfMeasure,
                l.Quantity,
                l.UnitPrice,
                l.LineTotal))
            .ToListAsync(cancellationToken);

        return new SalesOrderDto(
            header.Order.Id,
            header.Order.OrderNumber,
            header.Order.CustomerName,
            header.Order.CustomerEmail,
            header.Order.CustomerPhone,
            header.Order.ShippingAddress,
            header.Order.FulfillmentWarehouseId,
            header.WarehouseCode,
            header.WarehouseName,
            header.Order.Status,
            header.Order.SubtotalAmount,
            header.Order.TaxRatePercent,
            header.Order.TaxAmount,
            header.Order.DiscountAmount,
            header.Order.TotalAmount,
            header.Order.Notes,
            header.Order.CancellationReason,
            header.Order.CreatedAtUtc,
            header.Order.ConfirmedAtUtc,
            header.Order.FulfilledAtUtc,
            header.Order.CancelledAtUtc,
            lines);
    }

    public async Task<SalesOrderDto> CreateSalesOrderAsync(
        CreateSalesOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var warehouse = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == request.FulfillmentWarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.FulfillmentWarehouseId);

        if (!warehouse.IsActive)
        {
            throw new DomainException("Warehouse.Inactive", $"Warehouse '{warehouse.Code}' is inactive and cannot fulfill sales orders.");
        }

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstAsync(o => o.Id == tenantId, cancellationToken);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (products.Count != productIds.Count)
        {
            throw new DomainException("SalesOrder.InvalidProducts", "One or more requested products were not found in your organization.");
        }

        foreach (var product in products.Values)
        {
            product.EnsureCanBeOrdered();
        }

        // Authoritative server-side price lookup: never trust client-submitted prices
        var authoritativeLines = request.Lines.Select(l =>
        {
            var product = products[l.ProductId];
            return (
                l.ProductId,
                l.Quantity,
                AuthoritativeUnitPrice: product.UnitPrice,
                UnitCostSnapshot: product.MovingAverageCost);
        });

        var orderNumber = $"SO-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var taxRate = request.TaxRatePercent ?? organization.DefaultTaxRatePercent;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var salesOrder = new SalesOrder(
            tenantId,
            orderNumber,
            request.CustomerName,
            request.CustomerEmail,
            request.CustomerPhone,
            request.ShippingAddress,
            warehouse.Id,
            taxRate,
            request.DiscountAmount,
            request.Notes,
            _tenantContext.UserId,
            authoritativeLines,
            submitImmediately: true);

        _dbContext.SalesOrders.Add(salesOrder);

        if (request.ConfirmImmediately)
        {
            await ReserveStockForOrderInternalAsync(salesOrder, cancellationToken);
        }

        _dbContext.RecordAudit(
            action: request.ConfirmImmediately ? "SalesOrder.CreatedAndConfirmed" : "SalesOrder.Created",
            entityType: nameof(SalesOrder),
            entityId: salesOrder.Id.ToString(),
            summary: $"Created sales order '{salesOrder.OrderNumber}' for '{salesOrder.CustomerName}' totaling {salesOrder.TotalAmount:F2} (Status: {salesOrder.Status}).",
            afterState: new
            {
                salesOrder.Id,
                salesOrder.OrderNumber,
                salesOrder.CustomerName,
                salesOrder.Status,
                salesOrder.TotalAmount
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetSalesOrderByIdAsync(salesOrder.Id, cancellationToken);
    }

    public async Task<SalesOrderDto> ConfirmSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var salesOrder = await _dbContext.SalesOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == salesOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);

        await ReserveStockForOrderInternalAsync(salesOrder, cancellationToken);

        _dbContext.RecordAudit(
            action: "SalesOrder.Confirmed",
            entityType: nameof(SalesOrder),
            entityId: salesOrder.Id.ToString(),
            summary: $"Confirmed sales order '{salesOrder.OrderNumber}' and reserved inventory for {salesOrder.Lines.Count} line(s).");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetSalesOrderByIdAsync(salesOrder.Id, cancellationToken);
    }

    public async Task<SalesOrderDto> StartProcessingSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var salesOrder = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == salesOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);

        salesOrder.StartProcessing();

        _dbContext.RecordAudit(
            action: "SalesOrder.ProcessingStarted",
            entityType: nameof(SalesOrder),
            entityId: salesOrder.Id.ToString(),
            summary: $"Marked sales order '{salesOrder.OrderNumber}' as Processing.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetSalesOrderByIdAsync(salesOrder.Id, cancellationToken);
    }

    public async Task<SalesOrderDto> FulfillSalesOrderAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var salesOrder = await _dbContext.SalesOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == salesOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);

        salesOrder.MarkFulfilled(_tenantContext.UserId);

        var activeReservations = await _dbContext.StockReservations
            .Where(r => r.TenantId == tenantId &&
                        r.SalesOrderId == salesOrder.Id &&
                        r.Status == ReservationStatus.Active)
            .OrderBy(r => r.ProductId)
            .ToListAsync(cancellationToken);

        if (activeReservations.Count == 0)
        {
            throw new ConflictException(
                "SalesOrder.MissingActiveReservations",
                $"Cannot fulfill sales order '{salesOrder.OrderNumber}' because no active stock reservations were found.");
        }

        var lockTargets = activeReservations.Select(r => (r.WarehouseId, r.ProductId));
        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            lockTargets,
            cancellationToken);

        var lineCostLookup = salesOrder.Lines.ToDictionary(l => l.Id, l => l.UnitCostSnapshot);

        foreach (var reservation in activeReservations)
        {
            var inventoryItem = lockedItems[(reservation.WarehouseId, reservation.ProductId)];
            var costSnapshot = lineCostLookup.GetValueOrDefault(reservation.SalesOrderLineId, 0m);

            var movement = inventoryItem.FulfillReservedStock(
                reservation,
                costSnapshot,
                salesOrder.OrderNumber,
                _tenantContext.UserId);

            _dbContext.StockMovements.Add(movement);
        }

        _dbContext.RecordAudit(
            action: "SalesOrder.Fulfilled",
            entityType: nameof(SalesOrder),
            entityId: salesOrder.Id.ToString(),
            summary: $"Fulfilled sales order '{salesOrder.OrderNumber}' and deducted {activeReservations.Sum(r => r.Quantity)} reserved unit(s) from on-hand stock.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetSalesOrderByIdAsync(salesOrder.Id, cancellationToken);
    }

    public async Task<SalesOrderDto> CancelSalesOrderAsync(
        Guid salesOrderId,
        CancelSalesOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RequestValidationException(nameof(request.Reason), "A cancellation reason is required.");
        }

        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var salesOrder = await _dbContext.SalesOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == salesOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);

        var hadActiveReservations = salesOrder.Cancel(request.Reason);

        if (hadActiveReservations)
        {
            var activeReservations = await _dbContext.StockReservations
                .Where(r => r.TenantId == tenantId &&
                            r.SalesOrderId == salesOrder.Id &&
                            r.Status == ReservationStatus.Active)
                .OrderBy(r => r.ProductId)
                .ToListAsync(cancellationToken);

            var lockTargets = activeReservations.Select(r => (r.WarehouseId, r.ProductId));
            var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
                tenantId,
                lockTargets,
                cancellationToken);

            foreach (var reservation in activeReservations)
            {
                var inventoryItem = lockedItems[(reservation.WarehouseId, reservation.ProductId)];
                inventoryItem.ReleaseReservation(reservation, request.Reason);
            }
        }

        _dbContext.RecordAudit(
            action: "SalesOrder.Cancelled",
            entityType: nameof(SalesOrder),
            entityId: salesOrder.Id.ToString(),
            summary: $"Cancelled sales order '{salesOrder.OrderNumber}'. ReleasedReservations={hadActiveReservations}. Reason: {request.Reason}");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetSalesOrderByIdAsync(salesOrder.Id, cancellationToken);
    }

    private async Task ReserveStockForOrderInternalAsync(
        SalesOrder salesOrder,
        CancellationToken cancellationToken)
    {
        var tenantId = salesOrder.TenantId;

        // Confirm state transition first to validate order status
        salesOrder.Confirm(_tenantContext.UserId);

        // Sort targets deterministically by ProductId and lock inventory_items rows (SELECT ... FOR UPDATE)
        var lockTargets = salesOrder.Lines
            .OrderBy(l => l.ProductId)
            .Select(l => (salesOrder.FulfillmentWarehouseId, l.ProductId));

        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            lockTargets,
            cancellationToken);

        foreach (var line in salesOrder.Lines.OrderBy(l => l.ProductId))
        {
            var inventoryItem = lockedItems[(salesOrder.FulfillmentWarehouseId, line.ProductId)];

            var reservation = inventoryItem.ReserveStock(
                line.Quantity,
                salesOrder.Id,
                line.Id,
                salesOrder.OrderNumber);

            _dbContext.StockReservations.Add(reservation);
        }
    }
}
