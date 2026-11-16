using FluentValidation;
using Inventory.Application.Common;
using Inventory.Domain.Catalog;
using Inventory.Domain.Common;
using Inventory.Domain.StockTransfers;
using Inventory.Domain.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.StockTransfers;

public sealed record StockTransferFilterRequest : PagedRequest
{
    public Guid? SourceWarehouseId { get; init; }
    public Guid? DestinationWarehouseId { get; init; }
    public TransferStatus? Status { get; init; }
}

public sealed record CreateStockTransferLineRequest(
    Guid ProductId,
    int Quantity,
    string? Notes = null);

public sealed record CreateStockTransferRequest(
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    string? Notes,
    IReadOnlyList<CreateStockTransferLineRequest> Lines);

public sealed record CancelStockTransferRequest(
    string Reason);

public sealed record StockTransferLineDto(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasure,
    int Quantity,
    string? Notes);

public sealed record StockTransferDto(
    Guid Id,
    string TransferNumber,
    Guid SourceWarehouseId,
    string SourceWarehouseCode,
    string SourceWarehouseName,
    Guid DestinationWarehouseId,
    string DestinationWarehouseCode,
    string DestinationWarehouseName,
    TransferStatus Status,
    int TotalQuantity,
    string? Notes,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DispatchedAtUtc,
    DateTimeOffset? ReceivedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    IReadOnlyList<StockTransferLineDto> Lines);

public sealed class CreateStockTransferRequestValidator : AbstractValidator<CreateStockTransferRequest>
{
    public CreateStockTransferRequestValidator()
    {
        RuleFor(x => x.SourceWarehouseId).NotEmpty();
        RuleFor(x => x.DestinationWarehouseId)
            .NotEmpty()
            .NotEqual(x => x.SourceWarehouseId)
            .WithMessage("Destination warehouse must be different from source warehouse.");

        RuleFor(x => x.Lines)
            .NotNull()
            .NotEmpty().WithMessage("At least one transfer line is required.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty();
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Transfer line quantity must be greater than zero.");
        });
    }
}

public interface IStockTransferService
{
    Task<PagedResponse<StockTransferDto>> GetTransfersAsync(StockTransferFilterRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDto> GetTransferByIdAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<StockTransferDto> CreateTransferAsync(CreateStockTransferRequest request, CancellationToken cancellationToken = default);
    Task<StockTransferDto> DispatchTransferAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<StockTransferDto> ReceiveTransferAsync(Guid transferId, CancellationToken cancellationToken = default);
    Task<StockTransferDto> CancelTransferAsync(Guid transferId, CancelStockTransferRequest request, CancellationToken cancellationToken = default);
}

public sealed class StockTransferService : IStockTransferService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly CreateStockTransferRequestValidator _createValidator = new();

    public StockTransferService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<PagedResponse<StockTransferDto>> GetTransfersAsync(
        StockTransferFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from t in _dbContext.StockTransfers.AsNoTracking()
                    join sw in _dbContext.Warehouses.AsNoTracking() on t.SourceWarehouseId equals sw.Id
                    join dw in _dbContext.Warehouses.AsNoTracking() on t.DestinationWarehouseId equals dw.Id
                    where t.TenantId == tenantId
                    select new
                    {
                        Transfer = t,
                        SourceCode = sw.Code,
                        SourceName = sw.Name,
                        DestCode = dw.Code,
                        DestName = dw.Name
                    };

        if (request.SourceWarehouseId.HasValue)
        {
            query = query.Where(x => x.Transfer.SourceWarehouseId == request.SourceWarehouseId.Value);
        }

        if (request.DestinationWarehouseId.HasValue)
        {
            query = query.Where(x => x.Transfer.DestinationWarehouseId == request.DestinationWarehouseId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Transfer.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x =>
                x.Transfer.TransferNumber.ToLower().Contains(term) ||
                x.SourceCode.ToLower().Contains(term) ||
                x.DestCode.ToLower().Contains(term));
        }

        query = query.OrderByDescending(x => x.Transfer.CreatedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, PagedRequest.MaxPageSize);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var pageHeaders = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var transferIds = pageHeaders.Select(x => x.Transfer.Id).ToList();

        var linesByTransfer = await (
            from l in _dbContext.StockTransferLines.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on l.ProductId equals p.Id
            where l.TenantId == tenantId && transferIds.Contains(l.StockTransferId)
            select new
            {
                l.StockTransferId,
                Dto = new StockTransferLineDto(
                    l.Id,
                    l.ProductId,
                    p.Sku,
                    p.Name,
                    p.UnitOfMeasure,
                    l.Quantity,
                    l.Notes)
            }).ToListAsync(cancellationToken);

        var lookup = linesByTransfer.ToLookup(x => x.StockTransferId, x => x.Dto);

        var items = pageHeaders
            .Select(h =>
            {
                var lines = lookup[h.Transfer.Id].ToList();
                return new StockTransferDto(
                    h.Transfer.Id,
                    h.Transfer.TransferNumber,
                    h.Transfer.SourceWarehouseId,
                    h.SourceCode,
                    h.SourceName,
                    h.Transfer.DestinationWarehouseId,
                    h.DestCode,
                    h.DestName,
                    h.Transfer.Status,
                    lines.Sum(l => l.Quantity),
                    h.Transfer.Notes,
                    h.Transfer.CancellationReason,
                    h.Transfer.CreatedAtUtc,
                    h.Transfer.DispatchedAtUtc,
                    h.Transfer.ReceivedAtUtc,
                    h.Transfer.CancelledAtUtc,
                    lines);
            })
            .ToList();

        return new PagedResponse<StockTransferDto>(
            items,
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            pageNumber > 1,
            pageNumber < totalPages);
    }

