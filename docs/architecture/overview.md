# Architecture Overview & 4+1 Views

## 1. System Vision & Architectural Style

The **Enterprise Inventory & Order Management SaaS** is a multi-tenant B2B platform engineered using **Clean Architecture**, **Modular Monolith**, and **Domain-Driven Design (DDD)** principles.

### Core Architectural Tenets
1. **Backend Authority:** The frontend is strictly a presentation client. All stock availability checks, price/total calculations, state transitions, and permission checks occur on the backend.
2. **Inward Dependency Flow:**
   - `Inventory.Domain` has zero external infrastructure dependencies.
   - `Inventory.Application` depends only on `Inventory.Domain`.
   - `Inventory.Infrastructure` implements persistence, identity, tenancy, and background jobs, depending on `Application` and `Domain`.
   - `Inventory.API` serves as the HTTP entry point and Dependency Injection composition root.
3. **Transactional Integrity:** Every stock change is executed atomically with an immutable `StockMovement` ledger record inside a PostgreSQL transaction.

---

## 2. C4 Architecture Diagrams

### C4 Level 1 — System Context

```mermaid
flowchart TB
    TenantUser["Tenant Business User<br/>(Owner, Admin, Warehouse Manager, Clerk, Procurement, Sales, Auditor)"]
    PlatformAdmin["Platform Administrator<br/>(SaaS Operations)"]
    SaaSSystem["Enterprise Inventory & Order Management SaaS"]

    TenantUser -->|"Manages catalog, warehouses, inventory, POs, SOs, transfers & analytics via HTTPS"| SaaSSystem
    PlatformAdmin -->|"Monitors platform health & tenant governance via HTTPS"| SaaSSystem
```

### C4 Level 2 — Container Diagram

```mermaid
flowchart TB
    Browser["Web Browser"]

    subgraph SystemBoundary["Enterprise Inventory & Order Management Platform"]
        Web["Next.js Web Dashboard<br/>(TypeScript, Tailwind CSS, shadcn/ui)"]
        Api["ASP.NET Core Web API<br/>(Clean Architecture Modular Monolith)"]
        Workers["Hosted Background Services<br/>(Low-Stock Monitor & Ledger Reconciliation)"]
        Db[("PostgreSQL 17 Database<br/>(Shared Schema Multi-Tenant Storage)")]
    end

    Browser -->|"HTTPS :3000"| Web
    Browser -->|"HTTPS :8080/api/v1"| Api
    Web -->|"REST JSON"| Api
    Api -->|"EF Core / Npgsql (ACID)"| Db
    Workers -->|"Periodic Tenant-Scoped Checks"| Db
```

---

## 3. 4+1 Architectural View Model

### 3.1 Logical View
The platform is partitioned into 11 cohesive modules:
1. **Identity & Access Management (`Identity`)**: Users, JWT generation, Refresh Token rotation & reuse detection, Role/Permission claims.
2. **Organization Management (`Organizations`)**: Multi-tenant workspaces, members, invitations, roles, workspace settings.
3. **Product Management (`Catalog`)**: Categories, Products, SKUs, pricing, reorder thresholds.
4. **Warehouse Management (`Warehouses`)**: Warehouses, addresses, internal bin locations.
5. **Inventory Management (`Inventory`)**: Materialized balances (`InventoryItem`), immutable ledger (`StockMovement`), active reservations (`StockReservation`), and cycle-count reconciliation (`InventoryReconciliation`).
6. **Supplier Management (`Suppliers`)**: Suppliers, contacts, and supplier-product catalog links.
7. **Purchase Order Management (`PurchaseOrders`)**: Procurement lifecycle and partial/full goods receipts (`GoodsReceipt`).
8. **Sales Order Management (`SalesOrders`)**: Customer order lifecycle, stock reservation, fulfillment, and cancellation release.
9. **Stock Transfer Management (`StockTransfers`)**: Multi-step inter-warehouse stock transfers (`Draft` -> `Requested` -> `InTransit` -> `Received`/`Cancelled`).
10. **Reporting & Analytics (`Reporting`)**: Valuation, low-stock alerts, movement history, warehouse performance, and order statistics.
11. **Audit & Activity Logs (`Auditing`)**: Append-only `AuditLog` and transactional `IdempotencyRecord`.

### 3.2 Development View (Clean Architecture Dependency Flow)

```mermaid
flowchart LR
    API["Inventory.API<br/>(Controllers, Middleware, Filters, Program.cs)"]
    INFRA["Inventory.Infrastructure<br/>(EF Core DbContext, Interceptors, Auth, Jobs)"]
    APP["Inventory.Application<br/>(Feature Services, DTOs, Validators, Interfaces)"]
    DOM["Inventory.Domain<br/>(Core Entities, Value Objects, Enums, Exceptions)"]

    API --> INFRA
    API --> APP
    INFRA --> APP
    INFRA --> DOM
    APP --> DOM
```

```text
src/
├── Inventory.Domain/          # Core Entities, Value Objects, Enums, Domain Exceptions
├── Inventory.Application/     # Feature Services, DTOs, Validators, Interfaces
├── Inventory.Infrastructure/  # EF Core DbContext, Configurations, Interceptors, Auth, Jobs
└── Inventory.API/             # REST Controllers, Middleware, Filters, Program.cs
frontend/
└── inventory-web/             # Next.js App Router Frontend
tests/
├── Inventory.Domain.UnitTests/
├── Inventory.Application.UnitTests/
├── Inventory.API.IntegrationTests/
└── Inventory.ArchitectureTests/
```

### 3.3 Process View (Domain State Machines)

#### Sales Order Lifecycle
```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Pending : Submit
    Pending --> Confirmed : Confirm (Reserves Stock)
    Confirmed --> Processing : Begin Picking
    Processing --> Fulfilled : Fulfill (Deducts Stock & Writes Ledger)
    Pending --> Cancelled : Cancel
    Confirmed --> Cancelled : Cancel (Releases Reservation)
    Processing --> Cancelled : Cancel (Releases Reservation)
```

#### Purchase Order Lifecycle
```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Submitted : Submit
    Submitted --> Approved : Approve
    Approved --> PartiallyReceived : Partial GRN (Updates Moving Avg Cost)
    PartiallyReceived --> PartiallyReceived : Partial GRN
    Approved --> Received : Full GRN (Increments Stock & Writes Ledger)
    PartiallyReceived --> Received : Final GRN
    Draft --> Cancelled : Cancel
    Submitted --> Cancelled : Cancel
    Approved --> Cancelled : Cancel
```

#### Stock Transfer Lifecycle
```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Requested : Request Transfer
    Requested --> InTransit : Dispatch (Deducts Source OnHand -> InTransit)
    InTransit --> Received : Receive (Increments Dest OnHand & Writes Ledger)
    Draft --> Cancelled : Cancel
    Requested --> Cancelled : Cancel
```

### 3.4 Physical View
- Deployed as multi-stage Docker containers (`inventory-db`, `inventory-api`, `inventory-web`) connected via an internal Docker bridge network with persistent volume storage for PostgreSQL and health check orchestration.

### 3.5 +1 Scenario View
Critical runtime scenarios (Organization Onboarding, Token Rotation, Product Creation, Goods Receipt, Sales Order Creation, Stock Reservation, Concurrent Reservation Contention, Order Cancellation, and Warehouse Transfer) are enforced through explicit database transactions and verified by integration tests in `Inventory.API.IntegrationTests`.
