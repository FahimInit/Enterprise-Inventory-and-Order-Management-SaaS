using Inventory.Application.Common;
using Inventory.Application.Inventory;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.StockTransfers;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Application.Reporting;

public sealed record WarehouseValuationDto(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    int DistinctProductsCount,
    int TotalOnHandUnits,
    int TotalReservedUnits,
    int TotalAvailableUnits,
    int TotalInTransitUnits,
    decimal TotalValuation);

public sealed record CategoryValuationDto(
    Guid CategoryId,
    string CategoryName,
    int ProductCount,
    int TotalOnHandUnits,
    decimal TotalValuation);

public sealed record LowStockAlertItemDto(
    Guid InventoryItemId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string CategoryName,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int InTransitQuantity,
    int ReorderPoint,
    int TargetStockLevel,
    int SuggestedReorderQuantity,
    decimal MovingAverageCost);

public sealed record OrderStatusSummaryDto(
    string Status,
    int OrderCount,
    decimal TotalAmount);

public sealed record OrderStatisticsReportDto(
    int TotalPurchaseOrders,
    decimal TotalProcurementSpend,
    int TotalSalesOrders,
    decimal TotalConfirmedAndFulfilledRevenue,
    decimal TotalFulfilledRevenue,
    decimal EstimatedFulfilledCogs,
    decimal EstimatedGrossMargin,
    IReadOnlyList<OrderStatusSummaryDto> PurchaseOrdersByStatus,
    IReadOnlyList<OrderStatusSummaryDto> SalesOrdersByStatus);

public sealed record InventoryValuationReportDto(
    string BaseCurrency,
    int TotalOnHandUnits,
    int TotalReservedUnits,
    int TotalAvailableUnits,
    int TotalInTransitUnits,
    decimal TotalInventoryValuation,
    IReadOnlyList<WarehouseValuationDto> ByWarehouse,
    IReadOnlyList<CategoryValuationDto> ByCategory);

public sealed record DashboardSummaryDto(
    string OrganizationName,
    string BaseCurrency,
    bool IsDemoEnvironment,
    int ActiveProductCount,
    int ActiveCategoryCount,
    int ActiveWarehouseCount,
    int ActiveSupplierCount,
    int TotalOnHandUnits,
    int TotalReservedUnits,
    int TotalAvailableUnits,
    int TotalInTransitUnits,
    decimal TotalInventoryValuation,
    int LowStockAlertCount,
    int OpenPurchaseOrderCount,
    decimal OpenPurchaseOrderValue,
    int OpenSalesOrderCount,
    decimal FulfilledSalesRevenue,
    int PendingTransferCount,
    IReadOnlyList<WarehouseValuationDto> WarehouseSummaries,
    IReadOnlyList<LowStockAlertItemDto> TopLowStockItems,
    IReadOnlyList<StockMovementDto> RecentStockMovements);

public interface IReportingService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(bool isDemoEnvironment, CancellationToken cancellationToken = default);
    Task<InventoryValuationReportDto> GetInventoryValuationReportAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowStockAlertItemDto>> GetLowStockReportAsync(Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<OrderStatisticsReportDto> GetOrderStatisticsReportAsync(CancellationToken cancellationToken = default);
}

