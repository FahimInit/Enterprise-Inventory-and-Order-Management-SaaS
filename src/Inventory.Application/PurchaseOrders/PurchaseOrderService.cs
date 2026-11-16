using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.Suppliers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.PurchaseOrders;

public sealed record PurchaseOrderFilterRequest : PagedRequest
{
    public Guid? SupplierId { get; init; }
    public Guid? WarehouseId { get; init; }
    public PurchaseOrderStatus? Status { get; init; }
}

public sealed record CreatePurchaseOrderLineRequest(
    Guid ProductId,
    int OrderedQuantity,
    decimal? UnitCost = null);

public sealed record CreatePurchaseOrderRequest(
    Guid SupplierId,
    Guid TargetWarehouseId,
    DateTimeOffset? ExpectedDeliveryDateUtc,
    decimal? TaxRatePercent,
    string? Notes,
    IReadOnlyList<CreatePurchaseOrderLineRequest> Lines);

public sealed record ReceivePurchaseOrderLineRequest(
    Guid PurchaseOrderLineId,
    int QuantityReceived);

public sealed record ReceivePurchaseOrderRequest(
    string? Notes,
    IReadOnlyList<ReceivePurchaseOrderLineRequest> Lines);

public sealed record CancelPurchaseOrderRequest(
    string Reason);

public sealed record PurchaseOrderLineDto(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int OrderedQuantity,
    int ReceivedQuantity,
    int RemainingQuantity,
    decimal UnitCost,
    decimal LineTotal);

public sealed record GoodsReceiptDto(
    Guid Id,
    string ReceiptNumber,
    string? Notes,
    DateTimeOffset ReceivedAtUtc,
    int TotalUnitsReceived);

public sealed record PurchaseOrderDto(
    Guid Id,
    string OrderNumber,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid TargetWarehouseId,
    string TargetWarehouseCode,
    string TargetWarehouseName,
    PurchaseOrderStatus Status,
    decimal SubtotalAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTimeOffset? ExpectedDeliveryDateUtc,
    string? Notes,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<PurchaseOrderLineDto> Lines,
    IReadOnlyList<GoodsReceiptDto> Receipts);

public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.TargetWarehouseId).NotEmpty();
        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty().WithMessage("At least one purchase order line is required.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.OrderedQuantity).GreaterThan(0);
            line.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0m).When(l => l.UnitCost.HasValue);
        });
    }
}