    public async Task<StockTransferDto> GetTransferByIdAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var header = await (
            from t in _dbContext.StockTransfers.AsNoTracking()
            join sw in _dbContext.Warehouses.AsNoTracking() on t.SourceWarehouseId equals sw.Id
            join dw in _dbContext.Warehouses.AsNoTracking() on t.DestinationWarehouseId equals dw.Id
            where t.TenantId == tenantId && t.Id == transferId
            select new
            {
                Transfer = t,
                SourceCode = sw.Code,
                SourceName = sw.Name,
                DestCode = dw.Code,
                DestName = dw.Name
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(StockTransfer), transferId);

        var lines = await (
            from l in _dbContext.StockTransferLines.AsNoTracking()
            join p in _dbContext.Products.AsNoTracking() on l.ProductId equals p.Id
            where l.TenantId == tenantId && l.StockTransferId == transferId
            orderby p.Sku
            select new StockTransferLineDto(
                l.Id,
                l.ProductId,
                p.Sku,
                p.Name,
                p.UnitOfMeasure,
                l.Quantity,
                l.Notes))
            .ToListAsync(cancellationToken);

        return new StockTransferDto(
            header.Transfer.Id,
            header.Transfer.TransferNumber,
            header.Transfer.SourceWarehouseId,
            header.SourceCode,
            header.SourceName,
            header.Transfer.DestinationWarehouseId,
            header.DestCode,
            header.DestName,
            header.Transfer.Status,
            lines.Sum(l => l.Quantity),
            header.Transfer.Notes,
            header.Transfer.CancellationReason,
            header.Transfer.CreatedAtUtc,
            header.Transfer.DispatchedAtUtc,
            header.Transfer.ReceivedAtUtc,
            header.Transfer.CancelledAtUtc,
            lines);
    }

    public async Task<StockTransferDto> CreateTransferAsync(
        CreateStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowProblemAsync(request, cancellationToken);

        var tenantId = _tenantContext.RequiredTenantId;

        var warehouses = await _dbContext.Warehouses
            .AsNoTracking()
            .Where(w => w.TenantId == tenantId &&
                        (w.Id == request.SourceWarehouseId || w.Id == request.DestinationWarehouseId) &&
                        w.IsActive)
            .ToListAsync(cancellationToken);

        if (warehouses.Count != 2)
        {
            throw new DomainException("Transfer.InvalidWarehouses", "Both source and destination warehouses must exist and be active in your organization.");
        }

        var distinctProductIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && distinctProductIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (products.Count != distinctProductIds.Count)
        {
            throw new DomainException("Transfer.InvalidProducts", "One or more specified products were not found in your organization.");
        }

        foreach (var product in products)
        {
            product.EnsureCanBeOrdered();
        }

        var transferNumber = $"TRF-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var transfer = new StockTransfer(
            tenantId,
            transferNumber,
            request.SourceWarehouseId,
            request.DestinationWarehouseId,
            request.Notes,
            _tenantContext.UserId,
            request.Lines.Select(l => (l.ProductId, l.Quantity, l.Notes)));

        _dbContext.StockTransfers.Add(transfer);

