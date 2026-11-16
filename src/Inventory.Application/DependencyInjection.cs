using Inventory.Application.Auditing;
using Inventory.Application.Catalog;
using Inventory.Application.Identity;
using Inventory.Application.Inventory;
using Inventory.Application.Organizations;
using Inventory.Application.PurchaseOrders;
using Inventory.Application.Reporting;
using Inventory.Application.SalesOrders;
using Inventory.Application.StockTransfers;
using Inventory.Application.Suppliers;
using Inventory.Application.Warehouses;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
