using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Inventory.Application.Auditing;
using Inventory.Application.Catalog;
using Inventory.Application.Common;
using Inventory.Application.Identity;
using Inventory.Application.Inventory;
using Inventory.Application.PurchaseOrders;
using Inventory.Application.Reporting;
using Inventory.Application.SalesOrders;
using Inventory.Application.StockTransfers;
using Inventory.Application.Suppliers;
using Inventory.Application.Warehouses;
using Inventory.Domain.Catalog;
using Inventory.Domain.Inventory;
using Inventory.Domain.PurchaseOrders;
using Inventory.Domain.SalesOrders;
using Inventory.Domain.StockTransfers;

namespace Inventory.API.IntegrationTests;

public sealed class EnterpriseIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IntegrationTestWebAppFactory _factory;

    public EnterpriseIntegrationTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Auth_RegisterLoginRefreshAndReuseDetection_ShouldRevokeCompromisedTokenFamily()
    {
        using var client = _factory.CreateClient();
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        // 1. Register user + initial organization
        var registerReq = new RegisterRequest(
            $"security.{uniqueSuffix}@acme.test",
            "Security Tester",
            "SecurePass123!",
            $"Acme Security {uniqueSuffix}",
            $"acme-sec-{uniqueSuffix}");

        var registerRes = await client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
        registerRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var initialAuth = await registerRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        initialAuth.Should().NotBeNull();
        initialAuth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        initialAuth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        initialAuth.ActiveTenantId.Should().NotBeNull();

        // 2. Rotate the refresh token once (legitimate rotation)
        var rotateRes = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(initialAuth.RefreshToken));

        rotateRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotatedAuth = await rotateRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        rotatedAuth.Should().NotBeNull();
        rotatedAuth!.RefreshToken.Should().NotBe(initialAuth.RefreshToken);

        // 3. Replay the OLD already-rotated refresh token -> must trigger token family reuse revocation!
        var replayRes = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(initialAuth.RefreshToken));

        replayRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Verify that the second (rotated) token in the same family was also revoked for security
        var compromisedFamilyRes = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(rotatedAuth.RefreshToken));

        compromisedFamilyRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MultiTenancy_TwoOrganizations_ShouldStrictlyIsolateResourcesAndBlockIdorAndHeaderSpoofing()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // Provision Tenant A & User A
        var (clientA, authA) = await CreateAuthenticatedTenantClientAsync($"alpha.{suffix}@tenant-a.test", $"Tenant Alpha {suffix}");

        // Provision Tenant B & User B
        var (clientB, authB) = await CreateAuthenticatedTenantClientAsync($"beta.{suffix}@tenant-b.test", $"Tenant Beta {suffix}");

        // Tenant A creates a Category and Product
        var catResA = await clientA.PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Precision Robotics", "precision-robotics", null));
        catResA.StatusCode.Should().Be(HttpStatusCode.Created);
        var categoryA = (await catResA.Content.ReadFromJsonAsync<CategoryDto>(JsonOptions))!;

        var prodResA = await clientA.PostAsJsonAsync("/api/v1/products", new CreateProductRequest(
            categoryA.Id,
            "ALPHA-SECRET-01",
            "Classified Servo Motor",
            "Confidential Tenant A product",
            null,
            "EA",
            StandardCost: 200m,
            UnitPrice: 350m,
            ReorderPoint: 5,
            TargetStockLevel: 25));
        prodResA.StatusCode.Should().Be(HttpStatusCode.Created);
        var productA = (await prodResA.Content.ReadFromJsonAsync<ProductDto>(JsonOptions))!;

        // 1. User B queries /api/v1/products -> must NOT see Tenant A's product
        var listResB = await clientB.GetAsync("/api/v1/products");
        listResB.StatusCode.Should().Be(HttpStatusCode.OK);
        var pageB = (await listResB.Content.ReadFromJsonAsync<PagedResponse<ProductDto>>(JsonOptions))!;
        pageB.Items.Should().NotContain(p => p.Id == productA.Id);

        // 2. User B attempts IDOR GET /api/v1/products/{productA.Id} -> must receive 404 NotFound
        var idorGetRes = await clientB.GetAsync($"/api/v1/products/{productA.Id}");
        idorGetRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. User B attempts IDOR PUT /api/v1/products/{productA.Id} -> must receive 404 NotFound
        var idorPutRes = await clientB.PutAsJsonAsync($"/api/v1/products/{productA.Id}", new UpdateProductRequest(
            categoryA.Id,
            "HIJACKED",
            "Hijacked Name",
            null,
            null,
            "EA",
            1m,
            1m,
            1,
            5,
            ProductStatus.Active));
        idorPutRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 4. User B attempts to spoof X-Tenant-Id header with Tenant A's Organization ID -> must receive 403 Forbidden
        using var spoofClient = _factory.CreateClient();
        spoofClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);
        spoofClient.DefaultRequestHeaders.Add("X-Tenant-Id", authA.ActiveTenantId!.Value.ToString());

        var spoofRes = await spoofClient.GetAsync("/api/v1/products");
        spoofRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EndToEnd_PurchaseOrderReceiving_SalesOrderReservation_CancellationAndFulfillment_ShouldMaintainExactLedgerConsistency()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await CreateAuthenticatedTenantClientAsync($"ops.{suffix}@enterprise.test", $"Ops Corp {suffix}");

        // 1. Create Category, Product, Supplier, and Warehouse
        var category = await PostAndReadAsync<CategoryDto>(
            client,
            "/api/v1/categories",
            new CreateCategoryRequest("Industrial Valves", $"valves-{suffix}", null));

        var product = await PostAndReadAsync<ProductDto>(
            client,
            "/api/v1/products",
            new CreateProductRequest(
                category.Id,
                $"VLV-{suffix}",
                "High Pressure Control Valve",
                null,
                null,
                "EA",
                StandardCost: 100.00m,
                UnitPrice: 180.00m,
                ReorderPoint: 10,
                TargetStockLevel: 50));

        var supplier = await PostAndReadAsync<SupplierDto>(
            client,
            "/api/v1/suppliers",
            new CreateSupplierRequest($"SUP-{suffix}", "Precision Hydraulics Ltd", "John Doe", "john@hydraulics.test", null, null, "Boston", "USA"));

        var warehouse = await PostAndReadAsync<WarehouseDto>(
            client,
            "/api/v1/warehouses",
            new CreateWarehouseRequest($"WH-{suffix}", "Primary East Warehouse", "100 Dock St", "Boston", "MA", "02110", "USA", null, IsDefault: true));

        // 2. Create, Submit, Approve, and Partially + Fully Receive a Purchase Order for 30 units @ $100.00
        var po = await PostAndReadAsync<PurchaseOrderDto>(
            client,
            "/api/v1/purchase-orders",
            new CreatePurchaseOrderRequest(
                supplier.Id,
                warehouse.Id,
                DateTimeOffset.UtcNow.AddDays(7),
                TaxRatePercent: 0m,
                Notes: "Initial PO",
                Lines: [new CreatePurchaseOrderLineRequest(product.Id, OrderedQuantity: 30, UnitCost: 100.00m)]));

        var submitRes = await client.PostAsync($"/api/v1/purchase-orders/{po.Id}/submit", null);
        submitRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var approveRes = await client.PostAsync($"/api/v1/purchase-orders/{po.Id}/approve", null);
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var poLineId = po.Lines.Single().Id;

        // Partial receipt of 20 units
        var partialReceiveRes = await client.PostAsJsonAsync(
            $"/api/v1/purchase-orders/{po.Id}/receive",
            new ReceivePurchaseOrderRequest("First batch", [new ReceivePurchaseOrderLineRequest(poLineId, 20)]));
        partialReceiveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var partialPo = (await partialReceiveRes.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions))!;
        partialPo.Status.Should().Be(PurchaseOrderStatus.PartiallyReceived);

        // Final receipt of remaining 10 units
        var finalReceiveRes = await client.PostAsJsonAsync(
            $"/api/v1/purchase-orders/{po.Id}/receive",
            new ReceivePurchaseOrderRequest("Second batch", [new ReceivePurchaseOrderLineRequest(poLineId, 10)]));
        finalReceiveRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedPo = (await finalReceiveRes.Content.ReadFromJsonAsync<PurchaseOrderDto>(JsonOptions))!;
        completedPo.Status.Should().Be(PurchaseOrderStatus.Received);

        // Verify inventory balance is OnHand=30, Reserved=0, Available=30
        var balanceAfterPo = await GetSingleBalanceAsync(client, warehouse.Id, product.Id);
        balanceAfterPo.OnHandQuantity.Should().Be(30);
        balanceAfterPo.ReservedQuantity.Should().Be(0);
        balanceAfterPo.AvailableQuantity.Should().Be(30);

        // 3. Create & Confirm Sales Order #1 for 12 units -> reserves 12 units
        var so1 = await PostAndReadAsync<SalesOrderDto>(
            client,
            "/api/v1/sales-orders",
            new CreateSalesOrderRequest(
                "Apex Assembly Inc",
                "orders@apex.test",
                null,
                "500 Factory Rd",
                warehouse.Id,
                TaxRatePercent: 10m,
                DiscountAmount: 0m,
                Notes: "Order #1",
                ConfirmImmediately: true,
                Lines: [new CreateSalesOrderLineRequest(product.Id, Quantity: 12)]));

        so1.Status.Should().Be(SalesOrderStatus.Confirmed);
        // Server-side calculation check: 12 * $180 = $2,160 + 10% tax ($216) = $2,376.00
        so1.SubtotalAmount.Should().Be(2160.00m);
        so1.TaxAmount.Should().Be(216.00m);
        so1.TotalAmount.Should().Be(2376.00m);

        var balanceAfterReserve = await GetSingleBalanceAsync(client, warehouse.Id, product.Id);
        balanceAfterReserve.OnHandQuantity.Should().Be(30);
        balanceAfterReserve.ReservedQuantity.Should().Be(12);
        balanceAfterReserve.AvailableQuantity.Should().Be(18);

        // 4. Cancel Sales Order #1 -> must atomically release the 12 reserved units
        var cancelRes = await client.PostAsJsonAsync(
            $"/api/v1/sales-orders/{so1.Id}/cancel",
            new CancelSalesOrderRequest("Customer revised quantity"));
        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var balanceAfterCancel = await GetSingleBalanceAsync(client, warehouse.Id, product.Id);
        balanceAfterCancel.OnHandQuantity.Should().Be(30);
        balanceAfterCancel.ReservedQuantity.Should().Be(0);
        balanceAfterCancel.AvailableQuantity.Should().Be(30);

        // 5. Create, Confirm, Process, and Fulfill Sales Order #2 for 8 units
        var so2 = await PostAndReadAsync<SalesOrderDto>(
            client,
            "/api/v1/sales-orders",
            new CreateSalesOrderRequest(
                "Apex Assembly Inc",
                "orders@apex.test",
                null,
                "500 Factory Rd",
                warehouse.Id,
                TaxRatePercent: 0m,
                DiscountAmount: 0m,
                Notes: "Order #2",
                ConfirmImmediately: true,
                Lines: [new CreateSalesOrderLineRequest(product.Id, Quantity: 8)]));

        var processRes = await client.PostAsync($"/api/v1/sales-orders/{so2.Id}/process", null);
        processRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var fulfillRes = await client.PostAsync($"/api/v1/sales-orders/{so2.Id}/fulfill", null);
        fulfillRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var fulfilledSo = (await fulfillRes.Content.ReadFromJsonAsync<SalesOrderDto>(JsonOptions))!;
        fulfilledSo.Status.Should().Be(SalesOrderStatus.Fulfilled);

        // Final stock must be OnHand=22, Reserved=0, Available=22
        var finalBalance = await GetSingleBalanceAsync(client, warehouse.Id, product.Id);
        finalBalance.OnHandQuantity.Should().Be(22);
        finalBalance.ReservedQuantity.Should().Be(0);
        finalBalance.AvailableQuantity.Should().Be(22);

        // Verify ledger movements (+20, +10, -8 = +22)
        var movementsRes = await client.GetFromJsonAsync<PagedResponse<StockMovementDto>>(
            $"/api/v1/inventory/movements?warehouseId={warehouse.Id}&productId={product.Id}",
            JsonOptions);
        movementsRes!.Items.Should().HaveCount(3);
        movementsRes.Items.Sum(m => m.QuantityDelta).Should().Be(22);
    }

    [Fact]
    public async Task Concurrency_SimultaneousOrderConfirmationsForLimitedStock_ShouldPreventOverselling()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, auth) = await CreateAuthenticatedTenantClientAsync($"concurrency.{suffix}@test.local", $"Concurrency Corp {suffix}");

        var category = await PostAndReadAsync<CategoryDto>(
            client,
            "/api/v1/categories",
            new CreateCategoryRequest("Semiconductors", $"semi-{suffix}", null));

        var product = await PostAndReadAsync<ProductDto>(
            client,
            "/api/v1/products",
            new CreateProductRequest(
                category.Id,
                $"GPU-{suffix}",
                "Enterprise AI Accelerator",
                null,
                null,
                "EA",
                StandardCost: 1000m,
                UnitPrice: 1500m,
                ReorderPoint: 2,
                TargetStockLevel: 20));

        var warehouse = await PostAndReadAsync<WarehouseDto>(
            client,
            "/api/v1/warehouses",
            new CreateWarehouseRequest($"WH-CONC-{suffix}", "Concurrency Hub", null, null, null, null, null));

        // Seed exactly 5 units in stock via controlled adjustment
        var adjustRes = await client.PostAsJsonAsync(
            "/api/v1/inventory/adjustments",
            new AdjustInventoryRequest(warehouse.Id, product.Id, QuantityDelta: 5, Reason: "Initial concurrency seed"));
        adjustRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Create 12 separate Pending (unconfirmed) Sales Orders, each requesting 1 unit of the 5 available units
        var pendingOrderIds = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            var order = await PostAndReadAsync<SalesOrderDto>(
                client,
                "/api/v1/sales-orders",
                new CreateSalesOrderRequest(
                    $"Concurrent Buyer #{i + 1}",
                    null,
                    null,
                    null,
                    warehouse.Id,
                    TaxRatePercent: 0m,
                    DiscountAmount: 0m,
                    Notes: null,
                    ConfirmImmediately: false,
                    Lines: [new CreateSalesOrderLineRequest(product.Id, Quantity: 1)]));

            pendingOrderIds.Add(order.Id);
        }

        // Fire all 12 ConfirmSalesOrder requests simultaneously in parallel
        var tasks = pendingOrderIds.Select(async orderId =>
        {
            using var parallelClient = _factory.CreateClient();
            parallelClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return await parallelClient.PostAsync($"/api/v1/sales-orders/{orderId}/confirm", null);
        });

        var responses = await Task.WhenAll(tasks);

        var succeededCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Exactly 5 orders must succeed, and 7 must fail with 409 Conflict (InsufficientStock)
        succeededCount.Should().Be(5);
        conflictCount.Should().Be(7);

        var finalBalance = await GetSingleBalanceAsync(client, warehouse.Id, product.Id);
        finalBalance.OnHandQuantity.Should().Be(5);
        finalBalance.ReservedQuantity.Should().Be(5);
        finalBalance.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Idempotency_RepeatedAdjustmentsAndStockTransferLifecycle_ShouldPreventDuplicateMutations()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (client, _) = await CreateAuthenticatedTenantClientAsync($"transfer.{suffix}@test.local", $"Transfer Corp {suffix}");

        var category = await PostAndReadAsync<CategoryDto>(
            client,
            "/api/v1/categories",
            new CreateCategoryRequest("Motors", $"motors-{suffix}", null));

        var product = await PostAndReadAsync<ProductDto>(
            client,
            "/api/v1/products",
            new CreateProductRequest(
                category.Id,
                $"MTR-{suffix}",
                "Brushless Servo Motor",
                null,
                null,
                "EA",
                StandardCost: 80m,
                UnitPrice: 140m,
                ReorderPoint: 5,
                TargetStockLevel: 40));

        var whSource = await PostAndReadAsync<WarehouseDto>(
            client,
            "/api/v1/warehouses",
            new CreateWarehouseRequest($"WH-SRC-{suffix}", "Source Warehouse", null, null, null, null, null));

        var whDest = await PostAndReadAsync<WarehouseDto>(
            client,
            "/api/v1/warehouses",
            new CreateWarehouseRequest($"WH-DST-{suffix}", "Destination Warehouse", null, null, null, null, null));

        // 1. Perform idempotent stock adjustment (+25 units) with Idempotency-Key
        var idempotencyKey = $"idem-adj-{Guid.NewGuid():N}";
        var payload = new AdjustInventoryRequest(whSource.Id, product.Id, QuantityDelta: 25, Reason: "Cycle count seed");

        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/inventory/adjustments")
        {
            Content = JsonContent.Create(payload)
        };
        req1.Headers.Add("Idempotency-Key", idempotencyKey);
        var res1 = await client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Replay the exact same request with the same Idempotency-Key
        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/inventory/adjustments")
        {
            Content = JsonContent.Create(payload)
        };
        req2.Headers.Add("Idempotency-Key", idempotencyKey);
        var res2 = await client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        res2.Headers.Contains("X-Idempotent-Replay").Should().BeTrue();

        // Balance must be 25, NOT 50!
        var sourceBalance = await GetSingleBalanceAsync(client, whSource.Id, product.Id);
        sourceBalance.OnHandQuantity.Should().Be(25);

        // Reusing the same Idempotency-Key with a DIFFERENT payload must return 422 UnprocessableEntity
        using var reqMismatch = new HttpRequestMessage(HttpMethod.Post, "/api/v1/inventory/adjustments")
        {
            Content = JsonContent.Create(new AdjustInventoryRequest(whSource.Id, product.Id, QuantityDelta: 99, Reason: "Different payload"))
        };
        reqMismatch.Headers.Add("Idempotency-Key", idempotencyKey);
        var resMismatch = await client.SendAsync(reqMismatch);
        resMismatch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // 2. Inter-Warehouse Stock Transfer of 10 units from Source to Destination
        var transfer = await PostAndReadAsync<StockTransferDto>(
            client,
            "/api/v1/stock-transfers",
            new CreateStockTransferRequest(
                whSource.Id,
                whDest.Id,
                "Transfer 10 motors to destination warehouse",
                [new CreateStockTransferLineRequest(product.Id, Quantity: 10)]));

        transfer.Status.Should().Be(TransferStatus.Requested);

        // Dispatch Transfer -> Source OnHand becomes 15, Destination InTransit becomes 10
        var dispatchRes = await client.PostAsync($"/api/v1/stock-transfers/{transfer.Id}/dispatch", null);
        dispatchRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var sourceAfterDispatch = await GetSingleBalanceAsync(client, whSource.Id, product.Id);
        var destAfterDispatch = await GetSingleBalanceAsync(client, whDest.Id, product.Id);
        sourceAfterDispatch.OnHandQuantity.Should().Be(15);
        destAfterDispatch.OnHandQuantity.Should().Be(0);
        destAfterDispatch.InTransitQuantity.Should().Be(10);

        // Receive Transfer -> Destination InTransit becomes 0, Destination OnHand becomes 10
        var receiveRes = await client.PostAsync($"/api/v1/stock-transfers/{transfer.Id}/receive", null);
        receiveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var destAfterReceive = await GetSingleBalanceAsync(client, whDest.Id, product.Id);
        destAfterReceive.OnHandQuantity.Should().Be(10);
        destAfterReceive.InTransitQuantity.Should().Be(0);
        destAfterReceive.AvailableQuantity.Should().Be(10);

        // 3. Verify Reporting & Audit Logs reflect all operations for this tenant
        var dashboardRes = await client.GetFromJsonAsync<DashboardSummaryDto>("/api/v1/reports/dashboard-summary", JsonOptions);
        dashboardRes.Should().NotBeNull();
        dashboardRes!.TotalOnHandUnits.Should().Be(25); // 15 in Source + 10 in Destination

        var auditRes = await client.GetFromJsonAsync<PagedResponse<AuditLogDto>>("/api/v1/audit-logs", JsonOptions);
        auditRes.Should().NotBeNull();
        auditRes!.TotalCount.Should().BeGreaterThanOrEqualTo(6);
    }

    private async Task<(HttpClient Client, AuthResponse Auth)> CreateAuthenticatedTenantClientAsync(string email, string organizationName)
    {
        var client = _factory.CreateClient();
        var registerRes = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "Integration User", "StrongPass123!", organizationName));

        registerRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var auth = (await registerRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task<T> PostAndReadAsync<T>(HttpClient client, string url, object payload)
    {
        var response = await client.PostAsJsonAsync(url, payload);
        response.IsSuccessStatusCode.Should().BeTrue(
            because: $"POST {url} should succeed, but returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private static async Task<InventoryBalanceDto> GetSingleBalanceAsync(HttpClient client, Guid warehouseId, Guid productId)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<InventoryBalanceDto>>(
            $"/api/v1/inventory?warehouseId={warehouseId}&productId={productId}",
            JsonOptions);

        page.Should().NotBeNull();
        page!.Items.Should().ContainSingle();
        return page.Items[0];
    }
}
