using Inventory.Application.Identity;
using Inventory.Domain.Catalog;
using Inventory.Domain.Identity;
using Inventory.Domain.Inventory;
using Inventory.Domain.Organizations;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.Suppliers;
using Inventory.Domain.Warehouses;
using Inventory.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        var enableDemoSeed = configuration.GetValue<bool>("Seed:EnableDemoData", false);
        if (!enableDemoSeed)
        {
            return;
        }

        var anyOrganizationExists = await dbContext.Organizations.AnyAsync(cancellationToken);
        if (anyOrganizationExists)
        {
            return;
        }

        logger.LogInformation("Seeding initial DEMO workspace data (Seed:EnableDemoData = true)...");

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);

        // 1. Create Demo Organization clearly identified as DEMO
        var demoOrg = new Organization(
            name: "Acme Industrial Supply [DEMO]",
            slug: "acme-industrial-demo",
            description: "Demonstration workspace pre-populated with sample industrial catalog, warehouses, inventory ledger, and orders.",
            baseCurrency: "USD",
            timeZoneId: "UTC",
            defaultLowStockThreshold: 15,
            defaultTaxRatePercent: 8.25m);

        dbContext.Organizations.Add(demoOrg);

        tenantContext.SetTenantContext(
            demoOrg.Id,
            userId: null,
            userEmail: "system-seed@acme-demo.local",
            permissions: Permissions.All,
            isPlatformAdmin: true);

        // 2. Seed Default Roles
        var roleByName = new Dictionary<string, OrganizationRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var (roleName, (description, perms)) in Permissions.GetDefaultRoleDefinitions())
        {
            var role = new OrganizationRole(demoOrg.Id, roleName, description, isSystemRole: true, perms);
            dbContext.OrganizationRoles.Add(role);
            roleByName[roleName] = role;
        }

        // 3. Seed Demo Users
        var ownerUser = new ApplicationUser("admin@acme-demo.local", "Elena Vance (Demo Owner)", "PENDING", isPlatformAdmin: true);
        ownerUser.UpdatePasswordHash(passwordHasher.HashPassword(ownerUser, "AdminPass123!"));

        var warehouseUser = new ApplicationUser("warehouse@acme-demo.local", "Marcus Chen (Warehouse Manager)", "PENDING");
        warehouseUser.UpdatePasswordHash(passwordHasher.HashPassword(warehouseUser, "WarehousePass123!"));

        var salesUser = new ApplicationUser("sales@acme-demo.local", "Sarah Jenkins (Sales Rep)", "PENDING");
        salesUser.UpdatePasswordHash(passwordHasher.HashPassword(salesUser, "SalesPass123!"));

        dbContext.Users.AddRange(ownerUser, warehouseUser, salesUser);

        dbContext.OrganizationMembers.AddRange(
            new OrganizationMember(demoOrg.Id, ownerUser.Id, roleByName[Permissions.SystemRoleNames.Owner].Id, isOwner: true, invitedByUserId: ownerUser.Id),
            new OrganizationMember(demoOrg.Id, warehouseUser.Id, roleByName[Permissions.SystemRoleNames.WarehouseManager].Id, isOwner: false, invitedByUserId: ownerUser.Id),
            new OrganizationMember(demoOrg.Id, salesUser.Id, roleByName[Permissions.SystemRoleNames.SalesRepresentative].Id, isOwner: false, invitedByUserId: ownerUser.Id));

        // 4. Seed Categories & Products
        var catAutomation = new Category(demoOrg.Id, "Industrial Automation", "industrial-automation", "PLCs, sensors, and control modules");
        var catHydraulics = new Category(demoOrg.Id, "Hydraulic Systems", "hydraulic-systems", "High-pressure valves, pumps, and actuators");
        var catSafety = new Category(demoOrg.Id, "Facility Safety", "facility-safety", "Light curtains, emergency stops, and PPE");
        dbContext.Categories.AddRange(catAutomation, catHydraulics, catSafety);

        var prodPlc = new Product(demoOrg.Id, catAutomation.Id, "IND-PLC-400", "Modular PLC Controller X400", "24V DC 32-Channel Programmable Logic Controller", "8901004001", "EA", 420.00m, 680.00m, reorderPoint: 15, targetStockLevel: 60);
        var prodSensor = new Product(demoOrg.Id, catAutomation.Id, "IND-SNS-108", "Photoelectric Proximity Sensor", "IP67 laser distance sensor", "8901001082", "EA", 45.00m, 85.00m, reorderPoint: 30, targetStockLevel: 120);
        var prodValve = new Product(demoOrg.Id, catHydraulics.Id, "HYD-VLV-220", "Proportional Directional Valve", "350 bar solenoid proportional valve", "8902002205", "EA", 210.00m, 340.00m, reorderPoint: 20, targetStockLevel: 80);
        var prodLightCurtain = new Product(demoOrg.Id, catSafety.Id, "SAF-LGT-900", "Optical Safety Light Curtain", "Type 4 safety light curtain pair, 900mm", "8903009009", "SET", 310.00m, 520.00m, reorderPoint: 10, targetStockLevel: 40);
        dbContext.Products.AddRange(prodPlc, prodSensor, prodValve, prodLightCurtain);

        // 5. Seed Warehouses & Bin Locations
        var whEast = new Warehouse(demoOrg.Id, "WH-EAST", "East Coast Distribution Center", "1200 Harbor Logistics Blvd", "Newark", "NJ", "07114", "USA", warehouseUser.Id, isDefault: true);
        var whWest = new Warehouse(demoOrg.Id, "WH-WEST", "Pacific Regional Hub", "4500 Aviation Way", "Ontario", "CA", "91761", "USA", warehouseUser.Id, isDefault: false);
        dbContext.Warehouses.AddRange(whEast, whWest);

        dbContext.WarehouseLocations.AddRange(
            new WarehouseLocation(demoOrg.Id, whEast.Id, "A01-R01-B01", "East Aisle 1 Rack 1 Bin 1", "AUTOMATION"),
            new WarehouseLocation(demoOrg.Id, whEast.Id, "A02-R01-B01", "East Aisle 2 Rack 1 Bin 1", "HYDRAULICS"),
            new WarehouseLocation(demoOrg.Id, whWest.Id, "W01-R01-B01", "West Main Receiving Bin", "GENERAL"));

        // 6. Seed Suppliers
        var supPrecision = new Supplier(demoOrg.Id, "SUP-NEXUS", "Nexus Motion & Controls GmbH", "Hans Richter", "orders@nexus-controls.example", "+49-89-555-0190", "Industriestrasse 44", "Munich", "Germany", "Net 30", leadTimeDays: 10);
        var supFluid = new Supplier(demoOrg.Id, "SUP-APEX", "Apex Fluid Power Inc.", "ramon@apexfluid.example", "sales@apexfluid.example", "+1-312-555-0144", "880 West Loop", "Chicago", "USA", "Net 45", leadTimeDays: 5);
        dbContext.Suppliers.AddRange(supPrecision, supFluid);

        dbContext.SupplierProducts.AddRange(
            new SupplierProduct(demoOrg.Id, supPrecision.Id, prodPlc.Id, "NX-PLC400", 420.00m, minimumOrderQuantity: 5, leadTimeDays: 10, isPreferred: true),
            new SupplierProduct(demoOrg.Id, supPrecision.Id, prodSensor.Id, "NX-SNS108", 45.00m, minimumOrderQuantity: 20, leadTimeDays: 7, isPreferred: true),
            new SupplierProduct(demoOrg.Id, supFluid.Id, prodValve.Id, "APX-V220", 210.00m, minimumOrderQuantity: 10, leadTimeDays: 5, isPreferred: true),
            new SupplierProduct(demoOrg.Id, supPrecision.Id, prodLightCurtain.Id, "NX-SAF900", 310.00m, minimumOrderQuantity: 5, leadTimeDays: 10, isPreferred: true));

        // 7. Seed Purchase Order & Receive Goods to establish audited Inventory Ledger
        var po = new PurchaseOrder(
            demoOrg.Id,
            "PO-DEMO-1001",
            supPrecision.Id,
            whEast.Id,
            DateTimeOffset.UtcNow.AddDays(-3),
            taxRatePercent: 0m,
            notes: "Initial stock replenishment for East Coast Distribution Center",
            createdByUserId: ownerUser.Id,
            lines:
            [
                (prodPlc.Id, OrderedQuantity: 45, UnitCost: 420.00m),
                (prodSensor.Id, OrderedQuantity: 100, UnitCost: 45.00m),
                (prodValve.Id, OrderedQuantity: 18, UnitCost: 210.00m),
                (prodLightCurtain.Id, OrderedQuantity: 8, UnitCost: 310.00m)
            ]);

        po.Submit();
        po.Approve(ownerUser.Id);

        var receipt = po.ReceiveGoods(
            "GRN-DEMO-1001",
            "Received full initial replenishment shipment",
            warehouseUser.Id,
            po.Lines.Select(l => (l.Id, l.OrderedQuantity)));

        dbContext.PurchaseOrders.Add(po);
        dbContext.GoodsReceipts.Add(receipt);

        var invPlcEast = new InventoryItem(demoOrg.Id, whEast.Id, prodPlc.Id);
        var invSensorEast = new InventoryItem(demoOrg.Id, whEast.Id, prodSensor.Id);
        var invValveEast = new InventoryItem(demoOrg.Id, whEast.Id, prodValve.Id);
        var invLightEast = new InventoryItem(demoOrg.Id, whEast.Id, prodLightCurtain.Id);

        dbContext.InventoryItems.AddRange(invPlcEast, invSensorEast, invValveEast, invLightEast);

        dbContext.StockMovements.AddRange(
            invPlcEast.ReceiveStock(45, 420.00m, "PurchaseOrder", po.OrderNumber, po.Id, "Initial PO receipt GRN-DEMO-1001", null, warehouseUser.Id),
            invSensorEast.ReceiveStock(100, 45.00m, "PurchaseOrder", po.OrderNumber, po.Id, "Initial PO receipt GRN-DEMO-1001", null, warehouseUser.Id),
            invValveEast.ReceiveStock(18, 210.00m, "PurchaseOrder", po.OrderNumber, po.Id, "Initial PO receipt GRN-DEMO-1001", null, warehouseUser.Id),
            invLightEast.ReceiveStock(8, 310.00m, "PurchaseOrder", po.OrderNumber, po.Id, "Initial PO receipt GRN-DEMO-1001", null, warehouseUser.Id));

        // 8. Seed a Confirmed Sales Order with Active Stock Reservations
        var salesOrder = new SalesOrder(
            demoOrg.Id,
            "SO-DEMO-2001",
            "Midwest Robotics Assembly Corp",
            "procurement@midwestrobotics.example",
            "+1-313-555-0188",
            "900 Automation Pkwy, Detroit, MI",
            whEast.Id,
            taxRatePercent: 8.25m,
            discountAmount: 0m,
            notes: "Scheduled Q4 production line upgrade order",
            createdByUserId: salesUser.Id,
            lines:
            [
                (prodPlc.Id, Quantity: 5, AuthoritativeUnitPrice: prodPlc.UnitPrice, UnitCostSnapshot: prodPlc.MovingAverageCost),
                (prodSensor.Id, Quantity: 20, AuthoritativeUnitPrice: prodSensor.UnitPrice, UnitCostSnapshot: prodSensor.MovingAverageCost)
            ]);

        salesOrder.Confirm(salesUser.Id);
        dbContext.SalesOrders.Add(salesOrder);

        var plcLine = salesOrder.Lines.First(l => l.ProductId == prodPlc.Id);
        var sensorLine = salesOrder.Lines.First(l => l.ProductId == prodSensor.Id);

        dbContext.StockReservations.AddRange(
            invPlcEast.ReserveStock(plcLine.Quantity, salesOrder.Id, plcLine.Id, salesOrder.OrderNumber),
            invSensorEast.ReserveStock(sensorLine.Quantity, salesOrder.Id, sensorLine.Id, salesOrder.OrderNumber));

        dbContext.RecordAudit(
            action: "DemoWorkspace.Seeded",
            entityType: nameof(Organization),
            entityId: demoOrg.Id.ToString(),
            summary: "Seeded demo organization 'Acme Industrial Supply [DEMO]' with catalog, warehouses, inventory ledger, and sample orders.",
            explicitTenantId: demoOrg.Id);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Demo workspace seeding completed successfully.");
    }
}
