# Automated Testing Guide

## 1. Test Suite Architecture

The solution includes four dedicated test projects under `tests/`:

1. **`Inventory.Domain.UnitTests`** (`tests/Inventory.Domain.UnitTests`)
   - Tests pure domain business rules and invariants:
     - `TenantEntityBase` non-empty `TenantId` enforcement.
     - `ApplicationUser` failed login lockout & `RefreshToken` rotation state.
     - `OrganizationRole` permission validation and `OrganizationMember` primary owner protection.
     - `Product` weighted moving-average cost calculation, reorder/target threshold validation, and archived product ordering blocks.
     - `InventoryItem` stock receipt, reservation (`Available = OnHand - Reserved`), reservation release, fulfillment, negative stock protection, and adjustment below reserved stock rejection.
     - `PurchaseOrder`, `SalesOrder`, and `StockTransfer` state machine transitions and server-side financial calculations.

2. **`Inventory.Application.UnitTests`** (`tests/Inventory.Application.UnitTests`)
   - Tests request pagination clamping (`MaxPageSize = 100`), FluentValidation validators across modules, and RBAC/PBAC role permission separation (`Owner`, `Administrator`, `Warehouse Manager`, `Inventory Clerk`, `Procurement Officer`, `Sales Representative`, `Auditor`).

3. **`Inventory.ArchitectureTests`** (`tests/Inventory.ArchitectureTests`)
   - Uses `NetArchTest.Rules` to verify Clean Architecture inward dependency rules:
     - `Inventory.Domain` has zero dependencies on `Application`, `Infrastructure`, `API`, `EntityFrameworkCore`, or `AspNetCore`.
     - `Inventory.Application` has zero dependencies on `Infrastructure` or `API`.
     - `Inventory.Infrastructure` has zero dependencies on `API`.
     - All controllers in `Inventory.API` return DTOs and never expose `EntityBase` domain entities directly.

4. **`Inventory.API.IntegrationTests`** (`tests/Inventory.API.IntegrationTests`)
   - End-to-end HTTP integration tests using `WebApplicationFactory<Program>`:
     - **Authentication & Refresh Token Reuse Detection:** Verifies that replaying a previously rotated refresh token revokes the entire token family immediately (`401 Unauthorized`).
     - **Multi-Tenant Isolation & IDOR Protection:** Provisions two independent organizations (`Tenant Alpha` and `Tenant Beta`) with separate users and verifies that cross-tenant list queries, direct ID manipulation (`GET`/`PUT`), and `X-Tenant-Id` header spoofing (`403 Forbidden`) are blocked.
     - **End-to-End Procurement, Reservation, Cancellation & Fulfillment:** Verifies partial and full PO goods receipts, SO stock reservations, reservation release on cancellation, stock deduction on fulfillment, and immutable `StockMovement` ledger conservation.
     - **Concurrent Stock Reservation Contention:** Fires `12` simultaneous `POST /api/v1/sales-orders/{id}/confirm` requests against a product with `5` available units; verifies that exactly `5` succeed (`200 OK`), `7` return `409 Conflict` (`Inventory.InsufficientStock`), and zero overselling occurs.
     - **Idempotency & Stock Transfers:** Verifies `Idempotency-Key` replay (`X-Idempotent-Replay: true`), payload mismatch rejection (`422 UnprocessableEntity`), and inter-warehouse stock transfer dispatch (`InTransit`) and receipt (`Received`).

---

## 2. Running the Tests

### Run All Tests
```bash
dotnet test Inventory.sln --configuration Release
```

### Run Against an Explicit PostgreSQL Instance
By default, `IntegrationTestWebAppFactory` checks `TEST_POSTGRES_CONNECTION`, then attempts to launch a `postgres:17-alpine` container via `Testcontainers.PostgreSql` (falling back to a relational WAL-mode SQLite database only if the local Docker daemon is offline). To target a specific PostgreSQL database:
```bash
export TEST_POSTGRES_CONNECTION="Host=localhost;Port=5432;Database=enterprise_inventory_test_db;Username=inventory_app;Password=CHANGE_ME_LOCAL_DEV_PASSWORD_ONLY"
dotnet test Inventory.sln --configuration Release
```