public interface IPurchaseOrderService
{
    Task<PagedResponse<PurchaseOrderDto>> GetPurchaseOrdersAsync(PurchaseOrderFilterRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> GetPurchaseOrderByIdAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> SubmitPurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> ApprovePurchaseOrderAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> ReceivePurchaseOrderAsync(Guid purchaseOrderId, ReceivePurchaseOrderRequest request, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDto> CancelPurchaseOrderAsync(Guid purchaseOrderId, CancelPurchaseOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly CreatePurchaseOrderRequestValidator _createValidator = new();

    public PurchaseOrderService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResponse<PurchaseOrderDto>> GetPurchaseOrdersAsync(
        PurchaseOrderFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from po in _dbContext.PurchaseOrders.AsNoTracking()
                    join s in _dbContext.Suppliers.AsNoTracking() on po.SupplierId equals s.Id
                    join w in _dbContext.Warehouses.AsNoTracking() on po.TargetWarehouseId equals w.Id
                    where po.TenantId == tenantId
                    select new
                    {
                        Order = po,
                        SupplierCode = s.Code,
                        SupplierName = s.Name,
                        WarehouseCode = w.Code,
                        WarehouseName = w.Name
                    };

        if (request.SupplierId.HasValue)
        {
            query = query.Where(x => x.Order.SupplierId == request.SupplierId.Value);
        }

        if (request.WarehouseId.HasValue)
        {
            query = query.Where(x => x.Order.TargetWarehouseId == request.WarehouseId.Value);
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
                x.SupplierName.ToLower().Contains(term) ||
                x.SupplierCode.ToLower().Contains(term));
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

        var poIds = headers.Select(h => h.Order.Id).ToList();

        var lines = await (
            from l in _dbContext.PurchaseOrderLines.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on l.ProductId equals p.Id
            where l.TenantId == tenantId && poIds.Contains(l.PurchaseOrderId)
            orderby p.Sku
            select new
            {
                l.PurchaseOrderId,
                Dto = new PurchaseOrderLineDto(
                    l.Id,
                    l.ProductId,
                    p.Sku,
                    p.Name,
                    p.UnitOfMeasure,
                    l.OrderedQuantity,
                    l.ReceivedQuantity,
                    l.OrderedQuantity - l.ReceivedQuantity,
                    l.UnitCost,
                    l.LineTotal)
            }).ToListAsync(cancellationToken);

        var receipts = await _dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId && poIds.Contains(r.PurchaseOrderId))
            .OrderByDescending(r => r.ReceivedAtUtc)
            .ToListAsync(cancellationToken);

        var linesLookup = lines.ToLookup(x => x.PurchaseOrderId, x => x.Dto);
        var receiptsLookup = receipts.ToLookup(
            r => r.PurchaseOrderId,
            r => new GoodsReceiptDto(
                r.Id,
                r.ReceiptNumber,
                r.Notes,
                r.ReceivedAtUtc,
                r.Lines.Sum(l => l.QuantityReceived)));

        var items = headers
            .Select(h => new PurchaseOrderDto(
                h.Order.Id,
                h.Order.OrderNumber,
                h.Order.SupplierId,
                h.SupplierCode,
                h.SupplierName,
                h.Order.TargetWarehouseId,
                h.WarehouseCode,
                h.WarehouseName,
                h.Order.Status,
                h.Order.SubtotalAmount,
                h.Order.TaxAmount,
                h.Order.TotalAmount,
                h.Order.ExpectedDeliveryDateUtc,
                h.Order.Notes,
                h.Order.CancellationReason,
                h.Order.CreatedAtUtc,
                h.Order.ApprovedAtUtc,
                h.Order.CompletedAtUtc,
                linesLookup[h.Order.Id].ToList(),
                receiptsLookup[h.Order.Id].ToList()))
            .ToList();

        return new PagedResponse<PurchaseOrderDto>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            pageNumber > 1,
            pageNumber < totalPages);
    }

    public async Task<PurchaseOrderDto> GetPurchaseOrderByIdAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var header = await (
            from po in _dbContext.PurchaseOrders.AsNoTracking()
            join s in _dbContext.Suppliers.AsNoTracking() on po.SupplierId equals s.Id
            join w in _dbContext.Warehouses.AsNoTracking() on po.TargetWarehouseId equals w.Id
            where po.TenantId == tenantId && po.Id == purchaseOrderId
            select new
            {
                Order = po,
                SupplierCode = s.Code,
                SupplierName = s.Name,
                WarehouseCode = w.Code,
                WarehouseName = w.Name
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        var lines = await (
            from l in _dbContext.PurchaseOrderLines.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on l.ProductId equals p.Id
            where l.TenantId == tenantId && l.PurchaseOrderId == purchaseOrderId
            orderby p.Sku
            select new PurchaseOrderLineDto(
                l.Id,
                l.ProductId,
                p.Sku,
                p.Name,
                p.UnitOfMeasure,
                l.OrderedQuantity,
                l.ReceivedQuantity,
                l.OrderedQuantity - l.ReceivedQuantity,
                l.UnitCost,
                l.LineTotal))
            .ToListAsync(cancellationToken);

        var receipts = await _dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId && r.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(r => r.ReceivedAtUtc)
            .Select(r => new GoodsReceiptDto(
                r.Id,
                r.ReceiptNumber,
                r.Notes,
                r.ReceivedAtUtc,
                r.Lines.Sum(l => l.QuantityReceived)))
            .ToListAsync(cancellationToken);

        return new PurchaseOrderDto(
            header.Order.Id,
            header.Order.OrderNumber,
            header.Order.SupplierId,
            header.SupplierCode,
            header.SupplierName,
            header.Order.TargetWarehouseId,
            header.WarehouseCode,
            header.WarehouseName,
            header.Order.Status,
            header.Order.SubtotalAmount,
            header.Order.TaxAmount,
            header.Order.TotalAmount,
            header.Order.ExpectedDeliveryDateUtc,
            header.Order.Notes,
            header.Order.CancellationReason,
            header.Order.CreatedAtUtc,
            header.Order.ApprovedAtUtc,
            header.Order.CompletedAtUtc,
            lines,
            receipts);
    }

    public async Task<PurchaseOrderDto> CreatePurchaseOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), request.SupplierId);