public sealed class ReportingService : IReportingService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ReportingService(IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(
        bool isDemoEnvironment,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstAsync(o => o.Id == tenantId, cancellationToken);

        var activeProductCount = await _dbContext.Products
            .CountAsync(p => p.TenantId == tenantId && p.Status == Domain.Catalog.ProductStatus.Active, cancellationToken);

        var activeCategoryCount = await _dbContext.Categories
            .CountAsync(c => c.TenantId == tenantId && c.IsActive, cancellationToken);

        var activeWarehouseCount = await _dbContext.Warehouses
            .CountAsync(w => w.TenantId == tenantId && w.IsActive, cancellationToken);

        var activeSupplierCount = await _dbContext.Suppliers
            .CountAsync(s => s.TenantId == tenantId && s.IsActive, cancellationToken);

        var valuationReport = await GetInventoryValuationReportAsync(cancellationToken);
        var lowStockItems = await GetLowStockReportAsync(null, cancellationToken);

        var openPoStats = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.TenantId == tenantId &&
                         po.Status != PurchaseOrderStatus.Received &&
                         po.Status != PurchaseOrderStatus.Cancelled)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                TotalValue = g.Sum(x => x.TotalAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var openSoCount = await _dbContext.SalesOrders
            .CountAsync(
                so => so.TenantId == tenantId &&
                      so.Status != SalesOrderStatus.Fulfilled &&
                      so.Status != SalesOrderStatus.Cancelled,
                cancellationToken);

        var fulfilledRevenue = await _dbContext.SalesOrders
            .AsNoTracking()
            .Where(so => so.TenantId == tenantId && so.Status == SalesOrderStatus.Fulfilled)
            .SumAsync(so => (decimal?)so.TotalAmount, cancellationToken) ?? 0m;

        var pendingTransferCount = await _dbContext.StockTransfers
            .CountAsync(
                t => t.TenantId == tenantId &&
                     (t.Status == TransferStatus.Requested || t.Status == TransferStatus.InTransit),
                cancellationToken);

        var recentMovements = await (
            from m in _dbContext.StockMovements.AsNoTracking()
            join w in _dbContext.Warehouses.AsNoTracking() on m.WarehouseId equals w.Id
            join p in _dbContext.Products.AsNoTracking() on m.ProductId equals p.Id
            where m.TenantId == tenantId
            orderby m.OccurredAtUtc descending
            select new StockMovementDto(
                m.Id,
                m.InventoryItemId,
                m.WarehouseId,
                w.Code,
                w.Name,
                m.ProductId,
                p.Sku,
                p.Name,
                m.MovementType,
                m.QuantityDelta,
                m.BalanceBefore,
                m.BalanceAfter,
                m.UnitCost,
                m.ReferenceType,
                m.ReferenceNumber,
                m.ReferenceId,
                m.Reason,
                m.Notes,
                m.CreatedByUserId,
                m.OccurredAtUtc))
            .Take(10)
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            organization.Name,
            organization.BaseCurrency,
            isDemoEnvironment,
            activeProductCount,
            activeCategoryCount,
            activeWarehouseCount,
            activeSupplierCount,
            valuationReport.TotalOnHandUnits,
            valuationReport.TotalReservedUnits,
            valuationReport.TotalAvailableUnits,
            valuationReport.TotalInTransitUnits,
            valuationReport.TotalInventoryValuation,
            lowStockItems.Count,
            openPoStats?.Count ?? 0,
            openPoStats?.TotalValue ?? 0m,
            openSoCount,
            fulfilledRevenue,
            pendingTransferCount,
            valuationReport.ByWarehouse,
            lowStockItems.Take(10).ToList(),
            recentMovements);
    }

    public async Task<InventoryValuationReportDto> GetInventoryValuationReportAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var organization = await _dbContext.Organizations
            .AsNoTracking()
            .FirstAsync(o => o.Id == tenantId, cancellationToken);

        var rows = await (
            from i in _dbContext.InventoryItems.AsNoTracking()
            join w in _dbContext.Warehouses.AsNoTracking() on i.WarehouseId equals w.Id
            join p in _dbContext.Products.AsNoTracking() on i.ProductId equals p.Id
            join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
            where i.TenantId == tenantId
            select new
            {
                i.WarehouseId,
                WarehouseCode = w.Code,
                WarehouseName = w.Name,
                p.CategoryId,
                CategoryName = c.Name,
                i.ProductId,
                i.OnHandQuantity,
                i.ReservedQuantity,
                i.InTransitQuantity,
                p.MovingAverageCost
            }).ToListAsync(cancellationToken);

        var byWarehouse = rows
            .GroupBy(r => new { r.WarehouseId, r.WarehouseCode, r.WarehouseName })
            .Select(g => new WarehouseValuationDto(
                g.Key.WarehouseId,
                g.Key.WarehouseCode,
                g.Key.WarehouseName,
                g.Select(x => x.ProductId).Distinct().Count(),
                g.Sum(x => x.OnHandQuantity),
                g.Sum(x => x.ReservedQuantity),
                g.Sum(x => x.OnHandQuantity - x.ReservedQuantity),
                g.Sum(x => x.InTransitQuantity),
                decimal.Round(g.Sum(x => x.OnHandQuantity * x.MovingAverageCost), 4)))
            .OrderByDescending(w => w.TotalValuation)
            .ToList();

        var byCategory = rows
            .GroupBy(r => new { r.CategoryId, r.CategoryName })
            .Select(g => new CategoryValuationDto(
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Select(x => x.ProductId).Distinct().Count(),
                g.Sum(x => x.OnHandQuantity),
                decimal.Round(g.Sum(x => x.OnHandQuantity * x.MovingAverageCost), 4)))
            .OrderByDescending(c => c.TotalValuation)
            .ToList();

        var totalOnHand = rows.Sum(r => r.OnHandQuantity);
        var totalReserved = rows.Sum(r => r.ReservedQuantity);
        var totalAvailable = totalOnHand - totalReserved;
        var totalInTransit = rows.Sum(r => r.InTransitQuantity);
        var totalValuation = decimal.Round(rows.Sum(r => r.OnHandQuantity * r.MovingAverageCost), 4);

        return new InventoryValuationReportDto(
            organization.BaseCurrency,
            totalOnHand,
            totalReserved,
            totalAvailable,
            totalInTransit,
            totalValuation,
            byWarehouse,
            byCategory);
    }

    public async Task<IReadOnlyList<LowStockAlertItemDto>> GetLowStockReportAsync(
        Guid? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var query = from i in _dbContext.InventoryItems.AsNoTracking()
                    join w in _dbContext.Warehouses.AsNoTracking() on i.WarehouseId equals w.Id
                    join p in _dbContext.Products.AsNoTracking() on i.ProductId equals p.Id
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    where i.TenantId == tenantId &&
                          (i.OnHandQuantity - i.ReservedQuantity) <= p.ReorderPoint
                    select new
                    {
                        Item = i,
                        WarehouseCode = w.Code,
                        WarehouseName = w.Name,
                        Product = p,
                        CategoryName = c.Name
                    };

        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Item.WarehouseId == warehouseId.Value);
        }

        var list = await query
            .OrderBy(x => x.Item.OnHandQuantity - x.Item.ReservedQuantity)
            .ThenBy(x => x.Product.Sku)
            .ToListAsync(cancellationToken);

        return list
            .Select(x =>
            {
                var available = x.Item.OnHandQuantity - x.Item.ReservedQuantity;
                var suggested = Math.Max(1, x.Product.TargetStockLevel - available);
                return new LowStockAlertItemDto(
                    x.Item.Id,
                    x.Item.WarehouseId,
                    x.WarehouseCode,
                    x.WarehouseName,
                    x.Item.ProductId,
                    x.Product.Sku,
                    x.Product.Name,
                    x.CategoryName,
                    x.Item.OnHandQuantity,
                    x.Item.ReservedQuantity,
                    available,
                    x.Item.InTransitQuantity,
                    x.Product.ReorderPoint,
                    x.Product.TargetStockLevel,
                    suggested,
                    x.Product.MovingAverageCost);
            })
            .ToList();
    }

    public async Task<OrderStatisticsReportDto> GetOrderStatisticsReportAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequiredTenantId;

        var poSummary = await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.TenantId == tenantId)
            .GroupBy(po => po.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Total = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var soSummary = await _dbContext.SalesOrders
            .AsNoTracking()
            .Where(so => so.TenantId == tenantId)
            .GroupBy(so => so.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Total = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync(cancellationToken);

        var fulfilledCogs = await (
            from so in _dbContext.SalesOrders.AsNoTracking()
            join l in _dbContext.SalesOrderLines.AsNoTracking() on so.Id equals l.SalesOrderId
            where so.TenantId == tenantId && so.Status == SalesOrderStatus.Fulfilled
            select (decimal?)(l.Quantity * l.UnitCostSnapshot))
            .SumAsync(cancellationToken) ?? 0m;

        var totalPoCount = poSummary.Sum(x => x.Count);
        var totalPoSpend = poSummary
            .Where(x => x.Status != PurchaseOrderStatus.Cancelled)
            .Sum(x => x.Total);

        var totalSoCount = soSummary.Sum(x => x.Count);
        var totalConfirmedAndFulfilledRevenue = soSummary
            .Where(x => x.Status is SalesOrderStatus.Confirmed or SalesOrderStatus.Processing or SalesOrderStatus.Fulfilled)
            .Sum(x => x.Total);

        var fulfilledRevenue = soSummary
            .Where(x => x.Status == SalesOrderStatus.Fulfilled)
            .Sum(x => x.Total);

        var grossMargin = decimal.Round(fulfilledRevenue - fulfilledCogs, 4);

        return new OrderStatisticsReportDto(
            totalPoCount,
            decimal.Round(totalPoSpend, 4),
            totalSoCount,
            decimal.Round(totalConfirmedAndFulfilledRevenue, 4),
            decimal.Round(fulfilledRevenue, 4),
            decimal.Round(fulfilledCogs, 4),
            grossMargin,
            poSummary.Select(x => new OrderStatusSummaryDto(x.Status.ToString(), x.Count, decimal.Round(x.Total, 4))).ToList(),
            soSummary.Select(x => new OrderStatusSummaryDto(x.Status.ToString(), x.Count, decimal.Round(x.Total, 4))).ToList());
    }
}
