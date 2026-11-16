using Inventory.Application.Common;
using Inventory.Domain.Identity;
using Inventory.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.BackgroundJobs;

/// <summary>
/// Tenant-aware background worker that periodically verifies inventory ledger-to-balance consistency,
/// monitors low-stock thresholds per tenant, and purges expired idempotency records.
/// </summary>
public sealed class InventoryMaintenanceBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InventoryMaintenanceBackgroundService> _logger;

    public InventoryMaintenanceBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<InventoryMaintenanceBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("BackgroundJobs:EnableLowStockMonitor", true);
        if (!enabled)
        {
            _logger.LogInformation("InventoryMaintenanceBackgroundService is disabled by configuration.");
            return;
        }

        var intervalMinutes = Math.Max(1, _configuration.GetValue<int>("BackgroundJobs:LowStockCheckIntervalMinutes", 15));

        // Initial delay so application startup and migrations finish first
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteTenantMaintenanceCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled tenant inventory maintenance cycle.");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task ExecuteTenantMaintenanceCycleAsync(CancellationToken cancellationToken)
    {
        using var rootScope = _scopeFactory.CreateScope();
        var rootDb = rootScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var activeTenantIds = await rootDb.Organizations
            .AsNoTracking()
            .Where(o => o.IsActive)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in activeTenantIds)
        {
            using var tenantScope = _scopeFactory.CreateScope();
            var tenantContext = tenantScope.ServiceProvider.GetRequiredService<TenantContext>();
            var dbContext = tenantScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            // Bind the scoped TenantContext to this specific organization
            tenantContext.SetTenantContext(
                tenantId,
                userId: null,
                userEmail: "system-background-worker@internal",
                permissions: Permissions.All,
                isPlatformAdmin: false);

            var lowStockCount = await (
                from i in dbContext.InventoryItems.AsNoTracking()
                join p in dbContext.Products.AsNoTracking() on i.ProductId equals p.Id
                where i.TenantId == tenantId && (i.OnHandQuantity - i.ReservedQuantity) <= p.ReorderPoint
                select i.Id)
                .CountAsync(cancellationToken);

            // Verify ledger conservation invariant for this tenant
            var items = await dbContext.InventoryItems
                .AsNoTracking()
                .Where(i => i.TenantId == tenantId)
                .Select(i => new { i.Id, i.OnHandQuantity })
                .ToListAsync(cancellationToken);

            var ledgerSums = await dbContext.StockMovements
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId)
                .GroupBy(m => m.InventoryItemId)
                .Select(g => new { InventoryItemId = g.Key, TotalDelta = g.Sum(x => x.QuantityDelta) })
                .ToDictionaryAsync(x => x.InventoryItemId, x => x.TotalDelta, cancellationToken);

            var driftCount = items.Count(item =>
                item.OnHandQuantity != ledgerSums.GetValueOrDefault(item.Id, 0));

            if (driftCount > 0)
            {
                _logger.LogWarning(
                    "Tenant {TenantId}: Detected {DriftCount} inventory item(s) with ledger-to-balance discrepancy.",
                    tenantId,
                    driftCount);
            }
            else if (lowStockCount > 0)
            {
                _logger.LogInformation(
                    "Tenant {TenantId}: Background check complete. Ledger consistent; {LowStockCount} item(s) at or below reorder point.",
                    tenantId,
                    lowStockCount);
            }

            // Clean up expired idempotency records for this tenant
            var now = DateTimeOffset.UtcNow;
            var expiredRecords = await dbContext.IdempotencyRecords
                .Where(r => r.TenantId == tenantId && r.ExpiresAtUtc < now)
                .ToListAsync(cancellationToken);

            if (expiredRecords.Count > 0)
            {
                dbContext.IdempotencyRecords.RemoveRange(expiredRecords);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