        if (!supplier.IsActive)
        {
            throw new DomainException("Supplier.Inactive", $"Supplier '{supplier.Code}' is inactive and cannot receive new purchase orders.");
        }

        var warehouse = await _dbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == request.TargetWarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), request.TargetWarehouseId);

        if (!warehouse.IsActive)
        {
            throw new DomainException("Warehouse.Inactive", $"Warehouse '{warehouse.Code}' is inactive.");
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
            throw new DomainException("PurchaseOrder.InvalidProducts", "One or more products in the purchase order do not exist.");
        }

        foreach (var product in products.Values)
        {
            product.EnsureCanBeOrdered();
        }

        var supplierProductCosts = await _dbContext.SupplierProducts
            .AsNoTracking()
            .Where(sp => sp.TenantId == tenantId && sp.SupplierId == supplier.Id && productIds.Contains(sp.ProductId))
            .ToDictionaryAsync(sp => sp.ProductId, sp => sp.AgreedUnitCost, cancellationToken);

        var resolvedLines = request.Lines.Select(l =>
        {
            var resolvedCost = l.UnitCost
                               ?? (supplierProductCosts.TryGetValue(l.ProductId, out var agreedCost)
                                   ? agreedCost
                                   : products[l.ProductId].StandardCost);

            return (l.ProductId, l.OrderedQuantity, resolvedCost);
        });

        var orderNumber = $"PO-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var taxRate = request.TaxRatePercent ?? organization.DefaultTaxRatePercent;

        var purchaseOrder = new PurchaseOrder(
            tenantId,
            orderNumber,
            supplier.Id,
            warehouse.Id,
            request.ExpectedDeliveryDateUtc,
            taxRate,
            request.Notes,
            _tenantContext.UserId,
            resolvedLines);

        _dbContext.PurchaseOrders.Add(purchaseOrder);

        _dbContext.RecordAudit(
            action: "PurchaseOrder.Created",
            entityType: nameof(PurchaseOrder),
            entityId: purchaseOrder.Id.ToString(),
            summary: $"Created purchase order '{purchaseOrder.OrderNumber}' for supplier '{supplier.Code}' totaling {purchaseOrder.TotalAmount:F2}.",
            afterState: new
            {
                purchaseOrder.Id,
                purchaseOrder.OrderNumber,
                purchaseOrder.SupplierId,
                purchaseOrder.TargetWarehouseId,
                purchaseOrder.TotalAmount
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPurchaseOrderByIdAsync(purchaseOrder.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> SubmitPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var po = await _dbContext.PurchaseOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        po.Submit();

        _dbContext.RecordAudit(
            action: "PurchaseOrder.Submitted",
            entityType: nameof(PurchaseOrder),
            entityId: po.Id.ToString(),
            summary: $"Submitted purchase order '{po.OrderNumber}' for approval.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPurchaseOrderByIdAsync(po.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> ApprovePurchaseOrderAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var po = await _dbContext.PurchaseOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        po.Approve(_tenantContext.UserId);

        _dbContext.RecordAudit(
            action: "PurchaseOrder.Approved",
            entityType: nameof(PurchaseOrder),
            entityId: po.Id.ToString(),
            summary: $"Approved purchase order '{po.OrderNumber}'.");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPurchaseOrderByIdAsync(po.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> ReceivePurchaseOrderAsync(
        Guid purchaseOrderId,
        ReceivePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new RequestValidationException(nameof(request.Lines), "At least one receipt line is required.");
        }

        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var po = await _dbContext.PurchaseOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        var receiptNumber = $"GRN-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var receipt = po.ReceiveGoods(
            receiptNumber,
            request.Notes,
            _tenantContext.UserId,
            request.Lines.Select(l => (l.PurchaseOrderLineId, l.QuantityReceived)));

        _dbContext.GoodsReceipts.Add(receipt);

        var receivedProductIds = receipt.Lines.Select(l => l.ProductId).Distinct().OrderBy(id => id).ToList();

        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            receivedProductIds.Select(pid => (po.TargetWarehouseId, pid)),
            cancellationToken);

        var products = await _dbContext.Products
            .Where(p => p.TenantId == tenantId && receivedProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Calculate total tenant on-hand before receipt per product for accurate moving average cost
        var existingOnHandByProduct = await _dbContext.InventoryItems
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && receivedProductIds.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, TotalOnHand = g.Sum(x => x.OnHandQuantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.TotalOnHand, cancellationToken);

        foreach (var receiptLine in receipt.Lines.OrderBy(l => l.ProductId))
        {
            var inventoryItem = lockedItems[(po.TargetWarehouseId, receiptLine.ProductId)];
            var product = products[receiptLine.ProductId];
            var totalTenantOnHandBefore = existingOnHandByProduct.GetValueOrDefault(receiptLine.ProductId, 0);

            product.UpdateMovingAverageCost(
                totalTenantOnHandBefore,
                receiptLine.QuantityReceived,
                receiptLine.UnitCost);

            existingOnHandByProduct[receiptLine.ProductId] = totalTenantOnHandBefore + receiptLine.QuantityReceived;

            var movement = inventoryItem.ReceiveStock(
                receiptLine.QuantityReceived,
                receiptLine.UnitCost,
                "PurchaseOrder",
                po.OrderNumber,
                po.Id,
                $"Goods receipt {receipt.ReceiptNumber} against {po.OrderNumber}",
                request.Notes,
                _tenantContext.UserId);

            _dbContext.StockMovements.Add(movement);
        }

        _dbContext.RecordAudit(
            action: "PurchaseOrder.GoodsReceived",
            entityType: nameof(PurchaseOrder),
            entityId: po.Id.ToString(),
            summary: $"Received {receipt.Lines.Sum(l => l.QuantityReceived)} unit(s) on '{po.OrderNumber}' via receipt '{receipt.ReceiptNumber}'. New status: {po.Status}.",
            afterState: new
            {
                po.Id,
                po.OrderNumber,
                po.Status,
                ReceiptId = receipt.Id,
                receipt.ReceiptNumber
            });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetPurchaseOrderByIdAsync(po.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> CancelPurchaseOrderAsync(
        Guid purchaseOrderId,
        CancelPurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RequestValidationException(nameof(request.Reason), "A cancellation reason is required.");
        }

        var tenantId = _tenantContext.RequiredTenantId;

        var po = await _dbContext.PurchaseOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == purchaseOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), purchaseOrderId);

        po.Cancel(request.Reason);

        _dbContext.RecordAudit(
            action: "PurchaseOrder.Cancelled",
            entityType: nameof(PurchaseOrder),
            entityId: po.Id.ToString(),
            summary: $"Cancelled purchase order '{po.OrderNumber}'. Reason: {request.Reason}");

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPurchaseOrderByIdAsync(po.Id, cancellationToken);
    }
}