        _dbContext.RecordAudit(
            action: "StockTransfer.Created",
            entityType: nameof(StockTransfer),
            entityId: transfer.Id.ToString(),
            summary: $"Created stock transfer '{transfer.TransferNumber}' with {transfer.Lines.Count} line(s).",
            afterState: new
            {
                transfer.Id,
                transfer.TransferNumber,
                transfer.SourceWarehouseId,
                transfer.DestinationWarehouseId,
                transfer.Status
            });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetTransferByIdAsync(transfer.Id, cancellationToken);
    }

    public async Task<StockTransferDto> DispatchTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var transfer = await _dbContext.StockTransfers
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transferId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockTransfer), transferId);

        transfer.MarkDispatched(_tenantContext.UserId);

        var productIds = transfer.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productCosts = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.MovingAverageCost, cancellationToken);

        // Lock both Source and Destination warehouse inventory rows in deterministic order
        var lockTargets = transfer.Lines
            .SelectMany(l => new[]
            {
                (transfer.SourceWarehouseId, l.ProductId),
                (transfer.DestinationWarehouseId, l.ProductId)
            });

        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            lockTargets,
            cancellationToken);

        foreach (var line in transfer.Lines.OrderBy(l => l.ProductId))
        {
            var sourceItem = lockedItems[(transfer.SourceWarehouseId, line.ProductId)];
            var destItem = lockedItems[(transfer.DestinationWarehouseId, line.ProductId)];
            var unitCost = productCosts.GetValueOrDefault(line.ProductId, 0m);

            var outMovement = sourceItem.DispatchTransferOut(
                line.Quantity,
                unitCost,
                transfer.Id,
                transfer.TransferNumber,
                _tenantContext.UserId);

            destItem.AddInboundInTransit(line.Quantity);

            _dbContext.StockMovements.Add(outMovement);
        }

        _dbContext.RecordAudit(
            action: "StockTransfer.Dispatched",
            entityType: nameof(StockTransfer),
            entityId: transfer.Id.ToString(),
            summary: $"Dispatched stock transfer '{transfer.TransferNumber}' from warehouse '{transfer.SourceWarehouseId}' to '{transfer.DestinationWarehouseId}'.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetTransferByIdAsync(transfer.Id, cancellationToken);
    }

    public async Task<StockTransferDto> ReceiveTransferAsync(
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var transfer = await _dbContext.StockTransfers
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transferId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockTransfer), transferId);

        transfer.MarkReceived(_tenantContext.UserId);

        var productIds = transfer.Lines.Select(l => l.ProductId).Distinct().ToList();
        var productCosts = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.MovingAverageCost, cancellationToken);

        var lockTargets = transfer.Lines.Select(l => (transfer.DestinationWarehouseId, l.ProductId));
        var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
            tenantId,
            lockTargets,
            cancellationToken);

        foreach (var line in transfer.Lines.OrderBy(l => l.ProductId))
        {
            var destItem = lockedItems[(transfer.DestinationWarehouseId, line.ProductId)];
            var unitCost = productCosts.GetValueOrDefault(line.ProductId, 0m);

            var inMovement = destItem.ReceiveTransferIn(
                line.Quantity,
                unitCost,
                transfer.Id,
                transfer.TransferNumber,
                _tenantContext.UserId);

            _dbContext.StockMovements.Add(inMovement);
        }

        _dbContext.RecordAudit(
            action: "StockTransfer.Received",
            entityType: nameof(StockTransfer),
            entityId: transfer.Id.ToString(),
            summary: $"Received stock transfer '{transfer.TransferNumber}' at destination warehouse '{transfer.DestinationWarehouseId}'.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetTransferByIdAsync(transfer.Id, cancellationToken);
    }

    public async Task<StockTransferDto> CancelTransferAsync(
        Guid transferId,
        CancelStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new RequestValidationException(nameof(request.Reason), "A cancellation reason is required.");
        }

        var tenantId = _tenantContext.RequiredTenantId;

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);

        var transfer = await _dbContext.StockTransfers
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == transferId, cancellationToken)
            ?? throw new NotFoundException(nameof(StockTransfer), transferId);

        var wasInTransit = transfer.Cancel(request.Reason);

        if (wasInTransit)
        {
            var productIds = transfer.Lines.Select(l => l.ProductId).Distinct().ToList();
            var productCosts = await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.MovingAverageCost, cancellationToken);

            var lockTargets = transfer.Lines
                .SelectMany(l => new[]
                {
                    (transfer.SourceWarehouseId, l.ProductId),
                    (transfer.DestinationWarehouseId, l.ProductId)
                });

            var lockedItems = await _dbContext.GetOrCreateAndLockInventoryItemsAsync(
                tenantId,
                lockTargets,
                cancellationToken);

            foreach (var line in transfer.Lines.OrderBy(l => l.ProductId))
            {
                var sourceItem = lockedItems[(transfer.SourceWarehouseId, line.ProductId)];
                var destItem = lockedItems[(transfer.DestinationWarehouseId, line.ProductId)];
                var unitCost = productCosts.GetValueOrDefault(line.ProductId, 0m);

                destItem.CancelInboundInTransit(line.Quantity);

                var reversalMovement = sourceItem.RestoreCancelledTransferOut(
                    line.Quantity,
                    unitCost,
                    transfer.Id,
                    transfer.TransferNumber,
                    request.Reason,
                    _tenantContext.UserId);

                _dbContext.StockMovements.Add(reversalMovement);
            }
        }

        _dbContext.RecordAudit(
            action: "StockTransfer.Cancelled",
            entityType: nameof(StockTransfer),
            entityId: transfer.Id.ToString(),
            summary: $"Cancelled stock transfer '{transfer.TransferNumber}'. WasInTransit={wasInTransit}. Reason: {request.Reason}");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetTransferByIdAsync(transfer.Id, cancellationToken);
    }
}
