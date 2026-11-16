namespace Inventory.Domain.Identity;

public static class Permissions
{
    public static class Organization
    {
        public const string Manage = "organization:manage";
        public const string MembersRead = "members:read";
        public const string MembersManage = "members:manage";
        public const string RolesManage = "roles:manage";
    }

    public static class Catalog
    {
        public const string Read = "catalog:read";
        public const string Write = "catalog:write";
    }

    public static class Warehouses
    {
        public const string Read = "warehouses:read";
        public const string Write = "warehouses:write";
    }

    public static class Inventory
    {
        public const string Read = "inventory:read";
        public const string Adjust = "inventory:adjust";
        public const string Reconcile = "inventory:reconcile";
    }

    public static class Transfers
    {
        public const string Create = "transfers:create";
        public const string Dispatch = "transfers:dispatch";
        public const string Receive = "transfers:receive";
    }

    public static class Suppliers
    {
        public const string Read = "suppliers:read";
        public const string Write = "suppliers:write";
    }

    public static class PurchaseOrders
    {
        public const string Read = "purchase-orders:read";
        public const string Write = "purchase-orders:write";
        public const string Approve = "purchase-orders:approve";
        public const string Receive = "purchase-orders:receive";
    }

    public static class SalesOrders
    {
        public const string Read = "sales-orders:read";
        public const string Write = "sales-orders:write";
        public const string Confirm = "sales-orders:confirm";
        public const string Fulfill = "sales-orders:fulfill";
        public const string Cancel = "sales-orders:cancel";
    }

    public static class Reports
    {
        public const string Read = "reports:read";
    }

    public static class AuditLogs
    {
        public const string Read = "audit-logs:read";
    }

    public static readonly IReadOnlyList<string> All =
    [
        Organization.Manage,
        Organization.MembersRead,
        Organization.MembersManage,
        Organization.RolesManage,
        Catalog.Read,
        Catalog.Write,
        Warehouses.Read,
        Warehouses.Write,
        Inventory.Read,
        Inventory.Adjust,
        Inventory.Reconcile,
        Transfers.Create,
        Transfers.Dispatch,
        Transfers.Receive,
        Suppliers.Read,
        Suppliers.Write,
        PurchaseOrders.Read,
        PurchaseOrders.Write,
        PurchaseOrders.Approve,
        PurchaseOrders.Receive,
        SalesOrders.Read,
        SalesOrders.Write,
        SalesOrders.Confirm,
        SalesOrders.Fulfill,
        SalesOrders.Cancel,
        Reports.Read,
        AuditLogs.Read
    ];

    public static class SystemRoleNames
    {
        public const string Owner = "Owner";
        public const string Administrator = "Administrator";
        public const string WarehouseManager = "Warehouse Manager";
        public const string InventoryClerk = "Inventory Clerk";
        public const string ProcurementOfficer = "Procurement Officer";
        public const string SalesRepresentative = "Sales Representative";
        public const string Auditor = "Auditor";
    }

    public static IReadOnlyDictionary<string, (string Description, IReadOnlyList<string> Permissions)> GetDefaultRoleDefinitions()
    {
        return new Dictionary<string, (string Description, IReadOnlyList<string> Permissions)>
        {
            [SystemRoleNames.Owner] = (
                "Full administrative control over the organization workspace, members, inventory, and orders.",
                All),

            [SystemRoleNames.Administrator] = (
                "Operational administrator with full access to workspace modules, members, and settings.",
                All),

            [SystemRoleNames.WarehouseManager] = (
                "Manages warehouses, bin locations, stock adjustments, reconciliations, receipts, fulfillments, and transfers.",
                [
                    Organization.MembersRead,
                    Catalog.Read,
                    Catalog.Write,
                    Warehouses.Read,
                    Warehouses.Write,
                    Inventory.Read,
                    Inventory.Adjust,
                    Inventory.Reconcile,
                    Transfers.Create,
                    Transfers.Dispatch,
                    Transfers.Receive,
                    Suppliers.Read,
                    PurchaseOrders.Read,
                    PurchaseOrders.Approve,
                    PurchaseOrders.Receive,
                    SalesOrders.Read,
                    SalesOrders.Confirm,
                    SalesOrders.Fulfill,
                    SalesOrders.Cancel,
                    Reports.Read
                ]),

            [SystemRoleNames.InventoryClerk] = (
                "Handles day-to-day warehouse stock counts, adjustments, goods receiving, order fulfillment, and transfers.",
                [
                    Catalog.Read,
                    Warehouses.Read,
                    Inventory.Read,
                    Inventory.Adjust,
                    Transfers.Create,
                    Transfers.Dispatch,
                    Transfers.Receive,
                    PurchaseOrders.Read,
                    PurchaseOrders.Receive,
                    SalesOrders.Read,
                    SalesOrders.Fulfill
                ]),

            [SystemRoleNames.ProcurementOfficer] = (
                "Manages suppliers, product sourcing catalog, and purchase orders.",
                [
                    Catalog.Read,
                    Catalog.Write,
                    Warehouses.Read,
                    Inventory.Read,
                    Suppliers.Read,
                    Suppliers.Write,
                    PurchaseOrders.Read,
                    PurchaseOrders.Write,
                    Reports.Read
                ]),

            [SystemRoleNames.SalesRepresentative] = (
                "Creates, confirms, and manages customer sales orders and views product availability.",
                [
                    Catalog.Read,
                    Warehouses.Read,
                    Inventory.Read,
                    SalesOrders.Read,
                    SalesOrders.Write,
                    SalesOrders.Confirm,
                    SalesOrders.Cancel,
                    Reports.Read
                ]),

            [SystemRoleNames.Auditor] = (
                "Read-only access to inventory, orders, reports, members, and compliance audit logs.",
                [
                    Organization.MembersRead,
                    Catalog.Read,
                    Warehouses.Read,
                    Inventory.Read,
                    Suppliers.Read,
                    PurchaseOrders.Read,
                    SalesOrders.Read,
                    Reports.Read,
                    AuditLogs.Read
                ])
        };
    }
}
